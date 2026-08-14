import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Guards for Phase 37 — tickets and registration 💰.
///
/// This is the money path and the gate credential in one flow, so these assert the two properties
/// that cost the most if they slip: a purchase must not be able to become two, and the code that
/// opens a gate must not acquire a casual way out of the app.
void main() {
  String read(String p) =>
      File('lib/features/orders/presentation/pages/$p').readAsStringSync();

  group('one tap and two taps are one purchase', () {
    test('the idempotency key is generated per screen, not per attempt', () {
      final src = read('checkout_page.dart');
      // A key minted inside `_placeOrder` would make a double-tap two orders, because the server's
      // §17.1 atomic claim is per key. Holding it as a field is what makes the claim mean anything.
      final field = RegExp(r'final String _idempotencyKey\s*=');
      expect(field.hasMatch(src), isTrue,
          reason: 'The idempotency key must be a field on the state, not a local in the handler.');

      final placeOrder = src.substring(src.indexOf('Future<void> _placeOrder'));
      final body = placeOrder.substring(0, placeOrder.indexOf('\n  }'));
      expect(body, isNot(contains('_idempotencyKey =')),
          reason: 'The key is being regenerated inside _placeOrder, so a retry is a second purchase.');
    });

    test('the order call cannot leave the button stuck', () {
      // `guard()` converts every throw — transport, parse, anything — into ApiError before it
      // reaches here, so `on ApiError` is total. If that ever stops being true, a failed payment
      // leaves a spinner and no answer, which on the money path is the worst state available.
      final guard = File('lib/core/network/api_guard.dart').readAsStringSync();
      expect(guard, contains('on DioException'));
      expect(guard, contains('catch (e, stack)'));
      expect(guard, contains('response_parse_failed'));
    });
  });

  group('the gate credential stays inside the app', () {
    test('the ticket screen offers no share affordance', () {
      /*
       * A share button sat here with an empty `onPressed` — a control that advertises itself and
       * does nothing (REG-004's pattern). Removed rather than implemented: the QR on this screen IS
       * the admission credential, so handing it to another app is a security decision, and transfer
       * already exists as the deliberate server-mediated route (D-062 rotates the code on claim
       * precisely so a copied one stops working).
       */
      final src = read('ticket_detail_page.dart');
      expect(src, isNot(contains('Icons.share')));
      expect(src, isNot(contains('share_outlined')));
    });

    test('no control on the ticket screen has an empty handler', () {
      final src = read('ticket_detail_page.dart');
      expect(RegExp(r'onPressed:\s*\(\)\s*\{\s*/?\*?').hasMatch(src), isFalse,
          reason: 'A control with an empty handler is a dead control.');
    });
  });
}
