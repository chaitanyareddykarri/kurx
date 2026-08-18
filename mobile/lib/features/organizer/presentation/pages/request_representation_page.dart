import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/org_dto.dart';
import '../providers/event_content_providers.dart';
import '../providers/organizer_providers.dart';

/// The registry's type vocabulary, shared with Create Event's Representing step — see [kOrgTypes].
const _orgTypes = kOrgTypes;

/// **Request to represent an organization** — D-074/D-075, and D-382 on Flutter.
///
/// A college, company, club or community not yet on Kurx. Submitting stages a HIDDEN `PendingReview`
/// organization plus the proof; an admin verifies it before it joins the registry, and the caller
/// becomes a Verified Representative on approval — never an owner, because organizations have no
/// account.
///
/// This screen did not exist on Flutter, and its absence was a closed loop rather than a missing
/// convenience: since D-379 no event can be created without a verified organization, the wizard sent
/// an organiser with none to "your profile", and the profile's Representing page sent them back to
/// event creation. Web has had `/host/representing/new` throughout.
class RequestRepresentationPage extends ConsumerStatefulWidget {
  const RequestRepresentationPage({super.key});

  @override
  ConsumerState<RequestRepresentationPage> createState() => _RequestRepresentationPageState();
}

class _RequestRepresentationPageState extends ConsumerState<RequestRepresentationPage> {
  final _name = TextEditingController();
  final _domain = TextEditingController();
  String _type = 'college';

  List<int>? _proofBytes;
  String? _proofName;
  String _proofContentType = 'application/octet-stream';

  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _name.dispose();
    _domain.dispose();
    super.dispose();
  }

  static const _proofTypes = ['pdf', 'jpg', 'jpeg', 'png'];

  Future<void> _pickProof() async {
    final picked = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: _proofTypes,
      withData: true,
    );
    final file = picked?.files.singleOrNull;
    final bytes = file?.bytes;
    if (file == null || bytes == null) return;

    // The extension filter is advisory on some platforms, so it is checked rather than trusted.
    final ext = file.extension?.toLowerCase();
    if (!_proofTypes.contains(ext)) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('Attach a PDF or an image.')));
      }
      return;
    }
    setState(() {
      _proofBytes = bytes;
      _proofName = file.name;
      _proofContentType = ext == 'pdf' ? 'application/pdf' : 'image/${ext == 'jpg' ? 'jpeg' : ext}';
    });
  }

  /// Both are what the server requires: a name, and evidence. A request with no proof is a claim the
  /// admin has nothing to verify against, which is a rejection with extra steps.
  String? get _missing {
    if (_name.text.trim().isEmpty) return 'Enter the organization name';
    if (_proofBytes == null) return 'Attach proof of affiliation';
    return null;
  }

  Future<void> _submit() async {
    final missing = _missing;
    if (missing != null) {
      setState(() => _error = missing);
      return;
    }
    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      final orgs = ref.read(orgSourceProvider);
      // The bytes go straight to storage; only the key it returns is submitted.
      final presign = await orgs.presignRepresentationDoc(_proofContentType, _proofBytes!.length);
      await ref
          .read(eventContentSourceProvider)
          .uploadToPresigned(presign, _proofBytes!, _proofContentType);
      await orgs.submitRepresentationRequest({
        'name': _name.text.trim(),
        'type': _type,
        'primaryDomain': _domain.text.trim().isEmpty ? null : _domain.text.trim(),
        'documents': [
          {'docType': 'letterhead', 'storageKey': presign.key},
        ],
      });
      // The staged organization joins the caller's representation list immediately, as PendingReview —
      // visible on the wizard's Representing step, and not selectable until an admin approves it.
      ref.invalidate(myRepresentationsProvider);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Request submitted. An admin verifies the organization.')),
      );
      // Back to wherever this was opened from — the wizard's Representing step, or the profile list.
      if (context.canPop()) {
        context.pop();
      } else {
        context.go('/representing');
      }
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _error = e.userMessage;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Represent an organization')),
      body: ListView(
        padding: const EdgeInsets.all(KSpace.lg),
        children: [
          Text(
            'A college, company, club, or community not yet on Kurx. This submits a representation '
            'request an admin verifies before it joins the registry — you become a Verified '
            'Representative on approval.',
            style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
          ),
          const SizedBox(height: KSpace.lg),

          TextField(
            controller: _name,
            enabled: !_submitting,
            onChanged: (_) => setState(() {}),
            decoration: const InputDecoration(
              labelText: 'Organization name',
              hintText: 'e.g. NSRIT College',
            ),
          ),
          const SizedBox(height: KSpace.md),

          DropdownButtonFormField<String>(
            initialValue: _type,
            decoration: const InputDecoration(labelText: 'Type'),
            items: [for (final t in _orgTypes) DropdownMenuItem(value: t, child: Text(t))],
            onChanged: _submitting ? null : (v) => setState(() => _type = v ?? 'college'),
          ),
          const SizedBox(height: KSpace.md),

          TextField(
            controller: _domain,
            enabled: !_submitting,
            decoration: const InputDecoration(
              labelText: 'Organisation email domain (optional)',
              hintText: 'nsrit.edu.in',
            ),
          ),
          const SizedBox(height: KSpace.lg),

          Text('Proof of affiliation',
              style: TextStyle(color: c.text, fontSize: 13, fontWeight: FontWeight.w600)),
          const SizedBox(height: KSpace.xs),
          Text(
            'A letterhead, official document or authorization proof showing you represent this '
            'organization. PDF or image.',
            style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
          ),
          const SizedBox(height: KSpace.sm),
          KurxButton(
            label: _proofName == null ? 'Choose file' : 'Replace file',
            variant: KurxButtonVariant.secondary,
            onPressed: _submitting ? null : _pickProof,
          ),
          if (_proofName != null) ...[
            const SizedBox(height: KSpace.xs),
            Text(_proofName!, style: TextStyle(color: c.text, fontSize: 12.5)),
          ],

          if (_error != null) ...[
            const SizedBox(height: KSpace.md),
            Text(_error!, style: TextStyle(color: c.danger, fontSize: 13)),
          ],

          const SizedBox(height: KSpace.xl),
          // The reason sits beside the control: a disabled button cannot explain itself.
          if (_missing != null)
            Padding(
              padding: const EdgeInsets.only(bottom: KSpace.sm),
              child: Text(_missing!, style: TextStyle(color: c.muted, fontSize: 12.5)),
            ),
          KurxButton(
            label: 'Register for verification',
            loading: _submitting,
            onPressed: _submitting || _missing != null ? null : _submit,
          ),
        ],
      ),
    );
  }
}
