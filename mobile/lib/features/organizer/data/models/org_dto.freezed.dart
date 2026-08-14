// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'org_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

OrgDto _$OrgDtoFromJson(Map<String, dynamic> json) {
  return _OrgDto.fromJson(json);
}

/// @nodoc
mixin _$OrgDto {
  String get id => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get slug => throw _privateConstructorUsedError;

  /// A storage key (`orgs/…`), not a URL — it needs presigning before it can be rendered.
  @JsonKey(name: 'logo_key')
  String? get logoKey => throw _privateConstructorUsedError;

  /// The caller's role in this org, lowercased (`owner`/`manager`/`staff`/`finance`).
  String? get role =>
      throw _privateConstructorUsedError; // ── Detail-only: absent from the list response, hence nullable. ──
  String? get bio => throw _privateConstructorUsedError;
  @JsonKey(name: 'links_json')
  String? get linksJson => throw _privateConstructorUsedError;
  @JsonKey(name: 'payout_account_status')
  String? get payoutAccountStatus => throw _privateConstructorUsedError;
  @JsonKey(name: 'bank_last4')
  String? get bankLast4 => throw _privateConstructorUsedError;

  /// An integer over the wire (`"tier": 1`), not a string.
  int? get tier => throw _privateConstructorUsedError;

  /// `OrganizationType`, lowercased (D-043).
  String? get type => throw _privateConstructorUsedError;
  @JsonKey(name: 'primary_domain')
  String? get primaryDomain => throw _privateConstructorUsedError;
  @JsonKey(name: 'verification_status')
  String? get verificationStatus => throw _privateConstructorUsedError;

  /// Serializes this OrgDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of OrgDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $OrgDtoCopyWith<OrgDto> get copyWith => throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $OrgDtoCopyWith<$Res> {
  factory $OrgDtoCopyWith(OrgDto value, $Res Function(OrgDto) then) =
      _$OrgDtoCopyWithImpl<$Res, OrgDto>;
  @useResult
  $Res call({
    String id,
    String name,
    String? slug,
    @JsonKey(name: 'logo_key') String? logoKey,
    String? role,
    String? bio,
    @JsonKey(name: 'links_json') String? linksJson,
    @JsonKey(name: 'payout_account_status') String? payoutAccountStatus,
    @JsonKey(name: 'bank_last4') String? bankLast4,
    int? tier,
    String? type,
    @JsonKey(name: 'primary_domain') String? primaryDomain,
    @JsonKey(name: 'verification_status') String? verificationStatus,
  });
}

/// @nodoc
class _$OrgDtoCopyWithImpl<$Res, $Val extends OrgDto>
    implements $OrgDtoCopyWith<$Res> {
  _$OrgDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of OrgDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? slug = freezed,
    Object? logoKey = freezed,
    Object? role = freezed,
    Object? bio = freezed,
    Object? linksJson = freezed,
    Object? payoutAccountStatus = freezed,
    Object? bankLast4 = freezed,
    Object? tier = freezed,
    Object? type = freezed,
    Object? primaryDomain = freezed,
    Object? verificationStatus = freezed,
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
            slug: freezed == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String?,
            logoKey: freezed == logoKey
                ? _value.logoKey
                : logoKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            role: freezed == role
                ? _value.role
                : role // ignore: cast_nullable_to_non_nullable
                      as String?,
            bio: freezed == bio
                ? _value.bio
                : bio // ignore: cast_nullable_to_non_nullable
                      as String?,
            linksJson: freezed == linksJson
                ? _value.linksJson
                : linksJson // ignore: cast_nullable_to_non_nullable
                      as String?,
            payoutAccountStatus: freezed == payoutAccountStatus
                ? _value.payoutAccountStatus
                : payoutAccountStatus // ignore: cast_nullable_to_non_nullable
                      as String?,
            bankLast4: freezed == bankLast4
                ? _value.bankLast4
                : bankLast4 // ignore: cast_nullable_to_non_nullable
                      as String?,
            tier: freezed == tier
                ? _value.tier
                : tier // ignore: cast_nullable_to_non_nullable
                      as int?,
            type: freezed == type
                ? _value.type
                : type // ignore: cast_nullable_to_non_nullable
                      as String?,
            primaryDomain: freezed == primaryDomain
                ? _value.primaryDomain
                : primaryDomain // ignore: cast_nullable_to_non_nullable
                      as String?,
            verificationStatus: freezed == verificationStatus
                ? _value.verificationStatus
                : verificationStatus // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$OrgDtoImplCopyWith<$Res> implements $OrgDtoCopyWith<$Res> {
  factory _$$OrgDtoImplCopyWith(
    _$OrgDtoImpl value,
    $Res Function(_$OrgDtoImpl) then,
  ) = __$$OrgDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String name,
    String? slug,
    @JsonKey(name: 'logo_key') String? logoKey,
    String? role,
    String? bio,
    @JsonKey(name: 'links_json') String? linksJson,
    @JsonKey(name: 'payout_account_status') String? payoutAccountStatus,
    @JsonKey(name: 'bank_last4') String? bankLast4,
    int? tier,
    String? type,
    @JsonKey(name: 'primary_domain') String? primaryDomain,
    @JsonKey(name: 'verification_status') String? verificationStatus,
  });
}

/// @nodoc
class __$$OrgDtoImplCopyWithImpl<$Res>
    extends _$OrgDtoCopyWithImpl<$Res, _$OrgDtoImpl>
    implements _$$OrgDtoImplCopyWith<$Res> {
  __$$OrgDtoImplCopyWithImpl(
    _$OrgDtoImpl _value,
    $Res Function(_$OrgDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of OrgDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? slug = freezed,
    Object? logoKey = freezed,
    Object? role = freezed,
    Object? bio = freezed,
    Object? linksJson = freezed,
    Object? payoutAccountStatus = freezed,
    Object? bankLast4 = freezed,
    Object? tier = freezed,
    Object? type = freezed,
    Object? primaryDomain = freezed,
    Object? verificationStatus = freezed,
  }) {
    return _then(
      _$OrgDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: freezed == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String?,
        logoKey: freezed == logoKey
            ? _value.logoKey
            : logoKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        role: freezed == role
            ? _value.role
            : role // ignore: cast_nullable_to_non_nullable
                  as String?,
        bio: freezed == bio
            ? _value.bio
            : bio // ignore: cast_nullable_to_non_nullable
                  as String?,
        linksJson: freezed == linksJson
            ? _value.linksJson
            : linksJson // ignore: cast_nullable_to_non_nullable
                  as String?,
        payoutAccountStatus: freezed == payoutAccountStatus
            ? _value.payoutAccountStatus
            : payoutAccountStatus // ignore: cast_nullable_to_non_nullable
                  as String?,
        bankLast4: freezed == bankLast4
            ? _value.bankLast4
            : bankLast4 // ignore: cast_nullable_to_non_nullable
                  as String?,
        tier: freezed == tier
            ? _value.tier
            : tier // ignore: cast_nullable_to_non_nullable
                  as int?,
        type: freezed == type
            ? _value.type
            : type // ignore: cast_nullable_to_non_nullable
                  as String?,
        primaryDomain: freezed == primaryDomain
            ? _value.primaryDomain
            : primaryDomain // ignore: cast_nullable_to_non_nullable
                  as String?,
        verificationStatus: freezed == verificationStatus
            ? _value.verificationStatus
            : verificationStatus // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$OrgDtoImpl implements _OrgDto {
  const _$OrgDtoImpl({
    required this.id,
    required this.name,
    this.slug,
    @JsonKey(name: 'logo_key') this.logoKey,
    this.role,
    this.bio,
    @JsonKey(name: 'links_json') this.linksJson,
    @JsonKey(name: 'payout_account_status') this.payoutAccountStatus,
    @JsonKey(name: 'bank_last4') this.bankLast4,
    this.tier,
    this.type,
    @JsonKey(name: 'primary_domain') this.primaryDomain,
    @JsonKey(name: 'verification_status') this.verificationStatus,
  });

  factory _$OrgDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$OrgDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String name;
  @override
  final String? slug;

  /// A storage key (`orgs/…`), not a URL — it needs presigning before it can be rendered.
  @override
  @JsonKey(name: 'logo_key')
  final String? logoKey;

  /// The caller's role in this org, lowercased (`owner`/`manager`/`staff`/`finance`).
  @override
  final String? role;
  // ── Detail-only: absent from the list response, hence nullable. ──
  @override
  final String? bio;
  @override
  @JsonKey(name: 'links_json')
  final String? linksJson;
  @override
  @JsonKey(name: 'payout_account_status')
  final String? payoutAccountStatus;
  @override
  @JsonKey(name: 'bank_last4')
  final String? bankLast4;

  /// An integer over the wire (`"tier": 1`), not a string.
  @override
  final int? tier;

  /// `OrganizationType`, lowercased (D-043).
  @override
  final String? type;
  @override
  @JsonKey(name: 'primary_domain')
  final String? primaryDomain;
  @override
  @JsonKey(name: 'verification_status')
  final String? verificationStatus;

  @override
  String toString() {
    return 'OrgDto(id: $id, name: $name, slug: $slug, logoKey: $logoKey, role: $role, bio: $bio, linksJson: $linksJson, payoutAccountStatus: $payoutAccountStatus, bankLast4: $bankLast4, tier: $tier, type: $type, primaryDomain: $primaryDomain, verificationStatus: $verificationStatus)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$OrgDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.logoKey, logoKey) || other.logoKey == logoKey) &&
            (identical(other.role, role) || other.role == role) &&
            (identical(other.bio, bio) || other.bio == bio) &&
            (identical(other.linksJson, linksJson) ||
                other.linksJson == linksJson) &&
            (identical(other.payoutAccountStatus, payoutAccountStatus) ||
                other.payoutAccountStatus == payoutAccountStatus) &&
            (identical(other.bankLast4, bankLast4) ||
                other.bankLast4 == bankLast4) &&
            (identical(other.tier, tier) || other.tier == tier) &&
            (identical(other.type, type) || other.type == type) &&
            (identical(other.primaryDomain, primaryDomain) ||
                other.primaryDomain == primaryDomain) &&
            (identical(other.verificationStatus, verificationStatus) ||
                other.verificationStatus == verificationStatus));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    name,
    slug,
    logoKey,
    role,
    bio,
    linksJson,
    payoutAccountStatus,
    bankLast4,
    tier,
    type,
    primaryDomain,
    verificationStatus,
  );

  /// Create a copy of OrgDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$OrgDtoImplCopyWith<_$OrgDtoImpl> get copyWith =>
      __$$OrgDtoImplCopyWithImpl<_$OrgDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$OrgDtoImplToJson(this);
  }
}

abstract class _OrgDto implements OrgDto {
  const factory _OrgDto({
    required final String id,
    required final String name,
    final String? slug,
    @JsonKey(name: 'logo_key') final String? logoKey,
    final String? role,
    final String? bio,
    @JsonKey(name: 'links_json') final String? linksJson,
    @JsonKey(name: 'payout_account_status') final String? payoutAccountStatus,
    @JsonKey(name: 'bank_last4') final String? bankLast4,
    final int? tier,
    final String? type,
    @JsonKey(name: 'primary_domain') final String? primaryDomain,
    @JsonKey(name: 'verification_status') final String? verificationStatus,
  }) = _$OrgDtoImpl;

  factory _OrgDto.fromJson(Map<String, dynamic> json) = _$OrgDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get name;
  @override
  String? get slug;

  /// A storage key (`orgs/…`), not a URL — it needs presigning before it can be rendered.
  @override
  @JsonKey(name: 'logo_key')
  String? get logoKey;

  /// The caller's role in this org, lowercased (`owner`/`manager`/`staff`/`finance`).
  @override
  String? get role; // ── Detail-only: absent from the list response, hence nullable. ──
  @override
  String? get bio;
  @override
  @JsonKey(name: 'links_json')
  String? get linksJson;
  @override
  @JsonKey(name: 'payout_account_status')
  String? get payoutAccountStatus;
  @override
  @JsonKey(name: 'bank_last4')
  String? get bankLast4;

  /// An integer over the wire (`"tier": 1`), not a string.
  @override
  int? get tier;

  /// `OrganizationType`, lowercased (D-043).
  @override
  String? get type;
  @override
  @JsonKey(name: 'primary_domain')
  String? get primaryDomain;
  @override
  @JsonKey(name: 'verification_status')
  String? get verificationStatus;

  /// Create a copy of OrgDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$OrgDtoImplCopyWith<_$OrgDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

OrgMemberDto _$OrgMemberDtoFromJson(Map<String, dynamic> json) {
  return _OrgMemberDto.fromJson(json);
}

/// @nodoc
mixin _$OrgMemberDto {
  @JsonKey(name: 'user_id')
  String get userId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get username => throw _privateConstructorUsedError;
  String? get phone => throw _privateConstructorUsedError;
  String get role => throw _privateConstructorUsedError;
  @JsonKey(name: 'joined_at')
  DateTime get joinedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'avatar_key')
  String? get avatarKey => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_verified')
  bool get isVerified => throw _privateConstructorUsedError;

  /// Serializes this OrgMemberDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of OrgMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $OrgMemberDtoCopyWith<OrgMemberDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $OrgMemberDtoCopyWith<$Res> {
  factory $OrgMemberDtoCopyWith(
    OrgMemberDto value,
    $Res Function(OrgMemberDto) then,
  ) = _$OrgMemberDtoCopyWithImpl<$Res, OrgMemberDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'user_id') String userId,
    String name,
    String? username,
    String? phone,
    String role,
    @JsonKey(name: 'joined_at') DateTime joinedAt,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'is_verified') bool isVerified,
  });
}

/// @nodoc
class _$OrgMemberDtoCopyWithImpl<$Res, $Val extends OrgMemberDto>
    implements $OrgMemberDtoCopyWith<$Res> {
  _$OrgMemberDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of OrgMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? userId = null,
    Object? name = null,
    Object? username = freezed,
    Object? phone = freezed,
    Object? role = null,
    Object? joinedAt = null,
    Object? avatarKey = freezed,
    Object? isVerified = null,
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
            phone: freezed == phone
                ? _value.phone
                : phone // ignore: cast_nullable_to_non_nullable
                      as String?,
            role: null == role
                ? _value.role
                : role // ignore: cast_nullable_to_non_nullable
                      as String,
            joinedAt: null == joinedAt
                ? _value.joinedAt
                : joinedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            avatarKey: freezed == avatarKey
                ? _value.avatarKey
                : avatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            isVerified: null == isVerified
                ? _value.isVerified
                : isVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$OrgMemberDtoImplCopyWith<$Res>
    implements $OrgMemberDtoCopyWith<$Res> {
  factory _$$OrgMemberDtoImplCopyWith(
    _$OrgMemberDtoImpl value,
    $Res Function(_$OrgMemberDtoImpl) then,
  ) = __$$OrgMemberDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'user_id') String userId,
    String name,
    String? username,
    String? phone,
    String role,
    @JsonKey(name: 'joined_at') DateTime joinedAt,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'is_verified') bool isVerified,
  });
}

/// @nodoc
class __$$OrgMemberDtoImplCopyWithImpl<$Res>
    extends _$OrgMemberDtoCopyWithImpl<$Res, _$OrgMemberDtoImpl>
    implements _$$OrgMemberDtoImplCopyWith<$Res> {
  __$$OrgMemberDtoImplCopyWithImpl(
    _$OrgMemberDtoImpl _value,
    $Res Function(_$OrgMemberDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of OrgMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? userId = null,
    Object? name = null,
    Object? username = freezed,
    Object? phone = freezed,
    Object? role = null,
    Object? joinedAt = null,
    Object? avatarKey = freezed,
    Object? isVerified = null,
  }) {
    return _then(
      _$OrgMemberDtoImpl(
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
        phone: freezed == phone
            ? _value.phone
            : phone // ignore: cast_nullable_to_non_nullable
                  as String?,
        role: null == role
            ? _value.role
            : role // ignore: cast_nullable_to_non_nullable
                  as String,
        joinedAt: null == joinedAt
            ? _value.joinedAt
            : joinedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        avatarKey: freezed == avatarKey
            ? _value.avatarKey
            : avatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        isVerified: null == isVerified
            ? _value.isVerified
            : isVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$OrgMemberDtoImpl implements _OrgMemberDto {
  const _$OrgMemberDtoImpl({
    @JsonKey(name: 'user_id') required this.userId,
    required this.name,
    this.username,
    this.phone,
    required this.role,
    @JsonKey(name: 'joined_at') required this.joinedAt,
    @JsonKey(name: 'avatar_key') this.avatarKey,
    @JsonKey(name: 'is_verified') this.isVerified = false,
  });

  factory _$OrgMemberDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$OrgMemberDtoImplFromJson(json);

  @override
  @JsonKey(name: 'user_id')
  final String userId;
  @override
  final String name;
  @override
  final String? username;
  @override
  final String? phone;
  @override
  final String role;
  @override
  @JsonKey(name: 'joined_at')
  final DateTime joinedAt;
  @override
  @JsonKey(name: 'avatar_key')
  final String? avatarKey;
  @override
  @JsonKey(name: 'is_verified')
  final bool isVerified;

  @override
  String toString() {
    return 'OrgMemberDto(userId: $userId, name: $name, username: $username, phone: $phone, role: $role, joinedAt: $joinedAt, avatarKey: $avatarKey, isVerified: $isVerified)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$OrgMemberDtoImpl &&
            (identical(other.userId, userId) || other.userId == userId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.phone, phone) || other.phone == phone) &&
            (identical(other.role, role) || other.role == role) &&
            (identical(other.joinedAt, joinedAt) ||
                other.joinedAt == joinedAt) &&
            (identical(other.avatarKey, avatarKey) ||
                other.avatarKey == avatarKey) &&
            (identical(other.isVerified, isVerified) ||
                other.isVerified == isVerified));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    userId,
    name,
    username,
    phone,
    role,
    joinedAt,
    avatarKey,
    isVerified,
  );

  /// Create a copy of OrgMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$OrgMemberDtoImplCopyWith<_$OrgMemberDtoImpl> get copyWith =>
      __$$OrgMemberDtoImplCopyWithImpl<_$OrgMemberDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$OrgMemberDtoImplToJson(this);
  }
}

abstract class _OrgMemberDto implements OrgMemberDto {
  const factory _OrgMemberDto({
    @JsonKey(name: 'user_id') required final String userId,
    required final String name,
    final String? username,
    final String? phone,
    required final String role,
    @JsonKey(name: 'joined_at') required final DateTime joinedAt,
    @JsonKey(name: 'avatar_key') final String? avatarKey,
    @JsonKey(name: 'is_verified') final bool isVerified,
  }) = _$OrgMemberDtoImpl;

  factory _OrgMemberDto.fromJson(Map<String, dynamic> json) =
      _$OrgMemberDtoImpl.fromJson;

  @override
  @JsonKey(name: 'user_id')
  String get userId;
  @override
  String get name;
  @override
  String? get username;
  @override
  String? get phone;
  @override
  String get role;
  @override
  @JsonKey(name: 'joined_at')
  DateTime get joinedAt;
  @override
  @JsonKey(name: 'avatar_key')
  String? get avatarKey;
  @override
  @JsonKey(name: 'is_verified')
  bool get isVerified;

  /// Create a copy of OrgMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$OrgMemberDtoImplCopyWith<_$OrgMemberDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

LedgerEntryDto _$LedgerEntryDtoFromJson(Map<String, dynamic> json) {
  return _LedgerEntryDto.fromJson(json);
}

/// @nodoc
mixin _$LedgerEntryDto {
  String get id => throw _privateConstructorUsedError;
  String get type => throw _privateConstructorUsedError;
  String get description => throw _privateConstructorUsedError;
  @JsonKey(name: 'amount_paise')
  int get amountPaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'balance_after_paise')
  int get balanceAfterPaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime get createdAt => throw _privateConstructorUsedError;

  /// Serializes this LedgerEntryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of LedgerEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $LedgerEntryDtoCopyWith<LedgerEntryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $LedgerEntryDtoCopyWith<$Res> {
  factory $LedgerEntryDtoCopyWith(
    LedgerEntryDto value,
    $Res Function(LedgerEntryDto) then,
  ) = _$LedgerEntryDtoCopyWithImpl<$Res, LedgerEntryDto>;
  @useResult
  $Res call({
    String id,
    String type,
    String description,
    @JsonKey(name: 'amount_paise') int amountPaise,
    @JsonKey(name: 'balance_after_paise') int balanceAfterPaise,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class _$LedgerEntryDtoCopyWithImpl<$Res, $Val extends LedgerEntryDto>
    implements $LedgerEntryDtoCopyWith<$Res> {
  _$LedgerEntryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of LedgerEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? type = null,
    Object? description = null,
    Object? amountPaise = null,
    Object? balanceAfterPaise = null,
    Object? createdAt = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            type: null == type
                ? _value.type
                : type // ignore: cast_nullable_to_non_nullable
                      as String,
            description: null == description
                ? _value.description
                : description // ignore: cast_nullable_to_non_nullable
                      as String,
            amountPaise: null == amountPaise
                ? _value.amountPaise
                : amountPaise // ignore: cast_nullable_to_non_nullable
                      as int,
            balanceAfterPaise: null == balanceAfterPaise
                ? _value.balanceAfterPaise
                : balanceAfterPaise // ignore: cast_nullable_to_non_nullable
                      as int,
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
abstract class _$$LedgerEntryDtoImplCopyWith<$Res>
    implements $LedgerEntryDtoCopyWith<$Res> {
  factory _$$LedgerEntryDtoImplCopyWith(
    _$LedgerEntryDtoImpl value,
    $Res Function(_$LedgerEntryDtoImpl) then,
  ) = __$$LedgerEntryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String type,
    String description,
    @JsonKey(name: 'amount_paise') int amountPaise,
    @JsonKey(name: 'balance_after_paise') int balanceAfterPaise,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class __$$LedgerEntryDtoImplCopyWithImpl<$Res>
    extends _$LedgerEntryDtoCopyWithImpl<$Res, _$LedgerEntryDtoImpl>
    implements _$$LedgerEntryDtoImplCopyWith<$Res> {
  __$$LedgerEntryDtoImplCopyWithImpl(
    _$LedgerEntryDtoImpl _value,
    $Res Function(_$LedgerEntryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of LedgerEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? type = null,
    Object? description = null,
    Object? amountPaise = null,
    Object? balanceAfterPaise = null,
    Object? createdAt = null,
  }) {
    return _then(
      _$LedgerEntryDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        type: null == type
            ? _value.type
            : type // ignore: cast_nullable_to_non_nullable
                  as String,
        description: null == description
            ? _value.description
            : description // ignore: cast_nullable_to_non_nullable
                  as String,
        amountPaise: null == amountPaise
            ? _value.amountPaise
            : amountPaise // ignore: cast_nullable_to_non_nullable
                  as int,
        balanceAfterPaise: null == balanceAfterPaise
            ? _value.balanceAfterPaise
            : balanceAfterPaise // ignore: cast_nullable_to_non_nullable
                  as int,
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
class _$LedgerEntryDtoImpl implements _LedgerEntryDto {
  const _$LedgerEntryDtoImpl({
    required this.id,
    required this.type,
    required this.description,
    @JsonKey(name: 'amount_paise') required this.amountPaise,
    @JsonKey(name: 'balance_after_paise') required this.balanceAfterPaise,
    @JsonKey(name: 'created_at') required this.createdAt,
  });

  factory _$LedgerEntryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$LedgerEntryDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String type;
  @override
  final String description;
  @override
  @JsonKey(name: 'amount_paise')
  final int amountPaise;
  @override
  @JsonKey(name: 'balance_after_paise')
  final int balanceAfterPaise;
  @override
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  @override
  String toString() {
    return 'LedgerEntryDto(id: $id, type: $type, description: $description, amountPaise: $amountPaise, balanceAfterPaise: $balanceAfterPaise, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$LedgerEntryDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.type, type) || other.type == type) &&
            (identical(other.description, description) ||
                other.description == description) &&
            (identical(other.amountPaise, amountPaise) ||
                other.amountPaise == amountPaise) &&
            (identical(other.balanceAfterPaise, balanceAfterPaise) ||
                other.balanceAfterPaise == balanceAfterPaise) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    type,
    description,
    amountPaise,
    balanceAfterPaise,
    createdAt,
  );

  /// Create a copy of LedgerEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$LedgerEntryDtoImplCopyWith<_$LedgerEntryDtoImpl> get copyWith =>
      __$$LedgerEntryDtoImplCopyWithImpl<_$LedgerEntryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$LedgerEntryDtoImplToJson(this);
  }
}

abstract class _LedgerEntryDto implements LedgerEntryDto {
  const factory _LedgerEntryDto({
    required final String id,
    required final String type,
    required final String description,
    @JsonKey(name: 'amount_paise') required final int amountPaise,
    @JsonKey(name: 'balance_after_paise') required final int balanceAfterPaise,
    @JsonKey(name: 'created_at') required final DateTime createdAt,
  }) = _$LedgerEntryDtoImpl;

  factory _LedgerEntryDto.fromJson(Map<String, dynamic> json) =
      _$LedgerEntryDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get type;
  @override
  String get description;
  @override
  @JsonKey(name: 'amount_paise')
  int get amountPaise;
  @override
  @JsonKey(name: 'balance_after_paise')
  int get balanceAfterPaise;
  @override
  @JsonKey(name: 'created_at')
  DateTime get createdAt;

  /// Create a copy of LedgerEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$LedgerEntryDtoImplCopyWith<_$LedgerEntryDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

WalletDto _$WalletDtoFromJson(Map<String, dynamic> json) {
  return _WalletDto.fromJson(json);
}

/// @nodoc
mixin _$WalletDto {
  @JsonKey(name: 'org_id')
  String get orgId => throw _privateConstructorUsedError;
  @JsonKey(name: 'collected_paise')
  int get collectedPaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'available_paise')
  int get availablePaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'advanced_paise')
  int get advancedPaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'reserved_paise')
  int get reservedPaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'settled_paise')
  int get settledPaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'lifetime_earned_paise')
  int get lifetimeEarnedPaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'lifetime_withdrawn_paise')
  int get lifetimeWithdrawnPaise => throw _privateConstructorUsedError; // One currency per wallet (V3 §9.1, D-257). The server has always sent it; nothing read it.
  String get currency => throw _privateConstructorUsedError;

  /// Serializes this WalletDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of WalletDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $WalletDtoCopyWith<WalletDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $WalletDtoCopyWith<$Res> {
  factory $WalletDtoCopyWith(WalletDto value, $Res Function(WalletDto) then) =
      _$WalletDtoCopyWithImpl<$Res, WalletDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'collected_paise') int collectedPaise,
    @JsonKey(name: 'available_paise') int availablePaise,
    @JsonKey(name: 'advanced_paise') int advancedPaise,
    @JsonKey(name: 'reserved_paise') int reservedPaise,
    @JsonKey(name: 'settled_paise') int settledPaise,
    @JsonKey(name: 'lifetime_earned_paise') int lifetimeEarnedPaise,
    @JsonKey(name: 'lifetime_withdrawn_paise') int lifetimeWithdrawnPaise,
    String currency,
  });
}

/// @nodoc
class _$WalletDtoCopyWithImpl<$Res, $Val extends WalletDto>
    implements $WalletDtoCopyWith<$Res> {
  _$WalletDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of WalletDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? orgId = null,
    Object? collectedPaise = null,
    Object? availablePaise = null,
    Object? advancedPaise = null,
    Object? reservedPaise = null,
    Object? settledPaise = null,
    Object? lifetimeEarnedPaise = null,
    Object? lifetimeWithdrawnPaise = null,
    Object? currency = null,
  }) {
    return _then(
      _value.copyWith(
            orgId: null == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String,
            collectedPaise: null == collectedPaise
                ? _value.collectedPaise
                : collectedPaise // ignore: cast_nullable_to_non_nullable
                      as int,
            availablePaise: null == availablePaise
                ? _value.availablePaise
                : availablePaise // ignore: cast_nullable_to_non_nullable
                      as int,
            advancedPaise: null == advancedPaise
                ? _value.advancedPaise
                : advancedPaise // ignore: cast_nullable_to_non_nullable
                      as int,
            reservedPaise: null == reservedPaise
                ? _value.reservedPaise
                : reservedPaise // ignore: cast_nullable_to_non_nullable
                      as int,
            settledPaise: null == settledPaise
                ? _value.settledPaise
                : settledPaise // ignore: cast_nullable_to_non_nullable
                      as int,
            lifetimeEarnedPaise: null == lifetimeEarnedPaise
                ? _value.lifetimeEarnedPaise
                : lifetimeEarnedPaise // ignore: cast_nullable_to_non_nullable
                      as int,
            lifetimeWithdrawnPaise: null == lifetimeWithdrawnPaise
                ? _value.lifetimeWithdrawnPaise
                : lifetimeWithdrawnPaise // ignore: cast_nullable_to_non_nullable
                      as int,
            currency: null == currency
                ? _value.currency
                : currency // ignore: cast_nullable_to_non_nullable
                      as String,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$WalletDtoImplCopyWith<$Res>
    implements $WalletDtoCopyWith<$Res> {
  factory _$$WalletDtoImplCopyWith(
    _$WalletDtoImpl value,
    $Res Function(_$WalletDtoImpl) then,
  ) = __$$WalletDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'collected_paise') int collectedPaise,
    @JsonKey(name: 'available_paise') int availablePaise,
    @JsonKey(name: 'advanced_paise') int advancedPaise,
    @JsonKey(name: 'reserved_paise') int reservedPaise,
    @JsonKey(name: 'settled_paise') int settledPaise,
    @JsonKey(name: 'lifetime_earned_paise') int lifetimeEarnedPaise,
    @JsonKey(name: 'lifetime_withdrawn_paise') int lifetimeWithdrawnPaise,
    String currency,
  });
}

/// @nodoc
class __$$WalletDtoImplCopyWithImpl<$Res>
    extends _$WalletDtoCopyWithImpl<$Res, _$WalletDtoImpl>
    implements _$$WalletDtoImplCopyWith<$Res> {
  __$$WalletDtoImplCopyWithImpl(
    _$WalletDtoImpl _value,
    $Res Function(_$WalletDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of WalletDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? orgId = null,
    Object? collectedPaise = null,
    Object? availablePaise = null,
    Object? advancedPaise = null,
    Object? reservedPaise = null,
    Object? settledPaise = null,
    Object? lifetimeEarnedPaise = null,
    Object? lifetimeWithdrawnPaise = null,
    Object? currency = null,
  }) {
    return _then(
      _$WalletDtoImpl(
        orgId: null == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String,
        collectedPaise: null == collectedPaise
            ? _value.collectedPaise
            : collectedPaise // ignore: cast_nullable_to_non_nullable
                  as int,
        availablePaise: null == availablePaise
            ? _value.availablePaise
            : availablePaise // ignore: cast_nullable_to_non_nullable
                  as int,
        advancedPaise: null == advancedPaise
            ? _value.advancedPaise
            : advancedPaise // ignore: cast_nullable_to_non_nullable
                  as int,
        reservedPaise: null == reservedPaise
            ? _value.reservedPaise
            : reservedPaise // ignore: cast_nullable_to_non_nullable
                  as int,
        settledPaise: null == settledPaise
            ? _value.settledPaise
            : settledPaise // ignore: cast_nullable_to_non_nullable
                  as int,
        lifetimeEarnedPaise: null == lifetimeEarnedPaise
            ? _value.lifetimeEarnedPaise
            : lifetimeEarnedPaise // ignore: cast_nullable_to_non_nullable
                  as int,
        lifetimeWithdrawnPaise: null == lifetimeWithdrawnPaise
            ? _value.lifetimeWithdrawnPaise
            : lifetimeWithdrawnPaise // ignore: cast_nullable_to_non_nullable
                  as int,
        currency: null == currency
            ? _value.currency
            : currency // ignore: cast_nullable_to_non_nullable
                  as String,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$WalletDtoImpl implements _WalletDto {
  const _$WalletDtoImpl({
    @JsonKey(name: 'org_id') required this.orgId,
    @JsonKey(name: 'collected_paise') this.collectedPaise = 0,
    @JsonKey(name: 'available_paise') this.availablePaise = 0,
    @JsonKey(name: 'advanced_paise') this.advancedPaise = 0,
    @JsonKey(name: 'reserved_paise') this.reservedPaise = 0,
    @JsonKey(name: 'settled_paise') this.settledPaise = 0,
    @JsonKey(name: 'lifetime_earned_paise') this.lifetimeEarnedPaise = 0,
    @JsonKey(name: 'lifetime_withdrawn_paise') this.lifetimeWithdrawnPaise = 0,
    this.currency = 'INR',
  });

  factory _$WalletDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$WalletDtoImplFromJson(json);

  @override
  @JsonKey(name: 'org_id')
  final String orgId;
  @override
  @JsonKey(name: 'collected_paise')
  final int collectedPaise;
  @override
  @JsonKey(name: 'available_paise')
  final int availablePaise;
  @override
  @JsonKey(name: 'advanced_paise')
  final int advancedPaise;
  @override
  @JsonKey(name: 'reserved_paise')
  final int reservedPaise;
  @override
  @JsonKey(name: 'settled_paise')
  final int settledPaise;
  @override
  @JsonKey(name: 'lifetime_earned_paise')
  final int lifetimeEarnedPaise;
  @override
  @JsonKey(name: 'lifetime_withdrawn_paise')
  final int lifetimeWithdrawnPaise;
  // One currency per wallet (V3 §9.1, D-257). The server has always sent it; nothing read it.
  @override
  @JsonKey()
  final String currency;

  @override
  String toString() {
    return 'WalletDto(orgId: $orgId, collectedPaise: $collectedPaise, availablePaise: $availablePaise, advancedPaise: $advancedPaise, reservedPaise: $reservedPaise, settledPaise: $settledPaise, lifetimeEarnedPaise: $lifetimeEarnedPaise, lifetimeWithdrawnPaise: $lifetimeWithdrawnPaise, currency: $currency)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$WalletDtoImpl &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.collectedPaise, collectedPaise) ||
                other.collectedPaise == collectedPaise) &&
            (identical(other.availablePaise, availablePaise) ||
                other.availablePaise == availablePaise) &&
            (identical(other.advancedPaise, advancedPaise) ||
                other.advancedPaise == advancedPaise) &&
            (identical(other.reservedPaise, reservedPaise) ||
                other.reservedPaise == reservedPaise) &&
            (identical(other.settledPaise, settledPaise) ||
                other.settledPaise == settledPaise) &&
            (identical(other.lifetimeEarnedPaise, lifetimeEarnedPaise) ||
                other.lifetimeEarnedPaise == lifetimeEarnedPaise) &&
            (identical(other.lifetimeWithdrawnPaise, lifetimeWithdrawnPaise) ||
                other.lifetimeWithdrawnPaise == lifetimeWithdrawnPaise) &&
            (identical(other.currency, currency) ||
                other.currency == currency));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    orgId,
    collectedPaise,
    availablePaise,
    advancedPaise,
    reservedPaise,
    settledPaise,
    lifetimeEarnedPaise,
    lifetimeWithdrawnPaise,
    currency,
  );

  /// Create a copy of WalletDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$WalletDtoImplCopyWith<_$WalletDtoImpl> get copyWith =>
      __$$WalletDtoImplCopyWithImpl<_$WalletDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$WalletDtoImplToJson(this);
  }
}

abstract class _WalletDto implements WalletDto {
  const factory _WalletDto({
    @JsonKey(name: 'org_id') required final String orgId,
    @JsonKey(name: 'collected_paise') final int collectedPaise,
    @JsonKey(name: 'available_paise') final int availablePaise,
    @JsonKey(name: 'advanced_paise') final int advancedPaise,
    @JsonKey(name: 'reserved_paise') final int reservedPaise,
    @JsonKey(name: 'settled_paise') final int settledPaise,
    @JsonKey(name: 'lifetime_earned_paise') final int lifetimeEarnedPaise,
    @JsonKey(name: 'lifetime_withdrawn_paise') final int lifetimeWithdrawnPaise,
    final String currency,
  }) = _$WalletDtoImpl;

  factory _WalletDto.fromJson(Map<String, dynamic> json) =
      _$WalletDtoImpl.fromJson;

  @override
  @JsonKey(name: 'org_id')
  String get orgId;
  @override
  @JsonKey(name: 'collected_paise')
  int get collectedPaise;
  @override
  @JsonKey(name: 'available_paise')
  int get availablePaise;
  @override
  @JsonKey(name: 'advanced_paise')
  int get advancedPaise;
  @override
  @JsonKey(name: 'reserved_paise')
  int get reservedPaise;
  @override
  @JsonKey(name: 'settled_paise')
  int get settledPaise;
  @override
  @JsonKey(name: 'lifetime_earned_paise')
  int get lifetimeEarnedPaise;
  @override
  @JsonKey(name: 'lifetime_withdrawn_paise')
  int get lifetimeWithdrawnPaise; // One currency per wallet (V3 §9.1, D-257). The server has always sent it; nothing read it.
  @override
  String get currency;

  /// Create a copy of WalletDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$WalletDtoImplCopyWith<_$WalletDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

RepresentationDto _$RepresentationDtoFromJson(Map<String, dynamic> json) {
  return _RepresentationDto.fromJson(json);
}

/// @nodoc
mixin _$RepresentationDto {
  @JsonKey(name: 'organization_id')
  String get organizationId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get slug => throw _privateConstructorUsedError;

  /// A storage key (`orgs/…`), not a URL — it needs presigning before it can be rendered.
  @JsonKey(name: 'logo_key')
  String? get logoKey => throw _privateConstructorUsedError;

  /// The caller's authority to act for this organization, lowercased — not a role over events.
  String get authority => throw _privateConstructorUsedError;

  /// Serializes this RepresentationDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of RepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $RepresentationDtoCopyWith<RepresentationDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $RepresentationDtoCopyWith<$Res> {
  factory $RepresentationDtoCopyWith(
    RepresentationDto value,
    $Res Function(RepresentationDto) then,
  ) = _$RepresentationDtoCopyWithImpl<$Res, RepresentationDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'organization_id') String organizationId,
    String name,
    String? slug,
    @JsonKey(name: 'logo_key') String? logoKey,
    String authority,
  });
}

/// @nodoc
class _$RepresentationDtoCopyWithImpl<$Res, $Val extends RepresentationDto>
    implements $RepresentationDtoCopyWith<$Res> {
  _$RepresentationDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of RepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? organizationId = null,
    Object? name = null,
    Object? slug = freezed,
    Object? logoKey = freezed,
    Object? authority = null,
  }) {
    return _then(
      _value.copyWith(
            organizationId: null == organizationId
                ? _value.organizationId
                : organizationId // ignore: cast_nullable_to_non_nullable
                      as String,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            slug: freezed == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String?,
            logoKey: freezed == logoKey
                ? _value.logoKey
                : logoKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            authority: null == authority
                ? _value.authority
                : authority // ignore: cast_nullable_to_non_nullable
                      as String,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$RepresentationDtoImplCopyWith<$Res>
    implements $RepresentationDtoCopyWith<$Res> {
  factory _$$RepresentationDtoImplCopyWith(
    _$RepresentationDtoImpl value,
    $Res Function(_$RepresentationDtoImpl) then,
  ) = __$$RepresentationDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'organization_id') String organizationId,
    String name,
    String? slug,
    @JsonKey(name: 'logo_key') String? logoKey,
    String authority,
  });
}

/// @nodoc
class __$$RepresentationDtoImplCopyWithImpl<$Res>
    extends _$RepresentationDtoCopyWithImpl<$Res, _$RepresentationDtoImpl>
    implements _$$RepresentationDtoImplCopyWith<$Res> {
  __$$RepresentationDtoImplCopyWithImpl(
    _$RepresentationDtoImpl _value,
    $Res Function(_$RepresentationDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of RepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? organizationId = null,
    Object? name = null,
    Object? slug = freezed,
    Object? logoKey = freezed,
    Object? authority = null,
  }) {
    return _then(
      _$RepresentationDtoImpl(
        organizationId: null == organizationId
            ? _value.organizationId
            : organizationId // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: freezed == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String?,
        logoKey: freezed == logoKey
            ? _value.logoKey
            : logoKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        authority: null == authority
            ? _value.authority
            : authority // ignore: cast_nullable_to_non_nullable
                  as String,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$RepresentationDtoImpl implements _RepresentationDto {
  const _$RepresentationDtoImpl({
    @JsonKey(name: 'organization_id') required this.organizationId,
    required this.name,
    this.slug,
    @JsonKey(name: 'logo_key') this.logoKey,
    required this.authority,
  });

  factory _$RepresentationDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$RepresentationDtoImplFromJson(json);

  @override
  @JsonKey(name: 'organization_id')
  final String organizationId;
  @override
  final String name;
  @override
  final String? slug;

  /// A storage key (`orgs/…`), not a URL — it needs presigning before it can be rendered.
  @override
  @JsonKey(name: 'logo_key')
  final String? logoKey;

  /// The caller's authority to act for this organization, lowercased — not a role over events.
  @override
  final String authority;

  @override
  String toString() {
    return 'RepresentationDto(organizationId: $organizationId, name: $name, slug: $slug, logoKey: $logoKey, authority: $authority)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$RepresentationDtoImpl &&
            (identical(other.organizationId, organizationId) ||
                other.organizationId == organizationId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.logoKey, logoKey) || other.logoKey == logoKey) &&
            (identical(other.authority, authority) ||
                other.authority == authority));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, organizationId, name, slug, logoKey, authority);

  /// Create a copy of RepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$RepresentationDtoImplCopyWith<_$RepresentationDtoImpl> get copyWith =>
      __$$RepresentationDtoImplCopyWithImpl<_$RepresentationDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$RepresentationDtoImplToJson(this);
  }
}

abstract class _RepresentationDto implements RepresentationDto {
  const factory _RepresentationDto({
    @JsonKey(name: 'organization_id') required final String organizationId,
    required final String name,
    final String? slug,
    @JsonKey(name: 'logo_key') final String? logoKey,
    required final String authority,
  }) = _$RepresentationDtoImpl;

  factory _RepresentationDto.fromJson(Map<String, dynamic> json) =
      _$RepresentationDtoImpl.fromJson;

  @override
  @JsonKey(name: 'organization_id')
  String get organizationId;
  @override
  String get name;
  @override
  String? get slug;

  /// A storage key (`orgs/…`), not a URL — it needs presigning before it can be rendered.
  @override
  @JsonKey(name: 'logo_key')
  String? get logoKey;

  /// The caller's authority to act for this organization, lowercased — not a role over events.
  @override
  String get authority;

  /// Create a copy of RepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$RepresentationDtoImplCopyWith<_$RepresentationDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
