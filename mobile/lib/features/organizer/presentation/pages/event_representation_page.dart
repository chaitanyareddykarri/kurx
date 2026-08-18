import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_content_dto.dart';
import '../../domain/event_wizard_payload.dart';
import '../providers/event_content_providers.dart';
import '../providers/organizer_providers.dart';

/// **Edit Event ▸ Representing** — the correction path, and nothing else.
///
/// Representation is answered ONCE, on the Representing step of Create Event: the organisation the
/// event is run on behalf of, and this event's own authorisation letter. This screen exists for the one
/// case that re-opens it — a reviewer rejected the letter or asked for changes — which is an edit of
/// the event, not a standing workspace section. It used to be `/events/:id/manage/representing`, a
/// permanent tile in the manage hub, and a second permanent place to answer a question creation had
/// already answered is what made organisers believe creation was unfinished.
///
/// Reached from Event Status, where the verdict that needs acting on is shown. Nothing else links here,
/// and no other organiser screen asks for a signatory, a designation, an official contact or a letter —
/// asking twice is how one `event_authorizations` row ends up with two different answers.
///
/// The organisation itself is fixed at creation — `Event.RepresentingOrgId` has no update path — so
/// this states which one it is rather than offering to change it.
class EventRepresentationPage extends ConsumerStatefulWidget {
  const EventRepresentationPage({super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  ConsumerState<EventRepresentationPage> createState() => _EventRepresentationPageState();
}

class _EventRepresentationPageState extends ConsumerState<EventRepresentationPage> {
  final _headName = TextEditingController();
  final _headDesignation = TextEditingController();
  final _officialEmail = TextEditingController();
  final _officialPhone = TextEditingController();
  final _representativeRoleOther = TextEditingController();
  String? _representativeRole;

  List<int>? _letterBytes;
  String? _letterName;
  String _letterContentType = 'application/octet-stream';

  bool _submitting = false;
  String? _error;

  /// Prefill runs once, from whatever was on file when the screen opened. Re-running it on every build
  /// would overwrite what the organiser is typing each time the provider re-emits.
  bool _prefilled = false;

  @override
  void dispose() {
    for (final c in [
      _headName,
      _headDesignation,
      _officialEmail,
      _officialPhone,
      _representativeRoleOther,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  void _prefill(EventAuthorizationDto? filed) {
    if (_prefilled || filed == null) return;
    _prefilled = true;
    _headName.text = filed.headName;
    _headDesignation.text = filed.headDesignation;
    _officialEmail.text = filed.officialEmail;
    _officialPhone.text = filed.officialPhone ?? '';
    _representativeRoleOther.text = filed.representativeRoleOther ?? '';
    _representativeRole = filed.representativeRole.isEmpty ? null : filed.representativeRole;
  }

  static const _letterTypes = ['pdf', 'jpg', 'jpeg', 'png'];

  Future<void> _pickLetter() async {
    final picked = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: _letterTypes,
      withData: true,
    );
    final file = picked?.files.singleOrNull;
    final bytes = file?.bytes;
    if (file == null || bytes == null) return;

    // The extension filter is advisory on some platforms, so it is checked rather than trusted.
    final ext = file.extension?.toLowerCase();
    if (!_letterTypes.contains(ext)) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('Attach a PDF or an image.')));
      }
      return;
    }
    setState(() {
      _letterBytes = bytes;
      _letterName = file.name;
      _letterContentType = ext == 'pdf' ? 'application/pdf' : 'image/${ext == 'jpg' ? 'jpeg' : ext}';
    });
  }

  /// The same per-field rules the wizard's Representing step runs — one validator, so a letter that
  /// passes there cannot be refused here (or the reverse).
  Map<String, String> _errorsFor(EventAuthorizationDto? filed) => validateEventAuthorization(
        headName: _headName.text,
        headDesignation: _headDesignation.text,
        officialEmail: _officialEmail.text,
        officialPhone: _officialPhone.text,
        representativeRole: _representativeRole,
        representativeRoleOther: _representativeRoleOther.text,
        // Only a FIRST filing must carry a letter. On a re-file an omitted key means "keep the one on
        // file" — the client never holds the stored key — so demanding a re-upload to correct a phone
        // number would be asking for the same document twice.
        letterAttached: _letterBytes != null || filed?.letterheadUrl != null,
      );

  Future<void> _submit(EventAuthorizationDto? filed) async {
    final errors = _errorsFor(filed);
    if (errors.isNotEmpty) {
      setState(() => _error = errors.values.first);
      return;
    }
    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      final content = ref.read(eventContentSourceProvider);
      String? letterheadDocumentKey;
      if (_letterBytes != null) {
        final presign = await content.presignAuthorizationDoc(
            widget.eventId, _letterContentType, _letterBytes!.length);
        await content.uploadToPresigned(presign, _letterBytes!, _letterContentType);
        letterheadDocumentKey = presign.key;
      }
      await content.submitAuthorization(widget.eventId, {
        'headName': _headName.text.trim(),
        'headDesignation': _headDesignation.text.trim(),
        'officialEmail': _officialEmail.text.trim(),
        'officialPhone': _officialPhone.text.trim(),
        'representativeRole': _representativeRole ?? '',
        'representativeRoleOther': _representativeRoleOther.text.trim().isEmpty
            ? null
            : _representativeRoleOther.text.trim(),
        'letterheadDocumentKey': letterheadDocumentKey,
      });
      // One row per event: re-filing REPLACES what is on file rather than adding a second record.
      ref.invalidate(eventAuthorizationProvider(widget.eventId));
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _letterBytes = null;
        _letterName = null;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Authorization filed. A reviewer reads it with the event.')),
      );
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
      appBar: AppBar(title: const Text('Representing')),
      body: AsyncValueView(
        value: ref.watch(eventAuthorizationProvider(widget.eventId)),
        onRetry: () => ref.invalidate(eventAuthorizationProvider(widget.eventId)),
        data: (filed) {
          _prefill(filed);
          final errors = _errorsFor(filed);
          final roles = ref.watch(representativeRolesProvider);
          final org = ref.watch(orgDetailProvider(widget.orgId));

          return ListView(
            padding: const EdgeInsets.all(KSpace.lg),
            children: [
              _StatusBanner(filed: filed),
              const SizedBox(height: KSpace.lg),

              // Which organization, and whether Kurx has verified it. Stated, never editable: an event
              // that needs a different organization is a different event.
              org.maybeWhen(
                data: (o) => Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(o.name,
                        style: TextStyle(color: c.text, fontSize: 16, fontWeight: FontWeight.w700)),
                    const SizedBox(height: KSpace.xs),
                    Text('Organization verification: ${o.verificationStatus}',
                        style: TextStyle(color: c.muted, fontSize: 12.5)),
                  ],
                ),
                orElse: () => const SizedBox.shrink(),
              ),
              const SizedBox(height: KSpace.xs),
              Text(
                'The organization an event represents is chosen when the event is created and cannot '
                'be changed afterwards.',
                style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
              ),

              if (filed?.reasonCode != null || filed?.notes != null) ...[
                const SizedBox(height: KSpace.lg),
                Container(
                  padding: const EdgeInsets.all(KSpace.md),
                  decoration: BoxDecoration(
                    color: c.accent.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(KRadius.md),
                    border: Border.all(color: c.accent.withValues(alpha: 0.3)),
                  ),
                  // The reviewer's own words. "Changes requested" with no reason is a verdict the
                  // organiser cannot act on.
                  child: Text(
                    [filed?.reasonCode, filed?.notes].whereType<String>().join(' — '),
                    style: TextStyle(color: c.text, fontSize: 12.5, height: 1.35),
                  ),
                ),
              ],

              const SizedBox(height: KSpace.xl),
              const Divider(),
              const SizedBox(height: KSpace.lg),

              Text('Who signed for the organization',
                  style: TextStyle(color: c.text, fontSize: 14, fontWeight: FontWeight.w700)),
              const SizedBox(height: KSpace.sm),
              _field(_headName, "Signatory's name", errors['headName']),
              _field(_headDesignation, 'Their designation', errors['headDesignation']),
              _field(_officialEmail, 'Official email', errors['officialEmail'],
                  keyboard: TextInputType.emailAddress),
              _field(_officialPhone, 'Official phone', errors['officialPhone'],
                  hint: '+919876543210', keyboard: TextInputType.phone),

              const SizedBox(height: KSpace.md),
              Text('Your role in this organization',
                  style: TextStyle(color: c.text, fontSize: 13, fontWeight: FontWeight.w600)),
              // The server's vocabulary, never a copy held here.
              roles.maybeWhen(
                data: (list) => DropdownButtonFormField<String>(
                  initialValue: _representativeRole,
                  items: [for (final r in list) DropdownMenuItem(value: r, child: Text(r))],
                  onChanged: (v) => setState(() => _representativeRole = v),
                ),
                orElse: () => const LinearProgressIndicator(),
              ),
              if (errors['representativeRole'] != null)
                Padding(
                  padding: const EdgeInsets.only(top: KSpace.xs),
                  child: Text(errors['representativeRole']!,
                      style: TextStyle(color: c.danger, fontSize: 12)),
                ),
              if (_representativeRole == 'Other') ...[
                const SizedBox(height: KSpace.md),
                _field(_representativeRoleOther, 'Describe your role',
                    errors['representativeRoleOther']),
              ],

              const SizedBox(height: KSpace.lg),
              Text('Authorization letter',
                  style: TextStyle(color: c.text, fontSize: 13, fontWeight: FontWeight.w600)),
              const SizedBox(height: KSpace.xs),
              Text(
                "On the organization's letterhead, naming this event and its dates. PDF or image, up "
                "to 10 MB. A reviewer reads it as part of this event's review.",
                style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
              ),
              if (filed?.letterheadUrl != null) ...[
                const SizedBox(height: KSpace.xs),
                Text('A letter is already on file. Attach one only to replace it.',
                    style: TextStyle(color: c.muted, fontSize: 12.5)),
              ],
              const SizedBox(height: KSpace.sm),
              KurxButton(
                label: _letterName == null ? 'Choose file' : 'Replace file',
                variant: KurxButtonVariant.secondary,
                onPressed: _submitting ? null : _pickLetter,
              ),
              if (_letterName != null) ...[
                const SizedBox(height: KSpace.xs),
                Text(_letterName!, style: TextStyle(color: c.text, fontSize: 12.5)),
              ],
              if (errors['letterFile'] != null) ...[
                const SizedBox(height: KSpace.xs),
                Text(errors['letterFile']!, style: TextStyle(color: c.danger, fontSize: 12)),
              ],

              if (_error != null) ...[
                const SizedBox(height: KSpace.md),
                Text(_error!, style: TextStyle(color: c.danger, fontSize: 13)),
              ],

              const SizedBox(height: KSpace.xl),
              // Re-filing CLEARS the verdict server-side — `EventAuthorizationService.Apply` resets the
              // row to `Submitted` and drops the reviewer, timestamp and notes, so an approved letter
              // cannot be swapped after the fact and published on the old decision. Correct, and
              // expensive: fixing a typo costs the approval and the event's permission to publish. Said
              // out loud, because the button alone reads as "save".
              if (filed?.status == 'Approved') ...[
                Text(
                  'This authorization is approved. Filing it again replaces the letter on record and '
                  'sends it back for review — the event cannot publish until a reviewer approves the '
                  'new one.',
                  style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
                ),
                const SizedBox(height: KSpace.sm),
              ],
              KurxButton(
                label: filed == null
                    ? 'File authorization'
                    : filed.status == 'Approved'
                        ? 'Replace and re-submit'
                        : 'Update authorization',
                loading: _submitting,
                onPressed: _submitting ? null : () => _submit(filed),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _field(TextEditingController controller, String label, String? error,
      {String? hint, TextInputType? keyboard}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.md),
      child: TextField(
        controller: controller,
        keyboardType: keyboard,
        enabled: !_submitting,
        // The rules run on every keystroke, so an error clears as soon as it is fixed.
        onChanged: (_) => setState(() {}),
        decoration: InputDecoration(labelText: label, hintText: hint, errorText: error),
      ),
    );
  }
}

/// What the server says about the filing, in the words the organiser needs. `Submitted` is the one
/// that is not self-evident: it means filed and queued, not being read — a reviewer opens it as part of
/// the event's review.
class _StatusBanner extends StatelessWidget {
  const _StatusBanner({required this.filed});
  final EventAuthorizationDto? filed;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final (label, message, color) = switch (filed?.status) {
      null => (
          'Action required',
          "This event's organization authorization hasn't been filed yet. It can't be submitted for "
              'review without it.',
          c.danger
        ),
      'Approved' => (
          'Approved',
          'The event is authorized to represent this organization.',
          c.success
        ),
      'Rejected' => (
          'Action required',
          'The authorization was rejected. File a corrected one before this event can go live.',
          c.danger
        ),
      'ChangesRequested' => (
          'Action required',
          'A reviewer asked for changes to the authorization.',
          c.accent
        ),
      _ => (
          'Pending review',
          'Your organization representation is awaiting verification — a reviewer reads it as part of '
              "this event's review.",
          c.accent
        ),
    };

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(KRadius.lg),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: TextStyle(color: color, fontSize: 14, fontWeight: FontWeight.w700)),
          const SizedBox(height: KSpace.xs),
          Text(message, style: TextStyle(color: c.text, fontSize: 12.5, height: 1.35)),
        ],
      ),
    );
  }
}
