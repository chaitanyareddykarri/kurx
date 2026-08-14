import 'event_category.dart';
import 'event_summary.dart';

/// Everything the discovery home shows, assembled by `GetHomeFeed`.
class HomeFeed {
  const HomeFeed({
    required this.featured,
    required this.trending,
    required this.upcoming,
    required this.latest,
    required this.categories,
  });

  final List<EventSummary> featured;
  final List<EventSummary> trending;
  final List<EventSummary> upcoming;
  final List<EventSummary> latest;
  final List<EventCategory> categories;

  bool get isEmpty =>
      featured.isEmpty && trending.isEmpty && upcoming.isEmpty && latest.isEmpty && categories.isEmpty;
}
