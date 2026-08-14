import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Phase 41 — one palette, named in one place.
///
/// Colour that carries meaning — pending, danger, body text — belongs to a token, because a token is
/// the only thing that has two values and picks the right one for the active theme. A `const
/// Color(0x……)` has exactly one, so every hardcoded colour is also a dark-theme bug waiting for the
/// user to flip the switch.
///
/// This found nine of them: seven copies of a Material amber standing in for `warning`, a red
/// standing in for `danger`, and a brown used as body text in two callouts. None adapted to theme;
/// the amber measured about 1.9:1 where it was used as text.
///
/// Decorative fills are a different question and are allowed below by name. A gradient needs stops
/// between the tokens, and inventing a token for each stop would say those stops mean something.
void main() {
  /// Files permitted to name a raw colour, each for a stated reason.
  const allowed = <String, String>{
    'core/theme/design_tokens.dart': 'the palette itself',
    'features/events/presentation/widgets/event_visuals.dart':
        'decorative gradient ramps behind event cards — stops between tokens, not semantic colour',
    'features/gamification/presentation/pages/leaderboard_page.dart':
        'medal tiers — gold/silver/bronze are the subject, not the theme',
    'core/theme/app_theme.dart':
        'maps the palette into Material\'s ColorScheme — pure black shadow and the white/near-black '
        'container extremes are Material\'s own scale, not Kurx colours',
  };

  test('no semantic colour is written as a literal', () {
    final offenders = <String>[];
    for (final f in Directory('lib').listSync(recursive: true).whereType<File>()) {
      if (!f.path.endsWith('.dart')) continue;
      // Separators normalised: listSync yields '' on Windows, so every '/'-keyed allow-list
      // entry below was silently missed and the palette's own file counted as an offender.
      final rel = f.path.substring('lib/'.length).replaceAll(RegExp(r'[\\/]'), '/');
      if (allowed.containsKey(rel)) continue;
      final lines = f.readAsLinesSync();
      for (var i = 0; i < lines.length; i++) {
        // Comment lines are stripped: a note *describing* this defect must not read as the defect.
        final t = lines[i].trim();
        if (t.startsWith('//') || t.startsWith('*') || t.startsWith('/*')) continue;
        if (RegExp(r'Color\(0x').hasMatch(lines[i])) offenders.add('$rel:${i + 1}');
      }
    }
    expect(offenders, isEmpty,
        reason: 'Use a `context.kurx` token, or add the file to `allowed` with a reason.');
  });

  test('every allowance still points at a file that exists', () {
    // An allowance outliving its file is how a list like this quietly stops meaning anything.
    for (final rel in allowed.keys) {
      expect(File('lib/$rel').existsSync(), isTrue, reason: '$rel is allowed but missing');
    }
  });
}
