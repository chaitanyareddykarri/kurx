// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'event_summary_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

EventSummaryDto _$EventSummaryDtoFromJson(Map<String, dynamic> json) {
  return _EventSummaryDto.fromJson(json);
}

/// @nodoc
mixin _$EventSummaryDto {
  String get id => throw _privateConstructorUsedError;
  String get title => throw _privateConstructorUsedError;
  String get slug => throw _privateConstructorUsedError;
  String? get subtitle => throw _privateConstructorUsedError;
  @JsonKey(name: 'venue_name')
  String? get venueName => throw _privateConstructorUsedError;
  String? get city => throw _privateConstructorUsedError;
  @JsonKey(name: 'starts_at')
  DateTime? get startsAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'ends_at')
  DateTime? get endsAt => throw _privateConstructorUsedError;
  String get status =>
      throw _privateConstructorUsedError; // D-302 — the card facts the API now sends. `banner_url` is presigned; `banner_key` is not fetchable.
  @JsonKey(name: 'banner_url')
  String? get bannerUrl => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_mode')
  String? get eventMode => throw _privateConstructorUsedError;
  @JsonKey(name: 'category_name')
  String? get categoryName => throw _privateConstructorUsedError;
  @JsonKey(name: 'price_from_paise')
  int? get priceFromPaise => throw _privateConstructorUsedError;
  String? get currency => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_featured')
  bool? get isFeatured => throw _privateConstructorUsedError;

  /// Serializes this EventSummaryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventSummaryDtoCopyWith<EventSummaryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventSummaryDtoCopyWith<$Res> {
  factory $EventSummaryDtoCopyWith(
    EventSummaryDto value,
    $Res Function(EventSummaryDto) then,
  ) = _$EventSummaryDtoCopyWithImpl<$Res, EventSummaryDto>;
  @useResult
  $Res call({
    String id,
    String title,
    String slug,
    String? subtitle,
    @JsonKey(name: 'venue_name') String? venueName,
    String? city,
    @JsonKey(name: 'starts_at') DateTime? startsAt,
    @JsonKey(name: 'ends_at') DateTime? endsAt,
    String status,
    @JsonKey(name: 'banner_url') String? bannerUrl,
    @JsonKey(name: 'event_mode') String? eventMode,
    @JsonKey(name: 'category_name') String? categoryName,
    @JsonKey(name: 'price_from_paise') int? priceFromPaise,
    String? currency,
    @JsonKey(name: 'is_featured') bool? isFeatured,
  });
}

/// @nodoc
class _$EventSummaryDtoCopyWithImpl<$Res, $Val extends EventSummaryDto>
    implements $EventSummaryDtoCopyWith<$Res> {
  _$EventSummaryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = null,
    Object? subtitle = freezed,
    Object? venueName = freezed,
    Object? city = freezed,
    Object? startsAt = freezed,
    Object? endsAt = freezed,
    Object? status = null,
    Object? bannerUrl = freezed,
    Object? eventMode = freezed,
    Object? categoryName = freezed,
    Object? priceFromPaise = freezed,
    Object? currency = freezed,
    Object? isFeatured = freezed,
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
            venueName: freezed == venueName
                ? _value.venueName
                : venueName // ignore: cast_nullable_to_non_nullable
                      as String?,
            city: freezed == city
                ? _value.city
                : city // ignore: cast_nullable_to_non_nullable
                      as String?,
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
            bannerUrl: freezed == bannerUrl
                ? _value.bannerUrl
                : bannerUrl // ignore: cast_nullable_to_non_nullable
                      as String?,
            eventMode: freezed == eventMode
                ? _value.eventMode
                : eventMode // ignore: cast_nullable_to_non_nullable
                      as String?,
            categoryName: freezed == categoryName
                ? _value.categoryName
                : categoryName // ignore: cast_nullable_to_non_nullable
                      as String?,
            priceFromPaise: freezed == priceFromPaise
                ? _value.priceFromPaise
                : priceFromPaise // ignore: cast_nullable_to_non_nullable
                      as int?,
            currency: freezed == currency
                ? _value.currency
                : currency // ignore: cast_nullable_to_non_nullable
                      as String?,
            isFeatured: freezed == isFeatured
                ? _value.isFeatured
                : isFeatured // ignore: cast_nullable_to_non_nullable
                      as bool?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EventSummaryDtoImplCopyWith<$Res>
    implements $EventSummaryDtoCopyWith<$Res> {
  factory _$$EventSummaryDtoImplCopyWith(
    _$EventSummaryDtoImpl value,
    $Res Function(_$EventSummaryDtoImpl) then,
  ) = __$$EventSummaryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String title,
    String slug,
    String? subtitle,
    @JsonKey(name: 'venue_name') String? venueName,
    String? city,
    @JsonKey(name: 'starts_at') DateTime? startsAt,
    @JsonKey(name: 'ends_at') DateTime? endsAt,
    String status,
    @JsonKey(name: 'banner_url') String? bannerUrl,
    @JsonKey(name: 'event_mode') String? eventMode,
    @JsonKey(name: 'category_name') String? categoryName,
    @JsonKey(name: 'price_from_paise') int? priceFromPaise,
    String? currency,
    @JsonKey(name: 'is_featured') bool? isFeatured,
  });
}

/// @nodoc
class __$$EventSummaryDtoImplCopyWithImpl<$Res>
    extends _$EventSummaryDtoCopyWithImpl<$Res, _$EventSummaryDtoImpl>
    implements _$$EventSummaryDtoImplCopyWith<$Res> {
  __$$EventSummaryDtoImplCopyWithImpl(
    _$EventSummaryDtoImpl _value,
    $Res Function(_$EventSummaryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? title = null,
    Object? slug = null,
    Object? subtitle = freezed,
    Object? venueName = freezed,
    Object? city = freezed,
    Object? startsAt = freezed,
    Object? endsAt = freezed,
    Object? status = null,
    Object? bannerUrl = freezed,
    Object? eventMode = freezed,
    Object? categoryName = freezed,
    Object? priceFromPaise = freezed,
    Object? currency = freezed,
    Object? isFeatured = freezed,
  }) {
    return _then(
      _$EventSummaryDtoImpl(
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
        venueName: freezed == venueName
            ? _value.venueName
            : venueName // ignore: cast_nullable_to_non_nullable
                  as String?,
        city: freezed == city
            ? _value.city
            : city // ignore: cast_nullable_to_non_nullable
                  as String?,
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
        bannerUrl: freezed == bannerUrl
            ? _value.bannerUrl
            : bannerUrl // ignore: cast_nullable_to_non_nullable
                  as String?,
        eventMode: freezed == eventMode
            ? _value.eventMode
            : eventMode // ignore: cast_nullable_to_non_nullable
                  as String?,
        categoryName: freezed == categoryName
            ? _value.categoryName
            : categoryName // ignore: cast_nullable_to_non_nullable
                  as String?,
        priceFromPaise: freezed == priceFromPaise
            ? _value.priceFromPaise
            : priceFromPaise // ignore: cast_nullable_to_non_nullable
                  as int?,
        currency: freezed == currency
            ? _value.currency
            : currency // ignore: cast_nullable_to_non_nullable
                  as String?,
        isFeatured: freezed == isFeatured
            ? _value.isFeatured
            : isFeatured // ignore: cast_nullable_to_non_nullable
                  as bool?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventSummaryDtoImpl extends _EventSummaryDto {
  const _$EventSummaryDtoImpl({
    required this.id,
    required this.title,
    required this.slug,
    this.subtitle,
    @JsonKey(name: 'venue_name') this.venueName,
    this.city,
    @JsonKey(name: 'starts_at') this.startsAt,
    @JsonKey(name: 'ends_at') this.endsAt,
    this.status = '',
    @JsonKey(name: 'banner_url') this.bannerUrl,
    @JsonKey(name: 'event_mode') this.eventMode,
    @JsonKey(name: 'category_name') this.categoryName,
    @JsonKey(name: 'price_from_paise') this.priceFromPaise,
    this.currency,
    @JsonKey(name: 'is_featured') this.isFeatured,
  }) : super._();

  factory _$EventSummaryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventSummaryDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String title;
  @override
  final String slug;
  @override
  final String? subtitle;
  @override
  @JsonKey(name: 'venue_name')
  final String? venueName;
  @override
  final String? city;
  @override
  @JsonKey(name: 'starts_at')
  final DateTime? startsAt;
  @override
  @JsonKey(name: 'ends_at')
  final DateTime? endsAt;
  @override
  @JsonKey()
  final String status;
  // D-302 — the card facts the API now sends. `banner_url` is presigned; `banner_key` is not fetchable.
  @override
  @JsonKey(name: 'banner_url')
  final String? bannerUrl;
  @override
  @JsonKey(name: 'event_mode')
  final String? eventMode;
  @override
  @JsonKey(name: 'category_name')
  final String? categoryName;
  @override
  @JsonKey(name: 'price_from_paise')
  final int? priceFromPaise;
  @override
  final String? currency;
  @override
  @JsonKey(name: 'is_featured')
  final bool? isFeatured;

  @override
  String toString() {
    return 'EventSummaryDto(id: $id, title: $title, slug: $slug, subtitle: $subtitle, venueName: $venueName, city: $city, startsAt: $startsAt, endsAt: $endsAt, status: $status, bannerUrl: $bannerUrl, eventMode: $eventMode, categoryName: $categoryName, priceFromPaise: $priceFromPaise, currency: $currency, isFeatured: $isFeatured)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventSummaryDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.subtitle, subtitle) ||
                other.subtitle == subtitle) &&
            (identical(other.venueName, venueName) ||
                other.venueName == venueName) &&
            (identical(other.city, city) || other.city == city) &&
            (identical(other.startsAt, startsAt) ||
                other.startsAt == startsAt) &&
            (identical(other.endsAt, endsAt) || other.endsAt == endsAt) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.bannerUrl, bannerUrl) ||
                other.bannerUrl == bannerUrl) &&
            (identical(other.eventMode, eventMode) ||
                other.eventMode == eventMode) &&
            (identical(other.categoryName, categoryName) ||
                other.categoryName == categoryName) &&
            (identical(other.priceFromPaise, priceFromPaise) ||
                other.priceFromPaise == priceFromPaise) &&
            (identical(other.currency, currency) ||
                other.currency == currency) &&
            (identical(other.isFeatured, isFeatured) ||
                other.isFeatured == isFeatured));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    title,
    slug,
    subtitle,
    venueName,
    city,
    startsAt,
    endsAt,
    status,
    bannerUrl,
    eventMode,
    categoryName,
    priceFromPaise,
    currency,
    isFeatured,
  );

  /// Create a copy of EventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventSummaryDtoImplCopyWith<_$EventSummaryDtoImpl> get copyWith =>
      __$$EventSummaryDtoImplCopyWithImpl<_$EventSummaryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventSummaryDtoImplToJson(this);
  }
}

abstract class _EventSummaryDto extends EventSummaryDto {
  const factory _EventSummaryDto({
    required final String id,
    required final String title,
    required final String slug,
    final String? subtitle,
    @JsonKey(name: 'venue_name') final String? venueName,
    final String? city,
    @JsonKey(name: 'starts_at') final DateTime? startsAt,
    @JsonKey(name: 'ends_at') final DateTime? endsAt,
    final String status,
    @JsonKey(name: 'banner_url') final String? bannerUrl,
    @JsonKey(name: 'event_mode') final String? eventMode,
    @JsonKey(name: 'category_name') final String? categoryName,
    @JsonKey(name: 'price_from_paise') final int? priceFromPaise,
    final String? currency,
    @JsonKey(name: 'is_featured') final bool? isFeatured,
  }) = _$EventSummaryDtoImpl;
  const _EventSummaryDto._() : super._();

  factory _EventSummaryDto.fromJson(Map<String, dynamic> json) =
      _$EventSummaryDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get title;
  @override
  String get slug;
  @override
  String? get subtitle;
  @override
  @JsonKey(name: 'venue_name')
  String? get venueName;
  @override
  String? get city;
  @override
  @JsonKey(name: 'starts_at')
  DateTime? get startsAt;
  @override
  @JsonKey(name: 'ends_at')
  DateTime? get endsAt;
  @override
  String get status; // D-302 — the card facts the API now sends. `banner_url` is presigned; `banner_key` is not fetchable.
  @override
  @JsonKey(name: 'banner_url')
  String? get bannerUrl;
  @override
  @JsonKey(name: 'event_mode')
  String? get eventMode;
  @override
  @JsonKey(name: 'category_name')
  String? get categoryName;
  @override
  @JsonKey(name: 'price_from_paise')
  int? get priceFromPaise;
  @override
  String? get currency;
  @override
  @JsonKey(name: 'is_featured')
  bool? get isFeatured;

  /// Create a copy of EventSummaryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventSummaryDtoImplCopyWith<_$EventSummaryDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
