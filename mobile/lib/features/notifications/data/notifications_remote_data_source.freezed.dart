// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'notifications_remote_data_source.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

NotificationDto _$NotificationDtoFromJson(Map<String, dynamic> json) {
  return _NotificationDto.fromJson(json);
}

/// @nodoc
mixin _$NotificationDto {
  String get id => throw _privateConstructorUsedError;
  String get kind => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String get body => throw _privateConstructorUsedError;

  /// Payload for deep-linking, mirroring the push `data` map.
  @JsonKey(name: 'data_json')
  String? get dataJson => throw _privateConstructorUsedError;
  @JsonKey(name: 'read_at')
  DateTime? get readAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this NotificationDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of NotificationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $NotificationDtoCopyWith<NotificationDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $NotificationDtoCopyWith<$Res> {
  factory $NotificationDtoCopyWith(
    NotificationDto value,
    $Res Function(NotificationDto) then,
  ) = _$NotificationDtoCopyWithImpl<$Res, NotificationDto>;
  @useResult
  $Res call({
    String id,
    String kind,
    String title,
    String body,
    @JsonKey(name: 'data_json') String? dataJson,
    @JsonKey(name: 'read_at') DateTime? readAt,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class _$NotificationDtoCopyWithImpl<$Res, $Val extends NotificationDto>
    implements $NotificationDtoCopyWith<$Res> {
  _$NotificationDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of NotificationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? kind = null,
    Object? title = null,
    Object? body = null,
    Object? dataJson = freezed,
    Object? readAt = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            kind: null == kind
                ? _value.kind
                : kind // ignore: cast_nullable_to_non_nullable
                      as String,
            title: null == title
                ? _value.title
                : title // ignore: cast_nullable_to_non_nullable
                      as String,
            body: null == body
                ? _value.body
                : body // ignore: cast_nullable_to_non_nullable
                      as String,
            dataJson: freezed == dataJson
                ? _value.dataJson
                : dataJson // ignore: cast_nullable_to_non_nullable
                      as String?,
            readAt: freezed == readAt
                ? _value.readAt
                : readAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$NotificationDtoImplCopyWith<$Res>
    implements $NotificationDtoCopyWith<$Res> {
  factory _$$NotificationDtoImplCopyWith(
    _$NotificationDtoImpl value,
    $Res Function(_$NotificationDtoImpl) then,
  ) = __$$NotificationDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String kind,
    String title,
    String body,
    @JsonKey(name: 'data_json') String? dataJson,
    @JsonKey(name: 'read_at') DateTime? readAt,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class __$$NotificationDtoImplCopyWithImpl<$Res>
    extends _$NotificationDtoCopyWithImpl<$Res, _$NotificationDtoImpl>
    implements _$$NotificationDtoImplCopyWith<$Res> {
  __$$NotificationDtoImplCopyWithImpl(
    _$NotificationDtoImpl _value,
    $Res Function(_$NotificationDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of NotificationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? kind = null,
    Object? title = null,
    Object? body = null,
    Object? dataJson = freezed,
    Object? readAt = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$NotificationDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        kind: null == kind
            ? _value.kind
            : kind // ignore: cast_nullable_to_non_nullable
                  as String,
        title: null == title
            ? _value.title
            : title // ignore: cast_nullable_to_non_nullable
                  as String,
        body: null == body
            ? _value.body
            : body // ignore: cast_nullable_to_non_nullable
                  as String,
        dataJson: freezed == dataJson
            ? _value.dataJson
            : dataJson // ignore: cast_nullable_to_non_nullable
                  as String?,
        readAt: freezed == readAt
            ? _value.readAt
            : readAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$NotificationDtoImpl implements _NotificationDto {
  const _$NotificationDtoImpl({
    required this.id,
    this.kind = '',
    this.title = '',
    this.body = '',
    @JsonKey(name: 'data_json') this.dataJson,
    @JsonKey(name: 'read_at') this.readAt,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$NotificationDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$NotificationDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey()
  final String kind;
  @override
  @JsonKey()
  final String title;
  @override
  @JsonKey()
  final String body;

  /// Payload for deep-linking, mirroring the push `data` map.
  @override
  @JsonKey(name: 'data_json')
  final String? dataJson;
  @override
  @JsonKey(name: 'read_at')
  final DateTime? readAt;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  @override
  String toString() {
    return 'NotificationDto(id: $id, kind: $kind, title: $title, body: $body, dataJson: $dataJson, readAt: $readAt, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$NotificationDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.kind, kind) || other.kind == kind) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.body, body) || other.body == body) &&
            (identical(other.dataJson, dataJson) ||
                other.dataJson == dataJson) &&
            (identical(other.readAt, readAt) || other.readAt == readAt) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    kind,
    title,
    body,
    dataJson,
    readAt,
    createdAt,
  );

  /// Create a copy of NotificationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$NotificationDtoImplCopyWith<_$NotificationDtoImpl> get copyWith =>
      __$$NotificationDtoImplCopyWithImpl<_$NotificationDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$NotificationDtoImplToJson(this);
  }
}

abstract class _NotificationDto implements NotificationDto {
  const factory _NotificationDto({
    required final String id,
    final String kind,
    final String title,
    final String body,
    @JsonKey(name: 'data_json') final String? dataJson,
    @JsonKey(name: 'read_at') final DateTime? readAt,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$NotificationDtoImpl;

  factory _NotificationDto.fromJson(Map<String, dynamic> json) =
      _$NotificationDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get kind;
  @override
  String get title;
  @override
  String get body;

  /// Payload for deep-linking, mirroring the push `data` map.
  @override
  @JsonKey(name: 'data_json')
  String? get dataJson;
  @override
  @JsonKey(name: 'read_at')
  DateTime? get readAt;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;

  /// Create a copy of NotificationDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$NotificationDtoImplCopyWith<_$NotificationDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

NotificationPageDto _$NotificationPageDtoFromJson(Map<String, dynamic> json) {
  return _NotificationPageDto.fromJson(json);
}

/// @nodoc
mixin _$NotificationPageDto {
  List<NotificationDto> get items => throw _privateConstructorUsedError;
  int get unreadCount => throw _privateConstructorUsedError;

  /// Serializes this NotificationPageDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of NotificationPageDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $NotificationPageDtoCopyWith<NotificationPageDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $NotificationPageDtoCopyWith<$Res> {
  factory $NotificationPageDtoCopyWith(
    NotificationPageDto value,
    $Res Function(NotificationPageDto) then,
  ) = _$NotificationPageDtoCopyWithImpl<$Res, NotificationPageDto>;
  @useResult
  $Res call({List<NotificationDto> items, int unreadCount});
}

/// @nodoc
class _$NotificationPageDtoCopyWithImpl<$Res, $Val extends NotificationPageDto>
    implements $NotificationPageDtoCopyWith<$Res> {
  _$NotificationPageDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of NotificationPageDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? items = null, Object? unreadCount = null}) {
    return _then(
      _value.copyWith(
            items: null == items
                ? _value.items
                : items // ignore: cast_nullable_to_non_nullable
                      as List<NotificationDto>,
            unreadCount: null == unreadCount
                ? _value.unreadCount
                : unreadCount // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$NotificationPageDtoImplCopyWith<$Res>
    implements $NotificationPageDtoCopyWith<$Res> {
  factory _$$NotificationPageDtoImplCopyWith(
    _$NotificationPageDtoImpl value,
    $Res Function(_$NotificationPageDtoImpl) then,
  ) = __$$NotificationPageDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({List<NotificationDto> items, int unreadCount});
}

/// @nodoc
class __$$NotificationPageDtoImplCopyWithImpl<$Res>
    extends _$NotificationPageDtoCopyWithImpl<$Res, _$NotificationPageDtoImpl>
    implements _$$NotificationPageDtoImplCopyWith<$Res> {
  __$$NotificationPageDtoImplCopyWithImpl(
    _$NotificationPageDtoImpl _value,
    $Res Function(_$NotificationPageDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of NotificationPageDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? items = null, Object? unreadCount = null}) {
    return _then(
      _$NotificationPageDtoImpl(
        items: null == items
            ? _value._items
            : items // ignore: cast_nullable_to_non_nullable
                  as List<NotificationDto>,
        unreadCount: null == unreadCount
            ? _value.unreadCount
            : unreadCount // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$NotificationPageDtoImpl implements _NotificationPageDto {
  const _$NotificationPageDtoImpl({
    final List<NotificationDto> items = const <NotificationDto>[],
    this.unreadCount = 0,
  }) : _items = items;

  factory _$NotificationPageDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$NotificationPageDtoImplFromJson(json);

  final List<NotificationDto> _items;
  @override
  @JsonKey()
  List<NotificationDto> get items {
    if (_items is EqualUnmodifiableListView) return _items;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_items);
  }

  @override
  @JsonKey()
  final int unreadCount;

  @override
  String toString() {
    return 'NotificationPageDto(items: $items, unreadCount: $unreadCount)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$NotificationPageDtoImpl &&
            const DeepCollectionEquality().equals(other._items, _items) &&
            (identical(other.unreadCount, unreadCount) ||
                other.unreadCount == unreadCount));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    const DeepCollectionEquality().hash(_items),
    unreadCount,
  );

  /// Create a copy of NotificationPageDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$NotificationPageDtoImplCopyWith<_$NotificationPageDtoImpl> get copyWith =>
      __$$NotificationPageDtoImplCopyWithImpl<_$NotificationPageDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$NotificationPageDtoImplToJson(this);
  }
}

abstract class _NotificationPageDto implements NotificationPageDto {
  const factory _NotificationPageDto({
    final List<NotificationDto> items,
    final int unreadCount,
  }) = _$NotificationPageDtoImpl;

  factory _NotificationPageDto.fromJson(Map<String, dynamic> json) =
      _$NotificationPageDtoImpl.fromJson;

  @override
  List<NotificationDto> get items;
  @override
  int get unreadCount;

  /// Create a copy of NotificationPageDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$NotificationPageDtoImplCopyWith<_$NotificationPageDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
