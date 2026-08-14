import 'package:dio/dio.dart';

import '../../../core/network/api_guard.dart';

/// One notification category's delivery choice (D-263).
///
/// [locked] means the server refuses to switch it off — `security` today. The switch renders disabled
/// because of this flag; the server enforces it regardless of what the app sends.
class NotificationPreference {
  const NotificationPreference({
    required this.category,
    required this.inApp,
    required this.push,
    required this.email,
    required this.whatsApp,
    required this.locked,
  });

  final String category;
  final bool inApp;
  final bool push;
  final bool email;
  final bool whatsApp;
  final bool locked;

  NotificationPreference copyWith({bool? inApp, bool? push, bool? email, bool? whatsApp}) =>
      NotificationPreference(
        category: category,
        inApp: inApp ?? this.inApp,
        push: push ?? this.push,
        email: email ?? this.email,
        whatsApp: whatsApp ?? this.whatsApp,
        locked: locked,
      );

  /// snake-then-camel, the tolerant read D-259 established for every mobile mapper: the wire is
  /// snake_case, but a cached payload written by an older build may still be camelCase.
  static NotificationPreference fromJson(Map<String, dynamic> j) => NotificationPreference(
        category: '${j['category'] ?? ''}',
        inApp: (j['in_app'] ?? j['inApp'] ?? true) as bool,
        push: (j['push'] ?? true) as bool,
        email: (j['email'] ?? true) as bool,
        whatsApp: (j['whats_app'] ?? j['whatsApp'] ?? false) as bool,
        locked: (j['locked'] ?? false) as bool,
      );
}

class BlockedUser {
  const BlockedUser({required this.userId, required this.name, this.username, this.avatarKey});

  final String userId;
  final String name;
  final String? username;
  final String? avatarKey;

  static BlockedUser fromJson(Map<String, dynamic> j) => BlockedUser(
        userId: '${j['user_id'] ?? j['userId'] ?? ''}',
        name: '${j['name'] ?? ''}',
        username: (j['username'] as String?),
        avatarKey: (j['avatar_key'] ?? j['avatarKey']) as String?,
      );
}

/// A handle the user has released. Reserved for 30 days before anyone else can take it.
class UsernameHistoryEntry {
  const UsernameHistoryEntry({required this.username, required this.releasedAt});

  final String username;
  final DateTime? releasedAt;

  static UsernameHistoryEntry fromJson(Map<String, dynamic> j) => UsernameHistoryEntry(
        username: '${j['username'] ?? ''}',
        releasedAt: DateTime.tryParse('${j['released_at'] ?? j['releasedAt'] ?? ''}'),
      );
}

/// A direct-message conversation (D-264), shaped around the other person.
class DmRoom {
  const DmRoom({
    required this.roomId,
    required this.otherUserId,
    required this.otherName,
    this.otherUsername,
    required this.requestState,
    required this.isRequest,
    required this.archived,
    this.lastMessagePreview,
    required this.unreadCount,
    this.lastActivity,
    this.pinned = false,
    this.notificationsMuted = false,
  });

  final String roomId;
  final String otherUserId;
  final String otherName;
  final String? otherUsername;
  final String requestState;
  final bool isRequest;
  final bool archived;
  final String? lastMessagePreview;
  final int unreadCount;
  final DateTime? lastActivity;

  /// D-295 — this reader's own filing. Pinned rooms arrive first from the server, so the list order
  /// already matches; these two only decide what the row shows.
  final bool pinned;
  final bool notificationsMuted;

  static DmRoom fromJson(Map<String, dynamic> j) => DmRoom(
        roomId: '${j['room_id'] ?? j['roomId'] ?? ''}',
        otherUserId: '${j['other_user_id'] ?? j['otherUserId'] ?? ''}',
        otherName: '${j['other_name'] ?? j['otherName'] ?? ''}',
        otherUsername: (j['other_username'] ?? j['otherUsername']) as String?,
        requestState: '${j['request_state'] ?? j['requestState'] ?? 'accepted'}',
        isRequest: (j['is_request'] ?? j['isRequest'] ?? false) as bool,
        archived: (j['archived'] ?? false) as bool,
        lastMessagePreview: (j['last_message_preview'] ?? j['lastMessagePreview']) as String?,
        unreadCount: (j['unread_count'] ?? j['unreadCount'] ?? 0) as int,
        lastActivity: DateTime.tryParse('${j['last_activity'] ?? j['lastActivity'] ?? ''}'),
        pinned: (j['pinned'] ?? false) as bool,
        notificationsMuted: (j['notifications_muted'] ?? j['notificationsMuted'] ?? false) as bool,
      );
}

/// REST access to account settings (D-263) and direct messages (D-264). One method per endpoint;
/// no shaping beyond parsing, so the decisions stay server-side where they are enforced.
class AccountRemoteDataSource {
  AccountRemoteDataSource(this._dio);

  final Dio _dio;

  // ── Notification preferences ───────────────────────────────────────────────

  Future<List<NotificationPreference>> notificationPreferences() => guard(() async {
        final res = await _dio.get('/v1/me/notification-preferences');
        final map = Map<String, dynamic>.from(res.data as Map);
        return (map['categories'] as List? ?? const [])
            .whereType<Map>()
            .map((e) => NotificationPreference.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      });

  /// Only the named channels move; absent ones are left as they were, so one switch does not rewrite
  /// the whole row.
  Future<List<NotificationPreference>> updateNotificationPreference(
    String category, {
    bool? inApp,
    bool? push,
    bool? email,
    bool? whatsApp,
  }) =>
      guard(() async {
        final change = <String, dynamic>{'category': category};
        if (inApp != null) change['inApp'] = inApp;
        if (push != null) change['push'] = push;
        if (email != null) change['email'] = email;
        if (whatsApp != null) change['whatsApp'] = whatsApp;

        final res = await _dio.patch('/v1/me/notification-preferences', data: {
          'categories': [change],
        });
        final map = Map<String, dynamic>.from(res.data as Map);
        return (map['categories'] as List? ?? const [])
            .whereType<Map>()
            .map((e) => NotificationPreference.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      });

  // ── Blocks ─────────────────────────────────────────────────────────────────

  Future<List<BlockedUser>> blocks() => guard(() async {
        final res = await _dio.get('/v1/me/blocks');
        return (res.data as List? ?? const [])
            .whereType<Map>()
            .map((e) => BlockedUser.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      });

  Future<void> block(String userId) => guard(() async {
        await _dio.post('/v1/me/blocks/$userId');
      });

  Future<void> unblock(String userId) => guard(() async {
        await _dio.delete('/v1/me/blocks/$userId');
      });

  // ── Username history ───────────────────────────────────────────────────────

  Future<List<UsernameHistoryEntry>> usernameHistory() => guard(() async {
        final res = await _dio.get('/v1/me/username-history');
        return (res.data as List? ?? const [])
            .whereType<Map>()
            .map((e) => UsernameHistoryEntry.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      });

  // ── Email change ───────────────────────────────────────────────────────────
  // Two steps because the code goes to the NEW address — proving the person asking can actually
  // receive mail there. The address on file is warned at step one, which is the only signal a
  // compromised account ever gets.

  Future<void> startEmailChange(String newEmail) => guard(() async {
        await _dio.post('/v1/me/email/change/start', data: {'newEmail': newEmail});
      });

  Future<String?> completeEmailChange(String newEmail, String code) => guard(() async {
        final res = await _dio.post(
          '/v1/me/email/change/complete',
          data: {'newEmail': newEmail, 'code': code},
        );
        final map = Map<String, dynamic>.from(res.data as Map);
        return map['email'] as String?;
      });

  // ── Phone change ───────────────────────────────────────────────────────────
  // Deliberately the PRE-EXISTING route (D-263): `/v1/me/phone/verify` already owns this ceremony —
  // it dual-writes all four phone columns (D-089), revokes every session (D-038) and re-issues
  // tokens. The OTP comes from the ordinary issuance endpoint first.

  Future<void> requestPhoneOtp(String phone) => guard(() async {
        await _dio.post('/v1/auth/otp/request', data: {'phone': phone});
      });

  Future<void> changePhone(String phone, String code) => guard(() async {
        await _dio.post('/v1/me/phone/verify', data: {'phone': phone, 'code': code});
      });

  // ── Language ───────────────────────────────────────────────────────────────

  /// Persists the choice on the account rather than only on the device, so it follows the user to
  /// their next sign-in. `en` | `hi`; anything else is refused server-side as `invalid_language`.
  Future<void> setLanguage(String language) => guard(() async {
        await _dio.patch('/v1/me/profile', data: {'language': language});
      });

  // ── Deletion ───────────────────────────────────────────────────────────────

  /// Null when nothing is scheduled — the API answers 404 for that, deliberately, so a client cannot
  /// confuse "never asked" with "cancelled".
  Future<DateTime?> deletionScheduledFor() async {
    try {
      final res = await _dio.get('/v1/me/deletion');
      final map = Map<String, dynamic>.from(res.data as Map);
      return DateTime.tryParse('${map['scheduled_for'] ?? map['scheduledFor'] ?? ''}');
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) return null;
      rethrow;
    }
  }

  Future<DateTime?> requestDeletion({String? reason}) => guard(() async {
        final res = await _dio.post('/v1/me/deletion', data: {'reason': reason});
        final map = Map<String, dynamic>.from(res.data as Map);
        return DateTime.tryParse('${map['scheduled_for'] ?? map['scheduledFor'] ?? ''}');
      });

  Future<void> cancelDeletion() => guard(() async {
        await _dio.delete('/v1/me/deletion');
      });

  // ── Direct messages ────────────────────────────────────────────────────────

  /// Idempotent server-side, so the app never checks whether a conversation already exists.
  Future<String> openDm(String userId) => guard(() async {
        final res = await _dio.post('/v1/dm/$userId');
        final map = Map<String, dynamic>.from(res.data as Map);
        return '${map['room_id'] ?? map['roomId'] ?? ''}';
      });

  Future<List<DmRoom>> dms({bool archived = false}) => guard(() async {
        final res = await _dio.get('/v1/me/dm', queryParameters: {'archived': archived});
        return (res.data as List? ?? const [])
            .whereType<Map>()
            .map((e) => DmRoom.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      });

  Future<List<DmRoom>> dmRequests() => guard(() async {
        final res = await _dio.get('/v1/me/dm/requests');
        return (res.data as List? ?? const [])
            .whereType<Map>()
            .map((e) => DmRoom.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      });

  Future<void> respondToRequest(String roomId, {required bool accept}) => guard(() async {
        await _dio.post('/v1/dm/$roomId/requests/${accept ? 'accept' : 'decline'}');
      });

  Future<void> setArchived(String roomId, {required bool archived}) => guard(() async {
        if (archived) {
          await _dio.post('/v1/dm/$roomId/archive');
        } else {
          await _dio.delete('/v1/dm/$roomId/archive');
        }
      });
}
