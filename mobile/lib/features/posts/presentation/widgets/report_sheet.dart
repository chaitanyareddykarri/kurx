import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/posts_providers.dart';

/// Report a post or a comment.
///
/// Reasons are a closed list rather than free text: a controlled vocabulary is what makes the
/// moderation queue triageable, and the optional note carries anything the list does not cover.
const _reasons = <String, String>{
  'spam': 'Spam or misleading',
  'harassment': 'Harassment or bullying',
  'hate': 'Hate speech',
  'violence': 'Violence or threats',
  'sexual': 'Sexual content',
  'misinformation': 'False information',
  'impersonation': 'Impersonation',
  'other': 'Something else',
};

/// Opens the report sheet. Returns true when a report was filed.
Future<bool> showReportSheet(
  BuildContext context, {
  required String entityType,
  required String entityId,
}) async {
  final filed = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    builder: (_) => _ReportSheet(entityType: entityType, entityId: entityId),
  );
  return filed ?? false;
}

class _ReportSheet extends ConsumerStatefulWidget {
  const _ReportSheet({required this.entityType, required this.entityId});

  final String entityType;
  final String entityId;

  @override
  ConsumerState<_ReportSheet> createState() => _ReportSheetState();
}

class _ReportSheetState extends ConsumerState<_ReportSheet> {
  final _details = TextEditingController();
  String? _reason;
  bool _sending = false;
  String? _error;

  @override
  void dispose() {
    _details.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final reason = _reason;
    if (reason == null || _sending) return;

    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      await ref.read(postsSourceProvider).report(
            entityType: widget.entityType,
            entityId: widget.entityId,
            reason: reason,
            details: _details.text.trim().isEmpty ? null : _details.text.trim(),
          );
      if (mounted) Navigator.of(context).pop(true);
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
    return SafeArea(
      child: Padding(
        // Lifts the sheet clear of the keyboard once the note field has focus.
        padding: EdgeInsets.fromLTRB(
          KSpace.lg,
          KSpace.lg,
          KSpace.lg,
          KSpace.lg + MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Report this ${widget.entityType == 'post_comment' ? 'comment' : 'post'}',
              style: Theme.of(context)
                  .textTheme
                  .titleMedium
                  ?.copyWith(fontWeight: FontWeight.w700, color: c.text),
            ),
            const SizedBox(height: KSpace.md),
            RadioGroup<String>(
              groupValue: _reason,
              onChanged: (v) => setState(() => _reason = v),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  for (final entry in _reasons.entries)
                    RadioListTile<String>(
                      contentPadding: EdgeInsets.zero,
                      dense: true,
                      value: entry.key,
                      title: Text(entry.value),
                    ),
                ],
              ),
            ),
            const SizedBox(height: KSpace.sm),
            TextField(
              controller: _details,
              maxLength: 500,
              minLines: 2,
              maxLines: 4,
              decoration: const InputDecoration(
                labelText: 'Anything else? (optional)',
                border: OutlineInputBorder(),
              ),
            ),
            if (_error != null)
              Text(_error!, style: TextStyle(color: c.danger)),
            const SizedBox(height: KSpace.sm),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(false),
                  child: const Text('Cancel'),
                ),
                const SizedBox(width: KSpace.sm),
                FilledButton(
                  onPressed: _reason == null || _sending ? null : _submit,
                  child: _sending
                      ? const SizedBox(
                          width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Text('Report'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
