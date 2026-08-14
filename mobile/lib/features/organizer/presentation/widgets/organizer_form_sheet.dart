import 'package:flutter/material.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';

/// The one submit-and-report path for every organiser editor sheet.
///
/// It exists because the previous sheets each hand-rolled a "Save" button whose `onPressed` was
/// `() => Navigator.pop(context)` — the form closed, nothing was sent, and the user was told
/// nothing. A sheet built with [OrganizerFormSheet] cannot do that: [onSubmit] is required, the
/// sheet stays open and shows the server's reason when it throws, and it only pops on success.
class OrganizerFormSheet extends StatefulWidget {
  const OrganizerFormSheet({
    super.key,
    required this.title,
    required this.submitLabel,
    required this.fields,
    required this.onSubmit,
    this.successMessage,
  });

  final String title;
  final String submitLabel;

  /// Built with the current enabled state so inputs lock while the request is in flight.
  final List<Widget> Function(bool enabled) fields;

  /// Performs the write. Throws [ApiError] on failure — the sheet renders `userMessage`.
  final Future<void> Function() onSubmit;

  final String? successMessage;

  /// Opens the sheet. Resolves `true` only when the write actually succeeded.
  static Future<bool?> show(
    BuildContext context, {
    required String title,
    required String submitLabel,
    required List<Widget> Function(bool enabled) fields,
    required Future<void> Function() onSubmit,
    String? successMessage,
  }) {
    final c = context.kurx;
    return showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: c.cardSurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(KRadius.xl)),
      ),
      builder: (_) => OrganizerFormSheet(
        title: title,
        submitLabel: submitLabel,
        fields: fields,
        onSubmit: onSubmit,
        successMessage: successMessage,
      ),
    );
  }

  @override
  State<OrganizerFormSheet> createState() => _OrganizerFormSheetState();
}

class _OrganizerFormSheetState extends State<OrganizerFormSheet> {
  bool _busy = false;
  String? _error;

  Future<void> _submit() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.onSubmit();
      if (!mounted) return;
      Navigator.of(context).pop(true);
      final message = widget.successMessage;
      if (message != null) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      }
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = e.userMessage;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = 'Something went wrong. Please try again.';
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
            Text(
              widget.title,
              style: TextStyle(color: c.text, fontWeight: FontWeight.w800, fontSize: 17),
            ),
            const SizedBox(height: KSpace.lg),
            ...widget.fields(!_busy),
            if (_error != null) ...[
              const SizedBox(height: KSpace.md),
              Semantics(
                liveRegion: true,
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(Icons.error_outline_rounded,
                        size: 18, color: Theme.of(context).colorScheme.error),
                    const SizedBox(width: KSpace.sm),
                    Expanded(
                      child: Text(
                        _error!,
                        style: TextStyle(color: Theme.of(context).colorScheme.error),
                      ),
                    ),
                  ],
                ),
              ),
            ],
            const SizedBox(height: KSpace.lg),
            FilledButton(
              onPressed: _busy ? null : _submit,
              child: _busy
                  ? const SizedBox(
                      height: 18,
                      width: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : Text(widget.submitLabel),
            ),
          ],
        ),
      ),
    );
  }
}

/// Confirm-then-run for destructive organiser actions, with the same
/// stay-open-and-show-the-reason failure handling as [OrganizerFormSheet].
Future<bool> confirmAndRun(
  BuildContext context, {
  required String title,
  required String message,
  required String confirmLabel,
  required Future<void> Function() action,
  String? successMessage,
}) async {
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: Text(title),
      content: Text(message),
      actions: [
        TextButton(onPressed: () => Navigator.of(ctx).pop(false), child: const Text('Cancel')),
        FilledButton(onPressed: () => Navigator.of(ctx).pop(true), child: Text(confirmLabel)),
      ],
    ),
  );
  if (confirmed != true || !context.mounted) return false;

  try {
    await action();
    if (context.mounted && successMessage != null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(successMessage)));
    }
    return true;
  } on ApiError catch (e) {
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
    }
    return false;
  }
}
