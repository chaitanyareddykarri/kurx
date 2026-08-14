import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/core/network/auth_retry.dart';

/// These deliberately use [ApiError], not DioException: `guard()` in the data layer converts every
/// transport failure before it reaches AuthRetry, so testing with DioException would pass while
/// production silently retried nothing (exactly the bug this suite now pins).
ApiError _offline() => const ApiError(status: 0, code: 'network_error');
ApiError _timeout() => const ApiError(status: 0, code: 'timeout');
ApiError _http(int status, {String code = 'x'}) => ApiError(status: status, code: code);

void main() {
  group('what gets retried', () {
    test('a connection error is retried — the request never reached the server', () async {
      var attempts = 0;
      await expectLater(
        AuthRetry.run(() async {
          attempts++;
          throw _offline();
        }, budgetOverride: const Duration(seconds: 5)),
        throwsA(isA<ApiError>()),
      );
      expect(attempts, AuthRetry.maxAttempts);
    });

    test('a 503 is retried', () async {
      var attempts = 0;
      final result = await AuthRetry.run(() async {
        attempts++;
        if (attempts < 2) throw _http(503);
        return 'ok';
      }, budgetOverride: const Duration(seconds: 5));

      expect(result, 'ok');
      expect(attempts, 2);
    });
  });

  group('what must NOT be retried', () {
    test('a 4xx is never retried — the server made a decision', () async {
      for (final status in [400, 401, 403, 404, 409]) {
        var attempts = 0;
        await expectLater(
          AuthRetry.run(() async {
            attempts++;
            throw _http(status);
          }),
          throwsA(isA<ApiError>()),
        );
        expect(attempts, 1, reason: '$status must not be retried');
      }
    });

    test('429 is not retried — the server asked for less traffic, not more', () async {
      var attempts = 0;
      await expectLater(
        AuthRetry.run(() async {
          attempts++;
          throw _http(429, code: 'rate_limited');
        }),
        throwsA(isA<ApiError>()),
      );
      expect(attempts, 1);
    });

    test('a timeout on a NON-idempotent action is not retried', () async {
      // The request may already have been applied server-side. Approving a single-use challenge
      // twice is exactly the ambiguity that must not be resolved by guessing.
      var attempts = 0;
      await expectLater(
        AuthRetry.run(
          () async {
            attempts++;
            throw _timeout();
          },
          isIdempotent: false,
        ),
        throwsA(isA<ApiError>()),
      );
      expect(attempts, 1);
    });

    test('the same timeout IS retried when the action is idempotent', () async {
      var attempts = 0;
      await expectLater(
        AuthRetry.run(
          () async {
            attempts++;
            throw _timeout();
          },
          isIdempotent: true,
          budgetOverride: const Duration(seconds: 5),
        ),
        throwsA(isA<ApiError>()),
      );
      expect(attempts, AuthRetry.maxAttempts);
    });

    test('an unknown transport failure is never retried past', () async {
      var attempts = 0;
      await expectLater(
        AuthRetry.run(() async {
          attempts++;
          throw const ApiError(status: 0, code: 'unknown_error');
        }, isIdempotent: true),
        throwsA(isA<ApiError>()),
      );
      expect(attempts, 1);
    });

    test('a raw non-ApiError propagates immediately — guard() should have wrapped it', () async {
      var attempts = 0;
      await expectLater(
        AuthRetry.run(() async {
          attempts++;
          throw StateError('bug');
        }, isIdempotent: true),
        throwsA(isA<StateError>()),
      );
      expect(attempts, 1);
    });
  });

  group('budget', () {
    test('the retry budget stays under the challenge TTL', () {
      // A retry must never outlive the challenge it is retrying (~120s server-side).
      expect(AuthRetry.budget.inSeconds, lessThan(120));
    });

    test('succeeds without retrying when the first attempt works', () async {
      var attempts = 0;
      final result = await AuthRetry.run(() async {
        attempts++;
        return 42;
      });
      expect(result, 42);
      expect(attempts, 1);
    });
  });

  group('offline classification', () {
    test('connection failures read as offline, server refusals do not', () {
      expect(AuthRetry.isOffline(_offline()), isTrue);
      // A timeout is not "offline" — we reached the network, so the remedy differs.
      expect(AuthRetry.isOffline(_timeout()), isFalse);
      // A 401 is the server saying no — offering "retry" would misdescribe it.
      expect(AuthRetry.isOffline(_http(401, code: 'invalid_token')), isFalse);
      expect(AuthRetry.isOffline(StateError('x')), isFalse);
    });
  });
}
