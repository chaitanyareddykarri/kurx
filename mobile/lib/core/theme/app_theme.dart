import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'design_tokens.dart';

/// Kurx Material 3 theme — the warm event palette, now shared with web and admin
/// (D-286; the palette itself is D-065, which the original comment mis-cited as
/// D-056). Typography is Anek (D-287): Plus Jakarta Sans has no Devanagari, and
/// `web/messages/hi.json` is a shipped locale, so Hindi fell back to an
/// unrelated system face mid-screen.
class AppTheme {
  /// [textTheme] exists so the theme can be built without `google_fonts`.
  ///
  /// `GoogleFonts.anekLatinTextTheme` fetches the face from gstatic at
  /// construction time, which made every assertion about colours, radii or
  /// durations depend on the network. Tests pass a plain [TextTheme]; production
  /// passes nothing and gets Anek.
  static ThemeData light({TextTheme? textTheme}) =>
      _base(Brightness.light, KurxColors.light, textTheme);
  static ThemeData dark({TextTheme? textTheme}) =>
      _base(Brightness.dark, KurxColors.dark, textTheme);

  static ThemeData _base(Brightness brightness, KurxColors c, [TextTheme? overrideTextTheme]) {
    final isDark = brightness == Brightness.dark;

    final scheme = ColorScheme(
      brightness:            brightness,
      primary:               c.accent,
      onPrimary:             c.onAccent,
      primaryContainer:      c.accent.withValues(alpha: isDark ? 0.18 : 0.12),
      onPrimaryContainer:    c.accent,
      secondary:             c.teal,
      // Not `onAccent`: that is solved against the blue fill, and white on teal
      // is only 3.30:1. The page background is 6.01:1 dark / 6.33:1 light.
      onSecondary:           c.background,
      secondaryContainer:    c.teal.withValues(alpha: isDark ? 0.18 : 0.12),
      onSecondaryContainer:  c.teal,
      surface:               c.background,
      onSurface:             c.text,
      surfaceContainerLowest:  isDark ? const Color(0xFF000000) : const Color(0xFFFFFFFF),
      surfaceContainerLow:     c.background,
      surfaceContainer:        c.cardSurface,
      surfaceContainerHigh:    c.elevated,
      surfaceContainerHighest: c.elevated,
      onSurfaceVariant:        c.muted,
      outline:                 c.border,
      outlineVariant:          c.border.withValues(alpha: 0.6),
      error:                   c.danger,
      onError:                 c.onDanger,
      errorContainer:          c.danger.withValues(alpha: isDark ? 0.18 : 0.12),
      onErrorContainer:        c.danger,
      inverseSurface:          c.text,
      onInverseSurface:        c.background,
      shadow:                  const Color(0xFF000000),
    );

    // Anek Latin covers Latin; Devanagari glyphs fall through to Anek Devanagari,
    // which is a matched cut from the same superfamily (D-287).
    final base = overrideTextTheme ??
        GoogleFonts.anekLatinTextTheme(ThemeData(brightness: brightness).textTheme);

    return ThemeData(
      useMaterial3:           true,
      brightness:             brightness,
      colorScheme:            scheme,
      scaffoldBackgroundColor: c.background,
      extensions:             [c],
      textTheme:              base.apply(bodyColor: c.text, displayColor: c.text),
      splashFactory:          InkSparkle.splashFactory,

      appBarTheme: AppBarTheme(
        centerTitle: false,
        backgroundColor:   c.background,
        foregroundColor:   c.text,
        surfaceTintColor:  Colors.transparent,
        elevation:         0,
        titleTextStyle: base.titleLarge?.copyWith(
          color: c.text, fontSize: 20, fontWeight: FontWeight.w700, letterSpacing: -0.2,
        ),
      ),

      // Primary: 56 px pill, orange fill (D-056)
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: c.accent,
          foregroundColor: c.onAccent,
          minimumSize:     const Size.fromHeight(56),
          textStyle:       const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(KRadius.pill),
          ),
        ),
      ),

      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: c.text,
          side:            BorderSide(color: c.borderStrong),
          minimumSize:     const Size.fromHeight(52),
          textStyle:       const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(KRadius.pill),
          ),
        ),
      ),

      // `accentText`, not `accent`: the ember fill is 2.80:1 on the cream
      // background and is not a text colour. On dark the two coincide.
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: c.accentText,
          minimumSize: const Size(48, 44), // touch floor
        ),
      ),

      inputDecorationTheme: InputDecorationTheme(
        filled:          true,
        fillColor:       c.cardSurface,
        contentPadding:  const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.lg),
        hintStyle:       TextStyle(color: c.muted),
        // `borderStrong`: an input's edge is what identifies the control, so it
        // must clear 3:1 (WCAG 1.4.11). `border` is decorative at 1.30:1.
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(KRadius.pill),
          borderSide:   BorderSide(color: c.borderStrong),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(KRadius.pill),
          borderSide:   BorderSide(color: c.accent, width: 1.5),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(KRadius.pill),
          borderSide:   BorderSide(color: c.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(KRadius.pill),
          borderSide:   BorderSide(color: c.danger, width: 1.5),
        ),
      ),

      // Cards: 24 px radius, soft shadow, no border (D-056)
      cardTheme: CardThemeData(
        color:            c.cardSurface,
        surfaceTintColor: Colors.transparent,
        elevation:        0,
        clipBehavior:     Clip.antiAlias,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(KRadius.xl),
        ),
        shadowColor: Colors.transparent,
      ),

      dividerTheme: DividerThemeData(color: c.border, thickness: 1, space: 1),

      chipTheme: ChipThemeData(
        backgroundColor: c.elevated,
        side:            BorderSide.none,
        labelStyle:      TextStyle(color: c.text, fontWeight: FontWeight.w600),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(KRadius.pill),
        ),
      ),

      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor:  c.cardSurface,
        surfaceTintColor: Colors.transparent,
        showDragHandle:   true,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(KRadius.xl)),
        ),
      ),

      dialogTheme: DialogThemeData(
        backgroundColor:  c.cardSurface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(KRadius.xl),
        ),
      ),

      snackBarTheme: SnackBarThemeData(
        behavior:         SnackBarBehavior.floating,
        backgroundColor:  c.elevated,
        contentTextStyle: TextStyle(color: c.text),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(KRadius.md),
        ),
      ),

      // NavigationBar kept for fallback; AppShell uses custom _KurxNavBar (D-057)
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor:  c.cardSurface,
        surfaceTintColor: Colors.transparent,
        indicatorColor:   c.accent.withValues(alpha: 0.18),
        elevation:        0,
      ),
    );
  }
}
