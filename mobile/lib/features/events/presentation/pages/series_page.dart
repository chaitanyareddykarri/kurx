import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/competition_dtos.dart';
import '../providers/competition_providers.dart';

/// Series / edition browser — `GET /v1/series/{id}` + `/events` (V3 Phase 12).
///
/// A series is either **RECURRING** (a repeating event driven by an rrule — a weekly meetup) or
/// **EDITIONS** (numbered instalments — a conference's 2024/2025/2026 editions). The two read very
/// differently, so editions are labelled by ordinal and recurrences by date.
///
/// Both series detail and its member list are `AllowAnonymous`, so this works for a signed-out
/// browser; only Follow needs a session.
class SeriesPage extends ConsumerWidget {
  const SeriesPage({super.key, required this.seriesId});
  final String seriesId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Series')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(seriesProvider(seriesId));
          ref.invalidate(seriesEditionsProvider(seriesId));
          await ref.read(seriesProvider(seriesId).future);
        },
        child: AsyncValueView(
          value: ref.watch(seriesProvider(seriesId)),
          onRetry: () => ref.invalidate(seriesProvider(seriesId)),
          data: (series) => ListView(
            padding: const EdgeInsets.all(KSpace.lg),
            children: [
              _Header(series: series, onFollow: () => _follow(context, ref, series)),
              const SizedBox(height: KSpace.xl),
              Text(
                series.mode.toLowerCase() == 'editions' ? 'Editions' : 'Upcoming dates',
                style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15),
              ),
              const SizedBox(height: KSpace.sm),
              AsyncValueView(
                value: ref.watch(seriesEditionsProvider(seriesId)),
                onRetry: () => ref.invalidate(seriesEditionsProvider(seriesId)),
                isEmpty: (list) => list.isEmpty,
                empty: const EmptyState(
                  icon: Icons.event_repeat_outlined,
                  title: 'No dates announced',
                  message: 'Follow this series to hear when the next one is scheduled.',
                ),
                data: (editions) => Column(
                  children: [
                    for (final e in editions)
                      _EditionTile(
                        edition: e,
                        isEditions: series.mode.toLowerCase() == 'editions',
                        onTap: () => context.push('/events/${e.slug}'),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _follow(BuildContext context, WidgetRef ref, SeriesDto series) async {
    try {
      await ref.read(competitionActionsProvider).followSeries(series.id, follow: true);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Following ${series.name}.')),
        );
      }
    } on ApiError catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
      }
    }
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.series, required this.onFollow});
  final SeriesDto series;
  final VoidCallback onFollow;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(series.name,
              style: TextStyle(color: c.text, fontWeight: FontWeight.w800, fontSize: 20)),
          if (series.description.isNotEmpty) ...[
            const SizedBox(height: KSpace.sm),
            Text(series.description, style: TextStyle(color: c.muted)),
          ],
          const SizedBox(height: KSpace.md),
          Row(
            children: [
              Icon(Icons.people_outline, size: 16, color: c.muted),
              const SizedBox(width: KSpace.xs),
              Text('${series.followerCount} following',
                  style: TextStyle(color: c.muted, fontSize: 13)),
              const SizedBox(width: KSpace.lg),
              Icon(Icons.event_outlined, size: 16, color: c.muted),
              const SizedBox(width: KSpace.xs),
              Text('${series.memberCount} events',
                  style: TextStyle(color: c.muted, fontSize: 13)),
            ],
          ),
          const SizedBox(height: KSpace.lg),
          FilledButton.icon(
            onPressed: onFollow,
            icon: const Icon(Icons.notifications_active_outlined, size: 18),
            label: const Text('Follow series'),
          ),
        ],
      ),
    );
  }
}

class _EditionTile extends StatelessWidget {
  const _EditionTile({
    required this.edition,
    required this.isEditions,
    required this.onTap,
  });

  final SeriesMemberDto edition;
  final bool isEditions;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final starts = edition.startsAt;

    final lead = isEditions
        ? (edition.editionLabel?.isNotEmpty == true
            ? edition.editionLabel!
            : edition.editionOrdinal != null
                ? '#${edition.editionOrdinal}'
                : '—')
        : (starts != null ? DateFormat('d MMM').format(starts.toLocal()) : '—');

    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: KSpace.xs),
      tileColor: c.cardSurface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(KRadius.md)),
      leading: SizedBox(
        width: 52,
        child: Text(
          lead,
          style: TextStyle(color: c.accent, fontWeight: FontWeight.w700),
        ),
      ),
      title: Text(edition.title, style: TextStyle(color: c.text)),
      subtitle: starts == null
          ? null
          : Text(DateFormat('EEE d MMM yyyy, h:mm a').format(starts.toLocal()),
              style: TextStyle(color: c.muted, fontSize: 13)),
      trailing: const Icon(Icons.chevron_right_rounded),
      onTap: onTap,
    );
  }
}
