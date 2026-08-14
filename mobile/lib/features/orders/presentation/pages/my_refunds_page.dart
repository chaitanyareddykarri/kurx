import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_card.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../../data/models/attendee_dtos.dart';
import '../providers/attendee_providers.dart';

/// The caller's refunds across every event (D-199).
///
/// Read-only by design: a refund is requested against an *order*, and it is the org's Owner/Finance
/// who issues it — this screen is where a buyer watches one land, not where they start one.
class MyRefundsPage extends ConsumerWidget {
  const MyRefundsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final refunds = ref.watch(myRefundsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Refunds')),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(myRefundsProvider),
        child: AsyncValueView(
          value: refunds,
          onRetry: () => ref.invalidate(myRefundsProvider),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.receipt_long_outlined,
            title: 'No refunds',
            message: 'Refunds on your orders appear here.',
          ),
          data: (list) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: list.length,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
            itemBuilder: (context, i) => _RefundCard(refund: list[i]),
          ),
        ),
      ),
    );
  }
}

class _RefundCard extends StatelessWidget {
  const _RefundCard({required this.refund});

  final RefundDto refund;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final (icon, colour, label) = switch (refund.status) {
      'succeeded' => (Icons.check_circle_outline_rounded, c.success, 'Refunded'),
      'failed' => (Icons.error_outline_rounded, c.danger, 'Failed'),
      'processing' => (Icons.sync_rounded, c.accent, 'Processing'),
      _ => (Icons.schedule_rounded, c.muted, 'Pending'),
    };

    return KurxCard(
      child: Row(
        children: [
          Icon(icon, color: colour),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  formatPaise(refund.amountPaise),
                  style: Theme.of(context)
                      .textTheme
                      .bodyLarge
                      ?.copyWith(fontWeight: FontWeight.w700, color: c.text),
                ),
                const SizedBox(height: KSpace.xs),
                Text(
                  [
                    label,
                    if (refund.reason != null && refund.reason!.isNotEmpty) refund.reason!,
                    if (refund.createdAt != null)
                      '${refund.createdAt!.day}/${refund.createdAt!.month}/${refund.createdAt!.year}',
                  ].join(' · '),
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
