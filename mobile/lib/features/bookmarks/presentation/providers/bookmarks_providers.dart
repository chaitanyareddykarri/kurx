import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/storage/event_cache.dart';
import '../../../events/domain/entities/event_summary.dart';
import '../../../orders/presentation/providers/attendee_providers.dart';

/// Saved events, **server-backed with an offline cache**.
///
/// This used to be Hive-only, with a comment saying `there is no server "saved" endpoint (D-019
/// doesn't cover it)`. That stopped being true when D-064 shipped `POST/DELETE /v1/events/{id}/save`
/// and `GET /v1/me/saved` — the web app has used them since. Local-only meant saves never synced
/// between a user's phone and the website, and vanished with the app.
///
/// Design, in priority order:
/// 1. **Hive first, synchronously** — `build()` returns the cached list so the Saved and Calendar
///    screens paint instantly and still work offline. The five existing call sites stay synchronous.
/// 2. **Server reconciles** — a refresh runs on construction and replaces state with the truth.
/// 3. **Optimistic toggle** — the star flips immediately, then the call goes out; a failure reverts
///    that exact change rather than refetching and hoping.
class BookmarksController extends Notifier<List<EventSummary>> {
  static const _key = 'kurx_bookmarks';

  @override
  List<EventSummary> build() {
    // Reconcile without blocking first paint.
    Future.microtask(refresh);
    return _readCache();
  }

  /// Pulls the authoritative list. Silent on failure: a failed sync must not wipe a user's saved
  /// events or throw into a screen already showing the cached list.
  Future<void> refresh() async {
    try {
      final dtos = await ref.read(attendeeSourceProvider).savedEvents();
      state = dtos.map((d) => d.toEntity()).toList();
      _persist();
    } catch (_) {
      // Offline or signed out — keep the cache.
    }
  }

  bool isSaved(String slug) => state.any((e) => e.slug == slug);

  /// Optimistic. Reverts precisely on failure instead of refetching.
  Future<void> toggle(EventSummary event) async {
    final wasSaved = isSaved(event.slug);
    final previous = state;

    state = wasSaved ? state.where((e) => e.slug != event.slug).toList() : [event, ...state];
    _persist();

    try {
      final api = ref.read(attendeeSourceProvider);
      if (wasSaved) {
        await api.unsaveEvent(event.id);
      } else {
        await api.saveEvent(event.id);
      }
    } catch (_) {
      state = previous;
      _persist();
      rethrow;
    }
  }

  List<EventSummary> _readCache() {
    try {
      final raw = ref.read(cacheBoxProvider).get(_key) as String?;
      if (raw == null) return [];
      final list = jsonDecode(raw) as List<dynamic>;
      return list.map((e) => _decode(e as Map<String, dynamic>)).toList();
    } catch (_) {
      return [];
    }
  }

  void _persist() {
    try {
      ref.read(cacheBoxProvider).put(_key, jsonEncode(state.map(_encode).toList()));
    } catch (_) {
      // No persistent box (tests) — in-memory only.
    }
  }

  static Map<String, dynamic> _encode(EventSummary e) => {
        'id': e.id,
        'title': e.title,
        'slug': e.slug,
        'subtitle': e.subtitle,
        'venueName': e.venueName,
        'city': e.city,
        'startsAt': e.startsAt?.toIso8601String(),
        'status': e.status,
        'categoryName': e.categoryName,
        'priceFromPaise': e.priceFromPaise,
        'isFeatured': e.isFeatured,
      };

  static EventSummary _decode(Map<String, dynamic> m) => EventSummary(
        id: m['id'] as String,
        title: m['title'] as String,
        slug: m['slug'] as String,
        subtitle: m['subtitle'] as String?,
        venueName: m['venueName'] as String?,
        city: m['city'] as String?,
        startsAt: m['startsAt'] == null ? null : DateTime.parse(m['startsAt'] as String),
        status: m['status'] as String? ?? 'published',
        categoryName: m['categoryName'] as String?,
        priceFromPaise: m['priceFromPaise'] as int?,
        isFeatured: m['isFeatured'] as bool? ?? false,
      );
}

final bookmarksControllerProvider =
    NotifierProvider<BookmarksController, List<EventSummary>>(BookmarksController.new);

/// Whether a given slug is currently saved (for the bookmark toggle on cards).
final isBookmarkedProvider = Provider.family<bool, String>(
  (ref, slug) => ref.watch(bookmarksControllerProvider).any((e) => e.slug == slug),
);
