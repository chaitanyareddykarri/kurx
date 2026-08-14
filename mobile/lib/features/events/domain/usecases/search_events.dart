import '../entities/event_query.dart';
import '../entities/paged_events.dart';
import '../repositories/events_repository.dart';

class SearchEvents {
  const SearchEvents(this._repo);
  final EventsRepository _repo;

  Future<PagedEvents> call(EventQuery query) => _repo.search(query);
}
