import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/kurx_chip.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/event_category.dart';
import '../../domain/entities/event_kind.dart';
import '../../domain/entities/event_query.dart';
import '../providers/search_providers.dart';

/// Search box + filters (city, category, event date, mode, price, sort). Owns only ephemeral input
/// controllers; all filter state lives in [EventSearchController].
///
/// The query and the applied-filter chips stay on screen; everything else sits behind one "Filters"
/// disclosure. Previously all nine controls were inline and always expanded, which pushed the
/// results — the point of the screen — below the fold on any phone.
class EventFilterBar extends ConsumerStatefulWidget {
  const EventFilterBar({super.key});

  /// The earliest day the date filter may select — today, midnight.
  ///
  /// Was a year in the past, which offered a range of dates that could only ever match nothing:
  /// search returns events by start date, and a past one has already happened.
  @visibleForTesting
  static DateTime dateFloor([DateTime? now]) => DateUtils.dateOnly(now ?? DateTime.now());

  /// A previously chosen range, but only while it still starts on or after [floor] — the date range
  /// picker asserts if handed an initial range beginning before its `firstDate`.
  @visibleForTesting
  static DateTimeRange? initialRangeWithin(DateTimeRange? current, DateTime floor) =>
      current != null && !current.start.isBefore(floor) ? current : null;

  @override
  ConsumerState<EventFilterBar> createState() => _EventFilterBarState();
}

class _EventFilterBarState extends ConsumerState<EventFilterBar> {
  final _searchController = TextEditingController();
  final _cityController = TextEditingController();
  Timer? _debounce;
  bool _expanded = false;

  static const _sorts = <String?, String>{
    null: 'Soonest',
    'date_desc': 'Latest date',
    'newest': 'Newest',
    'popular': 'Popular',
  };

  static const _modes = <String?, String>{
    null: 'Any mode',
    'offline': 'In person',
    'online': 'Online',
    'hybrid': 'Hybrid',
  };

  static const _prices = <String?, String>{
    null: 'Any price',
    'free': 'Free',
    'paid': 'Paid',
  };

  @override
  void initState() {
    super.initState();
    // The controller outlives this widget — arriving from a category tile, or coming back from an
    // event, keeps the filters that were applied. Seeding the fields from it stops the inputs from
    // looking empty while their filter is demonstrably active.
    final query = ref.read(eventSearchControllerProvider).query;
    _searchController.text = query.q;
    _cityController.text = query.city ?? '';
    _expanded = _activeFilterCount(query) > 0;
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    _cityController.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350),
        () => ref.read(eventSearchControllerProvider.notifier).setSearchText(value));
  }

  Future<void> _pickDateRange(DateTimeRange? current) async {
    final floor = EventFilterBar.dateFloor();
    final range = await showDateRangePicker(
      context: context,
      firstDate: floor,
      lastDate: DateTime(floor.year + 5),
      initialDateRange: EventFilterBar.initialRangeWithin(current, floor),
      // Says what the calendar is for. "Select range" alone left the user to guess whether they
      // were picking when the event runs, when to be notified, or something else entirely.
      helpText: 'Find events on these dates',
      saveText: 'Apply',
      fieldStartHintText: 'Start date',
      fieldEndHintText: 'End date',
    );
    if (range != null) {
      ref.read(eventSearchControllerProvider.notifier).setDateRange(range.start, range.end);
    }
  }

  void _clearAll() {
    _searchController.clear();
    _cityController.clear();
    ref.read(eventSearchControllerProvider.notifier).clearFilters();
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final query = ref.watch(eventSearchControllerProvider).query;
    final categoriesAsync = ref.watch(categoriesProvider);
    final kindsAsync = ref.watch(kindsProvider);
    final controller = ref.read(eventSearchControllerProvider.notifier);
    final count = _activeFilterCount(query);

    return Padding(
      padding: const EdgeInsets.fromLTRB(KSpace.lg, KSpace.md, KSpace.lg, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _searchController,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: 'Search events',
              prefixIcon: const Icon(Icons.search),
              isDense: true,
              border: const OutlineInputBorder(),
              suffixIcon: _searchController.text.isEmpty
                  ? null
                  : IconButton(
                      // An unnamed control: it announced as "button" with nothing to say what it
                      // does, on the only way to clear a search without deleting text by hand.
                      tooltip: 'Clear search',
                      icon: const Icon(Icons.clear),
                      onPressed: () {
                        _searchController.clear();
                        controller.setSearchText('');
                        setState(() {});
                      },
                    ),
            ),
            onChanged: (v) {
              _onSearchChanged(v);
              setState(() {});
            },
          ),
          const SizedBox(height: KSpace.sm),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => setState(() => _expanded = !_expanded),
                  icon: Icon(_expanded ? Icons.expand_less : Icons.tune, size: 18),
                  label: Text(count == 0 ? 'Filters' : 'Filters ($count)'),
                ),
              ),
              if (query.hasActiveFilters) ...[
                const SizedBox(width: KSpace.sm),
                IconButton(
                  tooltip: 'Clear filters',
                  icon: const Icon(Icons.filter_alt_off),
                  onPressed: _clearAll,
                ),
              ],
            ],
          ),
          if (query.hasActiveFilters) ...[
            const SizedBox(height: KSpace.sm),
            _activeFilterChips(query, categoriesAsync, kindsAsync, controller),
          ],
          if (_expanded) ...[
            const SizedBox(height: KSpace.lg),
            _panel(context, query, categoriesAsync, kindsAsync, controller),
          ],
        ],
      ),
    );
  }

  /// The disclosed filters, grouped so the primary ones (what kind of event, and when) read ahead of
  /// the qualifiers. Lives inside the page's scroll view, so it can be any height without ever
  /// competing for space with the results or the keyboard.
  Widget _panel(
    BuildContext context,
    EventQuery query,
    AsyncValue<List<EventCategory>> categoriesAsync,
    AsyncValue<List<EventKind>> kindsAsync,
    EventSearchController controller,
  ) {
    final dateLabel = query.dateFrom == null && query.dateTo == null
        ? 'Any dates'
        : _rangeLabel(query.dateFrom, query.dateTo);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const _SectionLabel('Where'),
        TextField(
          controller: _cityController,
          textInputAction: TextInputAction.search,
          decoration: const InputDecoration(
            labelText: 'City',
            // The backend matches the city exactly (case-insensitively), so a partial name finds
            // nothing — worth saying before the user concludes there are no events near them.
            helperText: 'Full city name',
            prefixIcon: Icon(Icons.location_city),
            border: OutlineInputBorder(),
            isDense: true,
          ),
          onSubmitted: controller.setCity,
        ),
        const SizedBox(height: KSpace.lg),
        const _SectionLabel('Filters'),
        categoriesAsync.maybeWhen(
          data: (categories) => _LabeledDropdown<String?>(
            label: 'Category',
            value: query.categoryId,
            items: [
              const DropdownMenuItem<String?>(value: null, child: Text('All categories')),
              ...categories.map((c) => DropdownMenuItem<String?>(value: c.id, child: Text(c.name))),
            ],
            onChanged: controller.setCategory,
          ),
          orElse: () => const _DisabledDropdown(label: 'Category', value: 'All categories'),
        ),
        const SizedBox(height: KSpace.md),
        kindsAsync.maybeWhen(
          data: (kinds) => _LabeledDropdown<String?>(
            label: 'Event type',
            value: query.kind,
            items: [
              const DropdownMenuItem<String?>(value: null, child: Text('All types')),
              ...kinds.map((k) => DropdownMenuItem<String?>(value: k.slug, child: Text(k.name))),
            ],
            onChanged: controller.setKind,
          ),
          orElse: () => const _DisabledDropdown(label: 'Event type', value: 'All types'),
        ),
        const SizedBox(height: KSpace.md),
        // Named "Event date", not "Date": it filters on when the event happens, which the bare
        // calendar icon never made clear.
        // The whole field opens the picker, rather than a separate button beside the value — with
        // a long range label ("7 Aug – 11 Aug") plus a clear button, a trailing action had nowhere
        // to go on a narrow screen and overflowed the row.
        InkWell(
          onTap: () => _pickDateRange(
            query.dateFrom != null && query.dateTo != null
                ? DateTimeRange(start: query.dateFrom!, end: query.dateTo!)
                : null,
          ),
          borderRadius: BorderRadius.circular(KRadius.sm),
          child: InputDecorator(
            decoration: const InputDecoration(
              labelText: 'Event date',
              border: OutlineInputBorder(),
              isDense: true,
            ),
            child: Row(
              children: [
                const Icon(Icons.date_range, size: 20),
                const SizedBox(width: KSpace.sm),
                Expanded(child: Text(dateLabel, overflow: TextOverflow.ellipsis)),
                if (query.dateFrom != null || query.dateTo != null)
                  IconButton(
                    tooltip: 'Clear dates',
                    visualDensity: VisualDensity.compact,
                    padding: EdgeInsets.zero,
                    constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
                    icon: const Icon(Icons.clear, size: 18),
                    onPressed: () => controller.setDateRange(null, null),
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(height: KSpace.lg),
        const _SectionLabel('Additional filters'),
        _ChipRow(
          label: 'Mode',
          options: _modes,
          selected: query.mode,
          onSelected: (value) => controller.setMode(value),
        ),
        const SizedBox(height: KSpace.md),
        _ChipRow(
          label: 'Price',
          options: _prices,
          selected: query.price,
          onSelected: (value) => controller.setPrice(value),
        ),
        const SizedBox(height: KSpace.lg),
        const _SectionLabel('Sort'),
        _LabeledDropdown<String?>(
          label: 'Sort by',
          value: query.sort,
          items: _sorts.entries
              .map((e) => DropdownMenuItem<String?>(value: e.key, child: Text(e.value)))
              .toList(),
          onChanged: controller.setSort,
        ),
        const SizedBox(height: KSpace.md),
        // Applies the city as well as collapsing: it is the one filter typed rather than tapped,
        // and expecting people to press the keyboard's submit key to commit it silently dropped
        // whatever they had entered.
        FilledButton(
          onPressed: () {
            controller.setCity(_cityController.text);
            FocusScope.of(context).unfocus();
            setState(() => _expanded = false);
          },
          child: const Text('Show results'),
        ),
      ],
    );
  }

  /// One removable [KurxChip] per active filter — a single glance at what's applied, each independently
  /// clearable without disturbing the others (the existing "Clear filters" button still resets all of them).
  Widget _activeFilterChips(
    EventQuery query,
    AsyncValue<List<EventCategory>> categoriesAsync,
    AsyncValue<List<EventKind>> kindsAsync,
    EventSearchController controller,
  ) {
    final categories = categoriesAsync.valueOrNull ?? const <EventCategory>[];
    final kinds = kindsAsync.valueOrNull ?? const <EventKind>[];
    String labelFor<T>(List<T> items, bool Function(T) test, String Function(T) name, String fallback) {
      for (final item in items) {
        if (test(item)) return name(item);
      }
      return fallback;
    }

    final chips = <Widget>[
      if (query.q.isNotEmpty)
        KurxChip(
          label: '"${query.q}"',
          selected: true,
          onDeleted: () {
            _searchController.clear();
            controller.setSearchText('');
            setState(() {});
          },
        ),
      if (query.city != null && query.city!.isNotEmpty)
        KurxChip(
          label: query.city!,
          selected: true,
          onDeleted: () {
            _cityController.clear();
            controller.setCity(null);
            setState(() {});
          },
        ),
      if (query.categoryId != null)
        KurxChip(
          label: labelFor(categories, (c) => c.id == query.categoryId, (c) => c.name, query.categoryId!),
          selected: true,
          onDeleted: () => controller.setCategory(null),
        ),
      if (query.kind != null)
        KurxChip(
          label: labelFor(kinds, (k) => k.slug == query.kind, (k) => k.name, query.kind!),
          selected: true,
          onDeleted: () => controller.setKind(null),
        ),
      if (query.mode != null)
        KurxChip(label: _modes[query.mode] ?? query.mode!, selected: true, onDeleted: () => controller.setMode(null)),
      if (query.price != null)
        KurxChip(label: _prices[query.price] ?? query.price!, selected: true, onDeleted: () => controller.setPrice(null)),
      if (query.sort != null)
        KurxChip(label: _sorts[query.sort] ?? query.sort!, selected: true, onDeleted: () => controller.setSort(null)),
      if (query.dateFrom != null || query.dateTo != null)
        KurxChip(
          label: _rangeLabel(query.dateFrom, query.dateTo),
          selected: true,
          onDeleted: () => controller.setDateRange(null, null),
        ),
    ];

    return Wrap(spacing: KSpace.sm, runSpacing: KSpace.sm, children: chips);
  }

  String _rangeLabel(DateTime? from, DateTime? to) {
    final fmt = DateFormat('d MMM', 'en_IN');
    if (from != null && to != null) return '${fmt.format(from)} – ${fmt.format(to)}';
    if (from != null) return 'From ${fmt.format(from)}';
    return 'Until ${fmt.format(to!)}';
  }
}

/// Filters applied beyond the search text — the count shown on the Filters button. `q` is excluded
/// because it has its own always-visible field; a badge counting it would double-report it.
int _activeFilterCount(EventQuery q) => [
      q.city != null && q.city!.isNotEmpty,
      q.categoryId != null,
      q.kind != null,
      q.mode != null,
      q.price != null,
      q.sort != null,
      q.dateFrom != null || q.dateTo != null,
    ].where((applied) => applied).length;

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: KSpace.sm),
        child: Text(
          text,
          style: Theme.of(context)
              .textTheme
              .labelLarge
              ?.copyWith(color: context.kurx.muted, letterSpacing: 0.4),
        ),
      );
}

/// A labelled row of mutually exclusive choices. `null` is a real option — "Any mode" means no mode
/// restriction is sent at all, rather than a value the backend has to interpret.
class _ChipRow extends StatelessWidget {
  const _ChipRow({
    required this.label,
    required this.options,
    required this.selected,
    required this.onSelected,
  });

  final String label;
  final Map<String?, String> options;
  final String? selected;
  final ValueChanged<String?> onSelected;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: Theme.of(context).textTheme.bodySmall),
        const SizedBox(height: KSpace.xs),
        Wrap(
          spacing: KSpace.sm,
          runSpacing: KSpace.sm,
          children: options.entries
              .map((e) => ChoiceChip(
                    label: Text(e.value),
                    selected: selected == e.key,
                    onSelected: (_) => onSelected(e.key),
                  ))
              .toList(),
        ),
      ],
    );
  }
}

/// A value-controlled dropdown (mirrors query state, unlike a FormField's initialValue).
class _LabeledDropdown<T> extends StatelessWidget {
  const _LabeledDropdown({
    required this.label,
    required this.value,
    required this.items,
    required this.onChanged,
  });

  final String label;
  final T value;
  final List<DropdownMenuItem<T>> items;
  final ValueChanged<T?> onChanged;

  @override
  Widget build(BuildContext context) {
    return InputDecorator(
      decoration: InputDecoration(labelText: label, isDense: true, border: const OutlineInputBorder()),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<T>(
          value: value,
          isExpanded: true,
          items: items,
          onChanged: onChanged,
        ),
      ),
    );
  }
}

class _DisabledDropdown extends StatelessWidget {
  const _DisabledDropdown({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return InputDecorator(
      decoration: InputDecoration(labelText: label, isDense: true, border: const OutlineInputBorder()),
      child: Text(value),
    );
  }
}
