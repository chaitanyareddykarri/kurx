// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'order_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$TicketDtoImpl _$$TicketDtoImplFromJson(Map<String, dynamic> json) =>
    _$TicketDtoImpl(
      id: json['id'] as String,
      code: json['code'] as String,
      state: json['state'] as String,
      checkedInAt: json['checked_in_at'] == null
          ? null
          : DateTime.parse(json['checked_in_at'] as String),
      answersJson: json['answers_json'] as String?,
      createdAt: DateTime.parse(json['created_at'] as String),
    );

Map<String, dynamic> _$$TicketDtoImplToJson(_$TicketDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'code': instance.code,
      'state': instance.state,
      'checked_in_at': instance.checkedInAt?.toIso8601String(),
      'answers_json': instance.answersJson,
      'created_at': instance.createdAt.toIso8601String(),
    };

_$OrderDtoImpl _$$OrderDtoImplFromJson(Map<String, dynamic> json) =>
    _$OrderDtoImpl(
      id: json['id'] as String,
      eventId: json['event_id'] as String,
      ticketTypeId: json['ticket_type_id'] as String,
      status: json['status'] as String,
      amountPaise: (json['amount_paise'] as num).toInt(),
      currency: json['currency'] as String? ?? 'INR',
      razorpayOrderId: json['razorpay_order_id'] as String?,
      groupId: json['group_id'] as String?,
      joinCode: json['join_code'] as String?,
      createdAt: DateTime.parse(json['created_at'] as String),
      tickets:
          (json['tickets'] as List<dynamic>?)
              ?.map((e) => TicketDto.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
      guestAccessToken: json['guest_access_token'] as String?,
    );

Map<String, dynamic> _$$OrderDtoImplToJson(_$OrderDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'event_id': instance.eventId,
      'ticket_type_id': instance.ticketTypeId,
      'status': instance.status,
      'amount_paise': instance.amountPaise,
      'currency': instance.currency,
      'razorpay_order_id': instance.razorpayOrderId,
      'group_id': instance.groupId,
      'join_code': instance.joinCode,
      'created_at': instance.createdAt.toIso8601String(),
      'tickets': instance.tickets,
      'guest_access_token': instance.guestAccessToken,
    };

_$GroupMemberDtoImpl _$$GroupMemberDtoImplFromJson(Map<String, dynamic> json) =>
    _$GroupMemberDtoImpl(
      id: json['id'] as String,
      userId: json['user_id'] as String?,
      name: json['name'] as String,
      phone: json['phone'] as String,
      ticketId: json['ticket_id'] as String?,
      answersJson: json['answers_json'] as String?,
      joinedAt: json['joined_at'] == null
          ? null
          : DateTime.parse(json['joined_at'] as String),
      username: json['username'] as String?,
      avatarKey: json['avatar_key'] as String?,
    );

Map<String, dynamic> _$$GroupMemberDtoImplToJson(
  _$GroupMemberDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'user_id': instance.userId,
  'name': instance.name,
  'phone': instance.phone,
  'ticket_id': instance.ticketId,
  'answers_json': instance.answersJson,
  'joined_at': instance.joinedAt?.toIso8601String(),
  'username': instance.username,
  'avatar_key': instance.avatarKey,
};

_$GroupDtoImpl _$$GroupDtoImplFromJson(Map<String, dynamic> json) =>
    _$GroupDtoImpl(
      id: json['id'] as String,
      eventId: json['event_id'] as String,
      ticketTypeId: json['ticket_type_id'] as String,
      groupNumber: (json['group_number'] as num).toInt(),
      displayName: json['display_name'] as String?,
      joinCode: json['join_code'] as String,
      leaderUserId: json['leader_user_id'] as String,
      capacity: (json['capacity'] as num).toInt(),
      members:
          (json['members'] as List<dynamic>?)
              ?.map((e) => GroupMemberDto.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
    );

Map<String, dynamic> _$$GroupDtoImplToJson(_$GroupDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'event_id': instance.eventId,
      'ticket_type_id': instance.ticketTypeId,
      'group_number': instance.groupNumber,
      'display_name': instance.displayName,
      'join_code': instance.joinCode,
      'leader_user_id': instance.leaderUserId,
      'capacity': instance.capacity,
      'members': instance.members,
    };

_$TransferDtoImpl _$$TransferDtoImplFromJson(Map<String, dynamic> json) =>
    _$TransferDtoImpl(
      id: json['id'] as String,
      ticketId: json['ticket_id'] as String,
      toPhone: json['to_phone'] as String,
      transferCode: json['transfer_code'] as String,
      status: json['status'] as String,
      expiresAt: DateTime.parse(json['expires_at'] as String),
      createdAt: DateTime.parse(json['created_at'] as String),
    );

Map<String, dynamic> _$$TransferDtoImplToJson(_$TransferDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'ticket_id': instance.ticketId,
      'to_phone': instance.toPhone,
      'transfer_code': instance.transferCode,
      'status': instance.status,
      'expires_at': instance.expiresAt.toIso8601String(),
      'created_at': instance.createdAt.toIso8601String(),
    };
