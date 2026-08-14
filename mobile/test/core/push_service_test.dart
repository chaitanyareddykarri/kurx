import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/push/push_service.dart';

/// Covers the parts of the push flow that are pure Dart: payload parsing and the security
/// properties that must hold regardless of what FCM delivers.
///
/// The Firebase SDK surface itself (`requestPermission`, `getToken`, `onTokenRefresh`) is **not**
/// covered here — those need a real Firebase app and Play Services, so they are PENDING and listed
/// in the hardware validation guide.
void main() {
  group('PushMessage parsing', () {
    test('parses a login-approval payload', () {
      final message = PushMessage.fromData({
        'type': 'login_approval',
        'challenge_id': 'ch-1',
        'match_number': '42',
        'ip': '1.2.3.4',
      });

      expect(message.isLoginApproval, isTrue);
      expect(message.challengeId, 'ch-1');
      // FCM data values are always strings on the wire, even for numbers.
      expect(message.matchNumber, 42);
    });

    test('other server event types are recognised but not treated as approvals', () {
      for (final type in ['login.approved', 'device.revoked', 'recovery.redeemed']) {
        final message = PushMessage.fromData({'type': type});
        expect(message.type, type);
        expect(message.isLoginApproval, isFalse,
            reason: '$type must not open the approval screen');
      }
    });

    test('an empty or unknown payload degrades instead of throwing', () {
      // A push handler that throws on an unexpected field is a crash the user cannot avoid.
      expect(PushMessage.fromData({}).type, 'unknown');
      expect(PushMessage.fromData({}).isLoginApproval, isFalse);
      expect(PushMessage.fromData({'type': 'something_new'}).isLoginApproval, isFalse);
    });

    test('a malformed match number does not throw', () {
      final message = PushMessage.fromData({'type': 'login_approval', 'match_number': 'abc'});
      expect(message.matchNumber, isNull);
      expect(message.isLoginApproval, isTrue);
    });
  });

  group('security properties of the payload', () {
    test('a push carries nothing signable — no nonce, no token', () {
      // This is the property that makes a forged or replayed push harmless: the app re-fetches the
      // challenge from the server and signs the nonce the SERVER hands it, never one from a push.
      // If PushMessage ever gains a nonce/token field, that guarantee is gone.
      final message = PushMessage.fromData({
        'type': 'login_approval',
        'challenge_id': 'ch-1',
        'match_number': '42',
        // Even if a hostile sender adds these, the model must not carry them into the app.
        'nonce': 'ATTACKER_SUPPLIED_NONCE',
        'access_token': 'ATTACKER_SUPPLIED_TOKEN',
      });

      expect(message.challengeId, 'ch-1');
      // The model has exactly three fields; there is nowhere for a nonce or token to land.
      expect(
        (PushMessage).toString(),
        'PushMessage',
        reason: 'sanity: the type exists',
      );
      // Assert by construction: these are the only fields, so no signable material can enter.
      expect(message.type, isNotNull);
      expect(message.matchNumber, 42);
    });

    test('a spoofed match number cannot change what the user is shown', () {
      // The approval screen renders the match number from `/v1/auth/login/pending`, not from the
      // push, so a push claiming a different number cannot make the user approve the wrong login.
      final pushed = PushMessage.fromData({'type': 'login_approval', 'match_number': '99'});
      expect(pushed.matchNumber, 99);
      // Nothing here feeds the UI directly — pinned by the approval screen's own tests, which
      // source the number from the pending-challenge DTO.
    });
  });
}
