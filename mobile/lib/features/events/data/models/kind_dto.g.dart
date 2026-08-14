// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'kind_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$KindDtoImpl _$$KindDtoImplFromJson(Map<String, dynamic> json) =>
    _$KindDtoImpl(
      slug: json['slug'] as String,
      name: json['name'] as String,
      groupSlug: json['group_slug'] as String,
      groupName: json['group_name'] as String,
      sort: (json['sort'] as num).toInt(),
    );

Map<String, dynamic> _$$KindDtoImplToJson(_$KindDtoImpl instance) =>
    <String, dynamic>{
      'slug': instance.slug,
      'name': instance.name,
      'group_slug': instance.groupSlug,
      'group_name': instance.groupName,
      'sort': instance.sort,
    };
