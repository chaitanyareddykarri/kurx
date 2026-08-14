// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'event_detail_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

VenueDto _$VenueDtoFromJson(Map<String, dynamic> json) {
  return _VenueDto.fromJson(json);
}

/// @nodoc
mixin _$VenueDto {
  String? get name => throw _privateConstructorUsedError;
  String? get address => throw _privateConstructorUsedError;
  String? get city => throw _privateConstructorUsedError;
  @JsonKey(name: 'google_maps_url')
  String? get googleMapsUrl => throw _privateConstructorUsedError;

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
    String? name,
    String? address,
    String? city,
    @JsonKey(name: 'google_maps_url') String? googleMapsUrl,
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
    Object? name = freezed,
    Object? address = freezed,
    Object? city = freezed,
    Object? googleMapsUrl = freezed,
  }) {
    return _then(
      _value.copyWith(
            name: freezed == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String?,
            address: freezed == address
                ? _value.address
                : address // ignore: cast_nullable_to_non_nullable
                      as String?,
            city: freezed == city
                ? _value.city
                : city // ignore: cast_nullable_to_non_nullable
                      as String?,
            googleMapsUrl: freezed == googleMapsUrl
                ? _value.googleMapsUrl
                : googleMapsUrl // ignore: cast_nullable_to_non_nullable
                      as String?,
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
    String? name,
    String? address,
    String? city,
    @JsonKey(name: 'google_maps_url') String? googleMapsUrl,
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
    Object? name = freezed,
    Object? address = freezed,
    Object? city = freezed,
    Object? googleMapsUrl = freezed,
  }) {
    return _then(
      _$VenueDtoImpl(
        name: freezed == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String?,
        address: freezed == address
            ? _value.address
            : address // ignore: cast_nullable_to_non_nullable
                  as String?,
        city: freezed == city
            ? _value.city
            : city // ignore: cast_nullable_to_non_nullable
                  as String?,
        googleMapsUrl: freezed == googleMapsUrl
            ? _value.googleMapsUrl
            : googleMapsUrl // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$VenueDtoImpl implements _VenueDto {
  const _$VenueDtoImpl({
    this.name,
    this.address,
    this.city,
    @JsonKey(name: 'google_maps_url') this.googleMapsUrl,
  });

  factory _$VenueDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$VenueDtoImplFromJson(json);

  @override
  final String? name;
  @override
  final String? address;
  @override
  final String? city;
  @override
  @JsonKey(name: 'google_maps_url')
  final String? googleMapsUrl;

  @override
  String toString() {
    return 'VenueDto(name: $name, address: $address, city: $city, googleMapsUrl: $googleMapsUrl)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$VenueDtoImpl &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.address, address) || other.address == address) &&
            (identical(other.city, city) || other.city == city) &&
            (identical(other.googleMapsUrl, googleMapsUrl) ||
                other.googleMapsUrl == googleMapsUrl));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, name, address, city, googleMapsUrl);

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
    final String? name,
    final String? address,
    final String? city,
    @JsonKey(name: 'google_maps_url') final String? googleMapsUrl,
  }) = _$VenueDtoImpl;

  factory _VenueDto.fromJson(Map<String, dynamic> json) =
      _$VenueDtoImpl.fromJson;

  @override
  String? get name;
  @override
  String? get address;
  @override
  String? get city;
  @override
  @JsonKey(name: 'google_maps_url')
  String? get googleMapsUrl;

  /// Create a copy of VenueDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$VenueDtoImplCopyWith<_$VenueDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EventDetailDto _$EventDetailDtoFromJson(Map<String, dynamic> json) {
  return _EventDetailDto.fromJson(json);
}

/// @nodoc
mixin _$EventDetailDto {
  String get id => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String get slug => throw _privateConstructorUsedError;
  String? get subtitle => throw _privateConstructorUsedError;
  String? get description => throw _privateConstructorUsedError;
  List<String> get tags => throw _privateConstructorUsedError;
  VenueDto? get venue => throw _privateConstructorUsedError;
  @JsonKey(name: 'starts_at')
  DateTime? get startsAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'ends_at')
  DateTime? get endsAt => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'view_count')
  int get viewCount => throw _privateConstructorUsedError;
  @JsonKey(name: 'banner_url')
  String? get bannerUrl => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_mode')
  String? get eventMode => throw _privateConstructorUsedError;
  @JsonKey(name: 'online_url')
  String? get onlineUrl => throw _privateConstructorUsedError;
  List<EventMediaDto> get media => throw _privateConstructorUsedError;
  EventRepresentationDto? get representing =>
      throw _privateConstructorUsedError;

  /// Serializes this EventDetailDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventDetailDtoCopyWith<EventDetailDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventDetailDtoCopyWith<$Res> {
  factory $EventDetailDtoCopyWith(
    EventDetailDto value,
    $Res Function(EventDetailDto) then,
  ) = _$EventDetailDtoCopyWithImpl<$Res, EventDetailDto>;
  @useResult
  $Res call({
    String id,
    String title,
    String slug,
    String? subtitle,
    String? description,
    List<String> tags,
    VenueDto? venue,
    @JsonKey(name: 'starts_at') DateTime? startsAt,
    @JsonKey(name: 'ends_at') DateTime? endsAt,
    String status,
    @JsonKey(name: 'view_count') int viewCount,
    @JsonKey(name: 'banner_url') String? bannerUrl,
    @JsonKey(name: 'event_mode') String? eventMode,
    @JsonKey(name: 'online_url') String? onlineUrl,
    List<EventMediaDto> media,
    EventRepresentationDto? representing,
  });

  $VenueDtoCopyWith<$Res>? get venue;
  $EventRepresentationDtoCopyWith<$Res>? get representing;
}

/// @nodoc
class _$EventDetailDtoCopyWithImpl<$Res, $Val extends EventDetailDto>
    implements $EventDetailDtoCopyWith<$Res> {
  _$EventDetailDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = null,
    Object? subtitle = freezed,
    Object? description = freezed,
    Object? tags = null,
    Object? venue = freezed,
    Object? startsAt = freezed,
    Object? endsAt = freezed,
    Object? status = null,
    Object? viewCount = null,
    Object? bannerUrl = freezed,
    Object? eventMode = freezed,
    Object? onlineUrl = freezed,
    Object? media = null,
    Object? representing = freezed,
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
            subtitle: freezed == subtitle
                ? _value.subtitle
                : subtitle // ignore: cast_nullable_to_non_nullable
                      as String?,
            description: freezed == description
                ? _value.description
                : description // ignore: cast_nullable_to_non_nullable
                      as String?,
            tags: null == tags
                ? _value.tags
                : tags // ignore: cast_nullable_to_non_nullable
                      as List<String>,
            venue: freezed == venue
                ? _value.venue
                : venue // ignore: cast_nullable_to_non_nullable
                      as VenueDto?,
            startsAt: freezed == startsAt
                ? _value.startsAt
                : startsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            endsAt: freezed == endsAt
                ? _value.endsAt
                : endsAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            viewCount: null == viewCount
                ? _value.viewCount
                : viewCount // ignore: cast_nullable_to_non_nullable
                      as int,
            bannerUrl: freezed == bannerUrl
                ? _value.bannerUrl
                : bannerUrl // ignore: cast_nullable_to_non_nullable
                      as String?,
            eventMode: freezed == eventMode
                ? _value.eventMode
                : eventMode // ignore: cast_nullable_to_non_nullable
                      as String?,
            onlineUrl: freezed == onlineUrl
                ? _value.onlineUrl
                : onlineUrl // ignore: cast_nullable_to_non_nullable
                      as String?,
            media: null == media
                ? _value.media
                : media // ignore: cast_nullable_to_non_nullable
                      as List<EventMediaDto>,
            representing: freezed == representing
                ? _value.representing
                : representing // ignore: cast_nullable_to_non_nullable
                      as EventRepresentationDto?,
          )
          as $Val,
    );
  }

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $VenueDtoCopyWith<$Res>? get venue {
    if (_value.venue == null) {
      return null;
    }

    return $VenueDtoCopyWith<$Res>(_value.venue!, (value) {
      return _then(_value.copyWith(venue: value) as $Val);
    });
  }

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $EventRepresentationDtoCopyWith<$Res>? get representing {
    if (_value.representing == null) {
      return null;
    }

    return $EventRepresentationDtoCopyWith<$Res>(_value.representing!, (value) {
      return _then(_value.copyWith(representing: value) as $Val);
    });
  }
}

/// @nodoc
abstract class _$$EventDetailDtoImplCopyWith<$Res>
    implements $EventDetailDtoCopyWith<$Res> {
  factory _$$EventDetailDtoImplCopyWith(
    _$EventDetailDtoImpl value,
    $Res Function(_$EventDetailDtoImpl) then,
  ) = __$$EventDetailDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String title,
    String slug,
    String? subtitle,
    String? description,
    List<String> tags,
    VenueDto? venue,
    @JsonKey(name: 'starts_at') DateTime? startsAt,
    @JsonKey(name: 'ends_at') DateTime? endsAt,
    String status,
    @JsonKey(name: 'view_count') int viewCount,
    @JsonKey(name: 'banner_url') String? bannerUrl,
    @JsonKey(name: 'event_mode') String? eventMode,
    @JsonKey(name: 'online_url') String? onlineUrl,
    List<EventMediaDto> media,
    EventRepresentationDto? representing,
  });

  @override
  $VenueDtoCopyWith<$Res>? get venue;
  @override
  $EventRepresentationDtoCopyWith<$Res>? get representing;
}

/// @nodoc
class __$$EventDetailDtoImplCopyWithImpl<$Res>
    extends _$EventDetailDtoCopyWithImpl<$Res, _$EventDetailDtoImpl>
    implements _$$EventDetailDtoImplCopyWith<$Res> {
  __$$EventDetailDtoImplCopyWithImpl(
    _$EventDetailDtoImpl _value,
    $Res Function(_$EventDetailDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = null,
    Object? subtitle = freezed,
    Object? description = freezed,
    Object? tags = null,
    Object? venue = freezed,
    Object? startsAt = freezed,
    Object? endsAt = freezed,
    Object? status = null,
    Object? viewCount = null,
    Object? bannerUrl = freezed,
    Object? eventMode = freezed,
    Object? onlineUrl = freezed,
    Object? media = null,
    Object? representing = freezed,
  }) {
    return _then(
      _$EventDetailDtoImpl(
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
        subtitle: freezed == subtitle
            ? _value.subtitle
            : subtitle // ignore: cast_nullable_to_non_nullable
                  as String?,
        description: freezed == description
            ? _value.description
            : description // ignore: cast_nullable_to_non_nullable
                  as String?,
        tags: null == tags
            ? _value._tags
            : tags // ignore: cast_nullable_to_non_nullable
                  as List<String>,
        venue: freezed == venue
            ? _value.venue
            : venue // ignore: cast_nullable_to_non_nullable
                  as VenueDto?,
        startsAt: freezed == startsAt
            ? _value.startsAt
            : startsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        endsAt: freezed == endsAt
            ? _value.endsAt
            : endsAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        viewCount: null == viewCount
            ? _value.viewCount
            : viewCount // ignore: cast_nullable_to_non_nullable
                  as int,
        bannerUrl: freezed == bannerUrl
            ? _value.bannerUrl
            : bannerUrl // ignore: cast_nullable_to_non_nullable
                  as String?,
        eventMode: freezed == eventMode
            ? _value.eventMode
            : eventMode // ignore: cast_nullable_to_non_nullable
                  as String?,
        onlineUrl: freezed == onlineUrl
            ? _value.onlineUrl
            : onlineUrl // ignore: cast_nullable_to_non_nullable
                  as String?,
        media: null == media
            ? _value._media
            : media // ignore: cast_nullable_to_non_nullable
                  as List<EventMediaDto>,
        representing: freezed == representing
            ? _value.representing
            : representing // ignore: cast_nullable_to_non_nullable
                  as EventRepresentationDto?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventDetailDtoImpl extends _EventDetailDto {
  const _$EventDetailDtoImpl({
    required this.id,
    required this.title,
    required this.slug,
    this.subtitle,
    this.description,
    final List<String> tags = const <String>[],
    this.venue,
    @JsonKey(name: 'starts_at') this.startsAt,
    @JsonKey(name: 'ends_at') this.endsAt,
    this.status = '',
    @JsonKey(name: 'view_count') this.viewCount = 0,
    @JsonKey(name: 'banner_url') this.bannerUrl,
    @JsonKey(name: 'event_mode') this.eventMode,
    @JsonKey(name: 'online_url') this.onlineUrl,
    final List<EventMediaDto> media = const <EventMediaDto>[],
    this.representing,
  }) : _tags = tags,
       _media = media,
       super._();

  factory _$EventDetailDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventDetailDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String title;
  @override
  final String slug;
  @override
  final String? subtitle;
  @override
  final String? description;
  final List<String> _tags;
  @override
  @JsonKey()
  List<String> get tags {
    if (_tags is EqualUnmodifiableListView) return _tags;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_tags);
  }

  @override
  final VenueDto? venue;
  @override
  @JsonKey(name: 'starts_at')
  final DateTime? startsAt;
  @override
  @JsonKey(name: 'ends_at')
  final DateTime? endsAt;
  @override
  @JsonKey()
  final String status;
  @override
  @JsonKey(name: 'view_count')
  final int viewCount;
  @override
  @JsonKey(name: 'banner_url')
  final String? bannerUrl;
  @override
  @JsonKey(name: 'event_mode')
  final String? eventMode;
  @override
  @JsonKey(name: 'online_url')
  final String? onlineUrl;
  final List<EventMediaDto> _media;
  @override
  @JsonKey()
  List<EventMediaDto> get media {
    if (_media is EqualUnmodifiableListView) return _media;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_media);
  }

  @override
  final EventRepresentationDto? representing;

  @override
  String toString() {
    return 'EventDetailDto(id: $id, title: $title, slug: $slug, subtitle: $subtitle, description: $description, tags: $tags, venue: $venue, startsAt: $startsAt, endsAt: $endsAt, status: $status, viewCount: $viewCount, bannerUrl: $bannerUrl, eventMode: $eventMode, onlineUrl: $onlineUrl, media: $media, representing: $representing)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventDetailDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.subtitle, subtitle) ||
                other.subtitle == subtitle) &&
            (identical(other.description, description) ||
                other.description == description) &&
            const DeepCollectionEquality().equals(other._tags, _tags) &&
            (identical(other.venue, venue) || other.venue == venue) &&
            (identical(other.startsAt, startsAt) ||
                other.startsAt == startsAt) &&
            (identical(other.endsAt, endsAt) || other.endsAt == endsAt) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.viewCount, viewCount) ||
                other.viewCount == viewCount) &&
            (identical(other.bannerUrl, bannerUrl) ||
                other.bannerUrl == bannerUrl) &&
            (identical(other.eventMode, eventMode) ||
                other.eventMode == eventMode) &&
            (identical(other.onlineUrl, onlineUrl) ||
                other.onlineUrl == onlineUrl) &&
            const DeepCollectionEquality().equals(other._media, _media) &&
            (identical(other.representing, representing) ||
                other.representing == representing));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    title,
    slug,
    subtitle,
    description,
    const DeepCollectionEquality().hash(_tags),
    venue,
    startsAt,
    endsAt,
    status,
    viewCount,
    bannerUrl,
    eventMode,
    onlineUrl,
    const DeepCollectionEquality().hash(_media),
    representing,
  );

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventDetailDtoImplCopyWith<_$EventDetailDtoImpl> get copyWith =>
      __$$EventDetailDtoImplCopyWithImpl<_$EventDetailDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventDetailDtoImplToJson(this);
  }
}

abstract class _EventDetailDto extends EventDetailDto {
  const factory _EventDetailDto({
    required final String id,
    required final String title,
    required final String slug,
    final String? subtitle,
    final String? description,
    final List<String> tags,
    final VenueDto? venue,
    @JsonKey(name: 'starts_at') final DateTime? startsAt,
    @JsonKey(name: 'ends_at') final DateTime? endsAt,
    final String status,
    @JsonKey(name: 'view_count') final int viewCount,
    @JsonKey(name: 'banner_url') final String? bannerUrl,
    @JsonKey(name: 'event_mode') final String? eventMode,
    @JsonKey(name: 'online_url') final String? onlineUrl,
    final List<EventMediaDto> media,
    final EventRepresentationDto? representing,
  }) = _$EventDetailDtoImpl;
  const _EventDetailDto._() : super._();

  factory _EventDetailDto.fromJson(Map<String, dynamic> json) =
      _$EventDetailDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get title;
  @override
  String get slug;
  @override
  String? get subtitle;
  @override
  String? get description;
  @override
  List<String> get tags;
  @override
  VenueDto? get venue;
  @override
  @JsonKey(name: 'starts_at')
  DateTime? get startsAt;
  @override
  @JsonKey(name: 'ends_at')
  DateTime? get endsAt;
  @override
  String get status;
  @override
  @JsonKey(name: 'view_count')
  int get viewCount;
  @override
  @JsonKey(name: 'banner_url')
  String? get bannerUrl;
  @override
  @JsonKey(name: 'event_mode')
  String? get eventMode;
  @override
  @JsonKey(name: 'online_url')
  String? get onlineUrl;
  @override
  List<EventMediaDto> get media;
  @override
  EventRepresentationDto? get representing;

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventDetailDtoImplCopyWith<_$EventDetailDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EventMediaDto _$EventMediaDtoFromJson(Map<String, dynamic> json) {
  return _EventMediaDto.fromJson(json);
}

/// @nodoc
mixin _$EventMediaDto {
  String get id => throw _privateConstructorUsedError;
  String get kind => throw _privateConstructorUsedError;
  String? get caption => throw _privateConstructorUsedError;
  String? get url => throw _privateConstructorUsedError;

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
  $Res call({String id, String kind, String? caption, String? url});
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
    Object? caption = freezed,
    Object? url = freezed,
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
            caption: freezed == caption
                ? _value.caption
                : caption // ignore: cast_nullable_to_non_nullable
                      as String?,
            url: freezed == url
                ? _value.url
                : url // ignore: cast_nullable_to_non_nullable
                      as String?,
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
  $Res call({String id, String kind, String? caption, String? url});
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
    Object? caption = freezed,
    Object? url = freezed,
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
        caption: freezed == caption
            ? _value.caption
            : caption // ignore: cast_nullable_to_non_nullable
                  as String?,
        url: freezed == url
            ? _value.url
            : url // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventMediaDtoImpl implements _EventMediaDto {
  const _$EventMediaDtoImpl({
    this.id = '',
    this.kind = '',
    this.caption,
    this.url,
  });

  factory _$EventMediaDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventMediaDtoImplFromJson(json);

  @override
  @JsonKey()
  final String id;
  @override
  @JsonKey()
  final String kind;
  @override
  final String? caption;
  @override
  final String? url;

  @override
  String toString() {
    return 'EventMediaDto(id: $id, kind: $kind, caption: $caption, url: $url)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventMediaDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.kind, kind) || other.kind == kind) &&
            (identical(other.caption, caption) || other.caption == caption) &&
            (identical(other.url, url) || other.url == url));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, id, kind, caption, url);

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
    final String id,
    final String kind,
    final String? caption,
    final String? url,
  }) = _$EventMediaDtoImpl;

  factory _EventMediaDto.fromJson(Map<String, dynamic> json) =
      _$EventMediaDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get kind;
  @override
  String? get caption;
  @override
  String? get url;

  /// Create a copy of EventMediaDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventMediaDtoImplCopyWith<_$EventMediaDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EventRepresentationDto _$EventRepresentationDtoFromJson(
  Map<String, dynamic> json,
) {
  return _EventRepresentationDto.fromJson(json);
}

/// @nodoc
mixin _$EventRepresentationDto {
  @JsonKey(name: 'org_id')
  String get orgId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String get slug => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_verified')
  bool get isVerified => throw _privateConstructorUsedError;

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
    @JsonKey(name: 'org_id') String orgId,
    String name,
    String slug,
    @JsonKey(name: 'is_verified') bool isVerified,
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
    Object? orgId = null,
    Object? name = null,
    Object? slug = null,
    Object? isVerified = null,
  }) {
    return _then(
      _value.copyWith(
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
abstract class _$$EventRepresentationDtoImplCopyWith<$Res>
    implements $EventRepresentationDtoCopyWith<$Res> {
  factory _$$EventRepresentationDtoImplCopyWith(
    _$EventRepresentationDtoImpl value,
    $Res Function(_$EventRepresentationDtoImpl) then,
  ) = __$$EventRepresentationDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'org_id') String orgId,
    String name,
    String slug,
    @JsonKey(name: 'is_verified') bool isVerified,
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
    Object? orgId = null,
    Object? name = null,
    Object? slug = null,
    Object? isVerified = null,
  }) {
    return _then(
      _$EventRepresentationDtoImpl(
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
class _$EventRepresentationDtoImpl implements _EventRepresentationDto {
  const _$EventRepresentationDtoImpl({
    @JsonKey(name: 'org_id') this.orgId = '',
    this.name = '',
    this.slug = '',
    @JsonKey(name: 'is_verified') this.isVerified = false,
  });

  factory _$EventRepresentationDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventRepresentationDtoImplFromJson(json);

  @override
  @JsonKey(name: 'org_id')
  final String orgId;
  @override
  @JsonKey()
  final String name;
  @override
  @JsonKey()
  final String slug;
  @override
  @JsonKey(name: 'is_verified')
  final bool isVerified;

  @override
  String toString() {
    return 'EventRepresentationDto(orgId: $orgId, name: $name, slug: $slug, isVerified: $isVerified)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventRepresentationDtoImpl &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.isVerified, isVerified) ||
                other.isVerified == isVerified));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, orgId, name, slug, isVerified);

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
    @JsonKey(name: 'org_id') final String orgId,
    final String name,
    final String slug,
    @JsonKey(name: 'is_verified') final bool isVerified,
  }) = _$EventRepresentationDtoImpl;

  factory _EventRepresentationDto.fromJson(Map<String, dynamic> json) =
      _$EventRepresentationDtoImpl.fromJson;

  @override
  @JsonKey(name: 'org_id')
  String get orgId;
  @override
  String get name;
  @override
  String get slug;
  @override
  @JsonKey(name: 'is_verified')
  bool get isVerified;

  /// Create a copy of EventRepresentationDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventRepresentationDtoImplCopyWith<_$EventRepresentationDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}
