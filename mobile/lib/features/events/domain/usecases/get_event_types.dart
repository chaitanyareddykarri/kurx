import '../entities/event_category.dart';
import '../repositories/events_repository.dart';

/// The taxonomy's TYPE level. Separate from [GetCategories] because the two answer different questions:
/// a category groups events for browsing, a type carries the archetype that decides what an event
/// supports. Creating an event without a type leaves it archetype-less.
class GetEventTypes {
  const GetEventTypes(this._repo);
  final EventsRepository _repo;

  Future<List<EventCategory>> call() => _repo.eventTypes();
}
