import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/gamification_dto.dart';
import '../providers/gamification_providers.dart';

/// The backend has exactly one real leaderboard view — `Leaderboard` rows are scoped
/// `global`/`organization` only, with no time-period dimension to filter by (D-212). A previous
/// version of this screen offered This Week/This Month/All Time chips that didn't correspond to
/// anything server-side and also called a route (`/v1/leaderboard`) that doesn't exist.
class LeaderboardPage extends ConsumerWidget {
  const LeaderboardPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Leaderboard')),
      body: AsyncValueView(
        value: ref.watch(leaderboardProvider),
        onRetry: () => ref.invalidate(leaderboardProvider),
        data: (entries) {
          final top3 = entries.take(3).toList();
          final rest = entries.skip(3).toList();
          return CustomScrollView(
            slivers: [
              if (top3.length == 3)
                SliverToBoxAdapter(
                  child: _Podium(entries: top3),
                ),
              SliverList.separated(
                itemCount: rest.length,
                separatorBuilder: (_, _) =>
                    Divider(indent: KSpace.lg, endIndent: KSpace.lg, color: c.border),
                itemBuilder: (_, i) => _RankRow(entry: rest[i]),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _Podium extends StatelessWidget {
  const _Podium({required this.entries});
  final List<LeaderboardEntryDto> entries;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // entries[0]=rank1, entries[1]=rank2, entries[2]=rank3
    return Padding(
      padding: const EdgeInsets.all(KSpace.lg),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          // 2nd place
          Expanded(child: _PodiumSlot(entry: entries[1], height: 80, c: c)),
          // 1st place (taller)
          Expanded(child: _PodiumSlot(entry: entries[0], height: 110, c: c, crown: true)),
          // 3rd place
          Expanded(child: _PodiumSlot(entry: entries[2], height: 60, c: c)),
        ],
      ),
    );
  }
}

class _PodiumSlot extends StatelessWidget {
  const _PodiumSlot({
    required this.entry,
    required this.height,
    required this.c,
    this.crown = false,
  });
  final LeaderboardEntryDto entry;
  final double height;
  final KurxColors c;
  final bool crown;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: entry.username != null,
      label: entry.username == null ? null : "${entry.name}'s profile",
      child: GestureDetector(
      onTap: entry.username != null ? () => context.push('/u/${entry.username}') : null,
      child: Column(
        children: [
        if (crown)
          Icon(Icons.workspace_premium_rounded, color: c.accent, size: 24),
        KurxAvatar(name: entry.name, size: crown ? 56 : 44),
        const SizedBox(height: KSpace.xs),
        Text(
          entry.name.split(' ').first,
          style: TextStyle(
            color: c.text,
            fontWeight: FontWeight.w700,
            fontSize: 12,
          ),
          overflow: TextOverflow.ellipsis,
        ),
        Text(
          '${entry.points} pts',
          style: TextStyle(color: c.muted, fontSize: 11),
        ),
        const SizedBox(height: KSpace.xs),
        Container(
          height: height,
          decoration: BoxDecoration(
            color: crown ? c.accent : c.elevated,
            borderRadius: const BorderRadius.vertical(
                top: Radius.circular(KRadius.md)),
          ),
          alignment: Alignment.center,
          child: Text(
            '#${entry.rank}',
            style: TextStyle(
              color: crown ? c.onAccent : c.muted,
              fontWeight: FontWeight.w900,
              fontSize: 18,
            ),
          ),
        ),
        ],
      ),
      ),
    );
  }
}

class _RankRow extends StatelessWidget {
  const _RankRow({required this.entry});
  final LeaderboardEntryDto entry;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return ListTile(
      onTap: entry.username != null ? () => context.push('/u/${entry.username}') : null,
      leading: Text(
        '#${entry.rank}',
        style: TextStyle(
          color: c.muted,
          fontWeight: FontWeight.w800,
          fontSize: 15,
        ),
      ),
      title: Text(
        entry.name,
        style: TextStyle(
          color: c.text,
          fontWeight: FontWeight.w700,
          fontSize: 14,
        ),
      ),
      trailing: Text(
        '${entry.points} pts',
        style: TextStyle(
          color: c.accent,
          fontWeight: FontWeight.w800,
          fontSize: 14,
        ),
      ),
    );
  }
}
