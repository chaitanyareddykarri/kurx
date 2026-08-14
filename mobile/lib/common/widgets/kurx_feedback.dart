import 'package:flutter/material.dart';

import '../../core/theme/design_tokens.dart';
import 'kurx_button.dart';

/// Toasts, confirm dialogs, and branded bottom sheets — the three feedback
/// surfaces every flow needs, in one place so success/error/confirm never get
/// styled ad-hoc per screen.
abstract final class KurxFeedback {
  static void toast(BuildContext context, String message, {KurxToastTone tone = KurxToastTone.info}) {
    final c = context.kurx;
    final (color, icon) = switch (tone) {
      KurxToastTone.success => (c.success, Icons.check_circle_outline_rounded),
      KurxToastTone.error => (c.danger, Icons.error_outline_rounded),
      KurxToastTone.info => (c.accent, Icons.info_outline_rounded),
    };
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Row(
            children: [
              Icon(icon, color: color, size: 20),
              const SizedBox(width: KSpace.md),
              Expanded(child: Text(message, style: TextStyle(color: c.text))),
            ],
          ),
        ),
      );
  }

  static void success(BuildContext context, String message) => toast(context, message, tone: KurxToastTone.success);
  static void error(BuildContext context, String message) => toast(context, message, tone: KurxToastTone.error);

  /// A destructive/confirm dialog. Resolves to `true` when the user confirms.
  static Future<bool> confirm(
    BuildContext context, {
    required String title,
    required String message,
    String confirmLabel = 'Confirm',
    String cancelLabel = 'Cancel',
    bool destructive = false,
  }) async {
    final result = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          KurxButton(
            label: cancelLabel,
            variant: KurxButtonVariant.ghost,
            size: KurxButtonSize.small,
            onPressed: () => Navigator.of(ctx).pop(false),
          ),
          KurxButton(
            label: confirmLabel,
            variant: destructive ? KurxButtonVariant.danger : KurxButtonVariant.primary,
            size: KurxButtonSize.small,
            onPressed: () => Navigator.of(ctx).pop(true),
          ),
        ],
      ),
    );
    return result ?? false;
  }

  /// A branded modal bottom sheet with a title + scrollable body.
  static Future<T?> sheet<T>(
    BuildContext context, {
    required String title,
    required Widget child,
  }) {
    return showModalBottomSheet<T>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) {
        final c = ctx.kurx;
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(KSpace.xl, KSpace.sm, KSpace.xl, KSpace.xl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: TextStyle(color: c.text, fontSize: 18, fontWeight: FontWeight.w800)),
                const SizedBox(height: KSpace.lg),
                Flexible(child: SingleChildScrollView(child: child)),
              ],
            ),
          ),
        );
      },
    );
  }
}

enum KurxToastTone { success, error, info }
