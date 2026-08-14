import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/organizer_providers.dart';

/// Organisation payout KYC — `GET /v1/orgs/{orgId}/kyc`.
///
/// ## Why submission is a stop and not a form
///
/// `POST /v1/orgs/{orgId}/kyc/bank` and `…/kyc/pan` exist and would accept input. They must not be
/// wired from this app yet:
///
/// 1. They take a **full bank account number, IFSC and PAN** — unmasked financial identity. That is
///    the single most sensitive payload in the product, and the mobile submission path has not had
///    the security review that `.claude/commands/security-review.md` makes mandatory for any
///    bank/KYC/PII change.
/// 2. `KYC_PROVIDER=mock` — `MockKycProvider` adjudicates. Collecting real bank details to feed a
///    mock verifier means holding regulated data for no verification benefit.
/// 3. Payouts do not execute regardless (see `WalletWithdrawPage`), so completing KYC unlocks
///    nothing today.
///
/// So this screen shows the **real, live status** — which is genuinely useful — and refuses to
/// collect the numbers. It does not render a disabled form pretending it will work later.
class KycPage extends ConsumerWidget {
  const KycPage({super.key, required this.orgId});
  final String orgId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Payout verification')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(orgDetailProvider(orgId));
          await ref.read(orgDetailProvider(orgId).future);
        },
        child: AsyncValueView(
          value: ref.watch(orgDetailProvider(orgId)),
          onRetry: () => ref.invalidate(orgDetailProvider(orgId)),
          data: (org) {
            final status = (org.payoutAccountStatus ?? 'not_started').toLowerCase();
            final bankLast4 = org.bankLast4;

            return ListView(
              padding: const EdgeInsets.all(KSpace.lg),
              children: [
                _StatusCard(status: status, bankLast4: bankLast4),
                const SizedBox(height: KSpace.lg),
                Container(
                  padding: const EdgeInsets.all(KSpace.lg),
                  decoration: BoxDecoration(
                    color: c.cardSurface,
                    borderRadius: BorderRadius.circular(KRadius.lg),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Icon(Icons.shield_outlined, size: 20, color: c.accent),
                          const SizedBox(width: KSpace.sm),
                          Text('Submit from the web dashboard',
                              style: TextStyle(
                                  color: c.text,
                                  fontWeight: FontWeight.w700,
                                  fontSize: 15)),
                        ],
                      ),
                      const SizedBox(height: KSpace.sm),
                      Text(
                        'Bank and PAN details are collected on the Kurx web dashboard, which has '
                        'the reviewed handling path for financial identity. This app shows the '
                        'status but never asks for account numbers.',
                        style: TextStyle(color: c.muted),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: KSpace.lg),
                Text(
                  'Payouts are not live yet regardless of verification status — completing KYC '
                  'does not currently release funds.',
                  style: TextStyle(color: c.muted, fontSize: 13),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  const _StatusCard({required this.status, required this.bankLast4});
  final String status;
  final String? bankLast4;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final scheme = Theme.of(context).colorScheme;

    final (Color tone, IconData icon, String label, String detail) = switch (status) {
      'active' => (
          c.success,
          Icons.verified_user_rounded,
          'Verified',
          bankLast4 != null
              ? 'Payout account ending $bankLast4 is verified.'
              : 'Your payout account is verified.'
        ),
      'pending' || 'under_review' => (
          c.accent,
          Icons.hourglass_top_rounded,
          'Under review',
          'Your payout details are being checked.'
        ),
      'rejected' => (
          scheme.error,
          Icons.gpp_bad_outlined,
          'Not accepted',
          'Something did not match. Resubmit from the web dashboard.'
        ),
      _ => (
          c.muted,
          Icons.account_balance_outlined,
          'Not started',
          'Add a payout account on the web dashboard to receive money from ticket sales.'
        ),
    };

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: tone.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(KRadius.lg),
        border: Border.all(color: tone.withValues(alpha: 0.3)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: tone, size: 26),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label,
                    style: TextStyle(
                        color: c.text, fontWeight: FontWeight.w700, fontSize: 16)),
                const SizedBox(height: KSpace.xs),
                Text(detail, style: TextStyle(color: c.text.withValues(alpha: 0.85))),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
