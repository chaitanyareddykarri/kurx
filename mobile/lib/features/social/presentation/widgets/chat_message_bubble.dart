import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../providers/chat_providers.dart';
import 'chat_presence.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/chat_message.dart';
import 'chat_attachment_view.dart';

/// The one genuinely chat-specific widget in this feature.
///
/// Everything else on these screens reuses the existing design system (KurxAvatar, EmptyState,
/// ErrorRetry, ShimmerBox/ListSkeleton, AsyncValueView, KurxBadge, KSpace/KRadius). A bubble has no
/// equivalent anywhere in the app, so it is built here rather than bent out of KurxCard.
class ChatMessageBubble extends ConsumerWidget {
  const ChatMessageBubble({
    super.key,
    required this.message,
    required this.isMine,
    required this.showSender,
    required this.roomId,
    this.myUserId,
    this.onRetry,
    this.onDiscard,
    this.onLongPress,
    this.onReact,
  });

  final ChatMessage message;
  final bool isMine;

  /// Room key for the presence selectors below. The bubble itself never watches presence — its two
  /// presence-aware children subscribe to one field each, so a dot or a tick changing repaints that
  /// child alone and leaves the list untouched.
  final String roomId;
  final String? myUserId;

  /// False when the previous message came from the same sender — avatar and name are drawn once
  /// per run rather than on every line.
  final bool showSender;

  final VoidCallback? onRetry;
  final VoidCallback? onDiscard;
  final VoidCallback? onLongPress;

  /// D-295 — toggles one emoji on this message. Null disables the affordance entirely, which is
  /// what a pending or failed row wants: there is no server message yet to react to.
  final void Function(String emoji)? onReact;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (message.isSystem) return _SystemNotice(text: message.body);
    if (message.isDeleted) return _DeletedNotice(isMine: isMine, showSender: showSender);

    final c = context.kurx;
    final failed = message.status == ChatSendStatus.failed;
    final pending = message.status == ChatSendStatus.pending;

    return Semantics(
      label: '${isMine ? 'You' : message.senderName ?? 'Unknown sender'} said ${message.body}',
      child: Padding(
        padding: const EdgeInsets.only(bottom: KSpace.sm),
        child: Row(
          mainAxisAlignment: isMine ? MainAxisAlignment.end : MainAxisAlignment.start,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            if (!isMine)
              SizedBox(
                width: 28 + KSpace.xs,
                child: showSender
                    ? _SenderAvatar(
                        roomId: roomId,
                        senderId: message.senderId,
                        name: message.senderName ?? '?',
                      )
                    : null,
              ),
            Flexible(
              // A long-press is the ONLY way to reach a message's actions — reply, copy, delete —
              // and it announced as nothing at all, so to a screen reader those actions did not
              // exist. `onLongPressHint` is what makes the gesture discoverable to TalkBack and
              // VoiceOver rather than something a sighted user has to already know.
              child: Semantics(
                onLongPressHint: onLongPress == null ? null : 'Show message actions',
                child: GestureDetector(
                onLongPress: onLongPress,
                child: Column(
                  crossAxisAlignment:
                      isMine ? CrossAxisAlignment.end : CrossAxisAlignment.start,
                  children: [
                    if (showSender && !isMine) _SenderLine(message: message),
                    Container(
                      constraints: const BoxConstraints(maxWidth: 520),
                      padding: const EdgeInsets.symmetric(
                          horizontal: KSpace.md, vertical: KSpace.sm),
                      decoration: BoxDecoration(
                        color: isMine ? c.accent : c.cardSurface,
                        // Tail on the outer edge, matching the app's existing bubble geometry.
                        borderRadius: BorderRadius.only(
                          topLeft: const Radius.circular(KRadius.lg),
                          topRight: const Radius.circular(KRadius.lg),
                          bottomLeft:
                              isMine ? const Radius.circular(KRadius.lg) : Radius.zero,
                          bottomRight:
                              isMine ? Radius.zero : const Radius.circular(KRadius.lg),
                        ),
                        border: failed ? Border.all(color: c.danger) : null,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          // Files first, then any caption — the attachment is usually the point of
                          // the message, and a caption reads as a caption underneath it.
                          for (final a in message.attachments)
                            ChatAttachmentView(attachment: a, onDismissFailed: () {}),
                          if (message.body.isNotEmpty)
                            Text(
                              message.body,
                              style: TextStyle(
                                color: isMine ? c.onAccent : c.text,
                                fontSize: 14,
                                height: 1.4,
                              ),
                            ),
                          // D-295 — the link card. `host` is derived server-side and is the one part
                          // a sender cannot fake, so it leads: the title may misdescribe the page,
                          // the host always tells the reader where the tap goes.
                          if (message.linkPreview != null)
                            _LinkCard(preview: message.linkPreview!, isMine: isMine),
                          // Reactions (D-295), aggregated per emoji by the server.
                          if (message.reactions.isNotEmpty)
                            _ReactionRow(
                              reactions: message.reactions,
                              isMine: isMine,
                              onToggle: onReact,
                            ),
                          const SizedBox(height: KSpace.xs),
                          _MetaLine(
                            message: message,
                            isMine: isMine,
                            pending: pending,
                            failed: failed,
                            roomId: roomId,
                            myUserId: myUserId,
                          ),
                        ],
                      ),
                    ),
                    if (failed) _FailedActions(onRetry: onRetry, onDiscard: onDiscard),
                  ],
                ),
              ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SenderLine extends StatelessWidget {
  const _SenderLine({required this.message});
  final ChatMessage message;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: 2, left: KSpace.xs),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            message.senderName ?? 'Unknown',
            style: TextStyle(color: c.muted, fontSize: 11.5, fontWeight: FontWeight.w700),
          ),
          if (message.isHostMessage) ...[
            const SizedBox(width: KSpace.xs),
            Text(
              'Host',
              style: TextStyle(color: c.accent, fontSize: 10.5, fontWeight: FontWeight.w800),
            ),
          ],
          // D-301 — the label IS the distinction. A colour-only difference between Host and Moderator
          // would say nothing to a screen reader and nothing to anyone who cannot separate the hues.
          if (message.isModeratorMessage) ...[
            const SizedBox(width: KSpace.xs),
            Text(
              'Mod',
              style: TextStyle(color: c.muted, fontSize: 10.5, fontWeight: FontWeight.w800),
            ),
          ],
        ],
      ),
    );
  }
}

class _MetaLine extends StatelessWidget {
  const _MetaLine({
    required this.message,
    required this.isMine,
    required this.pending,
    required this.failed,
    required this.roomId,
    this.myUserId,
  });

  final ChatMessage message;
  final bool isMine;
  final bool pending;
  final bool failed;
  final String roomId;
  final String? myUserId;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final onBubble = isMine ? c.onAccent : c.muted;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        // Sent time, never the edit time (D-293): the message did not move, so showing the edit time
        // here would misdate it in the conversation. The marker beside it says the body changed.
        Text(
          DateFormat('hh:mm a').format(message.createdAt.toLocal()),
          style: TextStyle(color: onBubble.withValues(alpha: 0.7), fontSize: 10.5),
        ),
        if (message.editedAt != null) ...[
          const SizedBox(width: KSpace.xs),
          Text(
            'edited',
            style: TextStyle(color: onBubble.withValues(alpha: 0.7), fontSize: 10.5),
          ),
        ],
        if (isMine) ...[
          const SizedBox(width: KSpace.xs),
          if (pending)
            Icon(Icons.schedule_rounded, size: 12, color: onBubble.withValues(alpha: 0.7))
          else if (failed)
            Icon(Icons.error_outline_rounded, size: 12, color: c.danger)
          else
            _ReadTick(
              roomId: roomId,
              messageId: message.id,
              myUserId: myUserId,
              color: onBubble,
            ),
        ],
      ],
    );
  }
}

/// Sender avatar plus the online dot (D-114).
///
/// Watches exactly one boolean. A member coming online repaints their own avatars and nothing else.
class _SenderAvatar extends ConsumerWidget {
  const _SenderAvatar({required this.roomId, required this.senderId, required this.name});

  final String roomId;
  final String? senderId;
  final String name;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(chatRoomControllerProvider(roomId).select(
      (s) => (known: s.presenceEnabled, online: s.isOnline(senderId)),
    ));
    return PresenceAvatar(
      name: name,
      size: 28,
      online: state.online,
      presenceKnown: state.known && senderId != null,
    );
  }
}

/// Single tick = delivered, double = read by someone else (D-114).
///
/// Compares this message against the room's furthest read pointer, so one receipt event settles
/// every bubble at once instead of storing per-message read state.
class _ReadTick extends ConsumerWidget {
  const _ReadTick({
    required this.roomId,
    required this.messageId,
    required this.myUserId,
    required this.color,
  });

  final String roomId;
  final String messageId;
  final String? myUserId;
  final Color color;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Both pointers in one watch, so a receipt of either kind repaints this tick once, not twice.
    final pointers = ref.watch(
      chatRoomControllerProvider(roomId).select(
        (s) => (read: s.highestReadByOthers(myUserId), delivered: s.highestDeliveredByOthers(myUserId)),
      ),
    );
    // UUIDv7 ids sort by time, which is the same order both pointers advance in.
    final read = pointers.read != null && messageId.compareTo(pointers.read!) <= 0;
    // D-295 — delivered is the middle state: it arrived on their device, nobody has looked at it yet.
    final delivered =
        read || (pointers.delivered != null && messageId.compareTo(pointers.delivered!) <= 0);

    return Semantics(
      label: read
          ? 'Read'
          : delivered
              ? 'Delivered'
              : 'Sent',
      child: Icon(
        // Shape carries sent vs delivered, opacity carries delivered vs read: each step differs from
        // the one before it by something other than colour, so the state survives greyscale.
        delivered ? Icons.done_all_rounded : Icons.check_rounded,
        size: 12,
        color: color.withValues(alpha: read ? 1.0 : 0.7),
      ),
    );
  }
}

class _FailedActions extends StatelessWidget {
  const _FailedActions({this.onRetry, this.onDiscard});
  final VoidCallback? onRetry;
  final VoidCallback? onDiscard;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(top: 2),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text('Not sent', style: TextStyle(color: c.danger, fontSize: 11)),
          TextButton(
            onPressed: onRetry,
            style: TextButton.styleFrom(
              minimumSize: const Size(0, 32),
              padding: const EdgeInsets.symmetric(horizontal: KSpace.sm),
            ),
            child: const Text('Retry', style: TextStyle(fontSize: 11.5)),
          ),
          TextButton(
            onPressed: onDiscard,
            style: TextButton.styleFrom(
              minimumSize: const Size(0, 32),
              padding: const EdgeInsets.symmetric(horizontal: KSpace.sm),
            ),
            child: Text('Discard',
                style: TextStyle(fontSize: 11.5, color: c.muted)),
          ),
        ],
      ),
    );
  }
}

class _SystemNotice extends StatelessWidget {
  const _SystemNotice({required this.text});
  final String text;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: KSpace.sm),
      child: Center(
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: KSpace.xs),
          decoration: BoxDecoration(
            color: c.elevated,
            borderRadius: BorderRadius.circular(KRadius.pill),
          ),
          child: Text(text, style: TextStyle(color: c.muted, fontSize: 11.5)),
        ),
      ),
    );
  }
}

class _DeletedNotice extends StatelessWidget {
  const _DeletedNotice({required this.isMine, required this.showSender});
  final bool isMine;
  final bool showSender;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.sm),
      child: Row(
        mainAxisAlignment: isMine ? MainAxisAlignment.end : MainAxisAlignment.start,
        children: [
          if (!isMine) const SizedBox(width: 28 + KSpace.xs),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: KSpace.sm),
            decoration: BoxDecoration(
              border: Border.all(color: c.border),
              borderRadius: BorderRadius.circular(KRadius.lg),
            ),
            child: Text(
              'Message deleted',
              style: TextStyle(color: c.muted, fontSize: 12.5, fontStyle: FontStyle.italic),
            ),
          ),
        ],
      ),
    );
  }
}

/// D-295 — the reaction row. `mine` drives a filled border rather than colour alone, so the toggled
/// state survives greyscale, and the semantic label carries the count for screen readers.
class _ReactionRow extends StatelessWidget {
  const _ReactionRow({required this.reactions, required this.isMine, this.onToggle});

  final List<ChatReaction> reactions;
  final bool isMine;
  final void Function(String emoji)? onToggle;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(top: KSpace.xs),
      child: Wrap(
        spacing: KSpace.xs,
        runSpacing: KSpace.xs,
        children: [
          for (final r in reactions)
            Semantics(
              button: onToggle != null,
              selected: r.mine,
              label: '${r.emoji} ${r.count}${r.mine ? ', you reacted' : ''}',
              child: InkWell(
                onTap: onToggle == null ? null : () => onToggle!(r.emoji),
                borderRadius: BorderRadius.circular(KRadius.pill),
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: isMine ? c.onAccent.withValues(alpha: 0.12) : c.elevated,
                    borderRadius: BorderRadius.circular(KRadius.pill),
                    border: Border.all(color: r.mine ? c.accent : c.border),
                  ),
                  child: ExcludeSemantics(
                    child: Text(
                      '${r.emoji} ${r.count}',
                      style: TextStyle(
                        color: isMine ? c.onAccent : c.text,
                        fontSize: 12,
                      ),
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// D-295 — a sender-supplied link card. Rendered without fetching anything: the server stores what
/// the sender's client sent and derives only the host, which is the part shown most prominently.
class _LinkCard extends StatelessWidget {
  const _LinkCard({required this.preview, required this.isMine});

  final ChatLinkPreview preview;
  final bool isMine;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final onBubble = isMine ? c.onAccent : c.text;
    return Padding(
      padding: const EdgeInsets.only(top: KSpace.xs),
      child: Container(
        decoration: BoxDecoration(
          color: isMine ? c.onAccent.withValues(alpha: 0.08) : c.elevated,
          borderRadius: BorderRadius.circular(KRadius.md),
          border: Border.all(color: c.border),
        ),
        padding: const EdgeInsets.all(KSpace.sm),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              preview.host,
              style: TextStyle(
                color: onBubble.withValues(alpha: 0.7),
                fontSize: 10.5,
                letterSpacing: 0.4,
              ),
            ),
            if (preview.title != null) ...[
              const SizedBox(height: 2),
              Text(
                preview.title!,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(color: onBubble, fontSize: 13, fontWeight: FontWeight.w600),
              ),
            ],
            if (preview.description != null) ...[
              const SizedBox(height: 2),
              Text(
                preview.description!,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(color: onBubble.withValues(alpha: 0.75), fontSize: 12),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
