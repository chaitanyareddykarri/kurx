// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'event_manage_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

EventManageDto _$EventManageDtoFromJson(Map<String, dynamic> json) {
  return _EventManageDto.fromJson(json);
}

/// @nodoc
mixin _$EventManageDto {
  String get id => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String? get slug => throw _privateConstructorUsedError;
  String get status =>
      throw _privateConstructorUsedError; // The organisation this event REPRESENTS — not its owner, which is the user in `created_by`
  // (D-268). Used to build the org-scoped management sub-resource URLs; the client never chooses
  // it, the event carries it. `org_id` is the deprecated D-273a alias, still read as a fallback so
  // this parses against a server deployed before the rename.
  @JsonKey(name: 'representing_org_id')
  String? get representingOrgId => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_id')
  String? get orgId => throw _privateConstructorUsedError;
  @JsonKey(name: 'starts_at')
  DateTime? get startsAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'ends_at')
  DateTime? get endsAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'tickets_sold')
  int get ticketsSold => throw _privateConstructorUsedError;
  int get capacity => throw _privateConstructorUsedError;
  @JsonKey(name: 'revenue_paise')
  int get revenuePaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'checked_in')
  int get checkedIn => throw _privateConstructorUsedError; // D-388 — the two facts that decide whether this event's details are frozen behind admin approval.
  // Defaulted rather than required, like everything else detail-only here: `GET /v1/me/events` rows
  // do not carry them, and a required field the row projection omits is what made every parse throw
  // before (see the class remarks).
  String get product => throw _privateConstructorUsedError;
  int get version =>
      throw _privateConstructorUsedError; // Detail-only (`ToEventJson`).
  @JsonKey(name: 'banner_key')
  String? get bannerKey => throw _privateConstructorUsedError;
  @JsonKey(name: 'view_count')
  int get viewCount => throw _privateConstructorUsedError; // Row-only: who the event REPRESENTS. Metadata, never a grouping key and never its owner (D-268).
  EventRepresentationDto? get representation =>
      throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this EventManageDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventManageDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventManageDtoCopyWith<EventManageDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventManageDtoCopyWith<$Res> {
  factory $EventManageDtoCopyWith(
    EventManageDto value,
    $Res Function(EventManageDto) then,
  ) = _$EventManageDtoCopyWithImpl<$Res, EventManageDto>;
  @useResult
  $Res call({
    String id,
    String title,
    String? slug,
    String status,
    @JsonKey(name: 'representing_org_id') String? representingOrgId,
    @JsonKey(name: 'org_id') String? orgId,
    @JsonKey(name: 'starts_at') DateTime? startsAt,
    @JsonKey(name: 'ends_at') DateTime? endsAt,
    @JsonKey(name: 'tickets_sold') int ticketsSold,
    int capacity,
    @JsonKey(name: 'revenue_paise') int revenuePaise,
    @JsonKey(name: 'checked_in') int checkedIn,
    String product,
    int version,
    @JsonKey(name: 'banner_key') String? bannerKey,
    @JsonKey(name: 'view_count') int viewCount,
    EventRepresentationDto? representation,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });

  $EventRepresentationDtoCopyWith<$Res>? get representation;
}

/// @nodoc
class _$EventManageDtoCopyWithImpl<$Res, $Val extends EventManageDto>
    implements $EventManageDtoCopyWith<$Res> {
  _$EventManageDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventManageDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = freezed,
    Object? status = null,
    Object? representingOrgId = freezed,
    Object? orgId = freezed,
    Object? startsAt = freezed,
    Object? endsAt = freezed,
    Object? ticketsSold = null,
    Object? capacity = null,
    Object? revenuePaise = null,
    Object? checkedIn = null,
    Object? product = null,
    Object? version = null,
    Object? bannerKey = freezed,
    Object? viewCount = null,
    Object? representation = freezed,
    Object? createdAt = freezed,
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
            slug: freezed == slug
                ? _value.slug
                : slug // ignore: cast_nullable_to_non_nullable
                      as String?,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            representingOrgId: freezed == representingOrgId
                ? _value.representingOrgId
                : representingOrgId // ignore: cast_nullable_to_non_nullable
                      as String?,
            orgId: freezed == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String?,
            startsAt: freezed == startsAt
                ? _value.startsAt
                : startsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            endsAt: freezed == endsAt
                ? _value.endsAt
                : endsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            ticketsSold: null == ticketsSold
                ? _value.ticketsSold
                : ticketsSold // ignore: cast_nullable_to_non_nullable
                      as int,
            capacity: null == capacity
                ? _value.capacity
                : capacity // ignore: cast_nullable_to_non_nullable
                      as int,
            revenuePaise: null == revenuePaise
                ? _value.revenuePaise
                : revenuePaise // ignore: cast_nullable_to_non_nullable
                      as int,
            checkedIn: null == checkedIn
                ? _value.checkedIn
                : checkedIn // ignore: cast_nullable_to_non_nullable
                      as int,
            product: null == product
                ? _value.product
                : product // ignore: cast_nullable_to_non_nullable
                      as String,
            version: null == version
                ? _value.version
                : version // ignore: cast_nullable_to_non_nullable
                      as int,
            bannerKey: freezed == bannerKey
                ? _value.bannerKey
                : bannerKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            viewCount: null == viewCount
                ? _value.viewCount
                : viewCount // ignore: cast_nullable_to_non_nullable
                      as int,
            representation: freezed == representation
                ? _value.representation
                : representation // ignore: cast_nullable_to_non_nullable
                      as EventRepresentationDto?,
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }

  /// Create a copy of EventManageDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $EventRepresentationDtoCopyWith<$Res>? get representation {
    if (_value.representation == null) {
      return null;
    }

    return $EventRepresentationDtoCopyWith<$Res>(_value.representation!, (
      value,
    ) {
      return _then(_value.copyWith(representation: value) as $Val);
    });
  }
}

/// @nodoc
abstract class _$$EventManageDtoImplCopyWith<$Res>
    implements $EventManageDtoCopyWith<$Res> {
  factory _$$EventManageDtoImplCopyWith(
    _$EventManageDtoImpl value,
    $Res Function(_$EventManageDtoImpl) then,
  ) = __$$EventManageDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String title,
    String? slug,
    String status,
    @JsonKey(name: 'representing_org_id') String? representingOrgId,
    @JsonKey(name: 'org_id') String? orgId,
    @JsonKey(name: 'starts_at') DateTime? startsAt,
    @JsonKey(name: 'ends_at') DateTime? endsAt,
    @JsonKey(name: 'tickets_sold') int ticketsSold,
    int capacity,
    @JsonKey(name: 'revenue_paise') int revenuePaise,
    @JsonKey(name: 'checked_in') int checkedIn,
    String product,
    int version,
    @JsonKey(name: 'banner_key') String? bannerKey,
    @JsonKey(name: 'view_count') int viewCount,
    EventRepresentationDto? representation,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });

  @override
  $EventRepresentationDtoCopyWith<$Res>? get representation;
}

/// @nodoc
class __$$EventManageDtoImplCopyWithImpl<$Res>
    extends _$EventManageDtoCopyWithImpl<$Res, _$EventManageDtoImpl>
    implements _$$EventManageDtoImplCopyWith<$Res> {
  __$$EventManageDtoImplCopyWithImpl(
    _$EventManageDtoImpl _value,
    $Res Function(_$EventManageDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventManageDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = freezed,
    Object? status = null,
    Object? representingOrgId = freezed,
    Object? orgId = freezed,
    Object? startsAt = freezed,
    Object? endsAt = freezed,
    Object? ticketsSold = null,
    Object? capacity = null,
    Object? revenuePaise = null,
    Object? checkedIn = null,
    Object? product = null,
    Object? version = null,
    Object? bannerKey = freezed,
    Object? viewCount = null,
    Object? representation = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$EventManageDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        title: null == title
            ? _value.title
            : title // ignore: cast_nullable_to_non_nullable
                  as String,
        slug: freezed == slug
            ? _value.slug
            : slug // ignore: cast_nullable_to_non_nullable
                  as String?,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        representingOrgId: freezed == representingOrgId
            ? _value.representingOrgId
            : representingOrgId // ignore: cast_nullable_to_non_nullable
                  as String?,
        orgId: freezed == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String?,
        startsAt: freezed == startsAt
            ? _value.startsAt
            : startsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        endsAt: freezed == endsAt
            ? _value.endsAt
            : endsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        ticketsSold: null == ticketsSold
            ? _value.ticketsSold
            : ticketsSold // ignore: cast_nullable_to_non_nullable
                  as int,
        capacity: null == capacity
            ? _value.capacity
            : capacity // ignore: cast_nullable_to_non_nullable
                  as int,
        revenuePaise: null == revenuePaise
            ? _value.revenuePaise
            : revenuePaise // ignore: cast_nullable_to_non_nullable
                  as int,
        checkedIn: null == checkedIn
            ? _value.checkedIn
            : checkedIn // ignore: cast_nullable_to_non_nullable
                  as int,
        product: null == product
            ? _value.product
            : product // ignore: cast_nullable_to_non_nullable
                  as String,
        version: null == version
            ? _value.version
            : version // ignore: cast_nullable_to_non_nullable
                  as int,
        bannerKey: freezed == bannerKey
            ? _value.bannerKey
            : bannerKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        viewCount: null == viewCount
            ? _value.viewCount
            : viewCount // ignore: cast_nullable_to_non_nullable
                  as int,
        representation: freezed == representation
            ? _value.representation
            : representation // ignore: cast_nullable_to_non_nullable
                  as EventRepresentationDto?,
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
class _$EventManageDtoImpl implements _EventManageDto {
  const _$EventManageDtoImpl({
    required this.id,
    required this.title,
    this.slug,
    required this.status,
    @JsonKey(name: 'representing_org_id') this.representingOrgId,
    @JsonKey(name: 'org_id') this.orgId,
    @JsonKey(name: 'starts_at') this.startsAt,
    @JsonKey(name: 'ends_at') this.endsAt,
    @JsonKey(name: 'tickets_sold') this.ticketsSold = 0,
    this.capacity = 0,
    @JsonKey(name: 'revenue_paise') this.revenuePaise = 0,
    @JsonKey(name: 'checked_in') this.checkedIn = 0,
    this.product = 'Public',
    this.version = 1,
    @JsonKey(name: 'banner_key') this.bannerKey,
    @JsonKey(name: 'view_count') this.viewCount = 0,
    this.representation,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$EventManageDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventManageDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String title;
  @override
  final String? slug;
  @override
  final String status;
  // The organisation this event REPRESENTS — not its owner, which is the user in `created_by`
  // (D-268). Used to build the org-scoped management sub-resource URLs; the client never chooses
  // it, the event carries it. `org_id` is the deprecated D-273a alias, still read as a fallback so
  // this parses against a server deployed before the rename.
  @override
  @JsonKey(name: 'representing_org_id')
  final String? representingOrgId;
  @override
  @JsonKey(name: 'org_id')
  final String? orgId;
  @override
  @JsonKey(name: 'starts_at')
  final DateTime? startsAt;
  @override
  @JsonKey(name: 'ends_at')
  final DateTime? endsAt;
  @override
  @JsonKey(name: 'tickets_sold')
  final int ticketsSold;
  @override
  @JsonKey()
  final int capacity;
  @override
  @JsonKey(name: 'revenue_paise')
  final int revenuePaise;
  @override
  @JsonKey(name: 'checked_in')
  final int checkedIn;
  // D-388 — the two facts that decide whether this event's details are frozen behind admin approval.
  // Defaulted rather than required, like everything else detail-only here: `GET /v1/me/events` rows
  // do not carry them, and a required field the row projection omits is what made every parse throw
  // before (see the class remarks).
  @override
  @JsonKey()
  final String product;
  @override
  @JsonKey()
  final int version;
  // Detail-only (`ToEventJson`).
  @override
  @JsonKey(name: 'banner_key')
  final String? bannerKey;
  @override
  @JsonKey(name: 'view_count')
  final int viewCount;
  // Row-only: who the event REPRESENTS. Metadata, never a grouping key and never its owner (D-268).
  @override
  final EventRepresentationDto? representation;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  @override
  String toString() {
    return 'EventManageDto(id: $id, title: $title, slug: $slug, status: $status, representingOrgId: $representingOrgId, orgId: $orgId, startsAt: $startsAt, endsAt: $endsAt, ticketsSold: $ticketsSold, capacity: $capacity, revenuePaise: $revenuePaise, checkedIn: $checkedIn, product: $product, version: $version, bannerKey: $bannerKey, viewCount: $viewCount, representation: $representation, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventManageDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.representingOrgId, representingOrgId) ||
                other.representingOrgId == representingOrgId) &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.startsAt, startsAt) ||
                other.startsAt == startsAt) &&
            (identical(other.endsAt, endsAt) || other.endsAt == endsAt) &&
            (identical(other.ticketsSold, ticketsSold) ||
                other.ticketsSold == ticketsSold) &&
            (identical(other.capacity, capacity) ||
                other.capacity == capacity) &&
            (identical(other.revenuePaise, revenuePaise) ||
                other.revenuePaise == revenuePaise) &&
            (identical(other.checkedIn, checkedIn) ||
                other.checkedIn == checkedIn) &&
            (identical(other.product, product) || other.product == product) &&
            (identical(other.version, version) || other.version == version) &&
            (identical(other.bannerKey, bannerKey) ||
                other.bannerKey == bannerKey) &&
            (identical(other.viewCount, viewCount) ||
                other.viewCount == viewCount) &&
            (identical(other.representation, representation) ||
                other.representation == representation) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    title,
    slug,
    status,
    representingOrgId,
    orgId,
    startsAt,
    endsAt,
    ticketsSold,
    capacity,
    revenuePaise,
    checkedIn,
    product,
    version,
    bannerKey,
    viewCount,
    representation,
    createdAt,
  );

  /// Create a copy of EventManageDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventManageDtoImplCopyWith<_$EventManageDtoImpl> get copyWith =>
      __$$EventManageDtoImplCopyWithImpl<_$EventManageDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventManageDtoImplToJson(this);
  }
}

abstract class _EventManageDto implements EventManageDto {
  const factory _EventManageDto({
    required final String id,
    required final String title,
    final String? slug,
    required final String status,
    @JsonKey(name: 'representing_org_id') final String? representingOrgId,
    @JsonKey(name: 'org_id') final String? orgId,
    @JsonKey(name: 'starts_at') final DateTime? startsAt,
    @JsonKey(name: 'ends_at') final DateTime? endsAt,
    @JsonKey(name: 'tickets_sold') final int ticketsSold,
    final int capacity,
    @JsonKey(name: 'revenue_paise') final int revenuePaise,
    @JsonKey(name: 'checked_in') final int checkedIn,
    final String product,
    final int version,
    @JsonKey(name: 'banner_key') final String? bannerKey,
    @JsonKey(name: 'view_count') final int viewCount,
    final EventRepresentationDto? representation,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$EventManageDtoImpl;

  factory _EventManageDto.fromJson(Map<String, dynamic> json) =
      _$EventManageDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get title;
  @override
  String? get slug;
  @override
  String get status; // The organisation this event REPRESENTS — not its owner, which is the user in `created_by`
  // (D-268). Used to build the org-scoped management sub-resource URLs; the client never chooses
  // it, the event carries it. `org_id` is the deprecated D-273a alias, still read as a fallback so
  // this parses against a server deployed before the rename.
  @override
  @JsonKey(name: 'representing_org_id')
  String? get representingOrgId;
  @override
  @JsonKey(name: 'org_id')
  String? get orgId;
  @override
  @JsonKey(name: 'starts_at')
  DateTime? get startsAt;
  @override
  @JsonKey(name: 'ends_at')
  DateTime? get endsAt;
  @override
  @JsonKey(name: 'tickets_sold')
  int get ticketsSold;
  @override
  int get capacity;
  @override
  @JsonKey(name: 'revenue_paise')
  int get revenuePaise;
  @override
  @JsonKey(name: 'checked_in')
  int get checkedIn; // D-388 — the two facts that decide whether this event's details are frozen behind admin approval.
  // Defaulted rather than required, like everything else detail-only here: `GET /v1/me/events` rows
  // do not carry them, and a required field the row projection omits is what made every parse throw
  // before (see the class remarks).
  @override
  String get product;
  @override
  int get version; // Detail-only (`ToEventJson`).
  @override
  @JsonKey(name: 'banner_key')
  String? get bannerKey;
  @override
  @JsonKey(name: 'view_count')
  int get viewCount; // Row-only: who the event REPRESENTS. Metadata, never a grouping key and never its owner (D-268).
  @override
  EventRepresentationDto? get representation;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;

  /// Create a copy of EventManageDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventManageDtoImplCopyWith<_$EventManageDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

AttendeeDto _$AttendeeDtoFromJson(Map<String, dynamic> json) {
  return _AttendeeDto.fromJson(json);
}

/// @nodoc
mixin _$AttendeeDto {
  String get ticketId => throw _privateConstructorUsedError;
  String get code => throw _privateConstructorUsedError;
  String get state => throw _privateConstructorUsedError;
  DateTime? get checkedInAt => throw _privateConstructorUsedError;
  String get buyerName => throw _privateConstructorUsedError;
  String get buyerPhone => throw _privateConstructorUsedError;
  String? get ticketTypeName => throw _privateConstructorUsedError;
  String? get groupDisplayName =>
      throw _privateConstructorUsedError; // Identity for the Professional Identity System (D-20x) — null for a guest ticket, or a
  // linked account whose profile isn't public.
  String? get buyerUserId => throw _privateConstructorUsedError;
  String? get buyerUsername => throw _privateConstructorUsedError;
  String? get buyerAvatarKey => throw _privateConstructorUsedError;

  /// Serializes this AttendeeDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of AttendeeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $AttendeeDtoCopyWith<AttendeeDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $AttendeeDtoCopyWith<$Res> {
  factory $AttendeeDtoCopyWith(
    AttendeeDto value,
    $Res Function(AttendeeDto) then,
  ) = _$AttendeeDtoCopyWithImpl<$Res, AttendeeDto>;
  @useResult
  $Res call({
    String ticketId,
    String code,
    String state,
    DateTime? checkedInAt,
    String buyerName,
    String buyerPhone,
    String? ticketTypeName,
    String? groupDisplayName,
    String? buyerUserId,
    String? buyerUsername,
    String? buyerAvatarKey,
  });
}

/// @nodoc
class _$AttendeeDtoCopyWithImpl<$Res, $Val extends AttendeeDto>
    implements $AttendeeDtoCopyWith<$Res> {
  _$AttendeeDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of AttendeeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? ticketId = null,
    Object? code = null,
    Object? state = null,
    Object? checkedInAt = freezed,
    Object? buyerName = null,
    Object? buyerPhone = null,
    Object? ticketTypeName = freezed,
    Object? groupDisplayName = freezed,
    Object? buyerUserId = freezed,
    Object? buyerUsername = freezed,
    Object? buyerAvatarKey = freezed,
  }) {
    return _then(
      _value.copyWith(
            ticketId: null == ticketId
                ? _value.ticketId
                : ticketId // ignore: cast_nullable_to_non_nullable
                      as String,
            code: null == code
                ? _value.code
                : code // ignore: cast_nullable_to_non_nullable
                      as String,
            state: null == state
                ? _value.state
                : state // ignore: cast_nullable_to_non_nullable
                      as String,
            checkedInAt: freezed == checkedInAt
                ? _value.checkedInAt
                : checkedInAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            buyerName: null == buyerName
                ? _value.buyerName
                : buyerName // ignore: cast_nullable_to_non_nullable
                      as String,
            buyerPhone: null == buyerPhone
                ? _value.buyerPhone
                : buyerPhone // ignore: cast_nullable_to_non_nullable
                      as String,
            ticketTypeName: freezed == ticketTypeName
                ? _value.ticketTypeName
                : ticketTypeName // ignore: cast_nullable_to_non_nullable
                      as String?,
            groupDisplayName: freezed == groupDisplayName
                ? _value.groupDisplayName
                : groupDisplayName // ignore: cast_nullable_to_non_nullable
                      as String?,
            buyerUserId: freezed == buyerUserId
                ? _value.buyerUserId
                : buyerUserId // ignore: cast_nullable_to_non_nullable
                      as String?,
            buyerUsername: freezed == buyerUsername
                ? _value.buyerUsername
                : buyerUsername // ignore: cast_nullable_to_non_nullable
                      as String?,
            buyerAvatarKey: freezed == buyerAvatarKey
                ? _value.buyerAvatarKey
                : buyerAvatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$AttendeeDtoImplCopyWith<$Res>
    implements $AttendeeDtoCopyWith<$Res> {
  factory _$$AttendeeDtoImplCopyWith(
    _$AttendeeDtoImpl value,
    $Res Function(_$AttendeeDtoImpl) then,
  ) = __$$AttendeeDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String ticketId,
    String code,
    String state,
    DateTime? checkedInAt,
    String buyerName,
    String buyerPhone,
    String? ticketTypeName,
    String? groupDisplayName,
    String? buyerUserId,
    String? buyerUsername,
    String? buyerAvatarKey,
  });
}

/// @nodoc
class __$$AttendeeDtoImplCopyWithImpl<$Res>
    extends _$AttendeeDtoCopyWithImpl<$Res, _$AttendeeDtoImpl>
    implements _$$AttendeeDtoImplCopyWith<$Res> {
  __$$AttendeeDtoImplCopyWithImpl(
    _$AttendeeDtoImpl _value,
    $Res Function(_$AttendeeDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of AttendeeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? ticketId = null,
    Object? code = null,
    Object? state = null,
    Object? checkedInAt = freezed,
    Object? buyerName = null,
    Object? buyerPhone = null,
    Object? ticketTypeName = freezed,
    Object? groupDisplayName = freezed,
    Object? buyerUserId = freezed,
    Object? buyerUsername = freezed,
    Object? buyerAvatarKey = freezed,
  }) {
    return _then(
      _$AttendeeDtoImpl(
        ticketId: null == ticketId
            ? _value.ticketId
            : ticketId // ignore: cast_nullable_to_non_nullable
                  as String,
        code: null == code
            ? _value.code
            : code // ignore: cast_nullable_to_non_nullable
                  as String,
        state: null == state
            ? _value.state
            : state // ignore: cast_nullable_to_non_nullable
                  as String,
        checkedInAt: freezed == checkedInAt
            ? _value.checkedInAt
            : checkedInAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        buyerName: null == buyerName
            ? _value.buyerName
            : buyerName // ignore: cast_nullable_to_non_nullable
                  as String,
        buyerPhone: null == buyerPhone
            ? _value.buyerPhone
            : buyerPhone // ignore: cast_nullable_to_non_nullable
                  as String,
        ticketTypeName: freezed == ticketTypeName
            ? _value.ticketTypeName
            : ticketTypeName // ignore: cast_nullable_to_non_nullable
                  as String?,
        groupDisplayName: freezed == groupDisplayName
            ? _value.groupDisplayName
            : groupDisplayName // ignore: cast_nullable_to_non_nullable
                  as String?,
        buyerUserId: freezed == buyerUserId
            ? _value.buyerUserId
            : buyerUserId // ignore: cast_nullable_to_non_nullable
                  as String?,
        buyerUsername: freezed == buyerUsername
            ? _value.buyerUsername
            : buyerUsername // ignore: cast_nullable_to_non_nullable
                  as String?,
        buyerAvatarKey: freezed == buyerAvatarKey
            ? _value.buyerAvatarKey
            : buyerAvatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$AttendeeDtoImpl implements _AttendeeDto {
  const _$AttendeeDtoImpl({
    required this.ticketId,
    required this.code,
    required this.state,
    this.checkedInAt,
    required this.buyerName,
    required this.buyerPhone,
    this.ticketTypeName,
    this.groupDisplayName,
    this.buyerUserId,
    this.buyerUsername,
    this.buyerAvatarKey,
  });

  factory _$AttendeeDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$AttendeeDtoImplFromJson(json);

  @override
  final String ticketId;
  @override
  final String code;
  @override
  final String state;
  @override
  final DateTime? checkedInAt;
  @override
  final String buyerName;
  @override
  final String buyerPhone;
  @override
  final String? ticketTypeName;
  @override
  final String? groupDisplayName;
  // Identity for the Professional Identity System (D-20x) — null for a guest ticket, or a
  // linked account whose profile isn't public.
  @override
  final String? buyerUserId;
  @override
  final String? buyerUsername;
  @override
  final String? buyerAvatarKey;

  @override
  String toString() {
    return 'AttendeeDto(ticketId: $ticketId, code: $code, state: $state, checkedInAt: $checkedInAt, buyerName: $buyerName, buyerPhone: $buyerPhone, ticketTypeName: $ticketTypeName, groupDisplayName: $groupDisplayName, buyerUserId: $buyerUserId, buyerUsername: $buyerUsername, buyerAvatarKey: $buyerAvatarKey)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$AttendeeDtoImpl &&
            (identical(other.ticketId, ticketId) ||
                other.ticketId == ticketId) &&
            (identical(other.code, code) || other.code == code) &&
            (identical(other.state, state) || other.state == state) &&
            (identical(other.checkedInAt, checkedInAt) ||
                other.checkedInAt == checkedInAt) &&
            (identical(other.buyerName, buyerName) ||
                other.buyerName == buyerName) &&
            (identical(other.buyerPhone, buyerPhone) ||
                other.buyerPhone == buyerPhone) &&
            (identical(other.ticketTypeName, ticketTypeName) ||
                other.ticketTypeName == ticketTypeName) &&
            (identical(other.groupDisplayName, groupDisplayName) ||
                other.groupDisplayName == groupDisplayName) &&
            (identical(other.buyerUserId, buyerUserId) ||
                other.buyerUserId == buyerUserId) &&
            (identical(other.buyerUsername, buyerUsername) ||
                other.buyerUsername == buyerUsername) &&
            (identical(other.buyerAvatarKey, buyerAvatarKey) ||
                other.buyerAvatarKey == buyerAvatarKey));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    ticketId,
    code,
    state,
    checkedInAt,
    buyerName,
    buyerPhone,
    ticketTypeName,
    groupDisplayName,
    buyerUserId,
    buyerUsername,
    buyerAvatarKey,
  );

  /// Create a copy of AttendeeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$AttendeeDtoImplCopyWith<_$AttendeeDtoImpl> get copyWith =>
      __$$AttendeeDtoImplCopyWithImpl<_$AttendeeDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$AttendeeDtoImplToJson(this);
  }
}

abstract class _AttendeeDto implements AttendeeDto {
  const factory _AttendeeDto({
    required final String ticketId,
    required final String code,
    required final String state,
    final DateTime? checkedInAt,
    required final String buyerName,
    required final String buyerPhone,
    final String? ticketTypeName,
    final String? groupDisplayName,
    final String? buyerUserId,
    final String? buyerUsername,
    final String? buyerAvatarKey,
  }) = _$AttendeeDtoImpl;

  factory _AttendeeDto.fromJson(Map<String, dynamic> json) =
      _$AttendeeDtoImpl.fromJson;

  @override
  String get ticketId;
  @override
  String get code;
  @override
  String get state;
  @override
  DateTime? get checkedInAt;
  @override
  String get buyerName;
  @override
  String get buyerPhone;
  @override
  String? get ticketTypeName;
  @override
  String? get groupDisplayName; // Identity for the Professional Identity System (D-20x) — null for a guest ticket, or a
  // linked account whose profile isn't public.
  @override
  String? get buyerUserId;
  @override
  String? get buyerUsername;
  @override
  String? get buyerAvatarKey;

  /// Create a copy of AttendeeDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$AttendeeDtoImplCopyWith<_$AttendeeDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

InvitationDto _$InvitationDtoFromJson(Map<String, dynamic> json) {
  return _InvitationDto.fromJson(json);
}

/// @nodoc
mixin _$InvitationDto {
  String get id => throw _privateConstructorUsedError;
  String? get email => throw _privateConstructorUsedError;
  String? get phone => throw _privateConstructorUsedError;
  String? get name => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'sent_at')
  DateTime get sentAt => throw _privateConstructorUsedError;

  /// Serializes this InvitationDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of InvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $InvitationDtoCopyWith<InvitationDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $InvitationDtoCopyWith<$Res> {
  factory $InvitationDtoCopyWith(
    InvitationDto value,
    $Res Function(InvitationDto) then,
  ) = _$InvitationDtoCopyWithImpl<$Res, InvitationDto>;
  @useResult
  $Res call({
    String id,
    String? email,
    String? phone,
    String? name,
    String status,
    @JsonKey(name: 'sent_at') DateTime sentAt,
  });
}

/// @nodoc
class _$InvitationDtoCopyWithImpl<$Res, $Val extends InvitationDto>
    implements $InvitationDtoCopyWith<$Res> {
  _$InvitationDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of InvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? email = freezed,
    Object? phone = freezed,
    Object? name = freezed,
    Object? status = null,
    Object? sentAt = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            email: freezed == email
                ? _value.email
                : email // ignore: cast_nullable_to_non_nullable
                      as String?,
            phone: freezed == phone
                ? _value.phone
                : phone // ignore: cast_nullable_to_non_nullable
                      as String?,
            name: freezed == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String?,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            sentAt: null == sentAt
                ? _value.sentAt
                : sentAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$InvitationDtoImplCopyWith<$Res>
    implements $InvitationDtoCopyWith<$Res> {
  factory _$$InvitationDtoImplCopyWith(
    _$InvitationDtoImpl value,
    $Res Function(_$InvitationDtoImpl) then,
  ) = __$$InvitationDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String? email,
    String? phone,
    String? name,
    String status,
    @JsonKey(name: 'sent_at') DateTime sentAt,
  });
}

/// @nodoc
class __$$InvitationDtoImplCopyWithImpl<$Res>
    extends _$InvitationDtoCopyWithImpl<$Res, _$InvitationDtoImpl>
    implements _$$InvitationDtoImplCopyWith<$Res> {
  __$$InvitationDtoImplCopyWithImpl(
    _$InvitationDtoImpl _value,
    $Res Function(_$InvitationDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of InvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? email = freezed,
    Object? phone = freezed,
    Object? name = freezed,
    Object? status = null,
    Object? sentAt = null,
  }) {
    return _then(
      _$InvitationDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        email: freezed == email
            ? _value.email
            : email // ignore: cast_nullable_to_non_nullable
                  as String?,
        phone: freezed == phone
            ? _value.phone
            : phone // ignore: cast_nullable_to_non_nullable
                  as String?,
        name: freezed == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String?,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        sentAt: null == sentAt
            ? _value.sentAt
            : sentAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$InvitationDtoImpl implements _InvitationDto {
  const _$InvitationDtoImpl({
    required this.id,
    this.email,
    this.phone,
    this.name,
    required this.status,
    @JsonKey(name: 'sent_at') required this.sentAt,
  });

  factory _$InvitationDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$InvitationDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String? email;
  @override
  final String? phone;
  @override
  final String? name;
  @override
  final String status;
  @override
  @JsonKey(name: 'sent_at')
  final DateTime sentAt;

  @override
  String toString() {
    return 'InvitationDto(id: $id, email: $email, phone: $phone, name: $name, status: $status, sentAt: $sentAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$InvitationDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.email, email) || other.email == email) &&
            (identical(other.phone, phone) || other.phone == phone) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.sentAt, sentAt) || other.sentAt == sentAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, id, email, phone, name, status, sentAt);

  /// Create a copy of InvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$InvitationDtoImplCopyWith<_$InvitationDtoImpl> get copyWith =>
      __$$InvitationDtoImplCopyWithImpl<_$InvitationDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$InvitationDtoImplToJson(this);
  }
}

abstract class _InvitationDto implements InvitationDto {
  const factory _InvitationDto({
    required final String id,
    final String? email,
    final String? phone,
    final String? name,
    required final String status,
    @JsonKey(name: 'sent_at') required final DateTime sentAt,
  }) = _$InvitationDtoImpl;

  factory _InvitationDto.fromJson(Map<String, dynamic> json) =
      _$InvitationDtoImpl.fromJson;

  @override
  String get id;
  @override
  String? get email;
  @override
  String? get phone;
  @override
  String? get name;
  @override
  String get status;
  @override
  @JsonKey(name: 'sent_at')
  DateTime get sentAt;

  /// Create a copy of InvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$InvitationDtoImplCopyWith<_$InvitationDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

AnnouncementDto _$AnnouncementDtoFromJson(Map<String, dynamic> json) {
  return _AnnouncementDto.fromJson(json);
}

/// @nodoc
mixin _$AnnouncementDto {
  String get id => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String? get body => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'open_count')
  int get openCount => throw _privateConstructorUsedError;
  @JsonKey(name: 'click_count')
  int get clickCount => throw _privateConstructorUsedError;
  @JsonKey(name: 'sent_at')
  DateTime? get sentAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime get createdAt => throw _privateConstructorUsedError;

  /// Serializes this AnnouncementDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of AnnouncementDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $AnnouncementDtoCopyWith<AnnouncementDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $AnnouncementDtoCopyWith<$Res> {
  factory $AnnouncementDtoCopyWith(
    AnnouncementDto value,
    $Res Function(AnnouncementDto) then,
  ) = _$AnnouncementDtoCopyWithImpl<$Res, AnnouncementDto>;
  @useResult
  $Res call({
    String id,
    String title,
    String? body,
    String status,
    @JsonKey(name: 'open_count') int openCount,
    @JsonKey(name: 'click_count') int clickCount,
    @JsonKey(name: 'sent_at') DateTime? sentAt,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class _$AnnouncementDtoCopyWithImpl<$Res, $Val extends AnnouncementDto>
    implements $AnnouncementDtoCopyWith<$Res> {
  _$AnnouncementDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of AnnouncementDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? body = freezed,
    Object? status = null,
    Object? openCount = null,
    Object? clickCount = null,
    Object? sentAt = freezed,
    Object? createdAt = null,
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
            body: freezed == body
                ? _value.body
                : body // ignore: cast_nullable_to_non_nullable
                      as String?,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            openCount: null == openCount
                ? _value.openCount
                : openCount // ignore: cast_nullable_to_non_nullable
                      as int,
            clickCount: null == clickCount
                ? _value.clickCount
                : clickCount // ignore: cast_nullable_to_non_nullable
                      as int,
            sentAt: freezed == sentAt
                ? _value.sentAt
                : sentAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
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
abstract class _$$AnnouncementDtoImplCopyWith<$Res>
    implements $AnnouncementDtoCopyWith<$Res> {
  factory _$$AnnouncementDtoImplCopyWith(
    _$AnnouncementDtoImpl value,
    $Res Function(_$AnnouncementDtoImpl) then,
  ) = __$$AnnouncementDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String title,
    String? body,
    String status,
    @JsonKey(name: 'open_count') int openCount,
    @JsonKey(name: 'click_count') int clickCount,
    @JsonKey(name: 'sent_at') DateTime? sentAt,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class __$$AnnouncementDtoImplCopyWithImpl<$Res>
    extends _$AnnouncementDtoCopyWithImpl<$Res, _$AnnouncementDtoImpl>
    implements _$$AnnouncementDtoImplCopyWith<$Res> {
  __$$AnnouncementDtoImplCopyWithImpl(
    _$AnnouncementDtoImpl _value,
    $Res Function(_$AnnouncementDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of AnnouncementDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? body = freezed,
    Object? status = null,
    Object? openCount = null,
    Object? clickCount = null,
    Object? sentAt = freezed,
    Object? createdAt = null,
  }) {
    return _then(
      _$AnnouncementDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        title: null == title
            ? _value.title
            : title // ignore: cast_nullable_to_non_nullable
                  as String,
        body: freezed == body
            ? _value.body
            : body // ignore: cast_nullable_to_non_nullable
                  as String?,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        openCount: null == openCount
            ? _value.openCount
            : openCount // ignore: cast_nullable_to_non_nullable
                  as int,
        clickCount: null == clickCount
            ? _value.clickCount
            : clickCount // ignore: cast_nullable_to_non_nullable
                  as int,
        sentAt: freezed == sentAt
            ? _value.sentAt
            : sentAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
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
class _$AnnouncementDtoImpl implements _AnnouncementDto {
  const _$AnnouncementDtoImpl({
    required this.id,
    required this.title,
    this.body,
    required this.status,
    @JsonKey(name: 'open_count') this.openCount = 0,
    @JsonKey(name: 'click_count') this.clickCount = 0,
    @JsonKey(name: 'sent_at') this.sentAt,
    @JsonKey(name: 'created_at') required this.createdAt,
  });

  factory _$AnnouncementDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$AnnouncementDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String title;
  @override
  final String? body;
  @override
  final String status;
  @override
  @JsonKey(name: 'open_count')
  final int openCount;
  @override
  @JsonKey(name: 'click_count')
  final int clickCount;
  @override
  @JsonKey(name: 'sent_at')
  final DateTime? sentAt;
  @override
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  @override
  String toString() {
    return 'AnnouncementDto(id: $id, title: $title, body: $body, status: $status, openCount: $openCount, clickCount: $clickCount, sentAt: $sentAt, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$AnnouncementDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.body, body) || other.body == body) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.openCount, openCount) ||
                other.openCount == openCount) &&
            (identical(other.clickCount, clickCount) ||
                other.clickCount == clickCount) &&
            (identical(other.sentAt, sentAt) || other.sentAt == sentAt) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    title,
    body,
    status,
    openCount,
    clickCount,
    sentAt,
    createdAt,
  );

  /// Create a copy of AnnouncementDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$AnnouncementDtoImplCopyWith<_$AnnouncementDtoImpl> get copyWith =>
      __$$AnnouncementDtoImplCopyWithImpl<_$AnnouncementDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$AnnouncementDtoImplToJson(this);
  }
}

abstract class _AnnouncementDto implements AnnouncementDto {
  const factory _AnnouncementDto({
    required final String id,
    required final String title,
    final String? body,
    required final String status,
    @JsonKey(name: 'open_count') final int openCount,
    @JsonKey(name: 'click_count') final int clickCount,
    @JsonKey(name: 'sent_at') final DateTime? sentAt,
    @JsonKey(name: 'created_at') required final DateTime createdAt,
  }) = _$AnnouncementDtoImpl;

  factory _AnnouncementDto.fromJson(Map<String, dynamic> json) =
      _$AnnouncementDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get title;
  @override
  String? get body;
  @override
  String get status;
  @override
  @JsonKey(name: 'open_count')
  int get openCount;
  @override
  @JsonKey(name: 'click_count')
  int get clickCount;
  @override
  @JsonKey(name: 'sent_at')
  DateTime? get sentAt;
  @override
  @JsonKey(name: 'created_at')
  DateTime get createdAt;

  /// Create a copy of AnnouncementDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$AnnouncementDtoImplCopyWith<_$AnnouncementDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

AnalyticsDto _$AnalyticsDtoFromJson(Map<String, dynamic> json) {
  return _AnalyticsDto.fromJson(json);
}

/// @nodoc
mixin _$AnalyticsDto {
  @JsonKey(name: 'total_revenue_paise')
  int get totalRevenuePaise => throw _privateConstructorUsedError;
  @JsonKey(name: 'tickets_sold')
  int get ticketsSold => throw _privateConstructorUsedError;
  @JsonKey(name: 'attendance_rate')
  double get attendanceRate => throw _privateConstructorUsedError;
  @JsonKey(name: 'total_views')
  int get totalViews => throw _privateConstructorUsedError;

  /// Serializes this AnalyticsDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of AnalyticsDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $AnalyticsDtoCopyWith<AnalyticsDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $AnalyticsDtoCopyWith<$Res> {
  factory $AnalyticsDtoCopyWith(
    AnalyticsDto value,
    $Res Function(AnalyticsDto) then,
  ) = _$AnalyticsDtoCopyWithImpl<$Res, AnalyticsDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'total_revenue_paise') int totalRevenuePaise,
    @JsonKey(name: 'tickets_sold') int ticketsSold,
    @JsonKey(name: 'attendance_rate') double attendanceRate,
    @JsonKey(name: 'total_views') int totalViews,
  });
}

/// @nodoc
class _$AnalyticsDtoCopyWithImpl<$Res, $Val extends AnalyticsDto>
    implements $AnalyticsDtoCopyWith<$Res> {
  _$AnalyticsDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of AnalyticsDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? totalRevenuePaise = null,
    Object? ticketsSold = null,
    Object? attendanceRate = null,
    Object? totalViews = null,
  }) {
    return _then(
      _value.copyWith(
            totalRevenuePaise: null == totalRevenuePaise
                ? _value.totalRevenuePaise
                : totalRevenuePaise // ignore: cast_nullable_to_non_nullable
                      as int,
            ticketsSold: null == ticketsSold
                ? _value.ticketsSold
                : ticketsSold // ignore: cast_nullable_to_non_nullable
                      as int,
            attendanceRate: null == attendanceRate
                ? _value.attendanceRate
                : attendanceRate // ignore: cast_nullable_to_non_nullable
                      as double,
            totalViews: null == totalViews
                ? _value.totalViews
                : totalViews // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$AnalyticsDtoImplCopyWith<$Res>
    implements $AnalyticsDtoCopyWith<$Res> {
  factory _$$AnalyticsDtoImplCopyWith(
    _$AnalyticsDtoImpl value,
    $Res Function(_$AnalyticsDtoImpl) then,
  ) = __$$AnalyticsDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'total_revenue_paise') int totalRevenuePaise,
    @JsonKey(name: 'tickets_sold') int ticketsSold,
    @JsonKey(name: 'attendance_rate') double attendanceRate,
    @JsonKey(name: 'total_views') int totalViews,
  });
}

/// @nodoc
class __$$AnalyticsDtoImplCopyWithImpl<$Res>
    extends _$AnalyticsDtoCopyWithImpl<$Res, _$AnalyticsDtoImpl>
    implements _$$AnalyticsDtoImplCopyWith<$Res> {
  __$$AnalyticsDtoImplCopyWithImpl(
    _$AnalyticsDtoImpl _value,
    $Res Function(_$AnalyticsDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of AnalyticsDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? totalRevenuePaise = null,
    Object? ticketsSold = null,
    Object? attendanceRate = null,
    Object? totalViews = null,
  }) {
    return _then(
      _$AnalyticsDtoImpl(
        totalRevenuePaise: null == totalRevenuePaise
            ? _value.totalRevenuePaise
            : totalRevenuePaise // ignore: cast_nullable_to_non_nullable
                  as int,
        ticketsSold: null == ticketsSold
            ? _value.ticketsSold
            : ticketsSold // ignore: cast_nullable_to_non_nullable
                  as int,
        attendanceRate: null == attendanceRate
            ? _value.attendanceRate
            : attendanceRate // ignore: cast_nullable_to_non_nullable
                  as double,
        totalViews: null == totalViews
            ? _value.totalViews
            : totalViews // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$AnalyticsDtoImpl implements _AnalyticsDto {
  const _$AnalyticsDtoImpl({
    @JsonKey(name: 'total_revenue_paise') this.totalRevenuePaise = 0,
    @JsonKey(name: 'tickets_sold') this.ticketsSold = 0,
    @JsonKey(name: 'attendance_rate') this.attendanceRate = 0.0,
    @JsonKey(name: 'total_views') this.totalViews = 0,
  });

  factory _$AnalyticsDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$AnalyticsDtoImplFromJson(json);

  @override
  @JsonKey(name: 'total_revenue_paise')
  final int totalRevenuePaise;
  @override
  @JsonKey(name: 'tickets_sold')
  final int ticketsSold;
  @override
  @JsonKey(name: 'attendance_rate')
  final double attendanceRate;
  @override
  @JsonKey(name: 'total_views')
  final int totalViews;

  @override
  String toString() {
    return 'AnalyticsDto(totalRevenuePaise: $totalRevenuePaise, ticketsSold: $ticketsSold, attendanceRate: $attendanceRate, totalViews: $totalViews)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$AnalyticsDtoImpl &&
            (identical(other.totalRevenuePaise, totalRevenuePaise) ||
                other.totalRevenuePaise == totalRevenuePaise) &&
            (identical(other.ticketsSold, ticketsSold) ||
                other.ticketsSold == ticketsSold) &&
            (identical(other.attendanceRate, attendanceRate) ||
                other.attendanceRate == attendanceRate) &&
            (identical(other.totalViews, totalViews) ||
                other.totalViews == totalViews));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    totalRevenuePaise,
    ticketsSold,
    attendanceRate,
    totalViews,
  );

  /// Create a copy of AnalyticsDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$AnalyticsDtoImplCopyWith<_$AnalyticsDtoImpl> get copyWith =>
      __$$AnalyticsDtoImplCopyWithImpl<_$AnalyticsDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$AnalyticsDtoImplToJson(this);
  }
}

abstract class _AnalyticsDto implements AnalyticsDto {
  const factory _AnalyticsDto({
    @JsonKey(name: 'total_revenue_paise') final int totalRevenuePaise,
    @JsonKey(name: 'tickets_sold') final int ticketsSold,
    @JsonKey(name: 'attendance_rate') final double attendanceRate,
    @JsonKey(name: 'total_views') final int totalViews,
  }) = _$AnalyticsDtoImpl;

  factory _AnalyticsDto.fromJson(Map<String, dynamic> json) =
      _$AnalyticsDtoImpl.fromJson;

  @override
  @JsonKey(name: 'total_revenue_paise')
  int get totalRevenuePaise;
  @override
  @JsonKey(name: 'tickets_sold')
  int get ticketsSold;
  @override
  @JsonKey(name: 'attendance_rate')
  double get attendanceRate;
  @override
  @JsonKey(name: 'total_views')
  int get totalViews;

  /// Create a copy of AnalyticsDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$AnalyticsDtoImplCopyWith<_$AnalyticsDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

AssignmentDto _$AssignmentDtoFromJson(Map<String, dynamic> json) {
  return _AssignmentDto.fromJson(json);
}

/// @nodoc
mixin _$AssignmentDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_id')
  String get orgId => throw _privateConstructorUsedError;
  @JsonKey(name: 'user_id')
  String get userId => throw _privateConstructorUsedError;
  String get role => throw _privateConstructorUsedError;
  @JsonKey(name: 'custom_role')
  String? get customRole => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'show_on_profile')
  bool get showOnProfile => throw _privateConstructorUsedError;
  String? get notes => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime get createdAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'assignee_name')
  String get assigneeName => throw _privateConstructorUsedError;
  @JsonKey(name: 'assignee_username')
  String? get assigneeUsername => throw _privateConstructorUsedError;
  @JsonKey(name: 'assignee_avatar_key')
  String? get assigneeAvatarKey => throw _privateConstructorUsedError; // D-319 — event context. The organiser's roster already knows which event it is looking at; the
  // invitee's own inbox is the one that needs this, and both read this DTO. Defaulted rather than
  // required so an older API build still deserialises rather than throwing on every row.
  @JsonKey(name: 'event_title')
  String get eventTitle => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_slug')
  String? get eventSlug => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_starts_at')
  DateTime? get eventStartsAt => throw _privateConstructorUsedError; // Null for a self-represented event, which names no organisation at all (D-268). Render nothing —
  // never a fallback label, which would reintroduce the "personal organization" concept.
  @JsonKey(name: 'representing_org_name')
  String? get representingOrgName => throw _privateConstructorUsedError;

  /// Serializes this AssignmentDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of AssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $AssignmentDtoCopyWith<AssignmentDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $AssignmentDtoCopyWith<$Res> {
  factory $AssignmentDtoCopyWith(
    AssignmentDto value,
    $Res Function(AssignmentDto) then,
  ) = _$AssignmentDtoCopyWithImpl<$Res, AssignmentDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'user_id') String userId,
    String role,
    @JsonKey(name: 'custom_role') String? customRole,
    String status,
    @JsonKey(name: 'show_on_profile') bool showOnProfile,
    String? notes,
    @JsonKey(name: 'created_at') DateTime createdAt,
    @JsonKey(name: 'assignee_name') String assigneeName,
    @JsonKey(name: 'assignee_username') String? assigneeUsername,
    @JsonKey(name: 'assignee_avatar_key') String? assigneeAvatarKey,
    @JsonKey(name: 'event_title') String eventTitle,
    @JsonKey(name: 'event_slug') String? eventSlug,
    @JsonKey(name: 'event_starts_at') DateTime? eventStartsAt,
    @JsonKey(name: 'representing_org_name') String? representingOrgName,
  });
}

/// @nodoc
class _$AssignmentDtoCopyWithImpl<$Res, $Val extends AssignmentDto>
    implements $AssignmentDtoCopyWith<$Res> {
  _$AssignmentDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of AssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? orgId = null,
    Object? userId = null,
    Object? role = null,
    Object? customRole = freezed,
    Object? status = null,
    Object? showOnProfile = null,
    Object? notes = freezed,
    Object? createdAt = null,
    Object? assigneeName = null,
    Object? assigneeUsername = freezed,
    Object? assigneeAvatarKey = freezed,
    Object? eventTitle = null,
    Object? eventSlug = freezed,
    Object? eventStartsAt = freezed,
    Object? representingOrgName = freezed,
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
            orgId: null == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String,
            userId: null == userId
                ? _value.userId
                : userId // ignore: cast_nullable_to_non_nullable
                      as String,
            role: null == role
                ? _value.role
                : role // ignore: cast_nullable_to_non_nullable
                      as String,
            customRole: freezed == customRole
                ? _value.customRole
                : customRole // ignore: cast_nullable_to_non_nullable
                      as String?,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            showOnProfile: null == showOnProfile
                ? _value.showOnProfile
                : showOnProfile // ignore: cast_nullable_to_non_nullable
                      as bool,
            notes: freezed == notes
                ? _value.notes
                : notes // ignore: cast_nullable_to_non_nullable
                      as String?,
            createdAt: null == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            assigneeName: null == assigneeName
                ? _value.assigneeName
                : assigneeName // ignore: cast_nullable_to_non_nullable
                      as String,
            assigneeUsername: freezed == assigneeUsername
                ? _value.assigneeUsername
                : assigneeUsername // ignore: cast_nullable_to_non_nullable
                      as String?,
            assigneeAvatarKey: freezed == assigneeAvatarKey
                ? _value.assigneeAvatarKey
                : assigneeAvatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
            eventTitle: null == eventTitle
                ? _value.eventTitle
                : eventTitle // ignore: cast_nullable_to_non_nullable
                      as String,
            eventSlug: freezed == eventSlug
                ? _value.eventSlug
                : eventSlug // ignore: cast_nullable_to_non_nullable
                      as String?,
            eventStartsAt: freezed == eventStartsAt
                ? _value.eventStartsAt
                : eventStartsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            representingOrgName: freezed == representingOrgName
                ? _value.representingOrgName
                : representingOrgName // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$AssignmentDtoImplCopyWith<$Res>
    implements $AssignmentDtoCopyWith<$Res> {
  factory _$$AssignmentDtoImplCopyWith(
    _$AssignmentDtoImpl value,
    $Res Function(_$AssignmentDtoImpl) then,
  ) = __$$AssignmentDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'user_id') String userId,
    String role,
    @JsonKey(name: 'custom_role') String? customRole,
    String status,
    @JsonKey(name: 'show_on_profile') bool showOnProfile,
    String? notes,
    @JsonKey(name: 'created_at') DateTime createdAt,
    @JsonKey(name: 'assignee_name') String assigneeName,
    @JsonKey(name: 'assignee_username') String? assigneeUsername,
    @JsonKey(name: 'assignee_avatar_key') String? assigneeAvatarKey,
    @JsonKey(name: 'event_title') String eventTitle,
    @JsonKey(name: 'event_slug') String? eventSlug,
    @JsonKey(name: 'event_starts_at') DateTime? eventStartsAt,
    @JsonKey(name: 'representing_org_name') String? representingOrgName,
  });
}

/// @nodoc
class __$$AssignmentDtoImplCopyWithImpl<$Res>
    extends _$AssignmentDtoCopyWithImpl<$Res, _$AssignmentDtoImpl>
    implements _$$AssignmentDtoImplCopyWith<$Res> {
  __$$AssignmentDtoImplCopyWithImpl(
    _$AssignmentDtoImpl _value,
    $Res Function(_$AssignmentDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of AssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? orgId = null,
    Object? userId = null,
    Object? role = null,
    Object? customRole = freezed,
    Object? status = null,
    Object? showOnProfile = null,
    Object? notes = freezed,
    Object? createdAt = null,
    Object? assigneeName = null,
    Object? assigneeUsername = freezed,
    Object? assigneeAvatarKey = freezed,
    Object? eventTitle = null,
    Object? eventSlug = freezed,
    Object? eventStartsAt = freezed,
    Object? representingOrgName = freezed,
  }) {
    return _then(
      _$AssignmentDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: null == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String,
        orgId: null == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String,
        userId: null == userId
            ? _value.userId
            : userId // ignore: cast_nullable_to_non_nullable
                  as String,
        role: null == role
            ? _value.role
            : role // ignore: cast_nullable_to_non_nullable
                  as String,
        customRole: freezed == customRole
            ? _value.customRole
            : customRole // ignore: cast_nullable_to_non_nullable
                  as String?,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        showOnProfile: null == showOnProfile
            ? _value.showOnProfile
            : showOnProfile // ignore: cast_nullable_to_non_nullable
                  as bool,
        notes: freezed == notes
            ? _value.notes
            : notes // ignore: cast_nullable_to_non_nullable
                  as String?,
        createdAt: null == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        assigneeName: null == assigneeName
            ? _value.assigneeName
            : assigneeName // ignore: cast_nullable_to_non_nullable
                  as String,
        assigneeUsername: freezed == assigneeUsername
            ? _value.assigneeUsername
            : assigneeUsername // ignore: cast_nullable_to_non_nullable
                  as String?,
        assigneeAvatarKey: freezed == assigneeAvatarKey
            ? _value.assigneeAvatarKey
            : assigneeAvatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
        eventTitle: null == eventTitle
            ? _value.eventTitle
            : eventTitle // ignore: cast_nullable_to_non_nullable
                  as String,
        eventSlug: freezed == eventSlug
            ? _value.eventSlug
            : eventSlug // ignore: cast_nullable_to_non_nullable
                  as String?,
        eventStartsAt: freezed == eventStartsAt
            ? _value.eventStartsAt
            : eventStartsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        representingOrgName: freezed == representingOrgName
            ? _value.representingOrgName
            : representingOrgName // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$AssignmentDtoImpl implements _AssignmentDto {
  const _$AssignmentDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'org_id') required this.orgId,
    @JsonKey(name: 'user_id') required this.userId,
    required this.role,
    @JsonKey(name: 'custom_role') this.customRole,
    required this.status,
    @JsonKey(name: 'show_on_profile') this.showOnProfile = false,
    this.notes,
    @JsonKey(name: 'created_at') required this.createdAt,
    @JsonKey(name: 'assignee_name') required this.assigneeName,
    @JsonKey(name: 'assignee_username') this.assigneeUsername,
    @JsonKey(name: 'assignee_avatar_key') this.assigneeAvatarKey,
    @JsonKey(name: 'event_title') this.eventTitle = '',
    @JsonKey(name: 'event_slug') this.eventSlug,
    @JsonKey(name: 'event_starts_at') this.eventStartsAt,
    @JsonKey(name: 'representing_org_name') this.representingOrgName,
  });

  factory _$AssignmentDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$AssignmentDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'org_id')
  final String orgId;
  @override
  @JsonKey(name: 'user_id')
  final String userId;
  @override
  final String role;
  @override
  @JsonKey(name: 'custom_role')
  final String? customRole;
  @override
  final String status;
  @override
  @JsonKey(name: 'show_on_profile')
  final bool showOnProfile;
  @override
  final String? notes;
  @override
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @override
  @JsonKey(name: 'assignee_name')
  final String assigneeName;
  @override
  @JsonKey(name: 'assignee_username')
  final String? assigneeUsername;
  @override
  @JsonKey(name: 'assignee_avatar_key')
  final String? assigneeAvatarKey;
  // D-319 — event context. The organiser's roster already knows which event it is looking at; the
  // invitee's own inbox is the one that needs this, and both read this DTO. Defaulted rather than
  // required so an older API build still deserialises rather than throwing on every row.
  @override
  @JsonKey(name: 'event_title')
  final String eventTitle;
  @override
  @JsonKey(name: 'event_slug')
  final String? eventSlug;
  @override
  @JsonKey(name: 'event_starts_at')
  final DateTime? eventStartsAt;
  // Null for a self-represented event, which names no organisation at all (D-268). Render nothing —
  // never a fallback label, which would reintroduce the "personal organization" concept.
  @override
  @JsonKey(name: 'representing_org_name')
  final String? representingOrgName;

  @override
  String toString() {
    return 'AssignmentDto(id: $id, eventId: $eventId, orgId: $orgId, userId: $userId, role: $role, customRole: $customRole, status: $status, showOnProfile: $showOnProfile, notes: $notes, createdAt: $createdAt, assigneeName: $assigneeName, assigneeUsername: $assigneeUsername, assigneeAvatarKey: $assigneeAvatarKey, eventTitle: $eventTitle, eventSlug: $eventSlug, eventStartsAt: $eventStartsAt, representingOrgName: $representingOrgName)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$AssignmentDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.userId, userId) || other.userId == userId) &&
            (identical(other.role, role) || other.role == role) &&
            (identical(other.customRole, customRole) ||
                other.customRole == customRole) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.showOnProfile, showOnProfile) ||
                other.showOnProfile == showOnProfile) &&
            (identical(other.notes, notes) || other.notes == notes) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt) &&
            (identical(other.assigneeName, assigneeName) ||
                other.assigneeName == assigneeName) &&
            (identical(other.assigneeUsername, assigneeUsername) ||
                other.assigneeUsername == assigneeUsername) &&
            (identical(other.assigneeAvatarKey, assigneeAvatarKey) ||
                other.assigneeAvatarKey == assigneeAvatarKey) &&
            (identical(other.eventTitle, eventTitle) ||
                other.eventTitle == eventTitle) &&
            (identical(other.eventSlug, eventSlug) ||
                other.eventSlug == eventSlug) &&
            (identical(other.eventStartsAt, eventStartsAt) ||
                other.eventStartsAt == eventStartsAt) &&
            (identical(other.representingOrgName, representingOrgName) ||
                other.representingOrgName == representingOrgName));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    orgId,
    userId,
    role,
    customRole,
    status,
    showOnProfile,
    notes,
    createdAt,
    assigneeName,
    assigneeUsername,
    assigneeAvatarKey,
    eventTitle,
    eventSlug,
    eventStartsAt,
    representingOrgName,
  );

  /// Create a copy of AssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$AssignmentDtoImplCopyWith<_$AssignmentDtoImpl> get copyWith =>
      __$$AssignmentDtoImplCopyWithImpl<_$AssignmentDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$AssignmentDtoImplToJson(this);
  }
}

abstract class _AssignmentDto implements AssignmentDto {
  const factory _AssignmentDto({
    required final String id,
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'org_id') required final String orgId,
    @JsonKey(name: 'user_id') required final String userId,
    required final String role,
    @JsonKey(name: 'custom_role') final String? customRole,
    required final String status,
    @JsonKey(name: 'show_on_profile') final bool showOnProfile,
    final String? notes,
    @JsonKey(name: 'created_at') required final DateTime createdAt,
    @JsonKey(name: 'assignee_name') required final String assigneeName,
    @JsonKey(name: 'assignee_username') final String? assigneeUsername,
    @JsonKey(name: 'assignee_avatar_key') final String? assigneeAvatarKey,
    @JsonKey(name: 'event_title') final String eventTitle,
    @JsonKey(name: 'event_slug') final String? eventSlug,
    @JsonKey(name: 'event_starts_at') final DateTime? eventStartsAt,
    @JsonKey(name: 'representing_org_name') final String? representingOrgName,
  }) = _$AssignmentDtoImpl;

  factory _AssignmentDto.fromJson(Map<String, dynamic> json) =
      _$AssignmentDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'org_id')
  String get orgId;
  @override
  @JsonKey(name: 'user_id')
  String get userId;
  @override
  String get role;
  @override
  @JsonKey(name: 'custom_role')
  String? get customRole;
  @override
  String get status;
  @override
  @JsonKey(name: 'show_on_profile')
  bool get showOnProfile;
  @override
  String? get notes;
  @override
  @JsonKey(name: 'created_at')
  DateTime get createdAt;
  @override
  @JsonKey(name: 'assignee_name')
  String get assigneeName;
  @override
  @JsonKey(name: 'assignee_username')
  String? get assigneeUsername;
  @override
  @JsonKey(name: 'assignee_avatar_key')
  String? get assigneeAvatarKey; // D-319 — event context. The organiser's roster already knows which event it is looking at; the
  // invitee's own inbox is the one that needs this, and both read this DTO. Defaulted rather than
  // required so an older API build still deserialises rather than throwing on every row.
  @override
  @JsonKey(name: 'event_title')
  String get eventTitle;
  @override
  @JsonKey(name: 'event_slug')
  String? get eventSlug;
  @override
  @JsonKey(name: 'event_starts_at')
  DateTime? get eventStartsAt; // Null for a self-represented event, which names no organisation at all (D-268). Render nothing —
  // never a fallback label, which would reintroduce the "personal organization" concept.
  @override
  @JsonKey(name: 'representing_org_name')
  String? get representingOrgName;

  /// Create a copy of AssignmentDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$AssignmentDtoImplCopyWith<_$AssignmentDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

RegistrationDto _$RegistrationDtoFromJson(Map<String, dynamic> json) {
  return _RegistrationDto.fromJson(json);
}

/// @nodoc
mixin _$RegistrationDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'ticket_type_id')
  String? get ticketTypeId => throw _privateConstructorUsedError;
  @JsonKey(name: 'subject_type')
  String get subjectType => throw _privateConstructorUsedError;
  @JsonKey(name: 'subject_id')
  String? get subjectId => throw _privateConstructorUsedError;
  @JsonKey(name: 'order_id')
  String? get orderId => throw _privateConstructorUsedError;

  /// `pending` / `confirmed` / `waitlisted` / `cancelled`, lowercased for display.
  String get state => throw _privateConstructorUsedError;
  @JsonKey(name: 'admission_count')
  int get admissionCount => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this RegistrationDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of RegistrationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $RegistrationDtoCopyWith<RegistrationDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $RegistrationDtoCopyWith<$Res> {
  factory $RegistrationDtoCopyWith(
    RegistrationDto value,
    $Res Function(RegistrationDto) then,
  ) = _$RegistrationDtoCopyWithImpl<$Res, RegistrationDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String? ticketTypeId,
    @JsonKey(name: 'subject_type') String subjectType,
    @JsonKey(name: 'subject_id') String? subjectId,
    @JsonKey(name: 'order_id') String? orderId,
    String state,
    @JsonKey(name: 'admission_count') int admissionCount,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class _$RegistrationDtoCopyWithImpl<$Res, $Val extends RegistrationDto>
    implements $RegistrationDtoCopyWith<$Res> {
  _$RegistrationDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of RegistrationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = freezed,
    Object? subjectType = null,
    Object? subjectId = freezed,
    Object? orderId = freezed,
    Object? state = null,
    Object? admissionCount = null,
    Object? createdAt = freezed,
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
            subjectType: null == subjectType
                ? _value.subjectType
                : subjectType // ignore: cast_nullable_to_non_nullable
                      as String,
            subjectId: freezed == subjectId
                ? _value.subjectId
                : subjectId // ignore: cast_nullable_to_non_nullable
                      as String?,
            orderId: freezed == orderId
                ? _value.orderId
                : orderId // ignore: cast_nullable_to_non_nullable
                      as String?,
            state: null == state
                ? _value.state
                : state // ignore: cast_nullable_to_non_nullable
                      as String,
            admissionCount: null == admissionCount
                ? _value.admissionCount
                : admissionCount // ignore: cast_nullable_to_non_nullable
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
abstract class _$$RegistrationDtoImplCopyWith<$Res>
    implements $RegistrationDtoCopyWith<$Res> {
  factory _$$RegistrationDtoImplCopyWith(
    _$RegistrationDtoImpl value,
    $Res Function(_$RegistrationDtoImpl) then,
  ) = __$$RegistrationDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String? ticketTypeId,
    @JsonKey(name: 'subject_type') String subjectType,
    @JsonKey(name: 'subject_id') String? subjectId,
    @JsonKey(name: 'order_id') String? orderId,
    String state,
    @JsonKey(name: 'admission_count') int admissionCount,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class __$$RegistrationDtoImplCopyWithImpl<$Res>
    extends _$RegistrationDtoCopyWithImpl<$Res, _$RegistrationDtoImpl>
    implements _$$RegistrationDtoImplCopyWith<$Res> {
  __$$RegistrationDtoImplCopyWithImpl(
    _$RegistrationDtoImpl _value,
    $Res Function(_$RegistrationDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of RegistrationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = freezed,
    Object? subjectType = null,
    Object? subjectId = freezed,
    Object? orderId = freezed,
    Object? state = null,
    Object? admissionCount = null,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$RegistrationDtoImpl(
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
        subjectType: null == subjectType
            ? _value.subjectType
            : subjectType // ignore: cast_nullable_to_non_nullable
                  as String,
        subjectId: freezed == subjectId
            ? _value.subjectId
            : subjectId // ignore: cast_nullable_to_non_nullable
                  as String?,
        orderId: freezed == orderId
            ? _value.orderId
            : orderId // ignore: cast_nullable_to_non_nullable
                  as String?,
        state: null == state
            ? _value.state
            : state // ignore: cast_nullable_to_non_nullable
                  as String,
        admissionCount: null == admissionCount
            ? _value.admissionCount
            : admissionCount // ignore: cast_nullable_to_non_nullable
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
class _$RegistrationDtoImpl implements _RegistrationDto {
  const _$RegistrationDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'ticket_type_id') this.ticketTypeId,
    @JsonKey(name: 'subject_type') this.subjectType = 'Person',
    @JsonKey(name: 'subject_id') this.subjectId,
    @JsonKey(name: 'order_id') this.orderId,
    this.state = 'pending',
    @JsonKey(name: 'admission_count') this.admissionCount = 0,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$RegistrationDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$RegistrationDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  final String? ticketTypeId;
  @override
  @JsonKey(name: 'subject_type')
  final String subjectType;
  @override
  @JsonKey(name: 'subject_id')
  final String? subjectId;
  @override
  @JsonKey(name: 'order_id')
  final String? orderId;

  /// `pending` / `confirmed` / `waitlisted` / `cancelled`, lowercased for display.
  @override
  @JsonKey()
  final String state;
  @override
  @JsonKey(name: 'admission_count')
  final int admissionCount;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  @override
  String toString() {
    return 'RegistrationDto(id: $id, eventId: $eventId, ticketTypeId: $ticketTypeId, subjectType: $subjectType, subjectId: $subjectId, orderId: $orderId, state: $state, admissionCount: $admissionCount, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$RegistrationDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.ticketTypeId, ticketTypeId) ||
                other.ticketTypeId == ticketTypeId) &&
            (identical(other.subjectType, subjectType) ||
                other.subjectType == subjectType) &&
            (identical(other.subjectId, subjectId) ||
                other.subjectId == subjectId) &&
            (identical(other.orderId, orderId) || other.orderId == orderId) &&
            (identical(other.state, state) || other.state == state) &&
            (identical(other.admissionCount, admissionCount) ||
                other.admissionCount == admissionCount) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    ticketTypeId,
    subjectType,
    subjectId,
    orderId,
    state,
    admissionCount,
    createdAt,
  );

  /// Create a copy of RegistrationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$RegistrationDtoImplCopyWith<_$RegistrationDtoImpl> get copyWith =>
      __$$RegistrationDtoImplCopyWithImpl<_$RegistrationDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$RegistrationDtoImplToJson(this);
  }
}

abstract class _RegistrationDto implements RegistrationDto {
  const factory _RegistrationDto({
    required final String id,
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'ticket_type_id') final String? ticketTypeId,
    @JsonKey(name: 'subject_type') final String subjectType,
    @JsonKey(name: 'subject_id') final String? subjectId,
    @JsonKey(name: 'order_id') final String? orderId,
    final String state,
    @JsonKey(name: 'admission_count') final int admissionCount,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$RegistrationDtoImpl;

  factory _RegistrationDto.fromJson(Map<String, dynamic> json) =
      _$RegistrationDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  String? get ticketTypeId;
  @override
  @JsonKey(name: 'subject_type')
  String get subjectType;
  @override
  @JsonKey(name: 'subject_id')
  String? get subjectId;
  @override
  @JsonKey(name: 'order_id')
  String? get orderId;

  /// `pending` / `confirmed` / `waitlisted` / `cancelled`, lowercased for display.
  @override
  String get state;
  @override
  @JsonKey(name: 'admission_count')
  int get admissionCount;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;

  /// Create a copy of RegistrationDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$RegistrationDtoImplCopyWith<_$RegistrationDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EventRepresentationDto _$EventRepresentationDtoFromJson(
  Map<String, dynamic> json,
) {
  return _EventRepresentationDto.fromJson(json);
}

/// @nodoc
mixin _$EventRepresentationDto {
  String get kind => throw _privateConstructorUsedError;
  @JsonKey(name: 'organization_id')
  String? get organizationId => throw _privateConstructorUsedError;
  @JsonKey(name: 'organization_name')
  String? get organizationName => throw _privateConstructorUsedError;
  bool get verified => throw _privateConstructorUsedError;

  /// Serializes this EventRepresentationDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventRepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventRepresentationDtoCopyWith<EventRepresentationDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventRepresentationDtoCopyWith<$Res> {
  factory $EventRepresentationDtoCopyWith(
    EventRepresentationDto value,
    $Res Function(EventRepresentationDto) then,
  ) = _$EventRepresentationDtoCopyWithImpl<$Res, EventRepresentationDto>;
  @useResult
  $Res call({
    String kind,
    @JsonKey(name: 'organization_id') String? organizationId,
    @JsonKey(name: 'organization_name') String? organizationName,
    bool verified,
  });
}

/// @nodoc
class _$EventRepresentationDtoCopyWithImpl<
  $Res,
  $Val extends EventRepresentationDto
>
    implements $EventRepresentationDtoCopyWith<$Res> {
  _$EventRepresentationDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventRepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? kind = null,
    Object? organizationId = freezed,
    Object? organizationName = freezed,
    Object? verified = null,
  }) {
    return _then(
      _value.copyWith(
            kind: null == kind
                ? _value.kind
                : kind // ignore: cast_nullable_to_non_nullable
                      as String,
            organizationId: freezed == organizationId
                ? _value.organizationId
                : organizationId // ignore: cast_nullable_to_non_nullable
                      as String?,
            organizationName: freezed == organizationName
                ? _value.organizationName
                : organizationName // ignore: cast_nullable_to_non_nullable
                      as String?,
            verified: null == verified
                ? _value.verified
                : verified // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EventRepresentationDtoImplCopyWith<$Res>
    implements $EventRepresentationDtoCopyWith<$Res> {
  factory _$$EventRepresentationDtoImplCopyWith(
    _$EventRepresentationDtoImpl value,
    $Res Function(_$EventRepresentationDtoImpl) then,
  ) = __$$EventRepresentationDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String kind,
    @JsonKey(name: 'organization_id') String? organizationId,
    @JsonKey(name: 'organization_name') String? organizationName,
    bool verified,
  });
}

/// @nodoc
class __$$EventRepresentationDtoImplCopyWithImpl<$Res>
    extends
        _$EventRepresentationDtoCopyWithImpl<$Res, _$EventRepresentationDtoImpl>
    implements _$$EventRepresentationDtoImplCopyWith<$Res> {
  __$$EventRepresentationDtoImplCopyWithImpl(
    _$EventRepresentationDtoImpl _value,
    $Res Function(_$EventRepresentationDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventRepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? kind = null,
    Object? organizationId = freezed,
    Object? organizationName = freezed,
    Object? verified = null,
  }) {
    return _then(
      _$EventRepresentationDtoImpl(
        kind: null == kind
            ? _value.kind
            : kind // ignore: cast_nullable_to_non_nullable
                  as String,
        organizationId: freezed == organizationId
            ? _value.organizationId
            : organizationId // ignore: cast_nullable_to_non_nullable
                  as String?,
        organizationName: freezed == organizationName
            ? _value.organizationName
            : organizationName // ignore: cast_nullable_to_non_nullable
                  as String?,
        verified: null == verified
            ? _value.verified
            : verified // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventRepresentationDtoImpl implements _EventRepresentationDto {
  const _$EventRepresentationDtoImpl({
    required this.kind,
    @JsonKey(name: 'organization_id') this.organizationId,
    @JsonKey(name: 'organization_name') this.organizationName,
    this.verified = false,
  });

  factory _$EventRepresentationDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventRepresentationDtoImplFromJson(json);

  @override
  final String kind;
  @override
  @JsonKey(name: 'organization_id')
  final String? organizationId;
  @override
  @JsonKey(name: 'organization_name')
  final String? organizationName;
  @override
  @JsonKey()
  final bool verified;

  @override
  String toString() {
    return 'EventRepresentationDto(kind: $kind, organizationId: $organizationId, organizationName: $organizationName, verified: $verified)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventRepresentationDtoImpl &&
            (identical(other.kind, kind) || other.kind == kind) &&
            (identical(other.organizationId, organizationId) ||
                other.organizationId == organizationId) &&
            (identical(other.organizationName, organizationName) ||
                other.organizationName == organizationName) &&
            (identical(other.verified, verified) ||
                other.verified == verified));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    kind,
    organizationId,
    organizationName,
    verified,
  );

  /// Create a copy of EventRepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventRepresentationDtoImplCopyWith<_$EventRepresentationDtoImpl>
  get copyWith =>
      __$$EventRepresentationDtoImplCopyWithImpl<_$EventRepresentationDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventRepresentationDtoImplToJson(this);
  }
}

abstract class _EventRepresentationDto implements EventRepresentationDto {
  const factory _EventRepresentationDto({
    required final String kind,
    @JsonKey(name: 'organization_id') final String? organizationId,
    @JsonKey(name: 'organization_name') final String? organizationName,
    final bool verified,
  }) = _$EventRepresentationDtoImpl;

  factory _EventRepresentationDto.fromJson(Map<String, dynamic> json) =
      _$EventRepresentationDtoImpl.fromJson;

  @override
  String get kind;
  @override
  @JsonKey(name: 'organization_id')
  String? get organizationId;
  @override
  @JsonKey(name: 'organization_name')
  String? get organizationName;
  @override
  bool get verified;

  /// Create a copy of EventRepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventRepresentationDtoImplCopyWith<_$EventRepresentationDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}
