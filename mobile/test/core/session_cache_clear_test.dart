import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hive/hive.dart';
import 'package:kurx_mobile/core/session/session_controller.dart';
import 'package:kurx_mobile/core/storage/chat_cache.dart';
import 'package:kurx_mobile/core/storage/event_cache.dart';
import 'package:kurx_mobile/core/storage/token_store.dart';

/// D-308 — logging out destroys this device's offline data.
///
/// The defect these pin: logout cleared the tokens and nothing else, and the cache keys carry no user
/// identity (`chat_rooms`, `chat_msgs_{roomId}`, `chat_outbox_{roomId}`, …). The next person to sign in
/// on the device read the same keys — seeing the previous user's rooms and messages, and, because
/// opening a room flushes the outbox on the CURRENT credentials, sending their queued message as
/// themselves.
void main() {
  late Directory tempDir;
  late Box box;

  setUpAll(() async {
    tempDir = await Directory.systemTemp.createTemp('kurx_session_test');
    Hive.init(tempDir.path);
  });

  tearDownAll(() async {
    await Hive.close();
    await tempDir.delete(recursive: true);
  });

  setUp(() async {
    box = await Hive.openBox('session_test_${DateTime.now().microsecondsSinceEpoch}');
  });

  tearDown(() async => box.deleteFromDisk());

  /// Everything a signed-in user leaves on the device, written the way the app writes it.
  Future<void> seedPreviousUsersData() async {
    await box.put('chat_rooms', '[{"roomId":"r1"}]');
    await box.put('chat_msgs_r1', '[{"id":"m1","body":"private"}]');
    await box.put('chat_outbox_r1', '[{"clientMessageId":"c1","body":"unsent, addressed from A"}]');
    await box.put('chat_read_r1', 'm1');
    await box.put('events_cached', '[{"id":"e1"}]');
  }

  ProviderContainer containerWith(Box b) => ProviderContainer(overrides: [
        cacheBoxProvider.overrideWithValue(b),
        // The real store writes to secure storage, which has no binding under `flutter test`. Logout
        // must still clear the cache, so the token half is stubbed rather than exercised here.
        tokenStoreProvider.overrideWithValue(_NoopTokenStore()),
      ]);

  test('logout empties every offline cache the previous user left behind', () async {
    await seedPreviousUsersData();
    expect(box.isNotEmpty, isTrue, reason: 'guard: the seed must actually write something');

    final container = containerWith(box);
    addTearDown(container.dispose);

    await container.read(sessionControllerProvider.notifier).logout();

    // The whole box, not a hand-picked list of keys: the box is the boundary, and a per-key cleanup is
    // how the next cache added to it would quietly re-open this hole.
    expect(box.isEmpty, isTrue);
  });

  test('the offline outbox cannot survive a change of user', () async {
    await seedPreviousUsersData();
    final container = containerWith(box);
    addTearDown(container.dispose);

    await container.read(sessionControllerProvider.notifier).logout();

    // The sharpest half of D-308: opening a room flushes `chat_outbox_{roomId}` through the shared Dio,
    // which carries whoever is signed in NOW. A surviving entry here is a message delivered under the
    // wrong account, with the previous user's clientMessageId.
    expect(ChatCache(box).readOutbox('r1'), isEmpty);
  });

  test('a start-up with no session clears data a crash mid-logout left behind', () async {
    await seedPreviousUsersData();
    final container = containerWith(box);
    addTearDown(container.dispose);

    // The one window logout cannot close: the process died after the tokens went and before the cache
    // did. On the next launch that is indistinguishable from "no session, data on disk" — which is
    // this branch, and why the backstop lives here rather than on the login path where it would race
    // the new user's first fetches.
    await container.read(sessionControllerProvider.notifier).bootstrap();

    expect(box.isEmpty, isTrue);
    expect(container.read(sessionControllerProvider).status, AuthStatus.unauthenticated);
  });

  test('logout still signs the user out when the cache cannot be cleared', () async {
    final closed = await Hive.openBox('session_closed_${DateTime.now().microsecondsSinceEpoch}');
    await closed.put('chat_rooms', '[]');
    await closed.close();   // every operation on it now throws

    final container = containerWith(closed);
    addTearDown(container.dispose);

    // Leaving someone signed in because a disk write failed is strictly worse than a cache that
    // outlives one more session, so the failure is swallowed and the state still flips.
    await container.read(sessionControllerProvider.notifier).logout();
    expect(container.read(sessionControllerProvider).status, AuthStatus.unauthenticated);
  });
}

/// Secure storage has no platform binding under `flutter test`, and the token half of logout is not
/// what these assert — the cache half is.
class _NoopTokenStore implements TokenStore {
  @override
  Future<void> clear() async {}
  @override
  Future<String?> accessToken() async => null;
  @override
  Future<String?> refreshToken() async => null;
  @override
  Future<bool> hasSession() async => false;
  @override
  Future<void> save({required String access, required String refresh}) async {}
  @override
  Future<void> saveDeviceId(String deviceId) async {}
  @override
  Future<String?> deviceId() async => null;
  @override
  Future<void> forgetDevice() async {}
}
