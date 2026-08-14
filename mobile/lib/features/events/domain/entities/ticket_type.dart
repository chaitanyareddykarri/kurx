/// A publicly-listed, on-sale ticket type (`GET /v1/events/{eventId}/ticket-types`).
class TicketType {
  const TicketType({
    required this.id,
    required this.name,
    required this.pricePaise,
    required this.available,
    required this.quantity,
    this.saleEnds,
    this.isAllAccess = false,
  });

  final String id;
  final String name;

  /// Price in paise (D-004); format with `Money.fromPaise` at the display edge.
  final int pricePaise;
  final int available;
  final int quantity;
  final DateTime? saleEnds;
  final bool isAllAccess;

  bool get soldOut => available <= 0;
  bool get isFree => pricePaise <= 0;
}
