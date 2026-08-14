import 'package:dio/dio.dart';

import '../storage/token_store.dart';

/// Attaches the bearer token, and on a `401` transparently refreshes once and retries.
///
/// Refreshes are serialized through a single in-flight future: the backend revokes every
/// session if an already-rotated refresh token is reused (D-009/D-014), so concurrent 401s
/// must share one refresh, never fire several.
class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required this.tokens,
    required this.baseUrl,
    required this.onExpired,
  });

  final TokenStore tokens;
  final String baseUrl;
  final void Function() onExpired;

  Future<bool>? _inFlightRefresh;

  bool _isAuthPath(String path) => path.contains('/v1/auth/');

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    if (!_isAuthPath(options.path)) {
      final access = await tokens.accessToken();
      if (access != null && access.isNotEmpty) {
        options.headers['Authorization'] = 'Bearer $access';
      }
    }
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) async {
    final opts = err.requestOptions;
    final is401 = err.response?.statusCode == 401;
    final alreadyRetried = opts.extra['retried'] == true;

    if (is401 && !_isAuthPath(opts.path) && !alreadyRetried) {
      final refreshed = await (_inFlightRefresh ??=
          _refresh().whenComplete(() => _inFlightRefresh = null));
      if (refreshed) {
        try {
          opts.extra['retried'] = true;
          final access = await tokens.accessToken();
          opts.headers['Authorization'] = 'Bearer $access';
          final response = await Dio(BaseOptions(baseUrl: baseUrl)).fetch(opts);
          return handler.resolve(response);
        } catch (_) {
          // fall through to propagate the original error
        }
      } else {
        onExpired();
      }
    }
    handler.next(err);
  }

  Future<bool> _refresh() async {
    final refresh = await tokens.refreshToken();
    if (refresh == null || refresh.isEmpty) return false;
    try {
      final res = await Dio(BaseOptions(baseUrl: baseUrl))
          .post('/v1/auth/refresh', data: {'refreshToken': refresh});
      final data = (res.data as Map).cast<String, dynamic>();
      await tokens.save(
        access: data['access_token'] as String,
        refresh: data['refresh_token'] as String,
      );
      return true;
    } catch (_) {
      return false;
    }
  }
}
