import 'dart:math' as math;
import 'package:flutter/material.dart';

import 'package:flutter_test/flutter_test.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/core/theme/design_tokens.dart';
import 'package:kurx_mobile/features/events/presentation/widgets/event_visuals.dart';

/// The Dart twin of `web/test/design-tokens.test.ts`.
///
/// D-286, and D-288 after it, chose their values by measuring contrast. A
/// measurement taken once is a claim; taken on every run it is a guarantee.
/// Mobile is where that matters most: `filledButtonTheme` put white on the ember
/// fill at 2.86:1, so **every primary CTA in the app** — Register, Book,
/// Continue — was below the AA floor from D-065 until D-286, and nothing caught
/// it.
///
/// Kept deliberately independent of the web suite: the two palettes are bound by
/// `docs/ui-ux/token-map.md`, and each side enforces its own copy so neither can
/// drift without a red test.

double _luminance(Color c) {
  double channel(double v) => v <= 0.03928 ? v / 12.92 : math.pow((v + 0.055) / 1.055, 2.4).toDouble();
  return 0.2126 * channel(c.r) + 0.7152 * channel(c.g) + 0.0722 * channel(c.b);
}

double contrast(Color a, Color b) {
  final la = _luminance(a);
  final lb = _luminance(b);
  final hi = math.max(la, lb);
  final lo = math.min(la, lb);
  return (hi + 0.05) / (lo + 0.05);
}

const _plain = TextTheme();

void main() {
  // AppTheme resolves fonts through google_fonts, which touches ServicesBinding.
  TestWidgetsFlutterBinding.ensureInitialized();
  // Every assertion here is about colour, radius and duration, so the theme is
  // built with a plain TextTheme rather than letting google_fonts reach gstatic.
  GoogleFonts.config.allowRuntimeFetching = false;

  const themes = <String, KurxColors>{'light': KurxColors.light, 'dark': KurxColors.dark};

  themes.forEach((name, c) {
    // Every foreground must clear the floor on all three surface steps, not just
    // the page background — a value can pass on `background` and fail on
    // `elevated`, which is how several first-draft values were caught.
    final steps = <String, Color>{
      'background': c.background,
      'cardSurface': c.cardSurface,
      'elevated': c.elevated,
    };

    group('$name palette', () {
      final textTokens = <String, Color>{
        'text': c.text,
        'muted': c.muted,
        'accentText': c.accentText,
        'teal': c.teal,
        'success': c.success,
        'warning': c.warning,
        'danger': c.danger,
      };

      textTokens.forEach((token, colour) {
        test('$token clears 4.5:1 on every surface step', () {
          steps.forEach((stepName, step) {
            final ratio = contrast(colour, step);
            expect(
              ratio,
              greaterThanOrEqualTo(4.5),
              reason: '$name: $token on $stepName is ${ratio.toStringAsFixed(2)}:1',
            );
          });
        });
      });

      test('borderStrong clears 3:1 — a control boundary must be perceivable', () {
        steps.forEach((stepName, step) {
          final ratio = contrast(c.borderStrong, step);
          expect(
            ratio,
            greaterThanOrEqualTo(3.0),
            reason: '$name: borderStrong on $stepName is ${ratio.toStringAsFixed(2)}:1',
          );
        });
      });

      test('onAccent is the more legible label on the accent fill', () {
        // The exact D-065 regression this file exists to prevent returning: white
        // on the ember fill was 2.86:1 and drove every primary CTA in the app.
        expect(contrast(c.onAccent, c.accent), greaterThanOrEqualTo(4.5));

        // Stated as a comparison, not a constant. D-286 read its measurement as
        // "the label is ink"; it was really "measure the fill". On D-288's
        // #2563EB the same measurement answers white.
        const white = Color(0xFFFFFFFF);
        final ink = _luminance(c.background) < _luminance(c.text) ? c.background : c.text;
        final better = contrast(white, c.accent) >= contrast(ink, c.accent) ? white : ink;
        expect(c.onAccent, equals(better));
      });

      test('the fill and the link are different blues on purpose', () {
        // A fill dark enough to carry a white label is too dark to read as text on
        // a near-black surface, and vice versa. One token cannot do both (D-288).
        expect(c.accent, isNot(equals(c.accentText)));
      });

      test('onSuccess and onDanger are legible on their fills', () {
        expect(contrast(c.onSuccess, c.success), greaterThanOrEqualTo(4.5));
        expect(contrast(c.onDanger, c.danger), greaterThanOrEqualTo(4.5));
      });

      test('teal stays distinct from success so provenance is not diluted', () {
        // KurxColors.light.success used to be byte-identical to teal.
        expect(c.success, isNot(equals(c.teal)));
      });
    });
  });

  group('theme wiring', () {
    test('the primary button label clears AA on its fill', () {
      for (final theme in [AppTheme.light(textTheme: _plain), AppTheme.dark(textTheme: _plain)]) {
        final style = theme.filledButtonTheme.style!;
        final fg = style.foregroundColor!.resolve({})!;
        final bg = style.backgroundColor!.resolve({})!;
        expect(
          contrast(fg, bg),
          greaterThanOrEqualTo(4.5),
          reason: 'filledButtonTheme is the app-wide primary CTA',
        );
      }
    });

    test('text buttons use the text-safe blue', () {
      final light = AppTheme.light(textTheme: _plain);
      final fg = light.textButtonTheme.style!.foregroundColor!.resolve({})!;
      expect(fg, equals(KurxColors.light.accentText));
      expect(contrast(fg, KurxColors.light.background), greaterThanOrEqualTo(4.5));
    });

    test('input borders identify the control', () {
      for (final entry in {'light': AppTheme.light(textTheme: _plain), 'dark': AppTheme.dark(textTheme: _plain)}.entries) {
        final c = entry.key == 'light' ? KurxColors.light : KurxColors.dark;
        final border = entry.value.inputDecorationTheme.enabledBorder!.borderSide.color;
        expect(contrast(border, c.cardSurface), greaterThanOrEqualTo(3.0), reason: entry.key);
      }
    });
  });

  group('event placeholder art', () {
    test('every gradient stop can carry the white icon drawn on it', () {
      // categories_page and category_detail_page paint a white Icon over these
      // tiles, so WCAG 1.4.11 (3:1 for meaningful non-text content) applies.
      // This art backs every event without an uploaded image, so it is the
      // most-seen colour in the app and has to belong to the palette.
      for (final pair in EventVisuals.allGradients) {
        for (final stop in pair) {
          final ratio = contrast(const Color(0xFFFFFFFF), stop);
          expect(
            ratio,
            greaterThanOrEqualTo(3.0),
            reason: 'white icon on ${stop.toARGB32().toRadixString(16)} is '
                '${ratio.toStringAsFixed(2)}:1',
          );
        }
      }
    });

    test('is deterministic — one event always looks the same', () {
      expect(EventVisuals.gradientFor('kurx-hack-night'), EventVisuals.gradientFor('kurx-hack-night'));
    });
  });

  group('web parity (docs/ui-ux/token-map.md)', () {
    test('radius scale matches the web one exactly', () {
      expect(KRadius.sm, 6);
      expect(KRadius.md, 10);
      expect(KRadius.lg, 16);
      expect(KRadius.xl, 24);
    });

    test('motion durations match the web ones exactly', () {
      expect(KMotion.fast.inMilliseconds, 120);
      expect(KMotion.base.inMilliseconds, 200);
      expect(KMotion.slow.inMilliseconds, 280);
    });

    test('spacing scale matches the web one exactly', () {
      expect([KSpace.xs, KSpace.sm, KSpace.md, KSpace.lg, KSpace.xl, KSpace.xxl, KSpace.xxxl],
          [4, 8, 12, 16, 24, 32, 48]);
    });
  });
}
