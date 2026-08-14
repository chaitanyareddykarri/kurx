import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// The system back gesture must not silently discard work.
///
/// Phase 40 found **no `PopScope` anywhere in the app**. On Android the back gesture is the primary
/// way people navigate, and registration is a single page with inline stages rather than separate
/// routes — so one gesture exits the whole flow, taking a verified phone, a verified email and
/// everything typed with it, without a word.
void main() {
  test('registration guards the back gesture while there is work to lose', () {
    final src = File('lib/features/auth/presentation/pages/registration_flow_page.dart')
        .readAsStringSync();

    expect(src, contains('PopScope'));
    // Conditional, not blanket: an untouched form must still close on the first gesture. A
    // confirmation that fires when nothing is at stake teaches people to dismiss it unread, which
    // makes it useless on the one occasion it matters.
    expect(src, contains('canPop: !_hasProgress'));
    expect(src, contains('onPopInvokedWithResult'));
  });

  test('the deprecated back API is not reintroduced', () {
    // `WillPopScope` was removed in Flutter 3.16+; anything reaching for it would also miss
    // predictive back on Android 14+.
    final offenders = <String>[];
    for (final entity in Directory('lib').listSync(recursive: true)) {
      if (entity is! File || !entity.path.endsWith('.dart')) continue;
      for (final line in entity.readAsLinesSync()) {
        final t = line.trimLeft();
        if (t.startsWith('//') || t.startsWith('*')) continue;
        if (line.contains('WillPopScope')) offenders.add(entity.path);
      }
    }
    expect(offenders, isEmpty);
  });
}
