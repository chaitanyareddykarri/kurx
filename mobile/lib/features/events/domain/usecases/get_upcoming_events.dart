import '../entities/event_summary.dart';
import '../repositories/events_repository.dart';

class GetUpcomingEvents {
  const GetUpcomingEvents(this._repo);
  final EventsRepository _repo;

  Future<List<EventSummary>> call({int limit = 20}) => _repo.upcoming(limit: limit);
}
