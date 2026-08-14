import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/chat_message.dart';
import '../../domain/entities/chat_room.dart';
import '../providers/chat_providers.dart';
import '../../../../common/widgets/kurx_shell_app_bar.dart';
import '../../../settings/data/account_remote_data_source.dart';
import '../../../settings/presentation/providers/account_providers.dart';

/// The Messages area: Direct · Events · Requests · Archived (D-264).
///
/// A DM room *is* a `ChatRoom`, so tapping any row opens the same `chat_room_page` — the tabs decide
/// what a row is about (a person or an event), not how the conversation works.
///
/// The header comment that used to sit here said there was deliberately no Direct tab because chat was
/// event-centric. That was true until D-264 made `ChatRoom.EventId` nullable.
class MyChatsPage extends ConsumerStatefulWidget {
  const MyChatsPage({super.key});

  @override
  ConsumerState<MyChatsPage> createState() => _MyChatsPageState();
}

class _MyChatsPageState extends ConsumerState<MyChatsPage> with SingleTickerProviderStateMixin {
  late final TabController _tabs = TabController(length: 4, vsync: this);

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: KurxShellAppBar(
        title: 'Messages',
        actions: [
          IconButton(
            tooltip: 'Search messages',
            icon: const Icon(Icons.search_rounded),
            onPressed: () => showSearch(context: context, delegate: ChatSearchDelegate(ref)),
          ),
        ],
        bottom: TabBar(
          controller: _tabs,
          isScrollable: true,
          tabs: const [
            Tab(text: 'Direct'),
            Tab(text: 'Events'),
            Tab(text: 'Requests'),
            Tab(text: 'Archived'),
          ],
        ),
      ),
      body: TabBarView(
        controller: _tabs,
        children: const [
          _DmList(archived: false),
          _EventChatList(),
          _RequestList(),
          // D-306 — archived DMs AND archived event chats. Before this the tab read `_DmList` alone, so
          // an archived event room was retrievable by the API and still invisible in the one place a
          // reader would look for it.
          _ArchivedList(),
        ],
      ),
    );
  }
}

/// Event chat, unchanged — the same list that was here before DMs existed.
/// D-306 — the Archived tab: both kinds of filed-away conversation, each under its own heading.
///
/// Two lists rather than one merged feed, because a DM row and an event row carry different identities
/// (a person versus an event) and interleaving them by timestamp would make the tab harder to scan than
/// the inbox it exists to keep tidy.
class _ArchivedList extends StatelessWidget {
  const _ArchivedList();

  /// Two independently-scrolling halves in a Column, NOT two lists inside a third. Both children own a
  /// `ListView` with `AlwaysScrollableScrollPhysics` and a `RefreshIndicator`; nesting those inside an
  /// outer scrollable gives them an unbounded height and throws at layout. `Expanded` gives each a real
  /// viewport, so each keeps its own pull-to-refresh and its own empty state.
  @override
  Widget build(BuildContext context) => const Column(
        children: [
          _ArchivedSection(label: 'Direct messages', child: _DmList(archived: true)),
          _ArchivedSection(label: 'Event chats', child: _EventChatList(archived: true)),
        ],
      );
}

class _ArchivedSection extends StatelessWidget {
  const _ArchivedSection({required this.label, required this.child});

  final String label;
  final Widget child;

  @override
  Widget build(BuildContext context) => Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(KSpace.lg, KSpace.lg, KSpace.lg, KSpace.sm),
              child: Text(label, style: Theme.of(context).textTheme.labelLarge),
            ),
            Expanded(child: child),
          ],
        ),
      );
}

class _EventChatList extends ConsumerWidget {
  /// D-306 — [archived] picks which side of the reader's filing this list shows. The Archived tab was
  /// DMs only, so an archived event room had nowhere to appear even once the server could return it.
  const _EventChatList({this.archived = false});

  final bool archived;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final provider = archived ? archivedChatsProvider : myChatsProvider;
    final chats = ref.watch(provider);

    return RefreshIndicator(
      onRefresh: () async => ref.refresh(provider.future),
      child: AsyncValueView<List<MyChat>>(
        value: chats,
        onRetry: () => ref.invalidate(provider),
        isEmpty: (rooms) => rooms.isEmpty,
        empty: EmptyState(
          icon: archived ? Icons.inventory_2_outlined : Icons.forum_outlined,
          title: archived ? 'No archived event chats' : 'No event chats',
          message: archived
              ? 'Event chats you archive are kept here.'
              : 'Chat rooms for events you attend appear here.',
        ),
        data: (rooms) => ListView.separated(
          padding: EdgeInsets.zero,
          physics: const AlwaysScrollableScrollPhysics(),
          itemCount: rooms.length,
          separatorBuilder: (_, _) =>
              Divider(height: 1, indent: 72, endIndent: KSpace.lg, color: c.border),
          itemBuilder: (context, i) => _ChatRow(chat: rooms[i]),
        ),
      ),
    );
  }
}

class _DmList extends ConsumerWidget {
  const _DmList({required this.archived});

  final bool archived;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final rooms = ref.watch(dmRoomsProvider(archived));

    return RefreshIndicator(
      onRefresh: () async => ref.refresh(dmRoomsProvider(archived).future),
      child: AsyncValueView<List<DmRoom>>(
        value: rooms,
        onRetry: () => ref.invalidate(dmRoomsProvider(archived)),
        isEmpty: (r) => r.isEmpty,
        empty: EmptyState(
          icon: archived ? Icons.inventory_2_outlined : Icons.chat_bubble_outline,
          title: archived ? 'Nothing archived' : 'No conversations',
          message: archived
              ? 'Archived conversations appear here.'
              : "Open someone's profile and tap Message to start one.",
        ),
        data: (list) => ListView.separated(
          padding: EdgeInsets.zero,
          physics: const AlwaysScrollableScrollPhysics(),
          itemCount: list.length,
          separatorBuilder: (_, _) =>
              Divider(height: 1, indent: 72, endIndent: KSpace.lg, color: c.border),
          itemBuilder: (context, i) => _DmRow(room: list[i], archived: archived),
        ),
      ),
    );
  }
}

class _RequestList extends ConsumerWidget {
  const _RequestList();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final rooms = ref.watch(dmRequestsProvider);

    return RefreshIndicator(
      onRefresh: () async => ref.refresh(dmRequestsProvider.future),
      child: AsyncValueView<List<DmRoom>>(
        value: rooms,
        onRetry: () => ref.invalidate(dmRequestsProvider),
        isEmpty: (r) => r.isEmpty,
        empty: const EmptyState(
          icon: Icons.drafts_outlined,
          title: 'No message requests',
          message: "Messages from people you're not connected to wait here first.",
        ),
        data: (list) => ListView.separated(
          padding: EdgeInsets.zero,
          physics: const AlwaysScrollableScrollPhysics(),
          itemCount: list.length,
          separatorBuilder: (_, _) => Divider(height: 1, color: c.border),
          itemBuilder: (context, i) => _RequestRow(room: list[i]),
        ),
      ),
    );
  }
}

class _DmRow extends ConsumerWidget {
  const _DmRow({required this.room, required this.archived});

  final DmRoom room;
  final bool archived;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ListTile(
      onLongPress: () => showRoomFilingSheet(
        context,
        ref,
        roomId: room.roomId,
        pinned: room.pinned,
        muted: room.notificationsMuted,
      ),
      leading: KurxAvatar(name: room.otherName, size: 44),
      title: Row(
        children: [
          Expanded(child: Text(room.otherName, maxLines: 1, overflow: TextOverflow.ellipsis)),
          if (room.pinned)
            const Icon(Icons.push_pin_rounded, size: 13, semanticLabel: 'Pinned'),
          if (room.notificationsMuted)
            const Icon(Icons.notifications_off_rounded, size: 13, semanticLabel: 'Muted'),
        ],
      ),
      subtitle: Text(
        room.lastMessagePreview ?? 'No messages yet',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (room.unreadCount > 0)
            Badge(label: Text('${room.unreadCount}'))
          else
            const SizedBox.shrink(),
          IconButton(
            tooltip: archived ? 'Unarchive' : 'Archive',
            icon: Icon(archived ? Icons.unarchive_outlined : Icons.archive_outlined),
            onPressed: () async {
              try {
                await ref
                    .read(accountDataSourceProvider)
                    .setArchived(room.roomId, archived: !archived);
                ref.invalidate(dmRoomsProvider(true));
                ref.invalidate(dmRoomsProvider(false));
              } catch (e) {
                if (!context.mounted) return;
                ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
              }
            },
          ),
        ],
      ),
      onTap: () => context.push('/chats/${room.roomId}'),
    );
  }
}

class _RequestRow extends ConsumerWidget {
  const _RequestRow({required this.room});

  final DmRoom room;

  Future<void> _respond(BuildContext context, WidgetRef ref, bool accept) async {
    try {
      await ref.read(accountDataSourceProvider).respondToRequest(room.roomId, accept: accept);
      ref.invalidate(dmRequestsProvider);
      ref.invalidate(dmRoomsProvider(false));
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ListTile(
      leading: KurxAvatar(name: room.otherName, size: 44),
      title: Text(room.otherName, maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text(
        room.lastMessagePreview ?? 'Wants to send you a message',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextButton(onPressed: () => _respond(context, ref, false), child: const Text('Decline')),
          FilledButton(onPressed: () => _respond(context, ref, true), child: const Text('Accept')),
        ],
      ),
      onTap: () => context.push('/chats/${room.roomId}'),
    );
  }
}

class _ChatRow extends ConsumerWidget {
  const _ChatRow({required this.chat});

  final MyChat chat;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final unread = chat.unreadCount;

    return ListTile(
      // D-295 - long-press for filing, the gesture every messenger uses for it. Pin and mute are not
      // controls on the row itself: the row's job is to open the conversation.
      onLongPress: () => showRoomFilingSheet(
        context,
        ref,
        roomId: chat.roomId,
        pinned: chat.pinned,
        muted: chat.notificationsMuted,
        archived: chat.archived,   // D-306 — so an archived row offers "Move to inbox", not "Archive"
      ),
      leading: KurxAvatar(name: chat.eventTitle, imageUrl: chat.bannerUrl, size: 44),
      title: Row(
        children: [
          Expanded(
            child: Text(
              chat.eventTitle,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                color: c.text,
                fontSize: 14,
                fontWeight: unread > 0 ? FontWeight.w800 : FontWeight.w700,
              ),
            ),
          ),
          // Semantic labels, so the state is not carried by an icon shape alone.
          if (chat.pinned) ...[
            const SizedBox(width: KSpace.xs),
            Icon(Icons.push_pin_rounded, size: 13, color: c.muted, semanticLabel: 'Pinned'),
          ],
          if (chat.notificationsMuted) ...[
            const SizedBox(width: KSpace.xs),
            Icon(Icons.notifications_off_rounded, size: 13, color: c.muted, semanticLabel: 'Muted'),
          ],
        ],
      ),
      subtitle: Text(
        chat.lastMessagePreview?.isNotEmpty == true
            ? chat.lastMessagePreview!
            : 'No messages yet',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: TextStyle(color: c.muted, fontSize: 13),
      ),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          if (chat.lastActivity != null)
            Text(
              _stamp(chat.lastActivity!),
              style: TextStyle(color: c.muted, fontSize: 11.5),
            ),
          if (unread > 0)
            Container(
              margin: const EdgeInsets.only(top: 4),
              constraints: const BoxConstraints(minWidth: 20),
              height: 20,
              padding: const EdgeInsets.symmetric(horizontal: 5),
              decoration: BoxDecoration(
                color: c.accent,
                borderRadius: BorderRadius.circular(KRadius.pill),
              ),
              child: Center(
                child: Text(
                  unread > 99 ? '99+' : '$unread',
                  style: TextStyle(
                      color: c.onAccent, fontSize: 11, fontWeight: FontWeight.w800),
                ),
              ),
            ),
        ],
      ),
      // Routed by roomId (D-292), like every other row in this screen — one navigation model for
      // event rooms and direct conversations alike.
      onTap: () => context.push(
        '/chats/${chat.roomId}?title=${Uri.encodeComponent(chat.eventTitle)}',
      ),
    );
  }

  /// Time for today, weekday inside the last week, date beyond that — the convention the rest of
  /// the app's lists already use.
  static String _stamp(DateTime utc) {
    final local = utc.toLocal();
    final now = DateTime.now();
    final sameDay =
        local.year == now.year && local.month == now.month && local.day == now.day;
    if (sameDay) return DateFormat('hh:mm a').format(local);
    if (now.difference(local).inDays < 7) return DateFormat('EEE').format(local);
    return DateFormat('dd MMM').format(local);
  }
}

/// D-295 - pin and mute for one conversation, from a long-press.
///
/// Both are PERSONAL: they change this reader's own list and their own notifications, and say nothing
/// to the other party. Mute here is not the host's `MutedUntil`, which stops someone POSTING - the two
/// share a word and nothing else.
Future<void> showRoomFilingSheet(
  BuildContext context,
  WidgetRef ref, {
  required String roomId,
  required bool pinned,
  required bool muted,
  bool archived = false,
}) async {
  final repo = ref.read(chatRepositoryProvider);

  Future<void> run(Future<void> Function() action) async {
    try {
      await action();
      ref.invalidate(myChatsProvider);
      // D-306 — archiving moves a room BETWEEN these two, so refreshing only the active list would
      // leave the Archived tab showing the state before the change.
      ref.invalidate(archivedChatsProvider);
      ref.invalidate(dmRoomsProvider(false));
      ref.invalidate(dmRoomsProvider(true));
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  await showModalBottomSheet<void>(
    context: context,
    builder: (sheetContext) => SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          ListTile(
            leading: Icon(pinned ? Icons.push_pin_rounded : Icons.push_pin_outlined),
            title: Text(pinned ? 'Unpin conversation' : 'Pin conversation'),
            onTap: () {
              Navigator.pop(sheetContext);
              run(() => repo.setPinned(roomId, pinned: !pinned));
            },
          ),
          ListTile(
            leading: Icon(
                muted ? Icons.notifications_active_outlined : Icons.notifications_off_outlined),
            title: Text(muted ? 'Unmute notifications' : 'Mute for 8 hours'),
            onTap: () {
              Navigator.pop(sheetContext);
              // An absolute instant, not a duration: the server stores WHEN silence ends, so a device
              // that was asleep for the whole window comes back correctly un-muted.
              run(() => repo.setMuted(
                  roomId, muted ? null : DateTime.now().toUtc().add(const Duration(hours: 8))));
            },
          ),
          if (!muted)
            ListTile(
              leading: const Icon(Icons.notifications_off_rounded),
              title: const Text('Mute for a week'),
              onTap: () {
                Navigator.pop(sheetContext);
                run(() =>
                    repo.setMuted(roomId, DateTime.now().toUtc().add(const Duration(days: 7))));
              },
            ),
          // D-295 said "archive any room" and only DMs could. Safe to offer for an EVENT room only
          // since D-306 gave `/v1/me/chats` an `archived` parameter: before that the room left the one
          // list that could return it, so this tile would have archived a conversation into a hole.
          ListTile(
            leading: Icon(archived ? Icons.unarchive_outlined : Icons.archive_outlined),
            title: Text(archived ? 'Move to inbox' : 'Archive conversation'),
            onTap: () {
              Navigator.pop(sheetContext);
              run(() => repo.setArchived(roomId, archived: !archived));
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

/// D-295 - message search across every room this reader is in.
///
/// The minimum length is the server's own, which rejects anything shorter with `query_too_short`:
/// checking it here too turns an error into a hint. Keep the two equal - a stricter client silently
/// refuses a search the server would have answered. Results are fetched on submit
/// rather than per keystroke - each call is a full-text query, and a phone keyboard would otherwise
/// fire one per letter.
const _minQuery = 2;

class ChatSearchDelegate extends SearchDelegate<void> {
  ChatSearchDelegate(this._ref);

  final WidgetRef _ref;

  @override
  String get searchFieldLabel => 'Search messages';

  @override
  List<Widget> buildActions(BuildContext context) => [
        if (query.isNotEmpty)
          IconButton(
            tooltip: 'Clear',
            icon: const Icon(Icons.clear_rounded),
            onPressed: () => query = '',
          ),
      ];

  @override
  Widget buildLeading(BuildContext context) => IconButton(
        tooltip: 'Back',
        icon: const Icon(Icons.arrow_back_rounded),
        onPressed: () => close(context, null),
      );

  @override
  Widget buildSuggestions(BuildContext context) => query.trim().length < _minQuery
      ? const Center(child: Text('Type at least $_minQuery characters'))
      : buildResults(context);

  @override
  Widget buildResults(BuildContext context) {
    final term = query.trim();
    if (term.length < _minQuery) return const Center(child: Text('Type at least $_minQuery characters'));

    return FutureBuilder<List<ChatSearchHit>>(
      future: _ref.read(chatRepositoryProvider).search(term),
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return const Center(child: Text('Search is unavailable right now.'));
        }
        final hits = snapshot.data ?? const <ChatSearchHit>[];
        if (hits.isEmpty) return Center(child: Text('No messages match "$term".'));

        return ListView.separated(
          itemCount: hits.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, i) {
            final hit = hits[i];
            return ListTile(
              title: Text(hit.roomLabel, maxLines: 1, overflow: TextOverflow.ellipsis),
              subtitle: Text(
                '${hit.senderName ?? 'System'}: ${hit.body}',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              onTap: () {
                close(context, null);
                context.push('/chats/${hit.roomId}');
              },
            );
          },
        );
      },
    );
  }
}
