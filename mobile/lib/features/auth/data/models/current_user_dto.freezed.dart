// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'current_user_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

CurrentUserDto _$CurrentUserDtoFromJson(Map<String, dynamic> json) {
  return _CurrentUserDto.fromJson(json);
}

/// @nodoc
mixin _$CurrentUserDto {
  String get id => throw _privateConstructorUsedError;
  String get phone => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get username => throw _privateConstructorUsedError;
  String? get email => throw _privateConstructorUsedError;

  /// Proven by an emailed one-time code, not merely present (D-182).
  @JsonKey(name: 'email_verified')
  bool get emailVerified => throw _privateConstructorUsedError;
  @JsonKey(name: 'needs_onboarding')
  bool get needsOnboarding => throw _privateConstructorUsedError;

  /// Date-only (`YYYY-MM-DD`, D-311). Required to finish onboarding, and the only fact on the
  /// account an age-restricted event's MinAge/MaxAge can read. Nullable so an older backend, or a
  /// pre-D-311 account, still parses.
  @JsonKey(name: 'date_of_birth')
  DateTime? get dateOfBirth => throw _privateConstructorUsedError;

  /// When the account was created — the "member since" fact.
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError; // Self-declared display fields (D-219). All optional so an older backend still parses.
  String? get headline => throw _privateConstructorUsedError;
  String? get bio => throw _privateConstructorUsedError;
  List<String> get skills => throw _privateConstructorUsedError;
  List<String> get languages => throw _privateConstructorUsedError;
  List<String> get interests => throw _privateConstructorUsedError;

  /// jsonb array of education entries. Self-declared and never proof (D-220): it was never
  /// migrated into evidence-backed membership claims.
  @JsonKey(name: 'education_json')
  String? get educationJson => throw _privateConstructorUsedError;
  @JsonKey(name: 'links_json')
  String? get linksJson => throw _privateConstructorUsedError;
  @JsonKey(name: 'avatar_key')
  String? get avatarKey => throw _privateConstructorUsedError;
  @JsonKey(name: 'cover_key')
  String? get coverKey => throw _privateConstructorUsedError;
  ProfilePrivacyDto? get privacy => throw _privateConstructorUsedError;

  /// Derived user trust (M7). **Nested on the wire and previously not parsed at all**, so this
  /// client had no access to `can_organize_paid` and could not tell a verified organiser from an
  /// unverified one. Web declared the same field flat and silently defaulted it to false for every
  /// user — the same failure class as D-245 and D-292, found in both clients at once.
  TrustCapabilitiesDto? get trust => throw _privateConstructorUsedError;

  /// Serializes this CurrentUserDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of CurrentUserDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $CurrentUserDtoCopyWith<CurrentUserDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $CurrentUserDtoCopyWith<$Res> {
  factory $CurrentUserDtoCopyWith(
    CurrentUserDto value,
    $Res Function(CurrentUserDto) then,
  ) = _$CurrentUserDtoCopyWithImpl<$Res, CurrentUserDto>;
  @useResult
  $Res call({
    String id,
    String phone,
    String name,
    String? username,
    String? email,
    @JsonKey(name: 'email_verified') bool emailVerified,
    @JsonKey(name: 'needs_onboarding') bool needsOnboarding,
    @JsonKey(name: 'date_of_birth') DateTime? dateOfBirth,
    @JsonKey(name: 'created_at') DateTime? createdAt,
    String? headline,
    String? bio,
    List<String> skills,
    List<String> languages,
    List<String> interests,
    @JsonKey(name: 'education_json') String? educationJson,
    @JsonKey(name: 'links_json') String? linksJson,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'cover_key') String? coverKey,
    ProfilePrivacyDto? privacy,
    TrustCapabilitiesDto? trust,
  });

  $ProfilePrivacyDtoCopyWith<$Res>? get privacy;
  $TrustCapabilitiesDtoCopyWith<$Res>? get trust;
}

/// @nodoc
class _$CurrentUserDtoCopyWithImpl<$Res, $Val extends CurrentUserDto>
    implements $CurrentUserDtoCopyWith<$Res> {
  _$CurrentUserDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of CurrentUserDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? phone = null,
    Object? name = null,
    Object? username = freezed,
    Object? email = freezed,
    Object? emailVerified = null,
    Object? needsOnboarding = null,
    Object? dateOfBirth = freezed,
    Object? createdAt = freezed,
    Object? headline = freezed,
    Object? bio = freezed,
    Object? skills = null,
    Object? languages = null,
    Object? interests = null,
    Object? educationJson = freezed,
    Object? linksJson = freezed,
    Object? avatarKey = freezed,
    Object? coverKey = freezed,
    Object? privacy = freezed,
    Object? trust = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            phone: null == phone
                ? _value.phone
                : phone // ignore: cast_nullable_to_non_nullable
                      as String,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            username: freezed == username
                ? _value.username
                : username // ignore: cast_nullable_to_non_nullable
                      as String?,
            email: freezed == email
                ? _value.email
                : email // ignore: cast_nullable_to_non_nullable
                      as String?,
            emailVerified: null == emailVerified
                ? _value.emailVerified
                : emailVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
            needsOnboarding: null == needsOnboarding
                ? _value.needsOnboarding
                : needsOnboarding // ignore: cast_nullable_to_non_nullable
                      as bool,
            dateOfBirth: freezed == dateOfBirth
                ? _value.dateOfBirth
                : dateOfBirth // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            headline: freezed == headline
                ? _value.headline
                : headline // ignore: cast_nullable_to_non_nullable
                      as String?,
            bio: freezed == bio
                ? _value.bio
                : bio // ignore: cast_nullable_to_non_nullable
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
            educationJson: freezed == educationJson
                ? _value.educationJson
                : educationJson // ignore: cast_nullable_to_non_nullable
                      as String?,
            linksJson: freezed == linksJson
                ? _value.linksJson
                : linksJson // ignore: cast_nullable_to_non_nullable
                      as String?,
            avatarKey: freezed == avatarKey
                ? _value.avatarKey
                : avatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            coverKey: freezed == coverKey
                ? _value.coverKey
                : coverKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            privacy: freezed == privacy
                ? _value.privacy
                : privacy // ignore: cast_nullable_to_non_nullable
                      as ProfilePrivacyDto?,
            trust: freezed == trust
                ? _value.trust
                : trust // ignore: cast_nullable_to_non_nullable
                      as TrustCapabilitiesDto?,
          )
          as $Val,
    );
  }

  /// Create a copy of CurrentUserDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $ProfilePrivacyDtoCopyWith<$Res>? get privacy {
    if (_value.privacy == null) {
      return null;
    }

    return $ProfilePrivacyDtoCopyWith<$Res>(_value.privacy!, (value) {
      return _then(_value.copyWith(privacy: value) as $Val);
    });
  }

  /// Create a copy of CurrentUserDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $TrustCapabilitiesDtoCopyWith<$Res>? get trust {
    if (_value.trust == null) {
      return null;
    }

    return $TrustCapabilitiesDtoCopyWith<$Res>(_value.trust!, (value) {
      return _then(_value.copyWith(trust: value) as $Val);
    });
  }
}

/// @nodoc
abstract class _$$CurrentUserDtoImplCopyWith<$Res>
    implements $CurrentUserDtoCopyWith<$Res> {
  factory _$$CurrentUserDtoImplCopyWith(
    _$CurrentUserDtoImpl value,
    $Res Function(_$CurrentUserDtoImpl) then,
  ) = __$$CurrentUserDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String phone,
    String name,
    String? username,
    String? email,
    @JsonKey(name: 'email_verified') bool emailVerified,
    @JsonKey(name: 'needs_onboarding') bool needsOnboarding,
    @JsonKey(name: 'date_of_birth') DateTime? dateOfBirth,
    @JsonKey(name: 'created_at') DateTime? createdAt,
    String? headline,
    String? bio,
    List<String> skills,
    List<String> languages,
    List<String> interests,
    @JsonKey(name: 'education_json') String? educationJson,
    @JsonKey(name: 'links_json') String? linksJson,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'cover_key') String? coverKey,
    ProfilePrivacyDto? privacy,
    TrustCapabilitiesDto? trust,
  });

  @override
  $ProfilePrivacyDtoCopyWith<$Res>? get privacy;
  @override
  $TrustCapabilitiesDtoCopyWith<$Res>? get trust;
}

/// @nodoc
class __$$CurrentUserDtoImplCopyWithImpl<$Res>
    extends _$CurrentUserDtoCopyWithImpl<$Res, _$CurrentUserDtoImpl>
    implements _$$CurrentUserDtoImplCopyWith<$Res> {
  __$$CurrentUserDtoImplCopyWithImpl(
    _$CurrentUserDtoImpl _value,
    $Res Function(_$CurrentUserDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of CurrentUserDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? phone = null,
    Object? name = null,
    Object? username = freezed,
    Object? email = freezed,
    Object? emailVerified = null,
    Object? needsOnboarding = null,
    Object? dateOfBirth = freezed,
    Object? createdAt = freezed,
    Object? headline = freezed,
    Object? bio = freezed,
    Object? skills = null,
    Object? languages = null,
    Object? interests = null,
    Object? educationJson = freezed,
    Object? linksJson = freezed,
    Object? avatarKey = freezed,
    Object? coverKey = freezed,
    Object? privacy = freezed,
    Object? trust = freezed,
  }) {
    return _then(
      _$CurrentUserDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        phone: null == phone
            ? _value.phone
            : phone // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        username: freezed == username
            ? _value.username
            : username // ignore: cast_nullable_to_non_nullable
                  as String?,
        email: freezed == email
            ? _value.email
            : email // ignore: cast_nullable_to_non_nullable
                  as String?,
        emailVerified: null == emailVerified
            ? _value.emailVerified
            : emailVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
        needsOnboarding: null == needsOnboarding
            ? _value.needsOnboarding
            : needsOnboarding // ignore: cast_nullable_to_non_nullable
                  as bool,
        dateOfBirth: freezed == dateOfBirth
            ? _value.dateOfBirth
            : dateOfBirth // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        headline: freezed == headline
            ? _value.headline
            : headline // ignore: cast_nullable_to_non_nullable
                  as String?,
        bio: freezed == bio
            ? _value.bio
            : bio // ignore: cast_nullable_to_non_nullable
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
        educationJson: freezed == educationJson
            ? _value.educationJson
            : educationJson // ignore: cast_nullable_to_non_nullable
                  as String?,
        linksJson: freezed == linksJson
            ? _value.linksJson
            : linksJson // ignore: cast_nullable_to_non_nullable
                  as String?,
        avatarKey: freezed == avatarKey
            ? _value.avatarKey
            : avatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        coverKey: freezed == coverKey
            ? _value.coverKey
            : coverKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        privacy: freezed == privacy
            ? _value.privacy
            : privacy // ignore: cast_nullable_to_non_nullable
                  as ProfilePrivacyDto?,
        trust: freezed == trust
            ? _value.trust
            : trust // ignore: cast_nullable_to_non_nullable
                  as TrustCapabilitiesDto?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$CurrentUserDtoImpl extends _CurrentUserDto {
  const _$CurrentUserDtoImpl({
    required this.id,
    required this.phone,
    this.name = '',
    this.username,
    this.email,
    @JsonKey(name: 'email_verified') this.emailVerified = false,
    @JsonKey(name: 'needs_onboarding') this.needsOnboarding = false,
    @JsonKey(name: 'date_of_birth') this.dateOfBirth,
    @JsonKey(name: 'created_at') this.createdAt,
    this.headline,
    this.bio,
    final List<String> skills = const <String>[],
    final List<String> languages = const <String>[],
    final List<String> interests = const <String>[],
    @JsonKey(name: 'education_json') this.educationJson,
    @JsonKey(name: 'links_json') this.linksJson,
    @JsonKey(name: 'avatar_key') this.avatarKey,
    @JsonKey(name: 'cover_key') this.coverKey,
    this.privacy,
    this.trust,
  }) : _skills = skills,
       _languages = languages,
       _interests = interests,
       super._();

  factory _$CurrentUserDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$CurrentUserDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String phone;
  @override
  @JsonKey()
  final String name;
  @override
  final String? username;
  @override
  final String? email;

  /// Proven by an emailed one-time code, not merely present (D-182).
  @override
  @JsonKey(name: 'email_verified')
  final bool emailVerified;
  @override
  @JsonKey(name: 'needs_onboarding')
  final bool needsOnboarding;

  /// Date-only (`YYYY-MM-DD`, D-311). Required to finish onboarding, and the only fact on the
  /// account an age-restricted event's MinAge/MaxAge can read. Nullable so an older backend, or a
  /// pre-D-311 account, still parses.
  @override
  @JsonKey(name: 'date_of_birth')
  final DateTime? dateOfBirth;

  /// When the account was created — the "member since" fact.
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  // Self-declared display fields (D-219). All optional so an older backend still parses.
  @override
  final String? headline;
  @override
  final String? bio;
  final List<String> _skills;
  @override
  @JsonKey()
  List<String> get skills {
    if (_skills is EqualUnmodifiableListView) return _skills;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_skills);
  }

  final List<String> _languages;
  @override
  @JsonKey()
  List<String> get languages {
    if (_languages is EqualUnmodifiableListView) return _languages;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_languages);
  }

  final List<String> _interests;
  @override
  @JsonKey()
  List<String> get interests {
    if (_interests is EqualUnmodifiableListView) return _interests;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_interests);
  }

  /// jsonb array of education entries. Self-declared and never proof (D-220): it was never
  /// migrated into evidence-backed membership claims.
  @override
  @JsonKey(name: 'education_json')
  final String? educationJson;
  @override
  @JsonKey(name: 'links_json')
  final String? linksJson;
  @override
  @JsonKey(name: 'avatar_key')
  final String? avatarKey;
  @override
  @JsonKey(name: 'cover_key')
  final String? coverKey;
  @override
  final ProfilePrivacyDto? privacy;

  /// Derived user trust (M7). **Nested on the wire and previously not parsed at all**, so this
  /// client had no access to `can_organize_paid` and could not tell a verified organiser from an
  /// unverified one. Web declared the same field flat and silently defaulted it to false for every
  /// user — the same failure class as D-245 and D-292, found in both clients at once.
  @override
  final TrustCapabilitiesDto? trust;

  @override
  String toString() {
    return 'CurrentUserDto(id: $id, phone: $phone, name: $name, username: $username, email: $email, emailVerified: $emailVerified, needsOnboarding: $needsOnboarding, dateOfBirth: $dateOfBirth, createdAt: $createdAt, headline: $headline, bio: $bio, skills: $skills, languages: $languages, interests: $interests, educationJson: $educationJson, linksJson: $linksJson, avatarKey: $avatarKey, coverKey: $coverKey, privacy: $privacy, trust: $trust)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$CurrentUserDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.phone, phone) || other.phone == phone) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.email, email) || other.email == email) &&
            (identical(other.emailVerified, emailVerified) ||
                other.emailVerified == emailVerified) &&
            (identical(other.needsOnboarding, needsOnboarding) ||
                other.needsOnboarding == needsOnboarding) &&
            (identical(other.dateOfBirth, dateOfBirth) ||
                other.dateOfBirth == dateOfBirth) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt) &&
            (identical(other.headline, headline) ||
                other.headline == headline) &&
            (identical(other.bio, bio) || other.bio == bio) &&
            const DeepCollectionEquality().equals(other._skills, _skills) &&
            const DeepCollectionEquality().equals(
              other._languages,
              _languages,
            ) &&
            const DeepCollectionEquality().equals(
              other._interests,
              _interests,
            ) &&
            (identical(other.educationJson, educationJson) ||
                other.educationJson == educationJson) &&
            (identical(other.linksJson, linksJson) ||
                other.linksJson == linksJson) &&
            (identical(other.avatarKey, avatarKey) ||
                other.avatarKey == avatarKey) &&
            (identical(other.coverKey, coverKey) ||
                other.coverKey == coverKey) &&
            (identical(other.privacy, privacy) || other.privacy == privacy) &&
            (identical(other.trust, trust) || other.trust == trust));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hashAll([
    runtimeType,
    id,
    phone,
    name,
    username,
    email,
    emailVerified,
    needsOnboarding,
    dateOfBirth,
    createdAt,
    headline,
    bio,
    const DeepCollectionEquality().hash(_skills),
    const DeepCollectionEquality().hash(_languages),
    const DeepCollectionEquality().hash(_interests),
    educationJson,
    linksJson,
    avatarKey,
    coverKey,
    privacy,
    trust,
  ]);

  /// Create a copy of CurrentUserDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$CurrentUserDtoImplCopyWith<_$CurrentUserDtoImpl> get copyWith =>
      __$$CurrentUserDtoImplCopyWithImpl<_$CurrentUserDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$CurrentUserDtoImplToJson(this);
  }
}

abstract class _CurrentUserDto extends CurrentUserDto {
  const factory _CurrentUserDto({
    required final String id,
    required final String phone,
    final String name,
    final String? username,
    final String? email,
    @JsonKey(name: 'email_verified') final bool emailVerified,
    @JsonKey(name: 'needs_onboarding') final bool needsOnboarding,
    @JsonKey(name: 'date_of_birth') final DateTime? dateOfBirth,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
    final String? headline,
    final String? bio,
    final List<String> skills,
    final List<String> languages,
    final List<String> interests,
    @JsonKey(name: 'education_json') final String? educationJson,
    @JsonKey(name: 'links_json') final String? linksJson,
    @JsonKey(name: 'avatar_key') final String? avatarKey,
    @JsonKey(name: 'cover_key') final String? coverKey,
    final ProfilePrivacyDto? privacy,
    final TrustCapabilitiesDto? trust,
  }) = _$CurrentUserDtoImpl;
  const _CurrentUserDto._() : super._();

  factory _CurrentUserDto.fromJson(Map<String, dynamic> json) =
      _$CurrentUserDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get phone;
  @override
  String get name;
  @override
  String? get username;
  @override
  String? get email;

  /// Proven by an emailed one-time code, not merely present (D-182).
  @override
  @JsonKey(name: 'email_verified')
  bool get emailVerified;
  @override
  @JsonKey(name: 'needs_onboarding')
  bool get needsOnboarding;

  /// Date-only (`YYYY-MM-DD`, D-311). Required to finish onboarding, and the only fact on the
  /// account an age-restricted event's MinAge/MaxAge can read. Nullable so an older backend, or a
  /// pre-D-311 account, still parses.
  @override
  @JsonKey(name: 'date_of_birth')
  DateTime? get dateOfBirth;

  /// When the account was created — the "member since" fact.
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt; // Self-declared display fields (D-219). All optional so an older backend still parses.
  @override
  String? get headline;
  @override
  String? get bio;
  @override
  List<String> get skills;
  @override
  List<String> get languages;
  @override
  List<String> get interests;

  /// jsonb array of education entries. Self-declared and never proof (D-220): it was never
  /// migrated into evidence-backed membership claims.
  @override
  @JsonKey(name: 'education_json')
  String? get educationJson;
  @override
  @JsonKey(name: 'links_json')
  String? get linksJson;
  @override
  @JsonKey(name: 'avatar_key')
  String? get avatarKey;
  @override
  @JsonKey(name: 'cover_key')
  String? get coverKey;
  @override
  ProfilePrivacyDto? get privacy;

  /// Derived user trust (M7). **Nested on the wire and previously not parsed at all**, so this
  /// client had no access to `can_organize_paid` and could not tell a verified organiser from an
  /// unverified one. Web declared the same field flat and silently defaulted it to false for every
  /// user — the same failure class as D-245 and D-292, found in both clients at once.
  @override
  TrustCapabilitiesDto? get trust;

  /// Create a copy of CurrentUserDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$CurrentUserDtoImplCopyWith<_$CurrentUserDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

TrustCapabilitiesDto _$TrustCapabilitiesDtoFromJson(Map<String, dynamic> json) {
  return _TrustCapabilitiesDto.fromJson(json);
}

/// @nodoc
mixin _$TrustCapabilitiesDto {
  String get level => throw _privateConstructorUsedError;
  @JsonKey(name: 'can_organize_free')
  bool get canOrganizeFree => throw _privateConstructorUsedError;
  @JsonKey(name: 'can_organize_paid')
  bool get canOrganizePaid => throw _privateConstructorUsedError;
  @JsonKey(name: 'can_receive_payout')
  bool get canReceivePayout => throw _privateConstructorUsedError;
  @JsonKey(name: 'identity_verified')
  bool get identityVerified => throw _privateConstructorUsedError;
  @JsonKey(name: 'bank_verified')
  bool get bankVerified => throw _privateConstructorUsedError;

  /// D-307. Closed position on absence — a missing capability must never read as permission.
  @JsonKey(name: 'can_create_public_event')
  bool get canCreatePublicEvent => throw _privateConstructorUsedError;

  /// Defaults true so an older backend does not accidentally block Private.
  @JsonKey(name: 'can_create_private_event')
  bool get canCreatePrivateEvent => throw _privateConstructorUsedError;

  /// Serializes this TrustCapabilitiesDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TrustCapabilitiesDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TrustCapabilitiesDtoCopyWith<TrustCapabilitiesDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TrustCapabilitiesDtoCopyWith<$Res> {
  factory $TrustCapabilitiesDtoCopyWith(
    TrustCapabilitiesDto value,
    $Res Function(TrustCapabilitiesDto) then,
  ) = _$TrustCapabilitiesDtoCopyWithImpl<$Res, TrustCapabilitiesDto>;
  @useResult
  $Res call({
    String level,
    @JsonKey(name: 'can_organize_free') bool canOrganizeFree,
    @JsonKey(name: 'can_organize_paid') bool canOrganizePaid,
    @JsonKey(name: 'can_receive_payout') bool canReceivePayout,
    @JsonKey(name: 'identity_verified') bool identityVerified,
    @JsonKey(name: 'bank_verified') bool bankVerified,
    @JsonKey(name: 'can_create_public_event') bool canCreatePublicEvent,
    @JsonKey(name: 'can_create_private_event') bool canCreatePrivateEvent,
  });
}

/// @nodoc
class _$TrustCapabilitiesDtoCopyWithImpl<
  $Res,
  $Val extends TrustCapabilitiesDto
>
    implements $TrustCapabilitiesDtoCopyWith<$Res> {
  _$TrustCapabilitiesDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TrustCapabilitiesDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? level = null,
    Object? canOrganizeFree = null,
    Object? canOrganizePaid = null,
    Object? canReceivePayout = null,
    Object? identityVerified = null,
    Object? bankVerified = null,
    Object? canCreatePublicEvent = null,
    Object? canCreatePrivateEvent = null,
  }) {
    return _then(
      _value.copyWith(
            level: null == level
                ? _value.level
                : level // ignore: cast_nullable_to_non_nullable
                      as String,
            canOrganizeFree: null == canOrganizeFree
                ? _value.canOrganizeFree
                : canOrganizeFree // ignore: cast_nullable_to_non_nullable
                      as bool,
            canOrganizePaid: null == canOrganizePaid
                ? _value.canOrganizePaid
                : canOrganizePaid // ignore: cast_nullable_to_non_nullable
                      as bool,
            canReceivePayout: null == canReceivePayout
                ? _value.canReceivePayout
                : canReceivePayout // ignore: cast_nullable_to_non_nullable
                      as bool,
            identityVerified: null == identityVerified
                ? _value.identityVerified
                : identityVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
            bankVerified: null == bankVerified
                ? _value.bankVerified
                : bankVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
            canCreatePublicEvent: null == canCreatePublicEvent
                ? _value.canCreatePublicEvent
                : canCreatePublicEvent // ignore: cast_nullable_to_non_nullable
                      as bool,
            canCreatePrivateEvent: null == canCreatePrivateEvent
                ? _value.canCreatePrivateEvent
                : canCreatePrivateEvent // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TrustCapabilitiesDtoImplCopyWith<$Res>
    implements $TrustCapabilitiesDtoCopyWith<$Res> {
  factory _$$TrustCapabilitiesDtoImplCopyWith(
    _$TrustCapabilitiesDtoImpl value,
    $Res Function(_$TrustCapabilitiesDtoImpl) then,
  ) = __$$TrustCapabilitiesDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String level,
    @JsonKey(name: 'can_organize_free') bool canOrganizeFree,
    @JsonKey(name: 'can_organize_paid') bool canOrganizePaid,
    @JsonKey(name: 'can_receive_payout') bool canReceivePayout,
    @JsonKey(name: 'identity_verified') bool identityVerified,
    @JsonKey(name: 'bank_verified') bool bankVerified,
    @JsonKey(name: 'can_create_public_event') bool canCreatePublicEvent,
    @JsonKey(name: 'can_create_private_event') bool canCreatePrivateEvent,
  });
}

/// @nodoc
class __$$TrustCapabilitiesDtoImplCopyWithImpl<$Res>
    extends _$TrustCapabilitiesDtoCopyWithImpl<$Res, _$TrustCapabilitiesDtoImpl>
    implements _$$TrustCapabilitiesDtoImplCopyWith<$Res> {
  __$$TrustCapabilitiesDtoImplCopyWithImpl(
    _$TrustCapabilitiesDtoImpl _value,
    $Res Function(_$TrustCapabilitiesDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TrustCapabilitiesDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? level = null,
    Object? canOrganizeFree = null,
    Object? canOrganizePaid = null,
    Object? canReceivePayout = null,
    Object? identityVerified = null,
    Object? bankVerified = null,
    Object? canCreatePublicEvent = null,
    Object? canCreatePrivateEvent = null,
  }) {
    return _then(
      _$TrustCapabilitiesDtoImpl(
        level: null == level
            ? _value.level
            : level // ignore: cast_nullable_to_non_nullable
                  as String,
        canOrganizeFree: null == canOrganizeFree
            ? _value.canOrganizeFree
            : canOrganizeFree // ignore: cast_nullable_to_non_nullable
                  as bool,
        canOrganizePaid: null == canOrganizePaid
            ? _value.canOrganizePaid
            : canOrganizePaid // ignore: cast_nullable_to_non_nullable
                  as bool,
        canReceivePayout: null == canReceivePayout
            ? _value.canReceivePayout
            : canReceivePayout // ignore: cast_nullable_to_non_nullable
                  as bool,
        identityVerified: null == identityVerified
            ? _value.identityVerified
            : identityVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
        bankVerified: null == bankVerified
            ? _value.bankVerified
            : bankVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
        canCreatePublicEvent: null == canCreatePublicEvent
            ? _value.canCreatePublicEvent
            : canCreatePublicEvent // ignore: cast_nullable_to_non_nullable
                  as bool,
        canCreatePrivateEvent: null == canCreatePrivateEvent
            ? _value.canCreatePrivateEvent
            : canCreatePrivateEvent // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TrustCapabilitiesDtoImpl extends _TrustCapabilitiesDto {
  const _$TrustCapabilitiesDtoImpl({
    this.level = 'L1',
    @JsonKey(name: 'can_organize_free') this.canOrganizeFree = true,
    @JsonKey(name: 'can_organize_paid') this.canOrganizePaid = false,
    @JsonKey(name: 'can_receive_payout') this.canReceivePayout = false,
    @JsonKey(name: 'identity_verified') this.identityVerified = false,
    @JsonKey(name: 'bank_verified') this.bankVerified = false,
    @JsonKey(name: 'can_create_public_event') this.canCreatePublicEvent = false,
    @JsonKey(name: 'can_create_private_event')
    this.canCreatePrivateEvent = true,
  }) : super._();

  factory _$TrustCapabilitiesDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TrustCapabilitiesDtoImplFromJson(json);

  @override
  @JsonKey()
  final String level;
  @override
  @JsonKey(name: 'can_organize_free')
  final bool canOrganizeFree;
  @override
  @JsonKey(name: 'can_organize_paid')
  final bool canOrganizePaid;
  @override
  @JsonKey(name: 'can_receive_payout')
  final bool canReceivePayout;
  @override
  @JsonKey(name: 'identity_verified')
  final bool identityVerified;
  @override
  @JsonKey(name: 'bank_verified')
  final bool bankVerified;

  /// D-307. Closed position on absence — a missing capability must never read as permission.
  @override
  @JsonKey(name: 'can_create_public_event')
  final bool canCreatePublicEvent;

  /// Defaults true so an older backend does not accidentally block Private.
  @override
  @JsonKey(name: 'can_create_private_event')
  final bool canCreatePrivateEvent;

  @override
  String toString() {
    return 'TrustCapabilitiesDto(level: $level, canOrganizeFree: $canOrganizeFree, canOrganizePaid: $canOrganizePaid, canReceivePayout: $canReceivePayout, identityVerified: $identityVerified, bankVerified: $bankVerified, canCreatePublicEvent: $canCreatePublicEvent, canCreatePrivateEvent: $canCreatePrivateEvent)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TrustCapabilitiesDtoImpl &&
            (identical(other.level, level) || other.level == level) &&
            (identical(other.canOrganizeFree, canOrganizeFree) ||
                other.canOrganizeFree == canOrganizeFree) &&
            (identical(other.canOrganizePaid, canOrganizePaid) ||
                other.canOrganizePaid == canOrganizePaid) &&
            (identical(other.canReceivePayout, canReceivePayout) ||
                other.canReceivePayout == canReceivePayout) &&
            (identical(other.identityVerified, identityVerified) ||
                other.identityVerified == identityVerified) &&
            (identical(other.bankVerified, bankVerified) ||
                other.bankVerified == bankVerified) &&
            (identical(other.canCreatePublicEvent, canCreatePublicEvent) ||
                other.canCreatePublicEvent == canCreatePublicEvent) &&
            (identical(other.canCreatePrivateEvent, canCreatePrivateEvent) ||
                other.canCreatePrivateEvent == canCreatePrivateEvent));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    level,
    canOrganizeFree,
    canOrganizePaid,
    canReceivePayout,
    identityVerified,
    bankVerified,
    canCreatePublicEvent,
    canCreatePrivateEvent,
  );

  /// Create a copy of TrustCapabilitiesDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TrustCapabilitiesDtoImplCopyWith<_$TrustCapabilitiesDtoImpl>
  get copyWith =>
      __$$TrustCapabilitiesDtoImplCopyWithImpl<_$TrustCapabilitiesDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$TrustCapabilitiesDtoImplToJson(this);
  }
}

abstract class _TrustCapabilitiesDto extends TrustCapabilitiesDto {
  const factory _TrustCapabilitiesDto({
    final String level,
    @JsonKey(name: 'can_organize_free') final bool canOrganizeFree,
    @JsonKey(name: 'can_organize_paid') final bool canOrganizePaid,
    @JsonKey(name: 'can_receive_payout') final bool canReceivePayout,
    @JsonKey(name: 'identity_verified') final bool identityVerified,
    @JsonKey(name: 'bank_verified') final bool bankVerified,
    @JsonKey(name: 'can_create_public_event') final bool canCreatePublicEvent,
    @JsonKey(name: 'can_create_private_event') final bool canCreatePrivateEvent,
  }) = _$TrustCapabilitiesDtoImpl;
  const _TrustCapabilitiesDto._() : super._();

  factory _TrustCapabilitiesDto.fromJson(Map<String, dynamic> json) =
      _$TrustCapabilitiesDtoImpl.fromJson;

  @override
  String get level;
  @override
  @JsonKey(name: 'can_organize_free')
  bool get canOrganizeFree;
  @override
  @JsonKey(name: 'can_organize_paid')
  bool get canOrganizePaid;
  @override
  @JsonKey(name: 'can_receive_payout')
  bool get canReceivePayout;
  @override
  @JsonKey(name: 'identity_verified')
  bool get identityVerified;
  @override
  @JsonKey(name: 'bank_verified')
  bool get bankVerified;

  /// D-307. Closed position on absence — a missing capability must never read as permission.
  @override
  @JsonKey(name: 'can_create_public_event')
  bool get canCreatePublicEvent;

  /// Defaults true so an older backend does not accidentally block Private.
  @override
  @JsonKey(name: 'can_create_private_event')
  bool get canCreatePrivateEvent;

  /// Create a copy of TrustCapabilitiesDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TrustCapabilitiesDtoImplCopyWith<_$TrustCapabilitiesDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

ProfilePrivacyDto _$ProfilePrivacyDtoFromJson(Map<String, dynamic> json) {
  return _ProfilePrivacyDto.fromJson(json);
}

/// @nodoc
mixin _$ProfilePrivacyDto {
  @JsonKey(name: 'profile_public')
  bool get profilePublic => throw _privateConstructorUsedError;
  @JsonKey(name: 'show_attended')
  bool get showAttended => throw _privateConstructorUsedError;
  @JsonKey(name: 'show_certificates')
  bool get showCertificates => throw _privateConstructorUsedError;
  @JsonKey(name: 'show_allies')
  bool get showAllies => throw _privateConstructorUsedError;

  /// Per-section four-tier visibility (D-221). Absent on an older backend, in which case
  /// [ProfilePrivacy.tierFor] derives the tier from the booleans above.
  Map<String, String> get sections => throw _privateConstructorUsedError;

  /// Serializes this ProfilePrivacyDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ProfilePrivacyDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ProfilePrivacyDtoCopyWith<ProfilePrivacyDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ProfilePrivacyDtoCopyWith<$Res> {
  factory $ProfilePrivacyDtoCopyWith(
    ProfilePrivacyDto value,
    $Res Function(ProfilePrivacyDto) then,
  ) = _$ProfilePrivacyDtoCopyWithImpl<$Res, ProfilePrivacyDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'profile_public') bool profilePublic,
    @JsonKey(name: 'show_attended') bool showAttended,
    @JsonKey(name: 'show_certificates') bool showCertificates,
    @JsonKey(name: 'show_allies') bool showAllies,
    Map<String, String> sections,
  });
}

/// @nodoc
class _$ProfilePrivacyDtoCopyWithImpl<$Res, $Val extends ProfilePrivacyDto>
    implements $ProfilePrivacyDtoCopyWith<$Res> {
  _$ProfilePrivacyDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ProfilePrivacyDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? profilePublic = null,
    Object? showAttended = null,
    Object? showCertificates = null,
    Object? showAllies = null,
    Object? sections = null,
  }) {
    return _then(
      _value.copyWith(
            profilePublic: null == profilePublic
                ? _value.profilePublic
                : profilePublic // ignore: cast_nullable_to_non_nullable
                      as bool,
            showAttended: null == showAttended
                ? _value.showAttended
                : showAttended // ignore: cast_nullable_to_non_nullable
                      as bool,
            showCertificates: null == showCertificates
                ? _value.showCertificates
                : showCertificates // ignore: cast_nullable_to_non_nullable
                      as bool,
            showAllies: null == showAllies
                ? _value.showAllies
                : showAllies // ignore: cast_nullable_to_non_nullable
                      as bool,
            sections: null == sections
                ? _value.sections
                : sections // ignore: cast_nullable_to_non_nullable
                      as Map<String, String>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ProfilePrivacyDtoImplCopyWith<$Res>
    implements $ProfilePrivacyDtoCopyWith<$Res> {
  factory _$$ProfilePrivacyDtoImplCopyWith(
    _$ProfilePrivacyDtoImpl value,
    $Res Function(_$ProfilePrivacyDtoImpl) then,
  ) = __$$ProfilePrivacyDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'profile_public') bool profilePublic,
    @JsonKey(name: 'show_attended') bool showAttended,
    @JsonKey(name: 'show_certificates') bool showCertificates,
    @JsonKey(name: 'show_allies') bool showAllies,
    Map<String, String> sections,
  });
}

/// @nodoc
class __$$ProfilePrivacyDtoImplCopyWithImpl<$Res>
    extends _$ProfilePrivacyDtoCopyWithImpl<$Res, _$ProfilePrivacyDtoImpl>
    implements _$$ProfilePrivacyDtoImplCopyWith<$Res> {
  __$$ProfilePrivacyDtoImplCopyWithImpl(
    _$ProfilePrivacyDtoImpl _value,
    $Res Function(_$ProfilePrivacyDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ProfilePrivacyDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? profilePublic = null,
    Object? showAttended = null,
    Object? showCertificates = null,
    Object? showAllies = null,
    Object? sections = null,
  }) {
    return _then(
      _$ProfilePrivacyDtoImpl(
        profilePublic: null == profilePublic
            ? _value.profilePublic
            : profilePublic // ignore: cast_nullable_to_non_nullable
                  as bool,
        showAttended: null == showAttended
            ? _value.showAttended
            : showAttended // ignore: cast_nullable_to_non_nullable
                  as bool,
        showCertificates: null == showCertificates
            ? _value.showCertificates
            : showCertificates // ignore: cast_nullable_to_non_nullable
                  as bool,
        showAllies: null == showAllies
            ? _value.showAllies
            : showAllies // ignore: cast_nullable_to_non_nullable
                  as bool,
        sections: null == sections
            ? _value._sections
            : sections // ignore: cast_nullable_to_non_nullable
                  as Map<String, String>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ProfilePrivacyDtoImpl extends _ProfilePrivacyDto {
  const _$ProfilePrivacyDtoImpl({
    @JsonKey(name: 'profile_public') this.profilePublic = true,
    @JsonKey(name: 'show_attended') this.showAttended = false,
    @JsonKey(name: 'show_certificates') this.showCertificates = true,
    @JsonKey(name: 'show_allies') this.showAllies = true,
    final Map<String, String> sections = const <String, String>{},
  }) : _sections = sections,
       super._();

  factory _$ProfilePrivacyDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ProfilePrivacyDtoImplFromJson(json);

  @override
  @JsonKey(name: 'profile_public')
  final bool profilePublic;
  @override
  @JsonKey(name: 'show_attended')
  final bool showAttended;
  @override
  @JsonKey(name: 'show_certificates')
  final bool showCertificates;
  @override
  @JsonKey(name: 'show_allies')
  final bool showAllies;

  /// Per-section four-tier visibility (D-221). Absent on an older backend, in which case
  /// [ProfilePrivacy.tierFor] derives the tier from the booleans above.
  final Map<String, String> _sections;

  /// Per-section four-tier visibility (D-221). Absent on an older backend, in which case
  /// [ProfilePrivacy.tierFor] derives the tier from the booleans above.
  @override
  @JsonKey()
  Map<String, String> get sections {
    if (_sections is EqualUnmodifiableMapView) return _sections;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableMapView(_sections);
  }

  @override
  String toString() {
    return 'ProfilePrivacyDto(profilePublic: $profilePublic, showAttended: $showAttended, showCertificates: $showCertificates, showAllies: $showAllies, sections: $sections)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ProfilePrivacyDtoImpl &&
            (identical(other.profilePublic, profilePublic) ||
                other.profilePublic == profilePublic) &&
            (identical(other.showAttended, showAttended) ||
                other.showAttended == showAttended) &&
            (identical(other.showCertificates, showCertificates) ||
                other.showCertificates == showCertificates) &&
            (identical(other.showAllies, showAllies) ||
                other.showAllies == showAllies) &&
            const DeepCollectionEquality().equals(other._sections, _sections));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    profilePublic,
    showAttended,
    showCertificates,
    showAllies,
    const DeepCollectionEquality().hash(_sections),
  );

  /// Create a copy of ProfilePrivacyDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ProfilePrivacyDtoImplCopyWith<_$ProfilePrivacyDtoImpl> get copyWith =>
      __$$ProfilePrivacyDtoImplCopyWithImpl<_$ProfilePrivacyDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$ProfilePrivacyDtoImplToJson(this);
  }
}

abstract class _ProfilePrivacyDto extends ProfilePrivacyDto {
  const factory _ProfilePrivacyDto({
    @JsonKey(name: 'profile_public') final bool profilePublic,
    @JsonKey(name: 'show_attended') final bool showAttended,
    @JsonKey(name: 'show_certificates') final bool showCertificates,
    @JsonKey(name: 'show_allies') final bool showAllies,
    final Map<String, String> sections,
  }) = _$ProfilePrivacyDtoImpl;
  const _ProfilePrivacyDto._() : super._();

  factory _ProfilePrivacyDto.fromJson(Map<String, dynamic> json) =
      _$ProfilePrivacyDtoImpl.fromJson;

  @override
  @JsonKey(name: 'profile_public')
  bool get profilePublic;
  @override
  @JsonKey(name: 'show_attended')
  bool get showAttended;
  @override
  @JsonKey(name: 'show_certificates')
  bool get showCertificates;
  @override
  @JsonKey(name: 'show_allies')
  bool get showAllies;

  /// Per-section four-tier visibility (D-221). Absent on an older backend, in which case
  /// [ProfilePrivacy.tierFor] derives the tier from the booleans above.
  @override
  Map<String, String> get sections;

  /// Create a copy of ProfilePrivacyDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ProfilePrivacyDtoImplCopyWith<_$ProfilePrivacyDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
