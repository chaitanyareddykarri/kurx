import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../data/models/public_profile_dto.dart';

/// Metrics and Experience (D-225), mirroring the web `MetricsPanel` one-for-one.
///
/// Two rules this widget exists to honour:
///
///  1. **Null means hidden, never zero.** A hidden section renders an em dash. Printing 0 would turn
///     the owner's privacy choice into a statement about them ("organized 0 events").
///  2. **The Experience band never appears alone.** A band on its own is an unfalsifiable judgement;
///     shown beside the counts that produced it, a reader can check it and disagree.
class MetricsPanel extends StatelessWidget {
  const MetricsPanel({super.key, this.metrics, this.experience});

  final ProfileMetricsDto? metrics;
  final ExperienceSummaryDto? experience;

  static String _count(int? value) => value?.toString() ?? '—';

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final m = metrics;
    final e = experience;
    // Both sections are independently gated server-side; if neither is visible to this viewer the
    // whole panel is omitted rather than rendered as an empty shell.
    if (m == null && e == null) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Activity',
              style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w800)),
          const SizedBox(height: KSpace.sm),
          if (e != null) _ExperienceBand(experience: e),
          if (m != null) ...[
            const SizedBox(height: KSpace.md),
            _MetricGrid(metrics: m),
            if (m.cities.isNotEmpty) ...[
              const SizedBox(height: KSpace.sm),
              Text(
                'Active in ${m.cities.take(4).join(', ')}'
                '${m.cities.length > 4 ? ' +${m.cities.length - 4} more' : ''}',
                style: TextStyle(color: c.muted, fontSize: 12.5),
              ),
            ],
            if (m.eventDna.isNotEmpty) ...[
              const SizedBox(height: KSpace.lg),
              _EventDna(dna: m.eventDna),
            ],
          ],
        ],
      ),
    );
  }
}

class _ExperienceBand extends StatelessWidget {
  const _ExperienceBand({required this.experience});

  final ExperienceSummaryDto experience;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final e = experience;

    // The counts behind the band — always shown with it, never instead of it.
    final parts = <String>[
      '${e.distinctEvents} event${e.distinctEvents == 1 ? '' : 's'}',
      if (e.leadershipEvents > 0) '${e.leadershipEvents} led',
      if (e.organizations > 0)
        '${e.organizations} organization${e.organizations == 1 ? '' : 's'}',
      if (e.yearsActive > 0) '${e.yearsActive} year${e.yearsActive == 1 ? '' : 's'} active',
    ];

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.md),
        border: Border.all(color: c.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(e.band,
              style: TextStyle(color: c.text, fontSize: 15, fontWeight: FontWeight.w700)),
          const SizedBox(height: 2),
          Text(parts.join(' · '), style: TextStyle(color: c.muted, fontSize: 12.5)),
        ],
      ),
    );
  }
}

class _MetricGrid extends StatelessWidget {
  const _MetricGrid({required this.metrics});

  final ProfileMetricsDto metrics;

  @override
  Widget build(BuildContext context) {
    final m = metrics;
    final tiles = <(String, String, String?)>[
      ('Organized', MetricsPanel._count(m.eventsOrganized), null),
      ('Participated', MetricsPanel._count(m.eventsParticipated), null),
      ('Attended', MetricsPanel._count(m.eventsAttended), null),
      if ((m.speakerSessions ?? 0) > 0) ('Talks given', MetricsPanel._count(m.speakerSessions), null),
      if ((m.competitionsEntered ?? 0) > 0)
        ('Competitions', MetricsPanel._count(m.competitionsEntered),
            (m.competitionsWon ?? 0) > 0 ? '${m.competitionsWon} won' : null),
      if ((m.assignmentsAccepted ?? 0) > 0)
        ('Assignments', MetricsPanel._count(m.assignmentsAccepted),
            '${m.assignmentsCompleted} completed'),
      ('Certificates', MetricsPanel._count(m.certificates), null),
      ('Organizations', MetricsPanel._count(m.organizations),
          (m.verifiedOrganizations ?? 0) > 0 ? '${m.verifiedOrganizations} verified' : null),
      if (m.completionRate != null)
        ('Completion', '${(m.completionRate! * 100).round()}%', null),
    ];

    return Wrap(
      spacing: KSpace.sm,
      runSpacing: KSpace.sm,
      children: [for (final (label, value, hint) in tiles) _MetricTile(label: label, value: value, hint: hint)],
    );
  }
}

class _MetricTile extends StatelessWidget {
  const _MetricTile({required this.label, required this.value, this.hint});

  final String label;
  final String value;
  final String? hint;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      width: 108,
      padding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: KSpace.sm),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.md),
        border: Border.all(color: c.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(value, style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w800)),
          Text(label, style: TextStyle(color: c.muted, fontSize: 11.5)),
          if (hint != null) Text(hint!, style: TextStyle(color: c.muted, fontSize: 10.5)),
        ],
      ),
    );
  }
}

/// Event DNA — a distribution across event kinds, drawn as proportional bars.
///
/// Deliberately bars rather than a sparkline: a sparkline reads as a time series, and this is a
/// categorical breakdown. Using one here would imply a trend the data does not describe.
class _EventDna extends StatelessWidget {
  const _EventDna({required this.dna});

  final List<EventDnaTagDto> dna;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final max = dna.map((d) => d.count).reduce((a, b) => a > b ? a : b);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Event DNA',
            style: TextStyle(color: c.text, fontSize: 14, fontWeight: FontWeight.w700)),
        const SizedBox(height: 2),
        Text('The kinds of event this person shows up for.',
            style: TextStyle(color: c.muted, fontSize: 12)),
        const SizedBox(height: KSpace.sm),
        for (final tag in dna)
          Padding(
            padding: const EdgeInsets.only(bottom: 6),
            child: Row(
              children: [
                SizedBox(
                  width: 96,
                  child: Text(
                    tag.kind.replaceAll(RegExp(r'[-_]'), ' '),
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(color: c.muted, fontSize: 12),
                  ),
                ),
                Expanded(
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(KRadius.pill),
                    child: LinearProgressIndicator(
                      value: tag.count / max,
                      minHeight: 8,
                      backgroundColor: c.border,
                      valueColor: AlwaysStoppedAnimation<Color>(c.accent),
                    ),
                  ),
                ),
                const SizedBox(width: KSpace.sm),
                SizedBox(
                  width: 24,
                  child: Text('${tag.count}',
                      textAlign: TextAlign.right,
                      style: TextStyle(color: c.muted, fontSize: 12)),
                ),
              ],
            ),
          ),
      ],
    );
  }
}
