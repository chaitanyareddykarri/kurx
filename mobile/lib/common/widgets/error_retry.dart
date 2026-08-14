import 'package:flutter/material.dart';

import '../../core/network/api_error.dart';

/// Consistent, friendly error view with a retry action. Accepts either a plain
/// message or an [ApiError] (whose RFC7807 code maps to a friendly message).
class ErrorRetry extends StatelessWidget {
  const ErrorRetry({super.key, required this.message, required this.onRetry})
      : _code = null;

  ErrorRetry.fromError({super.key, required Object? error, required this.onRetry})
      : message = error is ApiError ? error.userMessage : 'Something went wrong. Please try again.',
        _code = error is ApiError ? error.code : null;

  final String message;
  final VoidCallback onRetry;

  /// The [ApiError] code behind this view, when there is one — picks the icon.
  final String? _code;

  /// The icon is part of the message. A wifi-off glyph on a server or parse failure
  /// tells the user to check their connection, which sends them to fix the wrong thing
  /// (D-064) — it is reserved for errors that really are the connection.
  IconData get _icon => switch (_code) {
        'network_error' || 'timeout' => Icons.wifi_off_rounded,
        'response_parse_failed' => Icons.bug_report_outlined,
        _ => Icons.error_outline_rounded,
      };

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(_icon, size: 56, color: Theme.of(context).colorScheme.error),
            const SizedBox(height: 16),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 20),
            OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Retry'),
            ),
          ],
        ),
      ),
    );
  }
}
