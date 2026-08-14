import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive/hive.dart';

import '../../features/social/data/models/chat_mappers.dart';
import '../../features/social/domain/entities/chat_message.dart';
import '../../features/social/domain/entities/chat_room.dart';
import 'event_cache.dart';

/// Offline store for chat, in the app's existing non-secret Hive box.
///
/// Deliberately the same `kurx_cache` box [EventCache] uses — chat is not secret, and adding a
/// second database (Isar/Drift/sqflite) for what is a bounded list of JSON rows would be a new
/// dependency earning nothing. Keys are namespaced per room.
///
/// ponytail: the per-room window is capped at [maxCachedPerRoom]; older history is re-fetched from
/// the server on demand rather than kept forever. Raise the cap or move to a real database only if
/// offline scrollback depth becomes a product requirement.
class ChatCache {
  ChatCache(this._box);

  final Box _box;

  static const int maxCachedPerRoom = 200;

  static String _messagesKey(String roomId) => 'chat_msgs_$roomId';
  static String _outboxKey(String roomId) => 'chat_outbox_$roomId';
  static String _readKey(String roomId) => 'chat_read_$roomId';
  static const _roomsKey = 'chat_rooms';

  // ── Rooms list ────────────────────────────────────────────────────────────────────────────

  Future<void> saveRooms(List<MyChat> rooms) =>
      _box.put(_roomsKey, jsonEncode(rooms.map(ChatMappers.myChatToJson).toList()));

  List<MyChat> readRooms() => _decodeList(_roomsKey).map(ChatMappers.myChat).toList();

  // ── Messages ──────────────────────────────────────────────────────────────────────────────

  List<ChatMessage> readMessages(String roomId) =>
      _decodeList(_messagesKey(roomId)).map(ChatMappers.messageFromCache).toList();

  /// Merges [incoming] into the cached window, de-duplicating and re-sorting.
  ///
  /// De-duplication is by server id **and** by `clientMessageId`: when the server echoes a message
  /// this device sent, the optimistic row and the confirmed row are the same message under two
  /// identities, and collapsing them is what stops a self-sent message appearing twice.
  Future<List<ChatMessage>> mergeMessages(String roomId, List<ChatMessage> incoming) async {
    final merged = mergeInto(readMessages(roomId), incoming);
    final window = merged.length > maxCachedPerRoom
        ? merged.sublist(merged.length - maxCachedPerRoom)
        : merged;
    await _box.put(_messagesKey(roomId), jsonEncode(window.map(ChatMappers.messageToJson).toList()));
    return window;
  }

  /// Pure merge, exposed for testing and for in-memory use by the controller.
  static List<ChatMessage> mergeInto(List<ChatMessage> existing, List<ChatMessage> incoming) {
    final byId = <String, ChatMessage>{};
    final idByClientId = <String, String>{};

    void put(ChatMessage m) {
      // A confirmed message supersedes the optimistic row that carried the same clientMessageId.
      final clientId = m.clientMessageId;
      if (clientId != null) {
        final existingId = idByClientId[clientId];
        if (existingId != null && existingId != m.id) byId.remove(existingId);
        idByClientId[clientId] = m.id;
      }
      byId[m.id] = m;
    }

    for (final m in existing) {
      put(m);
    }
    for (final m in incoming) {
      put(m);
    }

    final all = byId.values.toList()..sort((a, b) => a.compareTo(b));
    return all;
  }

  Future<void> clearMessages(String roomId) => _box.delete(_messagesKey(roomId));

  // ── Outbox ────────────────────────────────────────────────────────────────────────────────

  /// Messages this device has accepted from the user but not yet confirmed by the server. Survives
  /// restarts so a send composed offline is not silently lost.
  List<ChatMessage> readOutbox(String roomId) =>
      _decodeList(_outboxKey(roomId)).map(ChatMappers.messageFromCache).toList();

  Future<void> saveOutbox(String roomId, List<ChatMessage> pending) =>
      _box.put(_outboxKey(roomId), jsonEncode(pending.map(ChatMappers.messageToJson).toList()));

  Future<void> addToOutbox(String roomId, ChatMessage message) async {
    final pending = readOutbox(roomId)..add(message);
    await saveOutbox(roomId, pending);
  }

  Future<void> removeFromOutbox(String roomId, String clientMessageId) async {
    final pending = readOutbox(roomId)
      ..removeWhere((m) => m.clientMessageId == clientMessageId);
    await saveOutbox(roomId, pending);
  }

  // ── Read pointer ──────────────────────────────────────────────────────────────────────────

  String? readPointer(String roomId) => _box.get(_readKey(roomId)) as String?;

  Future<void> savePointer(String roomId, String messageId) =>
      _box.put(_readKey(roomId), messageId);

  // ── internals ─────────────────────────────────────────────────────────────────────────────

  /// Returns an empty list when nothing is cached or the payload is unreadable — a corrupt entry
  /// must degrade to "no cache", never crash a screen. Same contract as [EventCache].
  List<Map<String, dynamic>> _decodeList(String key) {
    final raw = _box.get(key) as String?;
    if (raw == null || raw.isEmpty) return const [];
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! List) return const [];
      return decoded.whereType<Map>().map((e) => Map<String, dynamic>.from(e)).toList();
    } on FormatException {
      return const [];
    }
  }
}

final chatCacheProvider = Provider<ChatCache>((ref) => ChatCache(ref.watch(cacheBoxProvider)));
