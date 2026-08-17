import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/event_status.dart';
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
          // D-362 — Approved is a step of its own. The rail went Review → Published, so approval had
          // nowhere to land and an approved event was drawn as still "Submitted for review". Approval is
          // not publication: it hands the event back, and the organiser decides when it goes live.
          final steps = [
            _Step('Draft', 'Event created and being set up', true),
            _Step('Under Review', 'Submitted for review', false),
            _Step('Approved', 'Cleared review — publish when you are ready', false),
            _Step('Published', 'Visible to public', false),
            _Step('Live', 'Event is happening now', false),
            _Step('Closed', 'Event ended', false),
          ];

          final currentIdx =
              statusIndex(event.status).clamp(0, steps.length - 1);
          for (var i = 0; i <= currentIdx; i++) {
            steps[i] = _Step(steps[i].label, steps[i].desc, true);
          }
          final offRail = offRailNote(event.status);

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
                              eventStatusLabel(event.status).toUpperCase(),
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
                // The rail, or — for a state that is off it — the sentence that is true instead.
                if (offRail != null)
                  _OffRailCard(note: offRail)
                else
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
                // Not a transition — its own endpoint — which is why it sits outside the list above.
                // Web and the admin console both had it; this app could create a draft and never get
                // rid of it. Draft only, matching the server (`not_draft` past that point).
                if (event.status == 'draft')
                  KurxButton(
                    label: 'Delete draft',
                    variant: KurxButtonVariant.secondary,
                    expand: true,
                    onPressed: () => _deleteDraft(context, ref),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }

  /// The states that are not ON the rail at all, and what to say instead.
  ///
  /// `statusIndex` sends anything it does not recognise to step 0, which is right for an UNKNOWN status
  /// — guessing forward would tell someone their event is published when it is not. But `rejected`,
  /// `cancelled` and `archived` are perfectly well known, were simply never given an arm, and inherited
  /// that fallback: the rail drew them as **Draft**. They are not an earlier point on the line, they are
  /// off it, and a progress rail has no way to say so. So for these three the rail is replaced by the
  /// one sentence that is actually true, and the action list below still offers whatever remains.
  ///
  /// Returns null for every status that genuinely belongs on the rail.
  static ({String title, String detail})? offRailNote(String s) => switch (s) {
        'rejected' => (
            title: 'Rejected',
            detail: 'A reviewer refused this event. Read their notes, fix what they raised, '
                'and resubmit — it goes back to the queue, not back to the start.',
          ),
        'cancelled' => (
            title: 'Cancelled',
            detail: 'This event was called off. Cancelling is final (D-101) — it is never reopened. '
                'Archive it to file it away, or clone it to start again.',
          ),
        'archived' => (
            title: 'Archived',
            detail: 'This event is filed away. It stays readable, and nothing further happens to it.',
          ),
        _ => null,
      };

  /// Static and public so the rail's status→step mapping is testable without pumping the page.
  static int statusIndex(String s) => switch (s) {
        'draft' => 0,
        // D-266 M4 states, serialized as the lowercased enum name — no underscore. `under_review`
        // never matched anything the API returns and silently fell through to the Draft step.
        'pendingreview' || 'underreview' || 'changesrequested' => 1,
        // D-362 — `approved` sat here too, so an organiser holding an approved event was shown "in
        // review" and no reason to act. Approval ENDS review; the event is theirs to publish, and the
        // rail has to say the ball has come back. Still short of `published`: approval is permission.
        'approved' => 2,
        'scheduled' || 'published' => 3,
        'live' => 4,
        'closed' || 'completed' => 5,
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
        // `archive` from Draft is in EventStatusWorkflow and was offered by no client — the admin
        // console had it, the two host surfaces did not. It files a draft away without destroying it,
        // which is the softer half of the pair with Delete below.
        'draft' => const [
            _LifecycleAction('submit_review', 'Submit for review'),
            _LifecycleAction('publish', 'Publish now'),
            _LifecycleAction('archive', 'Archive', destructive: true),
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
        // No `cancel` on these two: EventStatusWorkflow's cancel table does not accept them, and this
        // list only offers transitions the server declares valid. (`approved` used to be a third; D-363
        // §2 added it to the table, so it now offers cancel below.)
        'changesrequested' || 'rejected' => const [
            _LifecycleAction('submit_for_review', 'Resubmit for review'),
          ],
        // D-363 §1/§2 — Publish was the only exit, so an approved event whose organiser changed their
        // mind was a dead end. Withdraw returns it to Draft (never public, so nothing is lost); cancel
        // is terminal, for abandoning it outright.
        // `schedule` is here because nothing else offered it anywhere: `Scheduled` is reachable only
        // through this action, both clients render a whole action list for that status, and neither
        // could ever get an event into it. It differs from Publish — Scheduled means visible with
        // registration still shut, then `open_registration` opens it (V3 §14.1).
        'approved' => const [
            _LifecycleAction('publish_approved', 'Publish now'),
            _LifecycleAction('schedule', 'Schedule — registration opens later'),
            _LifecycleAction('withdraw', 'Withdraw to draft'),
            _LifecycleAction('cancel', 'Cancel event', destructive: true),
          ],
        // `cancel` on all three: EventStatusWorkflow accepts Published/Scheduled/Live, and omitting it
        // left the one action that obliges refunds (D-101) reachable on web but not here — an organiser
        // calling off a live event had no way to say so from their phone.
        'scheduled' => const [
            _LifecycleAction('open_registration', 'Open registration'),
            _LifecycleAction('unpublish', 'Back to draft', destructive: true),
            _LifecycleAction('cancel', 'Cancel event', destructive: true),
          ],
        'published' => const [
            _LifecycleAction('go_live', 'Go live'),
            // `complete` from Published as well as from Live: an event that ended without anyone
            // pressing "Go live" could otherwise never be completed, and completion is what releases
            // certificates and results.
            _LifecycleAction('complete', 'Mark completed'),
            _LifecycleAction('close', 'Close event', destructive: true),
            _LifecycleAction('unpublish', 'Unpublish', destructive: true),
            _LifecycleAction('cancel', 'Cancel event', destructive: true),
          ],
        'live' => const [
            _LifecycleAction('complete', 'Mark completed'),
            _LifecycleAction('cancel', 'Cancel event', destructive: true),
          ],
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

  /// Discards a draft and leaves the screen — the event it was showing no longer exists to show.
  ///
  /// Confirmed first, and worded so the confirm is answerable: "are you sure" without saying what is
  /// lost is a dialog people learn to dismiss. Soft on the server (D-364), but not to this organiser —
  /// it is gone from every list they have, so it is presented as permanent.
  Future<void> _deleteDraft(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete draft'),
        content: const Text(
            'This draft and everything set up on it — ticket types, sessions, media — go with it. '
            'It cannot be undone. Archive it instead if you only want it out of your list.'),
        actions: [
          TextButton(
              onPressed: () => Navigator.of(ctx).pop(false),
              child: const Text('Back')),
          FilledButton(
              onPressed: () => Navigator.of(ctx).pop(true),
              child: const Text('Delete')),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;

    try {
      await ref.read(eventManageSourceProvider).deleteEvent(orgId, eventId);
      ref.invalidate(myEventsProvider);
      if (!context.mounted) return;
      // Pop before the snackbar: this screen reads the event that was just deleted, so leaving it
      // mounted means its next rebuild fetches a 404.
      Navigator.of(context).maybePop();
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Draft deleted.')));
    } on ApiError catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(_explain(e))));
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
        'event_already_started' =>
          'This event has already started — ask an admin to cancel it.',
        // Was `approval_required` and `no_ticket_types` — two codes the backend has never emitted, so
        // both lines were dead while the codes that DO arrive (`approval_pending` from the OrgUnit
        // approval chain, `no_pass` for an event with no ticket type) fell through to the generic
        // sentence. The same class of defect as the `weak_password` mapping in api_error.dart.
        'approval_pending' => 'This event is waiting on an approval chain.',
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
        // A paid event cannot self-publish out of Draft (EventService: paid_event_requires_review).
        // Saying so points at the action that works instead of reporting a dead end.
        'paid_event_requires_review' =>
          'Paid events are reviewed before they go live. Submit it for review instead of publishing.',
        // D-362 — reachable from this screen only in the window where the event is already in the queue
        // and the organiser taps a stale Publish. Mirrors packages/ui PROBLEM_COPY word for word.
        'reviewer_required' =>
          "A reviewer has to approve this — you can't approve your own submission.",
        'claimed_by_another_reviewer' =>
          'A reviewer has already picked this event up, so it can no longer be withdrawn.',
        // D-363 — refused to protect orders, tickets and registrations from a cascade delete. Names the
        // two things that DO work, because a "no" with no alternative reads as a bug.
        'event_has_history' =>
          'People have already registered for this event, so it cannot be unpublished or deleted. '
              'Cancel it instead — everyone is refunded and notified — or close it once it is over.',
        'event_under_review' =>
          "This event is with a reviewer right now, so it can't be edited. You'll get it back with their notes.",
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

/// Shown in place of the rail for a status that is off it. Deliberately not styled as a step: the whole
/// defect was a terminal state borrowing the vocabulary of progress.
class _OffRailCard extends StatelessWidget {
  const _OffRailCard({required this.note});
  final ({String title, String detail}) note;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.muted.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(KRadius.lg),
        border: Border.all(color: c.muted.withValues(alpha: 0.25)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              note.title,
              style: TextStyle(
                  color: c.text, fontSize: 15, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: KSpace.sm),
            Text(note.detail, style: TextStyle(color: c.muted, fontSize: 13)),
          ],
        ),
      ),
    );
  }
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
