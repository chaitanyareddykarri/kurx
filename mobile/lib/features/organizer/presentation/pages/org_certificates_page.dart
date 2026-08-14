import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_content_dto.dart';
import '../providers/event_content_providers.dart';
import '../widgets/organizer_form_sheet.dart';

/// Organiser certificates — `CertificateEndpoints` roster, bulk generate and revoke.
///
/// Generate is issued against everyone eligible on the event, so it is confirmation-gated: the
/// backend returns how many it created and the roster is re-read from the server rather than
/// optimistically extended, because eligibility is decided server-side.
class OrgCertificatesPage extends ConsumerWidget {
  const OrgCertificatesPage({super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Certificates'),
        actions: [
          TextButton.icon(
            onPressed: () => _generate(context, ref),
            icon: const Icon(Icons.workspace_premium_outlined, size: 18),
            label: const Text('Generate'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(certificateRosterProvider(eventId));
          await ref.read(certificateRosterProvider(eventId).future);
        },
        child: AsyncValueView(
          value: ref.watch(certificateRosterProvider(eventId)),
          onRetry: () => ref.invalidate(certificateRosterProvider(eventId)),
          isEmpty: (list) => list.isEmpty,
          empty: EmptyState(
            icon: Icons.workspace_premium_outlined,
            title: 'No certificates issued',
            message: 'Generate certificates for everyone who attended this event.',
            actionLabel: 'Generate',
            onAction: () => _generate(context, ref),
          ),
          data: (roster) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: roster.length,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.sm),
            itemBuilder: (_, i) => _CertificateTile(
              certificate: roster[i],
              onRevoke: roster[i].isRevoked
                  ? null
                  : () => _revoke(context, ref, roster[i]),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _generate(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Generate certificates'),
        content: const Text(
          'This issues a certificate to every eligible attendee on this event. '
          'Attendees who already hold one are skipped.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(ctx).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(ctx).pop(true), child: const Text('Generate')),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;

    try {
      final count = await ref.read(eventContentActionsProvider).generateCertificates(eventId);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              count == 0
                  ? 'No new certificates — everyone eligible already has one.'
                  : 'Issued $count certificate${count == 1 ? '' : 's'}.',
            ),
          ),
        );
      }
    } on ApiError catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
      }
    }
  }

  Future<void> _revoke(BuildContext context, WidgetRef ref, CertificateRosterDto cert) async {
    final reason = TextEditingController();
    await OrganizerFormSheet.show(
      context,
      title: 'Revoke certificate',
      submitLabel: 'Revoke',
      successMessage: 'Certificate revoked.',
      fields: (enabled) => [
        Text(
          'Revoking ${cert.holderName ?? 'this certificate'} keeps it visible on the public '
          'verify page, marked as revoked — it is never silently hidden.',
          style: TextStyle(color: context.kurx.muted),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: reason,
          enabled: enabled,
          textCapitalization: TextCapitalization.sentences,
          decoration: const InputDecoration(labelText: 'Reason', hintText: 'Why is this revoked?'),
        ),
      ],
      onSubmit: () => ref
          .read(eventContentActionsProvider)
          .revokeCertificate(eventId, cert.id, reason.text.trim()),
    );
  }
}

class _CertificateTile extends StatelessWidget {
  const _CertificateTile({required this.certificate, required this.onRevoke});

  final CertificateRosterDto certificate;
  final VoidCallback? onRevoke;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final scheme = Theme.of(context).colorScheme;
    final issued = certificate.issuedAt;

    final bits = [
      if (certificate.kind != null && certificate.kind!.isNotEmpty) certificate.kind,
      if (certificate.verifyCode != null) certificate.verifyCode,
      if (issued != null) DateFormat('d MMM yyyy').format(issued.toLocal()),
    ].whereType<String>().join(' · ');

    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: KSpace.xs),
      tileColor: c.cardSurface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(KRadius.md)),
      leading: CircleAvatar(
        backgroundColor: (certificate.isRevoked ? scheme.error : c.accent).withValues(alpha: 0.15),
        child: Icon(
          certificate.isRevoked ? Icons.block_rounded : Icons.verified_outlined,
          color: certificate.isRevoked ? scheme.error : c.accent,
          size: 20,
        ),
      ),
      title: Text(certificate.holderName ?? 'Unnamed holder', style: TextStyle(color: c.text)),
      subtitle: Text(
        certificate.isRevoked
            ? 'Revoked${certificate.revokedReason != null ? ' · ${certificate.revokedReason}' : ''}'
            : bits,
        style: TextStyle(color: certificate.isRevoked ? scheme.error : c.muted),
      ),
      trailing: onRevoke == null
          ? null
          : IconButton(
              onPressed: onRevoke,
              icon: const Icon(Icons.block_outlined),
              tooltip: 'Revoke certificate',
            ),
    );
  }
}
