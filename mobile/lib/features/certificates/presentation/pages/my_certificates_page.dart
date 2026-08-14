import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/certificate_dto.dart';
import '../providers/certificates_providers.dart';

class MyCertificatesPage extends ConsumerWidget {
  const MyCertificatesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('My Certificates')),
      body: AsyncValueView(
        value: ref.watch(myCertificatesProvider),
        onRetry: () => ref.invalidate(myCertificatesProvider),
        isEmpty: (list) => list.isEmpty,
        empty: const EmptyState(
          icon: Icons.workspace_premium_outlined,
          title: 'No certificates yet',
          message:
              'Certificates awarded after events you attend will appear here.',
        ),
        data: (certs) => ListView.separated(
          padding: const EdgeInsets.all(KSpace.lg),
          itemCount: certs.length,
          separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
          itemBuilder: (_, i) => _CertCard(
            cert: certs[i],
            onTap: () => context.push('/certificates/${certs[i].verifyCode ?? certs[i].code}'),
          ),
        ),
      ),
    );
  }
}

class _CertCard extends StatelessWidget {
  const _CertCard({required this.cert, required this.onTap});
  final CertificateDto cert;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // A whole certificate card, tappable and announcing as nothing — the same shape as the ticket
    // card in Phase 35, on the credential a person shows an employer.
    return Semantics(
      button: true,
      label: 'Certificate for ${cert.eventTitle}',
      child: GestureDetector(
      onTap: onTap,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: c.cardSurface,
          borderRadius: BorderRadius.circular(KRadius.xl),
          boxShadow: kCardShadow(context),
        ),
        child: Padding(
          padding: const EdgeInsets.all(KSpace.lg),
          child: Row(
            children: [
              Container(
                width: 52,
                height: 52,
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    colors: [
                      c.teal.withValues(alpha: 0.18),
                      c.accent.withValues(alpha: 0.10),
                    ],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  borderRadius: BorderRadius.circular(KRadius.md),
                ),
                child:
                    Icon(Icons.workspace_premium_rounded, color: c.teal, size: 28),
              ),
              const SizedBox(width: KSpace.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      cert.eventTitle,
                      style: TextStyle(
                        color: c.text,
                        fontWeight: FontWeight.w700,
                        fontSize: 14,
                      ),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 2),
                    Text(
                      DateFormat('dd MMM yyyy').format(cert.issuedAt),
                      style: TextStyle(color: c.muted, fontSize: 12.5),
                    ),
                    if (cert.isRevoked)
                      Padding(
                        padding: const EdgeInsets.only(top: KSpace.xs),
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: KSpace.sm, vertical: 2),
                          decoration: BoxDecoration(
                            color: c.danger.withValues(alpha: 0.1),
                            borderRadius: BorderRadius.circular(KRadius.pill),
                          ),
                          child: Text(
                            'Revoked',
                            style: TextStyle(
                              color: c.danger,
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              Icon(Icons.chevron_right_rounded, color: c.muted),
            ],
          ),
        ),
      ),
      ),
    );
  }
}
