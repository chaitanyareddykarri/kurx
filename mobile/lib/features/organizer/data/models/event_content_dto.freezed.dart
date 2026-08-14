// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'event_content_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

SpeakerDto _$SpeakerDtoFromJson(Map<String, dynamic> json) {
  return _SpeakerDto.fromJson(json);
}

/// @nodoc
mixin _$SpeakerDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_id')
  String? get orgId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get bio => throw _privateConstructorUsedError;

  /// A storage key, not a URL — needs presigning before it renders.
  @JsonKey(name: 'photo_key')
  String? get photoKey => throw _privateConstructorUsedError;
  String? get company => throw _privateConstructorUsedError;
  String? get role => throw _privateConstructorUsedError;
  @JsonKey(name: 'social_links_json')
  String? get socialLinksJson => throw _privateConstructorUsedError; // Null for the common case (curated content, no account) — a linked speaker can Connect/View
  // Profile (D-20x).
  @JsonKey(name: 'user_id')
  String? get userId => throw _privateConstructorUsedError;
  String? get username => throw _privateConstructorUsedError;
  @JsonKey(name: 'avatar_key')
  String? get avatarKey => throw _privateConstructorUsedError;

  /// Serializes this SpeakerDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of SpeakerDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $SpeakerDtoCopyWith<SpeakerDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $SpeakerDtoCopyWith<$Res> {
  factory $SpeakerDtoCopyWith(
    SpeakerDto value,
    $Res Function(SpeakerDto) then,
  ) = _$SpeakerDtoCopyWithImpl<$Res, SpeakerDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String? orgId,
    String name,
    String? bio,
    @JsonKey(name: 'photo_key') String? photoKey,
    String? company,
    String? role,
    @JsonKey(name: 'social_links_json') String? socialLinksJson,
    @JsonKey(name: 'user_id') String? userId,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
  });
}

/// @nodoc
class _$SpeakerDtoCopyWithImpl<$Res, $Val extends SpeakerDto>
    implements $SpeakerDtoCopyWith<$Res> {
  _$SpeakerDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of SpeakerDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = freezed,
    Object? name = null,
    Object? bio = freezed,
    Object? photoKey = freezed,
    Object? company = freezed,
    Object? role = freezed,
    Object? socialLinksJson = freezed,
    Object? userId = freezed,
    Object? username = freezed,
    Object? avatarKey = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            orgId: freezed == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String?,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            bio: freezed == bio
                ? _value.bio
                : bio // ignore: cast_nullable_to_non_nullable
                      as String?,
            photoKey: freezed == photoKey
                ? _value.photoKey
                : photoKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            company: freezed == company
                ? _value.company
                : company // ignore: cast_nullable_to_non_nullable
                      as String?,
            role: freezed == role
                ? _value.role
                : role // ignore: cast_nullable_to_non_nullable
                      as String?,
            socialLinksJson: freezed == socialLinksJson
                ? _value.socialLinksJson
                : socialLinksJson // ignore: cast_nullable_to_non_nullable
                      as String?,
            userId: freezed == userId
                ? _value.userId
                : userId // ignore: cast_nullable_to_non_nullable
                      as String?,
            username: freezed == username
                ? _value.username
                : username // ignore: cast_nullable_to_non_nullable
                      as String?,
            avatarKey: freezed == avatarKey
                ? _value.avatarKey
                : avatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$SpeakerDtoImplCopyWith<$Res>
    implements $SpeakerDtoCopyWith<$Res> {
  factory _$$SpeakerDtoImplCopyWith(
    _$SpeakerDtoImpl value,
    $Res Function(_$SpeakerDtoImpl) then,
  ) = __$$SpeakerDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String? orgId,
    String name,
    String? bio,
    @JsonKey(name: 'photo_key') String? photoKey,
    String? company,
    String? role,
    @JsonKey(name: 'social_links_json') String? socialLinksJson,
    @JsonKey(name: 'user_id') String? userId,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
  });
}

/// @nodoc
class __$$SpeakerDtoImplCopyWithImpl<$Res>
    extends _$SpeakerDtoCopyWithImpl<$Res, _$SpeakerDtoImpl>
    implements _$$SpeakerDtoImplCopyWith<$Res> {
  __$$SpeakerDtoImplCopyWithImpl(
    _$SpeakerDtoImpl _value,
    $Res Function(_$SpeakerDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of SpeakerDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = freezed,
    Object? name = null,
    Object? bio = freezed,
    Object? photoKey = freezed,
    Object? company = freezed,
    Object? role = freezed,
    Object? socialLinksJson = freezed,
    Object? userId = freezed,
    Object? username = freezed,
    Object? avatarKey = freezed,
  }) {
    return _then(
      _$SpeakerDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        orgId: freezed == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String?,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        bio: freezed == bio
            ? _value.bio
            : bio // ignore: cast_nullable_to_non_nullable
                  as String?,
        photoKey: freezed == photoKey
            ? _value.photoKey
            : photoKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        company: freezed == company
            ? _value.company
            : company // ignore: cast_nullable_to_non_nullable
                  as String?,
        role: freezed == role
            ? _value.role
            : role // ignore: cast_nullable_to_non_nullable
                  as String?,
        socialLinksJson: freezed == socialLinksJson
            ? _value.socialLinksJson
            : socialLinksJson // ignore: cast_nullable_to_non_nullable
                  as String?,
        userId: freezed == userId
            ? _value.userId
            : userId // ignore: cast_nullable_to_non_nullable
                  as String?,
        username: freezed == username
            ? _value.username
            : username // ignore: cast_nullable_to_non_nullable
                  as String?,
        avatarKey: freezed == avatarKey
            ? _value.avatarKey
            : avatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$SpeakerDtoImpl implements _SpeakerDto {
  const _$SpeakerDtoImpl({
    required this.id,
    @JsonKey(name: 'org_id') this.orgId,
    required this.name,
    this.bio,
    @JsonKey(name: 'photo_key') this.photoKey,
    this.company,
    this.role,
    @JsonKey(name: 'social_links_json') this.socialLinksJson,
    @JsonKey(name: 'user_id') this.userId,
    this.username,
    @JsonKey(name: 'avatar_key') this.avatarKey,
  });

  factory _$SpeakerDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$SpeakerDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'org_id')
  final String? orgId;
  @override
  final String name;
  @override
  final String? bio;

  /// A storage key, not a URL — needs presigning before it renders.
  @override
  @JsonKey(name: 'photo_key')
  final String? photoKey;
  @override
  final String? company;
  @override
  final String? role;
  @override
  @JsonKey(name: 'social_links_json')
  final String? socialLinksJson;
  // Null for the common case (curated content, no account) — a linked speaker can Connect/View
  // Profile (D-20x).
  @override
  @JsonKey(name: 'user_id')
  final String? userId;
  @override
  final String? username;
  @override
  @JsonKey(name: 'avatar_key')
  final String? avatarKey;

  @override
  String toString() {
    return 'SpeakerDto(id: $id, orgId: $orgId, name: $name, bio: $bio, photoKey: $photoKey, company: $company, role: $role, socialLinksJson: $socialLinksJson, userId: $userId, username: $username, avatarKey: $avatarKey)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$SpeakerDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.bio, bio) || other.bio == bio) &&
            (identical(other.photoKey, photoKey) ||
                other.photoKey == photoKey) &&
            (identical(other.company, company) || other.company == company) &&
            (identical(other.role, role) || other.role == role) &&
            (identical(other.socialLinksJson, socialLinksJson) ||
                other.socialLinksJson == socialLinksJson) &&
            (identical(other.userId, userId) || other.userId == userId) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.avatarKey, avatarKey) ||
                other.avatarKey == avatarKey));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    orgId,
    name,
    bio,
    photoKey,
    company,
    role,
    socialLinksJson,
    userId,
    username,
    avatarKey,
  );

  /// Create a copy of SpeakerDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$SpeakerDtoImplCopyWith<_$SpeakerDtoImpl> get copyWith =>
      __$$SpeakerDtoImplCopyWithImpl<_$SpeakerDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$SpeakerDtoImplToJson(this);
  }
}

abstract class _SpeakerDto implements SpeakerDto {
  const factory _SpeakerDto({
    required final String id,
    @JsonKey(name: 'org_id') final String? orgId,
    required final String name,
    final String? bio,
    @JsonKey(name: 'photo_key') final String? photoKey,
    final String? company,
    final String? role,
    @JsonKey(name: 'social_links_json') final String? socialLinksJson,
    @JsonKey(name: 'user_id') final String? userId,
    final String? username,
    @JsonKey(name: 'avatar_key') final String? avatarKey,
  }) = _$SpeakerDtoImpl;

  factory _SpeakerDto.fromJson(Map<String, dynamic> json) =
      _$SpeakerDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'org_id')
  String? get orgId;
  @override
  String get name;
  @override
  String? get bio;

  /// A storage key, not a URL — needs presigning before it renders.
  @override
  @JsonKey(name: 'photo_key')
  String? get photoKey;
  @override
  String? get company;
  @override
  String? get role;
  @override
  @JsonKey(name: 'social_links_json')
  String? get socialLinksJson; // Null for the common case (curated content, no account) — a linked speaker can Connect/View
  // Profile (D-20x).
  @override
  @JsonKey(name: 'user_id')
  String? get userId;
  @override
  String? get username;
  @override
  @JsonKey(name: 'avatar_key')
  String? get avatarKey;

  /// Create a copy of SpeakerDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$SpeakerDtoImplCopyWith<_$SpeakerDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

SponsorDto _$SponsorDtoFromJson(Map<String, dynamic> json) {
  return _SponsorDto.fromJson(json);
}

/// @nodoc
mixin _$SponsorDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_id')
  String? get orgId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  @JsonKey(name: 'logo_key')
  String? get logoKey => throw _privateConstructorUsedError;
  String? get website => throw _privateConstructorUsedError;

  /// Lowercased server-side (`title`/`gold`/`silver`/`bronze`/`partner`).
  String? get tier => throw _privateConstructorUsedError;
  int? get priority => throw _privateConstructorUsedError;

  /// Serializes this SponsorDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of SponsorDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $SponsorDtoCopyWith<SponsorDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $SponsorDtoCopyWith<$Res> {
  factory $SponsorDtoCopyWith(
    SponsorDto value,
    $Res Function(SponsorDto) then,
  ) = _$SponsorDtoCopyWithImpl<$Res, SponsorDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String? orgId,
    String name,
    @JsonKey(name: 'logo_key') String? logoKey,
    String? website,
    String? tier,
    int? priority,
  });
}

/// @nodoc
class _$SponsorDtoCopyWithImpl<$Res, $Val extends SponsorDto>
    implements $SponsorDtoCopyWith<$Res> {
  _$SponsorDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of SponsorDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = freezed,
    Object? name = null,
    Object? logoKey = freezed,
    Object? website = freezed,
    Object? tier = freezed,
    Object? priority = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            orgId: freezed == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String?,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            logoKey: freezed == logoKey
                ? _value.logoKey
                : logoKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            website: freezed == website
                ? _value.website
                : website // ignore: cast_nullable_to_non_nullable
                      as String?,
            tier: freezed == tier
                ? _value.tier
                : tier // ignore: cast_nullable_to_non_nullable
                      as String?,
            priority: freezed == priority
                ? _value.priority
                : priority // ignore: cast_nullable_to_non_nullable
                      as int?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$SponsorDtoImplCopyWith<$Res>
    implements $SponsorDtoCopyWith<$Res> {
  factory _$$SponsorDtoImplCopyWith(
    _$SponsorDtoImpl value,
    $Res Function(_$SponsorDtoImpl) then,
  ) = __$$SponsorDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String? orgId,
    String name,
    @JsonKey(name: 'logo_key') String? logoKey,
    String? website,
    String? tier,
    int? priority,
  });
}

/// @nodoc
class __$$SponsorDtoImplCopyWithImpl<$Res>
    extends _$SponsorDtoCopyWithImpl<$Res, _$SponsorDtoImpl>
    implements _$$SponsorDtoImplCopyWith<$Res> {
  __$$SponsorDtoImplCopyWithImpl(
    _$SponsorDtoImpl _value,
    $Res Function(_$SponsorDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of SponsorDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = freezed,
    Object? name = null,
    Object? logoKey = freezed,
    Object? website = freezed,
    Object? tier = freezed,
    Object? priority = freezed,
  }) {
    return _then(
      _$SponsorDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        orgId: freezed == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String?,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        logoKey: freezed == logoKey
            ? _value.logoKey
            : logoKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        website: freezed == website
            ? _value.website
            : website // ignore: cast_nullable_to_non_nullable
                  as String?,
        tier: freezed == tier
            ? _value.tier
            : tier // ignore: cast_nullable_to_non_nullable
                  as String?,
        priority: freezed == priority
            ? _value.priority
            : priority // ignore: cast_nullable_to_non_nullable
                  as int?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$SponsorDtoImpl implements _SponsorDto {
  const _$SponsorDtoImpl({
    required this.id,
    @JsonKey(name: 'org_id') this.orgId,
    required this.name,
    @JsonKey(name: 'logo_key') this.logoKey,
    this.website,
    this.tier,
    this.priority,
  });

  factory _$SponsorDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$SponsorDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'org_id')
  final String? orgId;
  @override
  final String name;
  @override
  @JsonKey(name: 'logo_key')
  final String? logoKey;
  @override
  final String? website;

  /// Lowercased server-side (`title`/`gold`/`silver`/`bronze`/`partner`).
  @override
  final String? tier;
  @override
  final int? priority;

  @override
  String toString() {
    return 'SponsorDto(id: $id, orgId: $orgId, name: $name, logoKey: $logoKey, website: $website, tier: $tier, priority: $priority)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$SponsorDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.logoKey, logoKey) || other.logoKey == logoKey) &&
            (identical(other.website, website) || other.website == website) &&
            (identical(other.tier, tier) || other.tier == tier) &&
            (identical(other.priority, priority) ||
                other.priority == priority));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    orgId,
    name,
    logoKey,
    website,
    tier,
    priority,
  );

  /// Create a copy of SponsorDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$SponsorDtoImplCopyWith<_$SponsorDtoImpl> get copyWith =>
      __$$SponsorDtoImplCopyWithImpl<_$SponsorDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$SponsorDtoImplToJson(this);
  }
}

abstract class _SponsorDto implements SponsorDto {
  const factory _SponsorDto({
    required final String id,
    @JsonKey(name: 'org_id') final String? orgId,
    required final String name,
    @JsonKey(name: 'logo_key') final String? logoKey,
    final String? website,
    final String? tier,
    final int? priority,
  }) = _$SponsorDtoImpl;

  factory _SponsorDto.fromJson(Map<String, dynamic> json) =
      _$SponsorDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'org_id')
  String? get orgId;
  @override
  String get name;
  @override
  @JsonKey(name: 'logo_key')
  String? get logoKey;
  @override
  String? get website;

  /// Lowercased server-side (`title`/`gold`/`silver`/`bronze`/`partner`).
  @override
  String? get tier;
  @override
  int? get priority;

  /// Create a copy of SponsorDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$SponsorDtoImplCopyWith<_$SponsorDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

VenueDto _$VenueDtoFromJson(Map<String, dynamic> json) {
  return _VenueDto.fromJson(json);
}

/// @nodoc
mixin _$VenueDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_id')
  String? get orgId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get address => throw _privateConstructorUsedError;
  String? get city => throw _privateConstructorUsedError;
  double? get lat => throw _privateConstructorUsedError;
  double? get lng => throw _privateConstructorUsedError;
  @JsonKey(name: 'google_maps_url')
  String? get googleMapsUrl => throw _privateConstructorUsedError;
  int? get capacity => throw _privateConstructorUsedError;
  @JsonKey(name: 'has_parking')
  bool get hasParking => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_accessible')
  bool get isAccessible => throw _privateConstructorUsedError;
  String? get notes => throw _privateConstructorUsedError;
  @JsonKey(name: 'image_keys')
  List<String> get imageKeys => throw _privateConstructorUsedError;

  /// Serializes this VenueDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of VenueDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $VenueDtoCopyWith<VenueDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $VenueDtoCopyWith<$Res> {
  factory $VenueDtoCopyWith(VenueDto value, $Res Function(VenueDto) then) =
      _$VenueDtoCopyWithImpl<$Res, VenueDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String? orgId,
    String name,
    String? address,
    String? city,
    double? lat,
    double? lng,
    @JsonKey(name: 'google_maps_url') String? googleMapsUrl,
    int? capacity,
    @JsonKey(name: 'has_parking') bool hasParking,
    @JsonKey(name: 'is_accessible') bool isAccessible,
    String? notes,
    @JsonKey(name: 'image_keys') List<String> imageKeys,
  });
}

/// @nodoc
class _$VenueDtoCopyWithImpl<$Res, $Val extends VenueDto>
    implements $VenueDtoCopyWith<$Res> {
  _$VenueDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of VenueDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = freezed,
    Object? name = null,
    Object? address = freezed,
    Object? city = freezed,
    Object? lat = freezed,
    Object? lng = freezed,
    Object? googleMapsUrl = freezed,
    Object? capacity = freezed,
    Object? hasParking = null,
    Object? isAccessible = null,
    Object? notes = freezed,
    Object? imageKeys = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            orgId: freezed == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String?,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            address: freezed == address
                ? _value.address
                : address // ignore: cast_nullable_to_non_nullable
                      as String?,
            city: freezed == city
                ? _value.city
                : city // ignore: cast_nullable_to_non_nullable
                      as String?,
            lat: freezed == lat
                ? _value.lat
                : lat // ignore: cast_nullable_to_non_nullable
                      as double?,
            lng: freezed == lng
                ? _value.lng
                : lng // ignore: cast_nullable_to_non_nullable
                      as double?,
            googleMapsUrl: freezed == googleMapsUrl
                ? _value.googleMapsUrl
                : googleMapsUrl // ignore: cast_nullable_to_non_nullable
                      as String?,
            capacity: freezed == capacity
                ? _value.capacity
                : capacity // ignore: cast_nullable_to_non_nullable
                      as int?,
            hasParking: null == hasParking
                ? _value.hasParking
                : hasParking // ignore: cast_nullable_to_non_nullable
                      as bool,
            isAccessible: null == isAccessible
                ? _value.isAccessible
                : isAccessible // ignore: cast_nullable_to_non_nullable
                      as bool,
            notes: freezed == notes
                ? _value.notes
                : notes // ignore: cast_nullable_to_non_nullable
                      as String?,
            imageKeys: null == imageKeys
                ? _value.imageKeys
                : imageKeys // ignore: cast_nullable_to_non_nullable
                      as List<String>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$VenueDtoImplCopyWith<$Res>
    implements $VenueDtoCopyWith<$Res> {
  factory _$$VenueDtoImplCopyWith(
    _$VenueDtoImpl value,
    $Res Function(_$VenueDtoImpl) then,
  ) = __$$VenueDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String? orgId,
    String name,
    String? address,
    String? city,
    double? lat,
    double? lng,
    @JsonKey(name: 'google_maps_url') String? googleMapsUrl,
    int? capacity,
    @JsonKey(name: 'has_parking') bool hasParking,
    @JsonKey(name: 'is_accessible') bool isAccessible,
    String? notes,
    @JsonKey(name: 'image_keys') List<String> imageKeys,
  });
}

/// @nodoc
class __$$VenueDtoImplCopyWithImpl<$Res>
    extends _$VenueDtoCopyWithImpl<$Res, _$VenueDtoImpl>
    implements _$$VenueDtoImplCopyWith<$Res> {
  __$$VenueDtoImplCopyWithImpl(
    _$VenueDtoImpl _value,
    $Res Function(_$VenueDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of VenueDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = freezed,
    Object? name = null,
    Object? address = freezed,
    Object? city = freezed,
    Object? lat = freezed,
    Object? lng = freezed,
    Object? googleMapsUrl = freezed,
    Object? capacity = freezed,
    Object? hasParking = null,
    Object? isAccessible = null,
    Object? notes = freezed,
    Object? imageKeys = null,
  }) {
    return _then(
      _$VenueDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        orgId: freezed == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String?,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        address: freezed == address
            ? _value.address
            : address // ignore: cast_nullable_to_non_nullable
                  as String?,
        city: freezed == city
            ? _value.city
            : city // ignore: cast_nullable_to_non_nullable
                  as String?,
        lat: freezed == lat
            ? _value.lat
            : lat // ignore: cast_nullable_to_non_nullable
                  as double?,
        lng: freezed == lng
            ? _value.lng
            : lng // ignore: cast_nullable_to_non_nullable
                  as double?,
        googleMapsUrl: freezed == googleMapsUrl
            ? _value.googleMapsUrl
            : googleMapsUrl // ignore: cast_nullable_to_non_nullable
                  as String?,
        capacity: freezed == capacity
            ? _value.capacity
            : capacity // ignore: cast_nullable_to_non_nullable
                  as int?,
        hasParking: null == hasParking
            ? _value.hasParking
            : hasParking // ignore: cast_nullable_to_non_nullable
                  as bool,
        isAccessible: null == isAccessible
            ? _value.isAccessible
            : isAccessible // ignore: cast_nullable_to_non_nullable
                  as bool,
        notes: freezed == notes
            ? _value.notes
            : notes // ignore: cast_nullable_to_non_nullable
                  as String?,
        imageKeys: null == imageKeys
            ? _value._imageKeys
            : imageKeys // ignore: cast_nullable_to_non_nullable
                  as List<String>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$VenueDtoImpl implements _VenueDto {
  const _$VenueDtoImpl({
    required this.id,
    @JsonKey(name: 'org_id') this.orgId,
    required this.name,
    this.address,
    this.city,
    this.lat,
    this.lng,
    @JsonKey(name: 'google_maps_url') this.googleMapsUrl,
    this.capacity,
    @JsonKey(name: 'has_parking') this.hasParking = false,
    @JsonKey(name: 'is_accessible') this.isAccessible = false,
    this.notes,
    @JsonKey(name: 'image_keys')
    final List<String> imageKeys = const <String>[],
  }) : _imageKeys = imageKeys;

  factory _$VenueDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$VenueDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'org_id')
  final String? orgId;
  @override
  final String name;
  @override
  final String? address;
  @override
  final String? city;
  @override
  final double? lat;
  @override
  final double? lng;
  @override
  @JsonKey(name: 'google_maps_url')
  final String? googleMapsUrl;
  @override
  final int? capacity;
  @override
  @JsonKey(name: 'has_parking')
  final bool hasParking;
  @override
  @JsonKey(name: 'is_accessible')
  final bool isAccessible;
  @override
  final String? notes;
  final List<String> _imageKeys;
  @override
  @JsonKey(name: 'image_keys')
  List<String> get imageKeys {
    if (_imageKeys is EqualUnmodifiableListView) return _imageKeys;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_imageKeys);
  }

  @override
  String toString() {
    return 'VenueDto(id: $id, orgId: $orgId, name: $name, address: $address, city: $city, lat: $lat, lng: $lng, googleMapsUrl: $googleMapsUrl, capacity: $capacity, hasParking: $hasParking, isAccessible: $isAccessible, notes: $notes, imageKeys: $imageKeys)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$VenueDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.address, address) || other.address == address) &&
            (identical(other.city, city) || other.city == city) &&
            (identical(other.lat, lat) || other.lat == lat) &&
            (identical(other.lng, lng) || other.lng == lng) &&
            (identical(other.googleMapsUrl, googleMapsUrl) ||
                other.googleMapsUrl == googleMapsUrl) &&
            (identical(other.capacity, capacity) ||
                other.capacity == capacity) &&
            (identical(other.hasParking, hasParking) ||
                other.hasParking == hasParking) &&
            (identical(other.isAccessible, isAccessible) ||
                other.isAccessible == isAccessible) &&
            (identical(other.notes, notes) || other.notes == notes) &&
            const DeepCollectionEquality().equals(
              other._imageKeys,
              _imageKeys,
            ));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    orgId,
    name,
    address,
    city,
    lat,
    lng,
    googleMapsUrl,
    capacity,
    hasParking,
    isAccessible,
    notes,
    const DeepCollectionEquality().hash(_imageKeys),
  );

  /// Create a copy of VenueDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$VenueDtoImplCopyWith<_$VenueDtoImpl> get copyWith =>
      __$$VenueDtoImplCopyWithImpl<_$VenueDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$VenueDtoImplToJson(this);
  }
}

abstract class _VenueDto implements VenueDto {
  const factory _VenueDto({
    required final String id,
    @JsonKey(name: 'org_id') final String? orgId,
    required final String name,
    final String? address,
    final String? city,
    final double? lat,
    final double? lng,
    @JsonKey(name: 'google_maps_url') final String? googleMapsUrl,
    final int? capacity,
    @JsonKey(name: 'has_parking') final bool hasParking,
    @JsonKey(name: 'is_accessible') final bool isAccessible,
    final String? notes,
    @JsonKey(name: 'image_keys') final List<String> imageKeys,
  }) = _$VenueDtoImpl;

  factory _VenueDto.fromJson(Map<String, dynamic> json) =
      _$VenueDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'org_id')
  String? get orgId;
  @override
  String get name;
  @override
  String? get address;
  @override
  String? get city;
  @override
  double? get lat;
  @override
  double? get lng;
  @override
  @JsonKey(name: 'google_maps_url')
  String? get googleMapsUrl;
  @override
  int? get capacity;
  @override
  @JsonKey(name: 'has_parking')
  bool get hasParking;
  @override
  @JsonKey(name: 'is_accessible')
  bool get isAccessible;
  @override
  String? get notes;
  @override
  @JsonKey(name: 'image_keys')
  List<String> get imageKeys;

  /// Create a copy of VenueDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$VenueDtoImplCopyWith<_$VenueDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

SessionDto _$SessionDtoFromJson(Map<String, dynamic> json) {
  return _SessionDto.fromJson(json);
}

/// @nodoc
mixin _$SessionDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String? get eventId => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String? get description => throw _privateConstructorUsedError;

  /// Lowercased server-side (`session`/`break`/`keynote`/`workshop`).
  String get kind => throw _privateConstructorUsedError;
  @JsonKey(name: 'starts_at')
  DateTime get startsAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'ends_at')
  DateTime get endsAt => throw _privateConstructorUsedError;
  int get sort => throw _privateConstructorUsedError;
  @JsonKey(name: 'speaker_ids')
  List<String> get speakerIds => throw _privateConstructorUsedError;

  /// Serializes this SessionDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of SessionDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $SessionDtoCopyWith<SessionDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $SessionDtoCopyWith<$Res> {
  factory $SessionDtoCopyWith(
    SessionDto value,
    $Res Function(SessionDto) then,
  ) = _$SessionDtoCopyWithImpl<$Res, SessionDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String? eventId,
    String title,
    String? description,
    String kind,
    @JsonKey(name: 'starts_at') DateTime startsAt,
    @JsonKey(name: 'ends_at') DateTime endsAt,
    int sort,
    @JsonKey(name: 'speaker_ids') List<String> speakerIds,
  });
}

/// @nodoc
class _$SessionDtoCopyWithImpl<$Res, $Val extends SessionDto>
    implements $SessionDtoCopyWith<$Res> {
  _$SessionDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of SessionDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = freezed,
    Object? title = null,
    Object? description = freezed,
    Object? kind = null,
    Object? startsAt = null,
    Object? endsAt = null,
    Object? sort = null,
    Object? speakerIds = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            eventId: freezed == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String?,
            title: null == title
                ? _value.title
                : title // ignore: cast_nullable_to_non_nullable
                      as String,
            description: freezed == description
                ? _value.description
                : description // ignore: cast_nullable_to_non_nullable
                      as String?,
            kind: null == kind
                ? _value.kind
                : kind // ignore: cast_nullable_to_non_nullable
                      as String,
            startsAt: null == startsAt
                ? _value.startsAt
                : startsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            endsAt: null == endsAt
                ? _value.endsAt
                : endsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            sort: null == sort
                ? _value.sort
                : sort // ignore: cast_nullable_to_non_nullable
                      as int,
            speakerIds: null == speakerIds
                ? _value.speakerIds
                : speakerIds // ignore: cast_nullable_to_non_nullable
                      as List<String>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$SessionDtoImplCopyWith<$Res>
    implements $SessionDtoCopyWith<$Res> {
  factory _$$SessionDtoImplCopyWith(
    _$SessionDtoImpl value,
    $Res Function(_$SessionDtoImpl) then,
  ) = __$$SessionDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String? eventId,
    String title,
    String? description,
    String kind,
    @JsonKey(name: 'starts_at') DateTime startsAt,
    @JsonKey(name: 'ends_at') DateTime endsAt,
    int sort,
    @JsonKey(name: 'speaker_ids') List<String> speakerIds,
  });
}

/// @nodoc
class __$$SessionDtoImplCopyWithImpl<$Res>
    extends _$SessionDtoCopyWithImpl<$Res, _$SessionDtoImpl>
    implements _$$SessionDtoImplCopyWith<$Res> {
  __$$SessionDtoImplCopyWithImpl(
    _$SessionDtoImpl _value,
    $Res Function(_$SessionDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of SessionDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = freezed,
    Object? title = null,
    Object? description = freezed,
    Object? kind = null,
    Object? startsAt = null,
    Object? endsAt = null,
    Object? sort = null,
    Object? speakerIds = null,
  }) {
    return _then(
      _$SessionDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: freezed == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String?,
        title: null == title
            ? _value.title
            : title // ignore: cast_nullable_to_non_nullable
                  as String,
        description: freezed == description
            ? _value.description
            : description // ignore: cast_nullable_to_non_nullable
                  as String?,
        kind: null == kind
            ? _value.kind
            : kind // ignore: cast_nullable_to_non_nullable
                  as String,
        startsAt: null == startsAt
            ? _value.startsAt
            : startsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        endsAt: null == endsAt
            ? _value.endsAt
            : endsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        sort: null == sort
            ? _value.sort
            : sort // ignore: cast_nullable_to_non_nullable
                  as int,
        speakerIds: null == speakerIds
            ? _value._speakerIds
            : speakerIds // ignore: cast_nullable_to_non_nullable
                  as List<String>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$SessionDtoImpl implements _SessionDto {
  const _$SessionDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') this.eventId,
    required this.title,
    this.description,
    this.kind = 'session',
    @JsonKey(name: 'starts_at') required this.startsAt,
    @JsonKey(name: 'ends_at') required this.endsAt,
    this.sort = 0,
    @JsonKey(name: 'speaker_ids')
    final List<String> speakerIds = const <String>[],
  }) : _speakerIds = speakerIds;

  factory _$SessionDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$SessionDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String? eventId;
  @override
  final String title;
  @override
  final String? description;

  /// Lowercased server-side (`session`/`break`/`keynote`/`workshop`).
  @override
  @JsonKey()
  final String kind;
  @override
  @JsonKey(name: 'starts_at')
  final DateTime startsAt;
  @override
  @JsonKey(name: 'ends_at')
  final DateTime endsAt;
  @override
  @JsonKey()
  final int sort;
  final List<String> _speakerIds;
  @override
  @JsonKey(name: 'speaker_ids')
  List<String> get speakerIds {
    if (_speakerIds is EqualUnmodifiableListView) return _speakerIds;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_speakerIds);
  }

  @override
  String toString() {
    return 'SessionDto(id: $id, eventId: $eventId, title: $title, description: $description, kind: $kind, startsAt: $startsAt, endsAt: $endsAt, sort: $sort, speakerIds: $speakerIds)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$SessionDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.description, description) ||
                other.description == description) &&
            (identical(other.kind, kind) || other.kind == kind) &&
            (identical(other.startsAt, startsAt) ||
                other.startsAt == startsAt) &&
            (identical(other.endsAt, endsAt) || other.endsAt == endsAt) &&
            (identical(other.sort, sort) || other.sort == sort) &&
            const DeepCollectionEquality().equals(
              other._speakerIds,
              _speakerIds,
            ));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    title,
    description,
    kind,
    startsAt,
    endsAt,
    sort,
    const DeepCollectionEquality().hash(_speakerIds),
  );

  /// Create a copy of SessionDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$SessionDtoImplCopyWith<_$SessionDtoImpl> get copyWith =>
      __$$SessionDtoImplCopyWithImpl<_$SessionDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$SessionDtoImplToJson(this);
  }
}

abstract class _SessionDto implements SessionDto {
  const factory _SessionDto({
    required final String id,
    @JsonKey(name: 'event_id') final String? eventId,
    required final String title,
    final String? description,
    final String kind,
    @JsonKey(name: 'starts_at') required final DateTime startsAt,
    @JsonKey(name: 'ends_at') required final DateTime endsAt,
    final int sort,
    @JsonKey(name: 'speaker_ids') final List<String> speakerIds,
  }) = _$SessionDtoImpl;

  factory _SessionDto.fromJson(Map<String, dynamic> json) =
      _$SessionDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String? get eventId;
  @override
  String get title;
  @override
  String? get description;

  /// Lowercased server-side (`session`/`break`/`keynote`/`workshop`).
  @override
  String get kind;
  @override
  @JsonKey(name: 'starts_at')
  DateTime get startsAt;
  @override
  @JsonKey(name: 'ends_at')
  DateTime get endsAt;
  @override
  int get sort;
  @override
  @JsonKey(name: 'speaker_ids')
  List<String> get speakerIds;

  /// Create a copy of SessionDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$SessionDtoImplCopyWith<_$SessionDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

PresignDto _$PresignDtoFromJson(Map<String, dynamic> json) {
  return _PresignDto.fromJson(json);
}

/// @nodoc
mixin _$PresignDto {
  String get key => throw _privateConstructorUsedError;
  String get url => throw _privateConstructorUsedError;

  /// Headers the storage provider requires on the PUT. Empty for localdisk, populated for S3-style
  /// providers; always applied verbatim so a provider swap needs no client change.
  Map<String, String> get headers => throw _privateConstructorUsedError;

  /// Serializes this PresignDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of PresignDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $PresignDtoCopyWith<PresignDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $PresignDtoCopyWith<$Res> {
  factory $PresignDtoCopyWith(
    PresignDto value,
    $Res Function(PresignDto) then,
  ) = _$PresignDtoCopyWithImpl<$Res, PresignDto>;
  @useResult
  $Res call({String key, String url, Map<String, String> headers});
}

/// @nodoc
class _$PresignDtoCopyWithImpl<$Res, $Val extends PresignDto>
    implements $PresignDtoCopyWith<$Res> {
  _$PresignDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of PresignDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? key = null, Object? url = null, Object? headers = null}) {
    return _then(
      _value.copyWith(
            key: null == key
                ? _value.key
                : key // ignore: cast_nullable_to_non_nullable
                      as String,
            url: null == url
                ? _value.url
                : url // ignore: cast_nullable_to_non_nullable
                      as String,
            headers: null == headers
                ? _value.headers
                : headers // ignore: cast_nullable_to_non_nullable
                      as Map<String, String>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$PresignDtoImplCopyWith<$Res>
    implements $PresignDtoCopyWith<$Res> {
  factory _$$PresignDtoImplCopyWith(
    _$PresignDtoImpl value,
    $Res Function(_$PresignDtoImpl) then,
  ) = __$$PresignDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({String key, String url, Map<String, String> headers});
}

/// @nodoc
class __$$PresignDtoImplCopyWithImpl<$Res>
    extends _$PresignDtoCopyWithImpl<$Res, _$PresignDtoImpl>
    implements _$$PresignDtoImplCopyWith<$Res> {
  __$$PresignDtoImplCopyWithImpl(
    _$PresignDtoImpl _value,
    $Res Function(_$PresignDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of PresignDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? key = null, Object? url = null, Object? headers = null}) {
    return _then(
      _$PresignDtoImpl(
        key: null == key
            ? _value.key
            : key // ignore: cast_nullable_to_non_nullable
                  as String,
        url: null == url
            ? _value.url
            : url // ignore: cast_nullable_to_non_nullable
                  as String,
        headers: null == headers
            ? _value._headers
            : headers // ignore: cast_nullable_to_non_nullable
                  as Map<String, String>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$PresignDtoImpl implements _PresignDto {
  const _$PresignDtoImpl({
    required this.key,
    required this.url,
    final Map<String, String> headers = const <String, String>{},
  }) : _headers = headers;

  factory _$PresignDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$PresignDtoImplFromJson(json);

  @override
  final String key;
  @override
  final String url;

  /// Headers the storage provider requires on the PUT. Empty for localdisk, populated for S3-style
  /// providers; always applied verbatim so a provider swap needs no client change.
  final Map<String, String> _headers;

  /// Headers the storage provider requires on the PUT. Empty for localdisk, populated for S3-style
  /// providers; always applied verbatim so a provider swap needs no client change.
  @override
  @JsonKey()
  Map<String, String> get headers {
    if (_headers is EqualUnmodifiableMapView) return _headers;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableMapView(_headers);
  }

  @override
  String toString() {
    return 'PresignDto(key: $key, url: $url, headers: $headers)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$PresignDtoImpl &&
            (identical(other.key, key) || other.key == key) &&
            (identical(other.url, url) || other.url == url) &&
            const DeepCollectionEquality().equals(other._headers, _headers));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    key,
    url,
    const DeepCollectionEquality().hash(_headers),
  );

  /// Create a copy of PresignDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$PresignDtoImplCopyWith<_$PresignDtoImpl> get copyWith =>
      __$$PresignDtoImplCopyWithImpl<_$PresignDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$PresignDtoImplToJson(this);
  }
}

abstract class _PresignDto implements PresignDto {
  const factory _PresignDto({
    required final String key,
    required final String url,
    final Map<String, String> headers,
  }) = _$PresignDtoImpl;

  factory _PresignDto.fromJson(Map<String, dynamic> json) =
      _$PresignDtoImpl.fromJson;

  @override
  String get key;
  @override
  String get url;

  /// Headers the storage provider requires on the PUT. Empty for localdisk, populated for S3-style
  /// providers; always applied verbatim so a provider swap needs no client change.
  @override
  Map<String, String> get headers;

  /// Create a copy of PresignDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$PresignDtoImplCopyWith<_$PresignDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EventMediaDto _$EventMediaDtoFromJson(Map<String, dynamic> json) {
  return _EventMediaDto.fromJson(json);
}

/// @nodoc
mixin _$EventMediaDto {
  String get id => throw _privateConstructorUsedError;

  /// `gallery` or `document`.
  String get kind => throw _privateConstructorUsedError;
  String get key => throw _privateConstructorUsedError;
  String? get caption => throw _privateConstructorUsedError;
  int get sort => throw _privateConstructorUsedError;

  /// Serializes this EventMediaDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventMediaDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventMediaDtoCopyWith<EventMediaDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventMediaDtoCopyWith<$Res> {
  factory $EventMediaDtoCopyWith(
    EventMediaDto value,
    $Res Function(EventMediaDto) then,
  ) = _$EventMediaDtoCopyWithImpl<$Res, EventMediaDto>;
  @useResult
  $Res call({String id, String kind, String key, String? caption, int sort});
}

/// @nodoc
class _$EventMediaDtoCopyWithImpl<$Res, $Val extends EventMediaDto>
    implements $EventMediaDtoCopyWith<$Res> {
  _$EventMediaDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventMediaDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? kind = null,
    Object? key = null,
    Object? caption = freezed,
    Object? sort = null,
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
            key: null == key
                ? _value.key
                : key // ignore: cast_nullable_to_non_nullable
                      as String,
            caption: freezed == caption
                ? _value.caption
                : caption // ignore: cast_nullable_to_non_nullable
                      as String?,
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
abstract class _$$EventMediaDtoImplCopyWith<$Res>
    implements $EventMediaDtoCopyWith<$Res> {
  factory _$$EventMediaDtoImplCopyWith(
    _$EventMediaDtoImpl value,
    $Res Function(_$EventMediaDtoImpl) then,
  ) = __$$EventMediaDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({String id, String kind, String key, String? caption, int sort});
}

/// @nodoc
class __$$EventMediaDtoImplCopyWithImpl<$Res>
    extends _$EventMediaDtoCopyWithImpl<$Res, _$EventMediaDtoImpl>
    implements _$$EventMediaDtoImplCopyWith<$Res> {
  __$$EventMediaDtoImplCopyWithImpl(
    _$EventMediaDtoImpl _value,
    $Res Function(_$EventMediaDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventMediaDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? kind = null,
    Object? key = null,
    Object? caption = freezed,
    Object? sort = null,
  }) {
    return _then(
      _$EventMediaDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        kind: null == kind
            ? _value.kind
            : kind // ignore: cast_nullable_to_non_nullable
                  as String,
        key: null == key
            ? _value.key
            : key // ignore: cast_nullable_to_non_nullable
                  as String,
        caption: freezed == caption
            ? _value.caption
            : caption // ignore: cast_nullable_to_non_nullable
                  as String?,
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
class _$EventMediaDtoImpl implements _EventMediaDto {
  const _$EventMediaDtoImpl({
    required this.id,
    this.kind = 'gallery',
    required this.key,
    this.caption,
    this.sort = 0,
  });

  factory _$EventMediaDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventMediaDtoImplFromJson(json);

  @override
  final String id;

  /// `gallery` or `document`.
  @override
  @JsonKey()
  final String kind;
  @override
  final String key;
  @override
  final String? caption;
  @override
  @JsonKey()
  final int sort;

  @override
  String toString() {
    return 'EventMediaDto(id: $id, kind: $kind, key: $key, caption: $caption, sort: $sort)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventMediaDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.kind, kind) || other.kind == kind) &&
            (identical(other.key, key) || other.key == key) &&
            (identical(other.caption, caption) || other.caption == caption) &&
            (identical(other.sort, sort) || other.sort == sort));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, id, kind, key, caption, sort);

  /// Create a copy of EventMediaDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventMediaDtoImplCopyWith<_$EventMediaDtoImpl> get copyWith =>
      __$$EventMediaDtoImplCopyWithImpl<_$EventMediaDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$EventMediaDtoImplToJson(this);
  }
}

abstract class _EventMediaDto implements EventMediaDto {
  const factory _EventMediaDto({
    required final String id,
    final String kind,
    required final String key,
    final String? caption,
    final int sort,
  }) = _$EventMediaDtoImpl;

  factory _EventMediaDto.fromJson(Map<String, dynamic> json) =
      _$EventMediaDtoImpl.fromJson;

  @override
  String get id;

  /// `gallery` or `document`.
  @override
  String get kind;
  @override
  String get key;
  @override
  String? get caption;
  @override
  int get sort;

  /// Create a copy of EventMediaDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventMediaDtoImplCopyWith<_$EventMediaDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

CertificateRosterDto _$CertificateRosterDtoFromJson(Map<String, dynamic> json) {
  return _CertificateRosterDto.fromJson(json);
}

/// @nodoc
mixin _$CertificateRosterDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String? get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'verify_code')
  String? get verifyCode => throw _privateConstructorUsedError;
  @JsonKey(name: 'user_id')
  String? get userId => throw _privateConstructorUsedError;
  @JsonKey(name: 'holder_name')
  String? get holderName => throw _privateConstructorUsedError;

  /// Lowercased server-side (`participation`/`completion`/`achievement`…).
  String? get kind => throw _privateConstructorUsedError;

  /// Lowercased server-side.
  String? get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_revoked')
  bool get isRevoked => throw _privateConstructorUsedError;
  @JsonKey(name: 'revoked_reason')
  String? get revokedReason => throw _privateConstructorUsedError;
  @JsonKey(name: 'issued_at')
  DateTime? get issuedAt => throw _privateConstructorUsedError;

  /// Serializes this CertificateRosterDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of CertificateRosterDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $CertificateRosterDtoCopyWith<CertificateRosterDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $CertificateRosterDtoCopyWith<$Res> {
  factory $CertificateRosterDtoCopyWith(
    CertificateRosterDto value,
    $Res Function(CertificateRosterDto) then,
  ) = _$CertificateRosterDtoCopyWithImpl<$Res, CertificateRosterDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String? eventId,
    @JsonKey(name: 'verify_code') String? verifyCode,
    @JsonKey(name: 'user_id') String? userId,
    @JsonKey(name: 'holder_name') String? holderName,
    String? kind,
    String? status,
    @JsonKey(name: 'is_revoked') bool isRevoked,
    @JsonKey(name: 'revoked_reason') String? revokedReason,
    @JsonKey(name: 'issued_at') DateTime? issuedAt,
  });
}

/// @nodoc
class _$CertificateRosterDtoCopyWithImpl<
  $Res,
  $Val extends CertificateRosterDto
>
    implements $CertificateRosterDtoCopyWith<$Res> {
  _$CertificateRosterDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of CertificateRosterDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = freezed,
    Object? verifyCode = freezed,
    Object? userId = freezed,
    Object? holderName = freezed,
    Object? kind = freezed,
    Object? status = freezed,
    Object? isRevoked = null,
    Object? revokedReason = freezed,
    Object? issuedAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            eventId: freezed == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String?,
            verifyCode: freezed == verifyCode
                ? _value.verifyCode
                : verifyCode // ignore: cast_nullable_to_non_nullable
                      as String?,
            userId: freezed == userId
                ? _value.userId
                : userId // ignore: cast_nullable_to_non_nullable
                      as String?,
            holderName: freezed == holderName
                ? _value.holderName
                : holderName // ignore: cast_nullable_to_non_nullable
                      as String?,
            kind: freezed == kind
                ? _value.kind
                : kind // ignore: cast_nullable_to_non_nullable
                      as String?,
            status: freezed == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String?,
            isRevoked: null == isRevoked
                ? _value.isRevoked
                : isRevoked // ignore: cast_nullable_to_non_nullable
                      as bool,
            revokedReason: freezed == revokedReason
                ? _value.revokedReason
                : revokedReason // ignore: cast_nullable_to_non_nullable
                      as String?,
            issuedAt: freezed == issuedAt
                ? _value.issuedAt
                : issuedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$CertificateRosterDtoImplCopyWith<$Res>
    implements $CertificateRosterDtoCopyWith<$Res> {
  factory _$$CertificateRosterDtoImplCopyWith(
    _$CertificateRosterDtoImpl value,
    $Res Function(_$CertificateRosterDtoImpl) then,
  ) = __$$CertificateRosterDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String? eventId,
    @JsonKey(name: 'verify_code') String? verifyCode,
    @JsonKey(name: 'user_id') String? userId,
    @JsonKey(name: 'holder_name') String? holderName,
    String? kind,
    String? status,
    @JsonKey(name: 'is_revoked') bool isRevoked,
    @JsonKey(name: 'revoked_reason') String? revokedReason,
    @JsonKey(name: 'issued_at') DateTime? issuedAt,
  });
}

/// @nodoc
class __$$CertificateRosterDtoImplCopyWithImpl<$Res>
    extends _$CertificateRosterDtoCopyWithImpl<$Res, _$CertificateRosterDtoImpl>
    implements _$$CertificateRosterDtoImplCopyWith<$Res> {
  __$$CertificateRosterDtoImplCopyWithImpl(
    _$CertificateRosterDtoImpl _value,
    $Res Function(_$CertificateRosterDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of CertificateRosterDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = freezed,
    Object? verifyCode = freezed,
    Object? userId = freezed,
    Object? holderName = freezed,
    Object? kind = freezed,
    Object? status = freezed,
    Object? isRevoked = null,
    Object? revokedReason = freezed,
    Object? issuedAt = freezed,
  }) {
    return _then(
      _$CertificateRosterDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: freezed == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String?,
        verifyCode: freezed == verifyCode
            ? _value.verifyCode
            : verifyCode // ignore: cast_nullable_to_non_nullable
                  as String?,
        userId: freezed == userId
            ? _value.userId
            : userId // ignore: cast_nullable_to_non_nullable
                  as String?,
        holderName: freezed == holderName
            ? _value.holderName
            : holderName // ignore: cast_nullable_to_non_nullable
                  as String?,
        kind: freezed == kind
            ? _value.kind
            : kind // ignore: cast_nullable_to_non_nullable
                  as String?,
        status: freezed == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String?,
        isRevoked: null == isRevoked
            ? _value.isRevoked
            : isRevoked // ignore: cast_nullable_to_non_nullable
                  as bool,
        revokedReason: freezed == revokedReason
            ? _value.revokedReason
            : revokedReason // ignore: cast_nullable_to_non_nullable
                  as String?,
        issuedAt: freezed == issuedAt
            ? _value.issuedAt
            : issuedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$CertificateRosterDtoImpl implements _CertificateRosterDto {
  const _$CertificateRosterDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') this.eventId,
    @JsonKey(name: 'verify_code') this.verifyCode,
    @JsonKey(name: 'user_id') this.userId,
    @JsonKey(name: 'holder_name') this.holderName,
    this.kind,
    this.status,
    @JsonKey(name: 'is_revoked') this.isRevoked = false,
    @JsonKey(name: 'revoked_reason') this.revokedReason,
    @JsonKey(name: 'issued_at') this.issuedAt,
  });

  factory _$CertificateRosterDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$CertificateRosterDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String? eventId;
  @override
  @JsonKey(name: 'verify_code')
  final String? verifyCode;
  @override
  @JsonKey(name: 'user_id')
  final String? userId;
  @override
  @JsonKey(name: 'holder_name')
  final String? holderName;

  /// Lowercased server-side (`participation`/`completion`/`achievement`…).
  @override
  final String? kind;

  /// Lowercased server-side.
  @override
  final String? status;
  @override
  @JsonKey(name: 'is_revoked')
  final bool isRevoked;
  @override
  @JsonKey(name: 'revoked_reason')
  final String? revokedReason;
  @override
  @JsonKey(name: 'issued_at')
  final DateTime? issuedAt;

  @override
  String toString() {
    return 'CertificateRosterDto(id: $id, eventId: $eventId, verifyCode: $verifyCode, userId: $userId, holderName: $holderName, kind: $kind, status: $status, isRevoked: $isRevoked, revokedReason: $revokedReason, issuedAt: $issuedAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$CertificateRosterDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.verifyCode, verifyCode) ||
                other.verifyCode == verifyCode) &&
            (identical(other.userId, userId) || other.userId == userId) &&
            (identical(other.holderName, holderName) ||
                other.holderName == holderName) &&
            (identical(other.kind, kind) || other.kind == kind) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.isRevoked, isRevoked) ||
                other.isRevoked == isRevoked) &&
            (identical(other.revokedReason, revokedReason) ||
                other.revokedReason == revokedReason) &&
            (identical(other.issuedAt, issuedAt) ||
                other.issuedAt == issuedAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    verifyCode,
    userId,
    holderName,
    kind,
    status,
    isRevoked,
    revokedReason,
    issuedAt,
  );

  /// Create a copy of CertificateRosterDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$CertificateRosterDtoImplCopyWith<_$CertificateRosterDtoImpl>
  get copyWith =>
      __$$CertificateRosterDtoImplCopyWithImpl<_$CertificateRosterDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$CertificateRosterDtoImplToJson(this);
  }
}

abstract class _CertificateRosterDto implements CertificateRosterDto {
  const factory _CertificateRosterDto({
    required final String id,
    @JsonKey(name: 'event_id') final String? eventId,
    @JsonKey(name: 'verify_code') final String? verifyCode,
    @JsonKey(name: 'user_id') final String? userId,
    @JsonKey(name: 'holder_name') final String? holderName,
    final String? kind,
    final String? status,
    @JsonKey(name: 'is_revoked') final bool isRevoked,
    @JsonKey(name: 'revoked_reason') final String? revokedReason,
    @JsonKey(name: 'issued_at') final DateTime? issuedAt,
  }) = _$CertificateRosterDtoImpl;

  factory _CertificateRosterDto.fromJson(Map<String, dynamic> json) =
      _$CertificateRosterDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String? get eventId;
  @override
  @JsonKey(name: 'verify_code')
  String? get verifyCode;
  @override
  @JsonKey(name: 'user_id')
  String? get userId;
  @override
  @JsonKey(name: 'holder_name')
  String? get holderName;

  /// Lowercased server-side (`participation`/`completion`/`achievement`…).
  @override
  String? get kind;

  /// Lowercased server-side.
  @override
  String? get status;
  @override
  @JsonKey(name: 'is_revoked')
  bool get isRevoked;
  @override
  @JsonKey(name: 'revoked_reason')
  String? get revokedReason;
  @override
  @JsonKey(name: 'issued_at')
  DateTime? get issuedAt;

  /// Create a copy of CertificateRosterDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$CertificateRosterDtoImplCopyWith<_$CertificateRosterDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

MembershipClaimDto _$MembershipClaimDtoFromJson(Map<String, dynamic> json) {
  return _MembershipClaimDto.fromJson(json);
}

/// @nodoc
mixin _$MembershipClaimDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_id')
  String get orgId => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_name')
  String? get orgName => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_slug')
  String? get orgSlug => throw _privateConstructorUsedError;

  /// Lowercased server-side.
  @JsonKey(name: 'claimed_role')
  String get claimedRole => throw _privateConstructorUsedError;

  /// `submitted` / `under_review` / `official_contact_verification` / `approved` / `rejected`.
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'fast_track')
  bool get fastTrack => throw _privateConstructorUsedError;
  @JsonKey(name: 'valid_until')
  DateTime? get validUntil => throw _privateConstructorUsedError;
  @JsonKey(name: 'reviewed_at')
  DateTime? get reviewedAt => throw _privateConstructorUsedError;
  String? get notes => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this MembershipClaimDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of MembershipClaimDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $MembershipClaimDtoCopyWith<MembershipClaimDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $MembershipClaimDtoCopyWith<$Res> {
  factory $MembershipClaimDtoCopyWith(
    MembershipClaimDto value,
    $Res Function(MembershipClaimDto) then,
  ) = _$MembershipClaimDtoCopyWithImpl<$Res, MembershipClaimDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'org_name') String? orgName,
    @JsonKey(name: 'org_slug') String? orgSlug,
    @JsonKey(name: 'claimed_role') String claimedRole,
    String status,
    @JsonKey(name: 'fast_track') bool fastTrack,
    @JsonKey(name: 'valid_until') DateTime? validUntil,
    @JsonKey(name: 'reviewed_at') DateTime? reviewedAt,
    String? notes,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class _$MembershipClaimDtoCopyWithImpl<$Res, $Val extends MembershipClaimDto>
    implements $MembershipClaimDtoCopyWith<$Res> {
  _$MembershipClaimDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of MembershipClaimDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = null,
    Object? orgName = freezed,
    Object? orgSlug = freezed,
    Object? claimedRole = null,
    Object? status = null,
    Object? fastTrack = null,
    Object? validUntil = freezed,
    Object? reviewedAt = freezed,
    Object? notes = freezed,
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
            orgName: freezed == orgName
                ? _value.orgName
                : orgName // ignore: cast_nullable_to_non_nullable
                      as String?,
            orgSlug: freezed == orgSlug
                ? _value.orgSlug
                : orgSlug // ignore: cast_nullable_to_non_nullable
                      as String?,
            claimedRole: null == claimedRole
                ? _value.claimedRole
                : claimedRole // ignore: cast_nullable_to_non_nullable
                      as String,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            fastTrack: null == fastTrack
                ? _value.fastTrack
                : fastTrack // ignore: cast_nullable_to_non_nullable
                      as bool,
            validUntil: freezed == validUntil
                ? _value.validUntil
                : validUntil // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            reviewedAt: freezed == reviewedAt
                ? _value.reviewedAt
                : reviewedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            notes: freezed == notes
                ? _value.notes
                : notes // ignore: cast_nullable_to_non_nullable
                      as String?,
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
abstract class _$$MembershipClaimDtoImplCopyWith<$Res>
    implements $MembershipClaimDtoCopyWith<$Res> {
  factory _$$MembershipClaimDtoImplCopyWith(
    _$MembershipClaimDtoImpl value,
    $Res Function(_$MembershipClaimDtoImpl) then,
  ) = __$$MembershipClaimDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'org_name') String? orgName,
    @JsonKey(name: 'org_slug') String? orgSlug,
    @JsonKey(name: 'claimed_role') String claimedRole,
    String status,
    @JsonKey(name: 'fast_track') bool fastTrack,
    @JsonKey(name: 'valid_until') DateTime? validUntil,
    @JsonKey(name: 'reviewed_at') DateTime? reviewedAt,
    String? notes,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class __$$MembershipClaimDtoImplCopyWithImpl<$Res>
    extends _$MembershipClaimDtoCopyWithImpl<$Res, _$MembershipClaimDtoImpl>
    implements _$$MembershipClaimDtoImplCopyWith<$Res> {
  __$$MembershipClaimDtoImplCopyWithImpl(
    _$MembershipClaimDtoImpl _value,
    $Res Function(_$MembershipClaimDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of MembershipClaimDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = null,
    Object? orgName = freezed,
    Object? orgSlug = freezed,
    Object? claimedRole = null,
    Object? status = null,
    Object? fastTrack = null,
    Object? validUntil = freezed,
    Object? reviewedAt = freezed,
    Object? notes = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$MembershipClaimDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        orgId: null == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String,
        orgName: freezed == orgName
            ? _value.orgName
            : orgName // ignore: cast_nullable_to_non_nullable
                  as String?,
        orgSlug: freezed == orgSlug
            ? _value.orgSlug
            : orgSlug // ignore: cast_nullable_to_non_nullable
                  as String?,
        claimedRole: null == claimedRole
            ? _value.claimedRole
            : claimedRole // ignore: cast_nullable_to_non_nullable
                  as String,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        fastTrack: null == fastTrack
            ? _value.fastTrack
            : fastTrack // ignore: cast_nullable_to_non_nullable
                  as bool,
        validUntil: freezed == validUntil
            ? _value.validUntil
            : validUntil // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        reviewedAt: freezed == reviewedAt
            ? _value.reviewedAt
            : reviewedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        notes: freezed == notes
            ? _value.notes
            : notes // ignore: cast_nullable_to_non_nullable
                  as String?,
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
class _$MembershipClaimDtoImpl implements _MembershipClaimDto {
  const _$MembershipClaimDtoImpl({
    required this.id,
    @JsonKey(name: 'org_id') required this.orgId,
    @JsonKey(name: 'org_name') this.orgName,
    @JsonKey(name: 'org_slug') this.orgSlug,
    @JsonKey(name: 'claimed_role') this.claimedRole = 'staff',
    this.status = 'submitted',
    @JsonKey(name: 'fast_track') this.fastTrack = false,
    @JsonKey(name: 'valid_until') this.validUntil,
    @JsonKey(name: 'reviewed_at') this.reviewedAt,
    this.notes,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$MembershipClaimDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$MembershipClaimDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'org_id')
  final String orgId;
  @override
  @JsonKey(name: 'org_name')
  final String? orgName;
  @override
  @JsonKey(name: 'org_slug')
  final String? orgSlug;

  /// Lowercased server-side.
  @override
  @JsonKey(name: 'claimed_role')
  final String claimedRole;

  /// `submitted` / `under_review` / `official_contact_verification` / `approved` / `rejected`.
  @override
  @JsonKey()
  final String status;
  @override
  @JsonKey(name: 'fast_track')
  final bool fastTrack;
  @override
  @JsonKey(name: 'valid_until')
  final DateTime? validUntil;
  @override
  @JsonKey(name: 'reviewed_at')
  final DateTime? reviewedAt;
  @override
  final String? notes;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  @override
  String toString() {
    return 'MembershipClaimDto(id: $id, orgId: $orgId, orgName: $orgName, orgSlug: $orgSlug, claimedRole: $claimedRole, status: $status, fastTrack: $fastTrack, validUntil: $validUntil, reviewedAt: $reviewedAt, notes: $notes, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$MembershipClaimDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.orgName, orgName) || other.orgName == orgName) &&
            (identical(other.orgSlug, orgSlug) || other.orgSlug == orgSlug) &&
            (identical(other.claimedRole, claimedRole) ||
                other.claimedRole == claimedRole) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.fastTrack, fastTrack) ||
                other.fastTrack == fastTrack) &&
            (identical(other.validUntil, validUntil) ||
                other.validUntil == validUntil) &&
            (identical(other.reviewedAt, reviewedAt) ||
                other.reviewedAt == reviewedAt) &&
            (identical(other.notes, notes) || other.notes == notes) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    orgId,
    orgName,
    orgSlug,
    claimedRole,
    status,
    fastTrack,
    validUntil,
    reviewedAt,
    notes,
    createdAt,
  );

  /// Create a copy of MembershipClaimDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$MembershipClaimDtoImplCopyWith<_$MembershipClaimDtoImpl> get copyWith =>
      __$$MembershipClaimDtoImplCopyWithImpl<_$MembershipClaimDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$MembershipClaimDtoImplToJson(this);
  }
}

abstract class _MembershipClaimDto implements MembershipClaimDto {
  const factory _MembershipClaimDto({
    required final String id,
    @JsonKey(name: 'org_id') required final String orgId,
    @JsonKey(name: 'org_name') final String? orgName,
    @JsonKey(name: 'org_slug') final String? orgSlug,
    @JsonKey(name: 'claimed_role') final String claimedRole,
    final String status,
    @JsonKey(name: 'fast_track') final bool fastTrack,
    @JsonKey(name: 'valid_until') final DateTime? validUntil,
    @JsonKey(name: 'reviewed_at') final DateTime? reviewedAt,
    final String? notes,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$MembershipClaimDtoImpl;

  factory _MembershipClaimDto.fromJson(Map<String, dynamic> json) =
      _$MembershipClaimDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'org_id')
  String get orgId;
  @override
  @JsonKey(name: 'org_name')
  String? get orgName;
  @override
  @JsonKey(name: 'org_slug')
  String? get orgSlug;

  /// Lowercased server-side.
  @override
  @JsonKey(name: 'claimed_role')
  String get claimedRole;

  /// `submitted` / `under_review` / `official_contact_verification` / `approved` / `rejected`.
  @override
  String get status;
  @override
  @JsonKey(name: 'fast_track')
  bool get fastTrack;
  @override
  @JsonKey(name: 'valid_until')
  DateTime? get validUntil;
  @override
  @JsonKey(name: 'reviewed_at')
  DateTime? get reviewedAt;
  @override
  String? get notes;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;

  /// Create a copy of MembershipClaimDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$MembershipClaimDtoImplCopyWith<_$MembershipClaimDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
