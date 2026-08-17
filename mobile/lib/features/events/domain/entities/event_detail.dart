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
    this.rules,
    this.codeOfConduct,
    this.refundPolicy,
    this.cancellationPolicy,
    this.termsUrl,
    this.consentText,
    this.registrationOpensAt,
    this.registrationClosesAt,
    this.checkinOpensAt,
    this.checkinClosesAt,
    this.minAge,
    this.maxAge,
    this.genderRestriction,
    this.maxTeams,
    this.building,
    this.floor,
    this.room,
    this.meetingPlatform,
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

  /*
   * The D-265 field groups an attendee needs before registering.
   *
   * Collected by the create-event wizard, stored, returned by this endpoint since D-265, and mapped by
   * no client — so the rules a registration is subject to were only ever legible after it. Flattened
   * onto the entity rather than kept as nested objects because the UI reads them one at a time and a
   * five-object entity would make every widget null-check twice.
   *
   * `consentText` is non-null ONLY when the event actually requires consent — the DTO drops it
   * otherwise, so a stale statement on an event that no longer asks for one cannot render.
   */
  final String? rules;
  final String? codeOfConduct;
  final String? refundPolicy;
  final String? cancellationPolicy;
  final String? termsUrl;
  final String? consentText;
  final DateTime? registrationOpensAt;
  final DateTime? registrationClosesAt;
  final DateTime? checkinOpensAt;
  final DateTime? checkinClosesAt;
  final int? minAge;
  final int? maxAge;
  /// Null when unrestricted — the DTO maps the server's "Any" default to null so it never reads as a rule.
  final String? genderRestriction;
  final int? maxTeams;
  final String? building;
  final String? floor;
  final String? room;
  final String? meetingPlatform;

  /// "City · venue", trimmed to whichever parts exist.
  String? get location {
    final parts = [city, venueName].where((p) => p != null && p.isNotEmpty).cast<String>();
    return parts.isEmpty ? null : parts.join(' · ');
  }

  /// Where inside the venue — "Block A · Floor 3 · Room 301", or null when none was set.
  String? get placeInVenue {
    final parts = [
      building,
      if (floor != null && floor!.isNotEmpty) 'Floor $floor',
      if (room != null && room!.isNotEmpty) 'Room $room',
    ].where((p) => p != null && p.isNotEmpty).cast<String>();
    return parts.isEmpty ? null : parts.join(' · ');
  }

  /// The age rule as one line, or null when there is none.
  String? get ageRule {
    if (minAge == null && maxAge == null) return null;
    if (minAge != null && maxAge != null) return '$minAge–$maxAge';
    return minAge != null ? '$minAge and over' : '$maxAge and under';
  }
}
