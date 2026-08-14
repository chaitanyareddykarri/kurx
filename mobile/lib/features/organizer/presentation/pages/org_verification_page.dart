import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/organizer_providers.dart';

/// Org Verification — document status display only (KYC bank+PAN is a SECURITY STOP).
class OrgVerificationPage extends ConsumerWidget {
  const OrgVerificationPage({super.key, required this.orgId});
  final String orgId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Org Verification')),
      body: AsyncValueView(
        value: ref.watch(orgDetailProvider(orgId)),
        onRetry: () => ref.invalidate(orgDetailProvider(orgId)),
        data: (org) {
          // The detail endpoint always sends these two; the fallbacks are only for the
          // list shape, which shares this DTO. A "PAN Verification" step used to sit here
          // driven by a `kyc_status` field no endpoint returns — it is gone rather than
          // reporting an invented status (D-064).
          final verification = org.verificationStatus ?? 'unverified';
          final payout = org.payoutAccountStatus ?? 'not_submitted';
          final steps = [
            (
              'Business Documents',
              'GST / registration certificate',
              verification,
            ),
            (
              'Bank Account',
              'Bank details for payouts',
              payout,
            ),
            (
              'Under Review',
              'Our team reviews your documents',
              verification == 'verified' ? 'verified' : 'pending',
            ),
          ];

          return Column(
            children: [
              // Overall banner
              Container(
                margin: const EdgeInsets.all(KSpace.lg),
                padding: const EdgeInsets.all(KSpace.lg),
                decoration: BoxDecoration(
                  color: _bannerColor(verification, c)
                      .withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(KRadius.lg),
                  border: Border.all(
                    color: _bannerColor(verification, c)
                        .withValues(alpha: 0.3),
                  ),
                ),
                child: Row(
                  children: [
                    Icon(
                      _bannerIcon(verification),
                      color: _bannerColor(verification, c),
                    ),
                    const SizedBox(width: KSpace.md),
                    Expanded(
                      child: Text(
                        _bannerText(verification),
                        style: TextStyle(
                          color: _bannerColor(verification, c),
                          fontWeight: FontWeight.w700,
                          fontSize: 14,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
                  children: steps.map((s) {
                    return Padding(
                      padding: const EdgeInsets.only(bottom: KSpace.md),
                      child: DecoratedBox(
                        decoration: BoxDecoration(
                          color: c.cardSurface,
                          borderRadius:
                              BorderRadius.circular(KRadius.xl),
                          boxShadow: kCardShadow(context),
                        ),
                        child: ListTile(
                          leading: Container(
                            width: 40,
                            height: 40,
                            decoration: BoxDecoration(
                              color: c.elevated,
                              borderRadius:
                                  BorderRadius.circular(KRadius.md),
                            ),
                            child: Icon(Icons.upload_file_outlined,
                                color: c.muted, size: 20),
                          ),
                          title: Text(s.$1,
                              style: TextStyle(
                                  color: c.text,
                                  fontWeight: FontWeight.w700,
                                  fontSize: 14)),
                          subtitle: Text(s.$2,
                              style: TextStyle(
                                  color: c.muted, fontSize: 12.5)),
                          trailing: _StatusBadge(status: s.$3, c: c),
                        ),
                      ),
                    );
                  }).toList(),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  Color _bannerColor(String status, KurxColors c) => switch (status) {
        'verified' => c.success,
        'pending' => c.warning,
        'rejected' => c.danger,
        _ => c.muted,
      };

  IconData _bannerIcon(String status) => switch (status) {
        'verified' => Icons.verified_rounded,
        'pending' => Icons.hourglass_bottom_rounded,
        'rejected' => Icons.cancel_rounded,
        _ => Icons.info_outline_rounded,
      };

  String _bannerText(String status) => switch (status) {
        'verified' => 'Organization is fully verified',
        'pending' => 'Under review — we\'ll notify you once verified',
        'rejected' => 'Verification rejected — please resubmit',
        _ => 'Documents not yet submitted',
      };
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.status, required this.c});
  final String status;
  final KurxColors c;

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      'verified' => ('Verified', c.success),
      'pending' || 'in_review' => ('In Review', c.warning),
      'rejected' => ('Rejected', c.danger),
      'not_submitted' => ('Not Submitted', c.muted),
      _ => (status, c.muted),
    };
    return Container(
      padding: const EdgeInsets.symmetric(
          horizontal: KSpace.sm, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(KRadius.pill),
      ),
      child: Text(label,
          style: TextStyle(
              color: color, fontSize: 11, fontWeight: FontWeight.w700)),
    );
  }
}
