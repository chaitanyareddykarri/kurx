import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/post.dart';
import '../providers/posts_providers.dart';

/// Edit a post's body and audience.
///
/// Only those two: media, poll, event attachment and kind are immutable after creation, so the
/// screen offers nothing it cannot actually change. Saving stamps `edited_at`, which every card
/// renders — an edit is visible, never silent.
class EditPostPage extends ConsumerStatefulWidget {
  const EditPostPage({super.key, required this.post});

  final Post post;

  @override
  ConsumerState<EditPostPage> createState() => _EditPostPageState();
}

class _EditPostPageState extends ConsumerState<EditPostPage> {
  late final _body = TextEditingController(text: widget.post.body);
  late PostVisibility _visibility = PostVisibility.fromWire(widget.post.visibility);

  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _body.dispose();
    super.dispose();
  }

  bool get _dirty =>
      _body.text.trim() != widget.post.body.trim() ||
      _visibility.wire != widget.post.visibility;

  Future<void> _save() async {
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final updated = await ref.read(postsSourceProvider).update(
            widget.post.id,
            body: _body.text.trim(),
            visibility: _visibility.wire,
          );
      if (mounted) Navigator.of(context).pop(updated);
    } on ApiError catch (e) {
      setState(() {
        _saving = false;
        _error = e.code;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Scaffold(
      appBar: AppBar(title: const Text('Edit post')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: _body,
              maxLines: 8,
              maxLength: PostLimits.bodyMax,
              autofocus: true,
              onChanged: (_) => setState(() {}),
              decoration: const InputDecoration(border: OutlineInputBorder()),
            ),
            const SizedBox(height: KSpace.md),
            DropdownButtonFormField<PostVisibility>(
              initialValue: _visibility,
              decoration: const InputDecoration(
                labelText: 'Who can see this',
                border: OutlineInputBorder(),
              ),
              items: [
                for (final v in PostVisibility.values)
                  // event_participants only makes sense on a post attached to an event.
                  if (v != PostVisibility.eventParticipants || widget.post.event != null)
                    DropdownMenuItem(value: v, child: Text(v.label)),
              ],
              onChanged: (v) => setState(() => _visibility = v ?? _visibility),
            ),
            if (widget.post.media.isNotEmpty || widget.post.poll != null) ...[
              const SizedBox(height: KSpace.md),
              Text(
                widget.post.poll != null
                    ? 'The poll cannot be changed after posting.'
                    : 'Attachments cannot be changed after posting.',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
              ),
            ],
            if (_error != null) ...[
              const SizedBox(height: KSpace.md),
              Text(_error!, style: TextStyle(color: c.danger)),
            ],
            const SizedBox(height: KSpace.xl),
            KurxButton(
              label: 'Save changes',
              expand: true,
              loading: _saving,
              onPressed: _dirty && !_saving ? _save : null,
            ),
          ],
        ),
      ),
    );
  }
}
