import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import 'api_error.dart';

/// Runs a Dio call and converts anything it throws into the one [ApiError] shape,
/// so no raw error escapes the data layer.
///
/// [DioException] is the expected failure (transport + non-2xx). Anything else means the
/// **response parsed wrong** — a DTO field the API doesn't send, or a type that moved —
/// and it used to propagate raw: it escaped every `on ApiError` handler, surfaced as an
/// unhandled exception, and the shared error view repainted it as "no connection". That
/// hid a real contract drift behind a wifi-off icon (D-064). Parse failures now get their
/// own [ApiError] code so they read as a bug, not as the user's network, and the cause is
/// logged with the endpoint that produced it.
Future<T> guard<T>(Future<T> Function() call, {String? endpoint}) async {
  try {
    return await call();
  } on DioException catch (e) {
    throw ApiError.fromDioException(e);
  } on ApiError {
    rethrow;
  } catch (e, stack) {
    developer.log(
      'Failed to parse the response${endpoint == null ? '' : ' from $endpoint'}',
      name: 'kurx.api',
      error: e,
      stackTrace: stack,
    );
    throw ApiError(status: 0, code: 'response_parse_failed', detail: '$e');
  }
}
