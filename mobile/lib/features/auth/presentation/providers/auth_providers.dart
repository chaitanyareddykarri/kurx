import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../../../core/session/session_controller.dart';
import '../../../../core/storage/token_store.dart';
import '../../data/datasources/auth_remote_data_source.dart';
import '../../data/repositories/auth_repository_impl.dart';
import '../../domain/entities/current_user.dart';
import '../../domain/repositories/auth_repository.dart';
import '../../domain/usecases/complete_otp_login.dart';
import '../../domain/usecases/request_otp.dart';
import '../../domain/usecases/sign_out.dart';

final authRemoteDataSourceProvider =
    Provider<AuthRemoteDataSource>((ref) => AuthRemoteDataSource(ref.watch(dioProvider)));

final authRepositoryProvider = Provider<AuthRepository>((ref) {
  return AuthRepositoryImpl(
    ref.watch(authRemoteDataSourceProvider),
    ref.watch(tokenStoreProvider),
  );
});

final _requestOtpProvider = Provider((ref) => RequestOtp(ref.watch(authRepositoryProvider)));
final _completeOtpLoginProvider =
    Provider((ref) => CompleteOtpLogin(ref.watch(authRepositoryProvider)));
final _signOutProvider = Provider((ref) => SignOut(ref.watch(authRepositoryProvider)));

/// Phone carried from the request step into the verify step.
final pendingPhoneProvider = StateProvider<String?>((_) => null);

/// Where to return after a successful sign-in (set when a guest starts login from a
/// public screen). Consumed by the router; `null` falls back to the events home.
final loginReturnToProvider = StateProvider<String?>((_) => null);

/// The signed-in user (null until loaded), for the onboarding/home screens.
final currentUserProvider = StateProvider<CurrentUser?>((_) => null);

/// Drives the OTP request/verify/logout actions; pages watch its [AsyncValue]
/// for loading/error. Success transitions [SessionController], which the router observes.
class AuthController extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<bool> requestOtp(String phone) async {
    state = const AsyncLoading();
    try {
      await ref.read(_requestOtpProvider).call(phone);
      ref.read(pendingPhoneProvider.notifier).state = phone;
      state = const AsyncData(null);
      return true;
    } catch (e, st) {
      state = AsyncError(e, st);
      return false;
    }
  }

  Future<bool> verifyOtp(String code) async {
    final phone = ref.read(pendingPhoneProvider);
    if (phone == null) return false;
    state = const AsyncLoading();
    try {
      final user = await ref.read(_completeOtpLoginProvider).call(phone: phone, code: code);
      ref.read(currentUserProvider.notifier).state = user;
      ref.read(sessionControllerProvider.notifier)
          .markAuthenticated(needsOnboarding: user.needsOnboarding);
      state = const AsyncData(null);
      return true;
    } catch (e, st) {
      state = AsyncError(e, st);
      return false;
    }
  }

  Future<void> logout() async {
    await ref.read(_signOutProvider).call();
    ref.read(currentUserProvider.notifier).state = null;
    ref.read(loginReturnToProvider.notifier).state = null;
    await ref.read(sessionControllerProvider.notifier).logout();
  }

  /// Submits Name + Username, then only flips the local onboarding flag once the
  /// server confirms `needs_onboarding: false` (D-037) — never before.
  Future<bool> completeOnboarding({required String name, required String username}) async {
    state = const AsyncLoading();
    try {
      final user = await ref
          .read(authRepositoryProvider)
          .completeOnboarding(name: name, username: username);
      ref.read(currentUserProvider.notifier).state = user;
      ref.read(sessionControllerProvider.notifier).completeOnboarding();
      state = const AsyncData(null);
      return true;
    } catch (e, st) {
      state = AsyncError(e, st);
      return false;
    }
  }

  Future<bool> checkUsernameAvailable(String username) =>
      ref.read(authRepositoryProvider).checkUsernameAvailable(username);
}

final authControllerProvider =
    AsyncNotifierProvider<AuthController, void>(AuthController.new);
