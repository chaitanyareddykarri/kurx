import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/kurx_user_tile.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../public_profile/presentation/providers/public_profile_providers.dart';
import '../../../public_profile/presentation/widgets/ally_connect_button.dart';
import '../../data/models/order_dto.dart';
import '../providers/orders_providers.dart';

class GroupDetailPage extends ConsumerWidget {
  const GroupDetailPage({super.key, required this.groupId});
  final String groupId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Group Details'),
      ),
      body: AsyncValueView(
        value: ref.watch(groupDetailProvider(groupId)),
        onRetry: () => ref.invalidate(groupDetailProvider(groupId)),
        data: (group) => ListView(
          padding: const EdgeInsets.all(KSpace.lg),
          children: [
            // Group header
            DecoratedBox(
              decoration: BoxDecoration(
                color: c.cardSurface,
                borderRadius: BorderRadius.circular(KRadius.xl),
                boxShadow: kCardShadow(context),
              ),
              child: Padding(
                padding: const EdgeInsets.all(KSpace.lg),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      group.displayName ?? 'Group #${group.groupNumber}',
                      style: TextStyle(
                        color: c.text,
                        fontSize: 20,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: KSpace.sm),
                    Row(
                      children: [
                        Icon(Icons.people_outline_rounded,
                            color: c.muted, size: 16),
                        const SizedBox(width: KSpace.xs),
                        Text(
                          '${group.members.length} / ${group.capacity} members',
                          style: TextStyle(color: c.muted, fontSize: 13),
                        ),
                      ],
                    ),
                    const SizedBox(height: KSpace.md),
                    // Join code
                    Semantics(
                      button: true,
                      label: 'Copy the join code',
                      child: GestureDetector(
                      onTap: () {
                        Clipboard.setData(
                            ClipboardData(text: group.joinCode));
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(
                              content: Text('Join code copied')),
                        );
                      },
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: KSpace.md, vertical: KSpace.sm),
                        decoration: BoxDecoration(
                          color: c.elevated,
                          borderRadius: BorderRadius.circular(KRadius.md),
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(Icons.copy_rounded,
                                color: c.muted, size: 14),
                            const SizedBox(width: KSpace.xs),
                            Text(
                              group.joinCode,
                              style: TextStyle(
                                color: c.muted,
                                fontSize: 13,
                                fontFamily: 'monospace',
                                letterSpacing: 2,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: KSpace.lg),
            Text(
              'Members',
              style: TextStyle(
                color: c.text,
                fontWeight: FontWeight.w700,
                fontSize: 16,
              ),
            ),
            const SizedBox(height: KSpace.md),
            _GroupMemberList(members: group.members, leaderUserId: group.leaderUserId),
          ],
        ),
      ),
    );
  }
}

class _GroupMemberList extends ConsumerWidget {
  const _GroupMemberList({required this.members, required this.leaderUserId});
  final List<GroupMemberDto> members;
  final String leaderUserId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final myId = ref.watch(currentUserProvider)?.id;
    final otherIds = members
        .map((m) => m.userId)
        .whereType<String>()
        .where((id) => id != myId)
        .toSet()
        .toList();
    final status = otherIds.isEmpty
        ? const <String, String>{}
        : ref.watch(allyStatusBatchProvider(otherIds)).valueOrNull ?? const {};

    return Column(
      children: members
          .map((m) => Padding(
                padding: const EdgeInsets.only(bottom: KSpace.sm),
                child: _GroupMemberRow(
                  member: m,
                  isLeader: m.userId == leaderUserId,
                  myId: myId,
                  relation: _toRelation(status[m.userId]),
                ),
              ))
          .toList(),
    );
  }

  AllyRelation _toRelation(String? status) => switch (status) {
        'accepted' => AllyRelation.accepted,
        'pending_outgoing' => AllyRelation.outgoing,
        'pending_incoming' => AllyRelation.incoming,
        _ => AllyRelation.none,
      };
}

class _GroupMemberRow extends StatelessWidget {
  const _GroupMemberRow({required this.member, required this.isLeader, required this.myId, required this.relation});
  final GroupMemberDto member;
  final bool isLeader;
  final String? myId;
  final AllyRelation relation;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return KurxUserTile(
      name: member.name,
      username: member.username,
      avatarKey: member.avatarKey,
      subtitle: member.phone,
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (isLeader)
            Container(
              margin: const EdgeInsets.only(right: KSpace.sm),
              padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: 2),
              decoration: BoxDecoration(
                color: c.accent.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(KRadius.pill),
              ),
              child: Text('Leader', style: TextStyle(color: c.accent, fontSize: 11, fontWeight: FontWeight.w700)),
            ),
          if (member.userId != null && member.userId != myId)
            AllyConnectButton(targetUserId: member.userId!, initialRelation: relation),
          const SizedBox(width: KSpace.sm),
          Container(
            width: 8,
            height: 8,
            decoration: BoxDecoration(
              color: member.ticketId != null ? c.success : c.muted,
              shape: BoxShape.circle,
            ),
          ),
        ],
      ),
    );
  }
}
