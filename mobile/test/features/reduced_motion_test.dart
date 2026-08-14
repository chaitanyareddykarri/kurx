import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:kurx_mobile/common/widgets/shimmer.dart';

/// Phase 42 — motion honours the platform setting everywhere, not in one screen.
///
/// `core/theme/motion.dart` was built in Phase 32 and wired into exactly one consumer,
/// `app_shell.dart`. Seven other files animated with raw durations, so a user who had asked the OS
/// to reduce motion still got every one of them (WCAG 2.3.3). Two of those were worse than the
/// rest: the skeleton shimmer and the chat typing dots `..repeat()` with no end condition, and both
/// run for as long as their underlying wait lasts — the automatically-starting motion of over five
/// seconds that WCAG 2.2.2 asks to be stoppable.
void main() {
  Widget wrap(Widget child, {required bool reduce}) => MediaQuery(
        data: MediaQueryData(disableAnimations: reduce),
        child: MaterialApp(home: Scaffold(body: child)),
      );

  group('a repeating animation stops when the platform asks', () {
    testWidgets('the skeleton shimmer holds still under reduced motion', (tester) async {
      await tester.pumpWidget(wrap(const ShimmerBox(height: 40), reduce: true));
      double opacity() => tester
          .widget<FadeTransition>(find.descendant(
              of: find.byType(ShimmerBox), matching: find.byType(FadeTransition)))
          .opacity
          .value;
      final before = opacity();
      await tester.pump(const Duration(milliseconds: 600));
      expect(opacity(), before, reason: 'the block still pulsed');
      // A stopped controller must also leave nothing scheduled, or the test framework's own
      // pending-timer check would be the only thing noticing it never settles.
      await tester.pumpAndSettle();
    });

    testWidgets('and still pulses when it does not', (tester) async {
      await tester.pumpWidget(wrap(const ShimmerBox(height: 40), reduce: false));
      double opacity() => tester
          .widget<FadeTransition>(find.descendant(
              of: find.byType(ShimmerBox), matching: find.byType(FadeTransition)))
          .opacity
          .value;
      final before = opacity();
      await tester.pump(const Duration(milliseconds: 300));
      expect(opacity(), isNot(before), reason: 'clamping must not become disabling');
    });
  });

  test('no animation names a duration the setting cannot reach', () {
    // Every duration must go through `context.motion(...)`, which collapses it, or belong to a
    // controller that reads `reduceMotion` itself. A raw literal is one nothing can clamp.
    final offenders = <String>[];
    for (final f in Directory('lib').listSync(recursive: true).whereType<File>()) {
      if (!f.path.endsWith('.dart')) continue;
      // Separators normalised: listSync yields '' on Windows, so every '/'-keyed allow-list
      // entry below was silently missed and the palette's own file counted as an offender.
      final rel = f.path.substring('lib/'.length).replaceAll(RegExp(r'[\\/]'), '/');
      if (rel == 'core/theme/motion.dart' || rel == 'core/theme/design_tokens.dart') continue;
      final source = f.readAsStringSync();
      final honoursSetting = source.contains('reduceMotion');
      final lines = source.split('\n');
      for (var i = 0; i < lines.length; i++) {
        final t = lines[i].trim();
        if (t.startsWith('//') || t.startsWith('*') || t.startsWith('/*')) continue;
        final animates = RegExp(r'duration:\s*(KMotion\.|const Duration)').hasMatch(lines[i]);
        if (animates && !lines[i].contains('context.motion(') && !honoursSetting) {
          offenders.add('$rel:${i + 1}');
        }
      }
    }
    expect(offenders, isEmpty,
        reason: 'Wrap the duration in `context.motion(...)`, or read `context.reduceMotion`.');
  });
}
