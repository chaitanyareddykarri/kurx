import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/certificates_providers.dart';

class CertificateDetailPage extends ConsumerWidget {
  const CertificateDetailPage({super.key, required this.code});
  final String code;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Certificate'),
        // A share IconButton with an empty `onPressed` sat here. Removed rather than built: a
        // share needs a public URL for the credential, and mobile has no configured web base to
        // derive one from — constructing one would be inventing where this certificate lives.
      ),
      body: AsyncValueView(
        value: ref.watch(certificateDetailProvider(code)),
        onRetry: () => ref.invalidate(certificateDetailProvider(code)),
        data: (cert) => SingleChildScrollView(
          padding: const EdgeInsets.all(KSpace.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Certificate preview card
              DecoratedBox(
                decoration: BoxDecoration(
                  color: c.cardSurface,
                  borderRadius: BorderRadius.circular(KRadius.xl),
                  boxShadow: kCardShadow(context),
                  border: Border.all(
                    color: c.teal.withValues(alpha: 0.25),
                    width: 1.5,
                  ),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(KSpace.xl),
                  child: Column(
                    children: [
                      Container(
                        width: 72,
                        height: 72,
                        decoration: BoxDecoration(
                          gradient: LinearGradient(
                            colors: [c.teal, c.accent],
                            begin: Alignment.topLeft,
                            end: Alignment.bottomRight,
                          ),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(
                          Icons.workspace_premium_rounded,
                          color: Colors.white,
                          size: 36,
                        ),
                      ),
                      const SizedBox(height: KSpace.lg),
                      Text(
                        'Certificate of Participation',
                        style: TextStyle(
                          color: c.muted,
                          fontSize: 12,
                          letterSpacing: 1.5,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(height: KSpace.sm),
                      Text(
                        cert.eventTitle,
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          color: c.text,
                          fontSize: 20,
                          fontWeight: FontWeight.w800,
                          height: 1.2,
                        ),
                      ),
                      const SizedBox(height: KSpace.md),
                      Text(
                        DateFormat('MMMM dd, yyyy').format(cert.issuedAt),
                        style: TextStyle(color: c.muted, fontSize: 13),
                      ),
                      if (cert.verifyCode != null) ...[
                        const SizedBox(height: KSpace.lg),
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: KSpace.lg, vertical: KSpace.sm),
                          decoration: BoxDecoration(
                            color: c.elevated,
                            borderRadius: BorderRadius.circular(KRadius.md),
                          ),
                          child: Text(
                            cert.verifyCode!,
                            style: TextStyle(
                              color: c.muted,
                              fontSize: 12,
                              letterSpacing: 2,
                              fontFamily: 'monospace',
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
              const SizedBox(height: KSpace.xl),
              // Share icons row
                  // A row of four branded share buttons — WhatsApp, LinkedIn, Instagram, Copy
                  // Link — every one of them `onTap: () {}`. Somebody trying to share the
                  // credential they had just earned failed four different ways, each with a
                  // convincing brand colour behind it.
                  //
                  // Removed, not implemented, for the same reason as the app-bar button: every one
                  // needs a public URL for this certificate and mobile has no web base configured.
                  // Instagram has no share URL scheme at all, so that one could not be honest even
                  // with a base. Download below is kept because `pdfUrl` is a real server-issued
                  // URL and needs nothing invented.
              const SizedBox(height: KSpace.xl),
              if (cert.pdfUrl != null)
                OutlinedButton.icon(
                  // Was `onPressed: () {}` — gated on `pdfUrl != null`, so it only ever appeared
                  // when there WAS a document, and then did nothing with it.
                  onPressed: () => launchUrl(
                    Uri.parse(cert.pdfUrl!),
                    mode: LaunchMode.externalApplication,
                  ),
                  icon: const Icon(Icons.download_outlined),
                  label: const Text('Download PDF'),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
