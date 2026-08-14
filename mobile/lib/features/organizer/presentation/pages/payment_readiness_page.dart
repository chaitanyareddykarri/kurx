import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/organizer_providers.dart';

class PaymentReadinessPage extends ConsumerWidget {
  const PaymentReadinessPage(
      {super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Payment Readiness')),
      body: AsyncValueView(
        value: ref.watch(orgDetailProvider(orgId)),
        onRetry: () => ref.invalidate(orgDetailProvider(orgId)),
        data: (org) {
          // `payout_account_status` is PayoutAccountStatus lowercased — none/pending/active.
          // It replaces a `kyc_status` field no endpoint returns (D-064).
          final kycDone = org.payoutAccountStatus == 'active';
          final verifiedDone = org.verificationStatus == 'verified';
          final ready = kycDone && verifiedDone;

          return SingleChildScrollView(
            padding: const EdgeInsets.all(KSpace.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Overall status banner
                Container(
                  padding: const EdgeInsets.all(KSpace.lg),
                  decoration: BoxDecoration(
                    color: ready
                        ? c.success.withValues(alpha: 0.1)
                        : c.accent.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(KRadius.lg),
                    border: Border.all(
                      color: ready
                          ? c.success.withValues(alpha: 0.3)
                          : c.accent.withValues(alpha: 0.3),
                    ),
                  ),
                  child: Row(
                    children: [
                      Icon(
                        ready
                            ? Icons.check_circle_rounded
                            : Icons.warning_amber_rounded,
                        color: ready ? c.success : c.accent,
                        size: 24,
                      ),
                      const SizedBox(width: KSpace.md),
                      Text(
                        ready
                            ? 'Ready to sell tickets'
                            : 'Action needed before selling tickets',
                        style: TextStyle(
                          color: ready ? c.success : c.accent,
                          fontWeight: FontWeight.w700,
                          fontSize: 14,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: KSpace.xl),
                // Checklist
                DecoratedBox(
                  decoration: BoxDecoration(
                    color: c.cardSurface,
                    borderRadius: BorderRadius.circular(KRadius.xl),
                    boxShadow: kCardShadow(context),
                  ),
                  child: Column(
                    children: [
                      _CheckRow(
                        label: 'Organization Verified',
                        done: verifiedDone,
                        detail: verifiedDone
                            ? 'Your organization is verified'
                            : 'Submit documents for verification',
                        onTap: verifiedDone
                            ? null
                            : () => context.push('/representing/$orgId/verification'),
                      ),
                      Divider(color: c.border, height: 0),
                      _CheckRow(
                        label: 'Bank Account Linked',
                        done: kycDone,
                        detail: kycDone
                            ? 'Bank details verified'
                            : 'Complete KYC to receive payouts',
                        // KYC is a SECURITY STOP — not navigating here
                        onTap: null,
                      ),
                      Divider(color: c.border, height: 0),
                      _CheckRow(
                        label: 'Razorpay Configured',
                        done: ready,
                        detail: 'Linked automatically when org is verified',
                        onTap: null,
                      ),
                      Divider(color: c.border, height: 0),
                      // Payouts are reached from the event that needs them, not from an organisation
                      // dashboard (D-267). Settlement is legally org-bound, so the page is org-scoped —
                      // but nobody browses to it through an organisation any more.
                      _CheckRow(
                        label: 'Payout account',
                        done: kycDone,
                        detail: 'Balance and ledger for the account this event settles to',
                        onTap: () => context.push('/representing/$orgId/wallet'),
                      ),
                    ],
                  ),
                ),
                if (!ready) ...[
                  const SizedBox(height: KSpace.xl),
                  FilledButton(
                    onPressed: () =>
                        context.push('/representing/$orgId/verification'),
                    child: const Text('Complete Setup'),
                  ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}

class _CheckRow extends StatelessWidget {
  const _CheckRow({
    required this.label,
    required this.done,
    required this.detail,
    required this.onTap,
  });
  final String label;
  final bool done;
  final String detail;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return ListTile(
      leading: Container(
        width: 36,
        height: 36,
        decoration: BoxDecoration(
          color: done
              ? c.success.withValues(alpha: 0.12)
              : c.elevated,
          shape: BoxShape.circle,
        ),
        child: Icon(
          done ? Icons.check_rounded : Icons.close_rounded,
          color: done ? c.success : c.muted,
          size: 18,
        ),
      ),
      title: Text(label,
          style: TextStyle(
              color: c.text, fontWeight: FontWeight.w600, fontSize: 14)),
      subtitle: Text(detail,
          style: TextStyle(color: c.muted, fontSize: 12.5)),
      trailing: onTap != null
          ? TextButton(
              onPressed: onTap,
              child: const Text('Fix'),
            )
          : null,
    );
  }
}
