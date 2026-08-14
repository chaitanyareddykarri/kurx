import '../entities/event_kind.dart';
import '../repositories/events_repository.dart';

class GetKinds {
  const GetKinds(this._repo);
  final EventsRepository _repo;

  Future<List<EventKind>> call() => _repo.kinds();
}
