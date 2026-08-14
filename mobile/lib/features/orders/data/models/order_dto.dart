import 'package:freezed_annotation/freezed_annotation.dart';

part 'order_dto.freezed.dart';
part 'order_dto.g.dart';

@freezed
class TicketDto with _$TicketDto {
  const factory TicketDto({
    required String id,
    required String code,
    required String state,
    @JsonKey(name: 'checked_in_at') DateTime? checkedInAt,
    @JsonKey(name: 'answers_json') String? answersJson,
    @JsonKey(name: 'created_at') required DateTime createdAt,
  }) = _TicketDto;

  factory TicketDto.fromJson(Map<String, dynamic> json) =>
      _$TicketDtoFromJson(json);
}

@freezed
class OrderDto with _$OrderDto {
  const factory OrderDto({
    required String id,
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'ticket_type_id') required String ticketTypeId,
    required String status,
    @JsonKey(name: 'amount_paise') required int amountPaise,
    // ISO-4217, the event's settlement currency (V3 §9.1, D-257). Defaulted rather than required so an
    // app build newer than its server still parses — the server only began sending this alongside D-245.
    @Default('INR') String currency,
    @JsonKey(name: 'razorpay_order_id') String? razorpayOrderId,
    @JsonKey(name: 'group_id') String? groupId,
    @JsonKey(name: 'join_code') String? joinCode,
    @JsonKey(name: 'created_at') required DateTime createdAt,
    @Default([]) List<TicketDto> tickets,
    @JsonKey(name: 'guest_access_token') String? guestAccessToken,
  }) = _OrderDto;

  factory OrderDto.fromJson(Map<String, dynamic> json) =>
      _$OrderDtoFromJson(json);
}

@freezed
class GroupMemberDto with _$GroupMemberDto {
  const factory GroupMemberDto({
    required String id,
    @JsonKey(name: 'user_id') String? userId,
    required String name,
    required String phone,
    @JsonKey(name: 'ticket_id') String? ticketId,
    @JsonKey(name: 'answers_json') String? answersJson,
    @JsonKey(name: 'joined_at') DateTime? joinedAt,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
  }) = _GroupMemberDto;

  factory GroupMemberDto.fromJson(Map<String, dynamic> json) =>
      _$GroupMemberDtoFromJson(json);
}

@freezed
class GroupDto with _$GroupDto {
  const factory GroupDto({
    required String id,
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'ticket_type_id') required String ticketTypeId,
    @JsonKey(name: 'group_number') required int groupNumber,
    @JsonKey(name: 'display_name') String? displayName,
    @JsonKey(name: 'join_code') required String joinCode,
    @JsonKey(name: 'leader_user_id') required String leaderUserId,
    required int capacity,
    @Default([]) List<GroupMemberDto> members,
  }) = _GroupDto;

  factory GroupDto.fromJson(Map<String, dynamic> json) =>
      _$GroupDtoFromJson(json);
}

@freezed
class TransferDto with _$TransferDto {
  const factory TransferDto({
    required String id,
    @JsonKey(name: 'ticket_id') required String ticketId,
    @JsonKey(name: 'to_phone') required String toPhone,
    @JsonKey(name: 'transfer_code') required String transferCode,
    required String status,
    @JsonKey(name: 'expires_at') required DateTime expiresAt,
    @JsonKey(name: 'created_at') required DateTime createdAt,
  }) = _TransferDto;

  factory TransferDto.fromJson(Map<String, dynamic> json) =>
      _$TransferDtoFromJson(json);
}
