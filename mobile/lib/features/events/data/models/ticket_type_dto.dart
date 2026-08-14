import 'package:freezed_annotation/freezed_annotation.dart';

import '../../domain/entities/ticket_type.dart';

part 'ticket_type_dto.freezed.dart';
part 'ticket_type_dto.g.dart';

/// Mirrors the snake_case ticket-type JSON (`TicketTypeEndpoints.ToJson`).
///
/// **One DTO for both routes.** The public read (`GET /v1/events/{id}/ticket-types`) and the
/// organiser read (`GET /v1/orgs/{o}/events/{e}/ticket-types`) go through the same `ToJson`, so
/// they return the same shape — the organiser fields below were added here rather than in a second
/// organiser-only model. They are optional purely because the attendee screens never read them.
@freezed
class TicketTypeDto with _$TicketTypeDto {
  const TicketTypeDto._();

  const factory TicketTypeDto({
    required String id,
    required String name,
    @JsonKey(name: 'price_paise') @Default(0) int pricePaise,
    @Default(0) int quantity,
    @Default(0) int sold,
    @Default(0) int available,
    @JsonKey(name: 'sale_ends') DateTime? saleEnds,
    @JsonKey(name: 'is_all_access') @Default(false) bool isAllAccess,
    // ── Organiser-only: present on every response, read only by the manage screens. ──
    @JsonKey(name: 'event_id') String? eventId,
    /// `per_ticket` or `per_group`.
    @JsonKey(name: 'pricing_unit') String? pricingUnit,
    /// `individual` or `group`.
    @JsonKey(name: 'registration_mode') String? registrationMode,
    @JsonKey(name: 'group_min') int? groupMin,
    @JsonKey(name: 'group_max') int? groupMax,
    @JsonKey(name: 'sale_starts') DateTime? saleStarts,
    @JsonKey(name: 'per_user_limit') int? perUserLimit,
    @JsonKey(name: 'is_competition') @Default(false) bool isCompetition,
  }) = _TicketTypeDto;

  factory TicketTypeDto.fromJson(Map<String, dynamic> json) =>
      _$TicketTypeDtoFromJson(json);

  TicketType toEntity() => TicketType(
        id: id,
        name: name,
        pricePaise: pricePaise,
        available: available,
        quantity: quantity,
        saleEnds: saleEnds,
        isAllAccess: isAllAccess,
      );
}
