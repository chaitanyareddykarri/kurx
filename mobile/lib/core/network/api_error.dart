import 'package:flutter/foundation.dart';
import 'package:dio/dio.dart';

/// The single client-side error model. Mirrors exactly the RFC7807 ProblemDetails the
/// backend emits (`ProblemResults.cs` / `GlobalExceptionHandler.cs`): a stable machine
/// `error` code, an optional `detail`, a `correlationId`, and a field `errors` map on
/// `validation_failed`. No second error shape exists in the app.
class ApiError implements Exception {
  const ApiError({
    required this.status,
    required this.code,
    this.detail,
    this.correlationId,
    this.fieldErrors,
  });

  /// HTTP status (0 when the request never reached the server — timeout/offline).
  final int status;

  /// Stable machine-readable code (`error` extension), e.g. `invalid_code`, `rate_limited`.
  final String code;
  final String? detail;
  final String? correlationId;

  /// Present on `validation_failed`: field name → messages.
  final Map<String, List<String>>? fieldErrors;

  factory ApiError.fromDioException(DioException e) {
    final res = e.response;
    final data = res?.data;
    if (data is Map) {
      final map = data.cast<String, dynamic>();
      return ApiError(
        status: res?.statusCode ?? 0,
        code: (map['error'] as String?) ?? (map['title'] as String?) ?? 'unknown_error',
        detail: map['detail'] as String?,
        correlationId: map['correlationId'] as String?,
        fieldErrors: _parseFieldErrors(map['errors']),
      );
    }
    return ApiError(status: res?.statusCode ?? 0, code: _codeForType(e.type));
  }

  static Map<String, List<String>>? _parseFieldErrors(Object? errors) {
    if (errors is Map) {
      return errors.map(
        (k, v) => MapEntry(
          k.toString(),
          v is List ? v.map((e) => e.toString()).toList() : [v.toString()],
        ),
      );
    }
    return null;
  }

  static String _codeForType(DioExceptionType t) {
    switch (t) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return 'timeout';
      case DioExceptionType.connectionError:
        return 'network_error';
      default:
        return 'unknown_error';
    }
  }

  /// A human message safe to show. Never leaks internals; falls back by status.
  ///
  /// In a DEBUG build an unmapped code names itself instead of hiding behind the generic sentence.
  /// An unmapped code is a gap in [_messages], and the generic fallback is exactly what conceals the
  /// gap: `organizer_not_verified_for_paid` reached organisers as "Something went wrong. Please try
  /// again." for as long as the key beside it was misspelled, because nothing ever said which code
  /// arrived. Release builds are unaffected — `kDebugMode` is a compile-time constant, so the branch
  /// is tree-shaken out and users never see a code. Mirrors the same affordance in
  /// packages/ui/src/problem-copy.ts.
  String get userMessage {
    final mapped = _messages[code];
    if (mapped != null) return mapped;
    // 4xx only. A 5xx or a transport failure already has a correct, specific message from status,
    // and the "code" on those is often a ProblemDetails *title* rather than a machine code — naming
    // it would replace a good sentence with a worse one. The gap this exists to expose is a missing
    // mapping for a business refusal, which is always 4xx.
    if (kDebugMode && status >= 400 && status < 500) {
      return '$code (HTTP $status) — no copy for this code yet.';
    }
    return _fallbackByStatus();
  }

  String _fallbackByStatus() {
    if (status == 0) return "Can't reach Kurx. Check your connection and try again.";
    if (status >= 500) return 'Something went wrong on our end. Please try again.';
    return 'Something went wrong. Please try again.';
  }

  static const Map<String, String> _messages = {
    'rate_limited': 'Too many attempts. Please wait a minute and try again.',
    'invalid_code': "That code isn't right. Check it and try again.",
    'invalid_token': 'Your session expired. Please sign in again.',
    'validation_failed': 'Please check the details and try again.',
    'not_found': "We couldn't find that.",
    'timeout': 'The request timed out. Please try again.',
    'network_error': "Can't reach Kurx. Check your connection and try again.",

    // DB-2 database-capacity refusals (503 + Retry-After). Worded identically to
    // packages/ui/src/problem-copy.ts — one vocabulary across web, admin and mobile, which is the whole
    // reason that file was extracted. Distinct from 'network_error': the connection is fine, the server
    // reached its database ceiling, and blaming the user's signal would send them to the wrong remedy.
    'database_busy': 'Kurx is busy right now. Please try again in a moment.',
    'database_timeout': 'That took longer than expected. Please try again.',
    'database_unavailable': 'Kurx is temporarily unavailable. Please try again in a moment.',
    'resource_busy': 'Someone else is updating this right now. Please try again in a moment.',

    // The request succeeded; we couldn't read the reply. Never blame the connection.
    'response_parse_failed': "Kurx sent something this app couldn't read. Please update the app or try again.",
    // Ticket transfers (TicketTransferService)
    'phone_mismatch': 'This ticket was sent to a different mobile number.',
    'transfer_expired': 'This transfer has expired. Ask the sender to send it again.',
    'transfer_not_pending': 'This ticket has already been claimed.',
    'transfers_disabled': 'Transfers are turned off for this event.',
    'forbidden': "You don't have access to that.",

    // Password policy (PasswordService.RejectionCode, D-129). The registration screen used to switch
    // on these itself, and matched on `weak_password` — a code the backend has never emitted — so
    // every real refusal fell to "Could not set your password. Try again." Told to use 12 characters,
    // a user picks one of the 12-character strings the breach list exists to catch, and the app gave
    // them no way to know that. Mapped once here so every password surface inherits it.
    //
    // 12/128 track PasswordPolicy.MinLength/MaxLength, the same figures this app already falls back
    // to when `GET /v1/auth/password/status` is unreachable.
    'password_too_short': 'Use at least 12 characters.',
    'password_too_long': 'That password is too long (max 128).',
    'password_breached': 'That password is too common. Choose something less guessable.',
    'password_contains_identifier': "Don't use your name, email, or phone number in your password.",
    'password_reused': "You've used that password recently. Choose a new one.",
    'password_already_set': 'You already have a password. Use Change password instead.',
    'password_not_set': "You don't have a password yet. Create one first.",
    'password_invalid': "That password isn't allowed. Choose a different one.",

    // Phone. `invalid_phone` is phone-shaped but unplaceable; `validation_failed` is not phone-shaped.
    'invalid_phone': "That phone number doesn't look right. Check the country code and number.",

    // D-335 ID-card issuance. The issuer is the event's verified creator/organizer, never an
    // organization, so none of these mention a college or an org seat. Worded to match
    // packages/ui/src/problem-copy.ts — one vocabulary across web, admin and mobile.
    'not_event_organizer': "Only the event's creator or an organizer can issue ID cards for it.",
    'issuer_not_verified':
        'Your account needs to be verified before you can issue ID cards. Finish verification in Settings.',
    'cannot_issue_to_self':
        "You can't issue an ID card to yourself — that's what makes it proof of participation.",
    'holder_not_a_participant':
        "That person isn't a participant in this event yet, so they can't be issued a card for it.",
    'invalid_storage_key': "That file isn't one you uploaded. Upload the image again and retry.",

    // D-363 §4 — the ticket-type editor can now be refused by the EVENT's review state, not just by its
    // own rules, and neither code was mapped anywhere an organiser edits prices. Worded as in
    // packages/ui/src/problem-copy.ts.
    'event_under_review':
        "This event is with a reviewer right now, so it can't be edited. You'll get it back with their notes.",
    'tickets_already_sold':
        "This ticket type has already been sold, so it can't be deleted. Stop its sales instead by ending the sale period.",

    // The V3 §14.2 lifecycle gates, worded as in packages/ui/src/problem-copy.ts. None of them had copy
    // on any surface, so `open_registration` on an event with no ticket type read as "Something went
    // wrong" — a refusal the organiser could have cleared in a minute if anyone had told them what it was.
    // D-367 — the `teams` capability is now a domain invariant, so these arrive from the API rather
    // than being prevented only by this app hiding a radio. Worded as in packages/ui.
    'teams_not_supported':
        "This kind of event doesn't support team entry. Choose an event type that does, or register people individually.",
    'type_conflicts_with_team_ticket':
        "This event has a team registration, and the type you picked doesn't support teams. Change the registration to individual first, or choose a different type.",
    // D-366 — team-size price bands, worded as in packages/ui/src/problem-copy.ts.
    'overlapping_price_tiers':
        'Two price rules cover the same team size. Each size can have only one price — narrow one of the rules.',
    'price_tier_gap':
        'Some allowed team sizes have no price. Cover every size from the smallest team to the largest, with no gaps.',
    'price_tier_outside_group_size':
        "A price rule covers a team size this event doesn't allow. Keep every rule inside your smallest and largest team size.",
    'invalid_price_tier':
        'A price rule is incomplete — each needs a team size range and a price above zero.',
    'price_tiers_require_group': 'Price-by-team-size only applies to team registration.',
    'price_tiers_require_group_size':
        'Set the smallest and largest team size before pricing by team size.',
    'no_price_for_team_size':
        "There's no price set for a team of this size. Ask the organiser to add one, or change your team size.",
    'ambiguous_price_rule':
        "This event has two prices for a team of your size, so we can't charge you. We've told the organiser.",
    'no_pass':
        'Add at least one ticket type before opening registration — there is nothing for anyone to book yet.',
    'no_inventory_pool':
        'This event has no ticket inventory yet. Set a quantity on a ticket type, then open registration.',
    'no_currency':
        "This event has no settlement currency set, so it can't sell anything yet.",
    'no_staff_assigned':
        'Assign at least one staff member who has accepted before taking this event live.',
    'results_not_published':
        'Publish the results before marking this event completed.',
    'missing_owner_unit':
        "This event isn't attached to a team yet. Set one before publishing.",
  };

  @override
  String toString() => 'ApiError($status, $code${correlationId != null ? ', cid=$correlationId' : ''})';
}
