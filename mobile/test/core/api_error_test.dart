import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/network/api_error.dart';

DioException _dioWith({int? status, Object? data, DioExceptionType type = DioExceptionType.badResponse}) {
  final opts = RequestOptions(path: '/v1/auth/otp/verify');
  return DioException(
    requestOptions: opts,
    type: type,
    response: status == null
        ? null
        : Response(requestOptions: opts, statusCode: status, data: data),
  );
}

void main() {
  group('ApiError.fromDioException', () {
    test('parses an RFC7807 ProblemDetails body', () {
      final err = ApiError.fromDioException(_dioWith(
        status: 401,
        data: {
          'type': 'https://tools.ietf.org/html/rfc9110#section-15.5.2',
          'title': 'Unauthorized',
          'status': 401,
          'error': 'invalid_code',
          'detail': 'invalid_code',
          'correlationId': 'cid-123',
        },
      ));
      expect(err.status, 401);
      expect(err.code, 'invalid_code');
      expect(err.correlationId, 'cid-123');
      expect(err.userMessage, contains('code'));
    });

    test('extracts the validation_failed field errors map', () {
      final err = ApiError.fromDioException(_dioWith(
        status: 400,
        data: {
          'error': 'validation_failed',
          'errors': {
            'Phone': ['Phone is required'],
          },
        },
      ));
      expect(err.code, 'validation_failed');
      expect(err.fieldErrors?['Phone'], ['Phone is required']);
    });

    test('maps a connection error to a network code with status 0', () {
      final err = ApiError.fromDioException(_dioWith(type: DioExceptionType.connectionError));
      expect(err.status, 0);
      expect(err.code, 'network_error');
      expect(err.userMessage, contains("reach Kurx"));
    });

    test('falls back to title then unknown when no error code present', () {
      final err = ApiError.fromDioException(_dioWith(status: 500, data: {'title': 'Server error'}));
      expect(err.code, 'Server error');
      expect(err.userMessage, contains('our end'));
    });
  });

  /// An unmapped code is a gap in `_messages`, and the generic fallback is exactly what conceals it:
  /// `organizer_not_verified_for_paid` was mapped under a transposed key on the event-status screen
  /// and therefore never fired, for as long as nothing ever said which code had arrived.
  group('unmapped codes name themselves in debug', () {
    test('surfaces the code and status instead of the generic sentence', () {
      const e = ApiError(status: 409, code: 'some_code_nobody_mapped');

      expect(e.userMessage, contains('some_code_nobody_mapped'));
      expect(e.userMessage, contains('409'));
      expect(e.userMessage, isNot(equals('Something went wrong. Please try again.')));
    });

    test('the diagnostic leaks no detail or correlation id', () {
      // `detail` can echo server internals and a correlation id on screen invites a user to read an
      // internal identifier aloud. Neither belongs in a message, debug build or not.
      const e = ApiError(
        status: 500,
        code: 'unmapped',
        detail: 'Npgsql.PostgresException: relation "secrets" does not exist',
        correlationId: 'cid-should-not-appear',
      );

      expect(e.userMessage, isNot(contains('Npgsql')));
      expect(e.userMessage, isNot(contains('cid-should-not-appear')));
    });

    test('a mapped code still wins over the diagnostic', () {
      const e = ApiError(status: 403, code: 'forbidden');
      expect(e.userMessage, "You don't have access to that.");
    });
  });

  /// D-335 — the four issuance refusals. Each is a business outcome the organiser can act on, so
  /// none may fall through to the generic sentence or to the debug diagnostic.
  group('ID-card issuance refusals are mapped (D-335)', () {
    for (final code in const [
      'not_event_organizer',
      'issuer_not_verified',
      'cannot_issue_to_self',
      'holder_not_a_participant',
    ]) {
      test('$code reads as a business message', () {
        final e = ApiError(status: 403, code: code);
        expect(e.userMessage, isNot(equals('Something went wrong. Please try again.')));
        expect(e.userMessage, isNot(contains('no copy for this code yet')));
        // The issuer is the event's creator: no ID-card refusal may blame an organization.
        expect(e.userMessage.toLowerCase(), isNot(contains('organization')));
      });
    }
  });
}
