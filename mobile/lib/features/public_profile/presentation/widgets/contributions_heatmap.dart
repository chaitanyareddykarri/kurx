import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../data/models/public_profile_dto.dart';

/// The contributions heatmap (D-228), mirroring the web component.
///
/// Renders a **26-week** window by default rather than web's 52: a full year of columns does not fit
/// a phone at a legible cell size, and shrinking the cells to fit would make the grid unreadable
/// rather than merely smaller. It scrolls horizontally inside its own viewport so the page never does.
///
/// The server sends only days that *have* activity, each with a 0–4 intensity band, so this widget
/// fills the gaps and never re-decides the scale.
class ContributionsHeatmap extends StatelessWidget {
  const ContributionsHeatmap({super.key, required this.contributions, this.weeks = 26});

  final ContributionsDto? contributions;
  final int weeks;

  static const _cell = 11.0;
  static const _gap = 3.0;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final data = contributions;
    // Absence is honest for someone with no verified activity; an empty grid would read as
    // "inactive", which is a claim the data does not make.
    if (data == null || data.total == 0) return const SizedBox.shrink();

    final byDay = {for (final d in data.days) d.date: d};
    final end = _startOfWeek(DateTime.parse(data.to));

    final columns = <List<({String date, int count, int level})>>[];
    for (var w = weeks - 1; w >= 0; w--) {
      final weekStart = end.subtract(Duration(days: w * 7));
      final column = <({String date, int count, int level})>[];
      for (var d = 0; d < 7; d++) {
        final day = weekStart.add(Duration(days: d));
        final key = _isoDay(day);
        final hit = byDay[key];
        column.add((date: key, count: hit?.count ?? 0, level: hit?.level ?? 0));
      }
      columns.add(column);
    }

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Contributions',
              style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w800)),
          const SizedBox(height: 2),
          Text(
            '${data.total} contribution${data.total == 1 ? '' : 's'} — events run, taken part in, '
            'spoken at, and recognised.',
            style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.3),
          ),
          const SizedBox(height: KSpace.md),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            reverse: true, // open at the most recent week, which is what anyone looks at first
            child: Row(
              children: [
                for (final column in columns)
                  Padding(
                    padding: const EdgeInsets.only(right: _gap),
                    child: Column(
                      children: [
                        for (final day in column)
                          Padding(
                            padding: const EdgeInsets.only(bottom: _gap),
                            child: Tooltip(
                              message: '${day.count} on ${day.date}',
                              child: Container(
                                width: _cell,
                                height: _cell,
                                decoration: BoxDecoration(
                                  color: _levelColor(context, day.level),
                                  borderRadius: BorderRadius.circular(2),
                                ),
                              ),
                            ),
                          ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(height: KSpace.sm),
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              Text('Less', style: TextStyle(color: c.muted, fontSize: 11)),
              const SizedBox(width: 4),
              for (var level = 0; level <= 4; level++)
                Padding(
                  padding: const EdgeInsets.only(right: 3),
                  child: Container(
                    width: _cell,
                    height: _cell,
                    decoration: BoxDecoration(
                      color: _levelColor(context, level),
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ),
              Text('More', style: TextStyle(color: c.muted, fontSize: 11)),
            ],
          ),
        ],
      ),
    );
  }

  static Color _levelColor(BuildContext context, int level) {
    final c = context.kurx;
    return switch (level) {
      1 => c.accent.withValues(alpha: 0.25),
      2 => c.accent.withValues(alpha: 0.45),
      3 => c.accent.withValues(alpha: 0.7),
      >= 4 => c.accent,
      _ => c.border,
    };
  }

  static DateTime _startOfWeek(DateTime d) {
    final utc = DateTime.utc(d.year, d.month, d.day);
    return utc.subtract(Duration(days: utc.weekday % 7));
  }

  static String _isoDay(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-'
      '${d.month.toString().padLeft(2, '0')}-'
      '${d.day.toString().padLeft(2, '0')}';
}
