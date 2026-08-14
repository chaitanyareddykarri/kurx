import 'dart:async';
import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/util/content_type.dart';

import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/error_retry.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../common/widgets/shimmer.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/theme/motion.dart';
import '../../domain/entities/chat_attachment.dart';
import '../../domain/entities/chat_message.dart';
import '../../domain/entities/chat_room.dart';
import '../providers/chat_providers.dart';
import '../widgets/chat_attachment_view.dart';
import '../widgets/chat_message_bubble.dart';
import '../widgets/chat_presence.dart';
import '../widgets/voice_recorder_button.dart';
import '../../../settings/presentation/providers/account_providers.dart';

/// One event's chat room.
///
/// Keyed by **roomId** (D-292). Every list, profile and notification pushes a room id; an older
/// deep link or the workspace tile may still carry an eventId, which the controller resolves.
/// A direct room has no event, so a room id is the only identifier both kinds share.
///
/// Every affordance is driven by the server-computed capabilities object. Nothing here decides
/// permission from role or policy, so a future chat mode changes the server alone.
/// The slice of room state this page paints. Deliberately excludes presence, typing and read
/// pointers: those change far more often than messages do, and are watched by the small widgets
/// that render them so the message list is never rebuilt for a dot or a tick.
typedef _RoomView = ({
  List<ChatMessage> messages,
  ChatRoom? room,
  bool loading,
  bool loadingOlder,
  Object? error,
  bool live,
  Map<String, ChatAttachment> uploads,
});

class ChatRoomPage extends ConsumerStatefulWidget {
  const ChatRoomPage({super.key, required this.roomId, this.title});

  final String roomId;
  final String? title;

  @override
  ConsumerState<ChatRoomPage> createState() => _ChatRoomPageState();
}

class _ChatRoomPageState extends ConsumerState<ChatRoomPage> {
  final _composer = TextEditingController();

  /// The message the composer is quoting, or null. Client-side only until send — `replyToMessageId`
  /// reaches the server through the ordinary send path (D-292).
  ChatMessage? _replyingTo;
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_onScroll);
  }

  @override
  void dispose() {
    _composer.dispose();
    _scroll.removeListener(_onScroll);
    _scroll.dispose();
    super.dispose();
  }

  /// The list is reversed, so "near the top of the history" is a large scroll offset.
  void _onScroll() {
    if (!_scroll.hasClients) return;
    final threshold = _scroll.position.maxScrollExtent - 400;
    if (_scroll.position.pixels >= threshold) {
      ref.read(chatRoomControllerProvider(widget.roomId).notifier).loadOlder();
    }
  }

  /// Picks a file and starts the upload.
  ///
  /// `withData: true` gives the bytes directly, which is what lets the upload go straight to the
  /// presigned URL without the app ever writing to device storage — and therefore without needing
  /// any storage permission.
  Future<void> _pickAndUpload({required bool imagesOnly}) async {
    final controller = ref.read(chatRoomControllerProvider(widget.roomId).notifier);

    final picked = await FilePicker.pickFiles(
      type: imagesOnly ? FileType.image : FileType.any,
      withData: true,
    );
    final file = picked?.files.singleOrNull;
    if (file == null) return;

    final bytes = file.bytes;
    if (bytes == null) return;

    // The content type is a hint only — the server re-derives it from the bytes on confirm and
    // refuses anything that does not match, so guessing here cannot widen what is accepted.
    await controller.sendAttachment(
      fileName: file.name,
      contentType: guessContentType(file.extension),
      sizeBytes: bytes.length,
      bytes: bytes,
      localPath: file.path,
    );
  }


  /// D-298 - video from the camera or the library. `image_picker` rather than `file_picker` because
  /// only the former can open the camera; the resulting file goes through the ordinary upload path,
  /// so the server's size, MIME, magic-byte and malware checks all apply unchanged.
  Future<void> _captureVideo({required bool fromCamera}) async {
    final controller = ref.read(chatRoomControllerProvider(widget.roomId).notifier);
    try {
      final picked = await ImagePicker().pickVideo(
        source: fromCamera ? ImageSource.camera : ImageSource.gallery,
        // A cap rather than an open-ended clip: the server enforces a size limit, and being told
        // after filming for ten minutes is worse than being stopped at two.
        maxDuration: const Duration(minutes: 2),
      );
      if (picked == null) return;

      final bytes = await picked.readAsBytes();
      await controller.sendAttachment(
        fileName: picked.name,
        contentType: guessContentType(picked.name.split('.').last),
        sizeBytes: bytes.length,
        bytes: bytes,
        localPath: picked.path,
      );
    } catch (e) {
      if (!mounted) return;
      // Most often a declined camera permission. Say so plainly rather than failing silently.
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  /// D-298 - a finished voice note, onto the same pipeline as every other attachment.
  void _sendVoiceNote({
    required String fileName,
    required String contentType,
    required List<int> bytes,
    required String localPath,
  }) {
    final controller = ref.read(chatRoomControllerProvider(widget.roomId).notifier);
    unawaited(controller.sendAttachment(
      fileName: fileName,
      contentType: contentType,
      sizeBytes: bytes.length,
      bytes: Uint8List.fromList(bytes),
      localPath: localPath,
    ));
  }

  void _showAttachSheet() {
    showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.image_outlined),
              title: const Text('Photo'),
              onTap: () {
                Navigator.pop(sheetContext);
                _pickAndUpload(imagesOnly: true);
              },
            ),
            // D-298 - the camera, which file_picker cannot open. Recording and choosing an existing
            // clip are separate entries because they are separate intents: one opens the camera, the
            // other opens the library, and a combined "Video" tile would guess wrong half the time.
            ListTile(
              leading: const Icon(Icons.videocam_outlined),
              title: const Text('Record video'),
              onTap: () {
                Navigator.pop(sheetContext);
                _captureVideo(fromCamera: true);
              },
            ),
            ListTile(
              leading: const Icon(Icons.video_library_outlined),
              title: const Text('Choose a video'),
              onTap: () {
                Navigator.pop(sheetContext);
                _captureVideo(fromCamera: false);
              },
            ),
            ListTile(
              leading: const Icon(Icons.attach_file_rounded),
              title: const Text('File'),
              subtitle: const Text('PDF, Office, text or ZIP'),
              onTap: () {
                Navigator.pop(sheetContext);
                _pickAndUpload(imagesOnly: false);
              },
            ),
            ListTile(
              leading: const Icon(Icons.close_rounded),
              title: const Text('Cancel'),
              onTap: () => Navigator.pop(sheetContext),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _send() async {
    final text = _composer.text.trim();
    if (text.isEmpty) return;
    final replyTo = _replyingTo?.id;
    _composer.clear();
    // Cleared with the draft, not after the await: leaving the banner up during the round trip invites
    // a second send that quotes the same message twice.
    if (mounted) setState(() => _replyingTo = null);
    await ref
        .read(chatRoomControllerProvider(widget.roomId).notifier)
        .send(text, replyToMessageId: replyTo);
    if (_scroll.hasClients) {
      if (!mounted || context.reduceMotion) {
        _scroll.jumpTo(0);
      } else {
        _scroll.animateTo(0,
            duration: const Duration(milliseconds: 200), curve: Curves.easeOut);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final state = ref.watch(chatRoomControllerProvider(widget.roomId).select(
      (s) => (
        messages: s.messages,
        room: s.room,
        loading: s.loading,
        loadingOlder: s.loadingOlder,
        error: s.error,
        live: s.live,
        uploads: s.uploads,
      ),
    ));
    final controller = ref.read(chatRoomControllerProvider(widget.roomId).notifier);
    final myUserId = ref.watch(currentUserProvider)?.id;
    final capabilities = state.room?.capabilities ?? const ChatCapabilities.none();

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: Row(
          children: [
            KurxAvatar(name: state.room != null ? (widget.title ?? 'Chat') : '?', size: 32),
            const SizedBox(width: KSpace.sm),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    widget.title ?? 'Chat',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                        color: c.text, fontWeight: FontWeight.w700, fontSize: 15),
                  ),
                  _HeaderSubtitle(
                    roomId: widget.roomId,
                    myUserId: myUserId,
                    live: state.live,
                  ),
                ],
              ),
            ),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'Shared media',
            icon: const Icon(Icons.perm_media_outlined),
            onPressed: _showMedia,
          ),
          // D-301 — room settings, HOST-only. Gated on `canManageRoom`, which neither client referenced
          // until now: the capability was computed server-side and exposed nowhere, so a Host could not
          // lock a room or restrict posting from either app. Deliberately NOT `canModerate` — a
          // Moderator moderates people, never the room itself.
          if (state.room?.capabilities.canManageRoom ?? false)
            IconButton(
              tooltip: 'Room settings',
              icon: const Icon(Icons.tune_rounded),
              onPressed: () => _showRoomSettings(state.room!),
            ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            if (state.room?.isLocked ?? false) const _LockedBanner(),
            // D-296 — the pinned strip. A pin whose only trace is an icon on the original bubble
            // surfaces nothing; the server already drops expired pins, so anything here still holds.
            if (state.room != null && state.room!.pinnedMessages.isNotEmpty)
              _PinnedStrip(messages: state.room!.pinnedMessages),
            Expanded(child: _body(state, controller, myUserId)),
            // The quoted message, above the composer and dismissible. Outside the text field so it
            // survives the draft being cleared and is unmistakable before sending.
            if (_replyingTo != null)
              Container(
                margin: const EdgeInsets.fromLTRB(KSpace.lg, 0, KSpace.lg, KSpace.sm),
                padding: const EdgeInsets.all(KSpace.sm),
                decoration: BoxDecoration(
                  color: c.elevated,
                  border: Border(left: BorderSide(color: c.accent, width: 3)),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text('Replying to ${_replyingTo!.senderName ?? 'a message'}',
                              style: TextStyle(
                                  fontSize: 12, fontWeight: FontWeight.w600, color: c.accent)),
                          Text(
                            _replyingTo!.body.isEmpty ? 'Attachment' : _replyingTo!.body,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(fontSize: 12, color: c.muted),
                          ),
                        ],
                      ),
                    ),
                    IconButton(
                      tooltip: 'Cancel reply',
                      icon: const Icon(Icons.close_rounded, size: 18),
                      onPressed: () => setState(() => _replyingTo = null),
                    ),
                  ],
                ),
              ),
            // Uploads in flight live above the composer, not in the message list: no permanent
            // message exists until the upload AND the send have both been confirmed.
            if (state.uploads.isNotEmpty)
              Padding(
                padding: const EdgeInsets.fromLTRB(KSpace.lg, 0, KSpace.lg, KSpace.sm),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    for (final entry in state.uploads.entries)
                      ChatAttachmentView(
                        attachment: entry.value,
                        onDismissFailed: () => controller.dismissUpload(entry.key),
                      ),
                  ],
                ),
              ),
            TypingBanner(roomId: widget.roomId, myUserId: myUserId),
            // D-292 — a message request, answered in the conversation rather than only from the
            // inbox. The composer stays below it deliberately: replying IS accepting (the rule the
            // server now enforces), so this exists to make Decline reachable — it is available
            // nowhere else once the room is open — and to say what state the conversation is in.
            if (state.room?.awaitingMyDecision(myUserId) ?? false)
              _RequestBanner(
                roomId: state.room!.roomId,
                onAnswered: controller.refresh,
              ),
            _Composer(
              controller: _composer,
              onSend: _send,
              onChanged: controller.onComposerChanged,
              onAttach: capabilities.canUpload ? _showAttachSheet : null,
              onVoiceNote: capabilities.canUpload ? _sendVoiceNote : null,
              enabled: capabilities.canPost,
              disabledReason: state.room?.cannotPostReason,
            ),
          ],
        ),
      ),
    );
  }

  Widget _body(_RoomView state, ChatRoomController controller, String? myUserId) {
    if (state.loading && state.messages.isEmpty) return const ListSkeleton(rows: 8);

    if (state.error != null && state.messages.isEmpty) {
      return ErrorRetry.fromError(error: state.error, onRetry: controller.refresh);
    }

    if (state.messages.isEmpty) {
      return const EmptyState(
        icon: Icons.forum_outlined,
        title: 'No messages yet',
        message: 'Say hello to everyone attending this event.',
      );
    }

    // Reversed so new messages appear at the bottom without measuring content height, and so
    // pagination loads as the user scrolls back through history.
    final ordered = state.messages.reversed.toList();

    return RefreshIndicator(
      onRefresh: controller.refresh,
      child: ListView.builder(
        controller: _scroll,
        reverse: true,
        padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.md),
        itemCount: ordered.length + (state.loadingOlder ? 1 : 0),
        itemBuilder: (context, i) {
          if (i >= ordered.length) {
            return const Padding(
              padding: EdgeInsets.symmetric(vertical: KSpace.lg),
              child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
            );
          }

          final message = ordered[i];
          final isMine = myUserId != null && message.senderId == myUserId ||
              message.status != ChatSendStatus.sent;
          // `ordered` is newest-first, so the *next* index is the previous message in time.
          final previous = i + 1 < ordered.length ? ordered[i + 1] : null;
          final showSender = previous == null ||
              previous.senderId != message.senderId ||
              previous.isSystem;

          return ChatMessageBubble(
            message: message,
            isMine: isMine,
            showSender: showSender,
            roomId: widget.roomId,
            myUserId: myUserId,
            onRetry: message.clientMessageId == null
                ? null
                : () => controller.retry(message.clientMessageId!),
            onDiscard: message.clientMessageId == null
                ? null
                : () => controller.discard(message.clientMessageId!),
            onLongPress: () => _showActions(message, controller, isMine),
            // D-295 - only a confirmed server message can be reacted to; a pending or failed row has
            // no server id for a reaction to attach to.
            onReact: message.status == ChatSendStatus.sent && !message.isSystem
                ? (emoji) => _react(controller, message.id, emoji)
                : null,
          );
        },
      ),
    );
  }

  void _showActions(
    ChatMessage message,
    ChatRoomController controller,
    bool isMine,
  ) {
    if (message.isSystem || message.status != ChatSendStatus.sent) return;

    // Capability-driven: a host sees moderation actions because the *server* said so.
    final capabilities =
        ref.read(chatRoomControllerProvider(widget.roomId)).capabilities;
    final canDelete = capabilities.canModerate || (isMine && capabilities.canDelete);
    // D-293. Only your own, and only while the room accepts posts — an edit is speech, so it is gated
    // on the same right as sending. No host path: a host removes a message, never rewrites one. The
    // server enforces all of this; this only decides what to offer.
    final canEdit = isMine && !message.isDeleted && capabilities.canPost;

    showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            // D-295 - the quick row. A short fixed set covers the overwhelming majority of real
            // reactions; a full picker is a different component and not what a long-press wants.
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.sm),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceAround,
                children: [
                  for (final emoji in const ['👍', '❤', '😂', '🎉', '👏', '🙏'])
                    IconButton(
                      tooltip: 'React $emoji',
                      onPressed: () {
                        Navigator.pop(sheetContext);
                        _react(controller, message.id, emoji);
                      },
                      icon: Text(emoji, style: const TextStyle(fontSize: 22)),
                    ),
                ],
              ),
            ),
            const Divider(height: 1),
            if (canEdit)
              ListTile(
                leading: const Icon(Icons.edit_outlined),
                title: const Text('Edit message'),
                onTap: () {
                  Navigator.pop(sheetContext);
                  _promptEdit(message, controller);
                },
              ),
            // D-295 — forwarding is available to anyone who can read the message; the server re-checks
            // that the reader is a member of the TARGET room before it writes anything there.
            ListTile(
              leading: const Icon(Icons.shortcut_rounded),
              title: const Text('Forward'),
              onTap: () {
                Navigator.pop(sheetContext);
                _promptForward(message, controller);
              },
            ),
            // D-296 — hosts only, and the server re-checks. `canPin` is the capability the server
            // computed for this room, never a role read on the client.
            if (capabilities.canPin)
              ListTile(
                leading: Icon(
                    message.isPinned ? Icons.push_pin_rounded : Icons.push_pin_outlined),
                title: Text(message.isPinned ? 'Unpin message' : 'Pin message'),
                onTap: () {
                  Navigator.pop(sheetContext);
                  _promptPin(message, controller);
                },
              ),
            // Distinct from 'Delete for everyone' below: available to anyone in the room, for anyone's
            // message, and it changes nobody else's view.
            ListTile(
              leading: const Icon(Icons.visibility_off_outlined),
              title: const Text('Delete for me'),
              onTap: () async {
                Navigator.pop(sheetContext);
                try {
                  await controller.hideMessage(message.id);
                } catch (e) {
                  if (!mounted) return;
                  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
                }
              },
            ),
            if (canDelete)
              ListTile(
                leading: const Icon(Icons.delete_outline_rounded),
                title: const Text('Delete for everyone'),
                onTap: () {
                  Navigator.pop(sheetContext);
                  controller.deleteMessage(message.id);
                },
              ),
            // Reply. Gated on CanPost rather than a permission of its own: a reply is an ordinary
            // message that happens to quote another, so anyone who may post may send one.
            if (capabilities.canPost && !message.isDeleted)
              ListTile(
                leading: const Icon(Icons.reply_rounded),
                title: const Text('Reply'),
                onTap: () {
                  Navigator.pop(sheetContext);
                  if (mounted) setState(() => _replyingTo = message);
                },
              ),
            // D-301 — the moderation actions themselves, for a Moderator OR a Host. Flutter shipped
            // promote/demote below without these, so a Host could appoint a moderator and neither of
            // them could mute or remove anyone from the app. `canModerate` is the right gate: the
            // server's ladder still refuses a Moderator aiming at a peer or a Host.
            if (capabilities.canModerate && !isMine && message.senderId != null) ...[
              ListTile(
                leading: const Icon(Icons.volume_off_outlined),
                title: const Text('Mute member for an hour'),
                onTap: () {
                  Navigator.pop(sheetContext);
                  _moderateMember(
                    controller,
                    () => controller.muteMember(message.senderId!, 60),
                    'Muted for an hour',
                  );
                },
              ),
              ListTile(
                leading: const Icon(Icons.person_remove_outlined),
                title: const Text('Remove from chat'),
                onTap: () {
                  Navigator.pop(sheetContext);
                  _moderateMember(
                    controller,
                    () => controller.setBanned(message.senderId!, banned: true),
                    'Removed from the chat',
                  );
                },
              ),
            ],
            // D-301 — HOST-only, gated on `canManageModerators` rather than `canModerate`. A Moderator
            // sees the moderation entries and not these two; the server refuses either direction from a
            // Moderator regardless, so this decides what to OFFER, never what is allowed.
            if (capabilities.canManageModerators && !isMine && message.senderId != null) ...[
              ListTile(
                leading: const Icon(Icons.shield_outlined),
                title: const Text('Make chat moderator'),
                onTap: () {
                  Navigator.pop(sheetContext);
                  _setModerator(controller, message.senderId!, moderator: true);
                },
              ),
              ListTile(
                leading: const Icon(Icons.shield_moon_outlined),
                title: const Text('Remove moderator role'),
                onTap: () {
                  Navigator.pop(sheetContext);
                  _setModerator(controller, message.senderId!, moderator: false);
                },
              ),
            ],
            if (!isMine)
              ListTile(
                leading: const Icon(Icons.flag_outlined),
                title: const Text('Report message'),
                onTap: () async {
                  Navigator.pop(sheetContext);
                  await controller.report(message.id, 'Inappropriate');
                  if (!mounted) return;
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Reported for review')),
                  );
                },
              ),
            ListTile(
              leading: const Icon(Icons.close_rounded),
              title: const Text('Cancel'),
              onTap: () => Navigator.pop(sheetContext),
            ),
          ],
        ),
      ),
    );
  }

  /// D-295 — shared media. Loaded on open rather than with the room: most readers never open it, and
  /// the query spans the room's whole history.
  Future<void> _showMedia() async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _MediaSheet(roomId: widget.roomId),
    );
  }

  /// D-301 — promote or demote a chat Moderator. The server's refusal codes are surfaced verbatim:
  /// `cannot_change_host` and `forbidden` say different things, and collapsing them to "failed" would
  /// hide which rule was hit.
  Future<void> _setModerator(ChatRoomController controller, String userId,
      {required bool moderator}) async {
    try {
      await controller.setModerator(userId, moderator: moderator);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text(moderator ? 'Now a chat moderator' : 'Moderator role removed'),
      ));
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  /// D-301 - room settings, Hosts only. Two switches, both server-authoritative: who may post, and
  /// whether the room is open at all. The sheet reads the room it was handed rather than local role
  /// state, so a Host demoted a moment ago sees the control vanish on the next `RoomUpdated`.
  Future<void> _showRoomSettings(ChatRoom room) async {
    final controller = ref.read(chatRoomControllerProvider(widget.roomId).notifier);
    final hostsOnly = room.postPolicy == 'HostsOnly';
    final locked = room.isLocked;

    await showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: Icon(hostsOnly ? Icons.lock_open_rounded : Icons.campaign_outlined),
              title: Text(hostsOnly ? 'Let everyone post' : 'Only hosts can post'),
              subtitle: const Text('Members can always read.'),
              onTap: () {
                Navigator.pop(sheetContext);
                _moderateMember(
                  controller,
                  () => controller.updateRoom(postPolicy: hostsOnly ? 'Everyone' : 'HostsOnly'),
                  hostsOnly ? 'Everyone can post' : 'Only hosts can post now',
                );
              },
            ),
            ListTile(
              leading: Icon(locked ? Icons.lock_open_outlined : Icons.lock_outline),
              title: Text(locked ? 'Reopen the room' : 'Lock the room'),
              subtitle: const Text('A locked room keeps its history and stops new messages.'),
              onTap: () {
                Navigator.pop(sheetContext);
                _moderateMember(
                  controller,
                  () => controller.updateRoom(status: locked ? 'Active' : 'Locked'),
                  locked ? 'Room reopened' : 'Room locked',
                );
              },
            ),
            ListTile(
              leading: const Icon(Icons.close_rounded),
              title: const Text('Cancel'),
              onTap: () => Navigator.pop(sheetContext),
            ),
          ],
        ),
      ),
    );
  }

  /// D-301 - one path for every member-moderation action, so mute and remove report identically and a
  /// server refusal (`cannot_moderate_peer` when a Moderator aims at a peer or a Host) surfaces as a
  /// message rather than silence.
  Future<void> _moderateMember(
      ChatRoomController controller, Future<void> Function() action, String done) async {
    try {
      await action();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(done)));
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  /// D-295 - one place both the bubble row and the quick-reaction sheet route through, so a failure
  /// is reported identically from either.
  Future<void> _react(ChatRoomController controller, String messageId, String emoji) async {
    try {
      await controller.toggleReaction(messageId, emoji);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  /// D-296 — pinning asks how long for; unpinning is one tap. A pin with no end is the one a host
  /// never returns to clear, so the window is always chosen rather than defaulted silently.
  Future<void> _promptPin(ChatMessage message, ChatRoomController controller) async {
    if (message.isPinned) {
      await _runPin(controller, message.id, pin: false);
      return;
    }
    final hours = await showModalBottomSheet<int>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const ListTile(title: Text('Pin message for'), dense: true),
            for (final option in const [('24 hours', 24), ('7 days', 168), ('30 days', 720)])
              ListTile(
                leading: const Icon(Icons.push_pin_outlined),
                title: Text(option.$1),
                onTap: () => Navigator.pop(sheetContext, option.$2),
              ),
            ListTile(
              leading: const Icon(Icons.close_rounded),
              title: const Text('Cancel'),
              onTap: () => Navigator.pop(sheetContext),
            ),
          ],
        ),
      ),
    );
    if (hours != null) await _runPin(controller, message.id, pin: true, durationHours: hours);
  }

  Future<void> _runPin(ChatRoomController controller, String messageId,
      {required bool pin, int? durationHours}) async {
    try {
      await controller.pinMessage(messageId, pin: pin, durationHours: durationHours);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  /// D-295 — forwards to another conversation. The picker lists the reader's own rooms, so a message
  /// can only ever be forwarded somewhere they are already a member — the server re-checks that.
  Future<void> _promptForward(ChatMessage message, ChatRoomController controller) async {
    final rooms = await ref.read(myChatsProvider.future).catchError((_) => <MyChat>[]);
    if (!mounted) return;
    final targets = rooms.where((r) => r.roomId != widget.roomId).toList();
    if (targets.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('No other conversation to forward to')),
      );
      return;
    }

    final target = await showModalBottomSheet<String>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: ListView(
          shrinkWrap: true,
          children: [
            const ListTile(title: Text('Forward to'), dense: true),
            for (final room in targets)
              ListTile(
                leading: const Icon(Icons.forum_outlined),
                title: Text(room.eventTitle, maxLines: 1, overflow: TextOverflow.ellipsis),
                onTap: () => Navigator.pop(sheetContext, room.roomId),
              ),
          ],
        ),
      ),
    );
    if (target == null) return;

    try {
      await controller.forward(message.id, target);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Forwarded')));
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  /// D-293 — the edit composer. Seeded with the current body; Save is disabled until it actually
  /// differs, which is the same refusal the server makes (`no_change`) rather than a second rule.
  Future<void> _promptEdit(ChatMessage message, ChatRoomController controller) async {
    final field = TextEditingController(text: message.body);
    final edited = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Edit message'),
        content: TextField(
          controller: field,
          autofocus: true,
          maxLines: 5,
          minLines: 1,
          maxLength: 1000,
          decoration: const InputDecoration(border: OutlineInputBorder()),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Cancel'),
          ),
          ValueListenableBuilder<TextEditingValue>(
            valueListenable: field,
            builder: (_, value, _) {
              final next = value.text.trim();
              final changed = next.isNotEmpty && next != message.body.trim();
              return FilledButton(
                onPressed: changed ? () => Navigator.pop(dialogContext, next) : null,
                child: const Text('Save'),
              );
            },
          ),
        ],
      ),
    );
    field.dispose();
    if (edited == null) return;

    try {
      await controller.editMessage(message.id, edited);
    } catch (e) {
      if (!mounted) return;
      // The server's reason, not a generic failure: "edit_window_expired" and "no_change" are both
      // things the person can understand and act on.
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }
}

/// "Event chat • live", or the number of other people present when presence is available.
///
/// Watches one derived integer, so a member joining repaints this line and nothing else.
class _HeaderSubtitle extends ConsumerWidget {
  const _HeaderSubtitle({
    required this.roomId,
    required this.myUserId,
    required this.live,
  });

  final String roomId;
  final String? myUserId;
  final bool live;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    // -1 means presence is unavailable on this deployment: say nothing rather than report an empty
    // roster, which would read as "nobody is here" when the truth is "we cannot know".
    final online = ref.watch(chatRoomControllerProvider(roomId).select(
      (s) => s.presenceEnabled ? s.onlineUserIds.where((id) => id != myUserId).length : -1,
    ));

    final label = switch (online) {
      < 0 => live ? 'Event chat • live' : 'Event chat',
      0 => live ? 'Event chat • live' : 'Event chat',
      1 => '1 other person online',
      _ => '$online others online',
    };

    return Text(label, style: TextStyle(color: c.muted, fontSize: 11.5));
  }
}

class _LockedBanner extends StatelessWidget {
  const _LockedBanner();

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      width: double.infinity,
      color: c.elevated,
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.sm),
      child: Row(
        children: [
          Icon(Icons.lock_outline_rounded, size: 14, color: c.muted),
          const SizedBox(width: KSpace.sm),
          Expanded(
            child: Text('This chat is read-only.',
                style: TextStyle(color: c.muted, fontSize: 12)),
          ),
        ],
      ),
    );
  }
}

class _Composer extends StatelessWidget {
  const _Composer({
    required this.controller,
    required this.onSend,
    required this.onChanged,
    required this.enabled,
    this.onAttach,
    this.onVoiceNote,
    this.disabledReason,
  });

  final TextEditingController controller;
  final VoidCallback onSend;

  /// Reports composer activity for the typing indicator. The controller debounces, so this fires on
  /// every keystroke without producing an event per keystroke.
  final ValueChanged<String> onChanged;

  /// Null when the server says this member may not upload — the button is not rendered at all
  /// rather than shown and then refused.
  final VoidCallback? onAttach;

  /// D-298 - a finished voice note. Null hides the mic entirely, which is what a read-only room or a
  /// room this member cannot upload to wants.
  final void Function({
    required String fileName,
    required String contentType,
    required List<int> bytes,
    required String localPath,
  })? onVoiceNote;

  final bool enabled;
  final String? disabledReason;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    if (!enabled) {
      return Container(
        width: double.infinity,
        padding: const EdgeInsets.fromLTRB(KSpace.lg, KSpace.md, KSpace.lg, KSpace.md),
        color: c.cardSurface,
        child: Text(
          disabledReason ?? 'You cannot post in this chat.',
          textAlign: TextAlign.center,
          style: TextStyle(color: c.muted, fontSize: 12.5),
        ),
      );
    }

    return Padding(
      padding: const EdgeInsets.fromLTRB(KSpace.lg, KSpace.sm, KSpace.lg, KSpace.md),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          if (onAttach != null)
            IconButton(
              onPressed: onAttach,
              icon: const Icon(Icons.attach_file_rounded),
              tooltip: 'Attach a file',
              // Comfortably above the 48dp minimum target.
              constraints: const BoxConstraints(minWidth: 48, minHeight: 48),
            ),
          if (onVoiceNote != null) VoiceRecorderButton(onRecorded: onVoiceNote!),
          Expanded(
            child: TextField(
              controller: controller,
              onChanged: onChanged,
              minLines: 1,
              maxLines: 5,
              maxLength: 1000, // mirrors the server's limit so the refusal is visible up front
              textInputAction: TextInputAction.send,
              onSubmitted: (_) => onSend(),
              decoration: const InputDecoration(
                hintText: 'Type a message…',
                counterText: '',
                contentPadding:
                    EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.md),
              ),
            ),
          ),
          const SizedBox(width: KSpace.sm),
          Semantics(
            button: true,
            label: 'Send message',
            child: GestureDetector(
              onTap: onSend,
              child: Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(color: c.accent, shape: BoxShape.circle),
                child: Icon(Icons.send_rounded, color: c.onAccent, size: 20),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// D-292 — Accept / Decline for a message request, inside the conversation.
///
/// Accepting is not strictly required to talk (a reply accepts on its own, server-side), but Decline
/// is reachable nowhere else once the room is open, and a request that cannot be refused is not a
/// request. On success the room is re-read so the banner and the capability flags move together, and
/// both DM lists are invalidated so the conversation lands in the right tab.
class _RequestBanner extends ConsumerStatefulWidget {
  const _RequestBanner({required this.roomId, required this.onAnswered});

  final String roomId;
  final Future<void> Function() onAnswered;

  @override
  ConsumerState<_RequestBanner> createState() => _RequestBannerState();
}

class _RequestBannerState extends ConsumerState<_RequestBanner> {
  bool _busy = false;

  Future<void> _respond(bool accept) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await ref
          .read(accountDataSourceProvider)
          .respondToRequest(widget.roomId, accept: accept);
      ref.invalidate(dmRequestsProvider);
      ref.invalidate(dmRoomsProvider(false));
      await widget.onAnswered();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.md),
      decoration: BoxDecoration(
        color: c.elevated,
        border: Border(top: BorderSide(color: c.border)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'This is a message request. Replying accepts it.',
            style: TextStyle(color: c.text, fontSize: 13),
          ),
          const SizedBox(height: KSpace.sm),
          Row(
            children: [
              FilledButton(
                onPressed: _busy ? null : () => _respond(true),
                child: const Text('Accept'),
              ),
              const SizedBox(width: KSpace.sm),
              TextButton(
                onPressed: _busy ? null : () => _respond(false),
                child: const Text('Decline'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// D-296 — the pinned strip. Compact by design: it is a pointer to a message, not the message. Each
/// row states who pinned what and, when the server supplied one, when the pin lapses.
class _PinnedStrip extends StatelessWidget {
  const _PinnedStrip({required this.messages});

  final List<ChatMessage> messages;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.sm),
      decoration: BoxDecoration(
        color: c.elevated,
        border: Border(bottom: BorderSide(color: c.border)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (final m in messages)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.push_pin_rounded, size: 12, color: c.muted),
                  const SizedBox(width: KSpace.xs),
                  Expanded(
                    child: Text(
                      '${m.senderName ?? 'System'}: ${m.body}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(color: c.text, fontSize: 12),
                    ),
                  ),
                  if (m.pinnedUntil != null) ...[
                    const SizedBox(width: KSpace.xs),
                    Text(
                      'until ${_pinExpiryLabel(m.pinnedUntil!)}',
                      style: TextStyle(color: c.muted, fontSize: 11),
                    ),
                  ],
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// Absolute rather than "in 3 days", so a host reading it twice in a day is not told two different
/// things about the same instant.
String _pinExpiryLabel(DateTime until) {
  final local = until.toLocal();
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  final hh = local.hour.toString().padLeft(2, '0');
  final mm = local.minute.toString().padLeft(2, '0');
  return '${local.day} ${months[local.month - 1]}, $hh:$mm';
}

/// D-295 — shared media for one room: every live attachment, newest first, so the file someone posted
/// last week is findable without scrolling the history for it.
class _MediaSheet extends ConsumerStatefulWidget {
  const _MediaSheet({required this.roomId});

  final String roomId;

  @override
  ConsumerState<_MediaSheet> createState() => _MediaSheetState();
}

class _MediaSheetState extends ConsumerState<_MediaSheet> {
  late final Future<List<ChatAttachment>> _future =
      ref.read(chatRepositoryProvider).roomMedia(widget.roomId);

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return SafeArea(
      child: SizedBox(
        height: MediaQuery.of(context).size.height * 0.6,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.all(KSpace.lg),
              child: Text('Shared media',
                  style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 16)),
            ),
            Expanded(
              child: FutureBuilder<List<ChatAttachment>>(
                future: _future,
                builder: (context, snapshot) {
                  if (snapshot.connectionState != ConnectionState.done) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  if (snapshot.hasError) {
                    return const Center(child: Text('Shared media could not be loaded.'));
                  }
                  final items = snapshot.data ?? const <ChatAttachment>[];
                  if (items.isEmpty) {
                    return const EmptyState(
                      icon: Icons.perm_media_outlined,
                      title: 'Nothing shared yet',
                      message: 'Photos and files sent in this conversation appear here.',
                    );
                  }
                  return GridView.builder(
                    padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
                    gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                      crossAxisCount: 3,
                      mainAxisSpacing: KSpace.sm,
                      crossAxisSpacing: KSpace.sm,
                    ),
                    itemCount: items.length,
                    itemBuilder: (context, i) => ChatAttachmentView(
                      attachment: items[i],
                      onDismissFailed: () {},
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}
