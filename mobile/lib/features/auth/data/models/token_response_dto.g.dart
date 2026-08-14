// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'token_response_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$TokenResponseDtoImpl _$$TokenResponseDtoImplFromJson(
  Map<String, dynamic> json,
) => _$TokenResponseDtoImpl(
  accessToken: json['access_token'] as String,
  refreshToken: json['refresh_token'] as String,
  accessExpiresAt: json['access_expires_at'] == null
      ? null
      : DateTime.parse(json['access_expires_at'] as String),
  refreshExpiresAt: json['refresh_expires_at'] == null
      ? null
      : DateTime.parse(json['refresh_expires_at'] as String),
  userId: json['user_id'] as String?,
  isNewUser: json['is_new_user'] as bool? ?? false,
);

Map<String, dynamic> _$$TokenResponseDtoImplToJson(
  _$TokenResponseDtoImpl instance,
) => <String, dynamic>{
  'access_token': instance.accessToken,
  'refresh_token': instance.refreshToken,
  'access_expires_at': instance.accessExpiresAt?.toIso8601String(),
  'refresh_expires_at': instance.refreshExpiresAt?.toIso8601String(),
  'user_id': instance.userId,
  'is_new_user': instance.isNewUser,
};
