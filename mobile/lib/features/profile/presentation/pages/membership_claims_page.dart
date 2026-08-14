import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/util/content_type.dart';
import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../organizer/data/models/event_content_dto.dart';
import '../../../organizer/presentation/providers/event_content_providers.dart';

/// My membership claims — `MembershipClaimEndpoints`, the caller's own claims to represent an
/// organisation (`GET /v1/me/membership-claims`).
///
/// **Read-only by design, and that is a real constraint, not a stub.** Submitting a claim needs an
/// `orgId`, and mobile has no organisation registry search: `GET /v1/orgs/search` is a *public,
/// Verified-only* registry and `GET /v1/orgs` returns only orgs you already belong to — neither
/// can find the organisation you are claiming to be part of. Submission is therefore reached from
/// an organisation's own page, where the id is already known, rather than faked with a free-text
/// org field that could not resolve to anything.
class MembershipClaimsPage extends ConsumerWidget {
  const MembershipClaimsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Membership claims')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(myMembershipClaimsProvider);
          await ref.read(myMembershipClaimsProvider.future);
        },
        child: AsyncValueView(
          value: ref.watch(myMembershipClaimsProvider),
          onRetry: () => ref.invalidate(myMembershipClaimsProvider),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.badge_outlined,
            title: 'No claims yet',
            message:
                'Claim membership from an organisation’s page to represent it on Kurx.',
          ),
          data: (claims) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: claims.length,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
            itemBuilder: (_, i) => _ClaimCard(claim: claims[i]),
          ),
        ),
      ),
    );
  }
}

class _ClaimCard extends StatelessWidget {
  const _ClaimCard({required this.claim});
  final MembershipClaimDto claim;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final scheme = Theme.of(context).colorScheme;
    final created = claim.createdAt;

    final (Color tone, IconData icon, String label) = switch (claim.status) {
      'approved' => (c.success, Icons.verified_rounded, 'Approved'),
      'rejected' => (scheme.error, Icons.cancel_outlined, 'Rejected'),
      'under_review' => (c.accent, Icons.hourglass_top_rounded, 'Under review'),
      'official_contact_verification' => (
          c.accent,
          Icons.mark_email_read_outlined,
          'Verifying with the organisation'
        ),
      _ => (c.muted, Icons.schedule_rounded, 'Submitted'),
    };

    return Container(
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
              Expanded(
                child: Text(
                  claim.orgName ?? 'Organisation',
                  style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 16),
                ),
              ),
              Icon(icon, size: 18, color: tone),
              const SizedBox(width: KSpace.xs),
              Text(label, style: TextStyle(color: tone, fontWeight: FontWeight.w600, fontSize: 13)),
            ],
          ),
          const SizedBox(height: KSpace.xs),
          Text(
            'Claimed role: ${claim.claimedRole}',
            style: TextStyle(color: c.muted, fontSize: 13),
          ),
          if (claim.fastTrack) ...[
            const SizedBox(height: KSpace.xs),
            Text('Fast-tracked', style: TextStyle(color: c.accent, fontSize: 13)),
          ],
          if (created != null) ...[
            const SizedBox(height: KSpace.xs),
            Text(
              'Submitted ${DateFormat('d MMM yyyy').format(created.toLocal())}',
              style: TextStyle(color: c.muted, fontSize: 13),
            ),
          ],
          if (claim.notes != null && claim.notes!.isNotEmpty) ...[
            const SizedBox(height: KSpace.sm),
            Container(
              padding: const EdgeInsets.all(KSpace.md),
              decoration: BoxDecoration(
                color: tone.withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(KRadius.sm),
              ),
              child: Text(claim.notes!, style: TextStyle(color: c.text, fontSize: 13)),
            ),
          ],
        ],
      ),
    );
  }
}

/// Submit a membership claim for a **known** organisation.
///
/// Evidence is mandatory: the backend rejects an evidence-less claim with `invalid_evidence`, the
/// exact trap that made the web claim form always fail (D-055 G4). The document is uploaded to a
/// claim-scoped presigned URL first, and only the resulting storage key is submitted.
Future<bool?> showSubmitMembershipClaimSheet(
  BuildContext context,
  WidgetRef ref, {
  required String orgId,
  required String orgName,
}) {
  return showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    backgroundColor: context.kurx.cardSurface,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(KRadius.xl)),
    ),
    builder: (_) => _ClaimSheet(orgId: orgId, orgName: orgName, ref: ref),
  );
}

class _ClaimSheet extends StatefulWidget {
  const _ClaimSheet({required this.orgId, required this.orgName, required this.ref});

  static const roles = ['staff', 'manager', 'owner', 'finance'];

  final String orgId;
  final String orgName;
  final WidgetRef ref;

  @override
  State<_ClaimSheet> createState() => _ClaimSheetState();
}

class _ClaimSheetState extends State<_ClaimSheet> {
  String _role = _ClaimSheet.roles.first;
  String? _documentKey;
  String? _documentName;
  bool _busy = false;
  String? _error;

  Future<void> _pickDocument() async {
    final picked = await FilePicker.pickFiles(type: FileType.any, withData: true);
    final file = picked?.files.singleOrNull;
    final bytes = file?.bytes;
    if (file == null || bytes == null) return;

    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final key = await widget.ref.read(eventContentActionsProvider).uploadClaimDocument(
            widget.orgId,
            bytes: bytes,
            contentType: guessContentType(file.extension),
          );
      if (!mounted) return;
      setState(() {
        _documentKey = key;
        _documentName = file.name;
        _busy = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = 'Could not upload that document. Please try again.';
      });
    }
  }

  Future<void> _submit() async {
    final key = _documentKey;
    if (key == null) {
      setState(() => _error = 'Attach proof of your membership before submitting.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.ref.read(eventContentActionsProvider).submitMembershipClaim(
        widget.orgId,
        claimedRole: _role,
        documents: [
          {'kind': 'membership_proof', 'storageKey': key},
        ],
      );
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = 'Could not submit the claim. Please try again.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: EdgeInsets.fromLTRB(
        KSpace.lg,
        KSpace.lg,
        KSpace.lg,
        MediaQuery.viewInsetsOf(context).bottom + KSpace.lg,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Claim membership',
                style: TextStyle(color: c.text, fontWeight: FontWeight.w800, fontSize: 17)),
            const SizedBox(height: KSpace.xs),
            Text(widget.orgName, style: TextStyle(color: c.muted)),
            const SizedBox(height: KSpace.lg),
            DropdownButtonFormField<String>(
              initialValue: _role,
              decoration: const InputDecoration(labelText: 'Role you are claiming'),
              items: [
                for (final r in _ClaimSheet.roles)
                  DropdownMenuItem(
                    value: r,
                    child: Text(r[0].toUpperCase() + r.substring(1)),
                  ),
              ],
              onChanged: _busy ? null : (v) => setState(() => _role = v ?? _role),
            ),
            const SizedBox(height: KSpace.lg),
            OutlinedButton.icon(
              onPressed: _busy ? null : _pickDocument,
              icon: const Icon(Icons.attach_file_rounded, size: 18),
              label: Text(_documentName ?? 'Attach proof (required)'),
            ),
            if (_error != null) ...[
              const SizedBox(height: KSpace.md),
              Semantics(
                liveRegion: true,
                child: Text(_error!,
                    style: TextStyle(color: Theme.of(context).colorScheme.error)),
              ),
            ],
            const SizedBox(height: KSpace.lg),
            FilledButton(
              onPressed: _busy ? null : _submit,
              child: _busy
                  ? const SizedBox(
                      height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Submit claim'),
            ),
          ],
        ),
      ),
    );
  }
}
