import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/events/data/models/event_detail_dto.dart';
import 'package:kurx_mobile/features/events/data/models/ticket_type_dto.dart';

void main() {
  test('EventDetailDto maps nested venue, tags and description', () {
    final dto = EventDetailDto.fromJson({
      'id': 'cc341dd1',
      'title': 'Sunburn Arena — Live in Mumbai',
      'slug': 'sunburn-arena-live-in-mumbai-68cf',
      'subtitle': 'An unforgettable night of music',
      'description': 'Sunburn returns to Mumbai.',
      'tags': ['edm', 'festival'],
      'venue': {
        'venue_id': null,
        'name': 'NSCI Dome',
        'address': 'Worli',
        'city': 'Mumbai',
        'lat': null,
        'lng': null,
        'google_maps_url': 'https://maps.example/nsci',
      },
      'starts_at': '2026-07-19T21:06:16.249218Z',
      'ends_at': '2026-07-20T02:06:16.269221Z',
      'status': 'published',
      'view_count': 3,
    });

    final e = dto.toEntity();
    expect(e.title, 'Sunburn Arena — Live in Mumbai');
    expect(e.description, 'Sunburn returns to Mumbai.');
    expect(e.tags, ['edm', 'festival']);
    expect(e.venueName, 'NSCI Dome');
    expect(e.address, 'Worli');
    expect(e.location, 'Mumbai · NSCI Dome');
    expect(e.googleMapsUrl, 'https://maps.example/nsci');
    expect(e.viewCount, 3);
  });

  test('EventDetailDto tolerates a missing venue and empty tags', () {
    final dto = EventDetailDto.fromJson({
      'id': 'x',
      'title': 'Online Talk',
      'slug': 'online-talk',
      'status': 'published',
    });
    final e = dto.toEntity();
    expect(e.venueName, isNull);
    expect(e.location, isNull);
    expect(e.tags, isEmpty);
  });

  test('TicketTypeDto maps price_paise and availability', () {
    final dto = TicketTypeDto.fromJson({
      'id': 't1',
      'event_id': 'cc341dd1',
      'name': 'General Admission',
      'price_paise': 150000,
      'pricing_unit': 'per_ticket',
      'registration_mode': 'individual',
      'quantity': 100,
      'sold': 10,
      'available': 90,
      'sale_ends': '2026-07-18T00:00:00Z',
      'per_user_limit': 4,
      'is_all_access': false,
    });

    final t = dto.toEntity();
    expect(t.name, 'General Admission');
    expect(t.pricePaise, 150000);
    expect(t.available, 90);
    expect(t.soldOut, isFalse);
    expect(t.isFree, isFalse);
  });

  /*
   * The D-265 field groups.
   *
   * They have been on the wire since D-265 and this DTO mapped none of them, so an event's rules,
   * terms, consent statement, eligibility gates, registration windows and in-building location were
   * fetched by the app and thrown away. Mirrors `web/lib/api.ts`'s `eventDetailSchema` field for field.
   */
  const groups = {
    'id': 'e1',
    'title': 'Lifecycle Audit Summit',
    'slug': 'lifecycle-audit-summit',
    'status': 'published',
    'content': {'tagline': 'Proven', 'short_description': 'Blurb', 'rules': 'Bring a laptop.'},
    'legal': {
      'terms_url': 'https://audit.test/terms',
      'terms_text': null,
      'code_of_conduct': 'Be kind.',
      'refund_policy': 'Full refund 7 days prior.',
      'cancellation_policy': '48h notice.',
      'requires_consent': true,
      'consent_text': 'I accept the code of conduct.',
    },
    'schedule': {
      'registration_opens_at': '2026-09-01T09:00:00Z',
      'registration_closes_at': '2026-09-10T09:00:00Z',
      'checkin_opens_at': '2026-09-14T08:00:00Z',
      'checkin_closes_at': '2026-09-14T12:00:00Z',
    },
    'eligibility': {
      'min_age': 18,
      'max_age': 60,
      'gender_restriction': 'Any',
      'max_teams': 40,
    },
    'location_detail': {
      'building': 'Block A',
      'floor': '3',
      'room': '301',
      'google_maps_url': 'https://maps.example/event',
      'meeting_platform': 'Zoom',
    },
  };

  test('EventDetailDto maps every D-265 field group onto the entity', () {
    final e = EventDetailDto.fromJson(Map<String, dynamic>.from(groups)).toEntity();

    expect(e.rules, 'Bring a laptop.');
    expect(e.codeOfConduct, 'Be kind.');
    expect(e.refundPolicy, 'Full refund 7 days prior.');
    expect(e.cancellationPolicy, '48h notice.');
    expect(e.termsUrl, 'https://audit.test/terms');
    expect(e.consentText, 'I accept the code of conduct.');
    expect(e.registrationOpensAt, isNotNull);
    expect(e.checkinClosesAt, isNotNull);
    expect(e.minAge, 18);
    expect(e.maxAge, 60);
    expect(e.maxTeams, 40);
    expect(e.building, 'Block A');
    expect(e.meetingPlatform, 'Zoom');
  });

  test('"Any" is no restriction, so it never reaches the UI as one', () {
    // The server's default. Rendering it would put "Open to: Any" on a page as though it were a rule.
    final e = EventDetailDto.fromJson(Map<String, dynamic>.from(groups)).toEntity();
    expect(e.genderRestriction, isNull);

    final restricted = Map<String, dynamic>.from(groups)
      ..['eligibility'] = {'gender_restriction': 'Female'};
    expect(EventDetailDto.fromJson(restricted).toEntity().genderRestriction, 'Female');
  });

  test('the consent statement is dropped when the event does not require consent', () {
    // Otherwise a statement left behind by a since-disabled switch would render as binding.
    final noConsent = Map<String, dynamic>.from(groups)
      ..['legal'] = {'requires_consent': false, 'consent_text': 'stale wording'};
    expect(EventDetailDto.fromJson(noConsent).toEntity().consentText, isNull);
  });

  test("the event's own map link wins over the venue library record's", () {
    final e = EventDetailDto.fromJson(Map<String, dynamic>.from(groups)
      ..['venue'] = {'name': 'Hall', 'google_maps_url': 'https://maps.example/venue'}).toEntity();
    expect(e.googleMapsUrl, 'https://maps.example/event');
  });

  test('a response with no groups at all still parses — they post-date the endpoint', () {
    // The D-292 lesson: a required nested object here would 500 every event created before D-265.
    final e = EventDetailDto.fromJson({
      'id': 'e2', 'title': 'Bare', 'slug': 'bare', 'status': 'published',
    }).toEntity();
    expect(e.rules, isNull);
    expect(e.ageRule, isNull);
    expect(e.placeInVenue, isNull);
  });

  test('derived one-liners read the way a page needs them', () {
    final e = EventDetailDto.fromJson(Map<String, dynamic>.from(groups)).toEntity();
    expect(e.ageRule, '18–60');
    expect(e.placeInVenue, 'Block A · Floor 3 · Room 301');

    final minOnly = EventDetailDto.fromJson(Map<String, dynamic>.from(groups)
      ..['eligibility'] = {'min_age': 18}).toEntity();
    expect(minOnly.ageRule, '18 and over');

    final maxOnly = EventDetailDto.fromJson(Map<String, dynamic>.from(groups)
      ..['eligibility'] = {'max_age': 12}).toEntity();
    expect(maxOnly.ageRule, '12 and under');
  });
}
