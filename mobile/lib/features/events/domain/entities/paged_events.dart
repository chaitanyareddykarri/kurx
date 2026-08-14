import 'event_summary.dart';

/// One page of search results plus the full match count for pagination.
class PagedEvents {
  const PagedEvents({required this.items, required this.total});

  final List<EventSummary> items;
  final int total;
}
