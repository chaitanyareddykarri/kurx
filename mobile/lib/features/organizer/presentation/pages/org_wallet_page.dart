import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../providers/organizer_providers.dart';

class OrgWalletPage extends ConsumerWidget {
  const OrgWalletPage({super.key, required this.orgId});
  final String orgId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Wallet')),
      body: AsyncValueView(
        // The balance comes from GET /v1/orgs/{orgId}/wallet. It was previously read off
        // OrgDto.walletBalancePaise — a field no endpoint returns, so the card rendered its
        // zero default as though it were a real balance (D-064).
        value: ref.watch(orgWalletProvider(orgId)),
        onRetry: () => ref.invalidate(orgWalletProvider(orgId)),
        data: (wallet) => Column(
          children: [
            // Balance card
            Padding(
              padding: const EdgeInsets.all(KSpace.lg),
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    colors: [c.teal, c.accent],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  borderRadius: BorderRadius.circular(KRadius.xl),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(KSpace.xl),
                  child: Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text(
                              'Available Balance',
                              style: TextStyle(
                                  color: Colors.white70, fontSize: 13),
                            ),
                            const SizedBox(height: KSpace.xs),
                            Text(
                              formatPaise(wallet.availablePaise),
                              style: const TextStyle(
                                color: Colors.white,
                                fontSize: 28,
                                fontWeight: FontWeight.w900,
                              ),
                            ),
                          ],
                        ),
                      ),
                      // The earlier "SECURITY STOP: not implemented yet" here was stale (D-258).
                      // WalletWithdrawPage exists, is routed at /representing/:orgId/wallet/withdraw, and posts
                      // to the real POST /v1/orgs/{orgId}/wallet/withdraw — so the flow was already
                      // reachable by deep link while this button pretended it was not. That is the
                      // worst of both: no protection, and a control that looks broken.
                      //
                      // The substantive caution still holds and is honoured where it belongs: a
                      // withdrawal records a durable `requested` intent and nothing executes it (no
                      // payout provider). The destination screen states exactly that via _PayoutNotice
                      // rather than implying money is moving, so routing there is safe.
                      OutlinedButton(
                        onPressed: () => context.push('/representing/$orgId/wallet/withdraw'),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: Colors.white,
                          side: const BorderSide(color: Colors.white54),
                          disabledForegroundColor: Colors.white54,
                        ),
                        child: const Text('Withdraw'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
            // Ledger
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Transaction history',
                  style: TextStyle(
                    color: c.text,
                    fontWeight: FontWeight.w700,
                    fontSize: 15,
                  ),
                ),
              ),
            ),
            const SizedBox(height: KSpace.md),
            Expanded(
              child: AsyncValueView(
                value: ref.watch(orgLedgerProvider(orgId)),
                onRetry: () => ref.invalidate(orgLedgerProvider(orgId)),
                isEmpty: (l) => l.isEmpty,
                empty: const EmptyState(
                  icon: Icons.receipt_long_outlined,
                  title: 'No transactions yet',
                  message:
                      'Revenue from ticket sales will appear here.',
                ),
                data: (entries) => ListView.separated(
                  padding: const EdgeInsets.symmetric(
                      horizontal: KSpace.lg, vertical: 0),
                  itemCount: entries.length,
                  separatorBuilder: (_, _) => Divider(color: c.border),
                  itemBuilder: (_, i) {
                    final e = entries[i];
                    final isCredit = e.type == 'credit';
                    return ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: Container(
                        width: 40,
                        height: 40,
                        decoration: BoxDecoration(
                          color: isCredit
                              ? c.success.withValues(alpha: 0.12)
                              : c.accent.withValues(alpha: 0.12),
                          shape: BoxShape.circle,
                        ),
                        child: Icon(
                          isCredit
                              ? Icons.arrow_downward_rounded
                              : Icons.arrow_upward_rounded,
                          color: isCredit ? c.success : c.accent,
                          size: 18,
                        ),
                      ),
                      title: Text(e.description,
                          style: TextStyle(
                              color: c.text,
                              fontWeight: FontWeight.w600,
                              fontSize: 13.5)),
                      subtitle: Text(
                        DateFormat('dd MMM yyyy').format(e.createdAt),
                        style: TextStyle(color: c.muted, fontSize: 12),
                      ),
                      trailing: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text(
                            '${isCredit ? '+' : '-'} ${formatPaise(e.amountPaise)}',
                            style: TextStyle(
                              color: isCredit ? c.success : c.danger,
                              fontWeight: FontWeight.w800,
                              fontSize: 14,
                            ),
                          ),
                          Text(
                            formatPaise(e.balanceAfterPaise),
                            style: TextStyle(color: c.muted, fontSize: 11.5),
                          ),
                        ],
                      ),
                    );
                  },
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
