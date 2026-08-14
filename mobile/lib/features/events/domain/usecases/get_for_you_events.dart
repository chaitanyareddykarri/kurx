import '../entities/event_summary.dart';
import '../repositories/events_repository.dart';

/// V3 §15 (Phase 16) eligibility-aware recommended feed (D-184). Auth required — callers gate on the
/// signed-in state before watching this (see `home_dashboard_page.dart`'s `isGuest` check).
class GetForYouEvents {
  const GetForYouEvents(this._repo);
  final EventsRepository _repo;

  Future<List<EventSummary>> call({int limit = 10}) => _repo.forYou(limit: limit);
}
