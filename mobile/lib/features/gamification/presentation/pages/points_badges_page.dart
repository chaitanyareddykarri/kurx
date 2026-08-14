import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/gamification_dto.dart';
import '../providers/gamification_providers.dart';

class PointsBadgesPage extends ConsumerWidget {
  const PointsBadgesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      // The profile menu that reaches this screen is labelled "Points & leaderboard", but the
      // leaderboard had no entry point anywhere in the app — this is it.
      appBar: AppBar(
        title: const Text('Points & Badges'),
        actions: [
          IconButton(
            icon: const Icon(Icons.leaderboard_outlined),
            tooltip: 'Leaderboard',
            onPressed: () => context.push('/leaderboard'),
          ),
        ],
      ),
      body: AsyncValueView(
        value: ref.watch(myPointsProvider),
        onRetry: () => ref.invalidate(myPointsProvider),
        data: (summary) => ListView(
          padding: const EdgeInsets.all(KSpace.lg),
          children: [
            // Points summary card. No level/tier system exists server-side (PointsSummary is
            // just totalPoints + history) — this shows what's real rather than a fabricated
            // progress ring, per D-212.
            DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  colors: [c.accent, c.teal],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                ),
                borderRadius: BorderRadius.circular(KRadius.xl),
              ),
              child: Padding(
                padding: const EdgeInsets.all(KSpace.xl),
                child: Column(
                  children: [
                    Text(
                      '${summary.totalPoints}',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 40,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                    const Text(
                      'total points',
                      style: TextStyle(color: Colors.white70, fontSize: 13),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: KSpace.xl),
            Text(
              'Badges',
              style: TextStyle(
                color: c.text,
                fontWeight: FontWeight.w700,
                fontSize: 16,
              ),
            ),
            const SizedBox(height: KSpace.md),
            Consumer(
              builder: (context, ref, _) {
                final badges = ref.watch(myBadgesProvider);
                return badges.when(
                  loading: () => const Padding(
                    padding: EdgeInsets.symmetric(vertical: KSpace.xl),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                  error: (_, _) => Padding(
                    padding: const EdgeInsets.all(KSpace.xl),
                    child: Center(
                      child: TextButton(
                        onPressed: () => ref.invalidate(myBadgesProvider),
                        child: const Text('Retry'),
                      ),
                    ),
                  ),
                  data: (list) => list.isEmpty
                      ? Center(
                          child: Padding(
                            padding: const EdgeInsets.all(KSpace.xl),
                            child: Text(
                              'Earn badges by attending events and completing challenges.',
                              textAlign: TextAlign.center,
                              style: TextStyle(color: c.muted, fontSize: 13.5),
                            ),
                          ),
                        )
                      : GridView.builder(
                          shrinkWrap: true,
                          physics: const NeverScrollableScrollPhysics(),
                          gridDelegate:
                              const SliverGridDelegateWithFixedCrossAxisCount(
                            crossAxisCount: 3,
                            crossAxisSpacing: KSpace.md,
                            mainAxisSpacing: KSpace.md,
                          ),
                          itemCount: list.length,
                          itemBuilder: (_, i) => _BadgeTile(badge: list[i]),
                        ),
                );
              },
            ),
            if (summary.history.isNotEmpty) ...[
              const SizedBox(height: KSpace.xl),
              Text(
                'Recent activity',
                style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 16),
              ),
              const SizedBox(height: KSpace.md),
              ...summary.history.take(20).map((entry) => Padding(
                    padding: const EdgeInsets.symmetric(vertical: KSpace.xs),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(entry.reason ?? entry.source,
                              style: TextStyle(color: c.text, fontSize: 13.5)),
                        ),
                        Text('+${entry.points}',
                            style: TextStyle(
                                color: c.accent, fontWeight: FontWeight.w700, fontSize: 13.5)),
                      ],
                    ),
                  )),
            ],
          ],
        ),
      ),
    );
  }
}

class _BadgeTile extends StatelessWidget {
  const _BadgeTile({required this.badge});
  final BadgeDto badge;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
        boxShadow: kCardShadow(context),
      ),
      child: Padding(
        padding: const EdgeInsets.all(KSpace.md),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.star_rounded, size: 36, color: c.accent),
            const SizedBox(height: KSpace.xs),
            Text(
              badge.name,
              textAlign: TextAlign.center,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(color: c.text, fontSize: 11, fontWeight: FontWeight.w700),
            ),
          ],
        ),
      ),
    );
  }
}
