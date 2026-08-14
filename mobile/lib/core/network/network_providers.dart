import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../session/session_controller.dart';
import '../storage/token_store.dart';
import 'app_config.dart';
import 'auth_interceptor.dart';

/// The single Dio client: base URL from build config, bounded timeouts, and the
/// auth/refresh interceptor. All feature datasources depend on this provider.
final dioProvider = Provider<Dio>((ref) {
  final dio = Dio(
    BaseOptions(
      baseUrl: AppConfig.apiBase,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 20),
      contentType: Headers.jsonContentType,
      responseType: ResponseType.json,
    ),
  );

  dio.interceptors.add(
    AuthInterceptor(
      tokens: ref.read(tokenStoreProvider),
      baseUrl: AppConfig.apiBase,
      onExpired: () => ref.read(sessionControllerProvider.notifier).onExpired(),
    ),
  );

  return dio;
});
