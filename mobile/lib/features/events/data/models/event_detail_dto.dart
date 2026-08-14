import 'package:freezed_annotation/freezed_annotation.dart';

import '../../domain/entities/event_detail.dart';

part 'event_detail_dto.freezed.dart';
part 'event_detail_dto.g.dart';

/// The `venue` object nested in the event-detail response.
@freezed
class VenueDto with _$VenueDto {
  const factory VenueDto({
    String? name,
    String? address,
    String? city,
    @JsonKey(name: 'google_maps_url') String? googleMapsUrl,
  }) = _VenueDto;

  factory VenueDto.fromJson(Map<String, dynamic> json) => _$VenueDtoFromJson(json);
}

/// Mirrors the snake_case `GET /v1/events/{slug}` body (`EventEndpoints.ToEventJson`).
@freezed
class EventDetailDto with _$EventDetailDto {
  const EventDetailDto._();

  const factory EventDetailDto({
    required String id,
    required String title,
    required String slug,
    String? subtitle,
    String? description,
    @Default(<String>[]) List<String> tags,
    VenueDto? venue,
    @JsonKey(name: 'starts_at') DateTime? startsAt,
    @JsonKey(name: 'ends_at') DateTime? endsAt,
    @Default('') String status,
    @JsonKey(name: 'view_count') @Default(0) int viewCount,
    @JsonKey(name: 'banner_url') String? bannerUrl,
    @JsonKey(name: 'event_mode') String? eventMode,
    @JsonKey(name: 'online_url') String? onlineUrl,
    @Default(<EventMediaDto>[]) List<EventMediaDto> media,
    EventRepresentationDto? representing,
  }) = _EventDetailDto;

  factory EventDetailDto.fromJson(Map<String, dynamic> json) =>
      _$EventDetailDtoFromJson(json);

  EventDetail toEntity() => EventDetail(
        id: id,
        title: title,
        slug: slug,
        subtitle: subtitle,
        description: description,
        tags: tags,
        venueName: venue?.name,
        address: venue?.address,
        city: venue?.city,
        googleMapsUrl: venue?.googleMapsUrl,
        startsAt: startsAt,
        endsAt: endsAt,
        status: status,
        viewCount: viewCount,
        bannerUrl: bannerUrl,
        eventMode: eventMode,
        onlineUrl: onlineUrl,
        // Only media with a presigned url is renderable; a key alone is not.
        gallery: media
            .where((m) => (m.url ?? '').isNotEmpty)
            .map((m) => EventGalleryItem(id: m.id, url: m.url!, caption: m.caption))
            .toList(),
        representing: representing == null
            ? null
            : EventRepresentation(
                orgId: representing!.orgId,
                name: representing!.name,
                slug: representing!.slug,
                isVerified: representing!.isVerified,
              ),
      );
}

@freezed
class EventMediaDto with _$EventMediaDto {
  const factory EventMediaDto({
    @Default('') String id,
    @Default('') String kind,
    String? caption,
    String? url,
  }) = _EventMediaDto;
  factory EventMediaDto.fromJson(Map<String, dynamic> json) => _$EventMediaDtoFromJson(json);
}

@freezed
class EventRepresentationDto with _$EventRepresentationDto {
  const factory EventRepresentationDto({
    @JsonKey(name: 'org_id') @Default('') String orgId,
    @Default('') String name,
    @Default('') String slug,
    @JsonKey(name: 'is_verified') @Default(false) bool isVerified,
  }) = _EventRepresentationDto;
  factory EventRepresentationDto.fromJson(Map<String, dynamic> json) => _$EventRepresentationDtoFromJson(json);
}
