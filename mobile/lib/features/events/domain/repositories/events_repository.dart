import '../entities/event_category.dart';
import '../entities/event_detail.dart';
import '../entities/event_kind.dart';
import '../entities/event_query.dart';
import '../entities/event_summary.dart';
import '../entities/paged_events.dart';
import '../entities/ticket_type.dart';

/// Read access to the public event-discovery surface (`/v1/events/*`, `/v1/categories`, `/v1/kinds`).
abstract interface class EventsRepository {
  /// `GET /v1/events/upcoming` — soonest published, public events.
  Future<List<EventSummary>> upcoming({int limit});

  /// `GET /v1/events/featured` — editorially featured events.
  Future<List<EventSummary>> featured({int limit});

  /// `GET /v1/events/trending` — most-viewed events.
  Future<List<EventSummary>> trending({int limit});

  /// `GET /v1/events/latest` — most recently created events.
  Future<List<EventSummary>> latest({int limit});

  /// `GET /v1/events` — paged search/filter over published, public events.
  Future<PagedEvents> search(EventQuery query);

  /// `GET /v1/categories` — visible discovery categories, for the search filter.
  Future<List<EventCategory>> categories();

  /// The TYPE level of the taxonomy — the children of a category. A type carries the archetype that
  /// decides what an event supports, so an event created without one has no archetype at all.
  Future<List<EventCategory>> eventTypes();

  /// `GET /v1/kinds` — the V3 Kind registry (Phase 16), for the Kind filter/quick-browse rail.
  Future<List<EventKind>> kinds();

  /// `GET /v1/events/for-you` — eligibility-aware recommended feed (Phase 16, D-184). Auth required.
  Future<List<EventSummary>> forYou({int limit});

  /// `GET /v1/events/{slug}` — full detail (increments the server view counter).
  Future<EventDetail> detail(String slug);

  /// `GET /v1/events/{slug}/related` — same-category events, excluding this one.
  Future<List<EventSummary>> related(String slug, {int limit});

  /// `GET /v1/events/{eventId}/ticket-types` — public, on-sale types only.
  Future<List<TicketType>> ticketTypes(String eventId);
}
