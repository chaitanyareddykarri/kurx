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
}
