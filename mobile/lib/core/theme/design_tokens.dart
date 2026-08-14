import 'package:flutter/material.dart';

/// Kurx brand tokens — the blue-on-black palette, shared by all three surfaces.
///
/// **D-288 supersedes D-286's warm ember direction**, which in turn superseded
/// D-065's two-register split. Every value here matches
/// `packages/ui/src/styles/tokens.css` exactly, and `docs/ui-ux/token-map.md`
/// binds the two.
///
/// Every foreground below is verified at >= 4.5:1 against all three surface
/// steps, and control boundaries at >= 3:1.
@immutable
class KurxColors extends ThemeExtension<KurxColors> {
  const KurxColors({
    required this.accent,
    required this.onAccent,
    required this.accentText,
    required this.teal,
    required this.background,
    required this.cardSurface,
    required this.elevated,
    required this.border,
    required this.borderStrong,
    required this.text,
    required this.muted,
    required this.success,
    required this.onSuccess,
    required this.warning,
    required this.danger,
    required this.onDanger,
  });

  final Color accent;       // Primary blue — a FILL, not text-safe on either theme
  /// The label ON a blue fill. White, and measured: `#FFFFFF` on `#2563EB` is
  /// 5.17:1.
  ///
  /// This token drives `filledButtonTheme`, so it is every primary CTA in the
  /// app (Register, Book, Continue). Under D-286's ember fill the same measured
  /// answer came out the other way — white on `#F0762B` was 2.86:1 and the label
  /// had to be ink. The rule is to measure the fill, not to prefer a colour.
  final Color onAccent;
  /// Blue as TEXT — links and text buttons. A lighter step than the fill on
  /// dark, a darker one on light.
  final Color accentText;
  final Color teal;         // Attests: provenance, verification, certificates
  final Color background;   // Screen background
  final Color cardSurface;  // Card / input fill
  final Color elevated;     // Elevated surface + inactive chip bg
  final Color border;       // Decorative dividers only — not a control boundary
  /// Boundaries that IDENTIFY a control (inputs, selects). >= 3:1, WCAG 1.4.11.
  /// One value serves both themes (4.34:1 light, 3.54:1 dark).
  final Color borderStrong;
  final Color text;         // Heading / body text
  final Color muted;        // Secondary text / inactive icons
  final Color success;
  final Color onSuccess;
  final Color warning;      // Gold (hue 45), a different signal from the blue accent
  final Color danger;
  final Color onDanger;

  // ── Light ─────────────────────────────────────────────────────────────────
  static const light = KurxColors(
    accent:       Color(0xFF2563EB), // Blue fill — white label is 5.17:1
    onAccent:     Color(0xFFFFFFFF),
    accentText:   Color(0xFF1D4ED8), // 6.12:1 — the fill clears light at only 4.72:1
    teal:         Color(0xFF1F6B5C), // 5.78:1
    background:   Color(0xFFFFFFFF),
    cardSurface:  Color(0xFFF8FAFC),
    elevated:     Color(0xFFF1F5F9),
    border:       Color(0xFFE2E8F0),
    borderStrong: Color(0xFF64748B), // 4.34:1
    text:         Color(0xFF0F172A), // 16.30:1
    muted:        Color(0xFF475569), // 6.92:1
    // Kept distinct from `teal`: teal attests, success confirms. One value
    // cannot mean both (the dilution D-286 forbade and D-288 keeps forbidding).
    success:      Color(0xFF197C36), // 4.82:1
    onSuccess:    Color(0xFFFFFFFF), // 5.28:1 on the success fill
    warning:      Color(0xFF876809), // 4.77:1
    danger:       Color(0xFFCE2C2C), // 4.77:1
    onDanger:     Color(0xFFFFFFFF), // 5.22:1 on the danger fill
  );

  // ── Dark ──────────────────────────────────────────────────────────────────
  static const dark = KurxColors(
    accent:       Color(0xFF2563EB),
    onAccent:     Color(0xFFFFFFFF), // 5.17:1
    accentText:   Color(0xFF3B82F6), // 4.58:1 — the fill is only 3.26:1 as text
    teal:         Color(0xFF309E88), // 5.11:1
    background:   Color(0xFF0A0A0A),
    cardSurface:  Color(0xFF111827),
    elevated:     Color(0xFF161D2D),
    border:       Color(0xFF1F2937),
    borderStrong: Color(0xFF64748B), // 3.54:1
    text:         Color(0xFFFFFFFF), // 16.83:1
    muted:        Color(0xFF9CA3AF), // 6.63:1
    success:      Color(0xFF3FB950), // 6.63:1 — distinct from teal
    onSuccess:    Color(0xFF0A0A0A), // 7.79:1 on the success fill
    warning:      Color(0xFFE6B31E), // 9.94:1
    danger:       Color(0xFFF05C59), // 5.11:1
    onDanger:     Color(0xFF0A0A0A), // 6.01:1 on the danger fill
  );

  @override
  KurxColors copyWith({
    Color? accent, Color? onAccent, Color? accentText, Color? teal,
    Color? background, Color? cardSurface, Color? elevated,
    Color? border, Color? borderStrong,
    Color? text, Color? muted,
    Color? success, Color? onSuccess, Color? warning, Color? danger, Color? onDanger,
  }) => KurxColors(
    accent:       accent       ?? this.accent,
    onAccent:     onAccent     ?? this.onAccent,
    accentText:   accentText   ?? this.accentText,
    teal:         teal         ?? this.teal,
    background:   background   ?? this.background,
    cardSurface:  cardSurface  ?? this.cardSurface,
    elevated:     elevated     ?? this.elevated,
    border:       border       ?? this.border,
    borderStrong: borderStrong ?? this.borderStrong,
    text:         text         ?? this.text,
    muted:        muted        ?? this.muted,
    success:      success      ?? this.success,
    onSuccess:    onSuccess    ?? this.onSuccess,
    warning:      warning      ?? this.warning,
    danger:       danger       ?? this.danger,
    onDanger:     onDanger     ?? this.onDanger,
  );

  @override
  KurxColors lerp(covariant KurxColors? other, double t) {
    if (other == null) return this;
    return KurxColors(
      accent:       Color.lerp(accent,       other.accent,       t)!,
      onAccent:     Color.lerp(onAccent,     other.onAccent,     t)!,
      accentText:   Color.lerp(accentText,   other.accentText,   t)!,
      teal:         Color.lerp(teal,         other.teal,         t)!,
      background:   Color.lerp(background,   other.background,   t)!,
      cardSurface:  Color.lerp(cardSurface,  other.cardSurface,  t)!,
      elevated:     Color.lerp(elevated,     other.elevated,     t)!,
      border:       Color.lerp(border,       other.border,       t)!,
      borderStrong: Color.lerp(borderStrong, other.borderStrong, t)!,
      text:         Color.lerp(text,         other.text,         t)!,
      muted:        Color.lerp(muted,        other.muted,        t)!,
      success:      Color.lerp(success,      other.success,      t)!,
      onSuccess:    Color.lerp(onSuccess,    other.onSuccess,    t)!,
      warning:      Color.lerp(warning,      other.warning,      t)!,
      danger:       Color.lerp(danger,       other.danger,       t)!,
      onDanger:     Color.lerp(onDanger,     other.onDanger,     t)!,
    );
  }
}

extension KurxColorsX on BuildContext {
  KurxColors get kurx {
    final theme = Theme.of(this);
    return theme.extension<KurxColors>() ??
        (theme.brightness == Brightness.dark ? KurxColors.dark : KurxColors.light);
  }
}

/// Spacing scale (4 px base).
abstract final class KSpace {
  static const double xs  = 4;
  static const double sm  = 8;
  static const double md  = 12;
  static const double lg  = 16;
  static const double xl  = 24;
  static const double xxl = 32;
  static const double xxxl = 48;
}

/// Corner radii. Cards = [xl] (24 px); pills = [pill].
abstract final class KRadius {
  // sm and md shift by 2px each so this scale is identical to the web one
  // (`packages/ui/tailwind-preset.cjs`) — docs/ui-ux/token-map.md §3.
  static const double sm   = 6;
  static const double md   = 10;
  static const double lg   = 16;
  static const double xl   = 24;
  static const double pill = 999;
}

/// Motion durations + curves.
abstract final class KMotion {
  static const Duration fast = Duration(milliseconds: 120);
  static const Duration base = Duration(milliseconds: 200); // was 220 — web parity
  static const Duration slow = Duration(milliseconds: 280); // was 360 — web parity
  static const Curve curve   = Curves.easeOutCubic;
}

/// Standard card shadow (soft drop, no border).
List<BoxShadow> kCardShadow(BuildContext context) {
  final isDark = Theme.of(context).brightness == Brightness.dark;
  return [
    BoxShadow(
      color: isDark
          ? Colors.black.withValues(alpha: 0.32)
          : const Color(0xFF2563EB).withValues(alpha: 0.08),
      blurRadius: 20,
      offset: const Offset(0, 6),
    ),
    BoxShadow(
      color: isDark
          ? Colors.black.withValues(alpha: 0.16)
          : Colors.black.withValues(alpha: 0.06),
      blurRadius: 6,
      offset: const Offset(0, 2),
    ),
  ];
}
