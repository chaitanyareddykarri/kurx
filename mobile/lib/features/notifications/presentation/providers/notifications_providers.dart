import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../data/notifications_remote_data_source.dart';
import '../../domain/entities/app_notification.dart';

/// In-app notifications, backed by `/v1/me/notifications`.
///
/// This replaces a controller that returned a hardcoded `[]` behind a `TODO(D-019)` saying no
/// endpoint existed. It did — `MeNotificationEndpoints` shipped with D-064 over the previously-dead
/// `NotificationService` — so the feed and the unread badge had been permanently empty against a
/// working API.

final notificationsSourceProvider =
    Provider((ref) => NotificationsRemoteDataSource(ref.watch(dioProvider)));

/// The feed. `autoDispose` so opening the screen always shows current state.
final notificationsFeedProvider = FutureProvider.autoDispose<List<AppNotification>>((ref) async {
  final page = await ref.watch(notificationsSourceProvider).list();
  ref.watch(_unreadCacheProvider.notifier).state = page.unreadCount;
  return page.items.map(_toEntity).toList();
});

/// Last known unread count, kept outside `autoDispose` so the badge survives the feed screen
/// being closed. Zero until the first successful load — never a guess.
final _unreadCacheProvider = StateProvider<int>((ref) => 0);

/// Unread badge count for the app bar / nav. Same name and type as before, so the existing
/// consumers (home dashboard, profile) need no change.
final unreadNotificationsProvider = Provider<int>((ref) => ref.watch(_unreadCacheProvider));

/// Mark-read actions. Both re-read the feed rather than mutating locally: `read_at` is server
/// state, and the unread count has to come back from the same source that owns it.
class NotificationActions {
  const NotificationActions(this._ref);
  final Ref _ref;

  Future<void> markRead(String id) async {
    await _ref.read(notificationsSourceProvider).markRead(id);
    _ref.invalidate(notificationsFeedProvider);
  }

  Future<void> markAllRead() async {
    await _ref.read(notificationsSourceProvider).markAllRead();
    _ref.read(_unreadCacheProvider.notifier).state = 0;
    _ref.invalidate(notificationsFeedProvider);
  }
}

final notificationActionsProvider = Provider((ref) => NotificationActions(ref));

/// Maps the server's free-form `kind` onto the icon set the UI already has, and pulls a deep-link
/// route out of `data_json` when the payload carries one (the same shape push messages use).
AppNotification _toEntity(NotificationDto dto) => AppNotification(
      id: dto.id,
      kind: _kind(dto.kind),
      title: dto.title,
      body: dto.body,
      at: dto.createdAt ?? DateTime.now(),
      route: _route(dto.dataJson),
      connectionId: dto.kind == 'ally.requested' ? _connectionId(dto.dataJson) : null,
      read: dto.readAt != null,
    );

NotificationKind _kind(String kind) => switch (kind.toLowerCase()) {
      'reminder' || 'event_reminder' => NotificationKind.reminder,
      'price_drop' => NotificationKind.priceDrop,
      'new_event' || 'announcement' => NotificationKind.newEvent,
      'booking' || 'order' || 'ticket' => NotificationKind.booking,
      'ally.requested' || 'ally.accepted' || 'ally.declined' || 'ally.removed' => NotificationKind.ally,
      _ => NotificationKind.system,
    };

/// `data_json` is server-supplied and free-form, so a malformed payload must not take the whole
/// feed down — an unparseable blob simply yields no deep link.
String? _route(String? dataJson) {
  if (dataJson == null || dataJson.isEmpty) return null;
  try {
    final data = jsonDecode(dataJson);
    if (data is! Map) return null;
    // D-292 — the server-supplied route wins. It is already `/chats/{roomId}`, which is what the
    // route is keyed on. This used to be checked LAST, behind a chat branch testing `data['type']`
    // — a key the server never sends (it sends `notificationType`), so that branch was dead and the
    // fallback below carried an eventId into a room-keyed route.
    final route = data['route'];
    if (route is String && route.startsWith('/')) return route;

    final isChat = (data['notificationType'] ?? data['type']) == 'chat';
    if (!isChat) return null;

    // Older rows predate `route`. Prefer the room id; an eventId still resolves in the controller.
    final roomId = data['roomId'] ?? data['room_id'];
    if (roomId != null) return '/chats/$roomId';
    final eventId = data['eventId'] ?? data['event_id'];
    return eventId == null ? null : '/chats/$eventId';
  } catch (_) {
    return null;
  }
}

String? _connectionId(String? dataJson) {
  if (dataJson == null || dataJson.isEmpty) return null;
  try {
    final data = jsonDecode(dataJson);
    final id = data is Map ? data['connectionId'] : null;
    return id is String ? id : null;
  } catch (_) {
    return null;
  }
}
