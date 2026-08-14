import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/events/data/models/event_summary_dto.dart';

void main() {
  test('EventSummaryDto maps snake_case summary JSON and builds a location', () {
    final dto = EventSummaryDto.fromJson({
      'id': 'cc341dd1-79a5-4f5c-b5e7-3f24af20c571',
      'org_id': '9c193c97-c880-4769-a852-1b64d64f5e23',
      'parent_event_id': null,
      'title': 'Sunburn Arena — Live in Mumbai',
      'slug': 'sunburn-arena-live-in-mumbai-68cf',
      'subtitle': 'An unforgettable night of music',
      'banner_key': null,
      'starts_at': '2026-07-19T21:06:16.249218Z',
      'ends_at': '2026-07-20T02:06:16.269221Z',
      'status': 'published',
      'visibility': 'public',
      'venue_name': 'NSCI Dome',
      'city': 'Mumbai',
    });

    final e = dto.toEntity();
    expect(e.title, 'Sunburn Arena — Live in Mumbai');
    expect(e.status, 'published');
    expect(e.venueName, 'NSCI Dome');
    expect(e.city, 'Mumbai');
    expect(e.location, 'Mumbai · NSCI Dome');
    expect(e.startsAt, DateTime.parse('2026-07-19T21:06:16.249218Z'));
  });

  test('location omits missing parts', () {
    final dto = EventSummaryDto.fromJson({
      'id': 'x',
      'title': 'Online Meetup',
      'slug': 'online-meetup',
      'status': 'published',
    });
    expect(dto.toEntity().location, isNull);
  });
}
