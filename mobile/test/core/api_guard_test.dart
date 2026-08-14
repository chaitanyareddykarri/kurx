import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/core/network/api_guard.dart';

void main() {
  test('a DioException still becomes an ApiError', () async {
    final opts = RequestOptions(path: '/v1/orders');
    await expectLater(
      guard<int>(() async => throw DioException(
            requestOptions: opts,
            type: DioExceptionType.badResponse,
            response: Response(
              requestOptions: opts,
              statusCode: 404,
              data: {'error': 'not_found'},
            ),
          )),
      throwsA(isA<ApiError>().having((e) => e.code, 'code', 'not_found')),
    );
  });

  test('a parse failure becomes an ApiError, not a raw error', () async {
    // What a DTO/response mismatch actually throws — this used to escape `guard`, sail past
    // every `on ApiError` handler, and get painted as a network outage (D-064).
    await expectLater(
      guard<String>(() async => (<String, dynamic>{'a': 1}['a']) as String),
      throwsA(isA<ApiError>()
          .having((e) => e.code, 'code', 'response_parse_failed')),
    );
  });

  test('a parse failure never claims the connection is at fault', () async {
    try {
      await guard<String>(() async => (null as dynamic) as String);
      fail('expected an ApiError');
    } on ApiError catch (e) {
      expect(e.userMessage, isNot(contains("Can't reach Kurx")));
      expect(e.userMessage, contains("couldn't read"));
    }
  });

  test('an ApiError thrown inside passes through unchanged', () async {
    await expectLater(
      guard<int>(() async => throw const ApiError(status: 403, code: 'forbidden')),
      throwsA(isA<ApiError>().having((e) => e.code, 'code', 'forbidden')),
    );
  });
}
