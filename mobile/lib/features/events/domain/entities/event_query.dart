const Object _unset = Object();

/// Immutable search/filter state for `GET /v1/events`. `sort == null` uses the
/// backend default (soonest first); valid non-null sorts are `date_desc`, `newest`,
/// `popular` (source: `EventService.SearchAsync`). `city` is an exact, case-insensitive match.
class EventQuery {
  const EventQuery({
    this.q = '',
    this.city,
    this.categoryId,
    this.dateFrom,
    this.dateTo,
    this.sort,
    this.kind,
    this.mode,
    this.price,
    this.page = 1,
    this.pageSize = 20,
  });

  final String q;
  final String? city;
  final String? categoryId;
  final DateTime? dateFrom;
  final DateTime? dateTo;
  final String? sort;
  // V3 §15 (Phase 16) additive discovery filters (D-184): kind = event_kinds slug, mode = offline|online|hybrid,
  // price = free|paid.
  final String? kind;
  final String? mode;
  final String? price;
  final int page;
  final int pageSize;

  bool get hasActiveFilters =>
      q.isNotEmpty ||
      (city != null && city!.isNotEmpty) ||
      categoryId != null ||
      dateFrom != null ||
      dateTo != null ||
      sort != null ||
      kind != null ||
      mode != null ||
      price != null;

  /// Query parameters for Dio — omits empty/absent filters so we never send blanks.
  Map<String, dynamic> toQueryParameters() => {
        if (q.trim().isNotEmpty) 'q': q.trim(),
        if (city != null && city!.trim().isNotEmpty) 'city': city!.trim(),
        if (categoryId != null) 'categoryId': categoryId,
        if (dateFrom != null) 'dateFrom': dateFrom!.toUtc().toIso8601String(),
        if (dateTo != null) 'dateTo': dateTo!.toUtc().toIso8601String(),
        if (sort != null) 'sort': sort,
        if (kind != null) 'kind': kind,
        if (mode != null) 'mode': mode,
        if (price != null) 'price': price,
        'page': page,
        'pageSize': pageSize,
      };

  EventQuery copyWith({
    Object? q = _unset,
    Object? city = _unset,
    Object? categoryId = _unset,
    Object? dateFrom = _unset,
    Object? dateTo = _unset,
    Object? sort = _unset,
    Object? kind = _unset,
    Object? mode = _unset,
    Object? price = _unset,
    int? page,
    int? pageSize,
  }) =>
      EventQuery(
        q: identical(q, _unset) ? this.q : q as String,
        city: identical(city, _unset) ? this.city : city as String?,
        categoryId: identical(categoryId, _unset) ? this.categoryId : categoryId as String?,
        dateFrom: identical(dateFrom, _unset) ? this.dateFrom : dateFrom as DateTime?,
        dateTo: identical(dateTo, _unset) ? this.dateTo : dateTo as DateTime?,
        sort: identical(sort, _unset) ? this.sort : sort as String?,
        kind: identical(kind, _unset) ? this.kind : kind as String?,
        mode: identical(mode, _unset) ? this.mode : mode as String?,
        price: identical(price, _unset) ? this.price : price as String?,
        page: page ?? this.page,
        pageSize: pageSize ?? this.pageSize,
      );
}
