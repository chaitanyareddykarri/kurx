// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ticket_type_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$TicketTypeDtoImpl _$$TicketTypeDtoImplFromJson(Map<String, dynamic> json) =>
    _$TicketTypeDtoImpl(
      id: json['id'] as String,
      name: json['name'] as String,
      pricePaise: (json['price_paise'] as num?)?.toInt() ?? 0,
      quantity: (json['quantity'] as num?)?.toInt() ?? 0,
      sold: (json['sold'] as num?)?.toInt() ?? 0,
      available: (json['available'] as num?)?.toInt() ?? 0,
      saleEnds: json['sale_ends'] == null
          ? null
          : DateTime.parse(json['sale_ends'] as String),
      isAllAccess: json['is_all_access'] as bool? ?? false,
      eventId: json['event_id'] as String?,
      pricingUnit: json['pricing_unit'] as String?,
      registrationMode: json['registration_mode'] as String?,
      groupMin: (json['group_min'] as num?)?.toInt(),
      groupMax: (json['group_max'] as num?)?.toInt(),
      saleStarts: json['sale_starts'] == null
          ? null
          : DateTime.parse(json['sale_starts'] as String),
      perUserLimit: (json['per_user_limit'] as num?)?.toInt(),
      isCompetition: json['is_competition'] as bool? ?? false,
    );

Map<String, dynamic> _$$TicketTypeDtoImplToJson(_$TicketTypeDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'price_paise': instance.pricePaise,
      'quantity': instance.quantity,
      'sold': instance.sold,
      'available': instance.available,
      'sale_ends': instance.saleEnds?.toIso8601String(),
      'is_all_access': instance.isAllAccess,
      'event_id': instance.eventId,
      'pricing_unit': instance.pricingUnit,
      'registration_mode': instance.registrationMode,
      'group_min': instance.groupMin,
      'group_max': instance.groupMax,
      'sale_starts': instance.saleStarts?.toIso8601String(),
      'per_user_limit': instance.perUserLimit,
      'is_competition': instance.isCompetition,
    };
