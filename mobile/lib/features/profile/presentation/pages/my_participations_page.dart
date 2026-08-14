import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../orders/data/models/attendee_dtos.dart';
import '../../../orders/presentation/providers/attendee_providers.dart';

/// My participations — `GET /v1/me/participations`, respond via
/// `POST /v1/participants/{participantId}/respond`.
///
/// A participation is a *role* at an event (speaker, judge, mentor, volunteer…), not a ticket.
/// Invited roles are actionable and sorted to the top, because an unanswered invitation is the
/// only thing on this screen that needs the user to do something.
class MyParticipationsPage extends ConsumerWidget {
  const MyParticipationsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('My roles')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(myParticipationsProvider);
          await ref.read(myParticipationsProvider.future);
        },
        child: AsyncValueView(
          value: ref.watch(myParticipationsProvider),
          onRetry: () => ref.invalidate(myParticipationsProvider),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.badge_outlined,
            title: 'No roles yet',
            message:
                'When an organiser invites you to speak, judge or volunteer, it will appear here.',
          ),
          data: (all) {
            final invited = all.where((p) => p.state == 'invited').toList();
            final rest = all.where((p) => p.state != 'invited').toList();

            return ListView(
              padding: const EdgeInsets.all(KSpace.lg),
              children: [
                if (invited.isNotEmpty) ...[
                  Text('Needs your answer',
                      style: TextStyle(
                          color: c.text, fontWeight: FontWeight.w700, fontSize: 15)),
                  const SizedBox(height: KSpace.sm),
                  for (final p in invited)
                    Padding(
                      padding: const EdgeInsets.only(bottom: KSpace.md),
                      child: _ParticipationCard(
                        participation: p,
                        onRespond: (accept) => _respond(context, ref, p, accept: accept),
                      ),
                    ),
                  const SizedBox(height: KSpace.lg),
                ],
                if (rest.isNotEmpty) ...[
                  Text('Confirmed roles',
                      style: TextStyle(
                          color: c.text, fontWeight: FontWeight.w700, fontSize: 15)),
                  const SizedBox(height: KSpace.sm),
                  for (final p in rest)
                    Padding(
                      padding: const EdgeInsets.only(bottom: KSpace.md),
                      child: _ParticipationCard(participation: p, onRespond: null),
                    ),
                ],
              ],
            );
          },
        ),
      ),
    );
  }

  Future<void> _respond(
    BuildContext context,
    WidgetRef ref,
    ParticipationDto participation, {
    required bool accept,
  }) async {
    try {
      await ref
          .read(attendeeActionsProvider)
          .respondToParticipation(participation.id, accept: accept);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(accept ? 'Role accepted.' : 'Role declined.')),
        );
      }
    } on ApiError catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
      }
    }
  }
}

class _ParticipationCard extends StatelessWidget {
  const _ParticipationCard({required this.participation, required this.onRespond});

  final ParticipationDto participation;
  final void Function(bool accept)? onRespond;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final scheme = Theme.of(context).colorScheme;

    final roleLabel = participation.customLabel?.isNotEmpty == true
        ? participation.customLabel!
        : _humanise(participation.roleSlug);

    final (Color tone, String stateLabel) = switch (participation.state) {
      'accepted' => (c.success, 'Accepted'),
      'declined' => (c.muted, 'Declined'),
      'removed' => (c.muted, 'Removed'),
      _ => (c.accent, 'Invited'),
    };

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
        border: onRespond != null ? Border.all(color: c.accent, width: 1.5) : null,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(roleLabel,
                    style: TextStyle(
                        color: c.text, fontWeight: FontWeight.w700, fontSize: 16)),
              ),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: KSpace.xs),
                decoration: BoxDecoration(
                  color: tone.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(KRadius.pill),
                ),
                child: Text(stateLabel,
                    style: TextStyle(
                        color: tone, fontSize: 12, fontWeight: FontWeight.w600)),
              ),
            ],
          ),
          if (participation.roleClass != null) ...[
            const SizedBox(height: KSpace.xs),
            Text(_humanise(participation.roleClass!),
                style: TextStyle(color: c.muted, fontSize: 13)),
          ],
          if (onRespond != null) ...[
            const SizedBox(height: KSpace.md),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => onRespond!(false),
                    style: OutlinedButton.styleFrom(foregroundColor: scheme.error),
                    child: const Text('Decline'),
                  ),
                ),
                const SizedBox(width: KSpace.md),
                Expanded(
                  child: FilledButton(
                    onPressed: () => onRespond!(true),
                    child: const Text('Accept'),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  static String _humanise(String slug) {
    if (slug.isEmpty) return slug;
    final words = slug.replaceAll('_', ' ').replaceAll('-', ' ').split(' ');
    return words
        .map((w) => w.isEmpty ? w : '${w[0].toUpperCase()}${w.substring(1)}')
        .join(' ');
  }
}
