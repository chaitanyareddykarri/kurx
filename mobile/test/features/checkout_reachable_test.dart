import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// A working screen nobody can reach is not a working screen.
///
/// Phase 35 found `CheckoutPage` fully implemented — it calls `createOrder` with an
/// `Idempotency-Key`, exactly as the backend contract asks — behind the route
/// `/events/:slug/checkout/:eventId/:ticketTypeId`, with **nothing in the app navigating to it**.
/// It was reachable only by typing the URL, so on mobile a person could read an event and have no
/// way to register for it from the page that describes it.
///
/// That is the mirror of REG-009 on web: web has the entry point and no working checkout; mobile
/// had the working checkout and no entry point. This guards the half that is now fixed.
/// A *navigation* to checkout, not a mention of the word.
///
/// The first version of this guard matched the string anywhere in the file, which its own
/// explanatory comment satisfied — so breaking the real `context.push` left it green. That is the
/// same trap the web, admin and Flutter semantics sweeps each hit; here it made the guard useless
/// rather than noisy, which is worse.
final _navigatesToCheckout = RegExp(r'''push\(\s*[^)]*?/checkout/''', dotAll: true);

/// Comments stripped, so prose describing the route is never mistaken for using it.
String _withoutComments(String src) => src
    .split('\n')
    .where((l) {
      final t = l.trimLeft();
      return !t.startsWith('//') && !t.startsWith('*') && !t.startsWith('/*');
    })
    .join('\n');

void main() {
  group('checkout is reachable from the product', () {
    test('something other than the router navigates to it', () {
      final callers = <String>[];

      for (final entity in Directory('lib').listSync(recursive: true)) {
        if (entity is! File || !entity.path.endsWith('.dart')) continue;
        if (entity.path.endsWith('app_router.dart')) continue; // declares the route, does not use it
        final src = _withoutComments(entity.readAsStringSync());
        if (_navigatesToCheckout.hasMatch(src)) callers.add(entity.path);
      }

      expect(
        callers,
        isNotEmpty,
        reason: 'No screen navigates to checkout, so the only way in is to type the URL. '
            'Whatever removed the entry point has un-shipped registration on mobile.',
      );
    });

    test('the event detail page is one of them', () {
      // It is the page a person reads before deciding to register; if the CTA lives anywhere, it
      // lives here.
      final src = _withoutComments(
        File('lib/features/events/presentation/pages/event_detail_page.dart').readAsStringSync(),
      );
      expect(_navigatesToCheckout.hasMatch(src), isTrue);
      // …and it must not offer a button when there is nothing to sell.
      expect(src, contains('Registration is closed'));
    });
  });
}
