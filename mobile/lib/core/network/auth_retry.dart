import 'dart:async';
import 'dart:math';

import 'api_error.dart';

/// Retry + offline policy for authentication actions.
///
/// ## Why there is no "approval queue"
///
/// The obvious offline design — queue an approval and flush it when connectivity returns — is
/// **deliberately not implemented**, because it is both useless and unsafe here:
///
/// * **Useless.** A login challenge lives ~120s (`AUTH_CHALLENGE_TTL_SECONDS`). Anything queued long
///   enough to need a queue has already expired server-side, so flushing it just produces a
///   confusing `challenge_consumed` later.
/// * **Unsafe.** Approving a sign-in means "yes, that is me, right now, at that location". Replaying
///   that intent minutes later — against context the user can no longer see — is precisely the
///   confused-deputy situation number-matching (ADR-A9) exists to prevent.
///
/// So an approval that cannot reach the server **fails fast and visibly**. The user re-approves from
/// a fresh push, which is cheap and unambiguous.
///
/// What *is* retried: short-lived transient faults (dropped socket, DNS blip, 502 from a rolling
/// deploy) where the user's intent is still current. And revocations, which are safe to repeat.
class AuthRetry {
  const AuthRetry._();

  /// Total wall-clock budget. Kept under the challenge TTL so a retry can never outlive the thing
  /// it is retrying.
  static const Duration budget = Duration(seconds: 20);

  static const Duration _initialDelay = Duration(milliseconds: 300);
  static const int maxAttempts = 3;

  /// Runs [action], retrying only faults that are plausibly transient.
  ///
  /// [isIdempotent] governs whether a request that may already have been *received* can be repeated.
  /// A timeout is ambiguous — the server may have processed it — so a non-idempotent action (approve)
  /// is not retried on timeout, while a revoke (same outcome if applied twice) is.
  static Future<T> run<T>(
    Future<T> Function() action, {
    bool isIdempotent = false,
    Duration budgetOverride = budget,
  }) async {
    final deadline = DateTime.now().add(budgetOverride);
    var delay = _initialDelay;
    Object? lastError;

    for (var attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        return await action();
      } catch (error) {
        lastError = error;
        if (!_isRetryable(error, isIdempotent: isIdempotent)) rethrow;
        if (attempt == maxAttempts) break;
        if (DateTime.now().add(delay).isAfter(deadline)) break;

        await Future<void>.delayed(delay);
        // Exponential with jitter: a backend restart drops every device at once, and identical
        // backoff would bring them all back in the same instant.
        delay = Duration(
          milliseconds: (delay.inMilliseconds * 2) + Random().nextInt(200),
        );
      }
    }

    throw lastError!;
  }

  /// A fault is retryable only if repeating it could plausibly succeed **and** repeating it is safe.
  ///
  /// Classifies [ApiError], not [DioException]: the data layer's `guard()` converts every transport
  /// failure into `ApiError` before it reaches here, so matching on DioException would silently
  /// match nothing — retry and offline detection would both be dead code. (They were, until this
  /// was caught by a test that went through the real data layer instead of throwing at this class
  /// directly.)
  ///
  /// Deliberately excluded:
  /// * any 4xx — the server made a decision; retrying re-asks a settled question. In particular a
  ///   401/403 must never be hammered, and `challenge_consumed` will never become un-consumed.
  /// * `rate_limited` / 429 — the server is asking for less traffic, not more.
  /// * timeouts on non-idempotent actions — the request may already have been applied.
  static bool _isRetryable(Object error, {required bool isIdempotent}) {
    if (error is! ApiError) return false;

    // status 0 means the request never got a response: either it never left (network_error) or we
    // never heard back (timeout). Those are the only two ambiguity classes worth distinguishing.
    if (error.status == 0) {
      switch (error.code) {
        case 'network_error':
          // Never reached the server, so a repeat cannot double-apply anything.
          return true;
        case 'timeout':
          // Ambiguous: it may already have been applied server-side.
          return isIdempotent;
        default:
          // response_parse_failed and friends are bugs, not weather — retrying re-runs the bug.
          return false;
      }
    }

    if (error.code == 'rate_limited') return false;
    return error.status >= 500 && error.status < 600;
  }

  /// True when the failure is "you are offline" rather than "the server said no", so the UI can
  /// offer *retry* instead of an error that reads like rejection.
  static bool isOffline(Object error) =>
      error is ApiError && error.status == 0 && error.code == 'network_error';
}
