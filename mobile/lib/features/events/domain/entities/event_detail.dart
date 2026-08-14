/// One renderable gallery image. `url` is presigned — the raw storage key is not fetchable (D-302).
class EventGalleryItem {
  const EventGalleryItem({required this.id, required this.url, this.caption});
  final String id;
  final String url;
  final String? caption;
}

/// The organization an event is run on behalf of. Null when the host represents only themselves
/// (D-268) — there is no institution to name, and clients must not invent one.
class EventRepresentation {
  const EventRepresentation({required this.orgId, required this.name, required this.slug, this.isVerified = false});
  final String orgId;
  final String name;
  final String slug;
  final bool isVerified;
}

/// Full public event detail (`GET /v1/events/{slug}`).
class EventDetail {
  const EventDetail({
    required this.id,
    required this.title,
    required this.slug,
    this.subtitle,
    this.description,
    this.tags = const [],
    this.venueName,
    this.address,
    this.city,
    this.googleMapsUrl,
    this.startsAt,
    this.endsAt,
    required this.status,
    this.viewCount = 0,
    this.bannerUrl,
    this.gallery = const [],
    this.eventMode,
    this.onlineUrl,
    this.representing,
  });

  final String id;
  final String title;
  final String slug;
  final String? subtitle;
  final String? description;
  final List<String> tags;
  final String? venueName;
  final String? address;
  final String? city;
  final String? googleMapsUrl;
  final DateTime? startsAt;
  final DateTime? endsAt;
  final String status;
  final int viewCount;

  /// D-302 — presigned banner, the gallery, the event mode, and the organization the host represents.
  /// All were returned by the API and none reached this entity, so no Flutter surface could show them.
  final String? bannerUrl;
  final List<EventGalleryItem> gallery;
  final String? eventMode;
  final String? onlineUrl;
  final EventRepresentation? representing;

  /// "City · venue", trimmed to whichever parts exist.
  String? get location {
    final parts = [city, venueName].where((p) => p != null && p.isNotEmpty).cast<String>();
    return parts.isEmpty ? null : parts.join(' · ');
  }
}
