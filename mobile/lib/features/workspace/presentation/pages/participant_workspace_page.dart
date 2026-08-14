import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/kurx_card.dart';
import '../../../../core/theme/design_tokens.dart';

/// The **participant workspace** for one event — what opens once a registration is confirmed.
///
/// This is a launcher, not a re-implementation: every destination below is a screen the app already
/// ships, reached by event id. Building parallel copies of chat, teams, results and reviews inside a
/// "workspace" shell would have been a second implementation of each, and they would have drifted.
///
/// Tiles with nothing real behind them are deliberately absent rather than present and dead:
///
/// * Tasks/Task Submission, Resources/Files and Attendance have no endpoint at all — the
///   `submissions` capability slug is declared in the registry with no implementation behind it.
/// * Announcements has a participant-readable endpoint (`GET /v1/events/{id}/announcements`) but no
///   participant-facing screen; the only announcements page in the app is org-scoped and would 404
///   for an attendee. It belongs here the moment that screen exists.
class ParticipantWorkspacePage extends StatelessWidget {
  const ParticipantWorkspacePage({super.key, required this.eventId});

  final String eventId;

  @override
  Widget build(BuildContext context) {
    final tiles = <_Tile>[
      _Tile(
        icon: Icons.forum_outlined,
        label: 'Event chat',
        onTap: () => context.push('/chats/$eventId'),
      ),
      _Tile(
        icon: Icons.emoji_events_outlined,
        label: 'Results',
        onTap: () => context.push('/events/$eventId/competition'),
      ),
      _Tile(
        icon: Icons.leaderboard_outlined,
        label: 'Leaderboard',
        onTap: () => context.push('/leaderboard'),
      ),
      _Tile(
        icon: Icons.workspace_premium_outlined,
        label: 'Certificates',
        onTap: () => context.push('/certificates'),
      ),
      _Tile(
        icon: Icons.rate_review_outlined,
        label: 'Feedback',
        onTap: () => context.push('/events/$eventId/reviews'),
      ),
      _Tile(
        icon: Icons.article_outlined,
        label: 'Event posts',
        onTap: () => context.push('/posts/event/$eventId'),
      ),
    ];

    return Scaffold(
      appBar: AppBar(title: const Text('Participant workspace')),
      body: GridView.count(
        crossAxisCount: 2,
        padding: const EdgeInsets.all(KSpace.lg),
        mainAxisSpacing: KSpace.md,
        crossAxisSpacing: KSpace.md,
        childAspectRatio: 1.3,
        children: [
          for (final t in tiles)
            KurxCard(
              onTap: t.onTap,
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(t.icon, color: context.kurx.accent),
                  const SizedBox(height: KSpace.sm),
                  Text(
                    t.label,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          fontWeight: FontWeight.w600,
                          color: context.kurx.text,
                        ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _Tile {
  const _Tile({required this.icon, required this.label, required this.onTap});

  final IconData icon;
  final String label;
  final VoidCallback onTap;
}
