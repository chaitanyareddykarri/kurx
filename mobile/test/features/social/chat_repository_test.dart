import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hive/hive.dart';
import 'package:kurx_mobile/core/storage/chat_cache.dart';
import 'package:kurx_mobile/features/social/data/datasources/chat_remote_data_source.dart';
import 'package:kurx_mobile/features/social/data/repositories/chat_repository_impl.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_message.dart';

/// Scriptable adapter — same shape as the auth tests use, so a flaky network can be walked through
/// deliberately rather than mocked away.
class _ScriptedAdapter implements HttpClientAdapter {
  final Map<String, List<Object>> _script = {};
  final List<RequestOptions> calls = [];

  void queue(String path, List<Object> responses) => _script[path] = [...responses];

  int callsTo(String path) => calls.where((c) => c.path == path).length;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? _, Future<void>? _) async {
    calls.add(options);
    final queued = _script[options.path];
    if (queued == null || queued.isEmpty) return ResponseBody.fromString('{}', 404);

    final next = queued.length == 1 ? queued.first : queued.removeAt(0);
    if (next is DioExceptionType) throw DioException(requestOptions: options, type: next);

    final status = next is _Status ? next.code : 200;
    final body = next is _Status ? next.body : next;
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {Headers.contentTypeHeader: [Headers.jsonContentType]},
    );
  }

  @override
  void close({bool force = false}) {}
}

class _Status {
  const _Status(this.code, this.body);
  final int code;
  final Object body;
}

Map<String, dynamic> _msg(String id, String body, {String? clientId, String? createdAt}) => {
      'id': id,
      'roomId': 'room-1',
      'clientMessageId': clientId,
      'senderId': 'user-2',
      'senderName': 'Someone',
      'senderRole': 'Member',
      'kind': 'Text',
      'body': body,
      'replyToMessageId': null,
      'isPinned': false,
      'isDeleted': false,
      'attachments': const [],
      'createdAt': createdAt ?? '2026-07-19T10:00:00Z',
    };

void main() {
  late Directory tempDir;
  late Box box;
  late _ScriptedAdapter adapter;
  late ChatRepositoryImpl repo;
  late ChatCache cache;

  setUpAll(() async {
    tempDir = await Directory.systemTemp.createTemp('kurx_chat_test');
    Hive.init(tempDir.path);
  });

  tearDownAll(() async {
    await Hive.close();
    await tempDir.delete(recursive: true);
  });

  setUp(() async {
    box = await Hive.openBox('chat_test_${DateTime.now().microsecondsSinceEpoch}');
    adapter = _ScriptedAdapter();
    final dio = Dio(BaseOptions(baseUrl: 'http://test'))..httpClientAdapter = adapter;
    cache = ChatCache(box);
    repo = ChatRepositoryImpl(ChatRemoteDataSource(dio), cache);
  });

  tearDown(() async => box.deleteFromDisk());

  group('ordering and de-duplication', () {
    test('messages sort by (createdAt, id), never by createdAt alone', () {
      // Same instant, different ids — the case CreatedAt-only ordering got wrong. The backend
      // breaks the tie on id, and the client must agree or pagination and merge disagree.
      final a = ChatMessage(
          id: 'aaa', roomId: 'room-1', body: 'a', createdAt: DateTime.utc(2026, 7, 19, 10));
      final b = ChatMessage(
          id: 'bbb', roomId: 'room-1', body: 'b', createdAt: DateTime.utc(2026, 7, 19, 10));

      final merged = ChatCache.mergeInto([b], [a]);
      expect(merged.map((m) => m.id), ['aaa', 'bbb']);
    });

    test('the same message arriving twice is stored once', () {
      final m = ChatMessage(
          id: 'x', roomId: 'room-1', body: 'once', createdAt: DateTime.utc(2026, 7, 19));
      expect(ChatCache.mergeInto([m], [m]).length, 1);
    });

    test('a confirmed message replaces the optimistic row with the same clientMessageId', () {
      final optimistic = ChatMessage(
        id: 'local:c-1',
        roomId: 'room-1',
        clientMessageId: 'c-1',
        body: 'hello',
        createdAt: DateTime.utc(2026, 7, 19, 10),
        status: ChatSendStatus.pending,
      );
      final confirmed = ChatMessage(
        id: 'server-1',
        roomId: 'room-1',
        clientMessageId: 'c-1',
        body: 'hello',
        createdAt: DateTime.utc(2026, 7, 19, 10),
      );

      final merged = ChatCache.mergeInto([optimistic], [confirmed]);

      // One message, under the server's identity — not two bubbles saying the same thing.
      expect(merged.length, 1);
      expect(merged.single.id, 'server-1');
      expect(merged.single.status, ChatSendStatus.sent);
    });
  });

  group('pagination', () {
    test('latest merges the newest page into the cache', () async {
      adapter.queue('/v1/chat/rooms/room-1/messages', [
        {'messages': [_msg('m1', 'one'), _msg('m2', 'two')], 'olderCursor': 'm1', 'newerCursor': 'm2'},
      ]);

      final messages = await repo.latest('room-1');
      expect(messages.map((m) => m.body), ['one', 'two']);
      expect(cache.readMessages('room-1').length, 2);
    });

    test('older passes the cursor through opaquely and returns empty at the start', () async {
      adapter.queue('/v1/chat/rooms/room-1/messages', [
        {'messages': const [], 'olderCursor': null, 'newerCursor': null},
      ]);

      expect(await repo.older('room-1', 'cursor-abc'), isEmpty);
      expect(adapter.calls.last.queryParameters['before'], 'cursor-abc');
    });

    test('since uses the after cursor — the delta primitive', () async {
      adapter.queue('/v1/chat/rooms/room-1/messages', [
        {'messages': [_msg('m3', 'missed')], 'olderCursor': 'm3', 'newerCursor': 'm3'},
      ]);

      final delta = await repo.since('room-1', 'm2');
      expect(delta.single.body, 'missed');
      expect(adapter.calls.last.queryParameters['after'], 'm2');
      expect(adapter.calls.last.queryParameters.containsKey('before'), isFalse);
    });
  });

  group('offline behaviour', () {
    test('latest falls back to cache when the network is unreachable', () async {
      adapter.queue('/v1/chat/rooms/room-1/messages', [
        {'messages': [_msg('m1', 'cached')], 'olderCursor': 'm1', 'newerCursor': 'm1'},
        DioExceptionType.connectionError,
      ]);

      await repo.latest('room-1');                 // warm the cache
      final offline = await repo.latest('room-1'); // now offline

      expect(offline.single.body, 'cached');
    });

    test('myChats falls back to the cached room list when offline', () async {
      adapter.queue('/v1/me/chats', [
        [
          {'roomId': 'room-1', 'eventId': 'ev-1', 'eventTitle': 'Cached Event', 'unreadCount': 2},
        ],
        DioExceptionType.connectionError,
      ]);

      await repo.myChats();
      final offline = await repo.myChats();

      expect(offline.single.eventTitle, 'Cached Event');
    });
  });

  group('optimistic send and the outbox', () {
    test('queueSend renders immediately as pending and survives a restart', () async {
      final optimistic = await repo.queueSend('room-1', 'hello');

      expect(optimistic.status, ChatSendStatus.pending);
      expect(optimistic.clientMessageId, isNotNull);
      // Read through a *fresh* cache over the same box — i.e. what a relaunch would see.
      expect(ChatCache(box).readOutbox('room-1').single.body, 'hello');
    });

    test('flush confirms the message and clears it from the outbox', () async {
      final optimistic = await repo.queueSend('room-1', 'hello');
      adapter.queue('/v1/chat/rooms/room-1/messages',
          [_msg('server-1', 'hello', clientId: optimistic.clientMessageId)]);

      final confirmed = await repo.flushOutbox('room-1');

      expect(confirmed.single.id, 'server-1');
      expect(cache.readOutbox('room-1'), isEmpty);
      expect(repo.cachedMessages('room-1').single.status, ChatSendStatus.sent);
    });

    test('the same clientMessageId is reused across retries, so the server can dedupe', () async {
      final optimistic = await repo.queueSend('room-1', 'retry me');
      adapter.queue('/v1/chat/rooms/room-1/messages', [
        DioExceptionType.connectionError,
        _msg('server-1', 'retry me', clientId: optimistic.clientMessageId),
      ]);

      await repo.flushOutbox('room-1');   // offline: stays queued
      expect(cache.readOutbox('room-1').single.clientMessageId, optimistic.clientMessageId);

      await repo.flushOutbox('room-1');   // back online
      expect(cache.readOutbox('room-1'), isEmpty);
      expect(repo.cachedMessages('room-1').where((m) => m.body == 'retry me').length, 1);
    });

    test('an offline flush leaves the message queued rather than failing it', () async {
      await repo.queueSend('room-1', 'stuck');
      adapter.queue('/v1/chat/rooms/room-1/messages', [DioExceptionType.connectionError]);

      await repo.flushOutbox('room-1');

      expect(cache.readOutbox('room-1').single.status, ChatSendStatus.pending);
    });

    test('a definite refusal marks the message failed instead of retrying forever', () async {
      await repo.queueSend('room-1', 'while muted');
      adapter.queue('/v1/chat/rooms/room-1/messages',
          [const _Status(403, {'error': 'muted'})]);

      await repo.flushOutbox('room-1');

      // Retrying identical content cannot succeed, so the user is told rather than left waiting.
      expect(cache.readOutbox('room-1').single.status, ChatSendStatus.failed);
    });

    test('a failed message is skipped until explicitly retried', () async {
      final optimistic = await repo.queueSend('room-1', 'nope');
      adapter.queue('/v1/chat/rooms/room-1/messages',
          [const _Status(403, {'error': 'muted'})]);
      await repo.flushOutbox('room-1');

      final before = adapter.callsTo('/v1/chat/rooms/room-1/messages');
      await repo.flushOutbox('room-1');
      expect(adapter.callsTo('/v1/chat/rooms/room-1/messages'), before, reason: 'not re-sent');

      adapter.queue('/v1/chat/rooms/room-1/messages',
          [_msg('server-9', 'nope', clientId: optimistic.clientMessageId)]);
      await repo.retry('room-1', optimistic.clientMessageId!);
      await repo.flushOutbox('room-1');

      expect(cache.readOutbox('room-1'), isEmpty);
    });
  });

  group('read pointer', () {
    test('is stored locally and synced to the server', () async {
      adapter.queue('/v1/chat/rooms/room-1/read', [{'ok': true}]);

      await repo.markRead('room-1', 'server-1');

      expect(repo.localReadPointer('room-1'), 'server-1');
      expect(adapter.callsTo('/v1/chat/rooms/room-1/read'), 1);
    });

    test('an unconfirmed local id is never synced', () async {
      await repo.markRead('room-1', 'local:abc');

      expect(repo.localReadPointer('room-1'), isNull);
      expect(adapter.callsTo('/v1/chat/rooms/room-1/read'), 0);
    });

    test('a failed sync still advances the local pointer', () async {
      adapter.queue('/v1/chat/rooms/room-1/read', [DioExceptionType.connectionError]);

      await repo.markRead('room-1', 'server-5');

      // The pointer only moves forward server-side, so a later successful call supersedes this —
      // there is nothing to retry and the badge must not stay stuck.
      expect(repo.localReadPointer('room-1'), 'server-5');
    });
  });

  test('clientMessageId is a v4 uuid the server will accept as a Guid', () {
    final id = ChatRepositoryImpl.newClientMessageId();
    expect(
      RegExp(r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$')
          .hasMatch(id),
      isTrue,
      reason: 'got $id',
    );
  });
}
