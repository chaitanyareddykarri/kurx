// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'competition_dtos.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

SeriesDto _$SeriesDtoFromJson(Map<String, dynamic> json) {
  return _SeriesDto.fromJson(json);
}

/// @nodoc
mixin _$SeriesDto {
  String get id => throw _privateConstructorUsedError;
  String get orgId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String get slug => throw _privateConstructorUsedError;

  /// `recurring` or `editions`.
  String get mode => throw _privateConstructorUsedError;
  String get description => throw _privateConstructorUsedError;
  String? get bannerKey => throw _privateConstructorUsedError;
  String? get rrule => throw _privateConstructorUsedError;
  int get followerCount => throw _privateConstructorUsedError;
  int get memberCount => throw _privateConstructorUsedError;
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this SeriesDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of SeriesDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $SeriesDtoCopyWith<SeriesDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $SeriesDtoCopyWith<$Res> {
  factory $SeriesDtoCopyWith(SeriesDto value, $Res Function(SeriesDto) then) =
      _$SeriesDtoCopyWithImpl<$Res, SeriesDto>;
  @useResult
  $Res call({
    String id,
    String orgId,
    String name,
    String slug,
    String mode,
    String description,
    String? bannerKey,
    String? rrule,
    int followerCount,
    int memberCount,
    DateTime? createdAt,
  });
}

/// @nodoc
class _$SeriesDtoCopyWithImpl<$Res, $Val extends SeriesDto>
    implements $SeriesDtoCopyWith<$Res> {
  _$SeriesDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of SeriesDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = null,
    Object? name = null,
    Object? slug = null,
    Object? mode = null,
    Object? description = null,
    Object? bannerKey = freezed,
    Object? rrule = freezed,
    Object? followerCount = null,
    Object? memberCount = null,
    Object? createdAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            orgId: null == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            slug: null == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String,
            mode: null == mode
                ? _value.mode
                : mode // ignore: cast_nullable_to_non_nullable
                      as String,
            description: null == description
                ? _value.description
                : description // ignore: cast_nullable_to_non_nullable
                      as String,
            bannerKey: freezed == bannerKey
                ? _value.bannerKey
                : bannerKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            rrule: freezed == rrule
                ? _value.rrule
                : rrule // ignore: cast_nullable_to_non_nullable
                      as String?,
            followerCount: null == followerCount
                ? _value.followerCount
                : followerCount // ignore: cast_nullable_to_non_nullable
                      as int,
            memberCount: null == memberCount
                ? _value.memberCount
                : memberCount // ignore: cast_nullable_to_non_nullable
                      as int,
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
abstract class _$$SeriesDtoImplCopyWith<$Res>
    implements $SeriesDtoCopyWith<$Res> {
  factory _$$SeriesDtoImplCopyWith(
    _$SeriesDtoImpl value,
    $Res Function(_$SeriesDtoImpl) then,
  ) = __$$SeriesDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String orgId,
    String name,
    String slug,
    String mode,
    String description,
    String? bannerKey,
    String? rrule,
    int followerCount,
    int memberCount,
    DateTime? createdAt,
  });
}

/// @nodoc
class __$$SeriesDtoImplCopyWithImpl<$Res>
    extends _$SeriesDtoCopyWithImpl<$Res, _$SeriesDtoImpl>
    implements _$$SeriesDtoImplCopyWith<$Res> {
  __$$SeriesDtoImplCopyWithImpl(
    _$SeriesDtoImpl _value,
    $Res Function(_$SeriesDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of SeriesDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = null,
    Object? name = null,
    Object? slug = null,
    Object? mode = null,
    Object? description = null,
    Object? bannerKey = freezed,
    Object? rrule = freezed,
    Object? followerCount = null,
    Object? memberCount = null,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$SeriesDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        orgId: null == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: null == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String,
        mode: null == mode
            ? _value.mode
            : mode // ignore: cast_nullable_to_non_nullable
                  as String,
        description: null == description
            ? _value.description
            : description // ignore: cast_nullable_to_non_nullable
                  as String,
        bannerKey: freezed == bannerKey
            ? _value.bannerKey
            : bannerKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        rrule: freezed == rrule
            ? _value.rrule
            : rrule // ignore: cast_nullable_to_non_nullable
                  as String?,
        followerCount: null == followerCount
            ? _value.followerCount
            : followerCount // ignore: cast_nullable_to_non_nullable
                  as int,
        memberCount: null == memberCount
            ? _value.memberCount
            : memberCount // ignore: cast_nullable_to_non_nullable
                  as int,
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
class _$SeriesDtoImpl implements _SeriesDto {
  const _$SeriesDtoImpl({
    required this.id,
    required this.orgId,
    required this.name,
    this.slug = '',
    this.mode = 'recurring',
    this.description = '',
    this.bannerKey,
    this.rrule,
    this.followerCount = 0,
    this.memberCount = 0,
    this.createdAt,
  });

  factory _$SeriesDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$SeriesDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String orgId;
  @override
  final String name;
  @override
  @JsonKey()
  final String slug;

  /// `recurring` or `editions`.
  @override
  @JsonKey()
  final String mode;
  @override
  @JsonKey()
  final String description;
  @override
  final String? bannerKey;
  @override
  final String? rrule;
  @override
  @JsonKey()
  final int followerCount;
  @override
  @JsonKey()
  final int memberCount;
  @override
  final DateTime? createdAt;

  @override
  String toString() {
    return 'SeriesDto(id: $id, orgId: $orgId, name: $name, slug: $slug, mode: $mode, description: $description, bannerKey: $bannerKey, rrule: $rrule, followerCount: $followerCount, memberCount: $memberCount, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$SeriesDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.mode, mode) || other.mode == mode) &&
            (identical(other.description, description) ||
                other.description == description) &&
            (identical(other.bannerKey, bannerKey) ||
                other.bannerKey == bannerKey) &&
            (identical(other.rrule, rrule) || other.rrule == rrule) &&
            (identical(other.followerCount, followerCount) ||
                other.followerCount == followerCount) &&
            (identical(other.memberCount, memberCount) ||
                other.memberCount == memberCount) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    orgId,
    name,
    slug,
    mode,
    description,
    bannerKey,
    rrule,
    followerCount,
    memberCount,
    createdAt,
  );

  /// Create a copy of SeriesDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$SeriesDtoImplCopyWith<_$SeriesDtoImpl> get copyWith =>
      __$$SeriesDtoImplCopyWithImpl<_$SeriesDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$SeriesDtoImplToJson(this);
  }
}

abstract class _SeriesDto implements SeriesDto {
  const factory _SeriesDto({
    required final String id,
    required final String orgId,
    required final String name,
    final String slug,
    final String mode,
    final String description,
    final String? bannerKey,
    final String? rrule,
    final int followerCount,
    final int memberCount,
    final DateTime? createdAt,
  }) = _$SeriesDtoImpl;

  factory _SeriesDto.fromJson(Map<String, dynamic> json) =
      _$SeriesDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get orgId;
  @override
  String get name;
  @override
  String get slug;

  /// `recurring` or `editions`.
  @override
  String get mode;
  @override
  String get description;
  @override
  String? get bannerKey;
  @override
  String? get rrule;
  @override
  int get followerCount;
  @override
  int get memberCount;
  @override
  DateTime? get createdAt;

  /// Create a copy of SeriesDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$SeriesDtoImplCopyWith<_$SeriesDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

SeriesMemberDto _$SeriesMemberDtoFromJson(Map<String, dynamic> json) {
  return _SeriesMemberDto.fromJson(json);
}

/// @nodoc
mixin _$SeriesMemberDto {
  String get eventId => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String get slug => throw _privateConstructorUsedError;
  int? get editionOrdinal => throw _privateConstructorUsedError;
  String? get editionLabel => throw _privateConstructorUsedError;
  DateTime? get startsAt => throw _privateConstructorUsedError;
  String get timezone => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;

  /// Serializes this SeriesMemberDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of SeriesMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $SeriesMemberDtoCopyWith<SeriesMemberDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $SeriesMemberDtoCopyWith<$Res> {
  factory $SeriesMemberDtoCopyWith(
    SeriesMemberDto value,
    $Res Function(SeriesMemberDto) then,
  ) = _$SeriesMemberDtoCopyWithImpl<$Res, SeriesMemberDto>;
  @useResult
  $Res call({
    String eventId,
    String title,
    String slug,
    int? editionOrdinal,
    String? editionLabel,
    DateTime? startsAt,
    String timezone,
    String status,
  });
}

/// @nodoc
class _$SeriesMemberDtoCopyWithImpl<$Res, $Val extends SeriesMemberDto>
    implements $SeriesMemberDtoCopyWith<$Res> {
  _$SeriesMemberDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of SeriesMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? eventId = null,
    Object? title = null,
    Object? slug = null,
    Object? editionOrdinal = freezed,
    Object? editionLabel = freezed,
    Object? startsAt = freezed,
    Object? timezone = null,
    Object? status = null,
  }) {
    return _then(
      _value.copyWith(
            eventId: null == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String,
            title: null == title
                ? _value.title
                : title // ignore: cast_nullable_to_non_nullable
                      as String,
            slug: null == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String,
            editionOrdinal: freezed == editionOrdinal
                ? _value.editionOrdinal
                : editionOrdinal // ignore: cast_nullable_to_non_nullable
                      as int?,
            editionLabel: freezed == editionLabel
                ? _value.editionLabel
                : editionLabel // ignore: cast_nullable_to_non_nullable
                      as String?,
            startsAt: freezed == startsAt
                ? _value.startsAt
                : startsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            timezone: null == timezone
                ? _value.timezone
                : timezone // ignore: cast_nullable_to_non_nullable
                      as String,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$SeriesMemberDtoImplCopyWith<$Res>
    implements $SeriesMemberDtoCopyWith<$Res> {
  factory _$$SeriesMemberDtoImplCopyWith(
    _$SeriesMemberDtoImpl value,
    $Res Function(_$SeriesMemberDtoImpl) then,
  ) = __$$SeriesMemberDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String eventId,
    String title,
    String slug,
    int? editionOrdinal,
    String? editionLabel,
    DateTime? startsAt,
    String timezone,
    String status,
  });
}

/// @nodoc
class __$$SeriesMemberDtoImplCopyWithImpl<$Res>
    extends _$SeriesMemberDtoCopyWithImpl<$Res, _$SeriesMemberDtoImpl>
    implements _$$SeriesMemberDtoImplCopyWith<$Res> {
  __$$SeriesMemberDtoImplCopyWithImpl(
    _$SeriesMemberDtoImpl _value,
    $Res Function(_$SeriesMemberDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of SeriesMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? eventId = null,
    Object? title = null,
    Object? slug = null,
    Object? editionOrdinal = freezed,
    Object? editionLabel = freezed,
    Object? startsAt = freezed,
    Object? timezone = null,
    Object? status = null,
  }) {
    return _then(
      _$SeriesMemberDtoImpl(
        eventId: null == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String,
        title: null == title
            ? _value.title
            : title // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: null == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String,
        editionOrdinal: freezed == editionOrdinal
            ? _value.editionOrdinal
            : editionOrdinal // ignore: cast_nullable_to_non_nullable
                  as int?,
        editionLabel: freezed == editionLabel
            ? _value.editionLabel
            : editionLabel // ignore: cast_nullable_to_non_nullable
                  as String?,
        startsAt: freezed == startsAt
            ? _value.startsAt
            : startsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        timezone: null == timezone
            ? _value.timezone
            : timezone // ignore: cast_nullable_to_non_nullable
                  as String,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$SeriesMemberDtoImpl implements _SeriesMemberDto {
  const _$SeriesMemberDtoImpl({
    required this.eventId,
    required this.title,
    this.slug = '',
    this.editionOrdinal,
    this.editionLabel,
    this.startsAt,
    this.timezone = '',
    this.status = '',
  });

  factory _$SeriesMemberDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$SeriesMemberDtoImplFromJson(json);

  @override
  final String eventId;
  @override
  final String title;
  @override
  @JsonKey()
  final String slug;
  @override
  final int? editionOrdinal;
  @override
  final String? editionLabel;
  @override
  final DateTime? startsAt;
  @override
  @JsonKey()
  final String timezone;
  @override
  @JsonKey()
  final String status;

  @override
  String toString() {
    return 'SeriesMemberDto(eventId: $eventId, title: $title, slug: $slug, editionOrdinal: $editionOrdinal, editionLabel: $editionLabel, startsAt: $startsAt, timezone: $timezone, status: $status)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$SeriesMemberDtoImpl &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.editionOrdinal, editionOrdinal) ||
                other.editionOrdinal == editionOrdinal) &&
            (identical(other.editionLabel, editionLabel) ||
                other.editionLabel == editionLabel) &&
            (identical(other.startsAt, startsAt) ||
                other.startsAt == startsAt) &&
            (identical(other.timezone, timezone) ||
                other.timezone == timezone) &&
            (identical(other.status, status) || other.status == status));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    eventId,
    title,
    slug,
    editionOrdinal,
    editionLabel,
    startsAt,
    timezone,
    status,
  );

  /// Create a copy of SeriesMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$SeriesMemberDtoImplCopyWith<_$SeriesMemberDtoImpl> get copyWith =>
      __$$SeriesMemberDtoImplCopyWithImpl<_$SeriesMemberDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$SeriesMemberDtoImplToJson(this);
  }
}

abstract class _SeriesMemberDto implements SeriesMemberDto {
  const factory _SeriesMemberDto({
    required final String eventId,
    required final String title,
    final String slug,
    final int? editionOrdinal,
    final String? editionLabel,
    final DateTime? startsAt,
    final String timezone,
    final String status,
  }) = _$SeriesMemberDtoImpl;

  factory _SeriesMemberDto.fromJson(Map<String, dynamic> json) =
      _$SeriesMemberDtoImpl.fromJson;

  @override
  String get eventId;
  @override
  String get title;
  @override
  String get slug;
  @override
  int? get editionOrdinal;
  @override
  String? get editionLabel;
  @override
  DateTime? get startsAt;
  @override
  String get timezone;
  @override
  String get status;

  /// Create a copy of SeriesMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$SeriesMemberDtoImplCopyWith<_$SeriesMemberDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

StageDto _$StageDtoFromJson(Map<String, dynamic> json) {
  return _StageDto.fromJson(json);
}

/// @nodoc
mixin _$StageDto {
  String get id => throw _privateConstructorUsedError;
  String get eventId => throw _privateConstructorUsedError;
  int get sequence => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;

  /// `knockout` / `league` / `judged` / `freeform`…
  String get format => throw _privateConstructorUsedError;
  String get participantSource => throw _privateConstructorUsedError;
  String? get advancedFromStageId => throw _privateConstructorUsedError;
  String get advancementRule => throw _privateConstructorUsedError;
  int? get advancementThreshold => throw _privateConstructorUsedError;
  String? get scoringPolicyId => throw _privateConstructorUsedError;
  DateTime? get startsAt => throw _privateConstructorUsedError;
  DateTime? get endsAt => throw _privateConstructorUsedError;
  String? get venueId => throw _privateConstructorUsedError;
  String get mode => throw _privateConstructorUsedError;

  /// `public` / `participants` / `private` — drives whether results are shown at all.
  String get resultsVisibility => throw _privateConstructorUsedError;

  /// `draft` / `open` / `running` / `completed` / `published`.
  String get state => throw _privateConstructorUsedError;
  int get participantCount => throw _privateConstructorUsedError;

  /// Serializes this StageDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of StageDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $StageDtoCopyWith<StageDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $StageDtoCopyWith<$Res> {
  factory $StageDtoCopyWith(StageDto value, $Res Function(StageDto) then) =
      _$StageDtoCopyWithImpl<$Res, StageDto>;
  @useResult
  $Res call({
    String id,
    String eventId,
    int sequence,
    String name,
    String format,
    String participantSource,
    String? advancedFromStageId,
    String advancementRule,
    int? advancementThreshold,
    String? scoringPolicyId,
    DateTime? startsAt,
    DateTime? endsAt,
    String? venueId,
    String mode,
    String resultsVisibility,
    String state,
    int participantCount,
  });
}

/// @nodoc
class _$StageDtoCopyWithImpl<$Res, $Val extends StageDto>
    implements $StageDtoCopyWith<$Res> {
  _$StageDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of StageDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? sequence = null,
    Object? name = null,
    Object? format = null,
    Object? participantSource = null,
    Object? advancedFromStageId = freezed,
    Object? advancementRule = null,
    Object? advancementThreshold = freezed,
    Object? scoringPolicyId = freezed,
    Object? startsAt = freezed,
    Object? endsAt = freezed,
    Object? venueId = freezed,
    Object? mode = null,
    Object? resultsVisibility = null,
    Object? state = null,
    Object? participantCount = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            eventId: null == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String,
            sequence: null == sequence
                ? _value.sequence
                : sequence // ignore: cast_nullable_to_non_nullable
                      as int,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            format: null == format
                ? _value.format
                : format // ignore: cast_nullable_to_non_nullable
                      as String,
            participantSource: null == participantSource
                ? _value.participantSource
                : participantSource // ignore: cast_nullable_to_non_nullable
                      as String,
            advancedFromStageId: freezed == advancedFromStageId
                ? _value.advancedFromStageId
                : advancedFromStageId // ignore: cast_nullable_to_non_nullable
                      as String?,
            advancementRule: null == advancementRule
                ? _value.advancementRule
                : advancementRule // ignore: cast_nullable_to_non_nullable
                      as String,
            advancementThreshold: freezed == advancementThreshold
                ? _value.advancementThreshold
                : advancementThreshold // ignore: cast_nullable_to_non_nullable
                      as int?,
            scoringPolicyId: freezed == scoringPolicyId
                ? _value.scoringPolicyId
                : scoringPolicyId // ignore: cast_nullable_to_non_nullable
                      as String?,
            startsAt: freezed == startsAt
                ? _value.startsAt
                : startsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            endsAt: freezed == endsAt
                ? _value.endsAt
                : endsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            venueId: freezed == venueId
                ? _value.venueId
                : venueId // ignore: cast_nullable_to_non_nullable
                      as String?,
            mode: null == mode
                ? _value.mode
                : mode // ignore: cast_nullable_to_non_nullable
                      as String,
            resultsVisibility: null == resultsVisibility
                ? _value.resultsVisibility
                : resultsVisibility // ignore: cast_nullable_to_non_nullable
                      as String,
            state: null == state
                ? _value.state
                : state // ignore: cast_nullable_to_non_nullable
                      as String,
            participantCount: null == participantCount
                ? _value.participantCount
                : participantCount // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$StageDtoImplCopyWith<$Res>
    implements $StageDtoCopyWith<$Res> {
  factory _$$StageDtoImplCopyWith(
    _$StageDtoImpl value,
    $Res Function(_$StageDtoImpl) then,
  ) = __$$StageDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String eventId,
    int sequence,
    String name,
    String format,
    String participantSource,
    String? advancedFromStageId,
    String advancementRule,
    int? advancementThreshold,
    String? scoringPolicyId,
    DateTime? startsAt,
    DateTime? endsAt,
    String? venueId,
    String mode,
    String resultsVisibility,
    String state,
    int participantCount,
  });
}

/// @nodoc
class __$$StageDtoImplCopyWithImpl<$Res>
    extends _$StageDtoCopyWithImpl<$Res, _$StageDtoImpl>
    implements _$$StageDtoImplCopyWith<$Res> {
  __$$StageDtoImplCopyWithImpl(
    _$StageDtoImpl _value,
    $Res Function(_$StageDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of StageDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? sequence = null,
    Object? name = null,
    Object? format = null,
    Object? participantSource = null,
    Object? advancedFromStageId = freezed,
    Object? advancementRule = null,
    Object? advancementThreshold = freezed,
    Object? scoringPolicyId = freezed,
    Object? startsAt = freezed,
    Object? endsAt = freezed,
    Object? venueId = freezed,
    Object? mode = null,
    Object? resultsVisibility = null,
    Object? state = null,
    Object? participantCount = null,
  }) {
    return _then(
      _$StageDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: null == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String,
        sequence: null == sequence
            ? _value.sequence
            : sequence // ignore: cast_nullable_to_non_nullable
                  as int,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        format: null == format
            ? _value.format
            : format // ignore: cast_nullable_to_non_nullable
                  as String,
        participantSource: null == participantSource
            ? _value.participantSource
            : participantSource // ignore: cast_nullable_to_non_nullable
                  as String,
        advancedFromStageId: freezed == advancedFromStageId
            ? _value.advancedFromStageId
            : advancedFromStageId // ignore: cast_nullable_to_non_nullable
                  as String?,
        advancementRule: null == advancementRule
            ? _value.advancementRule
            : advancementRule // ignore: cast_nullable_to_non_nullable
                  as String,
        advancementThreshold: freezed == advancementThreshold
            ? _value.advancementThreshold
            : advancementThreshold // ignore: cast_nullable_to_non_nullable
                  as int?,
        scoringPolicyId: freezed == scoringPolicyId
            ? _value.scoringPolicyId
            : scoringPolicyId // ignore: cast_nullable_to_non_nullable
                  as String?,
        startsAt: freezed == startsAt
            ? _value.startsAt
            : startsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        endsAt: freezed == endsAt
            ? _value.endsAt
            : endsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        venueId: freezed == venueId
            ? _value.venueId
            : venueId // ignore: cast_nullable_to_non_nullable
                  as String?,
        mode: null == mode
            ? _value.mode
            : mode // ignore: cast_nullable_to_non_nullable
                  as String,
        resultsVisibility: null == resultsVisibility
            ? _value.resultsVisibility
            : resultsVisibility // ignore: cast_nullable_to_non_nullable
                  as String,
        state: null == state
            ? _value.state
            : state // ignore: cast_nullable_to_non_nullable
                  as String,
        participantCount: null == participantCount
            ? _value.participantCount
            : participantCount // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$StageDtoImpl implements _StageDto {
  const _$StageDtoImpl({
    required this.id,
    required this.eventId,
    this.sequence = 0,
    required this.name,
    this.format = '',
    this.participantSource = '',
    this.advancedFromStageId,
    this.advancementRule = '',
    this.advancementThreshold,
    this.scoringPolicyId,
    this.startsAt,
    this.endsAt,
    this.venueId,
    this.mode = '',
    this.resultsVisibility = 'private',
    this.state = 'draft',
    this.participantCount = 0,
  });

  factory _$StageDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$StageDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String eventId;
  @override
  @JsonKey()
  final int sequence;
  @override
  final String name;

  /// `knockout` / `league` / `judged` / `freeform`…
  @override
  @JsonKey()
  final String format;
  @override
  @JsonKey()
  final String participantSource;
  @override
  final String? advancedFromStageId;
  @override
  @JsonKey()
  final String advancementRule;
  @override
  final int? advancementThreshold;
  @override
  final String? scoringPolicyId;
  @override
  final DateTime? startsAt;
  @override
  final DateTime? endsAt;
  @override
  final String? venueId;
  @override
  @JsonKey()
  final String mode;

  /// `public` / `participants` / `private` — drives whether results are shown at all.
  @override
  @JsonKey()
  final String resultsVisibility;

  /// `draft` / `open` / `running` / `completed` / `published`.
  @override
  @JsonKey()
  final String state;
  @override
  @JsonKey()
  final int participantCount;

  @override
  String toString() {
    return 'StageDto(id: $id, eventId: $eventId, sequence: $sequence, name: $name, format: $format, participantSource: $participantSource, advancedFromStageId: $advancedFromStageId, advancementRule: $advancementRule, advancementThreshold: $advancementThreshold, scoringPolicyId: $scoringPolicyId, startsAt: $startsAt, endsAt: $endsAt, venueId: $venueId, mode: $mode, resultsVisibility: $resultsVisibility, state: $state, participantCount: $participantCount)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$StageDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.sequence, sequence) ||
                other.sequence == sequence) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.format, format) || other.format == format) &&
            (identical(other.participantSource, participantSource) ||
                other.participantSource == participantSource) &&
            (identical(other.advancedFromStageId, advancedFromStageId) ||
                other.advancedFromStageId == advancedFromStageId) &&
            (identical(other.advancementRule, advancementRule) ||
                other.advancementRule == advancementRule) &&
            (identical(other.advancementThreshold, advancementThreshold) ||
                other.advancementThreshold == advancementThreshold) &&
            (identical(other.scoringPolicyId, scoringPolicyId) ||
                other.scoringPolicyId == scoringPolicyId) &&
            (identical(other.startsAt, startsAt) ||
                other.startsAt == startsAt) &&
            (identical(other.endsAt, endsAt) || other.endsAt == endsAt) &&
            (identical(other.venueId, venueId) || other.venueId == venueId) &&
            (identical(other.mode, mode) || other.mode == mode) &&
            (identical(other.resultsVisibility, resultsVisibility) ||
                other.resultsVisibility == resultsVisibility) &&
            (identical(other.state, state) || other.state == state) &&
            (identical(other.participantCount, participantCount) ||
                other.participantCount == participantCount));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    sequence,
    name,
    format,
    participantSource,
    advancedFromStageId,
    advancementRule,
    advancementThreshold,
    scoringPolicyId,
    startsAt,
    endsAt,
    venueId,
    mode,
    resultsVisibility,
    state,
    participantCount,
  );

  /// Create a copy of StageDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$StageDtoImplCopyWith<_$StageDtoImpl> get copyWith =>
      __$$StageDtoImplCopyWithImpl<_$StageDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$StageDtoImplToJson(this);
  }
}

abstract class _StageDto implements StageDto {
  const factory _StageDto({
    required final String id,
    required final String eventId,
    final int sequence,
    required final String name,
    final String format,
    final String participantSource,
    final String? advancedFromStageId,
    final String advancementRule,
    final int? advancementThreshold,
    final String? scoringPolicyId,
    final DateTime? startsAt,
    final DateTime? endsAt,
    final String? venueId,
    final String mode,
    final String resultsVisibility,
    final String state,
    final int participantCount,
  }) = _$StageDtoImpl;

  factory _StageDto.fromJson(Map<String, dynamic> json) =
      _$StageDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get eventId;
  @override
  int get sequence;
  @override
  String get name;

  /// `knockout` / `league` / `judged` / `freeform`…
  @override
  String get format;
  @override
  String get participantSource;
  @override
  String? get advancedFromStageId;
  @override
  String get advancementRule;
  @override
  int? get advancementThreshold;
  @override
  String? get scoringPolicyId;
  @override
  DateTime? get startsAt;
  @override
  DateTime? get endsAt;
  @override
  String? get venueId;
  @override
  String get mode;

  /// `public` / `participants` / `private` — drives whether results are shown at all.
  @override
  String get resultsVisibility;

  /// `draft` / `open` / `running` / `completed` / `published`.
  @override
  String get state;
  @override
  int get participantCount;

  /// Create a copy of StageDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$StageDtoImplCopyWith<_$StageDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

StageParticipantDto _$StageParticipantDtoFromJson(Map<String, dynamic> json) {
  return _StageParticipantDto.fromJson(json);
}

/// @nodoc
mixin _$StageParticipantDto {
  String get id => throw _privateConstructorUsedError;
  String get stageId => throw _privateConstructorUsedError;
  String get subjectType => throw _privateConstructorUsedError;
  String get subjectId => throw _privateConstructorUsedError;
  String get subjectName => throw _privateConstructorUsedError;
  int? get seed => throw _privateConstructorUsedError;
  bool get advanced => throw _privateConstructorUsedError;

  /// Serializes this StageParticipantDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of StageParticipantDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $StageParticipantDtoCopyWith<StageParticipantDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $StageParticipantDtoCopyWith<$Res> {
  factory $StageParticipantDtoCopyWith(
    StageParticipantDto value,
    $Res Function(StageParticipantDto) then,
  ) = _$StageParticipantDtoCopyWithImpl<$Res, StageParticipantDto>;
  @useResult
  $Res call({
    String id,
    String stageId,
    String subjectType,
    String subjectId,
    String subjectName,
    int? seed,
    bool advanced,
  });
}

/// @nodoc
class _$StageParticipantDtoCopyWithImpl<$Res, $Val extends StageParticipantDto>
    implements $StageParticipantDtoCopyWith<$Res> {
  _$StageParticipantDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of StageParticipantDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? stageId = null,
    Object? subjectType = null,
    Object? subjectId = null,
    Object? subjectName = null,
    Object? seed = freezed,
    Object? advanced = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            stageId: null == stageId
                ? _value.stageId
                : stageId // ignore: cast_nullable_to_non_nullable
                      as String,
            subjectType: null == subjectType
                ? _value.subjectType
                : subjectType // ignore: cast_nullable_to_non_nullable
                      as String,
            subjectId: null == subjectId
                ? _value.subjectId
                : subjectId // ignore: cast_nullable_to_non_nullable
                      as String,
            subjectName: null == subjectName
                ? _value.subjectName
                : subjectName // ignore: cast_nullable_to_non_nullable
                      as String,
            seed: freezed == seed
                ? _value.seed
                : seed // ignore: cast_nullable_to_non_nullable
                      as int?,
            advanced: null == advanced
                ? _value.advanced
                : advanced // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$StageParticipantDtoImplCopyWith<$Res>
    implements $StageParticipantDtoCopyWith<$Res> {
  factory _$$StageParticipantDtoImplCopyWith(
    _$StageParticipantDtoImpl value,
    $Res Function(_$StageParticipantDtoImpl) then,
  ) = __$$StageParticipantDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String stageId,
    String subjectType,
    String subjectId,
    String subjectName,
    int? seed,
    bool advanced,
  });
}

/// @nodoc
class __$$StageParticipantDtoImplCopyWithImpl<$Res>
    extends _$StageParticipantDtoCopyWithImpl<$Res, _$StageParticipantDtoImpl>
    implements _$$StageParticipantDtoImplCopyWith<$Res> {
  __$$StageParticipantDtoImplCopyWithImpl(
    _$StageParticipantDtoImpl _value,
    $Res Function(_$StageParticipantDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of StageParticipantDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? stageId = null,
    Object? subjectType = null,
    Object? subjectId = null,
    Object? subjectName = null,
    Object? seed = freezed,
    Object? advanced = null,
  }) {
    return _then(
      _$StageParticipantDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        stageId: null == stageId
            ? _value.stageId
            : stageId // ignore: cast_nullable_to_non_nullable
                  as String,
        subjectType: null == subjectType
            ? _value.subjectType
            : subjectType // ignore: cast_nullable_to_non_nullable
                  as String,
        subjectId: null == subjectId
            ? _value.subjectId
            : subjectId // ignore: cast_nullable_to_non_nullable
                  as String,
        subjectName: null == subjectName
            ? _value.subjectName
            : subjectName // ignore: cast_nullable_to_non_nullable
                  as String,
        seed: freezed == seed
            ? _value.seed
            : seed // ignore: cast_nullable_to_non_nullable
                  as int?,
        advanced: null == advanced
            ? _value.advanced
            : advanced // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$StageParticipantDtoImpl implements _StageParticipantDto {
  const _$StageParticipantDtoImpl({
    required this.id,
    required this.stageId,
    this.subjectType = '',
    required this.subjectId,
    this.subjectName = '',
    this.seed,
    this.advanced = false,
  });

  factory _$StageParticipantDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$StageParticipantDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String stageId;
  @override
  @JsonKey()
  final String subjectType;
  @override
  final String subjectId;
  @override
  @JsonKey()
  final String subjectName;
  @override
  final int? seed;
  @override
  @JsonKey()
  final bool advanced;

  @override
  String toString() {
    return 'StageParticipantDto(id: $id, stageId: $stageId, subjectType: $subjectType, subjectId: $subjectId, subjectName: $subjectName, seed: $seed, advanced: $advanced)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$StageParticipantDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.stageId, stageId) || other.stageId == stageId) &&
            (identical(other.subjectType, subjectType) ||
                other.subjectType == subjectType) &&
            (identical(other.subjectId, subjectId) ||
                other.subjectId == subjectId) &&
            (identical(other.subjectName, subjectName) ||
                other.subjectName == subjectName) &&
            (identical(other.seed, seed) || other.seed == seed) &&
            (identical(other.advanced, advanced) ||
                other.advanced == advanced));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    stageId,
    subjectType,
    subjectId,
    subjectName,
    seed,
    advanced,
  );

  /// Create a copy of StageParticipantDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$StageParticipantDtoImplCopyWith<_$StageParticipantDtoImpl> get copyWith =>
      __$$StageParticipantDtoImplCopyWithImpl<_$StageParticipantDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$StageParticipantDtoImplToJson(this);
  }
}

abstract class _StageParticipantDto implements StageParticipantDto {
  const factory _StageParticipantDto({
    required final String id,
    required final String stageId,
    final String subjectType,
    required final String subjectId,
    final String subjectName,
    final int? seed,
    final bool advanced,
  }) = _$StageParticipantDtoImpl;

  factory _StageParticipantDto.fromJson(Map<String, dynamic> json) =
      _$StageParticipantDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get stageId;
  @override
  String get subjectType;
  @override
  String get subjectId;
  @override
  String get subjectName;
  @override
  int? get seed;
  @override
  bool get advanced;

  /// Create a copy of StageParticipantDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$StageParticipantDtoImplCopyWith<_$StageParticipantDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

StageResultDto _$StageResultDtoFromJson(Map<String, dynamic> json) {
  return _StageResultDto.fromJson(json);
}

/// @nodoc
mixin _$StageResultDto {
  String get id => throw _privateConstructorUsedError;
  String get stageId => throw _privateConstructorUsedError;
  String get subjectType => throw _privateConstructorUsedError;
  String get subjectId => throw _privateConstructorUsedError;
  String get subjectName => throw _privateConstructorUsedError;
  int get rank => throw _privateConstructorUsedError;

  /// `decimal?` over the wire — read as `num` then widened, never as `int`.
  double? get finalScore => throw _privateConstructorUsedError;
  String? get scoreBreakdownJson => throw _privateConstructorUsedError;
  String get state => throw _privateConstructorUsedError;
  DateTime? get publishedAt => throw _privateConstructorUsedError;
  int get correctionCount => throw _privateConstructorUsedError;

  /// Serializes this StageResultDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of StageResultDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $StageResultDtoCopyWith<StageResultDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $StageResultDtoCopyWith<$Res> {
  factory $StageResultDtoCopyWith(
    StageResultDto value,
    $Res Function(StageResultDto) then,
  ) = _$StageResultDtoCopyWithImpl<$Res, StageResultDto>;
  @useResult
  $Res call({
    String id,
    String stageId,
    String subjectType,
    String subjectId,
    String subjectName,
    int rank,
    double? finalScore,
    String? scoreBreakdownJson,
    String state,
    DateTime? publishedAt,
    int correctionCount,
  });
}

/// @nodoc
class _$StageResultDtoCopyWithImpl<$Res, $Val extends StageResultDto>
    implements $StageResultDtoCopyWith<$Res> {
  _$StageResultDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of StageResultDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? stageId = null,
    Object? subjectType = null,
    Object? subjectId = null,
    Object? subjectName = null,
    Object? rank = null,
    Object? finalScore = freezed,
    Object? scoreBreakdownJson = freezed,
    Object? state = null,
    Object? publishedAt = freezed,
    Object? correctionCount = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            stageId: null == stageId
                ? _value.stageId
                : stageId // ignore: cast_nullable_to_non_nullable
                      as String,
            subjectType: null == subjectType
                ? _value.subjectType
                : subjectType // ignore: cast_nullable_to_non_nullable
                      as String,
            subjectId: null == subjectId
                ? _value.subjectId
                : subjectId // ignore: cast_nullable_to_non_nullable
                      as String,
            subjectName: null == subjectName
                ? _value.subjectName
                : subjectName // ignore: cast_nullable_to_non_nullable
                      as String,
            rank: null == rank
                ? _value.rank
                : rank // ignore: cast_nullable_to_non_nullable
                      as int,
            finalScore: freezed == finalScore
                ? _value.finalScore
                : finalScore // ignore: cast_nullable_to_non_nullable
                      as double?,
            scoreBreakdownJson: freezed == scoreBreakdownJson
                ? _value.scoreBreakdownJson
                : scoreBreakdownJson // ignore: cast_nullable_to_non_nullable
                      as String?,
            state: null == state
                ? _value.state
                : state // ignore: cast_nullable_to_non_nullable
                      as String,
            publishedAt: freezed == publishedAt
                ? _value.publishedAt
                : publishedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            correctionCount: null == correctionCount
                ? _value.correctionCount
                : correctionCount // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$StageResultDtoImplCopyWith<$Res>
    implements $StageResultDtoCopyWith<$Res> {
  factory _$$StageResultDtoImplCopyWith(
    _$StageResultDtoImpl value,
    $Res Function(_$StageResultDtoImpl) then,
  ) = __$$StageResultDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String stageId,
    String subjectType,
    String subjectId,
    String subjectName,
    int rank,
    double? finalScore,
    String? scoreBreakdownJson,
    String state,
    DateTime? publishedAt,
    int correctionCount,
  });
}

/// @nodoc
class __$$StageResultDtoImplCopyWithImpl<$Res>
    extends _$StageResultDtoCopyWithImpl<$Res, _$StageResultDtoImpl>
    implements _$$StageResultDtoImplCopyWith<$Res> {
  __$$StageResultDtoImplCopyWithImpl(
    _$StageResultDtoImpl _value,
    $Res Function(_$StageResultDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of StageResultDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? stageId = null,
    Object? subjectType = null,
    Object? subjectId = null,
    Object? subjectName = null,
    Object? rank = null,
    Object? finalScore = freezed,
    Object? scoreBreakdownJson = freezed,
    Object? state = null,
    Object? publishedAt = freezed,
    Object? correctionCount = null,
  }) {
    return _then(
      _$StageResultDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        stageId: null == stageId
            ? _value.stageId
            : stageId // ignore: cast_nullable_to_non_nullable
                  as String,
        subjectType: null == subjectType
            ? _value.subjectType
            : subjectType // ignore: cast_nullable_to_non_nullable
                  as String,
        subjectId: null == subjectId
            ? _value.subjectId
            : subjectId // ignore: cast_nullable_to_non_nullable
                  as String,
        subjectName: null == subjectName
            ? _value.subjectName
            : subjectName // ignore: cast_nullable_to_non_nullable
                  as String,
        rank: null == rank
            ? _value.rank
            : rank // ignore: cast_nullable_to_non_nullable
                  as int,
        finalScore: freezed == finalScore
            ? _value.finalScore
            : finalScore // ignore: cast_nullable_to_non_nullable
                  as double?,
        scoreBreakdownJson: freezed == scoreBreakdownJson
            ? _value.scoreBreakdownJson
            : scoreBreakdownJson // ignore: cast_nullable_to_non_nullable
                  as String?,
        state: null == state
            ? _value.state
            : state // ignore: cast_nullable_to_non_nullable
                  as String,
        publishedAt: freezed == publishedAt
            ? _value.publishedAt
            : publishedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        correctionCount: null == correctionCount
            ? _value.correctionCount
            : correctionCount // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$StageResultDtoImpl implements _StageResultDto {
  const _$StageResultDtoImpl({
    required this.id,
    required this.stageId,
    this.subjectType = '',
    required this.subjectId,
    this.subjectName = '',
    this.rank = 0,
    this.finalScore,
    this.scoreBreakdownJson,
    this.state = '',
    this.publishedAt,
    this.correctionCount = 0,
  });

  factory _$StageResultDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$StageResultDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String stageId;
  @override
  @JsonKey()
  final String subjectType;
  @override
  final String subjectId;
  @override
  @JsonKey()
  final String subjectName;
  @override
  @JsonKey()
  final int rank;

  /// `decimal?` over the wire — read as `num` then widened, never as `int`.
  @override
  final double? finalScore;
  @override
  final String? scoreBreakdownJson;
  @override
  @JsonKey()
  final String state;
  @override
  final DateTime? publishedAt;
  @override
  @JsonKey()
  final int correctionCount;

  @override
  String toString() {
    return 'StageResultDto(id: $id, stageId: $stageId, subjectType: $subjectType, subjectId: $subjectId, subjectName: $subjectName, rank: $rank, finalScore: $finalScore, scoreBreakdownJson: $scoreBreakdownJson, state: $state, publishedAt: $publishedAt, correctionCount: $correctionCount)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$StageResultDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.stageId, stageId) || other.stageId == stageId) &&
            (identical(other.subjectType, subjectType) ||
                other.subjectType == subjectType) &&
            (identical(other.subjectId, subjectId) ||
                other.subjectId == subjectId) &&
            (identical(other.subjectName, subjectName) ||
                other.subjectName == subjectName) &&
            (identical(other.rank, rank) || other.rank == rank) &&
            (identical(other.finalScore, finalScore) ||
                other.finalScore == finalScore) &&
            (identical(other.scoreBreakdownJson, scoreBreakdownJson) ||
                other.scoreBreakdownJson == scoreBreakdownJson) &&
            (identical(other.state, state) || other.state == state) &&
            (identical(other.publishedAt, publishedAt) ||
                other.publishedAt == publishedAt) &&
            (identical(other.correctionCount, correctionCount) ||
                other.correctionCount == correctionCount));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    stageId,
    subjectType,
    subjectId,
    subjectName,
    rank,
    finalScore,
    scoreBreakdownJson,
    state,
    publishedAt,
    correctionCount,
  );

  /// Create a copy of StageResultDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$StageResultDtoImplCopyWith<_$StageResultDtoImpl> get copyWith =>
      __$$StageResultDtoImplCopyWithImpl<_$StageResultDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$StageResultDtoImplToJson(this);
  }
}

abstract class _StageResultDto implements StageResultDto {
  const factory _StageResultDto({
    required final String id,
    required final String stageId,
    final String subjectType,
    required final String subjectId,
    final String subjectName,
    final int rank,
    final double? finalScore,
    final String? scoreBreakdownJson,
    final String state,
    final DateTime? publishedAt,
    final int correctionCount,
  }) = _$StageResultDtoImpl;

  factory _StageResultDto.fromJson(Map<String, dynamic> json) =
      _$StageResultDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get stageId;
  @override
  String get subjectType;
  @override
  String get subjectId;
  @override
  String get subjectName;
  @override
  int get rank;

  /// `decimal?` over the wire — read as `num` then widened, never as `int`.
  @override
  double? get finalScore;
  @override
  String? get scoreBreakdownJson;
  @override
  String get state;
  @override
  DateTime? get publishedAt;
  @override
  int get correctionCount;

  /// Create a copy of StageResultDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$StageResultDtoImplCopyWith<_$StageResultDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

TeamMemberDto _$TeamMemberDtoFromJson(Map<String, dynamic> json) {
  return _TeamMemberDto.fromJson(json);
}

/// @nodoc
mixin _$TeamMemberDto {
  @JsonKey(name: 'membership_id')
  String get membershipId => throw _privateConstructorUsedError;
  @JsonKey(name: 'person_id')
  String? get personId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String get role => throw _privateConstructorUsedError;
  String get state => throw _privateConstructorUsedError;
  @JsonKey(name: 'joined_at')
  DateTime? get joinedAt => throw _privateConstructorUsedError;

  /// Serializes this TeamMemberDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TeamMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TeamMemberDtoCopyWith<TeamMemberDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TeamMemberDtoCopyWith<$Res> {
  factory $TeamMemberDtoCopyWith(
    TeamMemberDto value,
    $Res Function(TeamMemberDto) then,
  ) = _$TeamMemberDtoCopyWithImpl<$Res, TeamMemberDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'membership_id') String membershipId,
    @JsonKey(name: 'person_id') String? personId,
    String name,
    String role,
    String state,
    @JsonKey(name: 'joined_at') DateTime? joinedAt,
  });
}

/// @nodoc
class _$TeamMemberDtoCopyWithImpl<$Res, $Val extends TeamMemberDto>
    implements $TeamMemberDtoCopyWith<$Res> {
  _$TeamMemberDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TeamMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? membershipId = null,
    Object? personId = freezed,
    Object? name = null,
    Object? role = null,
    Object? state = null,
    Object? joinedAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            membershipId: null == membershipId
                ? _value.membershipId
                : membershipId // ignore: cast_nullable_to_non_nullable
                      as String,
            personId: freezed == personId
                ? _value.personId
                : personId // ignore: cast_nullable_to_non_nullable
                      as String?,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            role: null == role
                ? _value.role
                : role // ignore: cast_nullable_to_non_nullable
                      as String,
            state: null == state
                ? _value.state
                : state // ignore: cast_nullable_to_non_nullable
                      as String,
            joinedAt: freezed == joinedAt
                ? _value.joinedAt
                : joinedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TeamMemberDtoImplCopyWith<$Res>
    implements $TeamMemberDtoCopyWith<$Res> {
  factory _$$TeamMemberDtoImplCopyWith(
    _$TeamMemberDtoImpl value,
    $Res Function(_$TeamMemberDtoImpl) then,
  ) = __$$TeamMemberDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'membership_id') String membershipId,
    @JsonKey(name: 'person_id') String? personId,
    String name,
    String role,
    String state,
    @JsonKey(name: 'joined_at') DateTime? joinedAt,
  });
}

/// @nodoc
class __$$TeamMemberDtoImplCopyWithImpl<$Res>
    extends _$TeamMemberDtoCopyWithImpl<$Res, _$TeamMemberDtoImpl>
    implements _$$TeamMemberDtoImplCopyWith<$Res> {
  __$$TeamMemberDtoImplCopyWithImpl(
    _$TeamMemberDtoImpl _value,
    $Res Function(_$TeamMemberDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TeamMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? membershipId = null,
    Object? personId = freezed,
    Object? name = null,
    Object? role = null,
    Object? state = null,
    Object? joinedAt = freezed,
  }) {
    return _then(
      _$TeamMemberDtoImpl(
        membershipId: null == membershipId
            ? _value.membershipId
            : membershipId // ignore: cast_nullable_to_non_nullable
                  as String,
        personId: freezed == personId
            ? _value.personId
            : personId // ignore: cast_nullable_to_non_nullable
                  as String?,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        role: null == role
            ? _value.role
            : role // ignore: cast_nullable_to_non_nullable
                  as String,
        state: null == state
            ? _value.state
            : state // ignore: cast_nullable_to_non_nullable
                  as String,
        joinedAt: freezed == joinedAt
            ? _value.joinedAt
            : joinedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TeamMemberDtoImpl implements _TeamMemberDto {
  const _$TeamMemberDtoImpl({
    @JsonKey(name: 'membership_id') required this.membershipId,
    @JsonKey(name: 'person_id') this.personId,
    this.name = '',
    this.role = 'member',
    this.state = 'active',
    @JsonKey(name: 'joined_at') this.joinedAt,
  });

  factory _$TeamMemberDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TeamMemberDtoImplFromJson(json);

  @override
  @JsonKey(name: 'membership_id')
  final String membershipId;
  @override
  @JsonKey(name: 'person_id')
  final String? personId;
  @override
  @JsonKey()
  final String name;
  @override
  @JsonKey()
  final String role;
  @override
  @JsonKey()
  final String state;
  @override
  @JsonKey(name: 'joined_at')
  final DateTime? joinedAt;

  @override
  String toString() {
    return 'TeamMemberDto(membershipId: $membershipId, personId: $personId, name: $name, role: $role, state: $state, joinedAt: $joinedAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TeamMemberDtoImpl &&
            (identical(other.membershipId, membershipId) ||
                other.membershipId == membershipId) &&
            (identical(other.personId, personId) ||
                other.personId == personId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.role, role) || other.role == role) &&
            (identical(other.state, state) || other.state == state) &&
            (identical(other.joinedAt, joinedAt) ||
                other.joinedAt == joinedAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    membershipId,
    personId,
    name,
    role,
    state,
    joinedAt,
  );

  /// Create a copy of TeamMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TeamMemberDtoImplCopyWith<_$TeamMemberDtoImpl> get copyWith =>
      __$$TeamMemberDtoImplCopyWithImpl<_$TeamMemberDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$TeamMemberDtoImplToJson(this);
  }
}

abstract class _TeamMemberDto implements TeamMemberDto {
  const factory _TeamMemberDto({
    @JsonKey(name: 'membership_id') required final String membershipId,
    @JsonKey(name: 'person_id') final String? personId,
    final String name,
    final String role,
    final String state,
    @JsonKey(name: 'joined_at') final DateTime? joinedAt,
  }) = _$TeamMemberDtoImpl;

  factory _TeamMemberDto.fromJson(Map<String, dynamic> json) =
      _$TeamMemberDtoImpl.fromJson;

  @override
  @JsonKey(name: 'membership_id')
  String get membershipId;
  @override
  @JsonKey(name: 'person_id')
  String? get personId;
  @override
  String get name;
  @override
  String get role;
  @override
  String get state;
  @override
  @JsonKey(name: 'joined_at')
  DateTime? get joinedAt;

  /// Create a copy of TeamMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TeamMemberDtoImplCopyWith<_$TeamMemberDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

TeamDto _$TeamDtoFromJson(Map<String, dynamic> json) {
  return _TeamDto.fromJson(json);
}

/// @nodoc
mixin _$TeamDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'ticket_type_id')
  String? get ticketTypeId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get slug => throw _privateConstructorUsedError;
  @JsonKey(name: 'logo_url')
  String? get logoUrl => throw _privateConstructorUsedError;
  String? get tagline => throw _privateConstructorUsedError;

  /// `forming` / `locked` / `withdrawn` / `merged`.
  String get state => throw _privateConstructorUsedError;
  @JsonKey(name: 'active_member_count')
  int get activeMemberCount => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;
  List<TeamMemberDto> get members => throw _privateConstructorUsedError;

  /// Serializes this TeamDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TeamDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TeamDtoCopyWith<TeamDto> get copyWith => throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TeamDtoCopyWith<$Res> {
  factory $TeamDtoCopyWith(TeamDto value, $Res Function(TeamDto) then) =
      _$TeamDtoCopyWithImpl<$Res, TeamDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String? ticketTypeId,
    String name,
    String? slug,
    @JsonKey(name: 'logo_url') String? logoUrl,
    String? tagline,
    String state,
    @JsonKey(name: 'active_member_count') int activeMemberCount,
    @JsonKey(name: 'created_at') DateTime? createdAt,
    List<TeamMemberDto> members,
  });
}

/// @nodoc
class _$TeamDtoCopyWithImpl<$Res, $Val extends TeamDto>
    implements $TeamDtoCopyWith<$Res> {
  _$TeamDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TeamDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = freezed,
    Object? name = null,
    Object? slug = freezed,
    Object? logoUrl = freezed,
    Object? tagline = freezed,
    Object? state = null,
    Object? activeMemberCount = null,
    Object? createdAt = freezed,
    Object? members = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            eventId: null == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String,
            ticketTypeId: freezed == ticketTypeId
                ? _value.ticketTypeId
                : ticketTypeId // ignore: cast_nullable_to_non_nullable
                      as String?,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            slug: freezed == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String?,
            logoUrl: freezed == logoUrl
                ? _value.logoUrl
                : logoUrl // ignore: cast_nullable_to_non_nullable
                      as String?,
            tagline: freezed == tagline
                ? _value.tagline
                : tagline // ignore: cast_nullable_to_non_nullable
                      as String?,
            state: null == state
                ? _value.state
                : state // ignore: cast_nullable_to_non_nullable
                      as String,
            activeMemberCount: null == activeMemberCount
                ? _value.activeMemberCount
                : activeMemberCount // ignore: cast_nullable_to_non_nullable
                      as int,
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            members: null == members
                ? _value.members
                : members // ignore: cast_nullable_to_non_nullable
                      as List<TeamMemberDto>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TeamDtoImplCopyWith<$Res> implements $TeamDtoCopyWith<$Res> {
  factory _$$TeamDtoImplCopyWith(
    _$TeamDtoImpl value,
    $Res Function(_$TeamDtoImpl) then,
  ) = __$$TeamDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String? ticketTypeId,
    String name,
    String? slug,
    @JsonKey(name: 'logo_url') String? logoUrl,
    String? tagline,
    String state,
    @JsonKey(name: 'active_member_count') int activeMemberCount,
    @JsonKey(name: 'created_at') DateTime? createdAt,
    List<TeamMemberDto> members,
  });
}

/// @nodoc
class __$$TeamDtoImplCopyWithImpl<$Res>
    extends _$TeamDtoCopyWithImpl<$Res, _$TeamDtoImpl>
    implements _$$TeamDtoImplCopyWith<$Res> {
  __$$TeamDtoImplCopyWithImpl(
    _$TeamDtoImpl _value,
    $Res Function(_$TeamDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TeamDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = freezed,
    Object? name = null,
    Object? slug = freezed,
    Object? logoUrl = freezed,
    Object? tagline = freezed,
    Object? state = null,
    Object? activeMemberCount = null,
    Object? createdAt = freezed,
    Object? members = null,
  }) {
    return _then(
      _$TeamDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: null == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String,
        ticketTypeId: freezed == ticketTypeId
            ? _value.ticketTypeId
            : ticketTypeId // ignore: cast_nullable_to_non_nullable
                  as String?,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: freezed == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String?,
        logoUrl: freezed == logoUrl
            ? _value.logoUrl
            : logoUrl // ignore: cast_nullable_to_non_nullable
                  as String?,
        tagline: freezed == tagline
            ? _value.tagline
            : tagline // ignore: cast_nullable_to_non_nullable
                  as String?,
        state: null == state
            ? _value.state
            : state // ignore: cast_nullable_to_non_nullable
                  as String,
        activeMemberCount: null == activeMemberCount
            ? _value.activeMemberCount
            : activeMemberCount // ignore: cast_nullable_to_non_nullable
                  as int,
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        members: null == members
            ? _value._members
            : members // ignore: cast_nullable_to_non_nullable
                  as List<TeamMemberDto>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TeamDtoImpl implements _TeamDto {
  const _$TeamDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'ticket_type_id') this.ticketTypeId,
    required this.name,
    this.slug,
    @JsonKey(name: 'logo_url') this.logoUrl,
    this.tagline,
    this.state = 'forming',
    @JsonKey(name: 'active_member_count') this.activeMemberCount = 0,
    @JsonKey(name: 'created_at') this.createdAt,
    final List<TeamMemberDto> members = const <TeamMemberDto>[],
  }) : _members = members;

  factory _$TeamDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TeamDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  final String? ticketTypeId;
  @override
  final String name;
  @override
  final String? slug;
  @override
  @JsonKey(name: 'logo_url')
  final String? logoUrl;
  @override
  final String? tagline;

  /// `forming` / `locked` / `withdrawn` / `merged`.
  @override
  @JsonKey()
  final String state;
  @override
  @JsonKey(name: 'active_member_count')
  final int activeMemberCount;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  final List<TeamMemberDto> _members;
  @override
  @JsonKey()
  List<TeamMemberDto> get members {
    if (_members is EqualUnmodifiableListView) return _members;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_members);
  }

  @override
  String toString() {
    return 'TeamDto(id: $id, eventId: $eventId, ticketTypeId: $ticketTypeId, name: $name, slug: $slug, logoUrl: $logoUrl, tagline: $tagline, state: $state, activeMemberCount: $activeMemberCount, createdAt: $createdAt, members: $members)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TeamDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.ticketTypeId, ticketTypeId) ||
                other.ticketTypeId == ticketTypeId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.logoUrl, logoUrl) || other.logoUrl == logoUrl) &&
            (identical(other.tagline, tagline) || other.tagline == tagline) &&
            (identical(other.state, state) || other.state == state) &&
            (identical(other.activeMemberCount, activeMemberCount) ||
                other.activeMemberCount == activeMemberCount) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt) &&
            const DeepCollectionEquality().equals(other._members, _members));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    ticketTypeId,
    name,
    slug,
    logoUrl,
    tagline,
    state,
    activeMemberCount,
    createdAt,
    const DeepCollectionEquality().hash(_members),
  );

  /// Create a copy of TeamDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TeamDtoImplCopyWith<_$TeamDtoImpl> get copyWith =>
      __$$TeamDtoImplCopyWithImpl<_$TeamDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$TeamDtoImplToJson(this);
  }
}

abstract class _TeamDto implements TeamDto {
  const factory _TeamDto({
    required final String id,
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'ticket_type_id') final String? ticketTypeId,
    required final String name,
    final String? slug,
    @JsonKey(name: 'logo_url') final String? logoUrl,
    final String? tagline,
    final String state,
    @JsonKey(name: 'active_member_count') final int activeMemberCount,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
    final List<TeamMemberDto> members,
  }) = _$TeamDtoImpl;

  factory _TeamDto.fromJson(Map<String, dynamic> json) = _$TeamDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  String? get ticketTypeId;
  @override
  String get name;
  @override
  String? get slug;
  @override
  @JsonKey(name: 'logo_url')
  String? get logoUrl;
  @override
  String? get tagline;

  /// `forming` / `locked` / `withdrawn` / `merged`.
  @override
  String get state;
  @override
  @JsonKey(name: 'active_member_count')
  int get activeMemberCount;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;
  @override
  List<TeamMemberDto> get members;

  /// Create a copy of TeamDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TeamDtoImplCopyWith<_$TeamDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

TeamInviteDto _$TeamInviteDtoFromJson(Map<String, dynamic> json) {
  return _TeamInviteDto.fromJson(json);
}

/// @nodoc
mixin _$TeamInviteDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'team_id')
  String get teamId => throw _privateConstructorUsedError;
  @JsonKey(name: 'invitee_email')
  String? get inviteeEmail => throw _privateConstructorUsedError;
  @JsonKey(name: 'invitee_phone')
  String? get inviteePhone => throw _privateConstructorUsedError;
  String get role => throw _privateConstructorUsedError;
  String get state => throw _privateConstructorUsedError;
  String? get token => throw _privateConstructorUsedError;
  @JsonKey(name: 'expires_at')
  DateTime? get expiresAt => throw _privateConstructorUsedError;

  /// Serializes this TeamInviteDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TeamInviteDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TeamInviteDtoCopyWith<TeamInviteDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TeamInviteDtoCopyWith<$Res> {
  factory $TeamInviteDtoCopyWith(
    TeamInviteDto value,
    $Res Function(TeamInviteDto) then,
  ) = _$TeamInviteDtoCopyWithImpl<$Res, TeamInviteDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'team_id') String teamId,
    @JsonKey(name: 'invitee_email') String? inviteeEmail,
    @JsonKey(name: 'invitee_phone') String? inviteePhone,
    String role,
    String state,
    String? token,
    @JsonKey(name: 'expires_at') DateTime? expiresAt,
  });
}

/// @nodoc
class _$TeamInviteDtoCopyWithImpl<$Res, $Val extends TeamInviteDto>
    implements $TeamInviteDtoCopyWith<$Res> {
  _$TeamInviteDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TeamInviteDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? teamId = null,
    Object? inviteeEmail = freezed,
    Object? inviteePhone = freezed,
    Object? role = null,
    Object? state = null,
    Object? token = freezed,
    Object? expiresAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            teamId: null == teamId
                ? _value.teamId
                : teamId // ignore: cast_nullable_to_non_nullable
                      as String,
            inviteeEmail: freezed == inviteeEmail
                ? _value.inviteeEmail
                : inviteeEmail // ignore: cast_nullable_to_non_nullable
                      as String?,
            inviteePhone: freezed == inviteePhone
                ? _value.inviteePhone
                : inviteePhone // ignore: cast_nullable_to_non_nullable
                      as String?,
            role: null == role
                ? _value.role
                : role // ignore: cast_nullable_to_non_nullable
                      as String,
            state: null == state
                ? _value.state
                : state // ignore: cast_nullable_to_non_nullable
                      as String,
            token: freezed == token
                ? _value.token
                : token // ignore: cast_nullable_to_non_nullable
                      as String?,
            expiresAt: freezed == expiresAt
                ? _value.expiresAt
                : expiresAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TeamInviteDtoImplCopyWith<$Res>
    implements $TeamInviteDtoCopyWith<$Res> {
  factory _$$TeamInviteDtoImplCopyWith(
    _$TeamInviteDtoImpl value,
    $Res Function(_$TeamInviteDtoImpl) then,
  ) = __$$TeamInviteDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'team_id') String teamId,
    @JsonKey(name: 'invitee_email') String? inviteeEmail,
    @JsonKey(name: 'invitee_phone') String? inviteePhone,
    String role,
    String state,
    String? token,
    @JsonKey(name: 'expires_at') DateTime? expiresAt,
  });
}

/// @nodoc
class __$$TeamInviteDtoImplCopyWithImpl<$Res>
    extends _$TeamInviteDtoCopyWithImpl<$Res, _$TeamInviteDtoImpl>
    implements _$$TeamInviteDtoImplCopyWith<$Res> {
  __$$TeamInviteDtoImplCopyWithImpl(
    _$TeamInviteDtoImpl _value,
    $Res Function(_$TeamInviteDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TeamInviteDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? teamId = null,
    Object? inviteeEmail = freezed,
    Object? inviteePhone = freezed,
    Object? role = null,
    Object? state = null,
    Object? token = freezed,
    Object? expiresAt = freezed,
  }) {
    return _then(
      _$TeamInviteDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        teamId: null == teamId
            ? _value.teamId
            : teamId // ignore: cast_nullable_to_non_nullable
                  as String,
        inviteeEmail: freezed == inviteeEmail
            ? _value.inviteeEmail
            : inviteeEmail // ignore: cast_nullable_to_non_nullable
                  as String?,
        inviteePhone: freezed == inviteePhone
            ? _value.inviteePhone
            : inviteePhone // ignore: cast_nullable_to_non_nullable
                  as String?,
        role: null == role
            ? _value.role
            : role // ignore: cast_nullable_to_non_nullable
                  as String,
        state: null == state
            ? _value.state
            : state // ignore: cast_nullable_to_non_nullable
                  as String,
        token: freezed == token
            ? _value.token
            : token // ignore: cast_nullable_to_non_nullable
                  as String?,
        expiresAt: freezed == expiresAt
            ? _value.expiresAt
            : expiresAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TeamInviteDtoImpl implements _TeamInviteDto {
  const _$TeamInviteDtoImpl({
    required this.id,
    @JsonKey(name: 'team_id') required this.teamId,
    @JsonKey(name: 'invitee_email') this.inviteeEmail,
    @JsonKey(name: 'invitee_phone') this.inviteePhone,
    this.role = 'member',
    this.state = 'pending',
    this.token,
    @JsonKey(name: 'expires_at') this.expiresAt,
  });

  factory _$TeamInviteDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TeamInviteDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'team_id')
  final String teamId;
  @override
  @JsonKey(name: 'invitee_email')
  final String? inviteeEmail;
  @override
  @JsonKey(name: 'invitee_phone')
  final String? inviteePhone;
  @override
  @JsonKey()
  final String role;
  @override
  @JsonKey()
  final String state;
  @override
  final String? token;
  @override
  @JsonKey(name: 'expires_at')
  final DateTime? expiresAt;

  @override
  String toString() {
    return 'TeamInviteDto(id: $id, teamId: $teamId, inviteeEmail: $inviteeEmail, inviteePhone: $inviteePhone, role: $role, state: $state, token: $token, expiresAt: $expiresAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TeamInviteDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.teamId, teamId) || other.teamId == teamId) &&
            (identical(other.inviteeEmail, inviteeEmail) ||
                other.inviteeEmail == inviteeEmail) &&
            (identical(other.inviteePhone, inviteePhone) ||
                other.inviteePhone == inviteePhone) &&
            (identical(other.role, role) || other.role == role) &&
            (identical(other.state, state) || other.state == state) &&
            (identical(other.token, token) || other.token == token) &&
            (identical(other.expiresAt, expiresAt) ||
                other.expiresAt == expiresAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    teamId,
    inviteeEmail,
    inviteePhone,
    role,
    state,
    token,
    expiresAt,
  );

  /// Create a copy of TeamInviteDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TeamInviteDtoImplCopyWith<_$TeamInviteDtoImpl> get copyWith =>
      __$$TeamInviteDtoImplCopyWithImpl<_$TeamInviteDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$TeamInviteDtoImplToJson(this);
  }
}

abstract class _TeamInviteDto implements TeamInviteDto {
  const factory _TeamInviteDto({
    required final String id,
    @JsonKey(name: 'team_id') required final String teamId,
    @JsonKey(name: 'invitee_email') final String? inviteeEmail,
    @JsonKey(name: 'invitee_phone') final String? inviteePhone,
    final String role,
    final String state,
    final String? token,
    @JsonKey(name: 'expires_at') final DateTime? expiresAt,
  }) = _$TeamInviteDtoImpl;

  factory _TeamInviteDto.fromJson(Map<String, dynamic> json) =
      _$TeamInviteDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'team_id')
  String get teamId;
  @override
  @JsonKey(name: 'invitee_email')
  String? get inviteeEmail;
  @override
  @JsonKey(name: 'invitee_phone')
  String? get inviteePhone;
  @override
  String get role;
  @override
  String get state;
  @override
  String? get token;
  @override
  @JsonKey(name: 'expires_at')
  DateTime? get expiresAt;

  /// Create a copy of TeamInviteDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TeamInviteDtoImplCopyWith<_$TeamInviteDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
