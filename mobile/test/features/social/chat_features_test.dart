import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/social/data/models/chat_mappers.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_message.dart';
import 'package:kurx_mobile/features/social/presentation/providers/chat_providers.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_room.dart';

/// D-295 and D-296 on the client: the wire mapping, the delivery pointer, and the fields a `copyWith`
/// must carry. Every case here is one a happy-path check would pass while the rule was broken.
void main() {
  group('message mapping', () {
    test('reads reactions, forward provenance and a link card off the snake_case wire', () {
      final m = ChatMappers.message({
        'id': 'm1',
        'room_id': 'r1',
        'kind': 'Text',
        'body': 'have a look',
        'is_pinned': false,
        'is_deleted': false,
        'created_at': '2026-08-01T09:00:00Z',
        'reactions': [
          {'emoji': '👍', 'count': 2, 'mine': true, 'user_ids': ['u1', 'u2']},
        ],
        'forwarded_from': {'message_id': 'm0', 'sender_name': 'Alice'},
        'link_preview': {
          'url': 'https://example.org/x',
          'host': 'example.org',
          'title': 'X',
        },
      });

      expect(m.reactions.single.emoji, '👍');
      expect(m.reactions.single.count, 2);
      expect(m.reactions.single.mine, isTrue);
      expect(m.forwardedFrom!.messageId, 'm0');
      expect(m.linkPreview!.host, 'example.org');
    });

    test('reads the pin expiry (D-296)', () {
      final m = ChatMappers.message({
        'id': 'm1',
        'room_id': 'r1',
        'kind': 'Text',
        'body': 'read this',
        'is_pinned': true,
        'is_deleted': false,
        'created_at': '2026-08-01T09:00:00Z',
        'pinned_until': '2026-08-08T09:00:00Z',
      });

      expect(m.isPinned, isTrue);
      expect(m.pinnedUntil, DateTime.utc(2026, 8, 8, 9));
    });

    test('a server with no pin expiry still parses', () {
      // An older backend omits the field. Requiring it would fail the whole parse, which is the exact
      // failure D-292 already cost this surface once.
      final m = ChatMappers.message({
        'id': 'm1',
        'room_id': 'r1',
        'kind': 'Text',
        'body': 'read this',
        'is_pinned': true,
        'is_deleted': false,
        'created_at': '2026-08-01T09:00:00Z',
      });

      expect(m.pinnedUntil, isNull);
    });

    test('pinned messages on a room are typed, so a strip can render them', () {
      // They were `List<dynamic>` until D-296: parsed correctly, impossible to render, which is why
      // no pinned strip existed on this client at all.
      final room = ChatMappers.room({
        'room_id': 'r1',
        'kind': 'General',
        'status': 'Active',
        'post_policy': 'Everyone',
        'my_role': 'Member',
        'unread_count': 0,
        'capabilities': {'can_post': true},
        'pinned_messages': [
          {
            'id': 'm1',
            'room_id': 'r1',
            'kind': 'Text',
            'body': 'the venue moved',
            'is_pinned': true,
            'is_deleted': false,
            'created_at': '2026-08-01T09:00:00Z',
            'pinned_until': '2026-08-08T09:00:00Z',
          },
        ],
      });

      final ChatMessage pinned = room.pinnedMessages.single;
      expect(pinned.body, 'the venue moved');
      expect(pinned.pinnedUntil, isNotNull);
    });

    test('a chat row carries its own pin and mute filing', () {
      final chat = ChatMappers.myChat({
        'room_id': 'r1',
        'event_id': 'e1',
        'event_title': 'Design Summit',
        'unread_count': 0,
        'pinned': true,
        'notifications_muted': true,
      });

      expect(chat.pinned, isTrue);
      expect(chat.notificationsMuted, isTrue);
    });
  });

  group('copyWith carries what nothing copies a message to change', () {
    test('a status change does not strip the edit marker, reactions or a link card', () {
      final original = ChatMessage(
        id: 'm1',
        roomId: 'r1',
        body: 'hello',
        createdAt: DateTime.utc(2026, 8, 1),
        editedAt: DateTime.utc(2026, 8, 2),
        reactions: const [ChatReaction(emoji: '🎉', count: 1, mine: true, userIds: ['u1'])],
        linkPreview: const ChatLinkPreview(url: 'https://example.org', host: 'example.org'),
        pinnedUntil: DateTime.utc(2026, 8, 8),
        status: ChatSendStatus.pending,
      );

      // Omitting any of these from copyWith would silently un-mark an edited message, drop its
      // reactions or blank its card the moment a send confirmed.
      final confirmed = original.copyWith(status: ChatSendStatus.sent);

      expect(confirmed.editedAt, DateTime.utc(2026, 8, 2));
      expect(confirmed.reactions.single.emoji, '🎉');
      expect(confirmed.linkPreview!.host, 'example.org');
      expect(confirmed.pinnedUntil, DateTime.utc(2026, 8, 8));
    });
  });

  _d301();

  group('receipt pointers (D-295)', () {
    ChatRoomState stateWith({
      Map<String, String> read = const {},
      Map<String, String> delivered = const {},
    }) =>
        ChatRoomState(messages: const [], readPointers: read, deliveredPointers: delivered);

    test('delivered and read are separate, so arriving is not reading', () {
      final s = stateWith(delivered: {'u2': 'm5'});

      expect(s.highestDeliveredByOthers('u1'), 'm5');
      expect(s.highestReadByOthers('u1'), isNull);
    });

    test('my own pointer never drives my own ticks', () {
      // Otherwise every message would show as delivered to me the instant I sent it.
      final s = stateWith(read: {'u1': 'm9'}, delivered: {'u1': 'm9'});

      expect(s.highestReadByOthers('u1'), isNull);
      expect(s.highestDeliveredByOthers('u1'), isNull);
    });

    test('the highest pointer across members wins', () {
      final s = stateWith(delivered: {'u2': 'm3', 'u3': 'm7'});

      expect(s.highestDeliveredByOthers('u1'), 'm7');
    });
  });
}

/// D-301 — the chat role ladder on the client. The clients never re-derive authority from a role
/// string; they render from the server's capability flags. These pin the two flags that separate a
/// Moderator from a Host, because deriving either from `canModerate` would offer a Moderator a
/// control the server refuses.
void _d301() {
  group('moderator capabilities (D-301)', () {
    test('host-only flags default to false when an older server omits them', () {
      final caps = ChatMappers.capabilities({
        'can_post': true,
        'can_reply': true,
        'can_upload': true,
        'can_pin': true,
        'can_delete': true,
        'can_moderate': true,
        'can_mention_all': false,
      });

      // Absent reads as "no host powers" — the safe direction.
      expect(caps.canModerate, isTrue);
      expect(caps.canManageRoom, isFalse);
      expect(caps.canManageModerators, isFalse);
      expect(caps.myRole, 'Member');
    });

    test('a moderator moderates but does not manage the room', () {
      final caps = ChatMappers.capabilities({
        'can_post': true,
        'can_reply': true,
        'can_upload': true,
        'can_pin': true,
        'can_delete': true,
        'can_moderate': true,
        'can_mention_all': false,
        'can_manage_room': false,
        'can_manage_moderators': false,
        'my_role': 'Moderator',
      });

      expect(caps.canModerate, isTrue);
      expect(caps.canPin, isTrue);
      expect(caps.canManageRoom, isFalse);
      expect(caps.canManageModerators, isFalse);
      expect(caps.myRole, 'Moderator');
    });

    test('a host gets everything', () {
      final caps = ChatMappers.capabilities({
        'can_post': true,
        'can_reply': true,
        'can_upload': true,
        'can_pin': true,
        'can_delete': true,
        'can_moderate': true,
        'can_mention_all': true,
        'can_manage_room': true,
        'can_manage_moderators': true,
        'my_role': 'Host',
      });

      expect(caps.canManageRoom, isTrue);
      expect(caps.canManageModerators, isTrue);
      expect(caps.myRole, 'Host');
    });

    test('the denied default grants nothing', () {
      // What a room that failed to load resolves to. Every flag must be false, or a failed fetch
      // would briefly render moderation controls.
      const caps = ChatCapabilities.none();
      expect(caps.canModerate, isFalse);
      expect(caps.canManageRoom, isFalse);
      expect(caps.canManageModerators, isFalse);
      expect(caps.myRole, 'Member');
    });

    test('Host and Moderator are distinct badges on a message', () {
      ChatMessage withRole(String? role) => ChatMessage(
            id: 'm1', roomId: 'r1', body: 'x',
            createdAt: DateTime.utc(2026, 8, 1), senderRole: role,
          );

      expect(withRole('Host').isHostMessage, isTrue);
      expect(withRole('Host').isModeratorMessage, isFalse);
      expect(withRole('Moderator').isModeratorMessage, isTrue);
      expect(withRole('Moderator').isHostMessage, isFalse);
      expect(withRole('Member').isHostMessage, isFalse);
      expect(withRole('Member').isModeratorMessage, isFalse);
    });
  });
}
