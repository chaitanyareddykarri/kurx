import '../entities/event_page.dart';
import '../repositories/events_repository.dart';

/// Loads the full event detail page: the event by slug, then its public ticket
/// types (by the resolved id) and related events, fetched concurrently.
class GetEventPage {
  const GetEventPage(this._repo);
  final EventsRepository _repo;

  Future<EventPage> call(String slug) async {
    final detail = await _repo.detail(slug);
    final ticketsFuture = _repo.ticketTypes(detail.id);
    final relatedFuture = _repo.related(slug, limit: 6);
    return EventPage(
      detail: detail,
      ticketTypes: await ticketsFuture,
      related: await relatedFuture,
    );
  }
}
