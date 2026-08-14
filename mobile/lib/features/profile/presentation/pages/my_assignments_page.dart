import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../orders/presentation/providers/attendee_providers.dart';
import '../../../organizer/data/models/event_manage_dto.dart';

/// The invitee's side of event staffing — `GET /v1/me/assignments` (D-319).
///
/// Unlike [OrgInvitationsPage] this screen **is** actionable: assignments are keyed by id, and the
/// list endpoint returns it. `RespondAsync` refuses every actor except the invitee — not the
/// organiser who sent the invite, not a platform admin — so these two buttons are the only path an
/// assignment has from `Invited` to `Accepted`, and §14.2's go-live gate waits on exactly that.
///
/// Web equivalent: `web/app/(app)/assignments/page.tsx`.
class MyAssignmentsPage extends ConsumerWidget {
  const MyAssignmentsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Assignments')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(myAssignmentsProvider);
          await ref.read(myAssignmentsProvider.future);
        },
        child: AsyncValueView(
          value: ref.watch(myAssignmentsProvider),
          onRetry: () => ref.invalidate(myAssignmentsProvider),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.assignment_ind_outlined,
            title: 'No assignments yet',
            message:
                'When an organiser adds you to an event team, the invitation lands here for you to accept or decline.',
          ),
          data: (rows) {
            // Unanswered first: the list exists to surface what is waiting on this person.
            final pending = rows.where((a) => a.status == 'invited').toList();
            final settled = rows.where((a) => a.status != 'invited').toList();
            return ListView(
              padding: const EdgeInsets.all(KSpace.lg),
              children: [
                if (pending.isNotEmpty) ...[
                  const _SectionLabel('Waiting on you'),
                  const SizedBox(height: KSpace.md),
                  for (final a in pending) ...[
                    _AssignmentCard(assignment: a),
                    const SizedBox(height: KSpace.md),
                  ],
                ],
                if (settled.isNotEmpty) ...[
                  if (pending.isNotEmpty) const SizedBox(height: KSpace.lg),
                  const _SectionLabel('Accepted'),
                  const SizedBox(height: KSpace.md),
                  for (final a in settled) ...[
                    _AssignmentCard(assignment: a),
                    const SizedBox(height: KSpace.md),
                  ],
                ],
              ],
            );
          },
        ),
      ),
    );
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text);
  final String text;

  @override
  Widget build(BuildContext context) => Text(
        text,
        style: TextStyle(color: context.kurx.text, fontWeight: FontWeight.w700, fontSize: 16),
      );
}

class _AssignmentCard extends ConsumerStatefulWidget {
  const _AssignmentCard({required this.assignment});
  final AssignmentDto assignment;

  @override
  ConsumerState<_AssignmentCard> createState() => _AssignmentCardState();
}

class _AssignmentCardState extends ConsumerState<_AssignmentCard> {
  bool _busy = false;
  String? _error;

  Future<void> _respond({required bool accept}) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(attendeeActionsProvider).respondToAssignment(widget.assignment.id, accept: accept);
      if (!mounted) return;
      // The row leaves "Waiting on you" on the provider invalidation, so the card itself just stops
      // being busy — no local success state to drift from the server's.
      setState(() => _busy = false);
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = switch (e.code) {
          // Answering twice is ordinary — two devices, a stale list — not an exception.
          'not_pending' => 'You have already answered this invitation.',
          'not_found' => 'This assignment no longer exists.',
          'forbidden' => 'This invitation is not yours to answer.',
          _ => e.userMessage,
        };
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final a = widget.assignment;
    final pending = a.status == 'invited';
    final role = a.customRole ?? a.role;
    final starts = a.eventStartsAt;

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            a.eventTitle.isEmpty ? 'An event' : a.eventTitle,
            style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 16),
          ),
          const SizedBox(height: KSpace.xs),
          Text('Invited as $role', style: TextStyle(color: c.muted)),
          // A null organisation name means a self-represented event, which names no organisation at
          // all (D-268) — so nothing is rendered here, never a placeholder.
          if (a.representingOrgName != null) ...[
            const SizedBox(height: KSpace.xs),
            Text(a.representingOrgName!, style: TextStyle(color: c.muted, fontSize: 13)),
          ],
          if (starts != null) ...[
            const SizedBox(height: KSpace.xs),
            Text(DateFormat('d MMM yyyy, h:mm a').format(starts.toLocal()),
                style: TextStyle(color: c.muted, fontSize: 13)),
          ],
          if (a.notes != null && a.notes!.isNotEmpty) ...[
            const SizedBox(height: KSpace.sm),
            Text(a.notes!, style: TextStyle(color: c.muted, fontSize: 13)),
          ],
          if (_error != null) ...[
            const SizedBox(height: KSpace.md),
            Semantics(
              liveRegion: true,
              child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error, fontSize: 13)),
            ),
          ],
          if (pending) ...[
            const SizedBox(height: KSpace.lg),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: _busy ? null : () => _respond(accept: false),
                    child: const Text('Decline'),
                  ),
                ),
                const SizedBox(width: KSpace.md),
                Expanded(
                  child: FilledButton(
                    onPressed: _busy ? null : () => _respond(accept: true),
                    child: _busy
                        ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Text('Accept'),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
