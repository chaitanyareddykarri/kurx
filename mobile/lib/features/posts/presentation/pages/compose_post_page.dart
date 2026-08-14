import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/util/content_type.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../common/widgets/kurx_card.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/post.dart';
import '../providers/posts_providers.dart';
import '../widgets/post_body_text.dart';

/// A file being attached. Holds the picked name until the confirm call returns real media, so the
/// chip can show progress and a failure stays attached to the file it belongs to.
class _Attachment {
  _Attachment(this.name);

  final String name;
  PostMedia? media;
  bool failed = false;

  bool get pending => media == null && !failed;
}

/// Compose a new post, or reshare [sharing] when one is passed in.
///
/// Media uploads two-step exactly like chat attachments (D-110): presign → the device PUTs bytes
/// straight to storage → confirm. Bytes never pass through the API, which is what keeps a
/// ten-image post from being a ten-image request body.
class ComposePostPage extends ConsumerStatefulWidget {
  const ComposePostPage({super.key, this.sharing, this.eventId});

  final Post? sharing;
  final String? eventId;

  @override
  ConsumerState<ComposePostPage> createState() => _ComposePostPageState();
}

class _ComposePostPageState extends ConsumerState<ComposePostPage> {
  final _body = TextEditingController();
  final _pollQuestion = TextEditingController();
  final _pollOptions = [TextEditingController(), TextEditingController()];

  late PostVisibility _visibility =
      widget.eventId != null ? PostVisibility.eventParticipants : PostVisibility.public;

  final List<_Attachment> _attachments = [];

  bool _pollOpen = false;
  bool _pollMultiple = false;
  bool _sending = false;
  String? _error;

  int get _imageCount =>
      _attachments.where((a) => a.media?.isImage ?? false).length;

  Future<void> _pickMedia() async {
    if (_attachments.length >= PostLimits.images) {
      setState(() => _error = 'too_many_media');
      return;
    }

    final picked = await FilePicker.pickFiles(type: FileType.any, withData: true);
    final file = picked?.files.singleOrNull;
    final bytes = file?.bytes;
    if (file == null || bytes == null) return;

    final attachment = _Attachment(file.name);
    setState(() {
      _attachments.add(attachment);
      _error = null;
    });

    try {
      final source = ref.read(postsSourceProvider);
      // The content type is a hint only — the server re-derives it from the bytes on confirm and
      // refuses anything that does not match, so guessing here cannot widen what is accepted.
      final contentType = guessContentType(file.extension);
      final ticket = await source.presignMedia(
        fileName: file.name,
        contentType: contentType,
        sizeBytes: bytes.length,
      );
      await source.uploadBytes(ticket, bytes, contentType);
      final media = await source.confirmMedia(ticket.mediaId, ticket.storageKey);
      if (mounted) setState(() => attachment.media = media);
    } on ApiError {
      if (mounted) setState(() => attachment.failed = true);
    }
  }

  @override
  void dispose() {
    _body.dispose();
    _pollQuestion.dispose();
    for (final c in _pollOptions) {
      c.dispose();
    }
    super.dispose();
  }

  bool get _canSend {
    if (_sending) return false;
    // An upload still in flight has no media id yet, so sending now would silently drop it.
    if (_attachments.any((a) => a.pending)) return false;
    if (widget.sharing != null) return true;
    if (_pollOpen) {
      return _pollOptions.where((c) => c.text.trim().isNotEmpty).length >= 2;
    }
    return _body.text.trim().isNotEmpty || _attachments.any((a) => a.media != null);
  }

  Future<void> _send() async {
    setState(() {
      _sending = true;
      _error = null;
    });

    try {
      final options = _pollOptions.map((c) => c.text.trim()).where((t) => t.isNotEmpty).toList();
      final media = _attachments.map((a) => a.media).nonNulls.toList();
      final created = await ref.read(postsSourceProvider).create(
            body: _body.text.trim(),
            kind: widget.sharing != null
                ? 'share'
                : _pollOpen
                    ? 'poll'
                    : media.any((m) => m.isVideo)
                        ? 'video'
                        : media.any((m) => m.isImage)
                            ? 'images'
                            : media.isNotEmpty
                                ? 'document'
                                : widget.eventId != null
                                    ? 'event'
                                    : 'text',
            visibility: _visibility.wire,
            mediaIds: media.map((m) => m.id).toList(),
            eventId: widget.eventId,
            sharedPostId: widget.sharing?.id,
            poll: _pollOpen
                ? {
                    'question': _pollQuestion.text.trim(),
                    'allowMultiple': _pollMultiple,
                    'options': options,
                  }
                : null,
          );
      if (mounted) Navigator.of(context).pop(created);
    } on ApiError catch (e) {
      setState(() {
        _sending = false;
        _error = e.code;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final sharing = widget.sharing;

    return Scaffold(
      appBar: AppBar(title: Text(sharing != null ? 'Share post' : 'New post')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: _body,
              maxLines: 6,
              maxLength: PostLimits.bodyMax,
              autofocus: true,
              onChanged: (_) => setState(() {}),
              decoration: InputDecoration(
                hintText: sharing != null
                    ? 'Add a comment (optional)'
                    : widget.eventId != null
                        ? 'Post to this event…'
                        : 'Share something with your network…',
                border: const OutlineInputBorder(),
              ),
            ),

            if (sharing != null) ...[
              const SizedBox(height: KSpace.md),
              KurxCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      sharing.author.name,
                      style: Theme.of(context)
                          .textTheme
                          .bodySmall
                          ?.copyWith(fontWeight: FontWeight.w600, color: c.text),
                    ),
                    const SizedBox(height: KSpace.xs),
                    PostBodyText(sharing.body, maxLines: 4, muted: true),
                  ],
                ),
              ),
            ],

            if (sharing == null) ...[
              if (_attachments.isNotEmpty) ...[
                const SizedBox(height: KSpace.md),
                Wrap(
                  spacing: KSpace.sm,
                  runSpacing: KSpace.sm,
                  children: [
                    for (final a in _attachments)
                      Chip(
                        avatar: a.pending
                            ? const SizedBox(
                                width: 14,
                                height: 14,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : Icon(
                                a.failed ? Icons.error_outline_rounded : Icons.check_rounded,
                                size: 16,
                                color: a.failed ? c.danger : c.success,
                              ),
                        label: Text(
                          a.name,
                          overflow: TextOverflow.ellipsis,
                        ),
                        onDeleted: () => setState(() => _attachments.remove(a)),
                      ),
                  ],
                ),
              ],
              const SizedBox(height: KSpace.sm),
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton.icon(
                  onPressed: _attachments.length >= PostLimits.images ? null : _pickMedia,
                  icon: const Icon(Icons.attach_file_rounded, size: 18),
                  label: Text(
                    _imageCount > 0
                        ? 'Add media (${_attachments.length}/${PostLimits.images})'
                        : 'Add media',
                  ),
                ),
              ),
              const SizedBox(height: KSpace.md),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                value: _pollOpen,
                title: const Text('Add a poll'),
                onChanged: (v) => setState(() => _pollOpen = v),
              ),
              if (_pollOpen) ...[
                TextField(
                  controller: _pollQuestion,
                  decoration: const InputDecoration(labelText: 'Question', border: OutlineInputBorder()),
                ),
                const SizedBox(height: KSpace.sm),
                for (var i = 0; i < _pollOptions.length; i++)
                  Padding(
                    padding: const EdgeInsets.only(bottom: KSpace.sm),
                    child: TextField(
                      controller: _pollOptions[i],
                      onChanged: (_) => setState(() {}),
                      decoration: InputDecoration(
                        labelText: 'Option ${i + 1}',
                        border: const OutlineInputBorder(),
                      ),
                    ),
                  ),
                Row(
                  children: [
                    TextButton.icon(
                      onPressed: _pollOptions.length >= 6
                          ? null
                          : () => setState(() => _pollOptions.add(TextEditingController())),
                      icon: const Icon(Icons.add_rounded, size: 16),
                      label: const Text('Add option'),
                    ),
                    const Spacer(),
                    Text('Allow multiple', style: Theme.of(context).textTheme.bodySmall),
                    Switch(
                      value: _pollMultiple,
                      onChanged: (v) => setState(() => _pollMultiple = v),
                    ),
                  ],
                ),
              ],
            ],

            const SizedBox(height: KSpace.md),
            DropdownButtonFormField<PostVisibility>(
              initialValue: _visibility,
              decoration: const InputDecoration(labelText: 'Who can see this', border: OutlineInputBorder()),
              items: [
                for (final v in PostVisibility.values)
                  if (v != PostVisibility.eventParticipants || widget.eventId != null)
                    DropdownMenuItem(value: v, child: Text(v.label)),
              ],
              onChanged: (v) => setState(() => _visibility = v ?? _visibility),
            ),

            if (_error != null) ...[
              const SizedBox(height: KSpace.md),
              Text(_error!, style: TextStyle(color: c.danger)),
            ],

            const SizedBox(height: KSpace.xl),
            KurxButton(
              label: sharing != null ? 'Share' : 'Post',
              expand: true,
              loading: _sending,
              onPressed: _canSend ? _send : null,
            ),
          ],
        ),
      ),
    );
  }
}
