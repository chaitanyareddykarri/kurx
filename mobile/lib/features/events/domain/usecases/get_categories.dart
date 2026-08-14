import '../entities/event_category.dart';
import '../repositories/events_repository.dart';

class GetCategories {
  const GetCategories(this._repo);
  final EventsRepository _repo;

  Future<List<EventCategory>> call() => _repo.categories();
}
