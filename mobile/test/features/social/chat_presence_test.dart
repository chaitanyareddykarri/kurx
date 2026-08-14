import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/social/data/datasources/chat_hub_client.dart';
import 'package:kurx_mobile/features/social/data/models/chat_mappers.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_attachment.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_message.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_room.dart';
import 'package:kurx_mobile/features/social/domain/repositories/chat_repository.dart';
import 'package:kurx_mobile/features/social/presentation/providers/chat_providers.dart';
import 'package:kurx_mobile/features/social/presentation/widgets/chat_presence.dart';

/// A repository that answers from memory. Presence is a realtime concern — every test here drives
/// [ChatRoomController.onEvent] directly, so nothing needs a socket or a server.
class _FakeChatRepository implements ChatRepository {
  _FakeChatRepository({
    this.online = const [],
    this.presenceEnabled = true,
    this.roomByIdMissing = false,
  });

  final List<String> online;
  final bool presenceEnabled;

  /// When true, [roomById] answers 404 — the shape of an eventId arriving on a room-keyed route.
  final bool roomByIdMissing;

  /// Which lookup the controller actually used, so a test can prove the fallback rather than infer it.
  final List<String> lookups = [];

  /// D-292: the controller resolves through this first, falling back to [room] only on a 404.
  /// Returning the same room keeps these tests exercising the path production now takes.
  @override
  Future<ChatRoom> roomById(String roomId) async {
    lookups.add('roomById');
    if (roomByIdMissing) {
      throw const ApiError(status: 404, code: 'not_found');
    }
    return _build();
  }

  @override
  Future<ChatRoom> room(String eventId) async {
    lookups.add('room');
    return _build();
  }

  /// Shared by both lookups and deliberately NOT logging: when `roomById` delegated to `room`, the
  /// log showed a fallback that never happened and the assertions below meant nothing.
  ChatRoom _build() {
    return ChatRoom(
        roomId: 'room-1',
        kind: 'Event',
        status: 'Active',
        postPolicy: 'Everyone',
        myRole: 'Member',
        unreadCount: 0,
        capabilities: const ChatCapabilities(
          canPost: true,
          canReply: true,
          canUpload: true,
          canPin: false,
          canDelete: true,
          canModerate: false,
          canMentionAll: false,
        ),
        onlineUserIds: online,
        presenceEnabled: presenceEnabled,
      );
  }

  @override
  List<ChatMessage> cachedMessages(String roomId) => const [];
  @override
  Future<List<ChatMessage>> latest(String roomId) async => const [];
  @override
  Future<List<ChatMessage>> older(String roomId, String beforeCursor) async => const [];
  @override
  Future<List<ChatMessage>> since(String roomId, String afterCursor) async => const [];
  @override
  Future<List<ChatMessage>> pending(String roomId) async => const [];
  @override
  Future<List<ChatMessage>> flushOutbox(String roomId) async => const [];
  @override
  Future<List<MyChat>> myChats({bool archived = false}) async => const [];
  @override
  Future<void> setArchived(String roomId, {required bool archived}) async {}
  @override
  Future<void> updateRoom(String roomId, {String? postPolicy, String? status}) async {}
  @override
  Future<void> muteMember(String roomId, String userId, int minutes) async {}
  @override
  Future<void> setBanned(String roomId, String userId, {required bool banned}) async {}
  @override
  String? localReadPointer(String roomId) => null;
  @override
  Future<void> markRead(String roomId, String lastReadMessageId) async {}
  @override
  Future<ChatMessage> queueSend(String roomId, String body,
          {String? replyToMessageId, List<ChatAttachment> attachments = const []}) async =>
      throw UnimplementedError();
  @override
  Future<ChatAttachment> uploadAttachment(String roomId, ChatAttachment pending, List<int> bytes,
          {void Function(double progress)? onProgress, CancelToken? cancelToken}) async =>
      throw UnimplementedError();
  @override
  Future<String> attachmentUrl(String attachmentId) async => '';
  @override
  Future<void> retry(String roomId, String clientMessageId) async {}
  @override
  Future<void> discard(String roomId, String clientMessageId) async {}
  @override
  Future<ChatMessage> editMessage(String roomId, String messageId, String body) async =>
      throw UnimplementedError();
  @override
  Future<void> deleteMessage(String roomId, String messageId) async {}
  @override
  Future<void> hideMessage(String roomId, String messageId) async {}

  // D-295 — presence is what this class exercises, so these are inert stubs. Anything a test here
  // actually drives goes through onEvent, never the network.
  @override
  Future<List<ChatReaction>> toggleReaction(String roomId, String messageId, String emoji) async =>
      const [];
  @override
  Future<List<ChatSearchHit>> search(String query, {String? roomId}) async => const [];
  @override
  Future<void> markDelivered(String roomId, String messageId) async {}
  @override
  Future<ChatMessage> forward(String messageId, String targetRoomId) async =>
      throw UnimplementedError();
  @override
  Future<void> setPinned(String roomId, {required bool pinned}) async {}
  @override
  Future<void> pinMessage(String messageId, {required bool pin, int? durationHours}) async {}
  @override
  Future<void> setModerator(String roomId, String userId, {required bool moderator}) async {}
  @override
  Future<void> setMuted(String roomId, DateTime? until) async {}
  @override
  Future<List<ChatAttachment>> roomMedia(String roomId) async => const [];
  @override
  Future<void> report(String messageId, String reason) async {}
}

ChatEvent _event(String type, Map<String, dynamic> payload) =>
    ChatEvent(type: type, roomId: 'room-1', payload: payload);

Future<ChatRoomController> _controller(
  ProviderContainer container, {
  List<String> online = const [],
  bool presenceEnabled = true,
}) async {
  // autoDispose tears the controller down the moment nothing listens, so hold a subscription for
  // the length of the test.
  container.listen(chatRoomControllerProvider('event-1'), (_, _) {});
  final controller = container.read(chatRoomControllerProvider('event-1').notifier);
  // The controller loads the room in its constructor; let that settle before asserting.
  await Future<void>.delayed(Duration.zero);
  return controller;
}

ProviderContainer _container({
  List<String> online = const [],
  bool presenceEnabled = true,
}) {
  final container = ProviderContainer(overrides: [
    chatRepositoryProvider.overrideWithValue(
      _FakeChatRepository(online: online, presenceEnabled: presenceEnabled),
    ),
  ]);
  addTearDown(container.dispose);
  return container;
}

void main() {
  // ── D-292: room-keyed navigation ─────────────────────────────────────────────
  group('route id resolution', () {
    test('a room id loads through the room endpoint, with no event fallback', () async {
      final repo = _FakeChatRepository();
      final container = ProviderContainer(
          overrides: [chatRepositoryProvider.overrideWithValue(repo)]);
      addTearDown(container.dispose);

      container.listen(chatRoomControllerProvider('room-1'), (_, _) {});
      container.read(chatRoomControllerProvider('room-1').notifier);
      await Future<void>.delayed(Duration.zero);

      expect(container.read(chatRoomControllerProvider('room-1')).room, isNotNull);
      // The event-addressed door is not touched: a room id must not cost an extra round trip.
      expect(repo.lookups, ['roomById']);
    });

    test('an event id from an older deep link still resolves, via the event endpoint', () async {
      final repo = _FakeChatRepository(roomByIdMissing: true);
      final container = ProviderContainer(
          overrides: [chatRepositoryProvider.overrideWithValue(repo)]);
      addTearDown(container.dispose);

      container.listen(chatRoomControllerProvider('event-1'), (_, _) {});
      container.read(chatRoomControllerProvider('event-1').notifier);
      await Future<void>.delayed(Duration.zero);

      final state = container.read(chatRoomControllerProvider('event-1'));
      // Loaded, and loaded as the REAL room — everything downstream keys on room.roomId, which is
      // what makes an eventId entry point safe rather than merely tolerated.
      expect(state.room?.roomId, 'room-1');
      expect(state.error, isNull);
      expect(repo.lookups, ['roomById', 'room']);
    });
  });

  group('presence state', () {
    test('the roster is seeded from the room fetch', () async {
      final container = _container(online: ['user-2', 'user-3']);
      await _controller(container, online: ['user-2', 'user-3']);

      final state = container.read(chatRoomControllerProvider('event-1'));
      expect(state.presenceEnabled, isTrue);
      expect(state.onlineUserIds, {'user-2', 'user-3'});
    });

    test('a presence event adds and removes a member', () async {
      final container = _container();
      final controller = await _controller(container);

      controller.onEvent(_event('PresenceChanged', {'userId': 'user-2', 'online': true}));
      expect(container.read(chatRoomControllerProvider('event-1')).onlineUserIds, {'user-2'});

      controller.onEvent(_event('PresenceChanged', {'userId': 'user-2', 'online': false}));
      expect(container.read(chatRoomControllerProvider('event-1')).onlineUserIds, isEmpty);
    });

    test('going offline also clears that member\'s typing indicator', () async {
      final container = _container();
      final controller = await _controller(container);

      controller.onEvent(_event('TypingChanged',
          {'userId': 'user-2', 'userName': 'Alice', 'isTyping': true, 'ttlSeconds': 8}));
      expect(container.read(chatRoomControllerProvider('event-1')).typing, isNotEmpty);

      controller.onEvent(_event('PresenceChanged', {'userId': 'user-2', 'online': false}));
      expect(container.read(chatRoomControllerProvider('event-1')).typing, isEmpty);
    });

    test('presence is never inferred from a message arriving', () async {
      final container = _container();
      final controller = await _controller(container);

      controller.onEvent(const ChatEvent(
        type: 'MessageReceived',
        roomId: 'room-1',
        id: 'm1',
        payload: {'senderId': 'user-9'},
      ));
      await Future<void>.delayed(Duration.zero);

      // A sender is obviously connected, but only the server may say so.
      expect(container.read(chatRoomControllerProvider('event-1')).onlineUserIds, isEmpty);
    });

    test('an unknown event type is ignored rather than throwing', () async {
      final container = _container();
      final controller = await _controller(container);

      controller.onEvent(_event('ReactionAdded', {'emoji': '🎉'}));
      expect(container.read(chatRoomControllerProvider('event-1')).error, isNull);
    });
  });

  group('typing', () {
    test('a repeat pushes the expiry out instead of stacking entries', () async {
      final container = _container();
      final controller = await _controller(container);

      for (var i = 0; i < 3; i++) {
        controller.onEvent(_event('TypingChanged',
            {'userId': 'user-2', 'userName': 'Alice', 'isTyping': true, 'ttlSeconds': 8}));
      }

      expect(container.read(chatRoomControllerProvider('event-1')).typing.length, 1);
    });

    test('an expired entry stops being reported even without a stop event', () async {
      final container = _container();
      final controller = await _controller(container);

      // A TTL the server would never send, standing in for "the stop event never arrived".
      controller.onEvent(_event('TypingChanged',
          {'userId': 'user-2', 'userName': 'Alice', 'isTyping': true, 'ttlSeconds': 0}));

      await Future<void>.delayed(const Duration(milliseconds: 20));
      final state = container.read(chatRoomControllerProvider('event-1'));
      expect(state.typistsExcept('me'), isEmpty);
    });

    test('my own typing is never shown back to me', () async {
      final container = _container();
      final controller = await _controller(container);

      controller.onEvent(_event('TypingChanged',
          {'userId': 'me', 'userName': 'Me', 'isTyping': true, 'ttlSeconds': 8}));

      expect(container.read(chatRoomControllerProvider('event-1')).typistsExcept('me'), isEmpty);
    });
  });

  group('read receipts', () {
    test('the pointer only ever moves forward', () async {
      final container = _container();
      final controller = await _controller(container);

      controller.onEvent(
          _event('ReadReceiptChanged', {'userId': 'user-2', 'lastReadMessageId': 'm5'}));
      controller.onEvent(
          _event('ReadReceiptChanged', {'userId': 'user-2', 'lastReadMessageId': 'm3'}));

      expect(container.read(chatRoomControllerProvider('event-1')).readPointers['user-2'], 'm5');
    });

    test('the furthest pointer across members wins, ignoring my own', () async {
      final container = _container();
      final controller = await _controller(container);

      controller.onEvent(
          _event('ReadReceiptChanged', {'userId': 'user-2', 'lastReadMessageId': 'm2'}));
      controller.onEvent(
          _event('ReadReceiptChanged', {'userId': 'user-3', 'lastReadMessageId': 'm7'}));
      controller.onEvent(
          _event('ReadReceiptChanged', {'userId': 'me', 'lastReadMessageId': 'm9'}));

      final state = container.read(chatRoomControllerProvider('event-1'));
      expect(state.highestReadByOthers('me'), 'm7');
    });
  });

  group('presence disabled', () {
    test('an empty roster is reported as unknown, not as everyone offline', () async {
      final container = _container(presenceEnabled: false);
      await _controller(container, presenceEnabled: false);

      final state = container.read(chatRoomControllerProvider('event-1'));
      expect(state.presenceEnabled, isFalse);
      // isOnline stays false, and the UI keys off presenceEnabled to hide the dot entirely.
      expect(state.isOnline('user-2'), isFalse);
    });

    test('the room DTO defaults to presence disabled when the server omits the fields', () {
      final room = ChatMappers.room(const {
        'roomId': 'room-1',
        'kind': 'Event',
        'status': 'Active',
        'postPolicy': 'Everyone',
        'myRole': 'Member',
        'unreadCount': 0,
      });

      expect(room.presenceEnabled, isFalse);
      expect(room.onlineUserIds, isEmpty);
    });

    test('the room DTO parses the roster the server sends', () {
      final room = ChatMappers.room(const {
        'roomId': 'room-1',
        'kind': 'Event',
        'status': 'Active',
        'postPolicy': 'Everyone',
        'myRole': 'Member',
        'unreadCount': 0,
        'onlineUserIds': ['user-2', 'user-3'],
        'presenceEnabled': true,
      });

      expect(room.presenceEnabled, isTrue);
      expect(room.onlineUserIds, ['user-2', 'user-3']);
    });
  });

  group('connection loss', () {
    test('a dropped socket clears indicators but keeps read receipts', () async {
      final container = _container(online: ['user-2']);
      final controller = await _controller(container, online: ['user-2']);

      controller.onEvent(_event('TypingChanged',
          {'userId': 'user-2', 'userName': 'Alice', 'isTyping': true, 'ttlSeconds': 8}));
      controller.onEvent(
          _event('ReadReceiptChanged', {'userId': 'user-2', 'lastReadMessageId': 'm5'}));

      controller.debugConnectionLost();

      final state = container.read(chatRoomControllerProvider('event-1'));
      expect(state.live, isFalse);
      expect(state.onlineUserIds, isEmpty);
      expect(state.typing, isEmpty);
      // Read stays read — only presence is unknowable while disconnected.
      expect(state.readPointers['user-2'], 'm5');
    });
  });

  group('presence widgets', () {
    Widget host(Widget child, ProviderContainer container) => UncontrolledProviderScope(
          container: container,
          child: MaterialApp(theme: AppTheme.light(), home: Scaffold(body: child)),
        );

    testWidgets('the dot is omitted entirely when presence is unavailable', (tester) async {
      await tester.pumpWidget(MaterialApp(
        theme: AppTheme.light(),
        home: const Scaffold(
          body: PresenceAvatar(name: 'Alice', size: 28, online: false, presenceKnown: false),
        ),
      ));

      // No presence semantics at all — the dot is absent, not merely grey.
      expect(tester.getSemantics(find.byType(PresenceAvatar)).label, isNot(contains('online')));
      expect(tester.getSemantics(find.byType(PresenceAvatar)).label, isNot(contains('offline')));
    });

    testWidgets('online and offline are announced as words, not colour alone', (tester) async {
      await tester.pumpWidget(MaterialApp(
        theme: AppTheme.light(),
        home: const Scaffold(
          body: PresenceAvatar(name: 'Alice', size: 28, online: true, presenceKnown: true),
        ),
      ));

      expect(
        tester.getSemantics(find.byType(PresenceAvatar)).label,
        contains('online'),
      );
    });

    testWidgets('the typing banner names one, two, then overflows', (tester) async {
      final container = _container();
      // Inside testWidgets the clock is fake, so the room load is settled with a pump rather than a
      // real delay — awaiting one here would never complete.
      container.listen(chatRoomControllerProvider('event-1'), (_, _) {});
      final controller = container.read(chatRoomControllerProvider('event-1').notifier);

      await tester.pumpWidget(host(
        const TypingBanner(roomId: 'event-1', myUserId: 'me'),
        container,
      ));
      await tester.pump();
      expect(find.byType(SizedBox), findsWidgets); // nothing typing yet

      controller.onEvent(_event('TypingChanged',
          {'userId': 'u2', 'userName': 'Alice', 'isTyping': true, 'ttlSeconds': 8}));
      await tester.pump();
      expect(find.textContaining('Alice is typing'), findsOneWidget);

      controller.onEvent(_event('TypingChanged',
          {'userId': 'u3', 'userName': 'Bob', 'isTyping': true, 'ttlSeconds': 8}));
      await tester.pump();
      expect(find.textContaining('Alice and Bob are typing'), findsOneWidget);

      controller.onEvent(_event('TypingChanged',
          {'userId': 'u4', 'userName': 'Carol', 'isTyping': true, 'ttlSeconds': 8}));
      await tester.pump();
      expect(find.textContaining('2 others are typing'), findsOneWidget);

      // Clear typing so the prune timer cancels, then tear the banner down so its pulse ticker
      // stops — a widget test fails on either one still being alive at the end.
      for (final id in ['u2', 'u3', 'u4']) {
        controller.onEvent(_event('TypingChanged', {'userId': id, 'isTyping': false}));
      }
      await tester.pump();
      await tester.pumpWidget(const SizedBox.shrink());
    });
  });
}
