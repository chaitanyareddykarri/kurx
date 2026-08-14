// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'event_category_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

EventCategoryDto _$EventCategoryDtoFromJson(Map<String, dynamic> json) {
  return _EventCategoryDto.fromJson(json);
}

/// @nodoc
mixin _$EventCategoryDto {
  String get id => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String get level => throw _privateConstructorUsedError;
  @JsonKey(name: 'parent_id')
  String? get parentId => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_visible')
  bool get isVisible => throw _privateConstructorUsedError;

  /// D-305 — "Public"/"Private" on a Type node, used by the Create-Event gate to offer only the
  /// Types the chosen product class permits. Null = Public, matching the server's own fallback.
  @JsonKey(name: 'product_class')
  String? get productClass => throw _privateConstructorUsedError;

  /// Serializes this EventCategoryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventCategoryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventCategoryDtoCopyWith<EventCategoryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventCategoryDtoCopyWith<$Res> {
  factory $EventCategoryDtoCopyWith(
    EventCategoryDto value,
    $Res Function(EventCategoryDto) then,
  ) = _$EventCategoryDtoCopyWithImpl<$Res, EventCategoryDto>;
  @useResult
  $Res call({
    String id,
    String name,
    String level,
    @JsonKey(name: 'parent_id') String? parentId,
    @JsonKey(name: 'is_visible') bool isVisible,
    @JsonKey(name: 'product_class') String? productClass,
  });
}

/// @nodoc
class _$EventCategoryDtoCopyWithImpl<$Res, $Val extends EventCategoryDto>
    implements $EventCategoryDtoCopyWith<$Res> {
  _$EventCategoryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventCategoryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? level = null,
    Object? parentId = freezed,
    Object? isVisible = null,
    Object? productClass = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            level: null == level
                ? _value.level
                : level // ignore: cast_nullable_to_non_nullable
                      as String,
            parentId: freezed == parentId
                ? _value.parentId
                : parentId // ignore: cast_nullable_to_non_nullable
                      as String?,
            isVisible: null == isVisible
                ? _value.isVisible
                : isVisible // ignore: cast_nullable_to_non_nullable
                      as bool,
            productClass: freezed == productClass
                ? _value.productClass
                : productClass // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EventCategoryDtoImplCopyWith<$Res>
    implements $EventCategoryDtoCopyWith<$Res> {
  factory _$$EventCategoryDtoImplCopyWith(
    _$EventCategoryDtoImpl value,
    $Res Function(_$EventCategoryDtoImpl) then,
  ) = __$$EventCategoryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String name,
    String level,
    @JsonKey(name: 'parent_id') String? parentId,
    @JsonKey(name: 'is_visible') bool isVisible,
    @JsonKey(name: 'product_class') String? productClass,
  });
}

/// @nodoc
class __$$EventCategoryDtoImplCopyWithImpl<$Res>
    extends _$EventCategoryDtoCopyWithImpl<$Res, _$EventCategoryDtoImpl>
    implements _$$EventCategoryDtoImplCopyWith<$Res> {
  __$$EventCategoryDtoImplCopyWithImpl(
    _$EventCategoryDtoImpl _value,
    $Res Function(_$EventCategoryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventCategoryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? level = null,
    Object? parentId = freezed,
    Object? isVisible = null,
    Object? productClass = freezed,
  }) {
    return _then(
      _$EventCategoryDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        level: null == level
            ? _value.level
            : level // ignore: cast_nullable_to_non_nullable
                  as String,
        parentId: freezed == parentId
            ? _value.parentId
            : parentId // ignore: cast_nullable_to_non_nullable
                  as String?,
        isVisible: null == isVisible
            ? _value.isVisible
            : isVisible // ignore: cast_nullable_to_non_nullable
                  as bool,
        productClass: freezed == productClass
            ? _value.productClass
            : productClass // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventCategoryDtoImpl extends _EventCategoryDto {
  const _$EventCategoryDtoImpl({
    required this.id,
    required this.name,
    this.level = 'category',
    @JsonKey(name: 'parent_id') this.parentId,
    @JsonKey(name: 'is_visible') this.isVisible = true,
    @JsonKey(name: 'product_class') this.productClass,
  }) : super._();

  factory _$EventCategoryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventCategoryDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String name;
  @override
  @JsonKey()
  final String level;
  @override
  @JsonKey(name: 'parent_id')
  final String? parentId;
  @override
  @JsonKey(name: 'is_visible')
  final bool isVisible;

  /// D-305 — "Public"/"Private" on a Type node, used by the Create-Event gate to offer only the
  /// Types the chosen product class permits. Null = Public, matching the server's own fallback.
  @override
  @JsonKey(name: 'product_class')
  final String? productClass;

  @override
  String toString() {
    return 'EventCategoryDto(id: $id, name: $name, level: $level, parentId: $parentId, isVisible: $isVisible, productClass: $productClass)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventCategoryDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.level, level) || other.level == level) &&
            (identical(other.parentId, parentId) ||
                other.parentId == parentId) &&
            (identical(other.isVisible, isVisible) ||
                other.isVisible == isVisible) &&
            (identical(other.productClass, productClass) ||
                other.productClass == productClass));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    name,
    level,
    parentId,
    isVisible,
    productClass,
  );

  /// Create a copy of EventCategoryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventCategoryDtoImplCopyWith<_$EventCategoryDtoImpl> get copyWith =>
      __$$EventCategoryDtoImplCopyWithImpl<_$EventCategoryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventCategoryDtoImplToJson(this);
  }
}

abstract class _EventCategoryDto extends EventCategoryDto {
  const factory _EventCategoryDto({
    required final String id,
    required final String name,
    final String level,
    @JsonKey(name: 'parent_id') final String? parentId,
    @JsonKey(name: 'is_visible') final bool isVisible,
    @JsonKey(name: 'product_class') final String? productClass,
  }) = _$EventCategoryDtoImpl;
  const _EventCategoryDto._() : super._();

  factory _EventCategoryDto.fromJson(Map<String, dynamic> json) =
      _$EventCategoryDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get name;
  @override
  String get level;
  @override
  @JsonKey(name: 'parent_id')
  String? get parentId;
  @override
  @JsonKey(name: 'is_visible')
  bool get isVisible;

  /// D-305 — "Public"/"Private" on a Type node, used by the Create-Event gate to offer only the
  /// Types the chosen product class permits. Null = Public, matching the server's own fallback.
  @override
  @JsonKey(name: 'product_class')
  String? get productClass;

  /// Create a copy of EventCategoryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventCategoryDtoImplCopyWith<_$EventCategoryDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
