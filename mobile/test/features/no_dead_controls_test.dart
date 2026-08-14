import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// A control that advertises itself must do something.
///
/// This program has now found the same shape four times — web's bookmark button (REG-004), the fake
/// event QR (REG-005), the ticket screen's share button (Phase 37) and the devices screen's "Remove"
/// (Phase 38). The last is the one that justifies a permanent guard: an inert control on a *security*
/// screen tells someone their device was revoked when nothing happened.
void main() {
  /// **Empty.** All ten are closed as of Phase 39.
  ///
  /// The shape was always the same: a button that looks operable, announces as one, and runs an
  /// empty block. Each was decided by the phase that owned its screen — Download PDF was wired
  /// because `pdfUrl` was real, and the rest were removed once it was clear that implementing them
  /// meant inventing product behaviour.
  ///
  /// Kept as a set rather than deleted: it is where a future exception would have to be written
  /// down and justified, and the assertion below refuses to let one in quietly.
  const known = <String>{
  };

  test('no NEW widget has an empty tap handler', () {
    final empty = RegExp(r'(onPressed|onTap|onChanged):\s*\(\s*\w*\s*\)\s*\{\s*\}');
    final offenders = <String>[];

    for (final entity in Directory('lib').listSync(recursive: true)) {
      if (entity is! File || !entity.path.endsWith('.dart')) continue;
      final lines = entity.readAsLinesSync();
      for (var i = 0; i < lines.length; i++) {
        final t = lines[i].trimLeft();
        // Comments describing the defect read exactly like the defect — this trap has cost this
        // program four separate guards now.
        if (t.startsWith('//') || t.startsWith('*') || t.startsWith('/*')) continue;
        if (!empty.hasMatch(lines[i])) continue;
        if (known.contains(entity.path)) continue;
        offenders.add('${entity.path}:${i + 1}');
      }
    }

    expect(
      offenders,
      isEmpty,
      reason: 'A control with an empty handler is a dead control: it looks operable, announces as a '
          'button, and does nothing. Wire it or remove it.',
    );
  });

  test('the dead-control worklist is empty and stays that way', () {
    expect(known.length, isZero);
  });
}
