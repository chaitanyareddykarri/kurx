// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'event_category_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$EventCategoryDtoImpl _$$EventCategoryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$EventCategoryDtoImpl(
  id: json['id'] as String,
  name: json['name'] as String,
  level: json['level'] as String? ?? 'category',
  parentId: json['parent_id'] as String?,
  isVisible: json['is_visible'] as bool? ?? true,
  productClass: json['product_class'] as String?,
);

Map<String, dynamic> _$$EventCategoryDtoImplToJson(
  _$EventCategoryDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'level': instance.level,
  'parent_id': instance.parentId,
  'is_visible': instance.isVisible,
  'product_class': instance.productClass,
};
