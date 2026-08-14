import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../bookmarks/presentation/providers/bookmarks_providers.dart';
import '../../../events/presentation/widgets/event_visuals.dart';

/// A saved event on a given day.
class _Entry {
  const _Entry({
    required this.title,
    required this.at,
    required this.route,
    this.category,
  });
  final String title;
  final DateTime at;
  final String route;
  final String? category;
}

/// A month calendar over the user's saved events. Days with
/// events are dotted; tapping a day lists that day's events.
class CalendarPage extends ConsumerStatefulWidget {
  const CalendarPage({super.key});

  @override
  ConsumerState<CalendarPage> createState() => _CalendarPageState();
}

class _CalendarPageState extends ConsumerState<CalendarPage> {
  late DateTime _month;
  late DateTime _selected;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _month = DateTime(now.year, now.month);
    _selected = DateTime(now.year, now.month, now.day);
  }

  Map<DateTime, List<_Entry>> _entriesByDay(WidgetRef ref) {
    final map = <DateTime, List<_Entry>>{};
    void add(_Entry e) {
      final key = DateTime(e.at.year, e.at.month, e.at.day);
      map.putIfAbsent(key, () => []).add(e);
    }

    for (final ev in ref.watch(bookmarksControllerProvider)) {
      if (ev.startsAt != null) {
        add(
          _Entry(
            title: ev.title,
            at: ev.startsAt!,
            route: '/events/${ev.slug}',
            category: ev.categoryName,
          ),
        );
      }
    }
    return map;
  }

  @override
  Widget build(BuildContext context) {
    final byDay = _entriesByDay(ref);
    final dayEntries =
        byDay[DateTime(_selected.year, _selected.month, _selected.day)] ?? [];
    final hasAny = byDay.isNotEmpty;

    return Scaffold(
      appBar: AppBar(title: const Text('Calendar')),
      body: ContentWidth(
        maxWidth: 560,
        child: ListView(
          padding: const EdgeInsets.all(KSpace.lg),
          children: [
            _MonthHeader(
              month: _month,
              onPrev: () => setState(
                () => _month = DateTime(_month.year, _month.month - 1),
              ),
              onNext: () => setState(
                () => _month = DateTime(_month.year, _month.month + 1),
              ),
            ),
            const SizedBox(height: KSpace.md),
            _MonthGrid(
              month: _month,
              selected: _selected,
              markedDays: byDay.keys.toSet(),
              onSelect: (d) => setState(() => _selected = d),
            ),
            const SizedBox(height: KSpace.lg),
            if (!hasAny)
              const Padding(
                padding: EdgeInsets.only(top: KSpace.xl),
                child: EmptyState(
                  icon: Icons.calendar_month_outlined,
                  title: 'Your calendar is empty',
                  message: 'Save events and they’ll appear here by date.',
                ),
              )
            else ...[
              Text(
                DateFormat('EEEE, d MMMM', 'en_IN').format(_selected),
                style: TextStyle(
                  color: context.kurx.text,
                  fontSize: 15,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: KSpace.sm),
              if (dayEntries.isEmpty)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: KSpace.lg),
                  child: Text(
                    'No events on this day.',
                    style: TextStyle(color: context.kurx.muted),
                  ),
                )
              else
                for (final e in dayEntries) _EntryTile(entry: e),
            ],
          ],
        ),
      ),
    );
  }
}

class _MonthHeader extends StatelessWidget {
  const _MonthHeader({
    required this.month,
    required this.onPrev,
    required this.onNext,
  });

  final DateTime month;
  final VoidCallback onPrev;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Row(
      children: [
        Text(
          DateFormat('MMMM y', 'en_IN').format(month),
          style: TextStyle(
            color: c.text,
            fontSize: 18,
            fontWeight: FontWeight.w800,
          ),
        ),
        const Spacer(),
        IconButton(
          onPressed: onPrev,
          icon: const Icon(Icons.chevron_left_rounded),
        ),
        IconButton(
          onPressed: onNext,
          icon: const Icon(Icons.chevron_right_rounded),
        ),
      ],
    );
  }
}

class _MonthGrid extends StatelessWidget {
  const _MonthGrid({
    required this.month,
    required this.selected,
    required this.markedDays,
    required this.onSelect,
  });

  final DateTime month;
  final DateTime selected;
  final Set<DateTime> markedDays;
  final ValueChanged<DateTime> onSelect;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final daysInMonth = DateTime(month.year, month.month + 1, 0).day;
    final firstWeekday =
        DateTime(month.year, month.month, 1).weekday % 7; // 0 = Sunday
    final today = DateTime.now();
    const labels = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];

    return Column(
      children: [
        Row(
          children: [
            for (final l in labels)
              Expanded(
                child: Center(
                  child: Text(
                    l,
                    style: TextStyle(
                      color: c.muted,
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ),
          ],
        ),
        const SizedBox(height: KSpace.sm),
        GridView.count(
          crossAxisCount: 7,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          children: [
            for (var i = 0; i < firstWeekday; i++) const SizedBox.shrink(),
            for (var day = 1; day <= daysInMonth; day++)
              _DayCell(
                day: day,
                date: DateTime(month.year, month.month, day),
                isSelected:
                    selected.year == month.year &&
                    selected.month == month.month &&
                    selected.day == day,
                isToday:
                    today.year == month.year &&
                    today.month == month.month &&
                    today.day == day,
                marked: markedDays.contains(
                  DateTime(month.year, month.month, day),
                ),
                onTap: () => onSelect(DateTime(month.year, month.month, day)),
              ),
          ],
        ),
      ],
    );
  }
}

class _DayCell extends StatelessWidget {
  const _DayCell({
    required this.day,
    required this.date,
    required this.isSelected,
    required this.isToday,
    required this.marked,
    required this.onTap,
  });

  final int day;
  final DateTime date;
  final bool isSelected;
  final bool isToday;
  final bool marked;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return InkResponse(
      onTap: onTap,
      radius: 22,
      child: Center(
        child: Container(
          height: 36,
          width: 36,
          decoration: BoxDecoration(
            color: isSelected ? c.accent : Colors.transparent,
            shape: BoxShape.circle,
            border: isToday && !isSelected ? Border.all(color: c.accent) : null,
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(
                '$day',
                style: TextStyle(
                  color: isSelected ? c.onAccent : c.text,
                  fontSize: 13.5,
                  fontWeight: isToday || isSelected
                      ? FontWeight.w800
                      : FontWeight.w500,
                ),
              ),
              if (marked)
                Container(
                  margin: const EdgeInsets.only(top: 1),
                  height: 4,
                  width: 4,
                  decoration: BoxDecoration(
                    color: isSelected ? c.onAccent : c.accent,
                    shape: BoxShape.circle,
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EntryTile extends StatelessWidget {
  const _EntryTile({required this.entry});

  final _Entry entry;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.sm),
      child: Material(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.md),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: () => context.push(entry.route),
          child: Ink(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(KRadius.md),
              border: Border.all(color: c.border),
            ),
            padding: const EdgeInsets.all(KSpace.md),
            child: Row(
              children: [
                Icon(
                  EventVisuals.iconFor(entry.category),
                  size: 20,
                  color: c.accent,
                ),
                const SizedBox(width: KSpace.md),
                Expanded(
                  child: Text(
                    entry.title,
                    style: TextStyle(
                      color: c.text,
                      fontSize: 14.5,
                      fontWeight: FontWeight.w700,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                Text(
                  DateFormat('h:mm a').format(entry.at),
                  style: TextStyle(color: c.muted, fontSize: 12.5),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
