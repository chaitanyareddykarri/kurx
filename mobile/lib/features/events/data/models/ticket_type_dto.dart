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
    /// D-366 — team-size price bands. Absent (not `[]`) on a ticket priced by one amount, which is
    /// every ticket that predates the decision.
    @JsonKey(name: 'price_tiers') List<TicketPriceTierDto>? priceTiers,
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
        // D-372 — the DTO carried these four all along and the entity dropped them, so the app could
        // only ever show an amount with no unit.
        pricingUnit: pricingUnit,
        registrationMode: registrationMode,
        groupMin: groupMin,
        groupMax: groupMax,
        priceTiers: priceTiers
                ?.map((t) => TicketPriceTier(
                      minSize: t.minSize, maxSize: t.maxSize, pricePaise: t.pricePaise))
                .toList() ??
            const [],
      );
}

/// D-366 — one band on the wire.
@freezed
abstract class TicketPriceTierDto with _$TicketPriceTierDto {
  const factory TicketPriceTierDto({
    @JsonKey(name: 'min_size') @Default(0) int minSize,
    @JsonKey(name: 'max_size') @Default(0) int maxSize,
    @JsonKey(name: 'price_paise') @Default(0) int pricePaise,
  }) = _TicketPriceTierDto;

  factory TicketPriceTierDto.fromJson(Map<String, dynamic> json) =>
      _$TicketPriceTierDtoFromJson(json);
}
