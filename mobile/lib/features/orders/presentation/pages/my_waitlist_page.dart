import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/attendee_dtos.dart';
import '../providers/attendee_providers.dart';

/// My waitlist — `GET /v1/me/waitlist`, leave via
/// `DELETE /v1/events/{eventId}/ticket-types/{ticketTypeId}/waitlist`.
///
/// A `notified` entry carries an `offer_expires_at`: the holder has a window to convert before
/// `ExpireWaitlistOffersJob` releases the seat, so that deadline is the most important thing on
/// the screen and is rendered as a live countdown rather than a raw timestamp.
class MyWaitlistPage extends ConsumerWidget {
  const MyWaitlistPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('My waitlist')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(myWaitlistProvider);
          await ref.read(myWaitlistProvider.future);
        },
        child: AsyncValueView(
          value: ref.watch(myWaitlistProvider),
          onRetry: () => ref.invalidate(myWaitlistProvider),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.hourglass_empty_rounded,
            title: 'Not on any waitlists',
            message: 'When a ticket sells out you can join its waitlist from the event page.',
          ),
          data: (entries) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: entries.length,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
            itemBuilder: (_, i) => _WaitlistCard(
              entry: entries[i],
              onLeave: () => _leave(context, ref, entries[i]),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _leave(BuildContext context, WidgetRef ref, WaitlistEntryDto entry) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Leave waitlist'),
        content: Text(
          entry.status == 'notified'
              ? 'You currently hold an offer. Leaving releases it to the next person and cannot '
                  'be undone.'
              : 'You will lose your place in the queue.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(ctx).pop(false), child: const Text('Stay')),
          FilledButton(onPressed: () => Navigator.of(ctx).pop(true), child: const Text('Leave')),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;

    try {
      await ref.read(attendeeActionsProvider).leaveWaitlist(entry.eventId, entry.ticketTypeId);
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('Left the waitlist.')));
      }
    } on ApiError catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
      }
    }
  }
}

class _WaitlistCard extends StatelessWidget {
  const _WaitlistCard({required this.entry, required this.onLeave});

  final WaitlistEntryDto entry;
  final VoidCallback onLeave;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final scheme = Theme.of(context).colorScheme;
    final offer = entry.offerExpiresAt;
    final notified = entry.status == 'notified';

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
        border: notified ? Border.all(color: c.accent, width: 1.5) : null,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  notified ? 'A ticket is available for you' : 'Position #${entry.position}',
                  style: TextStyle(
                    color: notified ? c.accent : c.text,
                    fontWeight: FontWeight.w700,
                    fontSize: 16,
                  ),
                ),
              ),
              _StatusChip(status: entry.status),
            ],
          ),
          if (notified && offer != null) ...[
            const SizedBox(height: KSpace.sm),
            _OfferCountdown(expiresAt: offer),
          ] else ...[
            const SizedBox(height: KSpace.xs),
            Text(
              'You will be notified if a ticket is released.',
              style: TextStyle(color: c.muted, fontSize: 13),
            ),
          ],
          if (entry.createdAt != null) ...[
            const SizedBox(height: KSpace.xs),
            Text(
              'Joined ${DateFormat('d MMM yyyy').format(entry.createdAt!.toLocal())}',
              style: TextStyle(color: c.muted, fontSize: 12),
            ),
          ],
          const SizedBox(height: KSpace.sm),
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              onPressed: onLeave,
              icon: Icon(Icons.exit_to_app_rounded, size: 18, color: scheme.error),
              label: Text('Leave', style: TextStyle(color: scheme.error)),
            ),
          ),
        ],
      ),
    );
  }
}

/// Live countdown to the offer deadline. Rebuilds once a second only while an offer is open —
/// a static "expires at 14:32" is useless when the window is measured in minutes.
class _OfferCountdown extends StatefulWidget {
  const _OfferCountdown({required this.expiresAt});
  final DateTime expiresAt;

  @override
  State<_OfferCountdown> createState() => _OfferCountdownState();
}

class _OfferCountdownState extends State<_OfferCountdown> {
  late Duration _remaining = _compute();

  Duration _compute() {
    final left = widget.expiresAt.difference(DateTime.now());
    return left.isNegative ? Duration.zero : left;
  }

  @override
  void initState() {
    super.initState();
    _tick();
  }

  Future<void> _tick() async {
    while (mounted && _remaining > Duration.zero) {
      await Future<void>.delayed(const Duration(seconds: 1));
      if (!mounted) return;
      setState(() => _remaining = _compute());
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final scheme = Theme.of(context).colorScheme;

    if (_remaining == Duration.zero) {
      return Text('This offer has expired.', style: TextStyle(color: scheme.error));
    }

    final minutes = _remaining.inMinutes;
    final seconds = _remaining.inSeconds % 60;
    return Row(
      children: [
        Icon(Icons.timer_outlined, size: 18, color: c.accent),
        const SizedBox(width: KSpace.xs),
        Text(
          'Claim within ${minutes}m ${seconds.toString().padLeft(2, '0')}s',
          style: TextStyle(color: c.accent, fontWeight: FontWeight.w600),
        ),
      ],
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.status});
  final String status;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final (Color tone, String label) = switch (status) {
      'notified' => (c.accent, 'Offered'),
      // Material's green among four theme reads: no dark mode, never contrast-solved.
      'converted' => (c.success, 'Booked'),
      'expired' => (c.muted, 'Expired'),
      'cancelled' => (c.muted, 'Left'),
      _ => (c.muted, 'Waiting'),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: KSpace.xs),
      decoration: BoxDecoration(
        color: tone.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(KRadius.pill),
      ),
      child: Text(label,
          style: TextStyle(color: tone, fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }
}
