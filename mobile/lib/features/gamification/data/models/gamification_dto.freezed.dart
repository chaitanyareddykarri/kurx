// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'gamification_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

BadgeDto _$BadgeDtoFromJson(Map<String, dynamic> json) {
  return _BadgeDto.fromJson(json);
}

/// @nodoc
mixin _$BadgeDto {
  String get id => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String get type => throw _privateConstructorUsedError;
  String get description => throw _privateConstructorUsedError;
  String? get iconKey => throw _privateConstructorUsedError;
  DateTime get earnedAt => throw _privateConstructorUsedError;

  /// Serializes this BadgeDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of BadgeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $BadgeDtoCopyWith<BadgeDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $BadgeDtoCopyWith<$Res> {
  factory $BadgeDtoCopyWith(BadgeDto value, $Res Function(BadgeDto) then) =
      _$BadgeDtoCopyWithImpl<$Res, BadgeDto>;
  @useResult
  $Res call({
    String id,
    String name,
    String type,
    String description,
    String? iconKey,
    DateTime earnedAt,
  });
}

/// @nodoc
class _$BadgeDtoCopyWithImpl<$Res, $Val extends BadgeDto>
    implements $BadgeDtoCopyWith<$Res> {
  _$BadgeDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of BadgeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? type = null,
    Object? description = null,
    Object? iconKey = freezed,
    Object? earnedAt = null,
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
            type: null == type
                ? _value.type
                : type // ignore: cast_nullable_to_non_nullable
                      as String,
            description: null == description
                ? _value.description
                : description // ignore: cast_nullable_to_non_nullable
                      as String,
            iconKey: freezed == iconKey
                ? _value.iconKey
                : iconKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            earnedAt: null == earnedAt
                ? _value.earnedAt
                : earnedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$BadgeDtoImplCopyWith<$Res>
    implements $BadgeDtoCopyWith<$Res> {
  factory _$$BadgeDtoImplCopyWith(
    _$BadgeDtoImpl value,
    $Res Function(_$BadgeDtoImpl) then,
  ) = __$$BadgeDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String name,
    String type,
    String description,
    String? iconKey,
    DateTime earnedAt,
  });
}

/// @nodoc
class __$$BadgeDtoImplCopyWithImpl<$Res>
    extends _$BadgeDtoCopyWithImpl<$Res, _$BadgeDtoImpl>
    implements _$$BadgeDtoImplCopyWith<$Res> {
  __$$BadgeDtoImplCopyWithImpl(
    _$BadgeDtoImpl _value,
    $Res Function(_$BadgeDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of BadgeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? type = null,
    Object? description = null,
    Object? iconKey = freezed,
    Object? earnedAt = null,
  }) {
    return _then(
      _$BadgeDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        type: null == type
            ? _value.type
            : type // ignore: cast_nullable_to_non_nullable
                  as String,
        description: null == description
            ? _value.description
            : description // ignore: cast_nullable_to_non_nullable
                  as String,
        iconKey: freezed == iconKey
            ? _value.iconKey
            : iconKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        earnedAt: null == earnedAt
            ? _value.earnedAt
            : earnedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$BadgeDtoImpl implements _BadgeDto {
  const _$BadgeDtoImpl({
    required this.id,
    required this.name,
    required this.type,
    required this.description,
    this.iconKey,
    required this.earnedAt,
  });

  factory _$BadgeDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$BadgeDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String name;
  @override
  final String type;
  @override
  final String description;
  @override
  final String? iconKey;
  @override
  final DateTime earnedAt;

  @override
  String toString() {
    return 'BadgeDto(id: $id, name: $name, type: $type, description: $description, iconKey: $iconKey, earnedAt: $earnedAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$BadgeDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.type, type) || other.type == type) &&
            (identical(other.description, description) ||
                other.description == description) &&
            (identical(other.iconKey, iconKey) || other.iconKey == iconKey) &&
            (identical(other.earnedAt, earnedAt) ||
                other.earnedAt == earnedAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, id, name, type, description, iconKey, earnedAt);

  /// Create a copy of BadgeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$BadgeDtoImplCopyWith<_$BadgeDtoImpl> get copyWith =>
      __$$BadgeDtoImplCopyWithImpl<_$BadgeDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$BadgeDtoImplToJson(this);
  }
}

abstract class _BadgeDto implements BadgeDto {
  const factory _BadgeDto({
    required final String id,
    required final String name,
    required final String type,
    required final String description,
    final String? iconKey,
    required final DateTime earnedAt,
  }) = _$BadgeDtoImpl;

  factory _BadgeDto.fromJson(Map<String, dynamic> json) =
      _$BadgeDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get name;
  @override
  String get type;
  @override
  String get description;
  @override
  String? get iconKey;
  @override
  DateTime get earnedAt;

  /// Create a copy of BadgeDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$BadgeDtoImplCopyWith<_$BadgeDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

PointsLogEntryDto _$PointsLogEntryDtoFromJson(Map<String, dynamic> json) {
  return _PointsLogEntryDto.fromJson(json);
}

/// @nodoc
mixin _$PointsLogEntryDto {
  String get id => throw _privateConstructorUsedError;
  String get source => throw _privateConstructorUsedError;
  int get points => throw _privateConstructorUsedError;
  String? get reason => throw _privateConstructorUsedError;
  DateTime get createdAt => throw _privateConstructorUsedError;

  /// Serializes this PointsLogEntryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of PointsLogEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $PointsLogEntryDtoCopyWith<PointsLogEntryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $PointsLogEntryDtoCopyWith<$Res> {
  factory $PointsLogEntryDtoCopyWith(
    PointsLogEntryDto value,
    $Res Function(PointsLogEntryDto) then,
  ) = _$PointsLogEntryDtoCopyWithImpl<$Res, PointsLogEntryDto>;
  @useResult
  $Res call({
    String id,
    String source,
    int points,
    String? reason,
    DateTime createdAt,
  });
}

/// @nodoc
class _$PointsLogEntryDtoCopyWithImpl<$Res, $Val extends PointsLogEntryDto>
    implements $PointsLogEntryDtoCopyWith<$Res> {
  _$PointsLogEntryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of PointsLogEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? source = null,
    Object? points = null,
    Object? reason = freezed,
    Object? createdAt = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            source: null == source
                ? _value.source
                : source // ignore: cast_nullable_to_non_nullable
                      as String,
            points: null == points
                ? _value.points
                : points // ignore: cast_nullable_to_non_nullable
                      as int,
            reason: freezed == reason
                ? _value.reason
                : reason // ignore: cast_nullable_to_non_nullable
                      as String?,
            createdAt: null == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$PointsLogEntryDtoImplCopyWith<$Res>
    implements $PointsLogEntryDtoCopyWith<$Res> {
  factory _$$PointsLogEntryDtoImplCopyWith(
    _$PointsLogEntryDtoImpl value,
    $Res Function(_$PointsLogEntryDtoImpl) then,
  ) = __$$PointsLogEntryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String source,
    int points,
    String? reason,
    DateTime createdAt,
  });
}

/// @nodoc
class __$$PointsLogEntryDtoImplCopyWithImpl<$Res>
    extends _$PointsLogEntryDtoCopyWithImpl<$Res, _$PointsLogEntryDtoImpl>
    implements _$$PointsLogEntryDtoImplCopyWith<$Res> {
  __$$PointsLogEntryDtoImplCopyWithImpl(
    _$PointsLogEntryDtoImpl _value,
    $Res Function(_$PointsLogEntryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of PointsLogEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? source = null,
    Object? points = null,
    Object? reason = freezed,
    Object? createdAt = null,
  }) {
    return _then(
      _$PointsLogEntryDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        source: null == source
            ? _value.source
            : source // ignore: cast_nullable_to_non_nullable
                  as String,
        points: null == points
            ? _value.points
            : points // ignore: cast_nullable_to_non_nullable
                  as int,
        reason: freezed == reason
            ? _value.reason
            : reason // ignore: cast_nullable_to_non_nullable
                  as String?,
        createdAt: null == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$PointsLogEntryDtoImpl implements _PointsLogEntryDto {
  const _$PointsLogEntryDtoImpl({
    required this.id,
    required this.source,
    required this.points,
    this.reason,
    required this.createdAt,
  });

  factory _$PointsLogEntryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$PointsLogEntryDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String source;
  @override
  final int points;
  @override
  final String? reason;
  @override
  final DateTime createdAt;

  @override
  String toString() {
    return 'PointsLogEntryDto(id: $id, source: $source, points: $points, reason: $reason, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$PointsLogEntryDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.source, source) || other.source == source) &&
            (identical(other.points, points) || other.points == points) &&
            (identical(other.reason, reason) || other.reason == reason) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, id, source, points, reason, createdAt);

  /// Create a copy of PointsLogEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$PointsLogEntryDtoImplCopyWith<_$PointsLogEntryDtoImpl> get copyWith =>
      __$$PointsLogEntryDtoImplCopyWithImpl<_$PointsLogEntryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$PointsLogEntryDtoImplToJson(this);
  }
}

abstract class _PointsLogEntryDto implements PointsLogEntryDto {
  const factory _PointsLogEntryDto({
    required final String id,
    required final String source,
    required final int points,
    final String? reason,
    required final DateTime createdAt,
  }) = _$PointsLogEntryDtoImpl;

  factory _PointsLogEntryDto.fromJson(Map<String, dynamic> json) =
      _$PointsLogEntryDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get source;
  @override
  int get points;
  @override
  String? get reason;
  @override
  DateTime get createdAt;

  /// Create a copy of PointsLogEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$PointsLogEntryDtoImplCopyWith<_$PointsLogEntryDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

PointsSummaryDto _$PointsSummaryDtoFromJson(Map<String, dynamic> json) {
  return _PointsSummaryDto.fromJson(json);
}

/// @nodoc
mixin _$PointsSummaryDto {
  int get totalPoints => throw _privateConstructorUsedError;
  List<PointsLogEntryDto> get history => throw _privateConstructorUsedError;

  /// Serializes this PointsSummaryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of PointsSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $PointsSummaryDtoCopyWith<PointsSummaryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $PointsSummaryDtoCopyWith<$Res> {
  factory $PointsSummaryDtoCopyWith(
    PointsSummaryDto value,
    $Res Function(PointsSummaryDto) then,
  ) = _$PointsSummaryDtoCopyWithImpl<$Res, PointsSummaryDto>;
  @useResult
  $Res call({int totalPoints, List<PointsLogEntryDto> history});
}

/// @nodoc
class _$PointsSummaryDtoCopyWithImpl<$Res, $Val extends PointsSummaryDto>
    implements $PointsSummaryDtoCopyWith<$Res> {
  _$PointsSummaryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of PointsSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? totalPoints = null, Object? history = null}) {
    return _then(
      _value.copyWith(
            totalPoints: null == totalPoints
                ? _value.totalPoints
                : totalPoints // ignore: cast_nullable_to_non_nullable
                      as int,
            history: null == history
                ? _value.history
                : history // ignore: cast_nullable_to_non_nullable
                      as List<PointsLogEntryDto>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$PointsSummaryDtoImplCopyWith<$Res>
    implements $PointsSummaryDtoCopyWith<$Res> {
  factory _$$PointsSummaryDtoImplCopyWith(
    _$PointsSummaryDtoImpl value,
    $Res Function(_$PointsSummaryDtoImpl) then,
  ) = __$$PointsSummaryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({int totalPoints, List<PointsLogEntryDto> history});
}

/// @nodoc
class __$$PointsSummaryDtoImplCopyWithImpl<$Res>
    extends _$PointsSummaryDtoCopyWithImpl<$Res, _$PointsSummaryDtoImpl>
    implements _$$PointsSummaryDtoImplCopyWith<$Res> {
  __$$PointsSummaryDtoImplCopyWithImpl(
    _$PointsSummaryDtoImpl _value,
    $Res Function(_$PointsSummaryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of PointsSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? totalPoints = null, Object? history = null}) {
    return _then(
      _$PointsSummaryDtoImpl(
        totalPoints: null == totalPoints
            ? _value.totalPoints
            : totalPoints // ignore: cast_nullable_to_non_nullable
                  as int,
        history: null == history
            ? _value._history
            : history // ignore: cast_nullable_to_non_nullable
                  as List<PointsLogEntryDto>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$PointsSummaryDtoImpl implements _PointsSummaryDto {
  const _$PointsSummaryDtoImpl({
    required this.totalPoints,
    final List<PointsLogEntryDto> history = const [],
  }) : _history = history;

  factory _$PointsSummaryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$PointsSummaryDtoImplFromJson(json);

  @override
  final int totalPoints;
  final List<PointsLogEntryDto> _history;
  @override
  @JsonKey()
  List<PointsLogEntryDto> get history {
    if (_history is EqualUnmodifiableListView) return _history;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_history);
  }

  @override
  String toString() {
    return 'PointsSummaryDto(totalPoints: $totalPoints, history: $history)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$PointsSummaryDtoImpl &&
            (identical(other.totalPoints, totalPoints) ||
                other.totalPoints == totalPoints) &&
            const DeepCollectionEquality().equals(other._history, _history));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    totalPoints,
    const DeepCollectionEquality().hash(_history),
  );

  /// Create a copy of PointsSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$PointsSummaryDtoImplCopyWith<_$PointsSummaryDtoImpl> get copyWith =>
      __$$PointsSummaryDtoImplCopyWithImpl<_$PointsSummaryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$PointsSummaryDtoImplToJson(this);
  }
}

abstract class _PointsSummaryDto implements PointsSummaryDto {
  const factory _PointsSummaryDto({
    required final int totalPoints,
    final List<PointsLogEntryDto> history,
  }) = _$PointsSummaryDtoImpl;

  factory _PointsSummaryDto.fromJson(Map<String, dynamic> json) =
      _$PointsSummaryDtoImpl.fromJson;

  @override
  int get totalPoints;
  @override
  List<PointsLogEntryDto> get history;

  /// Create a copy of PointsSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$PointsSummaryDtoImplCopyWith<_$PointsSummaryDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

LeaderboardEntryDto _$LeaderboardEntryDtoFromJson(Map<String, dynamic> json) {
  return _LeaderboardEntryDto.fromJson(json);
}

/// @nodoc
mixin _$LeaderboardEntryDto {
  int get rank => throw _privateConstructorUsedError;
  String get userId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get username => throw _privateConstructorUsedError;
  int get points => throw _privateConstructorUsedError;

  /// Serializes this LeaderboardEntryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of LeaderboardEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $LeaderboardEntryDtoCopyWith<LeaderboardEntryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $LeaderboardEntryDtoCopyWith<$Res> {
  factory $LeaderboardEntryDtoCopyWith(
    LeaderboardEntryDto value,
    $Res Function(LeaderboardEntryDto) then,
  ) = _$LeaderboardEntryDtoCopyWithImpl<$Res, LeaderboardEntryDto>;
  @useResult
  $Res call({
    int rank,
    String userId,
    String name,
    String? username,
    int points,
  });
}

/// @nodoc
class _$LeaderboardEntryDtoCopyWithImpl<$Res, $Val extends LeaderboardEntryDto>
    implements $LeaderboardEntryDtoCopyWith<$Res> {
  _$LeaderboardEntryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of LeaderboardEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? rank = null,
    Object? userId = null,
    Object? name = null,
    Object? username = freezed,
    Object? points = null,
  }) {
    return _then(
      _value.copyWith(
            rank: null == rank
                ? _value.rank
                : rank // ignore: cast_nullable_to_non_nullable
                      as int,
            userId: null == userId
                ? _value.userId
                : userId // ignore: cast_nullable_to_non_nullable
                      as String,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            username: freezed == username
                ? _value.username
                : username // ignore: cast_nullable_to_non_nullable
                      as String?,
            points: null == points
                ? _value.points
                : points // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$LeaderboardEntryDtoImplCopyWith<$Res>
    implements $LeaderboardEntryDtoCopyWith<$Res> {
  factory _$$LeaderboardEntryDtoImplCopyWith(
    _$LeaderboardEntryDtoImpl value,
    $Res Function(_$LeaderboardEntryDtoImpl) then,
  ) = __$$LeaderboardEntryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    int rank,
    String userId,
    String name,
    String? username,
    int points,
  });
}

/// @nodoc
class __$$LeaderboardEntryDtoImplCopyWithImpl<$Res>
    extends _$LeaderboardEntryDtoCopyWithImpl<$Res, _$LeaderboardEntryDtoImpl>
    implements _$$LeaderboardEntryDtoImplCopyWith<$Res> {
  __$$LeaderboardEntryDtoImplCopyWithImpl(
    _$LeaderboardEntryDtoImpl _value,
    $Res Function(_$LeaderboardEntryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of LeaderboardEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? rank = null,
    Object? userId = null,
    Object? name = null,
    Object? username = freezed,
    Object? points = null,
  }) {
    return _then(
      _$LeaderboardEntryDtoImpl(
        rank: null == rank
            ? _value.rank
            : rank // ignore: cast_nullable_to_non_nullable
                  as int,
        userId: null == userId
            ? _value.userId
            : userId // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        username: freezed == username
            ? _value.username
            : username // ignore: cast_nullable_to_non_nullable
                  as String?,
        points: null == points
            ? _value.points
            : points // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$LeaderboardEntryDtoImpl implements _LeaderboardEntryDto {
  const _$LeaderboardEntryDtoImpl({
    required this.rank,
    required this.userId,
    required this.name,
    this.username,
    required this.points,
  });

  factory _$LeaderboardEntryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$LeaderboardEntryDtoImplFromJson(json);

  @override
  final int rank;
  @override
  final String userId;
  @override
  final String name;
  @override
  final String? username;
  @override
  final int points;

  @override
  String toString() {
    return 'LeaderboardEntryDto(rank: $rank, userId: $userId, name: $name, username: $username, points: $points)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$LeaderboardEntryDtoImpl &&
            (identical(other.rank, rank) || other.rank == rank) &&
            (identical(other.userId, userId) || other.userId == userId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.points, points) || other.points == points));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, rank, userId, name, username, points);

  /// Create a copy of LeaderboardEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$LeaderboardEntryDtoImplCopyWith<_$LeaderboardEntryDtoImpl> get copyWith =>
      __$$LeaderboardEntryDtoImplCopyWithImpl<_$LeaderboardEntryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$LeaderboardEntryDtoImplToJson(this);
  }
}

abstract class _LeaderboardEntryDto implements LeaderboardEntryDto {
  const factory _LeaderboardEntryDto({
    required final int rank,
    required final String userId,
    required final String name,
    final String? username,
    required final int points,
  }) = _$LeaderboardEntryDtoImpl;

  factory _LeaderboardEntryDto.fromJson(Map<String, dynamic> json) =
      _$LeaderboardEntryDtoImpl.fromJson;

  @override
  int get rank;
  @override
  String get userId;
  @override
  String get name;
  @override
  String? get username;
  @override
  int get points;

  /// Create a copy of LeaderboardEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$LeaderboardEntryDtoImplCopyWith<_$LeaderboardEntryDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
