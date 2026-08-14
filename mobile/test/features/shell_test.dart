import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/shell/presentation/app_shell.dart';

/// Guards on the mobile shell (Phase 33).
void main() {
  group('bottom nav content reservation', () {
    test('tracks the user text scale rather than a constant', () {
      // The reservation was a hardcoded 80px — a guess about the bar's height,
      // the device safe-area inset and the text scale, none of which it could
      // see. At large text sizes the last row of every list sat under the bar.
      final normal = AppShell.navBarHeightFor(const TextScaler.linear(1.0));
      final large = AppShell.navBarHeightFor(const TextScaler.linear(2.0));
      expect(large, greaterThan(normal), reason: 'must grow with the user text scale');
    });

    test('is in the right ballpark at normal scale', () {
      // Not a regression in the other direction: the bar really is about this
      // tall, so the original constant was correct for one configuration and
      // wrong for every other.
      final normal = AppShell.navBarHeightFor(const TextScaler.linear(1.0));
      expect(normal, greaterThan(40));
      expect(normal, lessThan(80));
    });
  });
}
