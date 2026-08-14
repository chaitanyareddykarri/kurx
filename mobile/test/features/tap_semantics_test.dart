import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// A tappable surface must announce itself.
///
/// Phase 33 found the tab bar was "a bare `GestureDetector`, unannounced and untappable to a11y"
/// and fixed that one. A scan then found fourteen more across `lib/` — a chip's delete affordance
/// at a 14px target, the ticket card a buyer opens at the gate, a post's avatar, a leaderboard
/// entry. To a screen reader each is neither a control nor named.
///
/// A source sweep rather than a widget test on purpose: the defect is the *absence* of a wrapper,
/// which is far cheaper to assert across a whole tree than to pump fifteen widgets. Each entry in
/// `_remaining` is a real open site, so this file doubles as the worklist REG-022 refers to — delete
/// a line as its screen is done and the sweep tightens itself.
void main() {
  group('tappable surfaces carry semantics', () {
    /// **Empty.** All fifteen are closed as of Phase 39.
    ///
    /// `app_shell` was the last entry and never needed work — Phase 33 had already wrapped it, and
    /// it sat on this list because the sweep's 12-line lookback could not see past the comment
    /// explaining the fix. Worth knowing before widening the window: the same comment that
    /// documents a fix can make the fix look absent.
    ///
    /// Kept as a set rather than deleted: it is where a future exception would have to be written
    /// down and justified, and the assertion below refuses to let one in quietly.
    const remaining = <String>{
    };

    test('no NEW GestureDetector escapes a Semantics wrapper', () {
      final offenders = <String>[];

      for (final entity in Directory('lib').listSync(recursive: true)) {
        if (entity is! File || !entity.path.endsWith('.dart')) continue;
        final lines = entity.readAsLinesSync();
        for (var i = 0; i < lines.length; i++) {
          final trimmed = lines[i].trimLeft();
          // A comment *describing* the defect reads exactly like the defect. This sweep has been
          // caught by its own explanation once per surface now (web, admin, and here), so comments
          // are skipped rather than matched.
          if (trimmed.startsWith('//') || trimmed.startsWith('*')) continue;
          if (!lines[i].contains('GestureDetector')) continue;
          final from = i - 12 < 0 ? 0 : i - 12;
          final to = i + 2 >= lines.length ? lines.length : i + 2;
          final window = lines.sublist(from, to).join('\n');
          if (window.contains('Semantics') || window.contains('InkWell')) continue;
          if (remaining.contains(entity.path)) continue;
          offenders.add('${entity.path}:${i + 1}');
        }
      }

      expect(
        offenders,
        isEmpty,
        reason: 'A tappable surface with no Semantics announces as neither a control nor a name. '
            'Wrap it, or — if its screen is not this phase\'s — add the file to `remaining` above.',
      );
    });

    test('the worklist is empty and stays that way', () {
      // REG-022 recorded fifteen sites. All fifteen are closed.
      expect(remaining, isEmpty);
    });
  });
}
