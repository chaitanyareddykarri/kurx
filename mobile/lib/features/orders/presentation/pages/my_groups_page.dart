import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/order_dto.dart';
import '../providers/orders_providers.dart';

class MyGroupsPage extends ConsumerWidget {
  const MyGroupsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('My Groups'),
        // A "Join a group" action with an empty `onPressed`. The backend has
        // `POST /v1/groups/join`, but **nothing in this app calls it** — there is no join screen, no
        // data source method, no route. Building one is a feature, not a redesign, so the control
        // goes rather than pretending; the endpoint is recorded in the Deferred Work Ledger.

      ),
      body: AsyncValueView(
        value: ref.watch(myGroupsProvider),
        onRetry: () => ref.invalidate(myGroupsProvider),
        isEmpty: (g) => g.isEmpty,
        empty: const EmptyState(
          icon: Icons.people_outline_rounded,
          title: 'No groups yet',
          message: 'Groups you create or join will appear here.',
        ),
        data: (groups) => ListView.separated(
          padding: const EdgeInsets.all(KSpace.lg),
          itemCount: groups.length,
          separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
          itemBuilder: (_, i) => _GroupCard(
            group: groups[i],
            onTap: () => context.push('/groups/${groups[i].id}'),
          ),
        ),
      ),
    );
  }
}

class _GroupCard extends StatelessWidget {
  const _GroupCard({required this.group, required this.onTap});
  final GroupDto group;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Semantics(
      button: true,
      child: GestureDetector(
      onTap: onTap,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: c.cardSurface,
          borderRadius: BorderRadius.circular(KRadius.xl),
          boxShadow: kCardShadow(context),
        ),
        child: Padding(
          padding: const EdgeInsets.all(KSpace.lg),
          child: Row(
            children: [
              // Overlapping avatar stack
              SizedBox(
                width: 52,
                height: 36,
                child: Stack(
                  children: [
                    for (var j = 0;
                        j < group.members.length.clamp(0, 3);
                        j++)
                      Positioned(
                        left: j * 16.0,
                        child: KurxAvatar(
                          name: group.members[j].name,
                          size: 32,
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(width: KSpace.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      group.displayName ?? 'Group #${group.groupNumber}',
                      style: TextStyle(
                        color: c.text,
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '${group.members.length} / ${group.capacity} members',
                      style: TextStyle(color: c.muted, fontSize: 13),
                    ),
                  ],
                ),
              ),
              Icon(Icons.chevron_right_rounded, color: c.muted),
            ],
          ),
        ),
      ),
      ),
    );
  }
}
