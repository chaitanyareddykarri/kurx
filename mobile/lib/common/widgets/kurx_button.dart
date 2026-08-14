import 'package:flutter/material.dart';

import '../../core/theme/design_tokens.dart';

enum KurxButtonVariant { primary, secondary, ghost, danger }

enum KurxButtonSize { regular, small }

/// The one button used across the app. Matches the web button variants
/// (primary / secondary / ghost) plus a danger variant, with a built-in loading
/// state so callers never wire a spinner by hand. Passing `null` to [onPressed]
/// disables it.
class KurxButton extends StatelessWidget {
  const KurxButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.variant = KurxButtonVariant.primary,
    this.size = KurxButtonSize.regular,
    this.icon,
    this.loading = false,
    this.expand = false,
  });

  final String label;
  final VoidCallback? onPressed;
  final KurxButtonVariant variant;
  final KurxButtonSize size;
  final IconData? icon;
  final bool loading;

  /// Stretch to the full width of the parent.
  final bool expand;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final small = size == KurxButtonSize.small;
    // Primary: 56 px pill. Secondary/ghost/danger: 48 px pill (D-056).
    final height = small ? 40.0 : (variant == KurxButtonVariant.primary ? 56.0 : 48.0);
    final disabled = onPressed == null || loading;

    final (bg, fg, border) = switch (variant) {
      KurxButtonVariant.primary   => (c.accent, c.onAccent, null),
      KurxButtonVariant.secondary => (c.cardSurface, c.text, c.border),
      KurxButtonVariant.ghost     => (Colors.transparent, c.muted, null),
      KurxButtonVariant.danger    => (c.danger, c.onDanger, null),
    };

    final child = loading
        ? SizedBox(
            height: small ? 16 : 20,
            width:  small ? 16 : 20,
            child: CircularProgressIndicator(strokeWidth: 2, color: fg),
          )
        : Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (icon != null) ...[Icon(icon, size: small ? 16 : 18), const SizedBox(width: KSpace.sm)],
              Text(label, style: TextStyle(fontSize: small ? 13 : 15, fontWeight: FontWeight.w700)),
            ],
          );

    final button = Opacity(
      opacity: disabled && !loading ? 0.5 : 1,
      child: Material(
        color: bg,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(KRadius.pill),
          side: border != null ? BorderSide(color: border) : BorderSide.none,
        ),
        child: InkWell(
          onTap: disabled ? null : onPressed,
          borderRadius: BorderRadius.circular(KRadius.pill),
          child: Container(
            height: height,
            padding: EdgeInsets.symmetric(horizontal: small ? KSpace.lg : KSpace.xl),
            alignment: Alignment.center,
            child: DefaultTextStyle.merge(
              style: TextStyle(color: fg),
              child: IconTheme.merge(data: IconThemeData(color: fg), child: child),
            ),
          ),
        ),
      ),
    );

    return expand ? SizedBox(width: double.infinity, child: button) : button;
  }
}
