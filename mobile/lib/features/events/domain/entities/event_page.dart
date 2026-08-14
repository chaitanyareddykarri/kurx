import 'event_detail.dart';
import 'event_summary.dart';
import 'ticket_type.dart';

/// Everything the event detail screen shows, assembled by `GetEventPage`.
class EventPage {
  const EventPage({
    required this.detail,
    required this.ticketTypes,
    required this.related,
  });

  final EventDetail detail;
  final List<TicketType> ticketTypes;
  final List<EventSummary> related;
}
