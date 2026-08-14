// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'token_response_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

TokenResponseDto _$TokenResponseDtoFromJson(Map<String, dynamic> json) {
  return _TokenResponseDto.fromJson(json);
}

/// @nodoc
mixin _$TokenResponseDto {
  @JsonKey(name: 'access_token')
  String get accessToken => throw _privateConstructorUsedError;
  @JsonKey(name: 'refresh_token')
  String get refreshToken => throw _privateConstructorUsedError;
  @JsonKey(name: 'access_expires_at')
  DateTime? get accessExpiresAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'refresh_expires_at')
  DateTime? get refreshExpiresAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'user_id')
  String? get userId => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_new_user')
  bool get isNewUser => throw _privateConstructorUsedError;

  /// Serializes this TokenResponseDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TokenResponseDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TokenResponseDtoCopyWith<TokenResponseDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TokenResponseDtoCopyWith<$Res> {
  factory $TokenResponseDtoCopyWith(
    TokenResponseDto value,
    $Res Function(TokenResponseDto) then,
  ) = _$TokenResponseDtoCopyWithImpl<$Res, TokenResponseDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'access_token') String accessToken,
    @JsonKey(name: 'refresh_token') String refreshToken,
    @JsonKey(name: 'access_expires_at') DateTime? accessExpiresAt,
    @JsonKey(name: 'refresh_expires_at') DateTime? refreshExpiresAt,
    @JsonKey(name: 'user_id') String? userId,
    @JsonKey(name: 'is_new_user') bool isNewUser,
  });
}

/// @nodoc
class _$TokenResponseDtoCopyWithImpl<$Res, $Val extends TokenResponseDto>
    implements $TokenResponseDtoCopyWith<$Res> {
  _$TokenResponseDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TokenResponseDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? accessToken = null,
    Object? refreshToken = null,
    Object? accessExpiresAt = freezed,
    Object? refreshExpiresAt = freezed,
    Object? userId = freezed,
    Object? isNewUser = null,
  }) {
    return _then(
      _value.copyWith(
            accessToken: null == accessToken
                ? _value.accessToken
                : accessToken // ignore: cast_nullable_to_non_nullable
                      as String,
            refreshToken: null == refreshToken
                ? _value.refreshToken
                : refreshToken // ignore: cast_nullable_to_non_nullable
                      as String,
            accessExpiresAt: freezed == accessExpiresAt
                ? _value.accessExpiresAt
                : accessExpiresAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            refreshExpiresAt: freezed == refreshExpiresAt
                ? _value.refreshExpiresAt
                : refreshExpiresAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            userId: freezed == userId
                ? _value.userId
                : userId // ignore: cast_nullable_to_non_nullable
                      as String?,
            isNewUser: null == isNewUser
                ? _value.isNewUser
                : isNewUser // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TokenResponseDtoImplCopyWith<$Res>
    implements $TokenResponseDtoCopyWith<$Res> {
  factory _$$TokenResponseDtoImplCopyWith(
    _$TokenResponseDtoImpl value,
    $Res Function(_$TokenResponseDtoImpl) then,
  ) = __$$TokenResponseDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'access_token') String accessToken,
    @JsonKey(name: 'refresh_token') String refreshToken,
    @JsonKey(name: 'access_expires_at') DateTime? accessExpiresAt,
    @JsonKey(name: 'refresh_expires_at') DateTime? refreshExpiresAt,
    @JsonKey(name: 'user_id') String? userId,
    @JsonKey(name: 'is_new_user') bool isNewUser,
  });
}

/// @nodoc
class __$$TokenResponseDtoImplCopyWithImpl<$Res>
    extends _$TokenResponseDtoCopyWithImpl<$Res, _$TokenResponseDtoImpl>
    implements _$$TokenResponseDtoImplCopyWith<$Res> {
  __$$TokenResponseDtoImplCopyWithImpl(
    _$TokenResponseDtoImpl _value,
    $Res Function(_$TokenResponseDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TokenResponseDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? accessToken = null,
    Object? refreshToken = null,
    Object? accessExpiresAt = freezed,
    Object? refreshExpiresAt = freezed,
    Object? userId = freezed,
    Object? isNewUser = null,
  }) {
    return _then(
      _$TokenResponseDtoImpl(
        accessToken: null == accessToken
            ? _value.accessToken
            : accessToken // ignore: cast_nullable_to_non_nullable
                  as String,
        refreshToken: null == refreshToken
            ? _value.refreshToken
            : refreshToken // ignore: cast_nullable_to_non_nullable
                  as String,
        accessExpiresAt: freezed == accessExpiresAt
            ? _value.accessExpiresAt
            : accessExpiresAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        refreshExpiresAt: freezed == refreshExpiresAt
            ? _value.refreshExpiresAt
            : refreshExpiresAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        userId: freezed == userId
            ? _value.userId
            : userId // ignore: cast_nullable_to_non_nullable
                  as String?,
        isNewUser: null == isNewUser
            ? _value.isNewUser
            : isNewUser // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TokenResponseDtoImpl implements _TokenResponseDto {
  const _$TokenResponseDtoImpl({
    @JsonKey(name: 'access_token') required this.accessToken,
    @JsonKey(name: 'refresh_token') required this.refreshToken,
    @JsonKey(name: 'access_expires_at') this.accessExpiresAt,
    @JsonKey(name: 'refresh_expires_at') this.refreshExpiresAt,
    @JsonKey(name: 'user_id') this.userId,
    @JsonKey(name: 'is_new_user') this.isNewUser = false,
  });

  factory _$TokenResponseDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TokenResponseDtoImplFromJson(json);

  @override
  @JsonKey(name: 'access_token')
  final String accessToken;
  @override
  @JsonKey(name: 'refresh_token')
  final String refreshToken;
  @override
  @JsonKey(name: 'access_expires_at')
  final DateTime? accessExpiresAt;
  @override
  @JsonKey(name: 'refresh_expires_at')
  final DateTime? refreshExpiresAt;
  @override
  @JsonKey(name: 'user_id')
  final String? userId;
  @override
  @JsonKey(name: 'is_new_user')
  final bool isNewUser;

  @override
  String toString() {
    return 'TokenResponseDto(accessToken: $accessToken, refreshToken: $refreshToken, accessExpiresAt: $accessExpiresAt, refreshExpiresAt: $refreshExpiresAt, userId: $userId, isNewUser: $isNewUser)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TokenResponseDtoImpl &&
            (identical(other.accessToken, accessToken) ||
                other.accessToken == accessToken) &&
            (identical(other.refreshToken, refreshToken) ||
                other.refreshToken == refreshToken) &&
            (identical(other.accessExpiresAt, accessExpiresAt) ||
                other.accessExpiresAt == accessExpiresAt) &&
            (identical(other.refreshExpiresAt, refreshExpiresAt) ||
                other.refreshExpiresAt == refreshExpiresAt) &&
            (identical(other.userId, userId) || other.userId == userId) &&
            (identical(other.isNewUser, isNewUser) ||
                other.isNewUser == isNewUser));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    accessToken,
    refreshToken,
    accessExpiresAt,
    refreshExpiresAt,
    userId,
    isNewUser,
  );

  /// Create a copy of TokenResponseDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TokenResponseDtoImplCopyWith<_$TokenResponseDtoImpl> get copyWith =>
      __$$TokenResponseDtoImplCopyWithImpl<_$TokenResponseDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$TokenResponseDtoImplToJson(this);
  }
}

abstract class _TokenResponseDto implements TokenResponseDto {
  const factory _TokenResponseDto({
    @JsonKey(name: 'access_token') required final String accessToken,
    @JsonKey(name: 'refresh_token') required final String refreshToken,
    @JsonKey(name: 'access_expires_at') final DateTime? accessExpiresAt,
    @JsonKey(name: 'refresh_expires_at') final DateTime? refreshExpiresAt,
    @JsonKey(name: 'user_id') final String? userId,
    @JsonKey(name: 'is_new_user') final bool isNewUser,
  }) = _$TokenResponseDtoImpl;

  factory _TokenResponseDto.fromJson(Map<String, dynamic> json) =
      _$TokenResponseDtoImpl.fromJson;

  @override
  @JsonKey(name: 'access_token')
  String get accessToken;
  @override
  @JsonKey(name: 'refresh_token')
  String get refreshToken;
  @override
  @JsonKey(name: 'access_expires_at')
  DateTime? get accessExpiresAt;
  @override
  @JsonKey(name: 'refresh_expires_at')
  DateTime? get refreshExpiresAt;
  @override
  @JsonKey(name: 'user_id')
  String? get userId;
  @override
  @JsonKey(name: 'is_new_user')
  bool get isNewUser;

  /// Create a copy of TokenResponseDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TokenResponseDtoImplCopyWith<_$TokenResponseDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
