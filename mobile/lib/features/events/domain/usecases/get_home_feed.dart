import '../entities/event_category.dart';
import '../entities/event_summary.dart';
import '../entities/home_feed.dart';
import '../repositories/events_repository.dart';

/// Loads all discovery sections + categories concurrently for the home dashboard.
class GetHomeFeed {
  const GetHomeFeed(this._repo);
  final EventsRepository _repo;

  Future<HomeFeed> call({int limit = 10}) async {
    // Future.wait attaches to every future, so a failure in one never leaves the
    // others as orphaned (unhandled) errors; it rethrows the first failure once all settle.
    final results = await Future.wait<Object>([
      _repo.featured(limit: limit),
      _repo.trending(limit: limit),
      _repo.upcoming(limit: limit),
      _repo.latest(limit: limit),
      _repo.categories(),
    ]);
    return HomeFeed(
      featured: results[0] as List<EventSummary>,
      trending: results[1] as List<EventSummary>,
      upcoming: results[2] as List<EventSummary>,
      latest: results[3] as List<EventSummary>,
      categories: results[4] as List<EventCategory>,
    );
  }
}
