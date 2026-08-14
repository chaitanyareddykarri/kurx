// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'public_profile_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

CollegeDto _$CollegeDtoFromJson(Map<String, dynamic> json) {
  return _CollegeDto.fromJson(json);
}

/// @nodoc
mixin _$CollegeDto {
  String? get institute => throw _privateConstructorUsedError;
  String? get degree => throw _privateConstructorUsedError;
  String? get branch => throw _privateConstructorUsedError;

  /// Serializes this CollegeDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of CollegeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $CollegeDtoCopyWith<CollegeDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $CollegeDtoCopyWith<$Res> {
  factory $CollegeDtoCopyWith(
    CollegeDto value,
    $Res Function(CollegeDto) then,
  ) = _$CollegeDtoCopyWithImpl<$Res, CollegeDto>;
  @useResult
  $Res call({String? institute, String? degree, String? branch});
}

/// @nodoc
class _$CollegeDtoCopyWithImpl<$Res, $Val extends CollegeDto>
    implements $CollegeDtoCopyWith<$Res> {
  _$CollegeDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of CollegeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? institute = freezed,
    Object? degree = freezed,
    Object? branch = freezed,
  }) {
    return _then(
      _value.copyWith(
            institute: freezed == institute
                ? _value.institute
                : institute // ignore: cast_nullable_to_non_nullable
                      as String?,
            degree: freezed == degree
                ? _value.degree
                : degree // ignore: cast_nullable_to_non_nullable
                      as String?,
            branch: freezed == branch
                ? _value.branch
                : branch // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$CollegeDtoImplCopyWith<$Res>
    implements $CollegeDtoCopyWith<$Res> {
  factory _$$CollegeDtoImplCopyWith(
    _$CollegeDtoImpl value,
    $Res Function(_$CollegeDtoImpl) then,
  ) = __$$CollegeDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({String? institute, String? degree, String? branch});
}

/// @nodoc
class __$$CollegeDtoImplCopyWithImpl<$Res>
    extends _$CollegeDtoCopyWithImpl<$Res, _$CollegeDtoImpl>
    implements _$$CollegeDtoImplCopyWith<$Res> {
  __$$CollegeDtoImplCopyWithImpl(
    _$CollegeDtoImpl _value,
    $Res Function(_$CollegeDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of CollegeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? institute = freezed,
    Object? degree = freezed,
    Object? branch = freezed,
  }) {
    return _then(
      _$CollegeDtoImpl(
        institute: freezed == institute
            ? _value.institute
            : institute // ignore: cast_nullable_to_non_nullable
                  as String?,
        degree: freezed == degree
            ? _value.degree
            : degree // ignore: cast_nullable_to_non_nullable
                  as String?,
        branch: freezed == branch
            ? _value.branch
            : branch // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$CollegeDtoImpl implements _CollegeDto {
  const _$CollegeDtoImpl({this.institute, this.degree, this.branch});

  factory _$CollegeDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$CollegeDtoImplFromJson(json);

  @override
  final String? institute;
  @override
  final String? degree;
  @override
  final String? branch;

  @override
  String toString() {
    return 'CollegeDto(institute: $institute, degree: $degree, branch: $branch)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$CollegeDtoImpl &&
            (identical(other.institute, institute) ||
                other.institute == institute) &&
            (identical(other.degree, degree) || other.degree == degree) &&
            (identical(other.branch, branch) || other.branch == branch));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, institute, degree, branch);

  /// Create a copy of CollegeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$CollegeDtoImplCopyWith<_$CollegeDtoImpl> get copyWith =>
      __$$CollegeDtoImplCopyWithImpl<_$CollegeDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$CollegeDtoImplToJson(this);
  }
}

abstract class _CollegeDto implements CollegeDto {
  const factory _CollegeDto({
    final String? institute,
    final String? degree,
    final String? branch,
  }) = _$CollegeDtoImpl;

  factory _CollegeDto.fromJson(Map<String, dynamic> json) =
      _$CollegeDtoImpl.fromJson;

  @override
  String? get institute;
  @override
  String? get degree;
  @override
  String? get branch;

  /// Create a copy of CollegeDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$CollegeDtoImplCopyWith<_$CollegeDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

ProfileStatsDto _$ProfileStatsDtoFromJson(Map<String, dynamic> json) {
  return _ProfileStatsDto.fromJson(json);
}

/// @nodoc
mixin _$ProfileStatsDto {
  // Null = hidden from this viewer, never zero (D-229/H2).
  @JsonKey(name: 'events_conducted')
  int? get eventsConducted => throw _privateConstructorUsedError;
  @JsonKey(name: 'events_attended')
  int? get eventsAttended => throw _privateConstructorUsedError;
  @JsonKey(name: 'certificates_count')
  int? get certificatesCount => throw _privateConstructorUsedError;
  int? get participations => throw _privateConstructorUsedError;
  int? get achievements => throw _privateConstructorUsedError;
  @JsonKey(name: 'ally_count')
  int? get allyCount => throw _privateConstructorUsedError;

  /// Serializes this ProfileStatsDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ProfileStatsDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ProfileStatsDtoCopyWith<ProfileStatsDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ProfileStatsDtoCopyWith<$Res> {
  factory $ProfileStatsDtoCopyWith(
    ProfileStatsDto value,
    $Res Function(ProfileStatsDto) then,
  ) = _$ProfileStatsDtoCopyWithImpl<$Res, ProfileStatsDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'events_conducted') int? eventsConducted,
    @JsonKey(name: 'events_attended') int? eventsAttended,
    @JsonKey(name: 'certificates_count') int? certificatesCount,
    int? participations,
    int? achievements,
    @JsonKey(name: 'ally_count') int? allyCount,
  });
}

/// @nodoc
class _$ProfileStatsDtoCopyWithImpl<$Res, $Val extends ProfileStatsDto>
    implements $ProfileStatsDtoCopyWith<$Res> {
  _$ProfileStatsDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ProfileStatsDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? eventsConducted = freezed,
    Object? eventsAttended = freezed,
    Object? certificatesCount = freezed,
    Object? participations = freezed,
    Object? achievements = freezed,
    Object? allyCount = freezed,
  }) {
    return _then(
      _value.copyWith(
            eventsConducted: freezed == eventsConducted
                ? _value.eventsConducted
                : eventsConducted // ignore: cast_nullable_to_non_nullable
                      as int?,
            eventsAttended: freezed == eventsAttended
                ? _value.eventsAttended
                : eventsAttended // ignore: cast_nullable_to_non_nullable
                      as int?,
            certificatesCount: freezed == certificatesCount
                ? _value.certificatesCount
                : certificatesCount // ignore: cast_nullable_to_non_nullable
                      as int?,
            participations: freezed == participations
                ? _value.participations
                : participations // ignore: cast_nullable_to_non_nullable
                      as int?,
            achievements: freezed == achievements
                ? _value.achievements
                : achievements // ignore: cast_nullable_to_non_nullable
                      as int?,
            allyCount: freezed == allyCount
                ? _value.allyCount
                : allyCount // ignore: cast_nullable_to_non_nullable
                      as int?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ProfileStatsDtoImplCopyWith<$Res>
    implements $ProfileStatsDtoCopyWith<$Res> {
  factory _$$ProfileStatsDtoImplCopyWith(
    _$ProfileStatsDtoImpl value,
    $Res Function(_$ProfileStatsDtoImpl) then,
  ) = __$$ProfileStatsDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'events_conducted') int? eventsConducted,
    @JsonKey(name: 'events_attended') int? eventsAttended,
    @JsonKey(name: 'certificates_count') int? certificatesCount,
    int? participations,
    int? achievements,
    @JsonKey(name: 'ally_count') int? allyCount,
  });
}

/// @nodoc
class __$$ProfileStatsDtoImplCopyWithImpl<$Res>
    extends _$ProfileStatsDtoCopyWithImpl<$Res, _$ProfileStatsDtoImpl>
    implements _$$ProfileStatsDtoImplCopyWith<$Res> {
  __$$ProfileStatsDtoImplCopyWithImpl(
    _$ProfileStatsDtoImpl _value,
    $Res Function(_$ProfileStatsDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ProfileStatsDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? eventsConducted = freezed,
    Object? eventsAttended = freezed,
    Object? certificatesCount = freezed,
    Object? participations = freezed,
    Object? achievements = freezed,
    Object? allyCount = freezed,
  }) {
    return _then(
      _$ProfileStatsDtoImpl(
        eventsConducted: freezed == eventsConducted
            ? _value.eventsConducted
            : eventsConducted // ignore: cast_nullable_to_non_nullable
                  as int?,
        eventsAttended: freezed == eventsAttended
            ? _value.eventsAttended
            : eventsAttended // ignore: cast_nullable_to_non_nullable
                  as int?,
        certificatesCount: freezed == certificatesCount
            ? _value.certificatesCount
            : certificatesCount // ignore: cast_nullable_to_non_nullable
                  as int?,
        participations: freezed == participations
            ? _value.participations
            : participations // ignore: cast_nullable_to_non_nullable
                  as int?,
        achievements: freezed == achievements
            ? _value.achievements
            : achievements // ignore: cast_nullable_to_non_nullable
                  as int?,
        allyCount: freezed == allyCount
            ? _value.allyCount
            : allyCount // ignore: cast_nullable_to_non_nullable
                  as int?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ProfileStatsDtoImpl implements _ProfileStatsDto {
  const _$ProfileStatsDtoImpl({
    @JsonKey(name: 'events_conducted') this.eventsConducted,
    @JsonKey(name: 'events_attended') this.eventsAttended,
    @JsonKey(name: 'certificates_count') this.certificatesCount,
    this.participations,
    this.achievements,
    @JsonKey(name: 'ally_count') this.allyCount,
  });

  factory _$ProfileStatsDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ProfileStatsDtoImplFromJson(json);

  // Null = hidden from this viewer, never zero (D-229/H2).
  @override
  @JsonKey(name: 'events_conducted')
  final int? eventsConducted;
  @override
  @JsonKey(name: 'events_attended')
  final int? eventsAttended;
  @override
  @JsonKey(name: 'certificates_count')
  final int? certificatesCount;
  @override
  final int? participations;
  @override
  final int? achievements;
  @override
  @JsonKey(name: 'ally_count')
  final int? allyCount;

  @override
  String toString() {
    return 'ProfileStatsDto(eventsConducted: $eventsConducted, eventsAttended: $eventsAttended, certificatesCount: $certificatesCount, participations: $participations, achievements: $achievements, allyCount: $allyCount)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ProfileStatsDtoImpl &&
            (identical(other.eventsConducted, eventsConducted) ||
                other.eventsConducted == eventsConducted) &&
            (identical(other.eventsAttended, eventsAttended) ||
                other.eventsAttended == eventsAttended) &&
            (identical(other.certificatesCount, certificatesCount) ||
                other.certificatesCount == certificatesCount) &&
            (identical(other.participations, participations) ||
                other.participations == participations) &&
            (identical(other.achievements, achievements) ||
                other.achievements == achievements) &&
            (identical(other.allyCount, allyCount) ||
                other.allyCount == allyCount));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    eventsConducted,
    eventsAttended,
    certificatesCount,
    participations,
    achievements,
    allyCount,
  );

  /// Create a copy of ProfileStatsDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ProfileStatsDtoImplCopyWith<_$ProfileStatsDtoImpl> get copyWith =>
      __$$ProfileStatsDtoImplCopyWithImpl<_$ProfileStatsDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$ProfileStatsDtoImplToJson(this);
  }
}

abstract class _ProfileStatsDto implements ProfileStatsDto {
  const factory _ProfileStatsDto({
    @JsonKey(name: 'events_conducted') final int? eventsConducted,
    @JsonKey(name: 'events_attended') final int? eventsAttended,
    @JsonKey(name: 'certificates_count') final int? certificatesCount,
    final int? participations,
    final int? achievements,
    @JsonKey(name: 'ally_count') final int? allyCount,
  }) = _$ProfileStatsDtoImpl;

  factory _ProfileStatsDto.fromJson(Map<String, dynamic> json) =
      _$ProfileStatsDtoImpl.fromJson;

  // Null = hidden from this viewer, never zero (D-229/H2).
  @override
  @JsonKey(name: 'events_conducted')
  int? get eventsConducted;
  @override
  @JsonKey(name: 'events_attended')
  int? get eventsAttended;
  @override
  @JsonKey(name: 'certificates_count')
  int? get certificatesCount;
  @override
  int? get participations;
  @override
  int? get achievements;
  @override
  @JsonKey(name: 'ally_count')
  int? get allyCount;

  /// Create a copy of ProfileStatsDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ProfileStatsDtoImplCopyWith<_$ProfileStatsDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

VerificationBadgesDto _$VerificationBadgesDtoFromJson(
  Map<String, dynamic> json,
) {
  return _VerificationBadgesDto.fromJson(json);
}

/// @nodoc
mixin _$VerificationBadgesDto {
  @JsonKey(name: 'identity_verified')
  bool get identityVerified => throw _privateConstructorUsedError;
  @JsonKey(name: 'verified_member')
  bool get verifiedMember => throw _privateConstructorUsedError;
  bool get organizer => throw _privateConstructorUsedError;
  @JsonKey(name: 'verified_certificates')
  int? get verifiedCertificates => throw _privateConstructorUsedError;
  @JsonKey(name: 'years_on_platform')
  int get yearsOnPlatform => throw _privateConstructorUsedError; // D-221 additions — defaulted so a client pinned to an older backend still parses. Positive
  // signals only: no risk, fraud, report or moderation field is ever exposed publicly.
  @JsonKey(name: 'phone_verified')
  bool get phoneVerified => throw _privateConstructorUsedError;
  @JsonKey(name: 'email_verified')
  bool get emailVerified => throw _privateConstructorUsedError;
  @JsonKey(name: 'speaker_verified')
  bool get speakerVerified => throw _privateConstructorUsedError;
  @JsonKey(name: 'community_verified')
  bool get communityVerified => throw _privateConstructorUsedError;

  /// Serializes this VerificationBadgesDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of VerificationBadgesDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $VerificationBadgesDtoCopyWith<VerificationBadgesDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $VerificationBadgesDtoCopyWith<$Res> {
  factory $VerificationBadgesDtoCopyWith(
    VerificationBadgesDto value,
    $Res Function(VerificationBadgesDto) then,
  ) = _$VerificationBadgesDtoCopyWithImpl<$Res, VerificationBadgesDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'identity_verified') bool identityVerified,
    @JsonKey(name: 'verified_member') bool verifiedMember,
    bool organizer,
    @JsonKey(name: 'verified_certificates') int? verifiedCertificates,
    @JsonKey(name: 'years_on_platform') int yearsOnPlatform,
    @JsonKey(name: 'phone_verified') bool phoneVerified,
    @JsonKey(name: 'email_verified') bool emailVerified,
    @JsonKey(name: 'speaker_verified') bool speakerVerified,
    @JsonKey(name: 'community_verified') bool communityVerified,
  });
}

/// @nodoc
class _$VerificationBadgesDtoCopyWithImpl<
  $Res,
  $Val extends VerificationBadgesDto
>
    implements $VerificationBadgesDtoCopyWith<$Res> {
  _$VerificationBadgesDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of VerificationBadgesDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? identityVerified = null,
    Object? verifiedMember = null,
    Object? organizer = null,
    Object? verifiedCertificates = freezed,
    Object? yearsOnPlatform = null,
    Object? phoneVerified = null,
    Object? emailVerified = null,
    Object? speakerVerified = null,
    Object? communityVerified = null,
  }) {
    return _then(
      _value.copyWith(
            identityVerified: null == identityVerified
                ? _value.identityVerified
                : identityVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
            verifiedMember: null == verifiedMember
                ? _value.verifiedMember
                : verifiedMember // ignore: cast_nullable_to_non_nullable
                      as bool,
            organizer: null == organizer
                ? _value.organizer
                : organizer // ignore: cast_nullable_to_non_nullable
                      as bool,
            verifiedCertificates: freezed == verifiedCertificates
                ? _value.verifiedCertificates
                : verifiedCertificates // ignore: cast_nullable_to_non_nullable
                      as int?,
            yearsOnPlatform: null == yearsOnPlatform
                ? _value.yearsOnPlatform
                : yearsOnPlatform // ignore: cast_nullable_to_non_nullable
                      as int,
            phoneVerified: null == phoneVerified
                ? _value.phoneVerified
                : phoneVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
            emailVerified: null == emailVerified
                ? _value.emailVerified
                : emailVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
            speakerVerified: null == speakerVerified
                ? _value.speakerVerified
                : speakerVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
            communityVerified: null == communityVerified
                ? _value.communityVerified
                : communityVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$VerificationBadgesDtoImplCopyWith<$Res>
    implements $VerificationBadgesDtoCopyWith<$Res> {
  factory _$$VerificationBadgesDtoImplCopyWith(
    _$VerificationBadgesDtoImpl value,
    $Res Function(_$VerificationBadgesDtoImpl) then,
  ) = __$$VerificationBadgesDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'identity_verified') bool identityVerified,
    @JsonKey(name: 'verified_member') bool verifiedMember,
    bool organizer,
    @JsonKey(name: 'verified_certificates') int? verifiedCertificates,
    @JsonKey(name: 'years_on_platform') int yearsOnPlatform,
    @JsonKey(name: 'phone_verified') bool phoneVerified,
    @JsonKey(name: 'email_verified') bool emailVerified,
    @JsonKey(name: 'speaker_verified') bool speakerVerified,
    @JsonKey(name: 'community_verified') bool communityVerified,
  });
}

/// @nodoc
class __$$VerificationBadgesDtoImplCopyWithImpl<$Res>
    extends
        _$VerificationBadgesDtoCopyWithImpl<$Res, _$VerificationBadgesDtoImpl>
    implements _$$VerificationBadgesDtoImplCopyWith<$Res> {
  __$$VerificationBadgesDtoImplCopyWithImpl(
    _$VerificationBadgesDtoImpl _value,
    $Res Function(_$VerificationBadgesDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of VerificationBadgesDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? identityVerified = null,
    Object? verifiedMember = null,
    Object? organizer = null,
    Object? verifiedCertificates = freezed,
    Object? yearsOnPlatform = null,
    Object? phoneVerified = null,
    Object? emailVerified = null,
    Object? speakerVerified = null,
    Object? communityVerified = null,
  }) {
    return _then(
      _$VerificationBadgesDtoImpl(
        identityVerified: null == identityVerified
            ? _value.identityVerified
            : identityVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
        verifiedMember: null == verifiedMember
            ? _value.verifiedMember
            : verifiedMember // ignore: cast_nullable_to_non_nullable
                  as bool,
        organizer: null == organizer
            ? _value.organizer
            : organizer // ignore: cast_nullable_to_non_nullable
                  as bool,
        verifiedCertificates: freezed == verifiedCertificates
            ? _value.verifiedCertificates
            : verifiedCertificates // ignore: cast_nullable_to_non_nullable
                  as int?,
        yearsOnPlatform: null == yearsOnPlatform
            ? _value.yearsOnPlatform
            : yearsOnPlatform // ignore: cast_nullable_to_non_nullable
                  as int,
        phoneVerified: null == phoneVerified
            ? _value.phoneVerified
            : phoneVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
        emailVerified: null == emailVerified
            ? _value.emailVerified
            : emailVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
        speakerVerified: null == speakerVerified
            ? _value.speakerVerified
            : speakerVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
        communityVerified: null == communityVerified
            ? _value.communityVerified
            : communityVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$VerificationBadgesDtoImpl implements _VerificationBadgesDto {
  const _$VerificationBadgesDtoImpl({
    @JsonKey(name: 'identity_verified') required this.identityVerified,
    @JsonKey(name: 'verified_member') required this.verifiedMember,
    required this.organizer,
    @JsonKey(name: 'verified_certificates') this.verifiedCertificates,
    @JsonKey(name: 'years_on_platform') required this.yearsOnPlatform,
    @JsonKey(name: 'phone_verified') this.phoneVerified = false,
    @JsonKey(name: 'email_verified') this.emailVerified = false,
    @JsonKey(name: 'speaker_verified') this.speakerVerified = false,
    @JsonKey(name: 'community_verified') this.communityVerified = false,
  });

  factory _$VerificationBadgesDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$VerificationBadgesDtoImplFromJson(json);

  @override
  @JsonKey(name: 'identity_verified')
  final bool identityVerified;
  @override
  @JsonKey(name: 'verified_member')
  final bool verifiedMember;
  @override
  final bool organizer;
  @override
  @JsonKey(name: 'verified_certificates')
  final int? verifiedCertificates;
  @override
  @JsonKey(name: 'years_on_platform')
  final int yearsOnPlatform;
  // D-221 additions — defaulted so a client pinned to an older backend still parses. Positive
  // signals only: no risk, fraud, report or moderation field is ever exposed publicly.
  @override
  @JsonKey(name: 'phone_verified')
  final bool phoneVerified;
  @override
  @JsonKey(name: 'email_verified')
  final bool emailVerified;
  @override
  @JsonKey(name: 'speaker_verified')
  final bool speakerVerified;
  @override
  @JsonKey(name: 'community_verified')
  final bool communityVerified;

  @override
  String toString() {
    return 'VerificationBadgesDto(identityVerified: $identityVerified, verifiedMember: $verifiedMember, organizer: $organizer, verifiedCertificates: $verifiedCertificates, yearsOnPlatform: $yearsOnPlatform, phoneVerified: $phoneVerified, emailVerified: $emailVerified, speakerVerified: $speakerVerified, communityVerified: $communityVerified)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$VerificationBadgesDtoImpl &&
            (identical(other.identityVerified, identityVerified) ||
                other.identityVerified == identityVerified) &&
            (identical(other.verifiedMember, verifiedMember) ||
                other.verifiedMember == verifiedMember) &&
            (identical(other.organizer, organizer) ||
                other.organizer == organizer) &&
            (identical(other.verifiedCertificates, verifiedCertificates) ||
                other.verifiedCertificates == verifiedCertificates) &&
            (identical(other.yearsOnPlatform, yearsOnPlatform) ||
                other.yearsOnPlatform == yearsOnPlatform) &&
            (identical(other.phoneVerified, phoneVerified) ||
                other.phoneVerified == phoneVerified) &&
            (identical(other.emailVerified, emailVerified) ||
                other.emailVerified == emailVerified) &&
            (identical(other.speakerVerified, speakerVerified) ||
                other.speakerVerified == speakerVerified) &&
            (identical(other.communityVerified, communityVerified) ||
                other.communityVerified == communityVerified));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    identityVerified,
    verifiedMember,
    organizer,
    verifiedCertificates,
    yearsOnPlatform,
    phoneVerified,
    emailVerified,
    speakerVerified,
    communityVerified,
  );

  /// Create a copy of VerificationBadgesDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$VerificationBadgesDtoImplCopyWith<_$VerificationBadgesDtoImpl>
  get copyWith =>
      __$$VerificationBadgesDtoImplCopyWithImpl<_$VerificationBadgesDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$VerificationBadgesDtoImplToJson(this);
  }
}

abstract class _VerificationBadgesDto implements VerificationBadgesDto {
  const factory _VerificationBadgesDto({
    @JsonKey(name: 'identity_verified') required final bool identityVerified,
    @JsonKey(name: 'verified_member') required final bool verifiedMember,
    required final bool organizer,
    @JsonKey(name: 'verified_certificates') final int? verifiedCertificates,
    @JsonKey(name: 'years_on_platform') required final int yearsOnPlatform,
    @JsonKey(name: 'phone_verified') final bool phoneVerified,
    @JsonKey(name: 'email_verified') final bool emailVerified,
    @JsonKey(name: 'speaker_verified') final bool speakerVerified,
    @JsonKey(name: 'community_verified') final bool communityVerified,
  }) = _$VerificationBadgesDtoImpl;

  factory _VerificationBadgesDto.fromJson(Map<String, dynamic> json) =
      _$VerificationBadgesDtoImpl.fromJson;

  @override
  @JsonKey(name: 'identity_verified')
  bool get identityVerified;
  @override
  @JsonKey(name: 'verified_member')
  bool get verifiedMember;
  @override
  bool get organizer;
  @override
  @JsonKey(name: 'verified_certificates')
  int? get verifiedCertificates;
  @override
  @JsonKey(name: 'years_on_platform')
  int get yearsOnPlatform; // D-221 additions — defaulted so a client pinned to an older backend still parses. Positive
  // signals only: no risk, fraud, report or moderation field is ever exposed publicly.
  @override
  @JsonKey(name: 'phone_verified')
  bool get phoneVerified;
  @override
  @JsonKey(name: 'email_verified')
  bool get emailVerified;
  @override
  @JsonKey(name: 'speaker_verified')
  bool get speakerVerified;
  @override
  @JsonKey(name: 'community_verified')
  bool get communityVerified;

  /// Create a copy of VerificationBadgesDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$VerificationBadgesDtoImplCopyWith<_$VerificationBadgesDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

EventDnaTagDto _$EventDnaTagDtoFromJson(Map<String, dynamic> json) {
  return _EventDnaTagDto.fromJson(json);
}

/// @nodoc
mixin _$EventDnaTagDto {
  String get kind => throw _privateConstructorUsedError;
  int get count => throw _privateConstructorUsedError;

  /// Serializes this EventDnaTagDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventDnaTagDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventDnaTagDtoCopyWith<EventDnaTagDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventDnaTagDtoCopyWith<$Res> {
  factory $EventDnaTagDtoCopyWith(
    EventDnaTagDto value,
    $Res Function(EventDnaTagDto) then,
  ) = _$EventDnaTagDtoCopyWithImpl<$Res, EventDnaTagDto>;
  @useResult
  $Res call({String kind, int count});
}

/// @nodoc
class _$EventDnaTagDtoCopyWithImpl<$Res, $Val extends EventDnaTagDto>
    implements $EventDnaTagDtoCopyWith<$Res> {
  _$EventDnaTagDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventDnaTagDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? kind = null, Object? count = null}) {
    return _then(
      _value.copyWith(
            kind: null == kind
                ? _value.kind
                : kind // ignore: cast_nullable_to_non_nullable
                      as String,
            count: null == count
                ? _value.count
                : count // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EventDnaTagDtoImplCopyWith<$Res>
    implements $EventDnaTagDtoCopyWith<$Res> {
  factory _$$EventDnaTagDtoImplCopyWith(
    _$EventDnaTagDtoImpl value,
    $Res Function(_$EventDnaTagDtoImpl) then,
  ) = __$$EventDnaTagDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({String kind, int count});
}

/// @nodoc
class __$$EventDnaTagDtoImplCopyWithImpl<$Res>
    extends _$EventDnaTagDtoCopyWithImpl<$Res, _$EventDnaTagDtoImpl>
    implements _$$EventDnaTagDtoImplCopyWith<$Res> {
  __$$EventDnaTagDtoImplCopyWithImpl(
    _$EventDnaTagDtoImpl _value,
    $Res Function(_$EventDnaTagDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventDnaTagDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? kind = null, Object? count = null}) {
    return _then(
      _$EventDnaTagDtoImpl(
        kind: null == kind
            ? _value.kind
            : kind // ignore: cast_nullable_to_non_nullable
                  as String,
        count: null == count
            ? _value.count
            : count // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventDnaTagDtoImpl implements _EventDnaTagDto {
  const _$EventDnaTagDtoImpl({required this.kind, required this.count});

  factory _$EventDnaTagDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventDnaTagDtoImplFromJson(json);

  @override
  final String kind;
  @override
  final int count;

  @override
  String toString() {
    return 'EventDnaTagDto(kind: $kind, count: $count)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventDnaTagDtoImpl &&
            (identical(other.kind, kind) || other.kind == kind) &&
            (identical(other.count, count) || other.count == count));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, kind, count);

  /// Create a copy of EventDnaTagDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventDnaTagDtoImplCopyWith<_$EventDnaTagDtoImpl> get copyWith =>
      __$$EventDnaTagDtoImplCopyWithImpl<_$EventDnaTagDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventDnaTagDtoImplToJson(this);
  }
}

abstract class _EventDnaTagDto implements EventDnaTagDto {
  const factory _EventDnaTagDto({
    required final String kind,
    required final int count,
  }) = _$EventDnaTagDtoImpl;

  factory _EventDnaTagDto.fromJson(Map<String, dynamic> json) =
      _$EventDnaTagDtoImpl.fromJson;

  @override
  String get kind;
  @override
  int get count;

  /// Create a copy of EventDnaTagDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventDnaTagDtoImplCopyWith<_$EventDnaTagDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

AchievementCardDto _$AchievementCardDtoFromJson(Map<String, dynamic> json) {
  return _AchievementCardDto.fromJson(json);
}

/// @nodoc
mixin _$AchievementCardDto {
  String get name => throw _privateConstructorUsedError;
  String? get description => throw _privateConstructorUsedError;
  @JsonKey(name: 'icon_key')
  String? get iconKey => throw _privateConstructorUsedError;
  @JsonKey(name: 'earned_at')
  DateTime get earnedAt => throw _privateConstructorUsedError;
  String get source => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_title')
  String? get eventTitle => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_slug')
  String? get eventSlug => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_name')
  String? get orgName => throw _privateConstructorUsedError;

  /// Serializes this AchievementCardDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of AchievementCardDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $AchievementCardDtoCopyWith<AchievementCardDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $AchievementCardDtoCopyWith<$Res> {
  factory $AchievementCardDtoCopyWith(
    AchievementCardDto value,
    $Res Function(AchievementCardDto) then,
  ) = _$AchievementCardDtoCopyWithImpl<$Res, AchievementCardDto>;
  @useResult
  $Res call({
    String name,
    String? description,
    @JsonKey(name: 'icon_key') String? iconKey,
    @JsonKey(name: 'earned_at') DateTime earnedAt,
    String source,
    @JsonKey(name: 'event_title') String? eventTitle,
    @JsonKey(name: 'event_slug') String? eventSlug,
    @JsonKey(name: 'org_name') String? orgName,
  });
}

/// @nodoc
class _$AchievementCardDtoCopyWithImpl<$Res, $Val extends AchievementCardDto>
    implements $AchievementCardDtoCopyWith<$Res> {
  _$AchievementCardDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of AchievementCardDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? name = null,
    Object? description = freezed,
    Object? iconKey = freezed,
    Object? earnedAt = null,
    Object? source = null,
    Object? eventTitle = freezed,
    Object? eventSlug = freezed,
    Object? orgName = freezed,
  }) {
    return _then(
      _value.copyWith(
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            description: freezed == description
                ? _value.description
                : description // ignore: cast_nullable_to_non_nullable
                      as String?,
            iconKey: freezed == iconKey
                ? _value.iconKey
                : iconKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            earnedAt: null == earnedAt
                ? _value.earnedAt
                : earnedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            source: null == source
                ? _value.source
                : source // ignore: cast_nullable_to_non_nullable
                      as String,
            eventTitle: freezed == eventTitle
                ? _value.eventTitle
                : eventTitle // ignore: cast_nullable_to_non_nullable
                      as String?,
            eventSlug: freezed == eventSlug
                ? _value.eventSlug
                : eventSlug // ignore: cast_nullable_to_non_nullable
                      as String?,
            orgName: freezed == orgName
                ? _value.orgName
                : orgName // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$AchievementCardDtoImplCopyWith<$Res>
    implements $AchievementCardDtoCopyWith<$Res> {
  factory _$$AchievementCardDtoImplCopyWith(
    _$AchievementCardDtoImpl value,
    $Res Function(_$AchievementCardDtoImpl) then,
  ) = __$$AchievementCardDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String name,
    String? description,
    @JsonKey(name: 'icon_key') String? iconKey,
    @JsonKey(name: 'earned_at') DateTime earnedAt,
    String source,
    @JsonKey(name: 'event_title') String? eventTitle,
    @JsonKey(name: 'event_slug') String? eventSlug,
    @JsonKey(name: 'org_name') String? orgName,
  });
}

/// @nodoc
class __$$AchievementCardDtoImplCopyWithImpl<$Res>
    extends _$AchievementCardDtoCopyWithImpl<$Res, _$AchievementCardDtoImpl>
    implements _$$AchievementCardDtoImplCopyWith<$Res> {
  __$$AchievementCardDtoImplCopyWithImpl(
    _$AchievementCardDtoImpl _value,
    $Res Function(_$AchievementCardDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of AchievementCardDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? name = null,
    Object? description = freezed,
    Object? iconKey = freezed,
    Object? earnedAt = null,
    Object? source = null,
    Object? eventTitle = freezed,
    Object? eventSlug = freezed,
    Object? orgName = freezed,
  }) {
    return _then(
      _$AchievementCardDtoImpl(
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        description: freezed == description
            ? _value.description
            : description // ignore: cast_nullable_to_non_nullable
                  as String?,
        iconKey: freezed == iconKey
            ? _value.iconKey
            : iconKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        earnedAt: null == earnedAt
            ? _value.earnedAt
            : earnedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        source: null == source
            ? _value.source
            : source // ignore: cast_nullable_to_non_nullable
                  as String,
        eventTitle: freezed == eventTitle
            ? _value.eventTitle
            : eventTitle // ignore: cast_nullable_to_non_nullable
                  as String?,
        eventSlug: freezed == eventSlug
            ? _value.eventSlug
            : eventSlug // ignore: cast_nullable_to_non_nullable
                  as String?,
        orgName: freezed == orgName
            ? _value.orgName
            : orgName // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$AchievementCardDtoImpl implements _AchievementCardDto {
  const _$AchievementCardDtoImpl({
    required this.name,
    this.description,
    @JsonKey(name: 'icon_key') this.iconKey,
    @JsonKey(name: 'earned_at') required this.earnedAt,
    required this.source,
    @JsonKey(name: 'event_title') this.eventTitle,
    @JsonKey(name: 'event_slug') this.eventSlug,
    @JsonKey(name: 'org_name') this.orgName,
  });

  factory _$AchievementCardDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$AchievementCardDtoImplFromJson(json);

  @override
  final String name;
  @override
  final String? description;
  @override
  @JsonKey(name: 'icon_key')
  final String? iconKey;
  @override
  @JsonKey(name: 'earned_at')
  final DateTime earnedAt;
  @override
  final String source;
  @override
  @JsonKey(name: 'event_title')
  final String? eventTitle;
  @override
  @JsonKey(name: 'event_slug')
  final String? eventSlug;
  @override
  @JsonKey(name: 'org_name')
  final String? orgName;

  @override
  String toString() {
    return 'AchievementCardDto(name: $name, description: $description, iconKey: $iconKey, earnedAt: $earnedAt, source: $source, eventTitle: $eventTitle, eventSlug: $eventSlug, orgName: $orgName)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$AchievementCardDtoImpl &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.description, description) ||
                other.description == description) &&
            (identical(other.iconKey, iconKey) || other.iconKey == iconKey) &&
            (identical(other.earnedAt, earnedAt) ||
                other.earnedAt == earnedAt) &&
            (identical(other.source, source) || other.source == source) &&
            (identical(other.eventTitle, eventTitle) ||
                other.eventTitle == eventTitle) &&
            (identical(other.eventSlug, eventSlug) ||
                other.eventSlug == eventSlug) &&
            (identical(other.orgName, orgName) || other.orgName == orgName));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    name,
    description,
    iconKey,
    earnedAt,
    source,
    eventTitle,
    eventSlug,
    orgName,
  );

  /// Create a copy of AchievementCardDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$AchievementCardDtoImplCopyWith<_$AchievementCardDtoImpl> get copyWith =>
      __$$AchievementCardDtoImplCopyWithImpl<_$AchievementCardDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$AchievementCardDtoImplToJson(this);
  }
}

abstract class _AchievementCardDto implements AchievementCardDto {
  const factory _AchievementCardDto({
    required final String name,
    final String? description,
    @JsonKey(name: 'icon_key') final String? iconKey,
    @JsonKey(name: 'earned_at') required final DateTime earnedAt,
    required final String source,
    @JsonKey(name: 'event_title') final String? eventTitle,
    @JsonKey(name: 'event_slug') final String? eventSlug,
    @JsonKey(name: 'org_name') final String? orgName,
  }) = _$AchievementCardDtoImpl;

  factory _AchievementCardDto.fromJson(Map<String, dynamic> json) =
      _$AchievementCardDtoImpl.fromJson;

  @override
  String get name;
  @override
  String? get description;
  @override
  @JsonKey(name: 'icon_key')
  String? get iconKey;
  @override
  @JsonKey(name: 'earned_at')
  DateTime get earnedAt;
  @override
  String get source;
  @override
  @JsonKey(name: 'event_title')
  String? get eventTitle;
  @override
  @JsonKey(name: 'event_slug')
  String? get eventSlug;
  @override
  @JsonKey(name: 'org_name')
  String? get orgName;

  /// Create a copy of AchievementCardDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$AchievementCardDtoImplCopyWith<_$AchievementCardDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

ProfileOrgDto _$ProfileOrgDtoFromJson(Map<String, dynamic> json) {
  return _ProfileOrgDto.fromJson(json);
}

/// @nodoc
mixin _$ProfileOrgDto {
  @JsonKey(name: 'org_id')
  String get orgId => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_name')
  String get orgName => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_slug')
  String get orgSlug => throw _privateConstructorUsedError;
  @JsonKey(name: 'logo_key')
  String? get logoKey => throw _privateConstructorUsedError;
  List<String> get roles => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_verified')
  bool get isVerified => throw _privateConstructorUsedError;
  @JsonKey(name: 'joined_at')
  DateTime get joinedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'valid_until')
  DateTime? get validUntil => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_events_conducted')
  int get orgEventsConducted => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_certificates_count')
  int get orgCertificatesCount => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_achievements_count')
  int get orgAchievementsCount => throw _privateConstructorUsedError;

  /// Serializes this ProfileOrgDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ProfileOrgDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ProfileOrgDtoCopyWith<ProfileOrgDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ProfileOrgDtoCopyWith<$Res> {
  factory $ProfileOrgDtoCopyWith(
    ProfileOrgDto value,
    $Res Function(ProfileOrgDto) then,
  ) = _$ProfileOrgDtoCopyWithImpl<$Res, ProfileOrgDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'org_name') String orgName,
    @JsonKey(name: 'org_slug') String orgSlug,
    @JsonKey(name: 'logo_key') String? logoKey,
    List<String> roles,
    @JsonKey(name: 'is_verified') bool isVerified,
    @JsonKey(name: 'joined_at') DateTime joinedAt,
    @JsonKey(name: 'valid_until') DateTime? validUntil,
    @JsonKey(name: 'org_events_conducted') int orgEventsConducted,
    @JsonKey(name: 'org_certificates_count') int orgCertificatesCount,
    @JsonKey(name: 'org_achievements_count') int orgAchievementsCount,
  });
}

/// @nodoc
class _$ProfileOrgDtoCopyWithImpl<$Res, $Val extends ProfileOrgDto>
    implements $ProfileOrgDtoCopyWith<$Res> {
  _$ProfileOrgDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ProfileOrgDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? orgId = null,
    Object? orgName = null,
    Object? orgSlug = null,
    Object? logoKey = freezed,
    Object? roles = null,
    Object? isVerified = null,
    Object? joinedAt = null,
    Object? validUntil = freezed,
    Object? orgEventsConducted = null,
    Object? orgCertificatesCount = null,
    Object? orgAchievementsCount = null,
  }) {
    return _then(
      _value.copyWith(
            orgId: null == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String,
            orgName: null == orgName
                ? _value.orgName
                : orgName // ignore: cast_nullable_to_non_nullable
                      as String,
            orgSlug: null == orgSlug
                ? _value.orgSlug
                : orgSlug // ignore: cast_nullable_to_non_nullable
                      as String,
            logoKey: freezed == logoKey
                ? _value.logoKey
                : logoKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            roles: null == roles
                ? _value.roles
                : roles // ignore: cast_nullable_to_non_nullable
                      as List<String>,
            isVerified: null == isVerified
                ? _value.isVerified
                : isVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
            joinedAt: null == joinedAt
                ? _value.joinedAt
                : joinedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            validUntil: freezed == validUntil
                ? _value.validUntil
                : validUntil // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            orgEventsConducted: null == orgEventsConducted
                ? _value.orgEventsConducted
                : orgEventsConducted // ignore: cast_nullable_to_non_nullable
                      as int,
            orgCertificatesCount: null == orgCertificatesCount
                ? _value.orgCertificatesCount
                : orgCertificatesCount // ignore: cast_nullable_to_non_nullable
                      as int,
            orgAchievementsCount: null == orgAchievementsCount
                ? _value.orgAchievementsCount
                : orgAchievementsCount // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ProfileOrgDtoImplCopyWith<$Res>
    implements $ProfileOrgDtoCopyWith<$Res> {
  factory _$$ProfileOrgDtoImplCopyWith(
    _$ProfileOrgDtoImpl value,
    $Res Function(_$ProfileOrgDtoImpl) then,
  ) = __$$ProfileOrgDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'org_name') String orgName,
    @JsonKey(name: 'org_slug') String orgSlug,
    @JsonKey(name: 'logo_key') String? logoKey,
    List<String> roles,
    @JsonKey(name: 'is_verified') bool isVerified,
    @JsonKey(name: 'joined_at') DateTime joinedAt,
    @JsonKey(name: 'valid_until') DateTime? validUntil,
    @JsonKey(name: 'org_events_conducted') int orgEventsConducted,
    @JsonKey(name: 'org_certificates_count') int orgCertificatesCount,
    @JsonKey(name: 'org_achievements_count') int orgAchievementsCount,
  });
}

/// @nodoc
class __$$ProfileOrgDtoImplCopyWithImpl<$Res>
    extends _$ProfileOrgDtoCopyWithImpl<$Res, _$ProfileOrgDtoImpl>
    implements _$$ProfileOrgDtoImplCopyWith<$Res> {
  __$$ProfileOrgDtoImplCopyWithImpl(
    _$ProfileOrgDtoImpl _value,
    $Res Function(_$ProfileOrgDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ProfileOrgDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? orgId = null,
    Object? orgName = null,
    Object? orgSlug = null,
    Object? logoKey = freezed,
    Object? roles = null,
    Object? isVerified = null,
    Object? joinedAt = null,
    Object? validUntil = freezed,
    Object? orgEventsConducted = null,
    Object? orgCertificatesCount = null,
    Object? orgAchievementsCount = null,
  }) {
    return _then(
      _$ProfileOrgDtoImpl(
        orgId: null == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String,
        orgName: null == orgName
            ? _value.orgName
            : orgName // ignore: cast_nullable_to_non_nullable
                  as String,
        orgSlug: null == orgSlug
            ? _value.orgSlug
            : orgSlug // ignore: cast_nullable_to_non_nullable
                  as String,
        logoKey: freezed == logoKey
            ? _value.logoKey
            : logoKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        roles: null == roles
            ? _value._roles
            : roles // ignore: cast_nullable_to_non_nullable
                  as List<String>,
        isVerified: null == isVerified
            ? _value.isVerified
            : isVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
        joinedAt: null == joinedAt
            ? _value.joinedAt
            : joinedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        validUntil: freezed == validUntil
            ? _value.validUntil
            : validUntil // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        orgEventsConducted: null == orgEventsConducted
            ? _value.orgEventsConducted
            : orgEventsConducted // ignore: cast_nullable_to_non_nullable
                  as int,
        orgCertificatesCount: null == orgCertificatesCount
            ? _value.orgCertificatesCount
            : orgCertificatesCount // ignore: cast_nullable_to_non_nullable
                  as int,
        orgAchievementsCount: null == orgAchievementsCount
            ? _value.orgAchievementsCount
            : orgAchievementsCount // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ProfileOrgDtoImpl implements _ProfileOrgDto {
  const _$ProfileOrgDtoImpl({
    @JsonKey(name: 'org_id') required this.orgId,
    @JsonKey(name: 'org_name') required this.orgName,
    @JsonKey(name: 'org_slug') required this.orgSlug,
    @JsonKey(name: 'logo_key') this.logoKey,
    final List<String> roles = const [],
    @JsonKey(name: 'is_verified') required this.isVerified,
    @JsonKey(name: 'joined_at') required this.joinedAt,
    @JsonKey(name: 'valid_until') this.validUntil,
    @JsonKey(name: 'org_events_conducted') required this.orgEventsConducted,
    @JsonKey(name: 'org_certificates_count') required this.orgCertificatesCount,
    @JsonKey(name: 'org_achievements_count') required this.orgAchievementsCount,
  }) : _roles = roles;

  factory _$ProfileOrgDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ProfileOrgDtoImplFromJson(json);

  @override
  @JsonKey(name: 'org_id')
  final String orgId;
  @override
  @JsonKey(name: 'org_name')
  final String orgName;
  @override
  @JsonKey(name: 'org_slug')
  final String orgSlug;
  @override
  @JsonKey(name: 'logo_key')
  final String? logoKey;
  final List<String> _roles;
  @override
  @JsonKey()
  List<String> get roles {
    if (_roles is EqualUnmodifiableListView) return _roles;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_roles);
  }

  @override
  @JsonKey(name: 'is_verified')
  final bool isVerified;
  @override
  @JsonKey(name: 'joined_at')
  final DateTime joinedAt;
  @override
  @JsonKey(name: 'valid_until')
  final DateTime? validUntil;
  @override
  @JsonKey(name: 'org_events_conducted')
  final int orgEventsConducted;
  @override
  @JsonKey(name: 'org_certificates_count')
  final int orgCertificatesCount;
  @override
  @JsonKey(name: 'org_achievements_count')
  final int orgAchievementsCount;

  @override
  String toString() {
    return 'ProfileOrgDto(orgId: $orgId, orgName: $orgName, orgSlug: $orgSlug, logoKey: $logoKey, roles: $roles, isVerified: $isVerified, joinedAt: $joinedAt, validUntil: $validUntil, orgEventsConducted: $orgEventsConducted, orgCertificatesCount: $orgCertificatesCount, orgAchievementsCount: $orgAchievementsCount)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ProfileOrgDtoImpl &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.orgName, orgName) || other.orgName == orgName) &&
            (identical(other.orgSlug, orgSlug) || other.orgSlug == orgSlug) &&
            (identical(other.logoKey, logoKey) || other.logoKey == logoKey) &&
            const DeepCollectionEquality().equals(other._roles, _roles) &&
            (identical(other.isVerified, isVerified) ||
                other.isVerified == isVerified) &&
            (identical(other.joinedAt, joinedAt) ||
                other.joinedAt == joinedAt) &&
            (identical(other.validUntil, validUntil) ||
                other.validUntil == validUntil) &&
            (identical(other.orgEventsConducted, orgEventsConducted) ||
                other.orgEventsConducted == orgEventsConducted) &&
            (identical(other.orgCertificatesCount, orgCertificatesCount) ||
                other.orgCertificatesCount == orgCertificatesCount) &&
            (identical(other.orgAchievementsCount, orgAchievementsCount) ||
                other.orgAchievementsCount == orgAchievementsCount));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    orgId,
    orgName,
    orgSlug,
    logoKey,
    const DeepCollectionEquality().hash(_roles),
    isVerified,
    joinedAt,
    validUntil,
    orgEventsConducted,
    orgCertificatesCount,
    orgAchievementsCount,
  );

  /// Create a copy of ProfileOrgDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ProfileOrgDtoImplCopyWith<_$ProfileOrgDtoImpl> get copyWith =>
      __$$ProfileOrgDtoImplCopyWithImpl<_$ProfileOrgDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$ProfileOrgDtoImplToJson(this);
  }
}

abstract class _ProfileOrgDto implements ProfileOrgDto {
  const factory _ProfileOrgDto({
    @JsonKey(name: 'org_id') required final String orgId,
    @JsonKey(name: 'org_name') required final String orgName,
    @JsonKey(name: 'org_slug') required final String orgSlug,
    @JsonKey(name: 'logo_key') final String? logoKey,
    final List<String> roles,
    @JsonKey(name: 'is_verified') required final bool isVerified,
    @JsonKey(name: 'joined_at') required final DateTime joinedAt,
    @JsonKey(name: 'valid_until') final DateTime? validUntil,
    @JsonKey(name: 'org_events_conducted')
    required final int orgEventsConducted,
    @JsonKey(name: 'org_certificates_count')
    required final int orgCertificatesCount,
    @JsonKey(name: 'org_achievements_count')
    required final int orgAchievementsCount,
  }) = _$ProfileOrgDtoImpl;

  factory _ProfileOrgDto.fromJson(Map<String, dynamic> json) =
      _$ProfileOrgDtoImpl.fromJson;

  @override
  @JsonKey(name: 'org_id')
  String get orgId;
  @override
  @JsonKey(name: 'org_name')
  String get orgName;
  @override
  @JsonKey(name: 'org_slug')
  String get orgSlug;
  @override
  @JsonKey(name: 'logo_key')
  String? get logoKey;
  @override
  List<String> get roles;
  @override
  @JsonKey(name: 'is_verified')
  bool get isVerified;
  @override
  @JsonKey(name: 'joined_at')
  DateTime get joinedAt;
  @override
  @JsonKey(name: 'valid_until')
  DateTime? get validUntil;
  @override
  @JsonKey(name: 'org_events_conducted')
  int get orgEventsConducted;
  @override
  @JsonKey(name: 'org_certificates_count')
  int get orgCertificatesCount;
  @override
  @JsonKey(name: 'org_achievements_count')
  int get orgAchievementsCount;

  /// Create a copy of ProfileOrgDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ProfileOrgDtoImplCopyWith<_$ProfileOrgDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

ProfileAssignmentDto _$ProfileAssignmentDtoFromJson(Map<String, dynamic> json) {
  return _ProfileAssignmentDto.fromJson(json);
}

/// @nodoc
mixin _$ProfileAssignmentDto {
  @JsonKey(name: 'event_id')
  String get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_title')
  String get eventTitle => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_slug')
  String get eventSlug => throw _privateConstructorUsedError;
  @JsonKey(name: 'banner_key')
  String? get bannerKey => throw _privateConstructorUsedError;
  String get role => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'completed_at')
  DateTime? get completedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'starts_at')
  DateTime get startsAt => throw _privateConstructorUsedError; // Null for a self-represented event (D-268) — there is no organization to name, and the
  // self-representation row is named after the person. `required` here threw on every such row.
  @JsonKey(name: 'org_name')
  String? get orgName => throw _privateConstructorUsedError;

  /// Serializes this ProfileAssignmentDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ProfileAssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ProfileAssignmentDtoCopyWith<ProfileAssignmentDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ProfileAssignmentDtoCopyWith<$Res> {
  factory $ProfileAssignmentDtoCopyWith(
    ProfileAssignmentDto value,
    $Res Function(ProfileAssignmentDto) then,
  ) = _$ProfileAssignmentDtoCopyWithImpl<$Res, ProfileAssignmentDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'event_title') String eventTitle,
    @JsonKey(name: 'event_slug') String eventSlug,
    @JsonKey(name: 'banner_key') String? bannerKey,
    String role,
    String status,
    @JsonKey(name: 'completed_at') DateTime? completedAt,
    @JsonKey(name: 'starts_at') DateTime startsAt,
    @JsonKey(name: 'org_name') String? orgName,
  });
}

/// @nodoc
class _$ProfileAssignmentDtoCopyWithImpl<
  $Res,
  $Val extends ProfileAssignmentDto
>
    implements $ProfileAssignmentDtoCopyWith<$Res> {
  _$ProfileAssignmentDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ProfileAssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? eventId = null,
    Object? eventTitle = null,
    Object? eventSlug = null,
    Object? bannerKey = freezed,
    Object? role = null,
    Object? status = null,
    Object? completedAt = freezed,
    Object? startsAt = null,
    Object? orgName = freezed,
  }) {
    return _then(
      _value.copyWith(
            eventId: null == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String,
            eventTitle: null == eventTitle
                ? _value.eventTitle
                : eventTitle // ignore: cast_nullable_to_non_nullable
                      as String,
            eventSlug: null == eventSlug
                ? _value.eventSlug
                : eventSlug // ignore: cast_nullable_to_non_nullable
                      as String,
            bannerKey: freezed == bannerKey
                ? _value.bannerKey
                : bannerKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            role: null == role
                ? _value.role
                : role // ignore: cast_nullable_to_non_nullable
                      as String,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            completedAt: freezed == completedAt
                ? _value.completedAt
                : completedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            startsAt: null == startsAt
                ? _value.startsAt
                : startsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            orgName: freezed == orgName
                ? _value.orgName
                : orgName // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ProfileAssignmentDtoImplCopyWith<$Res>
    implements $ProfileAssignmentDtoCopyWith<$Res> {
  factory _$$ProfileAssignmentDtoImplCopyWith(
    _$ProfileAssignmentDtoImpl value,
    $Res Function(_$ProfileAssignmentDtoImpl) then,
  ) = __$$ProfileAssignmentDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'event_title') String eventTitle,
    @JsonKey(name: 'event_slug') String eventSlug,
    @JsonKey(name: 'banner_key') String? bannerKey,
    String role,
    String status,
    @JsonKey(name: 'completed_at') DateTime? completedAt,
    @JsonKey(name: 'starts_at') DateTime startsAt,
    @JsonKey(name: 'org_name') String? orgName,
  });
}

/// @nodoc
class __$$ProfileAssignmentDtoImplCopyWithImpl<$Res>
    extends _$ProfileAssignmentDtoCopyWithImpl<$Res, _$ProfileAssignmentDtoImpl>
    implements _$$ProfileAssignmentDtoImplCopyWith<$Res> {
  __$$ProfileAssignmentDtoImplCopyWithImpl(
    _$ProfileAssignmentDtoImpl _value,
    $Res Function(_$ProfileAssignmentDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ProfileAssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? eventId = null,
    Object? eventTitle = null,
    Object? eventSlug = null,
    Object? bannerKey = freezed,
    Object? role = null,
    Object? status = null,
    Object? completedAt = freezed,
    Object? startsAt = null,
    Object? orgName = freezed,
  }) {
    return _then(
      _$ProfileAssignmentDtoImpl(
        eventId: null == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String,
        eventTitle: null == eventTitle
            ? _value.eventTitle
            : eventTitle // ignore: cast_nullable_to_non_nullable
                  as String,
        eventSlug: null == eventSlug
            ? _value.eventSlug
            : eventSlug // ignore: cast_nullable_to_non_nullable
                  as String,
        bannerKey: freezed == bannerKey
            ? _value.bannerKey
            : bannerKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        role: null == role
            ? _value.role
            : role // ignore: cast_nullable_to_non_nullable
                  as String,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        completedAt: freezed == completedAt
            ? _value.completedAt
            : completedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        startsAt: null == startsAt
            ? _value.startsAt
            : startsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        orgName: freezed == orgName
            ? _value.orgName
            : orgName // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ProfileAssignmentDtoImpl implements _ProfileAssignmentDto {
  const _$ProfileAssignmentDtoImpl({
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'event_title') required this.eventTitle,
    @JsonKey(name: 'event_slug') required this.eventSlug,
    @JsonKey(name: 'banner_key') this.bannerKey,
    required this.role,
    required this.status,
    @JsonKey(name: 'completed_at') this.completedAt,
    @JsonKey(name: 'starts_at') required this.startsAt,
    @JsonKey(name: 'org_name') this.orgName,
  });

  factory _$ProfileAssignmentDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ProfileAssignmentDtoImplFromJson(json);

  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'event_title')
  final String eventTitle;
  @override
  @JsonKey(name: 'event_slug')
  final String eventSlug;
  @override
  @JsonKey(name: 'banner_key')
  final String? bannerKey;
  @override
  final String role;
  @override
  final String status;
  @override
  @JsonKey(name: 'completed_at')
  final DateTime? completedAt;
  @override
  @JsonKey(name: 'starts_at')
  final DateTime startsAt;
  // Null for a self-represented event (D-268) — there is no organization to name, and the
  // self-representation row is named after the person. `required` here threw on every such row.
  @override
  @JsonKey(name: 'org_name')
  final String? orgName;

  @override
  String toString() {
    return 'ProfileAssignmentDto(eventId: $eventId, eventTitle: $eventTitle, eventSlug: $eventSlug, bannerKey: $bannerKey, role: $role, status: $status, completedAt: $completedAt, startsAt: $startsAt, orgName: $orgName)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ProfileAssignmentDtoImpl &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.eventTitle, eventTitle) ||
                other.eventTitle == eventTitle) &&
            (identical(other.eventSlug, eventSlug) ||
                other.eventSlug == eventSlug) &&
            (identical(other.bannerKey, bannerKey) ||
                other.bannerKey == bannerKey) &&
            (identical(other.role, role) || other.role == role) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.completedAt, completedAt) ||
                other.completedAt == completedAt) &&
            (identical(other.startsAt, startsAt) ||
                other.startsAt == startsAt) &&
            (identical(other.orgName, orgName) || other.orgName == orgName));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    eventId,
    eventTitle,
    eventSlug,
    bannerKey,
    role,
    status,
    completedAt,
    startsAt,
    orgName,
  );

  /// Create a copy of ProfileAssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ProfileAssignmentDtoImplCopyWith<_$ProfileAssignmentDtoImpl>
  get copyWith =>
      __$$ProfileAssignmentDtoImplCopyWithImpl<_$ProfileAssignmentDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$ProfileAssignmentDtoImplToJson(this);
  }
}

abstract class _ProfileAssignmentDto implements ProfileAssignmentDto {
  const factory _ProfileAssignmentDto({
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'event_title') required final String eventTitle,
    @JsonKey(name: 'event_slug') required final String eventSlug,
    @JsonKey(name: 'banner_key') final String? bannerKey,
    required final String role,
    required final String status,
    @JsonKey(name: 'completed_at') final DateTime? completedAt,
    @JsonKey(name: 'starts_at') required final DateTime startsAt,
    @JsonKey(name: 'org_name') final String? orgName,
  }) = _$ProfileAssignmentDtoImpl;

  factory _ProfileAssignmentDto.fromJson(Map<String, dynamic> json) =
      _$ProfileAssignmentDtoImpl.fromJson;

  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'event_title')
  String get eventTitle;
  @override
  @JsonKey(name: 'event_slug')
  String get eventSlug;
  @override
  @JsonKey(name: 'banner_key')
  String? get bannerKey;
  @override
  String get role;
  @override
  String get status;
  @override
  @JsonKey(name: 'completed_at')
  DateTime? get completedAt;
  @override
  @JsonKey(name: 'starts_at')
  DateTime get startsAt; // Null for a self-represented event (D-268) — there is no organization to name, and the
  // self-representation row is named after the person. `required` here threw on every such row.
  @override
  @JsonKey(name: 'org_name')
  String? get orgName;

  /// Create a copy of ProfileAssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ProfileAssignmentDtoImplCopyWith<_$ProfileAssignmentDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

PublicProfileDto _$PublicProfileDtoFromJson(Map<String, dynamic> json) {
  return _PublicProfileDto.fromJson(json);
}

/// @nodoc
mixin _$PublicProfileDto {
  String get id => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get username => throw _privateConstructorUsedError;
  String? get headline => throw _privateConstructorUsedError;
  String? get bio => throw _privateConstructorUsedError;
  String get summary => throw _privateConstructorUsedError;

  /// When the account was created, **month precision only** — ISO year-month, `"2026-08"`.
  ///
  /// A `String`, not a `DateTime`: `DateTime.parse('2026-08')` throws, and there is no instant here
  /// to parse. The server truncates deliberately so the exact signup time is never on a public wire
  /// (D-312); the owner's full timestamp comes from `/v1/me`'s `created_at` instead.
  ///
  /// Not to be confused with [ProfileOrgDto.joinedAt], which is when this person joined an
  /// organisation — a different object and a different fact.
  @JsonKey(name: 'joined_at')
  String? get joinedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'avatar_key')
  String? get avatarKey => throw _privateConstructorUsedError;
  @JsonKey(name: 'cover_key')
  String? get coverKey => throw _privateConstructorUsedError;
  CollegeDto? get college => throw _privateConstructorUsedError;

  /// The wire key is `links_json` (from `PublicProfileView.LinksJson`). Without the annotation
  /// json_serializable derived `links` from the field name, read a key the server never sends, and
  /// `_parseLinks` was handed null on every profile — so the About panel's social links rendered for
  /// nobody, silently. Web declared the same wrong key.
  @JsonKey(name: 'links_json')
  String? get links => throw _privateConstructorUsedError;
  List<String> get skills => throw _privateConstructorUsedError;

  /// Phase 2 (About), self-declared. Empty means "not stated" — the section is omitted rather
  /// than rendering a heading over nothing.
  List<String> get languages => throw _privateConstructorUsedError;

  /// Self-declared interests. Deliberately NOT [eventDna], which is what this person provably
  /// did: one is a claim, the other is proof, and the profile keeps them visibly apart (D-225).
  List<String> get interests => throw _privateConstructorUsedError;
  ProfileStatsDto get stats => throw _privateConstructorUsedError;
  VerificationBadgesDto get verification => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_dna')
  List<EventDnaTagDto> get eventDna => throw _privateConstructorUsedError;
  List<AchievementCardDto> get achievements =>
      throw _privateConstructorUsedError;
  @JsonKey(name: 'identity_labels')
  List<String> get identityLabels => throw _privateConstructorUsedError;

  /// The derived headline (D-225) — the short form of [identityLabels]. Distinct from [headline],
  /// which is the user's own self-declared line: one is proof, the other is a claim.
  @JsonKey(name: 'derived_headline')
  String get derivedHeadline => throw _privateConstructorUsedError;
  List<ProfileOrgDto> get organizations => throw _privateConstructorUsedError;

  /// Serializes this PublicProfileDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of PublicProfileDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $PublicProfileDtoCopyWith<PublicProfileDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $PublicProfileDtoCopyWith<$Res> {
  factory $PublicProfileDtoCopyWith(
    PublicProfileDto value,
    $Res Function(PublicProfileDto) then,
  ) = _$PublicProfileDtoCopyWithImpl<$Res, PublicProfileDto>;
  @useResult
  $Res call({
    String id,
    String name,
    String? username,
    String? headline,
    String? bio,
    String summary,
    @JsonKey(name: 'joined_at') String? joinedAt,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'cover_key') String? coverKey,
    CollegeDto? college,
    @JsonKey(name: 'links_json') String? links,
    List<String> skills,
    List<String> languages,
    List<String> interests,
    ProfileStatsDto stats,
    VerificationBadgesDto verification,
    @JsonKey(name: 'event_dna') List<EventDnaTagDto> eventDna,
    List<AchievementCardDto> achievements,
    @JsonKey(name: 'identity_labels') List<String> identityLabels,
    @JsonKey(name: 'derived_headline') String derivedHeadline,
    List<ProfileOrgDto> organizations,
  });

  $CollegeDtoCopyWith<$Res>? get college;
  $ProfileStatsDtoCopyWith<$Res> get stats;
  $VerificationBadgesDtoCopyWith<$Res> get verification;
}

/// @nodoc
class _$PublicProfileDtoCopyWithImpl<$Res, $Val extends PublicProfileDto>
    implements $PublicProfileDtoCopyWith<$Res> {
  _$PublicProfileDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of PublicProfileDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? username = freezed,
    Object? headline = freezed,
    Object? bio = freezed,
    Object? summary = null,
    Object? joinedAt = freezed,
    Object? avatarKey = freezed,
    Object? coverKey = freezed,
    Object? college = freezed,
    Object? links = freezed,
    Object? skills = null,
    Object? languages = null,
    Object? interests = null,
    Object? stats = null,
    Object? verification = null,
    Object? eventDna = null,
    Object? achievements = null,
    Object? identityLabels = null,
    Object? derivedHeadline = null,
    Object? organizations = null,
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
            username: freezed == username
                ? _value.username
                : username // ignore: cast_nullable_to_non_nullable
                      as String?,
            headline: freezed == headline
                ? _value.headline
                : headline // ignore: cast_nullable_to_non_nullable
                      as String?,
            bio: freezed == bio
                ? _value.bio
                : bio // ignore: cast_nullable_to_non_nullable
                      as String?,
            summary: null == summary
                ? _value.summary
                : summary // ignore: cast_nullable_to_non_nullable
                      as String,
            joinedAt: freezed == joinedAt
                ? _value.joinedAt
                : joinedAt // ignore: cast_nullable_to_non_nullable
                      as String?,
            avatarKey: freezed == avatarKey
                ? _value.avatarKey
                : avatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            coverKey: freezed == coverKey
                ? _value.coverKey
                : coverKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            college: freezed == college
                ? _value.college
                : college // ignore: cast_nullable_to_non_nullable
                      as CollegeDto?,
            links: freezed == links
                ? _value.links
                : links // ignore: cast_nullable_to_non_nullable
                      as String?,
            skills: null == skills
                ? _value.skills
                : skills // ignore: cast_nullable_to_non_nullable
                      as List<String>,
            languages: null == languages
                ? _value.languages
                : languages // ignore: cast_nullable_to_non_nullable
                      as List<String>,
            interests: null == interests
                ? _value.interests
                : interests // ignore: cast_nullable_to_non_nullable
                      as List<String>,
            stats: null == stats
                ? _value.stats
                : stats // ignore: cast_nullable_to_non_nullable
                      as ProfileStatsDto,
            verification: null == verification
                ? _value.verification
                : verification // ignore: cast_nullable_to_non_nullable
                      as VerificationBadgesDto,
            eventDna: null == eventDna
                ? _value.eventDna
                : eventDna // ignore: cast_nullable_to_non_nullable
                      as List<EventDnaTagDto>,
            achievements: null == achievements
                ? _value.achievements
                : achievements // ignore: cast_nullable_to_non_nullable
                      as List<AchievementCardDto>,
            identityLabels: null == identityLabels
                ? _value.identityLabels
                : identityLabels // ignore: cast_nullable_to_non_nullable
                      as List<String>,
            derivedHeadline: null == derivedHeadline
                ? _value.derivedHeadline
                : derivedHeadline // ignore: cast_nullable_to_non_nullable
                      as String,
            organizations: null == organizations
                ? _value.organizations
                : organizations // ignore: cast_nullable_to_non_nullable
                      as List<ProfileOrgDto>,
          )
          as $Val,
    );
  }

  /// Create a copy of PublicProfileDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $CollegeDtoCopyWith<$Res>? get college {
    if (_value.college == null) {
      return null;
    }

    return $CollegeDtoCopyWith<$Res>(_value.college!, (value) {
      return _then(_value.copyWith(college: value) as $Val);
    });
  }

  /// Create a copy of PublicProfileDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $ProfileStatsDtoCopyWith<$Res> get stats {
    return $ProfileStatsDtoCopyWith<$Res>(_value.stats, (value) {
      return _then(_value.copyWith(stats: value) as $Val);
    });
  }

  /// Create a copy of PublicProfileDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $VerificationBadgesDtoCopyWith<$Res> get verification {
    return $VerificationBadgesDtoCopyWith<$Res>(_value.verification, (value) {
      return _then(_value.copyWith(verification: value) as $Val);
    });
  }
}

/// @nodoc
abstract class _$$PublicProfileDtoImplCopyWith<$Res>
    implements $PublicProfileDtoCopyWith<$Res> {
  factory _$$PublicProfileDtoImplCopyWith(
    _$PublicProfileDtoImpl value,
    $Res Function(_$PublicProfileDtoImpl) then,
  ) = __$$PublicProfileDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String name,
    String? username,
    String? headline,
    String? bio,
    String summary,
    @JsonKey(name: 'joined_at') String? joinedAt,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'cover_key') String? coverKey,
    CollegeDto? college,
    @JsonKey(name: 'links_json') String? links,
    List<String> skills,
    List<String> languages,
    List<String> interests,
    ProfileStatsDto stats,
    VerificationBadgesDto verification,
    @JsonKey(name: 'event_dna') List<EventDnaTagDto> eventDna,
    List<AchievementCardDto> achievements,
    @JsonKey(name: 'identity_labels') List<String> identityLabels,
    @JsonKey(name: 'derived_headline') String derivedHeadline,
    List<ProfileOrgDto> organizations,
  });

  @override
  $CollegeDtoCopyWith<$Res>? get college;
  @override
  $ProfileStatsDtoCopyWith<$Res> get stats;
  @override
  $VerificationBadgesDtoCopyWith<$Res> get verification;
}

/// @nodoc
class __$$PublicProfileDtoImplCopyWithImpl<$Res>
    extends _$PublicProfileDtoCopyWithImpl<$Res, _$PublicProfileDtoImpl>
    implements _$$PublicProfileDtoImplCopyWith<$Res> {
  __$$PublicProfileDtoImplCopyWithImpl(
    _$PublicProfileDtoImpl _value,
    $Res Function(_$PublicProfileDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of PublicProfileDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? username = freezed,
    Object? headline = freezed,
    Object? bio = freezed,
    Object? summary = null,
    Object? joinedAt = freezed,
    Object? avatarKey = freezed,
    Object? coverKey = freezed,
    Object? college = freezed,
    Object? links = freezed,
    Object? skills = null,
    Object? languages = null,
    Object? interests = null,
    Object? stats = null,
    Object? verification = null,
    Object? eventDna = null,
    Object? achievements = null,
    Object? identityLabels = null,
    Object? derivedHeadline = null,
    Object? organizations = null,
  }) {
    return _then(
      _$PublicProfileDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        username: freezed == username
            ? _value.username
            : username // ignore: cast_nullable_to_non_nullable
                  as String?,
        headline: freezed == headline
            ? _value.headline
            : headline // ignore: cast_nullable_to_non_nullable
                  as String?,
        bio: freezed == bio
            ? _value.bio
            : bio // ignore: cast_nullable_to_non_nullable
                  as String?,
        summary: null == summary
            ? _value.summary
            : summary // ignore: cast_nullable_to_non_nullable
                  as String,
        joinedAt: freezed == joinedAt
            ? _value.joinedAt
            : joinedAt // ignore: cast_nullable_to_non_nullable
                  as String?,
        avatarKey: freezed == avatarKey
            ? _value.avatarKey
            : avatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        coverKey: freezed == coverKey
            ? _value.coverKey
            : coverKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        college: freezed == college
            ? _value.college
            : college // ignore: cast_nullable_to_non_nullable
                  as CollegeDto?,
        links: freezed == links
            ? _value.links
            : links // ignore: cast_nullable_to_non_nullable
                  as String?,
        skills: null == skills
            ? _value._skills
            : skills // ignore: cast_nullable_to_non_nullable
                  as List<String>,
        languages: null == languages
            ? _value._languages
            : languages // ignore: cast_nullable_to_non_nullable
                  as List<String>,
        interests: null == interests
            ? _value._interests
            : interests // ignore: cast_nullable_to_non_nullable
                  as List<String>,
        stats: null == stats
            ? _value.stats
            : stats // ignore: cast_nullable_to_non_nullable
                  as ProfileStatsDto,
        verification: null == verification
            ? _value.verification
            : verification // ignore: cast_nullable_to_non_nullable
                  as VerificationBadgesDto,
        eventDna: null == eventDna
            ? _value._eventDna
            : eventDna // ignore: cast_nullable_to_non_nullable
                  as List<EventDnaTagDto>,
        achievements: null == achievements
            ? _value._achievements
            : achievements // ignore: cast_nullable_to_non_nullable
                  as List<AchievementCardDto>,
        identityLabels: null == identityLabels
            ? _value._identityLabels
            : identityLabels // ignore: cast_nullable_to_non_nullable
                  as List<String>,
        derivedHeadline: null == derivedHeadline
            ? _value.derivedHeadline
            : derivedHeadline // ignore: cast_nullable_to_non_nullable
                  as String,
        organizations: null == organizations
            ? _value._organizations
            : organizations // ignore: cast_nullable_to_non_nullable
                  as List<ProfileOrgDto>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$PublicProfileDtoImpl implements _PublicProfileDto {
  const _$PublicProfileDtoImpl({
    required this.id,
    required this.name,
    this.username,
    this.headline,
    this.bio,
    required this.summary,
    @JsonKey(name: 'joined_at') this.joinedAt,
    @JsonKey(name: 'avatar_key') this.avatarKey,
    @JsonKey(name: 'cover_key') this.coverKey,
    this.college,
    @JsonKey(name: 'links_json') this.links,
    final List<String> skills = const [],
    final List<String> languages = const [],
    final List<String> interests = const [],
    required this.stats,
    required this.verification,
    @JsonKey(name: 'event_dna') final List<EventDnaTagDto> eventDna = const [],
    final List<AchievementCardDto> achievements = const [],
    @JsonKey(name: 'identity_labels')
    final List<String> identityLabels = const [],
    @JsonKey(name: 'derived_headline') this.derivedHeadline = '',
    final List<ProfileOrgDto> organizations = const [],
  }) : _skills = skills,
       _languages = languages,
       _interests = interests,
       _eventDna = eventDna,
       _achievements = achievements,
       _identityLabels = identityLabels,
       _organizations = organizations;

  factory _$PublicProfileDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$PublicProfileDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String name;
  @override
  final String? username;
  @override
  final String? headline;
  @override
  final String? bio;
  @override
  final String summary;

  /// When the account was created, **month precision only** — ISO year-month, `"2026-08"`.
  ///
  /// A `String`, not a `DateTime`: `DateTime.parse('2026-08')` throws, and there is no instant here
  /// to parse. The server truncates deliberately so the exact signup time is never on a public wire
  /// (D-312); the owner's full timestamp comes from `/v1/me`'s `created_at` instead.
  ///
  /// Not to be confused with [ProfileOrgDto.joinedAt], which is when this person joined an
  /// organisation — a different object and a different fact.
  @override
  @JsonKey(name: 'joined_at')
  final String? joinedAt;
  @override
  @JsonKey(name: 'avatar_key')
  final String? avatarKey;
  @override
  @JsonKey(name: 'cover_key')
  final String? coverKey;
  @override
  final CollegeDto? college;

  /// The wire key is `links_json` (from `PublicProfileView.LinksJson`). Without the annotation
  /// json_serializable derived `links` from the field name, read a key the server never sends, and
  /// `_parseLinks` was handed null on every profile — so the About panel's social links rendered for
  /// nobody, silently. Web declared the same wrong key.
  @override
  @JsonKey(name: 'links_json')
  final String? links;
  final List<String> _skills;
  @override
  @JsonKey()
  List<String> get skills {
    if (_skills is EqualUnmodifiableListView) return _skills;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_skills);
  }

  /// Phase 2 (About), self-declared. Empty means "not stated" — the section is omitted rather
  /// than rendering a heading over nothing.
  final List<String> _languages;

  /// Phase 2 (About), self-declared. Empty means "not stated" — the section is omitted rather
  /// than rendering a heading over nothing.
  @override
  @JsonKey()
  List<String> get languages {
    if (_languages is EqualUnmodifiableListView) return _languages;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_languages);
  }

  /// Self-declared interests. Deliberately NOT [eventDna], which is what this person provably
  /// did: one is a claim, the other is proof, and the profile keeps them visibly apart (D-225).
  final List<String> _interests;

  /// Self-declared interests. Deliberately NOT [eventDna], which is what this person provably
  /// did: one is a claim, the other is proof, and the profile keeps them visibly apart (D-225).
  @override
  @JsonKey()
  List<String> get interests {
    if (_interests is EqualUnmodifiableListView) return _interests;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_interests);
  }

  @override
  final ProfileStatsDto stats;
  @override
  final VerificationBadgesDto verification;
  final List<EventDnaTagDto> _eventDna;
  @override
  @JsonKey(name: 'event_dna')
  List<EventDnaTagDto> get eventDna {
    if (_eventDna is EqualUnmodifiableListView) return _eventDna;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_eventDna);
  }

  final List<AchievementCardDto> _achievements;
  @override
  @JsonKey()
  List<AchievementCardDto> get achievements {
    if (_achievements is EqualUnmodifiableListView) return _achievements;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_achievements);
  }

  final List<String> _identityLabels;
  @override
  @JsonKey(name: 'identity_labels')
  List<String> get identityLabels {
    if (_identityLabels is EqualUnmodifiableListView) return _identityLabels;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_identityLabels);
  }

  /// The derived headline (D-225) — the short form of [identityLabels]. Distinct from [headline],
  /// which is the user's own self-declared line: one is proof, the other is a claim.
  @override
  @JsonKey(name: 'derived_headline')
  final String derivedHeadline;
  final List<ProfileOrgDto> _organizations;
  @override
  @JsonKey()
  List<ProfileOrgDto> get organizations {
    if (_organizations is EqualUnmodifiableListView) return _organizations;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_organizations);
  }

  @override
  String toString() {
    return 'PublicProfileDto(id: $id, name: $name, username: $username, headline: $headline, bio: $bio, summary: $summary, joinedAt: $joinedAt, avatarKey: $avatarKey, coverKey: $coverKey, college: $college, links: $links, skills: $skills, languages: $languages, interests: $interests, stats: $stats, verification: $verification, eventDna: $eventDna, achievements: $achievements, identityLabels: $identityLabels, derivedHeadline: $derivedHeadline, organizations: $organizations)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$PublicProfileDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.headline, headline) ||
                other.headline == headline) &&
            (identical(other.bio, bio) || other.bio == bio) &&
            (identical(other.summary, summary) || other.summary == summary) &&
            (identical(other.joinedAt, joinedAt) ||
                other.joinedAt == joinedAt) &&
            (identical(other.avatarKey, avatarKey) ||
                other.avatarKey == avatarKey) &&
            (identical(other.coverKey, coverKey) ||
                other.coverKey == coverKey) &&
            (identical(other.college, college) || other.college == college) &&
            (identical(other.links, links) || other.links == links) &&
            const DeepCollectionEquality().equals(other._skills, _skills) &&
            const DeepCollectionEquality().equals(
              other._languages,
              _languages,
            ) &&
            const DeepCollectionEquality().equals(
              other._interests,
              _interests,
            ) &&
            (identical(other.stats, stats) || other.stats == stats) &&
            (identical(other.verification, verification) ||
                other.verification == verification) &&
            const DeepCollectionEquality().equals(other._eventDna, _eventDna) &&
            const DeepCollectionEquality().equals(
              other._achievements,
              _achievements,
            ) &&
            const DeepCollectionEquality().equals(
              other._identityLabels,
              _identityLabels,
            ) &&
            (identical(other.derivedHeadline, derivedHeadline) ||
                other.derivedHeadline == derivedHeadline) &&
            const DeepCollectionEquality().equals(
              other._organizations,
              _organizations,
            ));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hashAll([
    runtimeType,
    id,
    name,
    username,
    headline,
    bio,
    summary,
    joinedAt,
    avatarKey,
    coverKey,
    college,
    links,
    const DeepCollectionEquality().hash(_skills),
    const DeepCollectionEquality().hash(_languages),
    const DeepCollectionEquality().hash(_interests),
    stats,
    verification,
    const DeepCollectionEquality().hash(_eventDna),
    const DeepCollectionEquality().hash(_achievements),
    const DeepCollectionEquality().hash(_identityLabels),
    derivedHeadline,
    const DeepCollectionEquality().hash(_organizations),
  ]);

  /// Create a copy of PublicProfileDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$PublicProfileDtoImplCopyWith<_$PublicProfileDtoImpl> get copyWith =>
      __$$PublicProfileDtoImplCopyWithImpl<_$PublicProfileDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$PublicProfileDtoImplToJson(this);
  }
}

abstract class _PublicProfileDto implements PublicProfileDto {
  const factory _PublicProfileDto({
    required final String id,
    required final String name,
    final String? username,
    final String? headline,
    final String? bio,
    required final String summary,
    @JsonKey(name: 'joined_at') final String? joinedAt,
    @JsonKey(name: 'avatar_key') final String? avatarKey,
    @JsonKey(name: 'cover_key') final String? coverKey,
    final CollegeDto? college,
    @JsonKey(name: 'links_json') final String? links,
    final List<String> skills,
    final List<String> languages,
    final List<String> interests,
    required final ProfileStatsDto stats,
    required final VerificationBadgesDto verification,
    @JsonKey(name: 'event_dna') final List<EventDnaTagDto> eventDna,
    final List<AchievementCardDto> achievements,
    @JsonKey(name: 'identity_labels') final List<String> identityLabels,
    @JsonKey(name: 'derived_headline') final String derivedHeadline,
    final List<ProfileOrgDto> organizations,
  }) = _$PublicProfileDtoImpl;

  factory _PublicProfileDto.fromJson(Map<String, dynamic> json) =
      _$PublicProfileDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get name;
  @override
  String? get username;
  @override
  String? get headline;
  @override
  String? get bio;
  @override
  String get summary;

  /// When the account was created, **month precision only** — ISO year-month, `"2026-08"`.
  ///
  /// A `String`, not a `DateTime`: `DateTime.parse('2026-08')` throws, and there is no instant here
  /// to parse. The server truncates deliberately so the exact signup time is never on a public wire
  /// (D-312); the owner's full timestamp comes from `/v1/me`'s `created_at` instead.
  ///
  /// Not to be confused with [ProfileOrgDto.joinedAt], which is when this person joined an
  /// organisation — a different object and a different fact.
  @override
  @JsonKey(name: 'joined_at')
  String? get joinedAt;
  @override
  @JsonKey(name: 'avatar_key')
  String? get avatarKey;
  @override
  @JsonKey(name: 'cover_key')
  String? get coverKey;
  @override
  CollegeDto? get college;

  /// The wire key is `links_json` (from `PublicProfileView.LinksJson`). Without the annotation
  /// json_serializable derived `links` from the field name, read a key the server never sends, and
  /// `_parseLinks` was handed null on every profile — so the About panel's social links rendered for
  /// nobody, silently. Web declared the same wrong key.
  @override
  @JsonKey(name: 'links_json')
  String? get links;
  @override
  List<String> get skills;

  /// Phase 2 (About), self-declared. Empty means "not stated" — the section is omitted rather
  /// than rendering a heading over nothing.
  @override
  List<String> get languages;

  /// Self-declared interests. Deliberately NOT [eventDna], which is what this person provably
  /// did: one is a claim, the other is proof, and the profile keeps them visibly apart (D-225).
  @override
  List<String> get interests;
  @override
  ProfileStatsDto get stats;
  @override
  VerificationBadgesDto get verification;
  @override
  @JsonKey(name: 'event_dna')
  List<EventDnaTagDto> get eventDna;
  @override
  List<AchievementCardDto> get achievements;
  @override
  @JsonKey(name: 'identity_labels')
  List<String> get identityLabels;

  /// The derived headline (D-225) — the short form of [identityLabels]. Distinct from [headline],
  /// which is the user's own self-declared line: one is proof, the other is a claim.
  @override
  @JsonKey(name: 'derived_headline')
  String get derivedHeadline;
  @override
  List<ProfileOrgDto> get organizations;

  /// Create a copy of PublicProfileDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$PublicProfileDtoImplCopyWith<_$PublicProfileDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

PublicCertificateCardDto _$PublicCertificateCardDtoFromJson(
  Map<String, dynamic> json,
) {
  return _PublicCertificateCardDto.fromJson(json);
}

/// @nodoc
mixin _$PublicCertificateCardDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_title')
  String get eventTitle => throw _privateConstructorUsedError;
  @JsonKey(name: 'issued_at')
  DateTime get issuedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'verify_code')
  String get verifyCode => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_achievement')
  bool get isAchievement => throw _privateConstructorUsedError;

  /// Serializes this PublicCertificateCardDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of PublicCertificateCardDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $PublicCertificateCardDtoCopyWith<PublicCertificateCardDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $PublicCertificateCardDtoCopyWith<$Res> {
  factory $PublicCertificateCardDtoCopyWith(
    PublicCertificateCardDto value,
    $Res Function(PublicCertificateCardDto) then,
  ) = _$PublicCertificateCardDtoCopyWithImpl<$Res, PublicCertificateCardDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_title') String eventTitle,
    @JsonKey(name: 'issued_at') DateTime issuedAt,
    @JsonKey(name: 'verify_code') String verifyCode,
    @JsonKey(name: 'is_achievement') bool isAchievement,
  });
}

/// @nodoc
class _$PublicCertificateCardDtoCopyWithImpl<
  $Res,
  $Val extends PublicCertificateCardDto
>
    implements $PublicCertificateCardDtoCopyWith<$Res> {
  _$PublicCertificateCardDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of PublicCertificateCardDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventTitle = null,
    Object? issuedAt = null,
    Object? verifyCode = null,
    Object? isAchievement = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            eventTitle: null == eventTitle
                ? _value.eventTitle
                : eventTitle // ignore: cast_nullable_to_non_nullable
                      as String,
            issuedAt: null == issuedAt
                ? _value.issuedAt
                : issuedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            verifyCode: null == verifyCode
                ? _value.verifyCode
                : verifyCode // ignore: cast_nullable_to_non_nullable
                      as String,
            isAchievement: null == isAchievement
                ? _value.isAchievement
                : isAchievement // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$PublicCertificateCardDtoImplCopyWith<$Res>
    implements $PublicCertificateCardDtoCopyWith<$Res> {
  factory _$$PublicCertificateCardDtoImplCopyWith(
    _$PublicCertificateCardDtoImpl value,
    $Res Function(_$PublicCertificateCardDtoImpl) then,
  ) = __$$PublicCertificateCardDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_title') String eventTitle,
    @JsonKey(name: 'issued_at') DateTime issuedAt,
    @JsonKey(name: 'verify_code') String verifyCode,
    @JsonKey(name: 'is_achievement') bool isAchievement,
  });
}

/// @nodoc
class __$$PublicCertificateCardDtoImplCopyWithImpl<$Res>
    extends
        _$PublicCertificateCardDtoCopyWithImpl<
          $Res,
          _$PublicCertificateCardDtoImpl
        >
    implements _$$PublicCertificateCardDtoImplCopyWith<$Res> {
  __$$PublicCertificateCardDtoImplCopyWithImpl(
    _$PublicCertificateCardDtoImpl _value,
    $Res Function(_$PublicCertificateCardDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of PublicCertificateCardDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventTitle = null,
    Object? issuedAt = null,
    Object? verifyCode = null,
    Object? isAchievement = null,
  }) {
    return _then(
      _$PublicCertificateCardDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventTitle: null == eventTitle
            ? _value.eventTitle
            : eventTitle // ignore: cast_nullable_to_non_nullable
                  as String,
        issuedAt: null == issuedAt
            ? _value.issuedAt
            : issuedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        verifyCode: null == verifyCode
            ? _value.verifyCode
            : verifyCode // ignore: cast_nullable_to_non_nullable
                  as String,
        isAchievement: null == isAchievement
            ? _value.isAchievement
            : isAchievement // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$PublicCertificateCardDtoImpl implements _PublicCertificateCardDto {
  const _$PublicCertificateCardDtoImpl({
    required this.id,
    @JsonKey(name: 'event_title') required this.eventTitle,
    @JsonKey(name: 'issued_at') required this.issuedAt,
    @JsonKey(name: 'verify_code') required this.verifyCode,
    @JsonKey(name: 'is_achievement') required this.isAchievement,
  });

  factory _$PublicCertificateCardDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$PublicCertificateCardDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_title')
  final String eventTitle;
  @override
  @JsonKey(name: 'issued_at')
  final DateTime issuedAt;
  @override
  @JsonKey(name: 'verify_code')
  final String verifyCode;
  @override
  @JsonKey(name: 'is_achievement')
  final bool isAchievement;

  @override
  String toString() {
    return 'PublicCertificateCardDto(id: $id, eventTitle: $eventTitle, issuedAt: $issuedAt, verifyCode: $verifyCode, isAchievement: $isAchievement)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$PublicCertificateCardDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventTitle, eventTitle) ||
                other.eventTitle == eventTitle) &&
            (identical(other.issuedAt, issuedAt) ||
                other.issuedAt == issuedAt) &&
            (identical(other.verifyCode, verifyCode) ||
                other.verifyCode == verifyCode) &&
            (identical(other.isAchievement, isAchievement) ||
                other.isAchievement == isAchievement));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventTitle,
    issuedAt,
    verifyCode,
    isAchievement,
  );

  /// Create a copy of PublicCertificateCardDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$PublicCertificateCardDtoImplCopyWith<_$PublicCertificateCardDtoImpl>
  get copyWith =>
      __$$PublicCertificateCardDtoImplCopyWithImpl<
        _$PublicCertificateCardDtoImpl
      >(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$PublicCertificateCardDtoImplToJson(this);
  }
}

abstract class _PublicCertificateCardDto implements PublicCertificateCardDto {
  const factory _PublicCertificateCardDto({
    required final String id,
    @JsonKey(name: 'event_title') required final String eventTitle,
    @JsonKey(name: 'issued_at') required final DateTime issuedAt,
    @JsonKey(name: 'verify_code') required final String verifyCode,
    @JsonKey(name: 'is_achievement') required final bool isAchievement,
  }) = _$PublicCertificateCardDtoImpl;

  factory _PublicCertificateCardDto.fromJson(Map<String, dynamic> json) =
      _$PublicCertificateCardDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_title')
  String get eventTitle;
  @override
  @JsonKey(name: 'issued_at')
  DateTime get issuedAt;
  @override
  @JsonKey(name: 'verify_code')
  String get verifyCode;
  @override
  @JsonKey(name: 'is_achievement')
  bool get isAchievement;

  /// Create a copy of PublicCertificateCardDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$PublicCertificateCardDtoImplCopyWith<_$PublicCertificateCardDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

PublicEventCardDto _$PublicEventCardDtoFromJson(Map<String, dynamic> json) {
  return _PublicEventCardDto.fromJson(json);
}

/// @nodoc
mixin _$PublicEventCardDto {
  String get id => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String get slug => throw _privateConstructorUsedError;
  @JsonKey(name: 'banner_key')
  String? get bannerKey => throw _privateConstructorUsedError;
  @JsonKey(name: 'starts_at')
  DateTime get startsAt => throw _privateConstructorUsedError;
  String get city => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_name')
  String get orgName => throw _privateConstructorUsedError;
  List<String> get roles => throw _privateConstructorUsedError;
  String get visibility => throw _privateConstructorUsedError;
  @JsonKey(name: 'certificate_verify_code')
  String? get certificateVerifyCode => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_achievement')
  bool get isAchievement => throw _privateConstructorUsedError;

  /// Serializes this PublicEventCardDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of PublicEventCardDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $PublicEventCardDtoCopyWith<PublicEventCardDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $PublicEventCardDtoCopyWith<$Res> {
  factory $PublicEventCardDtoCopyWith(
    PublicEventCardDto value,
    $Res Function(PublicEventCardDto) then,
  ) = _$PublicEventCardDtoCopyWithImpl<$Res, PublicEventCardDto>;
  @useResult
  $Res call({
    String id,
    String title,
    String slug,
    @JsonKey(name: 'banner_key') String? bannerKey,
    @JsonKey(name: 'starts_at') DateTime startsAt,
    String city,
    @JsonKey(name: 'org_name') String orgName,
    List<String> roles,
    String visibility,
    @JsonKey(name: 'certificate_verify_code') String? certificateVerifyCode,
    @JsonKey(name: 'is_achievement') bool isAchievement,
  });
}

/// @nodoc
class _$PublicEventCardDtoCopyWithImpl<$Res, $Val extends PublicEventCardDto>
    implements $PublicEventCardDtoCopyWith<$Res> {
  _$PublicEventCardDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of PublicEventCardDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = null,
    Object? bannerKey = freezed,
    Object? startsAt = null,
    Object? city = null,
    Object? orgName = null,
    Object? roles = null,
    Object? visibility = null,
    Object? certificateVerifyCode = freezed,
    Object? isAchievement = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            title: null == title
                ? _value.title
                : title // ignore: cast_nullable_to_non_nullable
                      as String,
            slug: null == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String,
            bannerKey: freezed == bannerKey
                ? _value.bannerKey
                : bannerKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            startsAt: null == startsAt
                ? _value.startsAt
                : startsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            city: null == city
                ? _value.city
                : city // ignore: cast_nullable_to_non_nullable
                      as String,
            orgName: null == orgName
                ? _value.orgName
                : orgName // ignore: cast_nullable_to_non_nullable
                      as String,
            roles: null == roles
                ? _value.roles
                : roles // ignore: cast_nullable_to_non_nullable
                      as List<String>,
            visibility: null == visibility
                ? _value.visibility
                : visibility // ignore: cast_nullable_to_non_nullable
                      as String,
            certificateVerifyCode: freezed == certificateVerifyCode
                ? _value.certificateVerifyCode
                : certificateVerifyCode // ignore: cast_nullable_to_non_nullable
                      as String?,
            isAchievement: null == isAchievement
                ? _value.isAchievement
                : isAchievement // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$PublicEventCardDtoImplCopyWith<$Res>
    implements $PublicEventCardDtoCopyWith<$Res> {
  factory _$$PublicEventCardDtoImplCopyWith(
    _$PublicEventCardDtoImpl value,
    $Res Function(_$PublicEventCardDtoImpl) then,
  ) = __$$PublicEventCardDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String title,
    String slug,
    @JsonKey(name: 'banner_key') String? bannerKey,
    @JsonKey(name: 'starts_at') DateTime startsAt,
    String city,
    @JsonKey(name: 'org_name') String orgName,
    List<String> roles,
    String visibility,
    @JsonKey(name: 'certificate_verify_code') String? certificateVerifyCode,
    @JsonKey(name: 'is_achievement') bool isAchievement,
  });
}

/// @nodoc
class __$$PublicEventCardDtoImplCopyWithImpl<$Res>
    extends _$PublicEventCardDtoCopyWithImpl<$Res, _$PublicEventCardDtoImpl>
    implements _$$PublicEventCardDtoImplCopyWith<$Res> {
  __$$PublicEventCardDtoImplCopyWithImpl(
    _$PublicEventCardDtoImpl _value,
    $Res Function(_$PublicEventCardDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of PublicEventCardDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = null,
    Object? bannerKey = freezed,
    Object? startsAt = null,
    Object? city = null,
    Object? orgName = null,
    Object? roles = null,
    Object? visibility = null,
    Object? certificateVerifyCode = freezed,
    Object? isAchievement = null,
  }) {
    return _then(
      _$PublicEventCardDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        title: null == title
            ? _value.title
            : title // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: null == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String,
        bannerKey: freezed == bannerKey
            ? _value.bannerKey
            : bannerKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        startsAt: null == startsAt
            ? _value.startsAt
            : startsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        city: null == city
            ? _value.city
            : city // ignore: cast_nullable_to_non_nullable
                  as String,
        orgName: null == orgName
            ? _value.orgName
            : orgName // ignore: cast_nullable_to_non_nullable
                  as String,
        roles: null == roles
            ? _value._roles
            : roles // ignore: cast_nullable_to_non_nullable
                  as List<String>,
        visibility: null == visibility
            ? _value.visibility
            : visibility // ignore: cast_nullable_to_non_nullable
                  as String,
        certificateVerifyCode: freezed == certificateVerifyCode
            ? _value.certificateVerifyCode
            : certificateVerifyCode // ignore: cast_nullable_to_non_nullable
                  as String?,
        isAchievement: null == isAchievement
            ? _value.isAchievement
            : isAchievement // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$PublicEventCardDtoImpl implements _PublicEventCardDto {
  const _$PublicEventCardDtoImpl({
    required this.id,
    required this.title,
    required this.slug,
    @JsonKey(name: 'banner_key') this.bannerKey,
    @JsonKey(name: 'starts_at') required this.startsAt,
    required this.city,
    @JsonKey(name: 'org_name') required this.orgName,
    final List<String> roles = const [],
    required this.visibility,
    @JsonKey(name: 'certificate_verify_code') this.certificateVerifyCode,
    @JsonKey(name: 'is_achievement') required this.isAchievement,
  }) : _roles = roles;

  factory _$PublicEventCardDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$PublicEventCardDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String title;
  @override
  final String slug;
  @override
  @JsonKey(name: 'banner_key')
  final String? bannerKey;
  @override
  @JsonKey(name: 'starts_at')
  final DateTime startsAt;
  @override
  final String city;
  @override
  @JsonKey(name: 'org_name')
  final String orgName;
  final List<String> _roles;
  @override
  @JsonKey()
  List<String> get roles {
    if (_roles is EqualUnmodifiableListView) return _roles;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_roles);
  }

  @override
  final String visibility;
  @override
  @JsonKey(name: 'certificate_verify_code')
  final String? certificateVerifyCode;
  @override
  @JsonKey(name: 'is_achievement')
  final bool isAchievement;

  @override
  String toString() {
    return 'PublicEventCardDto(id: $id, title: $title, slug: $slug, bannerKey: $bannerKey, startsAt: $startsAt, city: $city, orgName: $orgName, roles: $roles, visibility: $visibility, certificateVerifyCode: $certificateVerifyCode, isAchievement: $isAchievement)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$PublicEventCardDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.bannerKey, bannerKey) ||
                other.bannerKey == bannerKey) &&
            (identical(other.startsAt, startsAt) ||
                other.startsAt == startsAt) &&
            (identical(other.city, city) || other.city == city) &&
            (identical(other.orgName, orgName) || other.orgName == orgName) &&
            const DeepCollectionEquality().equals(other._roles, _roles) &&
            (identical(other.visibility, visibility) ||
                other.visibility == visibility) &&
            (identical(other.certificateVerifyCode, certificateVerifyCode) ||
                other.certificateVerifyCode == certificateVerifyCode) &&
            (identical(other.isAchievement, isAchievement) ||
                other.isAchievement == isAchievement));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    title,
    slug,
    bannerKey,
    startsAt,
    city,
    orgName,
    const DeepCollectionEquality().hash(_roles),
    visibility,
    certificateVerifyCode,
    isAchievement,
  );

  /// Create a copy of PublicEventCardDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$PublicEventCardDtoImplCopyWith<_$PublicEventCardDtoImpl> get copyWith =>
      __$$PublicEventCardDtoImplCopyWithImpl<_$PublicEventCardDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$PublicEventCardDtoImplToJson(this);
  }
}

abstract class _PublicEventCardDto implements PublicEventCardDto {
  const factory _PublicEventCardDto({
    required final String id,
    required final String title,
    required final String slug,
    @JsonKey(name: 'banner_key') final String? bannerKey,
    @JsonKey(name: 'starts_at') required final DateTime startsAt,
    required final String city,
    @JsonKey(name: 'org_name') required final String orgName,
    final List<String> roles,
    required final String visibility,
    @JsonKey(name: 'certificate_verify_code')
    final String? certificateVerifyCode,
    @JsonKey(name: 'is_achievement') required final bool isAchievement,
  }) = _$PublicEventCardDtoImpl;

  factory _PublicEventCardDto.fromJson(Map<String, dynamic> json) =
      _$PublicEventCardDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get title;
  @override
  String get slug;
  @override
  @JsonKey(name: 'banner_key')
  String? get bannerKey;
  @override
  @JsonKey(name: 'starts_at')
  DateTime get startsAt;
  @override
  String get city;
  @override
  @JsonKey(name: 'org_name')
  String get orgName;
  @override
  List<String> get roles;
  @override
  String get visibility;
  @override
  @JsonKey(name: 'certificate_verify_code')
  String? get certificateVerifyCode;
  @override
  @JsonKey(name: 'is_achievement')
  bool get isAchievement;

  /// Create a copy of PublicEventCardDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$PublicEventCardDtoImplCopyWith<_$PublicEventCardDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

TimelineEntryDto _$TimelineEntryDtoFromJson(Map<String, dynamic> json) {
  return _TimelineEntryDto.fromJson(json);
}

/// @nodoc
mixin _$TimelineEntryDto {
  String get kind => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String? get slug => throw _privateConstructorUsedError;
  @JsonKey(name: 'banner_key')
  String? get bannerKey => throw _privateConstructorUsedError;
  List<String> get roles => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_name')
  String? get orgName => throw _privateConstructorUsedError;
  String? get city => throw _privateConstructorUsedError;
  @JsonKey(name: 'occurred_at')
  DateTime get occurredAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'verify_code')
  String? get verifyCode => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_first_event')
  bool get isFirstEvent => throw _privateConstructorUsedError;

  /// Serializes this TimelineEntryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TimelineEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TimelineEntryDtoCopyWith<TimelineEntryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TimelineEntryDtoCopyWith<$Res> {
  factory $TimelineEntryDtoCopyWith(
    TimelineEntryDto value,
    $Res Function(TimelineEntryDto) then,
  ) = _$TimelineEntryDtoCopyWithImpl<$Res, TimelineEntryDto>;
  @useResult
  $Res call({
    String kind,
    String title,
    String? slug,
    @JsonKey(name: 'banner_key') String? bannerKey,
    List<String> roles,
    @JsonKey(name: 'org_name') String? orgName,
    String? city,
    @JsonKey(name: 'occurred_at') DateTime occurredAt,
    @JsonKey(name: 'verify_code') String? verifyCode,
    @JsonKey(name: 'is_first_event') bool isFirstEvent,
  });
}

/// @nodoc
class _$TimelineEntryDtoCopyWithImpl<$Res, $Val extends TimelineEntryDto>
    implements $TimelineEntryDtoCopyWith<$Res> {
  _$TimelineEntryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TimelineEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? kind = null,
    Object? title = null,
    Object? slug = freezed,
    Object? bannerKey = freezed,
    Object? roles = null,
    Object? orgName = freezed,
    Object? city = freezed,
    Object? occurredAt = null,
    Object? verifyCode = freezed,
    Object? isFirstEvent = null,
  }) {
    return _then(
      _value.copyWith(
            kind: null == kind
                ? _value.kind
                : kind // ignore: cast_nullable_to_non_nullable
                      as String,
            title: null == title
                ? _value.title
                : title // ignore: cast_nullable_to_non_nullable
                      as String,
            slug: freezed == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String?,
            bannerKey: freezed == bannerKey
                ? _value.bannerKey
                : bannerKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            roles: null == roles
                ? _value.roles
                : roles // ignore: cast_nullable_to_non_nullable
                      as List<String>,
            orgName: freezed == orgName
                ? _value.orgName
                : orgName // ignore: cast_nullable_to_non_nullable
                      as String?,
            city: freezed == city
                ? _value.city
                : city // ignore: cast_nullable_to_non_nullable
                      as String?,
            occurredAt: null == occurredAt
                ? _value.occurredAt
                : occurredAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            verifyCode: freezed == verifyCode
                ? _value.verifyCode
                : verifyCode // ignore: cast_nullable_to_non_nullable
                      as String?,
            isFirstEvent: null == isFirstEvent
                ? _value.isFirstEvent
                : isFirstEvent // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TimelineEntryDtoImplCopyWith<$Res>
    implements $TimelineEntryDtoCopyWith<$Res> {
  factory _$$TimelineEntryDtoImplCopyWith(
    _$TimelineEntryDtoImpl value,
    $Res Function(_$TimelineEntryDtoImpl) then,
  ) = __$$TimelineEntryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String kind,
    String title,
    String? slug,
    @JsonKey(name: 'banner_key') String? bannerKey,
    List<String> roles,
    @JsonKey(name: 'org_name') String? orgName,
    String? city,
    @JsonKey(name: 'occurred_at') DateTime occurredAt,
    @JsonKey(name: 'verify_code') String? verifyCode,
    @JsonKey(name: 'is_first_event') bool isFirstEvent,
  });
}

/// @nodoc
class __$$TimelineEntryDtoImplCopyWithImpl<$Res>
    extends _$TimelineEntryDtoCopyWithImpl<$Res, _$TimelineEntryDtoImpl>
    implements _$$TimelineEntryDtoImplCopyWith<$Res> {
  __$$TimelineEntryDtoImplCopyWithImpl(
    _$TimelineEntryDtoImpl _value,
    $Res Function(_$TimelineEntryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TimelineEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? kind = null,
    Object? title = null,
    Object? slug = freezed,
    Object? bannerKey = freezed,
    Object? roles = null,
    Object? orgName = freezed,
    Object? city = freezed,
    Object? occurredAt = null,
    Object? verifyCode = freezed,
    Object? isFirstEvent = null,
  }) {
    return _then(
      _$TimelineEntryDtoImpl(
        kind: null == kind
            ? _value.kind
            : kind // ignore: cast_nullable_to_non_nullable
                  as String,
        title: null == title
            ? _value.title
            : title // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: freezed == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String?,
        bannerKey: freezed == bannerKey
            ? _value.bannerKey
            : bannerKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        roles: null == roles
            ? _value._roles
            : roles // ignore: cast_nullable_to_non_nullable
                  as List<String>,
        orgName: freezed == orgName
            ? _value.orgName
            : orgName // ignore: cast_nullable_to_non_nullable
                  as String?,
        city: freezed == city
            ? _value.city
            : city // ignore: cast_nullable_to_non_nullable
                  as String?,
        occurredAt: null == occurredAt
            ? _value.occurredAt
            : occurredAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        verifyCode: freezed == verifyCode
            ? _value.verifyCode
            : verifyCode // ignore: cast_nullable_to_non_nullable
                  as String?,
        isFirstEvent: null == isFirstEvent
            ? _value.isFirstEvent
            : isFirstEvent // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TimelineEntryDtoImpl implements _TimelineEntryDto {
  const _$TimelineEntryDtoImpl({
    required this.kind,
    required this.title,
    this.slug,
    @JsonKey(name: 'banner_key') this.bannerKey,
    final List<String> roles = const [],
    @JsonKey(name: 'org_name') this.orgName,
    this.city,
    @JsonKey(name: 'occurred_at') required this.occurredAt,
    @JsonKey(name: 'verify_code') this.verifyCode,
    @JsonKey(name: 'is_first_event') required this.isFirstEvent,
  }) : _roles = roles;

  factory _$TimelineEntryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TimelineEntryDtoImplFromJson(json);

  @override
  final String kind;
  @override
  final String title;
  @override
  final String? slug;
  @override
  @JsonKey(name: 'banner_key')
  final String? bannerKey;
  final List<String> _roles;
  @override
  @JsonKey()
  List<String> get roles {
    if (_roles is EqualUnmodifiableListView) return _roles;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_roles);
  }

  @override
  @JsonKey(name: 'org_name')
  final String? orgName;
  @override
  final String? city;
  @override
  @JsonKey(name: 'occurred_at')
  final DateTime occurredAt;
  @override
  @JsonKey(name: 'verify_code')
  final String? verifyCode;
  @override
  @JsonKey(name: 'is_first_event')
  final bool isFirstEvent;

  @override
  String toString() {
    return 'TimelineEntryDto(kind: $kind, title: $title, slug: $slug, bannerKey: $bannerKey, roles: $roles, orgName: $orgName, city: $city, occurredAt: $occurredAt, verifyCode: $verifyCode, isFirstEvent: $isFirstEvent)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TimelineEntryDtoImpl &&
            (identical(other.kind, kind) || other.kind == kind) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.bannerKey, bannerKey) ||
                other.bannerKey == bannerKey) &&
            const DeepCollectionEquality().equals(other._roles, _roles) &&
            (identical(other.orgName, orgName) || other.orgName == orgName) &&
            (identical(other.city, city) || other.city == city) &&
            (identical(other.occurredAt, occurredAt) ||
                other.occurredAt == occurredAt) &&
            (identical(other.verifyCode, verifyCode) ||
                other.verifyCode == verifyCode) &&
            (identical(other.isFirstEvent, isFirstEvent) ||
                other.isFirstEvent == isFirstEvent));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    kind,
    title,
    slug,
    bannerKey,
    const DeepCollectionEquality().hash(_roles),
    orgName,
    city,
    occurredAt,
    verifyCode,
    isFirstEvent,
  );

  /// Create a copy of TimelineEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TimelineEntryDtoImplCopyWith<_$TimelineEntryDtoImpl> get copyWith =>
      __$$TimelineEntryDtoImplCopyWithImpl<_$TimelineEntryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$TimelineEntryDtoImplToJson(this);
  }
}

abstract class _TimelineEntryDto implements TimelineEntryDto {
  const factory _TimelineEntryDto({
    required final String kind,
    required final String title,
    final String? slug,
    @JsonKey(name: 'banner_key') final String? bannerKey,
    final List<String> roles,
    @JsonKey(name: 'org_name') final String? orgName,
    final String? city,
    @JsonKey(name: 'occurred_at') required final DateTime occurredAt,
    @JsonKey(name: 'verify_code') final String? verifyCode,
    @JsonKey(name: 'is_first_event') required final bool isFirstEvent,
  }) = _$TimelineEntryDtoImpl;

  factory _TimelineEntryDto.fromJson(Map<String, dynamic> json) =
      _$TimelineEntryDtoImpl.fromJson;

  @override
  String get kind;
  @override
  String get title;
  @override
  String? get slug;
  @override
  @JsonKey(name: 'banner_key')
  String? get bannerKey;
  @override
  List<String> get roles;
  @override
  @JsonKey(name: 'org_name')
  String? get orgName;
  @override
  String? get city;
  @override
  @JsonKey(name: 'occurred_at')
  DateTime get occurredAt;
  @override
  @JsonKey(name: 'verify_code')
  String? get verifyCode;
  @override
  @JsonKey(name: 'is_first_event')
  bool get isFirstEvent;

  /// Create a copy of TimelineEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TimelineEntryDtoImplCopyWith<_$TimelineEntryDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

PublicUserSearchResultDto _$PublicUserSearchResultDtoFromJson(
  Map<String, dynamic> json,
) {
  return _PublicUserSearchResultDto.fromJson(json);
}

/// @nodoc
mixin _$PublicUserSearchResultDto {
  String get id => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String get username => throw _privateConstructorUsedError;
  @JsonKey(name: 'avatar_key')
  String? get avatarKey => throw _privateConstructorUsedError;
  String? get headline => throw _privateConstructorUsedError;

  /// Serializes this PublicUserSearchResultDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of PublicUserSearchResultDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $PublicUserSearchResultDtoCopyWith<PublicUserSearchResultDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $PublicUserSearchResultDtoCopyWith<$Res> {
  factory $PublicUserSearchResultDtoCopyWith(
    PublicUserSearchResultDto value,
    $Res Function(PublicUserSearchResultDto) then,
  ) = _$PublicUserSearchResultDtoCopyWithImpl<$Res, PublicUserSearchResultDto>;
  @useResult
  $Res call({
    String id,
    String name,
    String username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    String? headline,
  });
}

/// @nodoc
class _$PublicUserSearchResultDtoCopyWithImpl<
  $Res,
  $Val extends PublicUserSearchResultDto
>
    implements $PublicUserSearchResultDtoCopyWith<$Res> {
  _$PublicUserSearchResultDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of PublicUserSearchResultDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? username = null,
    Object? avatarKey = freezed,
    Object? headline = freezed,
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
            username: null == username
                ? _value.username
                : username // ignore: cast_nullable_to_non_nullable
                      as String,
            avatarKey: freezed == avatarKey
                ? _value.avatarKey
                : avatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            headline: freezed == headline
                ? _value.headline
                : headline // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$PublicUserSearchResultDtoImplCopyWith<$Res>
    implements $PublicUserSearchResultDtoCopyWith<$Res> {
  factory _$$PublicUserSearchResultDtoImplCopyWith(
    _$PublicUserSearchResultDtoImpl value,
    $Res Function(_$PublicUserSearchResultDtoImpl) then,
  ) = __$$PublicUserSearchResultDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String name,
    String username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    String? headline,
  });
}

/// @nodoc
class __$$PublicUserSearchResultDtoImplCopyWithImpl<$Res>
    extends
        _$PublicUserSearchResultDtoCopyWithImpl<
          $Res,
          _$PublicUserSearchResultDtoImpl
        >
    implements _$$PublicUserSearchResultDtoImplCopyWith<$Res> {
  __$$PublicUserSearchResultDtoImplCopyWithImpl(
    _$PublicUserSearchResultDtoImpl _value,
    $Res Function(_$PublicUserSearchResultDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of PublicUserSearchResultDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? username = null,
    Object? avatarKey = freezed,
    Object? headline = freezed,
  }) {
    return _then(
      _$PublicUserSearchResultDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        username: null == username
            ? _value.username
            : username // ignore: cast_nullable_to_non_nullable
                  as String,
        avatarKey: freezed == avatarKey
            ? _value.avatarKey
            : avatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        headline: freezed == headline
            ? _value.headline
            : headline // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$PublicUserSearchResultDtoImpl implements _PublicUserSearchResultDto {
  const _$PublicUserSearchResultDtoImpl({
    required this.id,
    required this.name,
    required this.username,
    @JsonKey(name: 'avatar_key') this.avatarKey,
    this.headline,
  });

  factory _$PublicUserSearchResultDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$PublicUserSearchResultDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String name;
  @override
  final String username;
  @override
  @JsonKey(name: 'avatar_key')
  final String? avatarKey;
  @override
  final String? headline;

  @override
  String toString() {
    return 'PublicUserSearchResultDto(id: $id, name: $name, username: $username, avatarKey: $avatarKey, headline: $headline)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$PublicUserSearchResultDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.avatarKey, avatarKey) ||
                other.avatarKey == avatarKey) &&
            (identical(other.headline, headline) ||
                other.headline == headline));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, id, name, username, avatarKey, headline);

  /// Create a copy of PublicUserSearchResultDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$PublicUserSearchResultDtoImplCopyWith<_$PublicUserSearchResultDtoImpl>
  get copyWith =>
      __$$PublicUserSearchResultDtoImplCopyWithImpl<
        _$PublicUserSearchResultDtoImpl
      >(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$PublicUserSearchResultDtoImplToJson(this);
  }
}

abstract class _PublicUserSearchResultDto implements PublicUserSearchResultDto {
  const factory _PublicUserSearchResultDto({
    required final String id,
    required final String name,
    required final String username,
    @JsonKey(name: 'avatar_key') final String? avatarKey,
    final String? headline,
  }) = _$PublicUserSearchResultDtoImpl;

  factory _PublicUserSearchResultDto.fromJson(Map<String, dynamic> json) =
      _$PublicUserSearchResultDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get name;
  @override
  String get username;
  @override
  @JsonKey(name: 'avatar_key')
  String? get avatarKey;
  @override
  String? get headline;

  /// Create a copy of PublicUserSearchResultDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$PublicUserSearchResultDtoImplCopyWith<_$PublicUserSearchResultDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

AllyProfileCardDto _$AllyProfileCardDtoFromJson(Map<String, dynamic> json) {
  return _AllyProfileCardDto.fromJson(json);
}

/// @nodoc
mixin _$AllyProfileCardDto {
  @JsonKey(name: 'user_id')
  String get userId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get username => throw _privateConstructorUsedError;
  @JsonKey(name: 'avatar_key')
  String? get avatarKey => throw _privateConstructorUsedError;
  @JsonKey(name: 'mutual_event_count')
  int get mutualEventCount => throw _privateConstructorUsedError;

  /// Serializes this AllyProfileCardDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of AllyProfileCardDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $AllyProfileCardDtoCopyWith<AllyProfileCardDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $AllyProfileCardDtoCopyWith<$Res> {
  factory $AllyProfileCardDtoCopyWith(
    AllyProfileCardDto value,
    $Res Function(AllyProfileCardDto) then,
  ) = _$AllyProfileCardDtoCopyWithImpl<$Res, AllyProfileCardDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'user_id') String userId,
    String name,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'mutual_event_count') int mutualEventCount,
  });
}

/// @nodoc
class _$AllyProfileCardDtoCopyWithImpl<$Res, $Val extends AllyProfileCardDto>
    implements $AllyProfileCardDtoCopyWith<$Res> {
  _$AllyProfileCardDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of AllyProfileCardDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? userId = null,
    Object? name = null,
    Object? username = freezed,
    Object? avatarKey = freezed,
    Object? mutualEventCount = null,
  }) {
    return _then(
      _value.copyWith(
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
            avatarKey: freezed == avatarKey
                ? _value.avatarKey
                : avatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            mutualEventCount: null == mutualEventCount
                ? _value.mutualEventCount
                : mutualEventCount // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$AllyProfileCardDtoImplCopyWith<$Res>
    implements $AllyProfileCardDtoCopyWith<$Res> {
  factory _$$AllyProfileCardDtoImplCopyWith(
    _$AllyProfileCardDtoImpl value,
    $Res Function(_$AllyProfileCardDtoImpl) then,
  ) = __$$AllyProfileCardDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'user_id') String userId,
    String name,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'mutual_event_count') int mutualEventCount,
  });
}

/// @nodoc
class __$$AllyProfileCardDtoImplCopyWithImpl<$Res>
    extends _$AllyProfileCardDtoCopyWithImpl<$Res, _$AllyProfileCardDtoImpl>
    implements _$$AllyProfileCardDtoImplCopyWith<$Res> {
  __$$AllyProfileCardDtoImplCopyWithImpl(
    _$AllyProfileCardDtoImpl _value,
    $Res Function(_$AllyProfileCardDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of AllyProfileCardDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? userId = null,
    Object? name = null,
    Object? username = freezed,
    Object? avatarKey = freezed,
    Object? mutualEventCount = null,
  }) {
    return _then(
      _$AllyProfileCardDtoImpl(
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
        avatarKey: freezed == avatarKey
            ? _value.avatarKey
            : avatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        mutualEventCount: null == mutualEventCount
            ? _value.mutualEventCount
            : mutualEventCount // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$AllyProfileCardDtoImpl implements _AllyProfileCardDto {
  const _$AllyProfileCardDtoImpl({
    @JsonKey(name: 'user_id') required this.userId,
    required this.name,
    this.username,
    @JsonKey(name: 'avatar_key') this.avatarKey,
    @JsonKey(name: 'mutual_event_count') required this.mutualEventCount,
  });

  factory _$AllyProfileCardDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$AllyProfileCardDtoImplFromJson(json);

  @override
  @JsonKey(name: 'user_id')
  final String userId;
  @override
  final String name;
  @override
  final String? username;
  @override
  @JsonKey(name: 'avatar_key')
  final String? avatarKey;
  @override
  @JsonKey(name: 'mutual_event_count')
  final int mutualEventCount;

  @override
  String toString() {
    return 'AllyProfileCardDto(userId: $userId, name: $name, username: $username, avatarKey: $avatarKey, mutualEventCount: $mutualEventCount)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$AllyProfileCardDtoImpl &&
            (identical(other.userId, userId) || other.userId == userId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.avatarKey, avatarKey) ||
                other.avatarKey == avatarKey) &&
            (identical(other.mutualEventCount, mutualEventCount) ||
                other.mutualEventCount == mutualEventCount));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    userId,
    name,
    username,
    avatarKey,
    mutualEventCount,
  );

  /// Create a copy of AllyProfileCardDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$AllyProfileCardDtoImplCopyWith<_$AllyProfileCardDtoImpl> get copyWith =>
      __$$AllyProfileCardDtoImplCopyWithImpl<_$AllyProfileCardDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$AllyProfileCardDtoImplToJson(this);
  }
}

abstract class _AllyProfileCardDto implements AllyProfileCardDto {
  const factory _AllyProfileCardDto({
    @JsonKey(name: 'user_id') required final String userId,
    required final String name,
    final String? username,
    @JsonKey(name: 'avatar_key') final String? avatarKey,
    @JsonKey(name: 'mutual_event_count') required final int mutualEventCount,
  }) = _$AllyProfileCardDtoImpl;

  factory _AllyProfileCardDto.fromJson(Map<String, dynamic> json) =
      _$AllyProfileCardDtoImpl.fromJson;

  @override
  @JsonKey(name: 'user_id')
  String get userId;
  @override
  String get name;
  @override
  String? get username;
  @override
  @JsonKey(name: 'avatar_key')
  String? get avatarKey;
  @override
  @JsonKey(name: 'mutual_event_count')
  int get mutualEventCount;

  /// Create a copy of AllyProfileCardDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$AllyProfileCardDtoImplCopyWith<_$AllyProfileCardDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

AllyConnectionDto _$AllyConnectionDtoFromJson(Map<String, dynamic> json) {
  return _AllyConnectionDto.fromJson(json);
}

/// @nodoc
mixin _$AllyConnectionDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'other_user_id')
  String get otherUserId => throw _privateConstructorUsedError;
  @JsonKey(name: 'other_name')
  String get otherName => throw _privateConstructorUsedError;
  @JsonKey(name: 'other_username')
  String? get otherUsername => throw _privateConstructorUsedError;
  @JsonKey(name: 'other_avatar_key')
  String? get otherAvatarKey => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  String get visibility => throw _privateConstructorUsedError;
  @JsonKey(name: 'requested_at')
  DateTime get requestedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'responded_at')
  DateTime? get respondedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'first_shared_event_id')
  String? get firstSharedEventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'first_shared_event_title')
  String? get firstSharedEventTitle => throw _privateConstructorUsedError;
  @JsonKey(name: 'first_shared_event_slug')
  String? get firstSharedEventSlug => throw _privateConstructorUsedError;

  /// Serializes this AllyConnectionDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of AllyConnectionDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $AllyConnectionDtoCopyWith<AllyConnectionDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $AllyConnectionDtoCopyWith<$Res> {
  factory $AllyConnectionDtoCopyWith(
    AllyConnectionDto value,
    $Res Function(AllyConnectionDto) then,
  ) = _$AllyConnectionDtoCopyWithImpl<$Res, AllyConnectionDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'other_user_id') String otherUserId,
    @JsonKey(name: 'other_name') String otherName,
    @JsonKey(name: 'other_username') String? otherUsername,
    @JsonKey(name: 'other_avatar_key') String? otherAvatarKey,
    String status,
    String visibility,
    @JsonKey(name: 'requested_at') DateTime requestedAt,
    @JsonKey(name: 'responded_at') DateTime? respondedAt,
    @JsonKey(name: 'first_shared_event_id') String? firstSharedEventId,
    @JsonKey(name: 'first_shared_event_title') String? firstSharedEventTitle,
    @JsonKey(name: 'first_shared_event_slug') String? firstSharedEventSlug,
  });
}

/// @nodoc
class _$AllyConnectionDtoCopyWithImpl<$Res, $Val extends AllyConnectionDto>
    implements $AllyConnectionDtoCopyWith<$Res> {
  _$AllyConnectionDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of AllyConnectionDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? otherUserId = null,
    Object? otherName = null,
    Object? otherUsername = freezed,
    Object? otherAvatarKey = freezed,
    Object? status = null,
    Object? visibility = null,
    Object? requestedAt = null,
    Object? respondedAt = freezed,
    Object? firstSharedEventId = freezed,
    Object? firstSharedEventTitle = freezed,
    Object? firstSharedEventSlug = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            otherUserId: null == otherUserId
                ? _value.otherUserId
                : otherUserId // ignore: cast_nullable_to_non_nullable
                      as String,
            otherName: null == otherName
                ? _value.otherName
                : otherName // ignore: cast_nullable_to_non_nullable
                      as String,
            otherUsername: freezed == otherUsername
                ? _value.otherUsername
                : otherUsername // ignore: cast_nullable_to_non_nullable
                      as String?,
            otherAvatarKey: freezed == otherAvatarKey
                ? _value.otherAvatarKey
                : otherAvatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            visibility: null == visibility
                ? _value.visibility
                : visibility // ignore: cast_nullable_to_non_nullable
                      as String,
            requestedAt: null == requestedAt
                ? _value.requestedAt
                : requestedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            respondedAt: freezed == respondedAt
                ? _value.respondedAt
                : respondedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            firstSharedEventId: freezed == firstSharedEventId
                ? _value.firstSharedEventId
                : firstSharedEventId // ignore: cast_nullable_to_non_nullable
                      as String?,
            firstSharedEventTitle: freezed == firstSharedEventTitle
                ? _value.firstSharedEventTitle
                : firstSharedEventTitle // ignore: cast_nullable_to_non_nullable
                      as String?,
            firstSharedEventSlug: freezed == firstSharedEventSlug
                ? _value.firstSharedEventSlug
                : firstSharedEventSlug // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$AllyConnectionDtoImplCopyWith<$Res>
    implements $AllyConnectionDtoCopyWith<$Res> {
  factory _$$AllyConnectionDtoImplCopyWith(
    _$AllyConnectionDtoImpl value,
    $Res Function(_$AllyConnectionDtoImpl) then,
  ) = __$$AllyConnectionDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'other_user_id') String otherUserId,
    @JsonKey(name: 'other_name') String otherName,
    @JsonKey(name: 'other_username') String? otherUsername,
    @JsonKey(name: 'other_avatar_key') String? otherAvatarKey,
    String status,
    String visibility,
    @JsonKey(name: 'requested_at') DateTime requestedAt,
    @JsonKey(name: 'responded_at') DateTime? respondedAt,
    @JsonKey(name: 'first_shared_event_id') String? firstSharedEventId,
    @JsonKey(name: 'first_shared_event_title') String? firstSharedEventTitle,
    @JsonKey(name: 'first_shared_event_slug') String? firstSharedEventSlug,
  });
}

/// @nodoc
class __$$AllyConnectionDtoImplCopyWithImpl<$Res>
    extends _$AllyConnectionDtoCopyWithImpl<$Res, _$AllyConnectionDtoImpl>
    implements _$$AllyConnectionDtoImplCopyWith<$Res> {
  __$$AllyConnectionDtoImplCopyWithImpl(
    _$AllyConnectionDtoImpl _value,
    $Res Function(_$AllyConnectionDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of AllyConnectionDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? otherUserId = null,
    Object? otherName = null,
    Object? otherUsername = freezed,
    Object? otherAvatarKey = freezed,
    Object? status = null,
    Object? visibility = null,
    Object? requestedAt = null,
    Object? respondedAt = freezed,
    Object? firstSharedEventId = freezed,
    Object? firstSharedEventTitle = freezed,
    Object? firstSharedEventSlug = freezed,
  }) {
    return _then(
      _$AllyConnectionDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        otherUserId: null == otherUserId
            ? _value.otherUserId
            : otherUserId // ignore: cast_nullable_to_non_nullable
                  as String,
        otherName: null == otherName
            ? _value.otherName
            : otherName // ignore: cast_nullable_to_non_nullable
                  as String,
        otherUsername: freezed == otherUsername
            ? _value.otherUsername
            : otherUsername // ignore: cast_nullable_to_non_nullable
                  as String?,
        otherAvatarKey: freezed == otherAvatarKey
            ? _value.otherAvatarKey
            : otherAvatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        visibility: null == visibility
            ? _value.visibility
            : visibility // ignore: cast_nullable_to_non_nullable
                  as String,
        requestedAt: null == requestedAt
            ? _value.requestedAt
            : requestedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        respondedAt: freezed == respondedAt
            ? _value.respondedAt
            : respondedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        firstSharedEventId: freezed == firstSharedEventId
            ? _value.firstSharedEventId
            : firstSharedEventId // ignore: cast_nullable_to_non_nullable
                  as String?,
        firstSharedEventTitle: freezed == firstSharedEventTitle
            ? _value.firstSharedEventTitle
            : firstSharedEventTitle // ignore: cast_nullable_to_non_nullable
                  as String?,
        firstSharedEventSlug: freezed == firstSharedEventSlug
            ? _value.firstSharedEventSlug
            : firstSharedEventSlug // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$AllyConnectionDtoImpl implements _AllyConnectionDto {
  const _$AllyConnectionDtoImpl({
    required this.id,
    @JsonKey(name: 'other_user_id') required this.otherUserId,
    @JsonKey(name: 'other_name') required this.otherName,
    @JsonKey(name: 'other_username') this.otherUsername,
    @JsonKey(name: 'other_avatar_key') this.otherAvatarKey,
    required this.status,
    required this.visibility,
    @JsonKey(name: 'requested_at') required this.requestedAt,
    @JsonKey(name: 'responded_at') this.respondedAt,
    @JsonKey(name: 'first_shared_event_id') this.firstSharedEventId,
    @JsonKey(name: 'first_shared_event_title') this.firstSharedEventTitle,
    @JsonKey(name: 'first_shared_event_slug') this.firstSharedEventSlug,
  });

  factory _$AllyConnectionDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$AllyConnectionDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'other_user_id')
  final String otherUserId;
  @override
  @JsonKey(name: 'other_name')
  final String otherName;
  @override
  @JsonKey(name: 'other_username')
  final String? otherUsername;
  @override
  @JsonKey(name: 'other_avatar_key')
  final String? otherAvatarKey;
  @override
  final String status;
  @override
  final String visibility;
  @override
  @JsonKey(name: 'requested_at')
  final DateTime requestedAt;
  @override
  @JsonKey(name: 'responded_at')
  final DateTime? respondedAt;
  @override
  @JsonKey(name: 'first_shared_event_id')
  final String? firstSharedEventId;
  @override
  @JsonKey(name: 'first_shared_event_title')
  final String? firstSharedEventTitle;
  @override
  @JsonKey(name: 'first_shared_event_slug')
  final String? firstSharedEventSlug;

  @override
  String toString() {
    return 'AllyConnectionDto(id: $id, otherUserId: $otherUserId, otherName: $otherName, otherUsername: $otherUsername, otherAvatarKey: $otherAvatarKey, status: $status, visibility: $visibility, requestedAt: $requestedAt, respondedAt: $respondedAt, firstSharedEventId: $firstSharedEventId, firstSharedEventTitle: $firstSharedEventTitle, firstSharedEventSlug: $firstSharedEventSlug)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$AllyConnectionDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.otherUserId, otherUserId) ||
                other.otherUserId == otherUserId) &&
            (identical(other.otherName, otherName) ||
                other.otherName == otherName) &&
            (identical(other.otherUsername, otherUsername) ||
                other.otherUsername == otherUsername) &&
            (identical(other.otherAvatarKey, otherAvatarKey) ||
                other.otherAvatarKey == otherAvatarKey) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.visibility, visibility) ||
                other.visibility == visibility) &&
            (identical(other.requestedAt, requestedAt) ||
                other.requestedAt == requestedAt) &&
            (identical(other.respondedAt, respondedAt) ||
                other.respondedAt == respondedAt) &&
            (identical(other.firstSharedEventId, firstSharedEventId) ||
                other.firstSharedEventId == firstSharedEventId) &&
            (identical(other.firstSharedEventTitle, firstSharedEventTitle) ||
                other.firstSharedEventTitle == firstSharedEventTitle) &&
            (identical(other.firstSharedEventSlug, firstSharedEventSlug) ||
                other.firstSharedEventSlug == firstSharedEventSlug));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    otherUserId,
    otherName,
    otherUsername,
    otherAvatarKey,
    status,
    visibility,
    requestedAt,
    respondedAt,
    firstSharedEventId,
    firstSharedEventTitle,
    firstSharedEventSlug,
  );

  /// Create a copy of AllyConnectionDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$AllyConnectionDtoImplCopyWith<_$AllyConnectionDtoImpl> get copyWith =>
      __$$AllyConnectionDtoImplCopyWithImpl<_$AllyConnectionDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$AllyConnectionDtoImplToJson(this);
  }
}

abstract class _AllyConnectionDto implements AllyConnectionDto {
  const factory _AllyConnectionDto({
    required final String id,
    @JsonKey(name: 'other_user_id') required final String otherUserId,
    @JsonKey(name: 'other_name') required final String otherName,
    @JsonKey(name: 'other_username') final String? otherUsername,
    @JsonKey(name: 'other_avatar_key') final String? otherAvatarKey,
    required final String status,
    required final String visibility,
    @JsonKey(name: 'requested_at') required final DateTime requestedAt,
    @JsonKey(name: 'responded_at') final DateTime? respondedAt,
    @JsonKey(name: 'first_shared_event_id') final String? firstSharedEventId,
    @JsonKey(name: 'first_shared_event_title')
    final String? firstSharedEventTitle,
    @JsonKey(name: 'first_shared_event_slug')
    final String? firstSharedEventSlug,
  }) = _$AllyConnectionDtoImpl;

  factory _AllyConnectionDto.fromJson(Map<String, dynamic> json) =
      _$AllyConnectionDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'other_user_id')
  String get otherUserId;
  @override
  @JsonKey(name: 'other_name')
  String get otherName;
  @override
  @JsonKey(name: 'other_username')
  String? get otherUsername;
  @override
  @JsonKey(name: 'other_avatar_key')
  String? get otherAvatarKey;
  @override
  String get status;
  @override
  String get visibility;
  @override
  @JsonKey(name: 'requested_at')
  DateTime get requestedAt;
  @override
  @JsonKey(name: 'responded_at')
  DateTime? get respondedAt;
  @override
  @JsonKey(name: 'first_shared_event_id')
  String? get firstSharedEventId;
  @override
  @JsonKey(name: 'first_shared_event_title')
  String? get firstSharedEventTitle;
  @override
  @JsonKey(name: 'first_shared_event_slug')
  String? get firstSharedEventSlug;

  /// Create a copy of AllyConnectionDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$AllyConnectionDtoImplCopyWith<_$AllyConnectionDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

SharedEventSummaryDto _$SharedEventSummaryDtoFromJson(
  Map<String, dynamic> json,
) {
  return _SharedEventSummaryDto.fromJson(json);
}

/// @nodoc
mixin _$SharedEventSummaryDto {
  String get id => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String get slug => throw _privateConstructorUsedError;
  @JsonKey(name: 'starts_at')
  DateTime get startsAt => throw _privateConstructorUsedError;

  /// Serializes this SharedEventSummaryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of SharedEventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $SharedEventSummaryDtoCopyWith<SharedEventSummaryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $SharedEventSummaryDtoCopyWith<$Res> {
  factory $SharedEventSummaryDtoCopyWith(
    SharedEventSummaryDto value,
    $Res Function(SharedEventSummaryDto) then,
  ) = _$SharedEventSummaryDtoCopyWithImpl<$Res, SharedEventSummaryDto>;
  @useResult
  $Res call({
    String id,
    String title,
    String slug,
    @JsonKey(name: 'starts_at') DateTime startsAt,
  });
}

/// @nodoc
class _$SharedEventSummaryDtoCopyWithImpl<
  $Res,
  $Val extends SharedEventSummaryDto
>
    implements $SharedEventSummaryDtoCopyWith<$Res> {
  _$SharedEventSummaryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of SharedEventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = null,
    Object? startsAt = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            title: null == title
                ? _value.title
                : title // ignore: cast_nullable_to_non_nullable
                      as String,
            slug: null == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String,
            startsAt: null == startsAt
                ? _value.startsAt
                : startsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$SharedEventSummaryDtoImplCopyWith<$Res>
    implements $SharedEventSummaryDtoCopyWith<$Res> {
  factory _$$SharedEventSummaryDtoImplCopyWith(
    _$SharedEventSummaryDtoImpl value,
    $Res Function(_$SharedEventSummaryDtoImpl) then,
  ) = __$$SharedEventSummaryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String title,
    String slug,
    @JsonKey(name: 'starts_at') DateTime startsAt,
  });
}

/// @nodoc
class __$$SharedEventSummaryDtoImplCopyWithImpl<$Res>
    extends
        _$SharedEventSummaryDtoCopyWithImpl<$Res, _$SharedEventSummaryDtoImpl>
    implements _$$SharedEventSummaryDtoImplCopyWith<$Res> {
  __$$SharedEventSummaryDtoImplCopyWithImpl(
    _$SharedEventSummaryDtoImpl _value,
    $Res Function(_$SharedEventSummaryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of SharedEventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = null,
    Object? startsAt = null,
  }) {
    return _then(
      _$SharedEventSummaryDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        title: null == title
            ? _value.title
            : title // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: null == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String,
        startsAt: null == startsAt
            ? _value.startsAt
            : startsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$SharedEventSummaryDtoImpl implements _SharedEventSummaryDto {
  const _$SharedEventSummaryDtoImpl({
    required this.id,
    required this.title,
    required this.slug,
    @JsonKey(name: 'starts_at') required this.startsAt,
  });

  factory _$SharedEventSummaryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$SharedEventSummaryDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String title;
  @override
  final String slug;
  @override
  @JsonKey(name: 'starts_at')
  final DateTime startsAt;

  @override
  String toString() {
    return 'SharedEventSummaryDto(id: $id, title: $title, slug: $slug, startsAt: $startsAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$SharedEventSummaryDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.startsAt, startsAt) ||
                other.startsAt == startsAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, id, title, slug, startsAt);

  /// Create a copy of SharedEventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$SharedEventSummaryDtoImplCopyWith<_$SharedEventSummaryDtoImpl>
  get copyWith =>
      __$$SharedEventSummaryDtoImplCopyWithImpl<_$SharedEventSummaryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$SharedEventSummaryDtoImplToJson(this);
  }
}

abstract class _SharedEventSummaryDto implements SharedEventSummaryDto {
  const factory _SharedEventSummaryDto({
    required final String id,
    required final String title,
    required final String slug,
    @JsonKey(name: 'starts_at') required final DateTime startsAt,
  }) = _$SharedEventSummaryDtoImpl;

  factory _SharedEventSummaryDto.fromJson(Map<String, dynamic> json) =
      _$SharedEventSummaryDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get title;
  @override
  String get slug;
  @override
  @JsonKey(name: 'starts_at')
  DateTime get startsAt;

  /// Create a copy of SharedEventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$SharedEventSummaryDtoImplCopyWith<_$SharedEventSummaryDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

SharedOrgSummaryDto _$SharedOrgSummaryDtoFromJson(Map<String, dynamic> json) {
  return _SharedOrgSummaryDto.fromJson(json);
}

/// @nodoc
mixin _$SharedOrgSummaryDto {
  String get id => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String get slug => throw _privateConstructorUsedError;

  /// Serializes this SharedOrgSummaryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of SharedOrgSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $SharedOrgSummaryDtoCopyWith<SharedOrgSummaryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $SharedOrgSummaryDtoCopyWith<$Res> {
  factory $SharedOrgSummaryDtoCopyWith(
    SharedOrgSummaryDto value,
    $Res Function(SharedOrgSummaryDto) then,
  ) = _$SharedOrgSummaryDtoCopyWithImpl<$Res, SharedOrgSummaryDto>;
  @useResult
  $Res call({String id, String name, String slug});
}

/// @nodoc
class _$SharedOrgSummaryDtoCopyWithImpl<$Res, $Val extends SharedOrgSummaryDto>
    implements $SharedOrgSummaryDtoCopyWith<$Res> {
  _$SharedOrgSummaryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of SharedOrgSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? id = null, Object? name = null, Object? slug = null}) {
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
            slug: null == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$SharedOrgSummaryDtoImplCopyWith<$Res>
    implements $SharedOrgSummaryDtoCopyWith<$Res> {
  factory _$$SharedOrgSummaryDtoImplCopyWith(
    _$SharedOrgSummaryDtoImpl value,
    $Res Function(_$SharedOrgSummaryDtoImpl) then,
  ) = __$$SharedOrgSummaryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({String id, String name, String slug});
}

/// @nodoc
class __$$SharedOrgSummaryDtoImplCopyWithImpl<$Res>
    extends _$SharedOrgSummaryDtoCopyWithImpl<$Res, _$SharedOrgSummaryDtoImpl>
    implements _$$SharedOrgSummaryDtoImplCopyWith<$Res> {
  __$$SharedOrgSummaryDtoImplCopyWithImpl(
    _$SharedOrgSummaryDtoImpl _value,
    $Res Function(_$SharedOrgSummaryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of SharedOrgSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? id = null, Object? name = null, Object? slug = null}) {
    return _then(
      _$SharedOrgSummaryDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: null == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$SharedOrgSummaryDtoImpl implements _SharedOrgSummaryDto {
  const _$SharedOrgSummaryDtoImpl({
    required this.id,
    required this.name,
    required this.slug,
  });

  factory _$SharedOrgSummaryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$SharedOrgSummaryDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String name;
  @override
  final String slug;

  @override
  String toString() {
    return 'SharedOrgSummaryDto(id: $id, name: $name, slug: $slug)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$SharedOrgSummaryDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.slug, slug) || other.slug == slug));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, id, name, slug);

  /// Create a copy of SharedOrgSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$SharedOrgSummaryDtoImplCopyWith<_$SharedOrgSummaryDtoImpl> get copyWith =>
      __$$SharedOrgSummaryDtoImplCopyWithImpl<_$SharedOrgSummaryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$SharedOrgSummaryDtoImplToJson(this);
  }
}

abstract class _SharedOrgSummaryDto implements SharedOrgSummaryDto {
  const factory _SharedOrgSummaryDto({
    required final String id,
    required final String name,
    required final String slug,
  }) = _$SharedOrgSummaryDtoImpl;

  factory _SharedOrgSummaryDto.fromJson(Map<String, dynamic> json) =
      _$SharedOrgSummaryDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get name;
  @override
  String get slug;

  /// Create a copy of SharedOrgSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$SharedOrgSummaryDtoImplCopyWith<_$SharedOrgSummaryDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

MutualDetailDto _$MutualDetailDtoFromJson(Map<String, dynamic> json) {
  return _MutualDetailDto.fromJson(json);
}

/// @nodoc
mixin _$MutualDetailDto {
  @JsonKey(name: 'shared_events')
  List<SharedEventSummaryDto> get sharedEvents =>
      throw _privateConstructorUsedError;
  @JsonKey(name: 'shared_orgs')
  List<SharedOrgSummaryDto> get sharedOrgs =>
      throw _privateConstructorUsedError;

  /// Why the two know each other (D-226). Defaults to empty so a pre-D-226 backend still parses.
  List<ProfileRelationshipDto> get relationships =>
      throw _privateConstructorUsedError;

  /// Serializes this MutualDetailDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of MutualDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $MutualDetailDtoCopyWith<MutualDetailDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $MutualDetailDtoCopyWith<$Res> {
  factory $MutualDetailDtoCopyWith(
    MutualDetailDto value,
    $Res Function(MutualDetailDto) then,
  ) = _$MutualDetailDtoCopyWithImpl<$Res, MutualDetailDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'shared_events') List<SharedEventSummaryDto> sharedEvents,
    @JsonKey(name: 'shared_orgs') List<SharedOrgSummaryDto> sharedOrgs,
    List<ProfileRelationshipDto> relationships,
  });
}

/// @nodoc
class _$MutualDetailDtoCopyWithImpl<$Res, $Val extends MutualDetailDto>
    implements $MutualDetailDtoCopyWith<$Res> {
  _$MutualDetailDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of MutualDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? sharedEvents = null,
    Object? sharedOrgs = null,
    Object? relationships = null,
  }) {
    return _then(
      _value.copyWith(
            sharedEvents: null == sharedEvents
                ? _value.sharedEvents
                : sharedEvents // ignore: cast_nullable_to_non_nullable
                      as List<SharedEventSummaryDto>,
            sharedOrgs: null == sharedOrgs
                ? _value.sharedOrgs
                : sharedOrgs // ignore: cast_nullable_to_non_nullable
                      as List<SharedOrgSummaryDto>,
            relationships: null == relationships
                ? _value.relationships
                : relationships // ignore: cast_nullable_to_non_nullable
                      as List<ProfileRelationshipDto>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$MutualDetailDtoImplCopyWith<$Res>
    implements $MutualDetailDtoCopyWith<$Res> {
  factory _$$MutualDetailDtoImplCopyWith(
    _$MutualDetailDtoImpl value,
    $Res Function(_$MutualDetailDtoImpl) then,
  ) = __$$MutualDetailDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'shared_events') List<SharedEventSummaryDto> sharedEvents,
    @JsonKey(name: 'shared_orgs') List<SharedOrgSummaryDto> sharedOrgs,
    List<ProfileRelationshipDto> relationships,
  });
}

/// @nodoc
class __$$MutualDetailDtoImplCopyWithImpl<$Res>
    extends _$MutualDetailDtoCopyWithImpl<$Res, _$MutualDetailDtoImpl>
    implements _$$MutualDetailDtoImplCopyWith<$Res> {
  __$$MutualDetailDtoImplCopyWithImpl(
    _$MutualDetailDtoImpl _value,
    $Res Function(_$MutualDetailDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of MutualDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? sharedEvents = null,
    Object? sharedOrgs = null,
    Object? relationships = null,
  }) {
    return _then(
      _$MutualDetailDtoImpl(
        sharedEvents: null == sharedEvents
            ? _value._sharedEvents
            : sharedEvents // ignore: cast_nullable_to_non_nullable
                  as List<SharedEventSummaryDto>,
        sharedOrgs: null == sharedOrgs
            ? _value._sharedOrgs
            : sharedOrgs // ignore: cast_nullable_to_non_nullable
                  as List<SharedOrgSummaryDto>,
        relationships: null == relationships
            ? _value._relationships
            : relationships // ignore: cast_nullable_to_non_nullable
                  as List<ProfileRelationshipDto>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$MutualDetailDtoImpl implements _MutualDetailDto {
  const _$MutualDetailDtoImpl({
    @JsonKey(name: 'shared_events')
    final List<SharedEventSummaryDto> sharedEvents = const [],
    @JsonKey(name: 'shared_orgs')
    final List<SharedOrgSummaryDto> sharedOrgs = const [],
    final List<ProfileRelationshipDto> relationships = const [],
  }) : _sharedEvents = sharedEvents,
       _sharedOrgs = sharedOrgs,
       _relationships = relationships;

  factory _$MutualDetailDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$MutualDetailDtoImplFromJson(json);

  final List<SharedEventSummaryDto> _sharedEvents;
  @override
  @JsonKey(name: 'shared_events')
  List<SharedEventSummaryDto> get sharedEvents {
    if (_sharedEvents is EqualUnmodifiableListView) return _sharedEvents;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_sharedEvents);
  }

  final List<SharedOrgSummaryDto> _sharedOrgs;
  @override
  @JsonKey(name: 'shared_orgs')
  List<SharedOrgSummaryDto> get sharedOrgs {
    if (_sharedOrgs is EqualUnmodifiableListView) return _sharedOrgs;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_sharedOrgs);
  }

  /// Why the two know each other (D-226). Defaults to empty so a pre-D-226 backend still parses.
  final List<ProfileRelationshipDto> _relationships;

  /// Why the two know each other (D-226). Defaults to empty so a pre-D-226 backend still parses.
  @override
  @JsonKey()
  List<ProfileRelationshipDto> get relationships {
    if (_relationships is EqualUnmodifiableListView) return _relationships;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_relationships);
  }

  @override
  String toString() {
    return 'MutualDetailDto(sharedEvents: $sharedEvents, sharedOrgs: $sharedOrgs, relationships: $relationships)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$MutualDetailDtoImpl &&
            const DeepCollectionEquality().equals(
              other._sharedEvents,
              _sharedEvents,
            ) &&
            const DeepCollectionEquality().equals(
              other._sharedOrgs,
              _sharedOrgs,
            ) &&
            const DeepCollectionEquality().equals(
              other._relationships,
              _relationships,
            ));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    const DeepCollectionEquality().hash(_sharedEvents),
    const DeepCollectionEquality().hash(_sharedOrgs),
    const DeepCollectionEquality().hash(_relationships),
  );

  /// Create a copy of MutualDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$MutualDetailDtoImplCopyWith<_$MutualDetailDtoImpl> get copyWith =>
      __$$MutualDetailDtoImplCopyWithImpl<_$MutualDetailDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$MutualDetailDtoImplToJson(this);
  }
}

abstract class _MutualDetailDto implements MutualDetailDto {
  const factory _MutualDetailDto({
    @JsonKey(name: 'shared_events')
    final List<SharedEventSummaryDto> sharedEvents,
    @JsonKey(name: 'shared_orgs') final List<SharedOrgSummaryDto> sharedOrgs,
    final List<ProfileRelationshipDto> relationships,
  }) = _$MutualDetailDtoImpl;

  factory _MutualDetailDto.fromJson(Map<String, dynamic> json) =
      _$MutualDetailDtoImpl.fromJson;

  @override
  @JsonKey(name: 'shared_events')
  List<SharedEventSummaryDto> get sharedEvents;
  @override
  @JsonKey(name: 'shared_orgs')
  List<SharedOrgSummaryDto> get sharedOrgs;

  /// Why the two know each other (D-226). Defaults to empty so a pre-D-226 backend still parses.
  @override
  List<ProfileRelationshipDto> get relationships;

  /// Create a copy of MutualDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$MutualDetailDtoImplCopyWith<_$MutualDetailDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

AllySuggestionDto _$AllySuggestionDtoFromJson(Map<String, dynamic> json) {
  return _AllySuggestionDto.fromJson(json);
}

/// @nodoc
mixin _$AllySuggestionDto {
  @JsonKey(name: 'user_id')
  String get userId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get username => throw _privateConstructorUsedError;
  @JsonKey(name: 'avatar_key')
  String? get avatarKey => throw _privateConstructorUsedError;
  @JsonKey(name: 'shared_event_count')
  int get sharedEventCount => throw _privateConstructorUsedError;
  @JsonKey(name: 'shared_org_count')
  int get sharedOrgCount => throw _privateConstructorUsedError;
  String get reason => throw _privateConstructorUsedError;

  /// Serializes this AllySuggestionDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of AllySuggestionDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $AllySuggestionDtoCopyWith<AllySuggestionDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $AllySuggestionDtoCopyWith<$Res> {
  factory $AllySuggestionDtoCopyWith(
    AllySuggestionDto value,
    $Res Function(AllySuggestionDto) then,
  ) = _$AllySuggestionDtoCopyWithImpl<$Res, AllySuggestionDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'user_id') String userId,
    String name,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'shared_event_count') int sharedEventCount,
    @JsonKey(name: 'shared_org_count') int sharedOrgCount,
    String reason,
  });
}

/// @nodoc
class _$AllySuggestionDtoCopyWithImpl<$Res, $Val extends AllySuggestionDto>
    implements $AllySuggestionDtoCopyWith<$Res> {
  _$AllySuggestionDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of AllySuggestionDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? userId = null,
    Object? name = null,
    Object? username = freezed,
    Object? avatarKey = freezed,
    Object? sharedEventCount = null,
    Object? sharedOrgCount = null,
    Object? reason = null,
  }) {
    return _then(
      _value.copyWith(
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
            avatarKey: freezed == avatarKey
                ? _value.avatarKey
                : avatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            sharedEventCount: null == sharedEventCount
                ? _value.sharedEventCount
                : sharedEventCount // ignore: cast_nullable_to_non_nullable
                      as int,
            sharedOrgCount: null == sharedOrgCount
                ? _value.sharedOrgCount
                : sharedOrgCount // ignore: cast_nullable_to_non_nullable
                      as int,
            reason: null == reason
                ? _value.reason
                : reason // ignore: cast_nullable_to_non_nullable
                      as String,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$AllySuggestionDtoImplCopyWith<$Res>
    implements $AllySuggestionDtoCopyWith<$Res> {
  factory _$$AllySuggestionDtoImplCopyWith(
    _$AllySuggestionDtoImpl value,
    $Res Function(_$AllySuggestionDtoImpl) then,
  ) = __$$AllySuggestionDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'user_id') String userId,
    String name,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'shared_event_count') int sharedEventCount,
    @JsonKey(name: 'shared_org_count') int sharedOrgCount,
    String reason,
  });
}

/// @nodoc
class __$$AllySuggestionDtoImplCopyWithImpl<$Res>
    extends _$AllySuggestionDtoCopyWithImpl<$Res, _$AllySuggestionDtoImpl>
    implements _$$AllySuggestionDtoImplCopyWith<$Res> {
  __$$AllySuggestionDtoImplCopyWithImpl(
    _$AllySuggestionDtoImpl _value,
    $Res Function(_$AllySuggestionDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of AllySuggestionDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? userId = null,
    Object? name = null,
    Object? username = freezed,
    Object? avatarKey = freezed,
    Object? sharedEventCount = null,
    Object? sharedOrgCount = null,
    Object? reason = null,
  }) {
    return _then(
      _$AllySuggestionDtoImpl(
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
        avatarKey: freezed == avatarKey
            ? _value.avatarKey
            : avatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        sharedEventCount: null == sharedEventCount
            ? _value.sharedEventCount
            : sharedEventCount // ignore: cast_nullable_to_non_nullable
                  as int,
        sharedOrgCount: null == sharedOrgCount
            ? _value.sharedOrgCount
            : sharedOrgCount // ignore: cast_nullable_to_non_nullable
                  as int,
        reason: null == reason
            ? _value.reason
            : reason // ignore: cast_nullable_to_non_nullable
                  as String,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$AllySuggestionDtoImpl implements _AllySuggestionDto {
  const _$AllySuggestionDtoImpl({
    @JsonKey(name: 'user_id') required this.userId,
    required this.name,
    this.username,
    @JsonKey(name: 'avatar_key') this.avatarKey,
    @JsonKey(name: 'shared_event_count') required this.sharedEventCount,
    @JsonKey(name: 'shared_org_count') required this.sharedOrgCount,
    required this.reason,
  });

  factory _$AllySuggestionDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$AllySuggestionDtoImplFromJson(json);

  @override
  @JsonKey(name: 'user_id')
  final String userId;
  @override
  final String name;
  @override
  final String? username;
  @override
  @JsonKey(name: 'avatar_key')
  final String? avatarKey;
  @override
  @JsonKey(name: 'shared_event_count')
  final int sharedEventCount;
  @override
  @JsonKey(name: 'shared_org_count')
  final int sharedOrgCount;
  @override
  final String reason;

  @override
  String toString() {
    return 'AllySuggestionDto(userId: $userId, name: $name, username: $username, avatarKey: $avatarKey, sharedEventCount: $sharedEventCount, sharedOrgCount: $sharedOrgCount, reason: $reason)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$AllySuggestionDtoImpl &&
            (identical(other.userId, userId) || other.userId == userId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.avatarKey, avatarKey) ||
                other.avatarKey == avatarKey) &&
            (identical(other.sharedEventCount, sharedEventCount) ||
                other.sharedEventCount == sharedEventCount) &&
            (identical(other.sharedOrgCount, sharedOrgCount) ||
                other.sharedOrgCount == sharedOrgCount) &&
            (identical(other.reason, reason) || other.reason == reason));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    userId,
    name,
    username,
    avatarKey,
    sharedEventCount,
    sharedOrgCount,
    reason,
  );

  /// Create a copy of AllySuggestionDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$AllySuggestionDtoImplCopyWith<_$AllySuggestionDtoImpl> get copyWith =>
      __$$AllySuggestionDtoImplCopyWithImpl<_$AllySuggestionDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$AllySuggestionDtoImplToJson(this);
  }
}

abstract class _AllySuggestionDto implements AllySuggestionDto {
  const factory _AllySuggestionDto({
    @JsonKey(name: 'user_id') required final String userId,
    required final String name,
    final String? username,
    @JsonKey(name: 'avatar_key') final String? avatarKey,
    @JsonKey(name: 'shared_event_count') required final int sharedEventCount,
    @JsonKey(name: 'shared_org_count') required final int sharedOrgCount,
    required final String reason,
  }) = _$AllySuggestionDtoImpl;

  factory _AllySuggestionDto.fromJson(Map<String, dynamic> json) =
      _$AllySuggestionDtoImpl.fromJson;

  @override
  @JsonKey(name: 'user_id')
  String get userId;
  @override
  String get name;
  @override
  String? get username;
  @override
  @JsonKey(name: 'avatar_key')
  String? get avatarKey;
  @override
  @JsonKey(name: 'shared_event_count')
  int get sharedEventCount;
  @override
  @JsonKey(name: 'shared_org_count')
  int get sharedOrgCount;
  @override
  String get reason;

  /// Create a copy of AllySuggestionDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$AllySuggestionDtoImplCopyWith<_$AllySuggestionDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

JourneyEvidenceDto _$JourneyEvidenceDtoFromJson(Map<String, dynamic> json) {
  return _JourneyEvidenceDto.fromJson(json);
}

/// @nodoc
mixin _$JourneyEvidenceDto {
  String get kind => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_title')
  String? get eventTitle => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_slug')
  String? get eventSlug => throw _privateConstructorUsedError;
  String? get detail => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_name')
  String? get orgName => throw _privateConstructorUsedError;

  /// Serializes this JourneyEvidenceDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of JourneyEvidenceDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $JourneyEvidenceDtoCopyWith<JourneyEvidenceDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $JourneyEvidenceDtoCopyWith<$Res> {
  factory $JourneyEvidenceDtoCopyWith(
    JourneyEvidenceDto value,
    $Res Function(JourneyEvidenceDto) then,
  ) = _$JourneyEvidenceDtoCopyWithImpl<$Res, JourneyEvidenceDto>;
  @useResult
  $Res call({
    String kind,
    @JsonKey(name: 'event_title') String? eventTitle,
    @JsonKey(name: 'event_slug') String? eventSlug,
    String? detail,
    @JsonKey(name: 'org_name') String? orgName,
  });
}

/// @nodoc
class _$JourneyEvidenceDtoCopyWithImpl<$Res, $Val extends JourneyEvidenceDto>
    implements $JourneyEvidenceDtoCopyWith<$Res> {
  _$JourneyEvidenceDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of JourneyEvidenceDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? kind = null,
    Object? eventTitle = freezed,
    Object? eventSlug = freezed,
    Object? detail = freezed,
    Object? orgName = freezed,
  }) {
    return _then(
      _value.copyWith(
            kind: null == kind
                ? _value.kind
                : kind // ignore: cast_nullable_to_non_nullable
                      as String,
            eventTitle: freezed == eventTitle
                ? _value.eventTitle
                : eventTitle // ignore: cast_nullable_to_non_nullable
                      as String?,
            eventSlug: freezed == eventSlug
                ? _value.eventSlug
                : eventSlug // ignore: cast_nullable_to_non_nullable
                      as String?,
            detail: freezed == detail
                ? _value.detail
                : detail // ignore: cast_nullable_to_non_nullable
                      as String?,
            orgName: freezed == orgName
                ? _value.orgName
                : orgName // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$JourneyEvidenceDtoImplCopyWith<$Res>
    implements $JourneyEvidenceDtoCopyWith<$Res> {
  factory _$$JourneyEvidenceDtoImplCopyWith(
    _$JourneyEvidenceDtoImpl value,
    $Res Function(_$JourneyEvidenceDtoImpl) then,
  ) = __$$JourneyEvidenceDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String kind,
    @JsonKey(name: 'event_title') String? eventTitle,
    @JsonKey(name: 'event_slug') String? eventSlug,
    String? detail,
    @JsonKey(name: 'org_name') String? orgName,
  });
}

/// @nodoc
class __$$JourneyEvidenceDtoImplCopyWithImpl<$Res>
    extends _$JourneyEvidenceDtoCopyWithImpl<$Res, _$JourneyEvidenceDtoImpl>
    implements _$$JourneyEvidenceDtoImplCopyWith<$Res> {
  __$$JourneyEvidenceDtoImplCopyWithImpl(
    _$JourneyEvidenceDtoImpl _value,
    $Res Function(_$JourneyEvidenceDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of JourneyEvidenceDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? kind = null,
    Object? eventTitle = freezed,
    Object? eventSlug = freezed,
    Object? detail = freezed,
    Object? orgName = freezed,
  }) {
    return _then(
      _$JourneyEvidenceDtoImpl(
        kind: null == kind
            ? _value.kind
            : kind // ignore: cast_nullable_to_non_nullable
                  as String,
        eventTitle: freezed == eventTitle
            ? _value.eventTitle
            : eventTitle // ignore: cast_nullable_to_non_nullable
                  as String?,
        eventSlug: freezed == eventSlug
            ? _value.eventSlug
            : eventSlug // ignore: cast_nullable_to_non_nullable
                  as String?,
        detail: freezed == detail
            ? _value.detail
            : detail // ignore: cast_nullable_to_non_nullable
                  as String?,
        orgName: freezed == orgName
            ? _value.orgName
            : orgName // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$JourneyEvidenceDtoImpl implements _JourneyEvidenceDto {
  const _$JourneyEvidenceDtoImpl({
    required this.kind,
    @JsonKey(name: 'event_title') this.eventTitle,
    @JsonKey(name: 'event_slug') this.eventSlug,
    this.detail,
    @JsonKey(name: 'org_name') this.orgName,
  });

  factory _$JourneyEvidenceDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$JourneyEvidenceDtoImplFromJson(json);

  @override
  final String kind;
  @override
  @JsonKey(name: 'event_title')
  final String? eventTitle;
  @override
  @JsonKey(name: 'event_slug')
  final String? eventSlug;
  @override
  final String? detail;
  @override
  @JsonKey(name: 'org_name')
  final String? orgName;

  @override
  String toString() {
    return 'JourneyEvidenceDto(kind: $kind, eventTitle: $eventTitle, eventSlug: $eventSlug, detail: $detail, orgName: $orgName)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$JourneyEvidenceDtoImpl &&
            (identical(other.kind, kind) || other.kind == kind) &&
            (identical(other.eventTitle, eventTitle) ||
                other.eventTitle == eventTitle) &&
            (identical(other.eventSlug, eventSlug) ||
                other.eventSlug == eventSlug) &&
            (identical(other.detail, detail) || other.detail == detail) &&
            (identical(other.orgName, orgName) || other.orgName == orgName));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, kind, eventTitle, eventSlug, detail, orgName);

  /// Create a copy of JourneyEvidenceDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$JourneyEvidenceDtoImplCopyWith<_$JourneyEvidenceDtoImpl> get copyWith =>
      __$$JourneyEvidenceDtoImplCopyWithImpl<_$JourneyEvidenceDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$JourneyEvidenceDtoImplToJson(this);
  }
}

abstract class _JourneyEvidenceDto implements JourneyEvidenceDto {
  const factory _JourneyEvidenceDto({
    required final String kind,
    @JsonKey(name: 'event_title') final String? eventTitle,
    @JsonKey(name: 'event_slug') final String? eventSlug,
    final String? detail,
    @JsonKey(name: 'org_name') final String? orgName,
  }) = _$JourneyEvidenceDtoImpl;

  factory _JourneyEvidenceDto.fromJson(Map<String, dynamic> json) =
      _$JourneyEvidenceDtoImpl.fromJson;

  @override
  String get kind;
  @override
  @JsonKey(name: 'event_title')
  String? get eventTitle;
  @override
  @JsonKey(name: 'event_slug')
  String? get eventSlug;
  @override
  String? get detail;
  @override
  @JsonKey(name: 'org_name')
  String? get orgName;

  /// Create a copy of JourneyEvidenceDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$JourneyEvidenceDtoImplCopyWith<_$JourneyEvidenceDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

JourneyNodeDto _$JourneyNodeDtoFromJson(Map<String, dynamic> json) {
  return _JourneyNodeDto.fromJson(json);
}

/// @nodoc
mixin _$JourneyNodeDto {
  String get tier => throw _privateConstructorUsedError;
  @JsonKey(name: 'first_attained_at')
  DateTime get firstAttainedAt => throw _privateConstructorUsedError;
  int get occurrences => throw _privateConstructorUsedError;
  String get source => throw _privateConstructorUsedError;
  JourneyEvidenceDto get evidence => throw _privateConstructorUsedError;

  /// Serializes this JourneyNodeDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of JourneyNodeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $JourneyNodeDtoCopyWith<JourneyNodeDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $JourneyNodeDtoCopyWith<$Res> {
  factory $JourneyNodeDtoCopyWith(
    JourneyNodeDto value,
    $Res Function(JourneyNodeDto) then,
  ) = _$JourneyNodeDtoCopyWithImpl<$Res, JourneyNodeDto>;
  @useResult
  $Res call({
    String tier,
    @JsonKey(name: 'first_attained_at') DateTime firstAttainedAt,
    int occurrences,
    String source,
    JourneyEvidenceDto evidence,
  });

  $JourneyEvidenceDtoCopyWith<$Res> get evidence;
}

/// @nodoc
class _$JourneyNodeDtoCopyWithImpl<$Res, $Val extends JourneyNodeDto>
    implements $JourneyNodeDtoCopyWith<$Res> {
  _$JourneyNodeDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of JourneyNodeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? tier = null,
    Object? firstAttainedAt = null,
    Object? occurrences = null,
    Object? source = null,
    Object? evidence = null,
  }) {
    return _then(
      _value.copyWith(
            tier: null == tier
                ? _value.tier
                : tier // ignore: cast_nullable_to_non_nullable
                      as String,
            firstAttainedAt: null == firstAttainedAt
                ? _value.firstAttainedAt
                : firstAttainedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            occurrences: null == occurrences
                ? _value.occurrences
                : occurrences // ignore: cast_nullable_to_non_nullable
                      as int,
            source: null == source
                ? _value.source
                : source // ignore: cast_nullable_to_non_nullable
                      as String,
            evidence: null == evidence
                ? _value.evidence
                : evidence // ignore: cast_nullable_to_non_nullable
                      as JourneyEvidenceDto,
          )
          as $Val,
    );
  }

  /// Create a copy of JourneyNodeDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $JourneyEvidenceDtoCopyWith<$Res> get evidence {
    return $JourneyEvidenceDtoCopyWith<$Res>(_value.evidence, (value) {
      return _then(_value.copyWith(evidence: value) as $Val);
    });
  }
}

/// @nodoc
abstract class _$$JourneyNodeDtoImplCopyWith<$Res>
    implements $JourneyNodeDtoCopyWith<$Res> {
  factory _$$JourneyNodeDtoImplCopyWith(
    _$JourneyNodeDtoImpl value,
    $Res Function(_$JourneyNodeDtoImpl) then,
  ) = __$$JourneyNodeDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String tier,
    @JsonKey(name: 'first_attained_at') DateTime firstAttainedAt,
    int occurrences,
    String source,
    JourneyEvidenceDto evidence,
  });

  @override
  $JourneyEvidenceDtoCopyWith<$Res> get evidence;
}

/// @nodoc
class __$$JourneyNodeDtoImplCopyWithImpl<$Res>
    extends _$JourneyNodeDtoCopyWithImpl<$Res, _$JourneyNodeDtoImpl>
    implements _$$JourneyNodeDtoImplCopyWith<$Res> {
  __$$JourneyNodeDtoImplCopyWithImpl(
    _$JourneyNodeDtoImpl _value,
    $Res Function(_$JourneyNodeDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of JourneyNodeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? tier = null,
    Object? firstAttainedAt = null,
    Object? occurrences = null,
    Object? source = null,
    Object? evidence = null,
  }) {
    return _then(
      _$JourneyNodeDtoImpl(
        tier: null == tier
            ? _value.tier
            : tier // ignore: cast_nullable_to_non_nullable
                  as String,
        firstAttainedAt: null == firstAttainedAt
            ? _value.firstAttainedAt
            : firstAttainedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        occurrences: null == occurrences
            ? _value.occurrences
            : occurrences // ignore: cast_nullable_to_non_nullable
                  as int,
        source: null == source
            ? _value.source
            : source // ignore: cast_nullable_to_non_nullable
                  as String,
        evidence: null == evidence
            ? _value.evidence
            : evidence // ignore: cast_nullable_to_non_nullable
                  as JourneyEvidenceDto,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$JourneyNodeDtoImpl implements _JourneyNodeDto {
  const _$JourneyNodeDtoImpl({
    required this.tier,
    @JsonKey(name: 'first_attained_at') required this.firstAttainedAt,
    this.occurrences = 1,
    this.source = 'verified',
    required this.evidence,
  });

  factory _$JourneyNodeDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$JourneyNodeDtoImplFromJson(json);

  @override
  final String tier;
  @override
  @JsonKey(name: 'first_attained_at')
  final DateTime firstAttainedAt;
  @override
  @JsonKey()
  final int occurrences;
  @override
  @JsonKey()
  final String source;
  @override
  final JourneyEvidenceDto evidence;

  @override
  String toString() {
    return 'JourneyNodeDto(tier: $tier, firstAttainedAt: $firstAttainedAt, occurrences: $occurrences, source: $source, evidence: $evidence)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$JourneyNodeDtoImpl &&
            (identical(other.tier, tier) || other.tier == tier) &&
            (identical(other.firstAttainedAt, firstAttainedAt) ||
                other.firstAttainedAt == firstAttainedAt) &&
            (identical(other.occurrences, occurrences) ||
                other.occurrences == occurrences) &&
            (identical(other.source, source) || other.source == source) &&
            (identical(other.evidence, evidence) ||
                other.evidence == evidence));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    tier,
    firstAttainedAt,
    occurrences,
    source,
    evidence,
  );

  /// Create a copy of JourneyNodeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$JourneyNodeDtoImplCopyWith<_$JourneyNodeDtoImpl> get copyWith =>
      __$$JourneyNodeDtoImplCopyWithImpl<_$JourneyNodeDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$JourneyNodeDtoImplToJson(this);
  }
}

abstract class _JourneyNodeDto implements JourneyNodeDto {
  const factory _JourneyNodeDto({
    required final String tier,
    @JsonKey(name: 'first_attained_at') required final DateTime firstAttainedAt,
    final int occurrences,
    final String source,
    required final JourneyEvidenceDto evidence,
  }) = _$JourneyNodeDtoImpl;

  factory _JourneyNodeDto.fromJson(Map<String, dynamic> json) =
      _$JourneyNodeDtoImpl.fromJson;

  @override
  String get tier;
  @override
  @JsonKey(name: 'first_attained_at')
  DateTime get firstAttainedAt;
  @override
  int get occurrences;
  @override
  String get source;
  @override
  JourneyEvidenceDto get evidence;

  /// Create a copy of JourneyNodeDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$JourneyNodeDtoImplCopyWith<_$JourneyNodeDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

ProfileRelationshipDto _$ProfileRelationshipDtoFromJson(
  Map<String, dynamic> json,
) {
  return _ProfileRelationshipDto.fromJson(json);
}

/// @nodoc
mixin _$ProfileRelationshipDto {
  String get type => throw _privateConstructorUsedError;
  String get label => throw _privateConstructorUsedError;
  int get count => throw _privateConstructorUsedError;
  String? get context => throw _privateConstructorUsedError;

  /// Serializes this ProfileRelationshipDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ProfileRelationshipDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ProfileRelationshipDtoCopyWith<ProfileRelationshipDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ProfileRelationshipDtoCopyWith<$Res> {
  factory $ProfileRelationshipDtoCopyWith(
    ProfileRelationshipDto value,
    $Res Function(ProfileRelationshipDto) then,
  ) = _$ProfileRelationshipDtoCopyWithImpl<$Res, ProfileRelationshipDto>;
  @useResult
  $Res call({String type, String label, int count, String? context});
}

/// @nodoc
class _$ProfileRelationshipDtoCopyWithImpl<
  $Res,
  $Val extends ProfileRelationshipDto
>
    implements $ProfileRelationshipDtoCopyWith<$Res> {
  _$ProfileRelationshipDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ProfileRelationshipDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? type = null,
    Object? label = null,
    Object? count = null,
    Object? context = freezed,
  }) {
    return _then(
      _value.copyWith(
            type: null == type
                ? _value.type
                : type // ignore: cast_nullable_to_non_nullable
                      as String,
            label: null == label
                ? _value.label
                : label // ignore: cast_nullable_to_non_nullable
                      as String,
            count: null == count
                ? _value.count
                : count // ignore: cast_nullable_to_non_nullable
                      as int,
            context: freezed == context
                ? _value.context
                : context // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ProfileRelationshipDtoImplCopyWith<$Res>
    implements $ProfileRelationshipDtoCopyWith<$Res> {
  factory _$$ProfileRelationshipDtoImplCopyWith(
    _$ProfileRelationshipDtoImpl value,
    $Res Function(_$ProfileRelationshipDtoImpl) then,
  ) = __$$ProfileRelationshipDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({String type, String label, int count, String? context});
}

/// @nodoc
class __$$ProfileRelationshipDtoImplCopyWithImpl<$Res>
    extends
        _$ProfileRelationshipDtoCopyWithImpl<$Res, _$ProfileRelationshipDtoImpl>
    implements _$$ProfileRelationshipDtoImplCopyWith<$Res> {
  __$$ProfileRelationshipDtoImplCopyWithImpl(
    _$ProfileRelationshipDtoImpl _value,
    $Res Function(_$ProfileRelationshipDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ProfileRelationshipDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? type = null,
    Object? label = null,
    Object? count = null,
    Object? context = freezed,
  }) {
    return _then(
      _$ProfileRelationshipDtoImpl(
        type: null == type
            ? _value.type
            : type // ignore: cast_nullable_to_non_nullable
                  as String,
        label: null == label
            ? _value.label
            : label // ignore: cast_nullable_to_non_nullable
                  as String,
        count: null == count
            ? _value.count
            : count // ignore: cast_nullable_to_non_nullable
                  as int,
        context: freezed == context
            ? _value.context
            : context // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ProfileRelationshipDtoImpl implements _ProfileRelationshipDto {
  const _$ProfileRelationshipDtoImpl({
    required this.type,
    required this.label,
    this.count = 1,
    this.context,
  });

  factory _$ProfileRelationshipDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ProfileRelationshipDtoImplFromJson(json);

  @override
  final String type;
  @override
  final String label;
  @override
  @JsonKey()
  final int count;
  @override
  final String? context;

  @override
  String toString() {
    return 'ProfileRelationshipDto(type: $type, label: $label, count: $count, context: $context)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ProfileRelationshipDtoImpl &&
            (identical(other.type, type) || other.type == type) &&
            (identical(other.label, label) || other.label == label) &&
            (identical(other.count, count) || other.count == count) &&
            (identical(other.context, context) || other.context == context));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, type, label, count, context);

  /// Create a copy of ProfileRelationshipDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ProfileRelationshipDtoImplCopyWith<_$ProfileRelationshipDtoImpl>
  get copyWith =>
      __$$ProfileRelationshipDtoImplCopyWithImpl<_$ProfileRelationshipDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$ProfileRelationshipDtoImplToJson(this);
  }
}

abstract class _ProfileRelationshipDto implements ProfileRelationshipDto {
  const factory _ProfileRelationshipDto({
    required final String type,
    required final String label,
    final int count,
    final String? context,
  }) = _$ProfileRelationshipDtoImpl;

  factory _ProfileRelationshipDto.fromJson(Map<String, dynamic> json) =
      _$ProfileRelationshipDtoImpl.fromJson;

  @override
  String get type;
  @override
  String get label;
  @override
  int get count;
  @override
  String? get context;

  /// Create a copy of ProfileRelationshipDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ProfileRelationshipDtoImplCopyWith<_$ProfileRelationshipDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

ProfileMetricsDto _$ProfileMetricsDtoFromJson(Map<String, dynamic> json) {
  return _ProfileMetricsDto.fromJson(json);
}

/// @nodoc
mixin _$ProfileMetricsDto {
  // Every count nullable: null means hidden from this viewer, never zero (D-229/H2). A
  // `@Default(0)` here would silently convert a privacy choice into a claim about the person.
  @JsonKey(name: 'events_organized')
  int? get eventsOrganized => throw _privateConstructorUsedError;
  @JsonKey(name: 'events_participated')
  int? get eventsParticipated => throw _privateConstructorUsedError;
  @JsonKey(name: 'events_attended')
  int? get eventsAttended => throw _privateConstructorUsedError;
  @JsonKey(name: 'completion_rate')
  double? get completionRate => throw _privateConstructorUsedError;
  @JsonKey(name: 'assignments_accepted')
  int? get assignmentsAccepted => throw _privateConstructorUsedError;
  @JsonKey(name: 'assignments_completed')
  int? get assignmentsCompleted => throw _privateConstructorUsedError;
  @JsonKey(name: 'competitions_entered')
  int? get competitionsEntered => throw _privateConstructorUsedError;
  @JsonKey(name: 'competitions_won')
  int? get competitionsWon => throw _privateConstructorUsedError;
  @JsonKey(name: 'speaker_sessions')
  int? get speakerSessions => throw _privateConstructorUsedError;
  int? get certificates => throw _privateConstructorUsedError;
  @JsonKey(name: 'achievement_certificates')
  int? get achievementCertificates => throw _privateConstructorUsedError;
  int? get organizations => throw _privateConstructorUsedError;
  @JsonKey(name: 'verified_organizations')
  int? get verifiedOrganizations => throw _privateConstructorUsedError;
  @JsonKey(name: 'ally_count')
  int? get allyCount => throw _privateConstructorUsedError;
  List<String> get cities => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_dna')
  List<EventDnaTagDto> get eventDna => throw _privateConstructorUsedError;

  /// Serializes this ProfileMetricsDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ProfileMetricsDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ProfileMetricsDtoCopyWith<ProfileMetricsDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ProfileMetricsDtoCopyWith<$Res> {
  factory $ProfileMetricsDtoCopyWith(
    ProfileMetricsDto value,
    $Res Function(ProfileMetricsDto) then,
  ) = _$ProfileMetricsDtoCopyWithImpl<$Res, ProfileMetricsDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'events_organized') int? eventsOrganized,
    @JsonKey(name: 'events_participated') int? eventsParticipated,
    @JsonKey(name: 'events_attended') int? eventsAttended,
    @JsonKey(name: 'completion_rate') double? completionRate,
    @JsonKey(name: 'assignments_accepted') int? assignmentsAccepted,
    @JsonKey(name: 'assignments_completed') int? assignmentsCompleted,
    @JsonKey(name: 'competitions_entered') int? competitionsEntered,
    @JsonKey(name: 'competitions_won') int? competitionsWon,
    @JsonKey(name: 'speaker_sessions') int? speakerSessions,
    int? certificates,
    @JsonKey(name: 'achievement_certificates') int? achievementCertificates,
    int? organizations,
    @JsonKey(name: 'verified_organizations') int? verifiedOrganizations,
    @JsonKey(name: 'ally_count') int? allyCount,
    List<String> cities,
    @JsonKey(name: 'event_dna') List<EventDnaTagDto> eventDna,
  });
}

/// @nodoc
class _$ProfileMetricsDtoCopyWithImpl<$Res, $Val extends ProfileMetricsDto>
    implements $ProfileMetricsDtoCopyWith<$Res> {
  _$ProfileMetricsDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ProfileMetricsDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? eventsOrganized = freezed,
    Object? eventsParticipated = freezed,
    Object? eventsAttended = freezed,
    Object? completionRate = freezed,
    Object? assignmentsAccepted = freezed,
    Object? assignmentsCompleted = freezed,
    Object? competitionsEntered = freezed,
    Object? competitionsWon = freezed,
    Object? speakerSessions = freezed,
    Object? certificates = freezed,
    Object? achievementCertificates = freezed,
    Object? organizations = freezed,
    Object? verifiedOrganizations = freezed,
    Object? allyCount = freezed,
    Object? cities = null,
    Object? eventDna = null,
  }) {
    return _then(
      _value.copyWith(
            eventsOrganized: freezed == eventsOrganized
                ? _value.eventsOrganized
                : eventsOrganized // ignore: cast_nullable_to_non_nullable
                      as int?,
            eventsParticipated: freezed == eventsParticipated
                ? _value.eventsParticipated
                : eventsParticipated // ignore: cast_nullable_to_non_nullable
                      as int?,
            eventsAttended: freezed == eventsAttended
                ? _value.eventsAttended
                : eventsAttended // ignore: cast_nullable_to_non_nullable
                      as int?,
            completionRate: freezed == completionRate
                ? _value.completionRate
                : completionRate // ignore: cast_nullable_to_non_nullable
                      as double?,
            assignmentsAccepted: freezed == assignmentsAccepted
                ? _value.assignmentsAccepted
                : assignmentsAccepted // ignore: cast_nullable_to_non_nullable
                      as int?,
            assignmentsCompleted: freezed == assignmentsCompleted
                ? _value.assignmentsCompleted
                : assignmentsCompleted // ignore: cast_nullable_to_non_nullable
                      as int?,
            competitionsEntered: freezed == competitionsEntered
                ? _value.competitionsEntered
                : competitionsEntered // ignore: cast_nullable_to_non_nullable
                      as int?,
            competitionsWon: freezed == competitionsWon
                ? _value.competitionsWon
                : competitionsWon // ignore: cast_nullable_to_non_nullable
                      as int?,
            speakerSessions: freezed == speakerSessions
                ? _value.speakerSessions
                : speakerSessions // ignore: cast_nullable_to_non_nullable
                      as int?,
            certificates: freezed == certificates
                ? _value.certificates
                : certificates // ignore: cast_nullable_to_non_nullable
                      as int?,
            achievementCertificates: freezed == achievementCertificates
                ? _value.achievementCertificates
                : achievementCertificates // ignore: cast_nullable_to_non_nullable
                      as int?,
            organizations: freezed == organizations
                ? _value.organizations
                : organizations // ignore: cast_nullable_to_non_nullable
                      as int?,
            verifiedOrganizations: freezed == verifiedOrganizations
                ? _value.verifiedOrganizations
                : verifiedOrganizations // ignore: cast_nullable_to_non_nullable
                      as int?,
            allyCount: freezed == allyCount
                ? _value.allyCount
                : allyCount // ignore: cast_nullable_to_non_nullable
                      as int?,
            cities: null == cities
                ? _value.cities
                : cities // ignore: cast_nullable_to_non_nullable
                      as List<String>,
            eventDna: null == eventDna
                ? _value.eventDna
                : eventDna // ignore: cast_nullable_to_non_nullable
                      as List<EventDnaTagDto>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ProfileMetricsDtoImplCopyWith<$Res>
    implements $ProfileMetricsDtoCopyWith<$Res> {
  factory _$$ProfileMetricsDtoImplCopyWith(
    _$ProfileMetricsDtoImpl value,
    $Res Function(_$ProfileMetricsDtoImpl) then,
  ) = __$$ProfileMetricsDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'events_organized') int? eventsOrganized,
    @JsonKey(name: 'events_participated') int? eventsParticipated,
    @JsonKey(name: 'events_attended') int? eventsAttended,
    @JsonKey(name: 'completion_rate') double? completionRate,
    @JsonKey(name: 'assignments_accepted') int? assignmentsAccepted,
    @JsonKey(name: 'assignments_completed') int? assignmentsCompleted,
    @JsonKey(name: 'competitions_entered') int? competitionsEntered,
    @JsonKey(name: 'competitions_won') int? competitionsWon,
    @JsonKey(name: 'speaker_sessions') int? speakerSessions,
    int? certificates,
    @JsonKey(name: 'achievement_certificates') int? achievementCertificates,
    int? organizations,
    @JsonKey(name: 'verified_organizations') int? verifiedOrganizations,
    @JsonKey(name: 'ally_count') int? allyCount,
    List<String> cities,
    @JsonKey(name: 'event_dna') List<EventDnaTagDto> eventDna,
  });
}

/// @nodoc
class __$$ProfileMetricsDtoImplCopyWithImpl<$Res>
    extends _$ProfileMetricsDtoCopyWithImpl<$Res, _$ProfileMetricsDtoImpl>
    implements _$$ProfileMetricsDtoImplCopyWith<$Res> {
  __$$ProfileMetricsDtoImplCopyWithImpl(
    _$ProfileMetricsDtoImpl _value,
    $Res Function(_$ProfileMetricsDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ProfileMetricsDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? eventsOrganized = freezed,
    Object? eventsParticipated = freezed,
    Object? eventsAttended = freezed,
    Object? completionRate = freezed,
    Object? assignmentsAccepted = freezed,
    Object? assignmentsCompleted = freezed,
    Object? competitionsEntered = freezed,
    Object? competitionsWon = freezed,
    Object? speakerSessions = freezed,
    Object? certificates = freezed,
    Object? achievementCertificates = freezed,
    Object? organizations = freezed,
    Object? verifiedOrganizations = freezed,
    Object? allyCount = freezed,
    Object? cities = null,
    Object? eventDna = null,
  }) {
    return _then(
      _$ProfileMetricsDtoImpl(
        eventsOrganized: freezed == eventsOrganized
            ? _value.eventsOrganized
            : eventsOrganized // ignore: cast_nullable_to_non_nullable
                  as int?,
        eventsParticipated: freezed == eventsParticipated
            ? _value.eventsParticipated
            : eventsParticipated // ignore: cast_nullable_to_non_nullable
                  as int?,
        eventsAttended: freezed == eventsAttended
            ? _value.eventsAttended
            : eventsAttended // ignore: cast_nullable_to_non_nullable
                  as int?,
        completionRate: freezed == completionRate
            ? _value.completionRate
            : completionRate // ignore: cast_nullable_to_non_nullable
                  as double?,
        assignmentsAccepted: freezed == assignmentsAccepted
            ? _value.assignmentsAccepted
            : assignmentsAccepted // ignore: cast_nullable_to_non_nullable
                  as int?,
        assignmentsCompleted: freezed == assignmentsCompleted
            ? _value.assignmentsCompleted
            : assignmentsCompleted // ignore: cast_nullable_to_non_nullable
                  as int?,
        competitionsEntered: freezed == competitionsEntered
            ? _value.competitionsEntered
            : competitionsEntered // ignore: cast_nullable_to_non_nullable
                  as int?,
        competitionsWon: freezed == competitionsWon
            ? _value.competitionsWon
            : competitionsWon // ignore: cast_nullable_to_non_nullable
                  as int?,
        speakerSessions: freezed == speakerSessions
            ? _value.speakerSessions
            : speakerSessions // ignore: cast_nullable_to_non_nullable
                  as int?,
        certificates: freezed == certificates
            ? _value.certificates
            : certificates // ignore: cast_nullable_to_non_nullable
                  as int?,
        achievementCertificates: freezed == achievementCertificates
            ? _value.achievementCertificates
            : achievementCertificates // ignore: cast_nullable_to_non_nullable
                  as int?,
        organizations: freezed == organizations
            ? _value.organizations
            : organizations // ignore: cast_nullable_to_non_nullable
                  as int?,
        verifiedOrganizations: freezed == verifiedOrganizations
            ? _value.verifiedOrganizations
            : verifiedOrganizations // ignore: cast_nullable_to_non_nullable
                  as int?,
        allyCount: freezed == allyCount
            ? _value.allyCount
            : allyCount // ignore: cast_nullable_to_non_nullable
                  as int?,
        cities: null == cities
            ? _value._cities
            : cities // ignore: cast_nullable_to_non_nullable
                  as List<String>,
        eventDna: null == eventDna
            ? _value._eventDna
            : eventDna // ignore: cast_nullable_to_non_nullable
                  as List<EventDnaTagDto>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ProfileMetricsDtoImpl implements _ProfileMetricsDto {
  const _$ProfileMetricsDtoImpl({
    @JsonKey(name: 'events_organized') this.eventsOrganized,
    @JsonKey(name: 'events_participated') this.eventsParticipated,
    @JsonKey(name: 'events_attended') this.eventsAttended,
    @JsonKey(name: 'completion_rate') this.completionRate,
    @JsonKey(name: 'assignments_accepted') this.assignmentsAccepted,
    @JsonKey(name: 'assignments_completed') this.assignmentsCompleted,
    @JsonKey(name: 'competitions_entered') this.competitionsEntered,
    @JsonKey(name: 'competitions_won') this.competitionsWon,
    @JsonKey(name: 'speaker_sessions') this.speakerSessions,
    this.certificates,
    @JsonKey(name: 'achievement_certificates') this.achievementCertificates,
    this.organizations,
    @JsonKey(name: 'verified_organizations') this.verifiedOrganizations,
    @JsonKey(name: 'ally_count') this.allyCount,
    final List<String> cities = const <String>[],
    @JsonKey(name: 'event_dna')
    final List<EventDnaTagDto> eventDna = const <EventDnaTagDto>[],
  }) : _cities = cities,
       _eventDna = eventDna;

  factory _$ProfileMetricsDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ProfileMetricsDtoImplFromJson(json);

  // Every count nullable: null means hidden from this viewer, never zero (D-229/H2). A
  // `@Default(0)` here would silently convert a privacy choice into a claim about the person.
  @override
  @JsonKey(name: 'events_organized')
  final int? eventsOrganized;
  @override
  @JsonKey(name: 'events_participated')
  final int? eventsParticipated;
  @override
  @JsonKey(name: 'events_attended')
  final int? eventsAttended;
  @override
  @JsonKey(name: 'completion_rate')
  final double? completionRate;
  @override
  @JsonKey(name: 'assignments_accepted')
  final int? assignmentsAccepted;
  @override
  @JsonKey(name: 'assignments_completed')
  final int? assignmentsCompleted;
  @override
  @JsonKey(name: 'competitions_entered')
  final int? competitionsEntered;
  @override
  @JsonKey(name: 'competitions_won')
  final int? competitionsWon;
  @override
  @JsonKey(name: 'speaker_sessions')
  final int? speakerSessions;
  @override
  final int? certificates;
  @override
  @JsonKey(name: 'achievement_certificates')
  final int? achievementCertificates;
  @override
  final int? organizations;
  @override
  @JsonKey(name: 'verified_organizations')
  final int? verifiedOrganizations;
  @override
  @JsonKey(name: 'ally_count')
  final int? allyCount;
  final List<String> _cities;
  @override
  @JsonKey()
  List<String> get cities {
    if (_cities is EqualUnmodifiableListView) return _cities;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_cities);
  }

  final List<EventDnaTagDto> _eventDna;
  @override
  @JsonKey(name: 'event_dna')
  List<EventDnaTagDto> get eventDna {
    if (_eventDna is EqualUnmodifiableListView) return _eventDna;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_eventDna);
  }

  @override
  String toString() {
    return 'ProfileMetricsDto(eventsOrganized: $eventsOrganized, eventsParticipated: $eventsParticipated, eventsAttended: $eventsAttended, completionRate: $completionRate, assignmentsAccepted: $assignmentsAccepted, assignmentsCompleted: $assignmentsCompleted, competitionsEntered: $competitionsEntered, competitionsWon: $competitionsWon, speakerSessions: $speakerSessions, certificates: $certificates, achievementCertificates: $achievementCertificates, organizations: $organizations, verifiedOrganizations: $verifiedOrganizations, allyCount: $allyCount, cities: $cities, eventDna: $eventDna)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ProfileMetricsDtoImpl &&
            (identical(other.eventsOrganized, eventsOrganized) ||
                other.eventsOrganized == eventsOrganized) &&
            (identical(other.eventsParticipated, eventsParticipated) ||
                other.eventsParticipated == eventsParticipated) &&
            (identical(other.eventsAttended, eventsAttended) ||
                other.eventsAttended == eventsAttended) &&
            (identical(other.completionRate, completionRate) ||
                other.completionRate == completionRate) &&
            (identical(other.assignmentsAccepted, assignmentsAccepted) ||
                other.assignmentsAccepted == assignmentsAccepted) &&
            (identical(other.assignmentsCompleted, assignmentsCompleted) ||
                other.assignmentsCompleted == assignmentsCompleted) &&
            (identical(other.competitionsEntered, competitionsEntered) ||
                other.competitionsEntered == competitionsEntered) &&
            (identical(other.competitionsWon, competitionsWon) ||
                other.competitionsWon == competitionsWon) &&
            (identical(other.speakerSessions, speakerSessions) ||
                other.speakerSessions == speakerSessions) &&
            (identical(other.certificates, certificates) ||
                other.certificates == certificates) &&
            (identical(
                  other.achievementCertificates,
                  achievementCertificates,
                ) ||
                other.achievementCertificates == achievementCertificates) &&
            (identical(other.organizations, organizations) ||
                other.organizations == organizations) &&
            (identical(other.verifiedOrganizations, verifiedOrganizations) ||
                other.verifiedOrganizations == verifiedOrganizations) &&
            (identical(other.allyCount, allyCount) ||
                other.allyCount == allyCount) &&
            const DeepCollectionEquality().equals(other._cities, _cities) &&
            const DeepCollectionEquality().equals(other._eventDna, _eventDna));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    eventsOrganized,
    eventsParticipated,
    eventsAttended,
    completionRate,
    assignmentsAccepted,
    assignmentsCompleted,
    competitionsEntered,
    competitionsWon,
    speakerSessions,
    certificates,
    achievementCertificates,
    organizations,
    verifiedOrganizations,
    allyCount,
    const DeepCollectionEquality().hash(_cities),
    const DeepCollectionEquality().hash(_eventDna),
  );

  /// Create a copy of ProfileMetricsDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ProfileMetricsDtoImplCopyWith<_$ProfileMetricsDtoImpl> get copyWith =>
      __$$ProfileMetricsDtoImplCopyWithImpl<_$ProfileMetricsDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$ProfileMetricsDtoImplToJson(this);
  }
}

abstract class _ProfileMetricsDto implements ProfileMetricsDto {
  const factory _ProfileMetricsDto({
    @JsonKey(name: 'events_organized') final int? eventsOrganized,
    @JsonKey(name: 'events_participated') final int? eventsParticipated,
    @JsonKey(name: 'events_attended') final int? eventsAttended,
    @JsonKey(name: 'completion_rate') final double? completionRate,
    @JsonKey(name: 'assignments_accepted') final int? assignmentsAccepted,
    @JsonKey(name: 'assignments_completed') final int? assignmentsCompleted,
    @JsonKey(name: 'competitions_entered') final int? competitionsEntered,
    @JsonKey(name: 'competitions_won') final int? competitionsWon,
    @JsonKey(name: 'speaker_sessions') final int? speakerSessions,
    final int? certificates,
    @JsonKey(name: 'achievement_certificates')
    final int? achievementCertificates,
    final int? organizations,
    @JsonKey(name: 'verified_organizations') final int? verifiedOrganizations,
    @JsonKey(name: 'ally_count') final int? allyCount,
    final List<String> cities,
    @JsonKey(name: 'event_dna') final List<EventDnaTagDto> eventDna,
  }) = _$ProfileMetricsDtoImpl;

  factory _ProfileMetricsDto.fromJson(Map<String, dynamic> json) =
      _$ProfileMetricsDtoImpl.fromJson;

  // Every count nullable: null means hidden from this viewer, never zero (D-229/H2). A
  // `@Default(0)` here would silently convert a privacy choice into a claim about the person.
  @override
  @JsonKey(name: 'events_organized')
  int? get eventsOrganized;
  @override
  @JsonKey(name: 'events_participated')
  int? get eventsParticipated;
  @override
  @JsonKey(name: 'events_attended')
  int? get eventsAttended;
  @override
  @JsonKey(name: 'completion_rate')
  double? get completionRate;
  @override
  @JsonKey(name: 'assignments_accepted')
  int? get assignmentsAccepted;
  @override
  @JsonKey(name: 'assignments_completed')
  int? get assignmentsCompleted;
  @override
  @JsonKey(name: 'competitions_entered')
  int? get competitionsEntered;
  @override
  @JsonKey(name: 'competitions_won')
  int? get competitionsWon;
  @override
  @JsonKey(name: 'speaker_sessions')
  int? get speakerSessions;
  @override
  int? get certificates;
  @override
  @JsonKey(name: 'achievement_certificates')
  int? get achievementCertificates;
  @override
  int? get organizations;
  @override
  @JsonKey(name: 'verified_organizations')
  int? get verifiedOrganizations;
  @override
  @JsonKey(name: 'ally_count')
  int? get allyCount;
  @override
  List<String> get cities;
  @override
  @JsonKey(name: 'event_dna')
  List<EventDnaTagDto> get eventDna;

  /// Create a copy of ProfileMetricsDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ProfileMetricsDtoImplCopyWith<_$ProfileMetricsDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

ExperienceSummaryDto _$ExperienceSummaryDtoFromJson(Map<String, dynamic> json) {
  return _ExperienceSummaryDto.fromJson(json);
}

/// @nodoc
mixin _$ExperienceSummaryDto {
  String get band => throw _privateConstructorUsedError;
  @JsonKey(name: 'distinct_events')
  int get distinctEvents => throw _privateConstructorUsedError;
  @JsonKey(name: 'events_organized')
  int get eventsOrganized => throw _privateConstructorUsedError;
  @JsonKey(name: 'events_participated')
  int get eventsParticipated => throw _privateConstructorUsedError;
  @JsonKey(name: 'events_attended')
  int get eventsAttended => throw _privateConstructorUsedError;
  @JsonKey(name: 'leadership_events')
  int get leadershipEvents => throw _privateConstructorUsedError;
  int get organizations => throw _privateConstructorUsedError;
  @JsonKey(name: 'verified_organizations')
  int get verifiedOrganizations => throw _privateConstructorUsedError;
  @JsonKey(name: 'assignments_completed')
  int get assignmentsCompleted => throw _privateConstructorUsedError;
  @JsonKey(name: 'speaker_sessions')
  int get speakerSessions => throw _privateConstructorUsedError;
  @JsonKey(name: 'competitions_won')
  int get competitionsWon => throw _privateConstructorUsedError;
  @JsonKey(name: 'years_active')
  int get yearsActive => throw _privateConstructorUsedError;
  @JsonKey(name: 'first_activity_at')
  DateTime? get firstActivityAt => throw _privateConstructorUsedError;

  /// Serializes this ExperienceSummaryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ExperienceSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ExperienceSummaryDtoCopyWith<ExperienceSummaryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ExperienceSummaryDtoCopyWith<$Res> {
  factory $ExperienceSummaryDtoCopyWith(
    ExperienceSummaryDto value,
    $Res Function(ExperienceSummaryDto) then,
  ) = _$ExperienceSummaryDtoCopyWithImpl<$Res, ExperienceSummaryDto>;
  @useResult
  $Res call({
    String band,
    @JsonKey(name: 'distinct_events') int distinctEvents,
    @JsonKey(name: 'events_organized') int eventsOrganized,
    @JsonKey(name: 'events_participated') int eventsParticipated,
    @JsonKey(name: 'events_attended') int eventsAttended,
    @JsonKey(name: 'leadership_events') int leadershipEvents,
    int organizations,
    @JsonKey(name: 'verified_organizations') int verifiedOrganizations,
    @JsonKey(name: 'assignments_completed') int assignmentsCompleted,
    @JsonKey(name: 'speaker_sessions') int speakerSessions,
    @JsonKey(name: 'competitions_won') int competitionsWon,
    @JsonKey(name: 'years_active') int yearsActive,
    @JsonKey(name: 'first_activity_at') DateTime? firstActivityAt,
  });
}

/// @nodoc
class _$ExperienceSummaryDtoCopyWithImpl<
  $Res,
  $Val extends ExperienceSummaryDto
>
    implements $ExperienceSummaryDtoCopyWith<$Res> {
  _$ExperienceSummaryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ExperienceSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? band = null,
    Object? distinctEvents = null,
    Object? eventsOrganized = null,
    Object? eventsParticipated = null,
    Object? eventsAttended = null,
    Object? leadershipEvents = null,
    Object? organizations = null,
    Object? verifiedOrganizations = null,
    Object? assignmentsCompleted = null,
    Object? speakerSessions = null,
    Object? competitionsWon = null,
    Object? yearsActive = null,
    Object? firstActivityAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            band: null == band
                ? _value.band
                : band // ignore: cast_nullable_to_non_nullable
                      as String,
            distinctEvents: null == distinctEvents
                ? _value.distinctEvents
                : distinctEvents // ignore: cast_nullable_to_non_nullable
                      as int,
            eventsOrganized: null == eventsOrganized
                ? _value.eventsOrganized
                : eventsOrganized // ignore: cast_nullable_to_non_nullable
                      as int,
            eventsParticipated: null == eventsParticipated
                ? _value.eventsParticipated
                : eventsParticipated // ignore: cast_nullable_to_non_nullable
                      as int,
            eventsAttended: null == eventsAttended
                ? _value.eventsAttended
                : eventsAttended // ignore: cast_nullable_to_non_nullable
                      as int,
            leadershipEvents: null == leadershipEvents
                ? _value.leadershipEvents
                : leadershipEvents // ignore: cast_nullable_to_non_nullable
                      as int,
            organizations: null == organizations
                ? _value.organizations
                : organizations // ignore: cast_nullable_to_non_nullable
                      as int,
            verifiedOrganizations: null == verifiedOrganizations
                ? _value.verifiedOrganizations
                : verifiedOrganizations // ignore: cast_nullable_to_non_nullable
                      as int,
            assignmentsCompleted: null == assignmentsCompleted
                ? _value.assignmentsCompleted
                : assignmentsCompleted // ignore: cast_nullable_to_non_nullable
                      as int,
            speakerSessions: null == speakerSessions
                ? _value.speakerSessions
                : speakerSessions // ignore: cast_nullable_to_non_nullable
                      as int,
            competitionsWon: null == competitionsWon
                ? _value.competitionsWon
                : competitionsWon // ignore: cast_nullable_to_non_nullable
                      as int,
            yearsActive: null == yearsActive
                ? _value.yearsActive
                : yearsActive // ignore: cast_nullable_to_non_nullable
                      as int,
            firstActivityAt: freezed == firstActivityAt
                ? _value.firstActivityAt
                : firstActivityAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ExperienceSummaryDtoImplCopyWith<$Res>
    implements $ExperienceSummaryDtoCopyWith<$Res> {
  factory _$$ExperienceSummaryDtoImplCopyWith(
    _$ExperienceSummaryDtoImpl value,
    $Res Function(_$ExperienceSummaryDtoImpl) then,
  ) = __$$ExperienceSummaryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String band,
    @JsonKey(name: 'distinct_events') int distinctEvents,
    @JsonKey(name: 'events_organized') int eventsOrganized,
    @JsonKey(name: 'events_participated') int eventsParticipated,
    @JsonKey(name: 'events_attended') int eventsAttended,
    @JsonKey(name: 'leadership_events') int leadershipEvents,
    int organizations,
    @JsonKey(name: 'verified_organizations') int verifiedOrganizations,
    @JsonKey(name: 'assignments_completed') int assignmentsCompleted,
    @JsonKey(name: 'speaker_sessions') int speakerSessions,
    @JsonKey(name: 'competitions_won') int competitionsWon,
    @JsonKey(name: 'years_active') int yearsActive,
    @JsonKey(name: 'first_activity_at') DateTime? firstActivityAt,
  });
}

/// @nodoc
class __$$ExperienceSummaryDtoImplCopyWithImpl<$Res>
    extends _$ExperienceSummaryDtoCopyWithImpl<$Res, _$ExperienceSummaryDtoImpl>
    implements _$$ExperienceSummaryDtoImplCopyWith<$Res> {
  __$$ExperienceSummaryDtoImplCopyWithImpl(
    _$ExperienceSummaryDtoImpl _value,
    $Res Function(_$ExperienceSummaryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ExperienceSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? band = null,
    Object? distinctEvents = null,
    Object? eventsOrganized = null,
    Object? eventsParticipated = null,
    Object? eventsAttended = null,
    Object? leadershipEvents = null,
    Object? organizations = null,
    Object? verifiedOrganizations = null,
    Object? assignmentsCompleted = null,
    Object? speakerSessions = null,
    Object? competitionsWon = null,
    Object? yearsActive = null,
    Object? firstActivityAt = freezed,
  }) {
    return _then(
      _$ExperienceSummaryDtoImpl(
        band: null == band
            ? _value.band
            : band // ignore: cast_nullable_to_non_nullable
                  as String,
        distinctEvents: null == distinctEvents
            ? _value.distinctEvents
            : distinctEvents // ignore: cast_nullable_to_non_nullable
                  as int,
        eventsOrganized: null == eventsOrganized
            ? _value.eventsOrganized
            : eventsOrganized // ignore: cast_nullable_to_non_nullable
                  as int,
        eventsParticipated: null == eventsParticipated
            ? _value.eventsParticipated
            : eventsParticipated // ignore: cast_nullable_to_non_nullable
                  as int,
        eventsAttended: null == eventsAttended
            ? _value.eventsAttended
            : eventsAttended // ignore: cast_nullable_to_non_nullable
                  as int,
        leadershipEvents: null == leadershipEvents
            ? _value.leadershipEvents
            : leadershipEvents // ignore: cast_nullable_to_non_nullable
                  as int,
        organizations: null == organizations
            ? _value.organizations
            : organizations // ignore: cast_nullable_to_non_nullable
                  as int,
        verifiedOrganizations: null == verifiedOrganizations
            ? _value.verifiedOrganizations
            : verifiedOrganizations // ignore: cast_nullable_to_non_nullable
                  as int,
        assignmentsCompleted: null == assignmentsCompleted
            ? _value.assignmentsCompleted
            : assignmentsCompleted // ignore: cast_nullable_to_non_nullable
                  as int,
        speakerSessions: null == speakerSessions
            ? _value.speakerSessions
            : speakerSessions // ignore: cast_nullable_to_non_nullable
                  as int,
        competitionsWon: null == competitionsWon
            ? _value.competitionsWon
            : competitionsWon // ignore: cast_nullable_to_non_nullable
                  as int,
        yearsActive: null == yearsActive
            ? _value.yearsActive
            : yearsActive // ignore: cast_nullable_to_non_nullable
                  as int,
        firstActivityAt: freezed == firstActivityAt
            ? _value.firstActivityAt
            : firstActivityAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ExperienceSummaryDtoImpl implements _ExperienceSummaryDto {
  const _$ExperienceSummaryDtoImpl({
    this.band = 'Building',
    @JsonKey(name: 'distinct_events') this.distinctEvents = 0,
    @JsonKey(name: 'events_organized') this.eventsOrganized = 0,
    @JsonKey(name: 'events_participated') this.eventsParticipated = 0,
    @JsonKey(name: 'events_attended') this.eventsAttended = 0,
    @JsonKey(name: 'leadership_events') this.leadershipEvents = 0,
    this.organizations = 0,
    @JsonKey(name: 'verified_organizations') this.verifiedOrganizations = 0,
    @JsonKey(name: 'assignments_completed') this.assignmentsCompleted = 0,
    @JsonKey(name: 'speaker_sessions') this.speakerSessions = 0,
    @JsonKey(name: 'competitions_won') this.competitionsWon = 0,
    @JsonKey(name: 'years_active') this.yearsActive = 0,
    @JsonKey(name: 'first_activity_at') this.firstActivityAt,
  });

  factory _$ExperienceSummaryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ExperienceSummaryDtoImplFromJson(json);

  @override
  @JsonKey()
  final String band;
  @override
  @JsonKey(name: 'distinct_events')
  final int distinctEvents;
  @override
  @JsonKey(name: 'events_organized')
  final int eventsOrganized;
  @override
  @JsonKey(name: 'events_participated')
  final int eventsParticipated;
  @override
  @JsonKey(name: 'events_attended')
  final int eventsAttended;
  @override
  @JsonKey(name: 'leadership_events')
  final int leadershipEvents;
  @override
  @JsonKey()
  final int organizations;
  @override
  @JsonKey(name: 'verified_organizations')
  final int verifiedOrganizations;
  @override
  @JsonKey(name: 'assignments_completed')
  final int assignmentsCompleted;
  @override
  @JsonKey(name: 'speaker_sessions')
  final int speakerSessions;
  @override
  @JsonKey(name: 'competitions_won')
  final int competitionsWon;
  @override
  @JsonKey(name: 'years_active')
  final int yearsActive;
  @override
  @JsonKey(name: 'first_activity_at')
  final DateTime? firstActivityAt;

  @override
  String toString() {
    return 'ExperienceSummaryDto(band: $band, distinctEvents: $distinctEvents, eventsOrganized: $eventsOrganized, eventsParticipated: $eventsParticipated, eventsAttended: $eventsAttended, leadershipEvents: $leadershipEvents, organizations: $organizations, verifiedOrganizations: $verifiedOrganizations, assignmentsCompleted: $assignmentsCompleted, speakerSessions: $speakerSessions, competitionsWon: $competitionsWon, yearsActive: $yearsActive, firstActivityAt: $firstActivityAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ExperienceSummaryDtoImpl &&
            (identical(other.band, band) || other.band == band) &&
            (identical(other.distinctEvents, distinctEvents) ||
                other.distinctEvents == distinctEvents) &&
            (identical(other.eventsOrganized, eventsOrganized) ||
                other.eventsOrganized == eventsOrganized) &&
            (identical(other.eventsParticipated, eventsParticipated) ||
                other.eventsParticipated == eventsParticipated) &&
            (identical(other.eventsAttended, eventsAttended) ||
                other.eventsAttended == eventsAttended) &&
            (identical(other.leadershipEvents, leadershipEvents) ||
                other.leadershipEvents == leadershipEvents) &&
            (identical(other.organizations, organizations) ||
                other.organizations == organizations) &&
            (identical(other.verifiedOrganizations, verifiedOrganizations) ||
                other.verifiedOrganizations == verifiedOrganizations) &&
            (identical(other.assignmentsCompleted, assignmentsCompleted) ||
                other.assignmentsCompleted == assignmentsCompleted) &&
            (identical(other.speakerSessions, speakerSessions) ||
                other.speakerSessions == speakerSessions) &&
            (identical(other.competitionsWon, competitionsWon) ||
                other.competitionsWon == competitionsWon) &&
            (identical(other.yearsActive, yearsActive) ||
                other.yearsActive == yearsActive) &&
            (identical(other.firstActivityAt, firstActivityAt) ||
                other.firstActivityAt == firstActivityAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    band,
    distinctEvents,
    eventsOrganized,
    eventsParticipated,
    eventsAttended,
    leadershipEvents,
    organizations,
    verifiedOrganizations,
    assignmentsCompleted,
    speakerSessions,
    competitionsWon,
    yearsActive,
    firstActivityAt,
  );

  /// Create a copy of ExperienceSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ExperienceSummaryDtoImplCopyWith<_$ExperienceSummaryDtoImpl>
  get copyWith =>
      __$$ExperienceSummaryDtoImplCopyWithImpl<_$ExperienceSummaryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$ExperienceSummaryDtoImplToJson(this);
  }
}

abstract class _ExperienceSummaryDto implements ExperienceSummaryDto {
  const factory _ExperienceSummaryDto({
    final String band,
    @JsonKey(name: 'distinct_events') final int distinctEvents,
    @JsonKey(name: 'events_organized') final int eventsOrganized,
    @JsonKey(name: 'events_participated') final int eventsParticipated,
    @JsonKey(name: 'events_attended') final int eventsAttended,
    @JsonKey(name: 'leadership_events') final int leadershipEvents,
    final int organizations,
    @JsonKey(name: 'verified_organizations') final int verifiedOrganizations,
    @JsonKey(name: 'assignments_completed') final int assignmentsCompleted,
    @JsonKey(name: 'speaker_sessions') final int speakerSessions,
    @JsonKey(name: 'competitions_won') final int competitionsWon,
    @JsonKey(name: 'years_active') final int yearsActive,
    @JsonKey(name: 'first_activity_at') final DateTime? firstActivityAt,
  }) = _$ExperienceSummaryDtoImpl;

  factory _ExperienceSummaryDto.fromJson(Map<String, dynamic> json) =
      _$ExperienceSummaryDtoImpl.fromJson;

  @override
  String get band;
  @override
  @JsonKey(name: 'distinct_events')
  int get distinctEvents;
  @override
  @JsonKey(name: 'events_organized')
  int get eventsOrganized;
  @override
  @JsonKey(name: 'events_participated')
  int get eventsParticipated;
  @override
  @JsonKey(name: 'events_attended')
  int get eventsAttended;
  @override
  @JsonKey(name: 'leadership_events')
  int get leadershipEvents;
  @override
  int get organizations;
  @override
  @JsonKey(name: 'verified_organizations')
  int get verifiedOrganizations;
  @override
  @JsonKey(name: 'assignments_completed')
  int get assignmentsCompleted;
  @override
  @JsonKey(name: 'speaker_sessions')
  int get speakerSessions;
  @override
  @JsonKey(name: 'competitions_won')
  int get competitionsWon;
  @override
  @JsonKey(name: 'years_active')
  int get yearsActive;
  @override
  @JsonKey(name: 'first_activity_at')
  DateTime? get firstActivityAt;

  /// Create a copy of ExperienceSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ExperienceSummaryDtoImplCopyWith<_$ExperienceSummaryDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

ContributionDayDto _$ContributionDayDtoFromJson(Map<String, dynamic> json) {
  return _ContributionDayDto.fromJson(json);
}

/// @nodoc
mixin _$ContributionDayDto {
  String get date => throw _privateConstructorUsedError;
  int get count => throw _privateConstructorUsedError;
  int get level => throw _privateConstructorUsedError;

  /// Serializes this ContributionDayDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ContributionDayDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ContributionDayDtoCopyWith<ContributionDayDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ContributionDayDtoCopyWith<$Res> {
  factory $ContributionDayDtoCopyWith(
    ContributionDayDto value,
    $Res Function(ContributionDayDto) then,
  ) = _$ContributionDayDtoCopyWithImpl<$Res, ContributionDayDto>;
  @useResult
  $Res call({String date, int count, int level});
}

/// @nodoc
class _$ContributionDayDtoCopyWithImpl<$Res, $Val extends ContributionDayDto>
    implements $ContributionDayDtoCopyWith<$Res> {
  _$ContributionDayDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ContributionDayDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? date = null, Object? count = null, Object? level = null}) {
    return _then(
      _value.copyWith(
            date: null == date
                ? _value.date
                : date // ignore: cast_nullable_to_non_nullable
                      as String,
            count: null == count
                ? _value.count
                : count // ignore: cast_nullable_to_non_nullable
                      as int,
            level: null == level
                ? _value.level
                : level // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ContributionDayDtoImplCopyWith<$Res>
    implements $ContributionDayDtoCopyWith<$Res> {
  factory _$$ContributionDayDtoImplCopyWith(
    _$ContributionDayDtoImpl value,
    $Res Function(_$ContributionDayDtoImpl) then,
  ) = __$$ContributionDayDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({String date, int count, int level});
}

/// @nodoc
class __$$ContributionDayDtoImplCopyWithImpl<$Res>
    extends _$ContributionDayDtoCopyWithImpl<$Res, _$ContributionDayDtoImpl>
    implements _$$ContributionDayDtoImplCopyWith<$Res> {
  __$$ContributionDayDtoImplCopyWithImpl(
    _$ContributionDayDtoImpl _value,
    $Res Function(_$ContributionDayDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ContributionDayDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? date = null, Object? count = null, Object? level = null}) {
    return _then(
      _$ContributionDayDtoImpl(
        date: null == date
            ? _value.date
            : date // ignore: cast_nullable_to_non_nullable
                  as String,
        count: null == count
            ? _value.count
            : count // ignore: cast_nullable_to_non_nullable
                  as int,
        level: null == level
            ? _value.level
            : level // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ContributionDayDtoImpl implements _ContributionDayDto {
  const _$ContributionDayDtoImpl({
    required this.date,
    this.count = 0,
    this.level = 0,
  });

  factory _$ContributionDayDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ContributionDayDtoImplFromJson(json);

  @override
  final String date;
  @override
  @JsonKey()
  final int count;
  @override
  @JsonKey()
  final int level;

  @override
  String toString() {
    return 'ContributionDayDto(date: $date, count: $count, level: $level)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ContributionDayDtoImpl &&
            (identical(other.date, date) || other.date == date) &&
            (identical(other.count, count) || other.count == count) &&
            (identical(other.level, level) || other.level == level));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, date, count, level);

  /// Create a copy of ContributionDayDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ContributionDayDtoImplCopyWith<_$ContributionDayDtoImpl> get copyWith =>
      __$$ContributionDayDtoImplCopyWithImpl<_$ContributionDayDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$ContributionDayDtoImplToJson(this);
  }
}

abstract class _ContributionDayDto implements ContributionDayDto {
  const factory _ContributionDayDto({
    required final String date,
    final int count,
    final int level,
  }) = _$ContributionDayDtoImpl;

  factory _ContributionDayDto.fromJson(Map<String, dynamic> json) =
      _$ContributionDayDtoImpl.fromJson;

  @override
  String get date;
  @override
  int get count;
  @override
  int get level;

  /// Create a copy of ContributionDayDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ContributionDayDtoImplCopyWith<_$ContributionDayDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

ContributionsDto _$ContributionsDtoFromJson(Map<String, dynamic> json) {
  return _ContributionsDto.fromJson(json);
}

/// @nodoc
mixin _$ContributionsDto {
  String get from => throw _privateConstructorUsedError;
  String get to => throw _privateConstructorUsedError;
  int get total => throw _privateConstructorUsedError;
  List<ContributionDayDto> get days => throw _privateConstructorUsedError;

  /// Serializes this ContributionsDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ContributionsDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ContributionsDtoCopyWith<ContributionsDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ContributionsDtoCopyWith<$Res> {
  factory $ContributionsDtoCopyWith(
    ContributionsDto value,
    $Res Function(ContributionsDto) then,
  ) = _$ContributionsDtoCopyWithImpl<$Res, ContributionsDto>;
  @useResult
  $Res call({String from, String to, int total, List<ContributionDayDto> days});
}

/// @nodoc
class _$ContributionsDtoCopyWithImpl<$Res, $Val extends ContributionsDto>
    implements $ContributionsDtoCopyWith<$Res> {
  _$ContributionsDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ContributionsDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? from = null,
    Object? to = null,
    Object? total = null,
    Object? days = null,
  }) {
    return _then(
      _value.copyWith(
            from: null == from
                ? _value.from
                : from // ignore: cast_nullable_to_non_nullable
                      as String,
            to: null == to
                ? _value.to
                : to // ignore: cast_nullable_to_non_nullable
                      as String,
            total: null == total
                ? _value.total
                : total // ignore: cast_nullable_to_non_nullable
                      as int,
            days: null == days
                ? _value.days
                : days // ignore: cast_nullable_to_non_nullable
                      as List<ContributionDayDto>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ContributionsDtoImplCopyWith<$Res>
    implements $ContributionsDtoCopyWith<$Res> {
  factory _$$ContributionsDtoImplCopyWith(
    _$ContributionsDtoImpl value,
    $Res Function(_$ContributionsDtoImpl) then,
  ) = __$$ContributionsDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({String from, String to, int total, List<ContributionDayDto> days});
}

/// @nodoc
class __$$ContributionsDtoImplCopyWithImpl<$Res>
    extends _$ContributionsDtoCopyWithImpl<$Res, _$ContributionsDtoImpl>
    implements _$$ContributionsDtoImplCopyWith<$Res> {
  __$$ContributionsDtoImplCopyWithImpl(
    _$ContributionsDtoImpl _value,
    $Res Function(_$ContributionsDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ContributionsDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? from = null,
    Object? to = null,
    Object? total = null,
    Object? days = null,
  }) {
    return _then(
      _$ContributionsDtoImpl(
        from: null == from
            ? _value.from
            : from // ignore: cast_nullable_to_non_nullable
                  as String,
        to: null == to
            ? _value.to
            : to // ignore: cast_nullable_to_non_nullable
                  as String,
        total: null == total
            ? _value.total
            : total // ignore: cast_nullable_to_non_nullable
                  as int,
        days: null == days
            ? _value._days
            : days // ignore: cast_nullable_to_non_nullable
                  as List<ContributionDayDto>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ContributionsDtoImpl implements _ContributionsDto {
  const _$ContributionsDtoImpl({
    required this.from,
    required this.to,
    this.total = 0,
    final List<ContributionDayDto> days = const <ContributionDayDto>[],
  }) : _days = days;

  factory _$ContributionsDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ContributionsDtoImplFromJson(json);

  @override
  final String from;
  @override
  final String to;
  @override
  @JsonKey()
  final int total;
  final List<ContributionDayDto> _days;
  @override
  @JsonKey()
  List<ContributionDayDto> get days {
    if (_days is EqualUnmodifiableListView) return _days;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_days);
  }

  @override
  String toString() {
    return 'ContributionsDto(from: $from, to: $to, total: $total, days: $days)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ContributionsDtoImpl &&
            (identical(other.from, from) || other.from == from) &&
            (identical(other.to, to) || other.to == to) &&
            (identical(other.total, total) || other.total == total) &&
            const DeepCollectionEquality().equals(other._days, _days));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    from,
    to,
    total,
    const DeepCollectionEquality().hash(_days),
  );

  /// Create a copy of ContributionsDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ContributionsDtoImplCopyWith<_$ContributionsDtoImpl> get copyWith =>
      __$$ContributionsDtoImplCopyWithImpl<_$ContributionsDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$ContributionsDtoImplToJson(this);
  }
}

abstract class _ContributionsDto implements ContributionsDto {
  const factory _ContributionsDto({
    required final String from,
    required final String to,
    final int total,
    final List<ContributionDayDto> days,
  }) = _$ContributionsDtoImpl;

  factory _ContributionsDto.fromJson(Map<String, dynamic> json) =
      _$ContributionsDtoImpl.fromJson;

  @override
  String get from;
  @override
  String get to;
  @override
  int get total;
  @override
  List<ContributionDayDto> get days;

  /// Create a copy of ContributionsDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ContributionsDtoImplCopyWith<_$ContributionsDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
