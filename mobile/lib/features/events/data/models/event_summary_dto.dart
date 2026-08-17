import 'package:freezed_annotation/freezed_annotation.dart';

import '../../domain/entities/event_summary.dart';

part 'event_summary_dto.freezed.dart';
part 'event_summary_dto.g.dart';

/// Mirrors the snake_case event-summary JSON (`EventEndpoints.ToSummaryJson`).
@freezed
class EventSummaryDto with _$EventSummaryDto {
  const EventSummaryDto._();

  const factory EventSummaryDto({
    required String id,
    required String title,
    required String slug,
    String? subtitle,
    @JsonKey(name: 'venue_name') String? venueName,
    String? city,
    @JsonKey(name: 'starts_at') DateTime? startsAt,
    @JsonKey(name: 'ends_at') DateTime? endsAt,
    @Default('') String status,
    // D-302 — the card facts the API now sends. `banner_url` is presigned; `banner_key` is not fetchable.
    @JsonKey(name: 'banner_url') String? bannerUrl,
    @JsonKey(name: 'event_mode') String? eventMode,
    @JsonKey(name: 'category_name') String? categoryName,
    @JsonKey(name: 'price_from_paise') int? priceFromPaise,
    // D-361 — the unit the "From" price is charged in.
    @JsonKey(name: 'price_from_unit') String? priceFromUnit,
    String? currency,
    @JsonKey(name: 'is_featured') bool? isFeatured,
  }) = _EventSummaryDto;

  factory EventSummaryDto.fromJson(Map<String, dynamic> json) =>
      _$EventSummaryDtoFromJson(json);

  EventSummary toEntity() => EventSummary(
        id: id,
        title: title,
        slug: slug,
        subtitle: subtitle,
        venueName: venueName,
        city: city,
        startsAt: startsAt,
        status: status,
        bannerUrl: bannerUrl,
        eventMode: eventMode,
        categoryName: categoryName,
        priceFromPaise: priceFromPaise,
        priceFromUnit: priceFromUnit,
        currency: currency ?? 'INR',
        isFeatured: isFeatured ?? false,
      );
}
