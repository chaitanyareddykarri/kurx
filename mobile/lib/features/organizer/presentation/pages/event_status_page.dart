import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/organizer_providers.dart';

class EventStatusPage extends ConsumerWidget {
  const EventStatusPage(
      {super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final param = eventId;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Event Status')),
      body: AsyncValueView(
        value: ref.watch(eventManageDetailProvider(param)),
        onRetry: () => ref.invalidate(eventManageDetailProvider(param)),
        data: (event) {
          final steps = [
            _Step('Draft', 'Event created and being set up', true),
            _Step('Under Review', 'Submitted for review', false),
            _Step('Published', 'Visible to public', false),
            _Step('Live', 'Event is happening now', false),
            _Step('Closed', 'Event ended', false),
          ];

          final currentIdx =
              _statusIndex(event.status).clamp(0, steps.length - 1);
          for (var i = 0; i <= currentIdx; i++) {
            steps[i] = _Step(steps[i].label, steps[i].desc, true);
          }

          return SingleChildScrollView(
            padding: const EdgeInsets.all(KSpace.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Current status banner
                DecoratedBox(
                  decoration: BoxDecoration(
                    color: c.accent.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(KRadius.lg),
                    border: Border.all(
                        color: c.accent.withValues(alpha: 0.25)),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(KSpace.lg),
                    child: Row(
                      children: [
                        Icon(Icons.info_outline_rounded,
                            color: c.accent, size: 22),
                        const SizedBox(width: KSpace.md),
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Current status',
                              style: TextStyle(
                                  color: c.muted, fontSize: 12),
                            ),
                            Text(
                              event.status.toUpperCase(),
                              style: TextStyle(
                                color: c.accent,
                                fontWeight: FontWeight.w900,
                                fontSize: 16,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: KSpace.xl),
                // Stepper
                for (var i = 0; i < steps.length; i++)
                  _StepRow(
                    step: steps[i],
                    isCurrent: i == currentIdx,
                    isLast: i == steps.length - 1,
                  ),
                const SizedBox(height: KSpace.xl),
                // Lifecycle actions, driven by the server's own workflow
                // (`EventStatusWorkflow`): only transitions valid *from the current status* are
                // offered, so the app never presents a button the backend will reject with
                // `invalid_transition`.
                ..._actionsFor(event.status).map(
                  (a) => Padding(
                    padding: const EdgeInsets.only(bottom: KSpace.md),
                    child: KurxButton(
                      label: a.label,
                      variant: a.destructive
                          ? KurxButtonVariant.secondary
                          : KurxButtonVariant.primary,
                      expand: true,
                      onPressed: () => _transition(context, ref, a),
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  int _statusIndex(String s) => switch (s) {
        'draft' => 0,
        // D-266 M4 states, serialized as the lowercased enum name — no underscore. `under_review`
        // never matched anything the API returns and silently fell through to the Draft step.
        'pendingreview' || 'underreview' || 'changesrequested' || 'approved' => 1,
        'published' => 2,
        'live' => 3,
        'closed' => 4,
        _ => 0,
      };

  /// Mirrors `EventStatusWorkflow.Actions` — only transitions the server declares valid from the
  /// current status. Kept in lifecycle order so the primary next step reads first.
  ///
  /// `publish` (Draft → Published directly) is offered alongside `submit_review`, because the
  /// backend allows both and which one succeeds depends on whether the event is paid: a paid event
  /// fails `submit_review` fast unless the organiser is paid-verified. The server decides; the app
  /// surfaces the refusal rather than pre-judging it.
  List<_LifecycleAction> _actionsFor(String status) => switch (status) {
        'draft' => const [
            _LifecycleAction('submit_review', 'Submit for review'),
            _LifecycleAction('publish', 'Publish now'),
            _LifecycleAction('cancel', 'Cancel event', destructive: true),
          ],
        // Submitted, not yet claimed: the organiser can still pull it back out of the queue.
        'pendingreview' => const [
            _LifecycleAction('withdraw', 'Withdraw from review'),
            _LifecycleAction('cancel', 'Cancel event', destructive: true),
          ],
        // Claimed by a reviewer — withdrawing would discard their in-flight review, so it is not offered.
        'underreview' => const [
            _LifecycleAction('cancel', 'Cancel event', destructive: true),
          ],
        // No `cancel` on these three: EventStatusWorkflow's cancel table does not accept them, and this
        // list only offers transitions the server declares valid.
        'changesrequested' || 'rejected' => const [
            _LifecycleAction('submit_for_review', 'Resubmit for review'),
          ],
        'approved' => const [
            _LifecycleAction('publish_approved', 'Publish'),
          ],
        'scheduled' => const [
            _LifecycleAction('open_registration', 'Open registration'),
            _LifecycleAction('unpublish', 'Back to draft', destructive: true),
          ],
        'published' => const [
            _LifecycleAction('go_live', 'Go live'),
            _LifecycleAction('close', 'Close event', destructive: true),
            _LifecycleAction('unpublish', 'Unpublish', destructive: true),
          ],
        'live' => const [_LifecycleAction('complete', 'Mark completed')],
        'closed' || 'completed' || 'cancelled' => const [
            _LifecycleAction('archive', 'Archive', destructive: true),
          ],
        _ => const [],
      };

  Future<void> _transition(
    BuildContext context,
    WidgetRef ref,
    _LifecycleAction action,
  ) async {
    if (action.destructive) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text(action.label),
          content: Text('${action.label} for this event? '
              'Cancelling and archiving cannot be undone.'),
          actions: [
            TextButton(
                onPressed: () => Navigator.of(ctx).pop(false), child: const Text('Back')),
            FilledButton(
                onPressed: () => Navigator.of(ctx).pop(true), child: Text(action.label)),
          ],
        ),
      );
      if (confirmed != true || !context.mounted) return;
    }

    try {
      await ref.read(eventManageSourceProvider).transitionEvent(orgId, eventId, action.action);
      ref.invalidate(eventManageDetailProvider(eventId));
      ref.invalidate(myEventsProvider);
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('${action.label} — done.')));
      }
    } on ApiError catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(_explain(e))));
    }
  }

  /// The transition gates are the whole point of this screen, so their codes get real sentences —
  /// "invalid_transition" tells an organiser nothing about what to fix.
  static String _explain(ApiError e) => switch (e.code) {
        'invalid_transition' => 'That step is not available from the current status.',
        'forbidden' => 'Only an Owner or Manager can change the event status.',
        'org_not_verified' =>
          'Your organisation must be verified before this event can be published.',
        // Was 'organizer_not_paid_verified' — a transposition the backend has never emitted, so this
        // sentence never once reached an organiser. Same defect as the 'weak_password' key documented
        // in api_error.dart. The emitted code is organizer_not_verified_for_paid (PaidOrganizerGateAsync).
        'organizer_not_verified_for_paid' =>
          'Paid events need your identity and bank details verified before they can go live.',
        'no_ticket_types' => 'Add at least one ticket type before publishing.',
        'event_already_started' =>
          'This event has already started — ask an admin to cancel it.',
        'approval_required' => 'This event is waiting on an approval chain.',
        // D-266 M5/M7 policy gates. Filing an authorization means uploading a letter on the
        // institution's letterhead, which this app has no screen for — so these say where the work
        // actually happens. A gate the organiser cannot act on is worse than one that is refused.
        'event_authorization_required' =>
          'This event needs written authorization from the organisation it represents. '
              'File it on the Kurx website, under the event’s Readiness tab.',
        'representation_required' =>
          'This kind of public event has to be run on behalf of an organisation. '
              'Set that on the Kurx website, under the event’s Readiness tab.',
        'financial_review_required' =>
          'This event needs a financial review before it can be published. '
              'A reviewer starts that once you submit the event for review.',
        'checklist_incomplete' =>
          'A reviewer has not finished the approval checklist for this event yet.',
        // The event moved between this screen loading and the tap — usually a reviewer acting at the same
        // moment. Pull to refresh rather than retrying against a state that no longer exists.
        'transition_conflict' =>
          'Someone else just changed this event’s status. Pull down to refresh, then try again.',
        // The publish readiness gates. An online event used to be refused `missing_venue` and could
        // never publish at all, so this pair is new copy for a path that previously dead-ended (D-299).
        'missing_description' => 'Add a description before publishing.',
        'missing_venue' => 'Add a venue before publishing.',
        'missing_online_url' =>
          'Add a joining link before publishing — an online event needs somewhere to go.',
        'missing_venue_or_url' =>
          'Add a venue or a joining link before publishing.',
        // A paid event can never self-publish (EventService: paid_event_requires_review). Saying so
        // points at the action that works instead of reporting a dead end.
        'paid_event_requires_review' =>
          'Paid events are reviewed before they go live. Submit it for review instead of publishing.',
        'event_under_review' =>
          "A reviewer has this event right now, so it can't be edited. You'll get it back with their notes.",
        'not_draft' => 'This event has moved past draft, so that step no longer applies.',
        'event_archived' => 'This event is archived. It has to be unarchived before anything else.',
        'pending_org_verification' =>
          "Your organisation's verification is still being reviewed. You can publish once it is approved.",
        // Derived live, never stored: the org has nobody verified to represent it right now.
        'representation_vacant' =>
          'This organisation has no verified representative at the moment, so it cannot put events live.',
        _ => e.userMessage,
      };
}

class _LifecycleAction {
  const _LifecycleAction(this.action, this.label, {this.destructive = false});
  final String action;
  final String label;
  final bool destructive;
}

class _Step {
  _Step(this.label, this.desc, this.done);
  final String label;
  final String desc;
  final bool done;
}

class _StepRow extends StatelessWidget {
  const _StepRow(
      {required this.step,
      required this.isCurrent,
      required this.isLast});
  final _Step step;
  final bool isCurrent;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Line + dot
          Column(
            children: [
              Container(
                width: 24,
                height: 24,
                decoration: BoxDecoration(
                  color: step.done
                      ? (isCurrent ? c.accent : c.success)
                      : c.elevated,
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: step.done ? Colors.transparent : c.border,
                  ),
                ),
                child: step.done
                    ? Icon(
                        isCurrent
                            ? Icons.radio_button_checked
                            : Icons.check_rounded,
                        size: 14,
                        color: Colors.white,
                      )
                    : null,
              ),
              if (!isLast)
                Expanded(
                  child: Container(
                      width: 2, color: step.done ? c.success : c.border),
                ),
            ],
          ),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(bottom: KSpace.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    step.label,
                    style: TextStyle(
                      color: isCurrent ? c.accent : (step.done ? c.text : c.muted),
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                    ),
                  ),
                  Text(
                    step.desc,
                    style: TextStyle(color: c.muted, fontSize: 12.5),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
