import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/presentation/providers/auth_providers.dart';
import '../storage/event_cache.dart';
import '../storage/token_store.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

class SessionState {
  const SessionState(this.status, {this.needsOnboarding = false});
  const SessionState.unknown() : this(AuthStatus.unknown);

  final AuthStatus status;
  final bool needsOnboarding;

  SessionState copyWith({AuthStatus? status, bool? needsOnboarding}) =>
      SessionState(status ?? this.status, needsOnboarding: needsOnboarding ?? this.needsOnboarding);
}

/// Cross-cutting auth state that the router redirects on. Driven by the auth feature
/// (login/logout) and by the Dio refresh interceptor (session expiry).
class SessionController extends Notifier<SessionState> {
  @override
  SessionState build() => const SessionState.unknown();

  /// Cold-start: presence of a refresh token means we can act as authenticated, but
  /// `needs_onboarding` must come from a real `/v1/me` call — token presence alone
  /// can't tell us whether onboarding is still pending (D-037). A dead/expired token
  /// (the Dio refresh interceptor already tries once underneath this) falls back to
  /// unauthenticated rather than wrongly skipping onboarding.
  Future<void> bootstrap() async {
    final has = await ref.read(tokenStoreProvider).hasSession();
    if (!has) {
      // D-308 — the backstop, placed here rather than on the login path. Logout clears the cache and
      // awaits it, so the only way data survives is a process killed mid-logout: tokens gone, box
      // intact. That state is exactly "no session, data on disk", which is what this branch is.
      //
      // Deliberately NOT on login: clearing there would race the new user's first fetches and could
      // delete what they had just cached. Here nothing is signed in and nothing is reading.
      await clearDeviceCache();
      state = const SessionState(AuthStatus.unauthenticated);
      return;
    }
    try {
      final user = await ref.read(authRepositoryProvider).me();
      ref.read(currentUserProvider.notifier).state = user;
      state = SessionState(AuthStatus.authenticated, needsOnboarding: user.needsOnboarding);
    } catch (_) {
      state = const SessionState(AuthStatus.unauthenticated);
    }
  }

  void markAuthenticated({required bool needsOnboarding}) =>
      state = SessionState(AuthStatus.authenticated, needsOnboarding: needsOnboarding);

  /// D-308 — empties `kurx_cache`: chat rooms, per-room message windows, the offline **outbox**, read
  /// pointers and cached events.
  ///
  /// One call rather than a per-cache cleanup, because the box is the boundary. Its keys are scoped to
  /// a ROOM (`chat_msgs_{roomId}`, `chat_outbox_{roomId}`, …) and carry no user identity at all, so
  /// anything left behind is readable — and in the outbox's case, *sendable* — by whoever signs in next.
  Future<void> clearDeviceCache() async {
    try {
      await ref.read(cacheBoxProvider).clear();
    } catch (_) {
      // Never block sign-out on a storage failure: leaving the user signed in with their tokens intact
      // because a disk write failed is strictly worse than a cache that outlives one more session.
    }
  }

  void completeOnboarding() =>
      state = state.copyWith(needsOnboarding: false);

  /// Called by the refresh interceptor when refresh fails — token already cleared server-side.
  void onExpired() => state = const SessionState(AuthStatus.unauthenticated);

  Future<void> logout() async {
    await ref.read(tokenStoreProvider).clear();
    // D-308 — awaited, and before the state flip. The router redirects the moment `state` changes, so
    // clearing afterwards races a new screen that may already be reading the cache we are emptying.
    await clearDeviceCache();
    state = const SessionState(AuthStatus.unauthenticated);
  }
}

final sessionControllerProvider =
    NotifierProvider<SessionController, SessionState>(SessionController.new);
