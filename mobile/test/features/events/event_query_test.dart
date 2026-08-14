import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_query.dart';

void main() {
  group('EventQuery.toQueryParameters', () {
    test('omits empty filters, always sends paging', () {
      final params = const EventQuery().toQueryParameters();
      expect(params.containsKey('q'), isFalse);
      expect(params.containsKey('city'), isFalse);
      expect(params.containsKey('categoryId'), isFalse);
      expect(params.containsKey('sort'), isFalse);
      expect(params['page'], 1);
      expect(params['pageSize'], 20);
    });

    test('includes set filters and serializes dates as UTC ISO-8601', () {
      final params = EventQuery(
        q: 'sunburn',
        city: 'Mumbai',
        categoryId: 'cat-1',
        sort: 'popular',
        dateFrom: DateTime.utc(2026, 7, 1),
        dateTo: DateTime.utc(2026, 7, 31),
        page: 2,
      ).toQueryParameters();

      expect(params['q'], 'sunburn');
      expect(params['city'], 'Mumbai');
      expect(params['categoryId'], 'cat-1');
      expect(params['sort'], 'popular');
      expect(params['dateFrom'], '2026-07-01T00:00:00.000Z');
      expect(params['dateTo'], '2026-07-31T00:00:00.000Z');
      expect(params['page'], 2);
    });
  });

  group('EventQuery.copyWith', () {
    test('keeps unspecified fields and can clear nullables', () {
      const base = EventQuery(q: 'x', city: 'Pune', categoryId: 'c1');
      final kept = base.copyWith(q: 'y');
      expect(kept.city, 'Pune');
      expect(kept.categoryId, 'c1');
      expect(kept.q, 'y');

      final cleared = base.copyWith(city: null, categoryId: null);
      expect(cleared.city, isNull);
      expect(cleared.categoryId, isNull);
      expect(cleared.q, 'x');
    });
  });
}
