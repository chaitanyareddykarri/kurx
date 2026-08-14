import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/push/push_service.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/social/data/datasources/chat_hub_client.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_message.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_room.dart';
import 'package:kurx_mobile/features/social/presentation/widgets/chat_message_bubble.dart';

ChatMessage _message({
  String id = 'm1',
  String body = 'hello',
  String? senderName = 'Alice',
  String kind = 'Text',
  bool isDeleted = false,
  ChatSendStatus status = ChatSendStatus.sent,
  String? clientMessageId,
}) =>
    ChatMessage(
      id: id,
      roomId: 'room-1',
      body: body,
      createdAt: DateTime.utc(2026, 7, 19, 10),
      senderId: 'user-2',
      senderName: senderName,
      senderRole: 'Member',
      kind: kind,
      isDeleted: isDeleted,
      status: status,
      clientMessageId: clientMessageId,
    );

Widget _host(Widget child, {Brightness brightness = Brightness.light}) => ProviderScope(
      child: MaterialApp(
        theme: brightness == Brightness.light ? AppTheme.light() : AppTheme.dark(),
        home: Scaffold(body: child),
      ),
    );

void main() {
  group('ChatMessageBubble', () {
    testWidgets('renders an incoming message with its sender', (tester) async {
      await tester.pumpWidget(_host(
        ChatMessageBubble(roomId: 'e1', message: _message(), isMine: false, showSender: true),
      ));

      expect(find.text('hello'), findsOneWidget);
      expect(find.text('Alice'), findsOneWidget);
    });

    testWidgets('hides the sender on a consecutive message from the same person', (tester) async {
      await tester.pumpWidget(_host(
        ChatMessageBubble(roomId: 'e1', message: _message(), isMine: false, showSender: false),
      ));

      expect(find.text('hello'), findsOneWidget);
      expect(find.text('Alice'), findsNothing);
    });

    testWidgets('a pending message shows a clock, a sent one a tick', (tester) async {
      await tester.pumpWidget(_host(Column(children: [
        ChatMessageBubble(
            roomId: 'e1',
            message: _message(status: ChatSendStatus.pending), isMine: true, showSender: false),
        ChatMessageBubble(roomId: 'e1', message: _message(id: 'm2'), isMine: true, showSender: false),
      ])));

      // Send state only — deliberately not a read receipt, which is a later phase.
      expect(find.byIcon(Icons.schedule_rounded), findsOneWidget);
      expect(find.byIcon(Icons.check_rounded), findsOneWidget);
    });

    testWidgets('a failed message offers retry and discard', (tester) async {
      var retried = false;
      var discarded = false;

      await tester.pumpWidget(_host(ChatMessageBubble(
            roomId: 'e1',
        message: _message(status: ChatSendStatus.failed, clientMessageId: 'c-1'),
        isMine: true,
        showSender: false,
        onRetry: () => retried = true,
        onDiscard: () => discarded = true,
      )));

      expect(find.text('Not sent'), findsOneWidget);
      await tester.tap(find.text('Retry'));
      await tester.tap(find.text('Discard'));
      expect(retried, isTrue);
      expect(discarded, isTrue);
    });

    testWidgets('a deleted message shows a tombstone, never the original body', (tester) async {
      await tester.pumpWidget(_host(ChatMessageBubble(
            roomId: 'e1',
        message: _message(body: 'secret', isDeleted: true),
        isMine: false,
        showSender: true,
      )));

      expect(find.text('Message deleted'), findsOneWidget);
      expect(find.text('secret'), findsNothing);
    });

    testWidgets('a system message renders as a centred notice with no avatar', (tester) async {
      await tester.pumpWidget(_host(ChatMessageBubble(
            roomId: 'e1',
        message: _message(body: 'Chat is now read-only.', kind: 'System', senderName: null),
        isMine: false,
        showSender: true,
      )));

      expect(find.text('Chat is now read-only.'), findsOneWidget);
      expect(find.byType(CircleAvatar), findsNothing);
    });

    testWidgets('renders in dark mode without overflow', (tester) async {
      await tester.pumpWidget(_host(
        ChatMessageBubble(
            roomId: 'e1',
          message: _message(body: 'a' * 400),
          isMine: false,
          showSender: true,
        ),
        brightness: Brightness.dark,
      ));

      expect(tester.takeException(), isNull);
      expect(find.byType(ChatMessageBubble), findsOneWidget);
    });

    testWidgets('exposes an accessible label for screen readers', (tester) async {
      await tester.pumpWidget(_host(
        ChatMessageBubble(roomId: 'e1', message: _message(), isMine: false, showSender: true),
      ));

      expect(
        find.bySemanticsLabel(RegExp('Alice said hello')),
        findsOneWidget,
      );
    });
  });

  group('capabilities drive the UI, never local role checks', () {
    test('a locked room reports the lock as the reason posting is blocked', () {
      const room = ChatRoom(
        roomId: 'room-1',
        kind: 'General',
        status: 'Locked',
        postPolicy: 'Everyone',
        myRole: 'Member',
        unreadCount: 0,
        capabilities: ChatCapabilities.none(),
      );

      expect(room.isLocked, isTrue);
      expect(room.cannotPostReason, 'This event has ended. Chat is read-only.');
    });

    test('a hosts-only room names that policy for a member', () {
      const room = ChatRoom(
        roomId: 'room-1',
        kind: 'General',
        status: 'Active',
        postPolicy: 'HostsOnly',
        myRole: 'Member',
        unreadCount: 0,
        capabilities: ChatCapabilities.none(),
      );

      expect(room.cannotPostReason, 'Only hosts can post right now.');
    });

    test('a member the server says can post has no blocking reason', () {
      const room = ChatRoom(
        roomId: 'room-1',
        kind: 'General',
        status: 'Active',
        postPolicy: 'HostsOnly',   // policy says hosts-only...
        myRole: 'Host',
        unreadCount: 0,
        // ...but the server already resolved it. The client trusts this, not the policy string.
        capabilities: ChatCapabilities(
          canPost: true,
          canReply: true,
          canUpload: false,
          canPin: true,
          canDelete: true,
          canModerate: true,
          canMentionAll: true,
        ),
      );

      expect(room.cannotPostReason, isNull);
    });

    test('an unknown room denies everything by default', () {
      const caps = ChatCapabilities.none();
      expect(caps.canPost, isFalse);
      expect(caps.canModerate, isFalse);
      expect(caps.canUpload, isFalse);
    });
  });

  group('realtime envelope', () {
    test('parses the versioned envelope', () {
      final event = ChatEvent.tryParse({
        'v': 1,
        'type': 'MessageReceived',
        'roomId': 'room-1',
        'id': 'msg-7',
        'payload': {'body': 'hi'},
      });

      expect(event, isNotNull);
      expect(event!.type, 'MessageReceived');
      expect(event.roomId, 'room-1');
      // The id is what lets a reconnecting client detect a gap and reconcile with ?after=.
      expect(event.id, 'msg-7');
    });

    test('an unknown event type still parses, so new server events cannot break an old client', () {
      final event = ChatEvent.tryParse({
        'v': 2,
        'type': 'SomethingInventedLater',
        'roomId': 'room-1',
        'unexpectedField': true,
      });

      expect(event, isNotNull);
      expect(event!.type, 'SomethingInventedLater');
    });

    test('a malformed envelope is dropped rather than thrown', () {
      expect(ChatEvent.tryParse(null), isNull);
      expect(ChatEvent.tryParse('not a map'), isNull);
      expect(ChatEvent.tryParse({'type': 'MessageReceived'}), isNull);  // no roomId
    });
  });

  group('push deep linking', () {
    test('the D-107 chat payload is recognised and carries the link identifiers', () {
      final message = PushMessage.fromData({
        'notificationType': 'chat',
        'eventId': 'ev-1',
        'roomId': 'room-1',
        'messageId': 'msg-1',
        'senderId': 'user-2',
      });

      expect(message.isChat, isTrue);
      expect(message.eventId, 'ev-1');
      expect(message.roomId, 'room-1');
      expect(message.messageId, 'msg-1');
      expect(message.senderId, 'user-2');
    });

    test('the existing auth payload still behaves exactly as before', () {
      final message = PushMessage.fromData({
        'type': 'login_approval',
        'challenge_id': 'ch-1',
        'match_number': '42',
      });

      expect(message.isLoginApproval, isTrue);
      expect(message.isChat, isFalse);
      expect(message.challengeId, 'ch-1');
      expect(message.matchNumber, 42);
    });

    test('type wins when a payload carries both keys', () {
      final message = PushMessage.fromData({
        'type': 'login_approval',
        'notificationType': 'chat',
      });

      expect(message.isLoginApproval, isTrue);
      expect(message.isChat, isFalse);
    });

    test('an unknown payload is inert rather than a crash', () {
      final message = PushMessage.fromData({'something': 'else'});

      expect(message.type, 'unknown');
      expect(message.isChat, isFalse);
      expect(message.eventId, isNull);
    });
  });
}
