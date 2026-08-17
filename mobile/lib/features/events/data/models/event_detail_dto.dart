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

/*
 * The D-265 field groups.
 *
 * `EventLegalView` / `EventScheduleView` / `EventEligibilityView` / `EventLocationDetailView` /
 * `EventContentView` have been on the wire since D-265 and were mapped by no client at all — so an
 * organiser could set an 18+ rule, a registration deadline, a refund policy and a binding consent
 * statement, and neither web nor Flutter showed any of it to the person deciding whether to register.
 *
 * Every field is nullable and every group is optional: they post-date the endpoint, so an older or
 * partial response must still parse (the D-292 lesson). `meeting_password` is deliberately absent —
 * it is shown only to confirmed registrants. `commerce` is absent too: platform fee and tax are
 * organiser accounting, never attendee-facing.
 */
@freezed
class EventContentDto with _$EventContentDto {
  const factory EventContentDto({
    String? tagline,
    @JsonKey(name: 'short_description') String? shortDescription,
    String? rules,
  }) = _EventContentDto;
  factory EventContentDto.fromJson(Map<String, dynamic> json) => _$EventContentDtoFromJson(json);
}

@freezed
class EventLegalDto with _$EventLegalDto {
  const factory EventLegalDto({
    @JsonKey(name: 'terms_url') String? termsUrl,
    @JsonKey(name: 'terms_text') String? termsText,
    @JsonKey(name: 'code_of_conduct') String? codeOfConduct,
    @JsonKey(name: 'refund_policy') String? refundPolicy,
    @JsonKey(name: 'cancellation_policy') String? cancellationPolicy,
    @JsonKey(name: 'requires_consent') @Default(false) bool requiresConsent,
    @JsonKey(name: 'consent_text') String? consentText,
  }) = _EventLegalDto;
  factory EventLegalDto.fromJson(Map<String, dynamic> json) => _$EventLegalDtoFromJson(json);
}

@freezed
class EventScheduleDto with _$EventScheduleDto {
  const factory EventScheduleDto({
    @JsonKey(name: 'registration_opens_at') DateTime? registrationOpensAt,
    @JsonKey(name: 'registration_closes_at') DateTime? registrationClosesAt,
    @JsonKey(name: 'checkin_opens_at') DateTime? checkinOpensAt,
    @JsonKey(name: 'checkin_closes_at') DateTime? checkinClosesAt,
  }) = _EventScheduleDto;
  factory EventScheduleDto.fromJson(Map<String, dynamic> json) => _$EventScheduleDtoFromJson(json);
}

@freezed
class EventEligibilityDto with _$EventEligibilityDto {
  const factory EventEligibilityDto({
    @JsonKey(name: 'min_age') int? minAge,
    @JsonKey(name: 'max_age') int? maxAge,
    @JsonKey(name: 'gender_restriction') String? genderRestriction,
    @JsonKey(name: 'max_teams') int? maxTeams,
  }) = _EventEligibilityDto;
  factory EventEligibilityDto.fromJson(Map<String, dynamic> json) => _$EventEligibilityDtoFromJson(json);
}

@freezed
class EventLocationDetailDto with _$EventLocationDetailDto {
  const factory EventLocationDetailDto({
    String? building,
    String? floor,
    String? room,
    @JsonKey(name: 'google_maps_url') String? googleMapsUrl,
    @JsonKey(name: 'meeting_platform') String? meetingPlatform,
  }) = _EventLocationDetailDto;
  factory EventLocationDetailDto.fromJson(Map<String, dynamic> json) =>
      _$EventLocationDetailDtoFromJson(json);
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
    EventContentDto? content,
    EventLegalDto? legal,
    EventScheduleDto? schedule,
    EventEligibilityDto? eligibility,
    @JsonKey(name: 'location_detail') EventLocationDetailDto? locationDetail,
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
        // The event's own `location_detail.google_maps_url` wins over the venue's: it is the link the
        // organiser set for THIS event, and the venue's is the library record's default.
        googleMapsUrl: locationDetail?.googleMapsUrl ?? venue?.googleMapsUrl,
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
        rules: content?.rules,
        codeOfConduct: legal?.codeOfConduct,
        refundPolicy: legal?.refundPolicy,
        cancellationPolicy: legal?.cancellationPolicy,
        termsUrl: legal?.termsUrl,
        consentText: (legal?.requiresConsent ?? false) ? legal?.consentText : null,
        registrationOpensAt: schedule?.registrationOpensAt,
        registrationClosesAt: schedule?.registrationClosesAt,
        checkinOpensAt: schedule?.checkinOpensAt,
        checkinClosesAt: schedule?.checkinClosesAt,
        minAge: eligibility?.minAge,
        maxAge: eligibility?.maxAge,
        // "Any" is the server default for no restriction; carrying it forward would render as a rule.
        genderRestriction: eligibility?.genderRestriction == 'Any' ? null : eligibility?.genderRestriction,
        maxTeams: eligibility?.maxTeams,
        building: locationDetail?.building,
        floor: locationDetail?.floor,
        room: locationDetail?.room,
        meetingPlatform: locationDetail?.meetingPlatform,
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
