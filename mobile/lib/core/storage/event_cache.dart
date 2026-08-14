import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive/hive.dart';

/// The app's non-secret local cache box.
///
/// Overridden in `main()` with the opened Hive box. Kept deliberately separate from
/// [TokenStore]: this is plaintext-at-rest and must never hold credentials or PII.
final cacheBoxProvider = Provider<Box>((ref) {
  throw UnimplementedError('cacheBoxProvider must be overridden in main() with the opened Hive box');
});

/// Offline read-through cache for the events list.
///
/// Exists so a cold start with no connectivity still renders the last known list instead of an
/// error screen — the repository writes on every successful fetch and falls back to [readUpcoming]
/// when the network call throws.
class EventCache {
  EventCache(this._box);

  final Box _box;

  static const _upcomingKey = 'events_upcoming';

  Future<void> saveUpcoming(List<Map<String, dynamic>> events) =>
      _box.put(_upcomingKey, jsonEncode(events));

  /// Returns an empty list rather than null when nothing is cached or the payload is unreadable —
  /// a corrupt cache entry must degrade to "no cache", never crash the list screen.
  List<Map<String, dynamic>> readUpcoming() {
    final raw = _box.get(_upcomingKey) as String?;
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

final eventCacheProvider = Provider<EventCache>((ref) => EventCache(ref.watch(cacheBoxProvider)));
