// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'kind_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

KindDto _$KindDtoFromJson(Map<String, dynamic> json) {
  return _KindDto.fromJson(json);
}

/// @nodoc
mixin _$KindDto {
  String get slug => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  @JsonKey(name: 'group_slug')
  String get groupSlug => throw _privateConstructorUsedError;
  @JsonKey(name: 'group_name')
  String get groupName => throw _privateConstructorUsedError;
  int get sort => throw _privateConstructorUsedError;

  /// Serializes this KindDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of KindDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $KindDtoCopyWith<KindDto> get copyWith => throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $KindDtoCopyWith<$Res> {
  factory $KindDtoCopyWith(KindDto value, $Res Function(KindDto) then) =
      _$KindDtoCopyWithImpl<$Res, KindDto>;
  @useResult
  $Res call({
    String slug,
    String name,
    @JsonKey(name: 'group_slug') String groupSlug,
    @JsonKey(name: 'group_name') String groupName,
    int sort,
  });
}

/// @nodoc
class _$KindDtoCopyWithImpl<$Res, $Val extends KindDto>
    implements $KindDtoCopyWith<$Res> {
  _$KindDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of KindDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? slug = null,
    Object? name = null,
    Object? groupSlug = null,
    Object? groupName = null,
    Object? sort = null,
  }) {
    return _then(
      _value.copyWith(
            slug: null == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            groupSlug: null == groupSlug
                ? _value.groupSlug
                : groupSlug // ignore: cast_nullable_to_non_nullable
                      as String,
            groupName: null == groupName
                ? _value.groupName
                : groupName // ignore: cast_nullable_to_non_nullable
                      as String,
            sort: null == sort
                ? _value.sort
                : sort // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$KindDtoImplCopyWith<$Res> implements $KindDtoCopyWith<$Res> {
  factory _$$KindDtoImplCopyWith(
    _$KindDtoImpl value,
    $Res Function(_$KindDtoImpl) then,
  ) = __$$KindDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String slug,
    String name,
    @JsonKey(name: 'group_slug') String groupSlug,
    @JsonKey(name: 'group_name') String groupName,
    int sort,
  });
}

/// @nodoc
class __$$KindDtoImplCopyWithImpl<$Res>
    extends _$KindDtoCopyWithImpl<$Res, _$KindDtoImpl>
    implements _$$KindDtoImplCopyWith<$Res> {
  __$$KindDtoImplCopyWithImpl(
    _$KindDtoImpl _value,
    $Res Function(_$KindDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of KindDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? slug = null,
    Object? name = null,
    Object? groupSlug = null,
    Object? groupName = null,
    Object? sort = null,
  }) {
    return _then(
      _$KindDtoImpl(
        slug: null == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        groupSlug: null == groupSlug
            ? _value.groupSlug
            : groupSlug // ignore: cast_nullable_to_non_nullable
                  as String,
        groupName: null == groupName
            ? _value.groupName
            : groupName // ignore: cast_nullable_to_non_nullable
                  as String,
        sort: null == sort
            ? _value.sort
            : sort // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$KindDtoImpl extends _KindDto {
  const _$KindDtoImpl({
    required this.slug,
    required this.name,
    @JsonKey(name: 'group_slug') required this.groupSlug,
    @JsonKey(name: 'group_name') required this.groupName,
    required this.sort,
  }) : super._();

  factory _$KindDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$KindDtoImplFromJson(json);

  @override
  final String slug;
  @override
  final String name;
  @override
  @JsonKey(name: 'group_slug')
  final String groupSlug;
  @override
  @JsonKey(name: 'group_name')
  final String groupName;
  @override
  final int sort;

  @override
  String toString() {
    return 'KindDto(slug: $slug, name: $name, groupSlug: $groupSlug, groupName: $groupName, sort: $sort)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$KindDtoImpl &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.groupSlug, groupSlug) ||
                other.groupSlug == groupSlug) &&
            (identical(other.groupName, groupName) ||
                other.groupName == groupName) &&
            (identical(other.sort, sort) || other.sort == sort));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, slug, name, groupSlug, groupName, sort);

  /// Create a copy of KindDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$KindDtoImplCopyWith<_$KindDtoImpl> get copyWith =>
      __$$KindDtoImplCopyWithImpl<_$KindDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$KindDtoImplToJson(this);
  }
}

abstract class _KindDto extends KindDto {
  const factory _KindDto({
    required final String slug,
    required final String name,
    @JsonKey(name: 'group_slug') required final String groupSlug,
    @JsonKey(name: 'group_name') required final String groupName,
    required final int sort,
  }) = _$KindDtoImpl;
  const _KindDto._() : super._();

  factory _KindDto.fromJson(Map<String, dynamic> json) = _$KindDtoImpl.fromJson;

  @override
  String get slug;
  @override
  String get name;
  @override
  @JsonKey(name: 'group_slug')
  String get groupSlug;
  @override
  @JsonKey(name: 'group_name')
  String get groupName;
  @override
  int get sort;

  /// Create a copy of KindDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$KindDtoImplCopyWith<_$KindDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
