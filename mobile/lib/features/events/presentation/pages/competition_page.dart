import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/competition_dtos.dart';
import '../providers/competition_providers.dart';

/// Competition / stage viewer — `GET /v1/events/{id}/stages`, `/v1/stages/{id}/participants`,
/// `/v1/stages/{id}/results` (V3 Phase 11).
///
/// **Read-only, on purpose.** Creating stages, setting scoring policies, entering judge scores and
/// publishing results are organiser actions that live in the admin console. This is the competitor
/// and spectator view.
///
/// **Fixtures are deliberately absent.** `GET /v1/stages/{id}/fixtures` exists, but `FixtureView`
/// types its participants as `IReadOnlyList<FixtureSubjectInput>` — an *input* record reused as
/// output, carrying `subjectType` + `subjectId` and **no display name**. A bracket rendered from
/// that would show raw GUIDs. Stages, rosters and results all carry a server-resolved
/// `subjectName`, so those render properly and fixtures wait for an output DTO.
class CompetitionPage extends ConsumerWidget {
  const CompetitionPage({super.key, required this.eventId});
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Competition')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(eventStagesProvider(eventId));
          await ref.read(eventStagesProvider(eventId).future);
        },
        child: AsyncValueView(
          value: ref.watch(eventStagesProvider(eventId)),
          onRetry: () => ref.invalidate(eventStagesProvider(eventId)),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.emoji_events_outlined,
            title: 'No stages yet',
            message: 'Rounds and results appear here once the organiser sets them up.',
          ),
          data: (stages) {
            final ordered = [...stages]..sort((a, b) => a.sequence.compareTo(b.sequence));
            return ListView.separated(
              padding: const EdgeInsets.all(KSpace.lg),
              itemCount: ordered.length,
              separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
              itemBuilder: (_, i) => _StageCard(stage: ordered[i]),
            );
          },
        ),
      ),
    );
  }
}

class _StageCard extends ConsumerStatefulWidget {
  const _StageCard({required this.stage});
  final StageDto stage;

  @override
  ConsumerState<_StageCard> createState() => _StageCardState();
}

class _StageCardState extends ConsumerState<_StageCard> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final stage = widget.stage;

    // The server decides whether results are visible at all; the client never guesses. A stage
    // whose results are private stays collapsed to its roster even when it has been scored.
    final resultsVisible =
        stage.resultsVisibility.toLowerCase() == 'public' && stage.state == 'published';

    return Container(
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
      ),
      child: Column(
        children: [
          ListTile(
            leading: CircleAvatar(
              backgroundColor: c.accent.withValues(alpha: 0.15),
              child: Text('${stage.sequence}',
                  style: TextStyle(color: c.accent, fontWeight: FontWeight.w700)),
            ),
            title: Text(stage.name,
                style: TextStyle(color: c.text, fontWeight: FontWeight.w700)),
            subtitle: Text(
              [
                if (stage.format.isNotEmpty) _humanise(stage.format),
                '${stage.participantCount} entrant${stage.participantCount == 1 ? '' : 's'}',
              ].join(' · '),
              style: TextStyle(color: c.muted, fontSize: 13),
            ),
            trailing: _StateChip(state: stage.state),
            onTap: () => setState(() => _expanded = !_expanded),
          ),
          if (_expanded) ...[
            const Divider(height: 1),
            Padding(
              padding: const EdgeInsets.all(KSpace.lg),
              child: resultsVisible
                  ? _Results(stageId: stage.id)
                  : _Roster(stageId: stage.id, state: stage.state),
            ),
          ],
        ],
      ),
    );
  }

  static String _humanise(String s) =>
      s.isEmpty ? s : s[0].toUpperCase() + s.substring(1).replaceAll('_', ' ');
}

class _Roster extends ConsumerWidget {
  const _Roster({required this.stageId, required this.state});
  final String stageId;
  final String state;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return AsyncValueView(
      value: ref.watch(stageParticipantsProvider(stageId)),
      onRetry: () => ref.invalidate(stageParticipantsProvider(stageId)),
      isEmpty: (list) => list.isEmpty,
      empty: Padding(
        padding: const EdgeInsets.symmetric(vertical: KSpace.md),
        child: Text('No entrants yet.', style: TextStyle(color: c.muted)),
      ),
      data: (participants) {
        final ordered = [...participants]
          ..sort((a, b) => (a.seed ?? 9999).compareTo(b.seed ?? 9999));
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (final p in ordered)
              Padding(
                padding: const EdgeInsets.only(bottom: KSpace.sm),
                child: Row(
                  children: [
                    SizedBox(
                      width: 32,
                      child: Text(p.seed != null ? '#${p.seed}' : '—',
                          style: TextStyle(color: c.muted, fontSize: 13)),
                    ),
                    Expanded(
                      child: Text(p.subjectName.isEmpty ? '(unknown)' : p.subjectName,
                          style: TextStyle(color: c.text)),
                    ),
                    if (p.advanced)
                      Icon(Icons.arrow_upward_rounded, size: 16, color: c.accent),
                  ],
                ),
              ),
            if (state != 'published')
              Padding(
                padding: const EdgeInsets.only(top: KSpace.sm),
                child: Text('Results are not published yet.',
                    style: TextStyle(color: c.muted, fontSize: 13)),
              ),
          ],
        );
      },
    );
  }
}

class _Results extends ConsumerWidget {
  const _Results({required this.stageId});
  final String stageId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return AsyncValueView(
      value: ref.watch(stageResultsProvider(stageId)),
      onRetry: () => ref.invalidate(stageResultsProvider(stageId)),
      isEmpty: (list) => list.isEmpty,
      empty: Padding(
        padding: const EdgeInsets.symmetric(vertical: KSpace.md),
        child: Text('No results recorded.', style: TextStyle(color: c.muted)),
      ),
      data: (results) {
        final ordered = [...results]..sort((a, b) => a.rank.compareTo(b.rank));
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (final r in ordered)
              Padding(
                padding: const EdgeInsets.only(bottom: KSpace.sm),
                child: Row(
                  children: [
                    SizedBox(
                      width: 32,
                      child: Text(
                        '${r.rank}',
                        style: TextStyle(
                          color: r.rank <= 3 ? c.accent : c.muted,
                          fontWeight: r.rank <= 3 ? FontWeight.w800 : FontWeight.w400,
                        ),
                      ),
                    ),
                    Expanded(
                      child: Text(r.subjectName.isEmpty ? '(unknown)' : r.subjectName,
                          style: TextStyle(color: c.text)),
                    ),
                    if (r.finalScore != null)
                      Text(r.finalScore!.toStringAsFixed(1),
                          style: TextStyle(color: c.text, fontWeight: FontWeight.w600)),
                    if (r.correctionCount > 0) ...[
                      const SizedBox(width: KSpace.xs),
                      Tooltip(
                        message: 'Corrected ${r.correctionCount}×',
                        child: Icon(Icons.history_rounded, size: 15, color: c.muted),
                      ),
                    ],
                  ],
                ),
              ),
          ],
        );
      },
    );
  }
}

class _StateChip extends StatelessWidget {
  const _StateChip({required this.state});
  final String state;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final (Color tone, String label) = switch (state) {
      // `Colors.green` is Material's, not Kurx's: it ignores the theme, ignores dark mode, and was
      // never solved against the surfaces it sits on — while every sibling below already reads from
      // the token set. This is the hardcoded-status-colour class the deferred ledger names.
      'published' => (c.success, 'Results'),
      'running' => (c.accent, 'Live'),
      'completed' => (c.muted, 'Done'),
      'open' => (c.accent, 'Open'),
      _ => (c.muted, 'Draft'),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: KSpace.xs),
      decoration: BoxDecoration(
        color: tone.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(KRadius.pill),
      ),
      child: Text(label,
          style: TextStyle(color: tone, fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }
}
