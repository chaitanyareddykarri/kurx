/// A public event as returned in list/discovery responses (`/v1/events/*`).
class EventSummary {
  const EventSummary({
    required this.id,
    required this.title,
    required this.slug,
    this.subtitle,
    this.venueName,
    this.city,
    this.startsAt,
    required this.status,
    this.categoryName,
    this.priceFromPaise,
    this.priceFromUnit,
    this.isFeatured = false,
    this.bannerUrl,
    this.eventMode,
    this.currency = 'INR',
  });

  final String id;
  final String title;
  final String slug;
  final String? subtitle;
  final String? venueName;
  final String? city;
  final DateTime? startsAt;
  final String status;

  /// Optional display enrichments from discovery responses. These were declared here long before the API
  /// sent them and `toEntity()` never set them, so every one was permanently null — a card that looked
  /// price-aware and never was (D-302).
  final String? categoryName;
  final int? priceFromPaise;
  final bool isFeatured;

  /// Presigned banner. The raw `banner_key` is NOT fetchable, so this is the only renderable form.
  final String? bannerUrl;
  final String? eventMode;
  final String currency;

  /// Null price means no ticket type exists yet — NOT free. Callers must distinguish the two.
  /// D-361 — what the "From" price buys. Absent reads as per-participant, which is every
  /// pre-D-357 ticket type.
  final String? priceFromUnit;

  bool get isFree => priceFromPaise != null && priceFromPaise! <= 0;
  bool get hasPrice => priceFromPaise != null;

  /// True when the "From" price is a whole team's entry fee rather than one person's.
  bool get isPricedPerTeam => priceFromUnit == 'PerGroup';

  /// "City · venue", trimmed to whichever parts exist.
  String? get location {
    final parts = [city, venueName].where((p) => p != null && p.isNotEmpty).cast<String>();
    return parts.isEmpty ? null : parts.join(' · ');
  }
}
