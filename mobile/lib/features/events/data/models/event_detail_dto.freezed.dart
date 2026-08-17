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

EventContentDto _$EventContentDtoFromJson(Map<String, dynamic> json) {
  return _EventContentDto.fromJson(json);
}

/// @nodoc
mixin _$EventContentDto {
  String? get tagline => throw _privateConstructorUsedError;
  @JsonKey(name: 'short_description')
  String? get shortDescription => throw _privateConstructorUsedError;
  String? get rules => throw _privateConstructorUsedError;

  /// Serializes this EventContentDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventContentDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventContentDtoCopyWith<EventContentDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventContentDtoCopyWith<$Res> {
  factory $EventContentDtoCopyWith(
    EventContentDto value,
    $Res Function(EventContentDto) then,
  ) = _$EventContentDtoCopyWithImpl<$Res, EventContentDto>;
  @useResult
  $Res call({
    String? tagline,
    @JsonKey(name: 'short_description') String? shortDescription,
    String? rules,
  });
}

/// @nodoc
class _$EventContentDtoCopyWithImpl<$Res, $Val extends EventContentDto>
    implements $EventContentDtoCopyWith<$Res> {
  _$EventContentDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventContentDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? tagline = freezed,
    Object? shortDescription = freezed,
    Object? rules = freezed,
  }) {
    return _then(
      _value.copyWith(
            tagline: freezed == tagline
                ? _value.tagline
                : tagline // ignore: cast_nullable_to_non_nullable
                      as String?,
            shortDescription: freezed == shortDescription
                ? _value.shortDescription
                : shortDescription // ignore: cast_nullable_to_non_nullable
                      as String?,
            rules: freezed == rules
                ? _value.rules
                : rules // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EventContentDtoImplCopyWith<$Res>
    implements $EventContentDtoCopyWith<$Res> {
  factory _$$EventContentDtoImplCopyWith(
    _$EventContentDtoImpl value,
    $Res Function(_$EventContentDtoImpl) then,
  ) = __$$EventContentDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String? tagline,
    @JsonKey(name: 'short_description') String? shortDescription,
    String? rules,
  });
}

/// @nodoc
class __$$EventContentDtoImplCopyWithImpl<$Res>
    extends _$EventContentDtoCopyWithImpl<$Res, _$EventContentDtoImpl>
    implements _$$EventContentDtoImplCopyWith<$Res> {
  __$$EventContentDtoImplCopyWithImpl(
    _$EventContentDtoImpl _value,
    $Res Function(_$EventContentDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventContentDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? tagline = freezed,
    Object? shortDescription = freezed,
    Object? rules = freezed,
  }) {
    return _then(
      _$EventContentDtoImpl(
        tagline: freezed == tagline
            ? _value.tagline
            : tagline // ignore: cast_nullable_to_non_nullable
                  as String?,
        shortDescription: freezed == shortDescription
            ? _value.shortDescription
            : shortDescription // ignore: cast_nullable_to_non_nullable
                  as String?,
        rules: freezed == rules
            ? _value.rules
            : rules // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventContentDtoImpl implements _EventContentDto {
  const _$EventContentDtoImpl({
    this.tagline,
    @JsonKey(name: 'short_description') this.shortDescription,
    this.rules,
  });

  factory _$EventContentDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventContentDtoImplFromJson(json);

  @override
  final String? tagline;
  @override
  @JsonKey(name: 'short_description')
  final String? shortDescription;
  @override
  final String? rules;

  @override
  String toString() {
    return 'EventContentDto(tagline: $tagline, shortDescription: $shortDescription, rules: $rules)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventContentDtoImpl &&
            (identical(other.tagline, tagline) || other.tagline == tagline) &&
            (identical(other.shortDescription, shortDescription) ||
                other.shortDescription == shortDescription) &&
            (identical(other.rules, rules) || other.rules == rules));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, tagline, shortDescription, rules);

  /// Create a copy of EventContentDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventContentDtoImplCopyWith<_$EventContentDtoImpl> get copyWith =>
      __$$EventContentDtoImplCopyWithImpl<_$EventContentDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventContentDtoImplToJson(this);
  }
}

abstract class _EventContentDto implements EventContentDto {
  const factory _EventContentDto({
    final String? tagline,
    @JsonKey(name: 'short_description') final String? shortDescription,
    final String? rules,
  }) = _$EventContentDtoImpl;

  factory _EventContentDto.fromJson(Map<String, dynamic> json) =
      _$EventContentDtoImpl.fromJson;

  @override
  String? get tagline;
  @override
  @JsonKey(name: 'short_description')
  String? get shortDescription;
  @override
  String? get rules;

  /// Create a copy of EventContentDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventContentDtoImplCopyWith<_$EventContentDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EventLegalDto _$EventLegalDtoFromJson(Map<String, dynamic> json) {
  return _EventLegalDto.fromJson(json);
}

/// @nodoc
mixin _$EventLegalDto {
  @JsonKey(name: 'terms_url')
  String? get termsUrl => throw _privateConstructorUsedError;
  @JsonKey(name: 'terms_text')
  String? get termsText => throw _privateConstructorUsedError;
  @JsonKey(name: 'code_of_conduct')
  String? get codeOfConduct => throw _privateConstructorUsedError;
  @JsonKey(name: 'refund_policy')
  String? get refundPolicy => throw _privateConstructorUsedError;
  @JsonKey(name: 'cancellation_policy')
  String? get cancellationPolicy => throw _privateConstructorUsedError;
  @JsonKey(name: 'requires_consent')
  bool get requiresConsent => throw _privateConstructorUsedError;
  @JsonKey(name: 'consent_text')
  String? get consentText => throw _privateConstructorUsedError;

  /// Serializes this EventLegalDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventLegalDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventLegalDtoCopyWith<EventLegalDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventLegalDtoCopyWith<$Res> {
  factory $EventLegalDtoCopyWith(
    EventLegalDto value,
    $Res Function(EventLegalDto) then,
  ) = _$EventLegalDtoCopyWithImpl<$Res, EventLegalDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'terms_url') String? termsUrl,
    @JsonKey(name: 'terms_text') String? termsText,
    @JsonKey(name: 'code_of_conduct') String? codeOfConduct,
    @JsonKey(name: 'refund_policy') String? refundPolicy,
    @JsonKey(name: 'cancellation_policy') String? cancellationPolicy,
    @JsonKey(name: 'requires_consent') bool requiresConsent,
    @JsonKey(name: 'consent_text') String? consentText,
  });
}

/// @nodoc
class _$EventLegalDtoCopyWithImpl<$Res, $Val extends EventLegalDto>
    implements $EventLegalDtoCopyWith<$Res> {
  _$EventLegalDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventLegalDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? termsUrl = freezed,
    Object? termsText = freezed,
    Object? codeOfConduct = freezed,
    Object? refundPolicy = freezed,
    Object? cancellationPolicy = freezed,
    Object? requiresConsent = null,
    Object? consentText = freezed,
  }) {
    return _then(
      _value.copyWith(
            termsUrl: freezed == termsUrl
                ? _value.termsUrl
                : termsUrl // ignore: cast_nullable_to_non_nullable
                      as String?,
            termsText: freezed == termsText
                ? _value.termsText
                : termsText // ignore: cast_nullable_to_non_nullable
                      as String?,
            codeOfConduct: freezed == codeOfConduct
                ? _value.codeOfConduct
                : codeOfConduct // ignore: cast_nullable_to_non_nullable
                      as String?,
            refundPolicy: freezed == refundPolicy
                ? _value.refundPolicy
                : refundPolicy // ignore: cast_nullable_to_non_nullable
                      as String?,
            cancellationPolicy: freezed == cancellationPolicy
                ? _value.cancellationPolicy
                : cancellationPolicy // ignore: cast_nullable_to_non_nullable
                      as String?,
            requiresConsent: null == requiresConsent
                ? _value.requiresConsent
                : requiresConsent // ignore: cast_nullable_to_non_nullable
                      as bool,
            consentText: freezed == consentText
                ? _value.consentText
                : consentText // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EventLegalDtoImplCopyWith<$Res>
    implements $EventLegalDtoCopyWith<$Res> {
  factory _$$EventLegalDtoImplCopyWith(
    _$EventLegalDtoImpl value,
    $Res Function(_$EventLegalDtoImpl) then,
  ) = __$$EventLegalDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'terms_url') String? termsUrl,
    @JsonKey(name: 'terms_text') String? termsText,
    @JsonKey(name: 'code_of_conduct') String? codeOfConduct,
    @JsonKey(name: 'refund_policy') String? refundPolicy,
    @JsonKey(name: 'cancellation_policy') String? cancellationPolicy,
    @JsonKey(name: 'requires_consent') bool requiresConsent,
    @JsonKey(name: 'consent_text') String? consentText,
  });
}

/// @nodoc
class __$$EventLegalDtoImplCopyWithImpl<$Res>
    extends _$EventLegalDtoCopyWithImpl<$Res, _$EventLegalDtoImpl>
    implements _$$EventLegalDtoImplCopyWith<$Res> {
  __$$EventLegalDtoImplCopyWithImpl(
    _$EventLegalDtoImpl _value,
    $Res Function(_$EventLegalDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventLegalDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? termsUrl = freezed,
    Object? termsText = freezed,
    Object? codeOfConduct = freezed,
    Object? refundPolicy = freezed,
    Object? cancellationPolicy = freezed,
    Object? requiresConsent = null,
    Object? consentText = freezed,
  }) {
    return _then(
      _$EventLegalDtoImpl(
        termsUrl: freezed == termsUrl
            ? _value.termsUrl
            : termsUrl // ignore: cast_nullable_to_non_nullable
                  as String?,
        termsText: freezed == termsText
            ? _value.termsText
            : termsText // ignore: cast_nullable_to_non_nullable
                  as String?,
        codeOfConduct: freezed == codeOfConduct
            ? _value.codeOfConduct
            : codeOfConduct // ignore: cast_nullable_to_non_nullable
                  as String?,
        refundPolicy: freezed == refundPolicy
            ? _value.refundPolicy
            : refundPolicy // ignore: cast_nullable_to_non_nullable
                  as String?,
        cancellationPolicy: freezed == cancellationPolicy
            ? _value.cancellationPolicy
            : cancellationPolicy // ignore: cast_nullable_to_non_nullable
                  as String?,
        requiresConsent: null == requiresConsent
            ? _value.requiresConsent
            : requiresConsent // ignore: cast_nullable_to_non_nullable
                  as bool,
        consentText: freezed == consentText
            ? _value.consentText
            : consentText // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventLegalDtoImpl implements _EventLegalDto {
  const _$EventLegalDtoImpl({
    @JsonKey(name: 'terms_url') this.termsUrl,
    @JsonKey(name: 'terms_text') this.termsText,
    @JsonKey(name: 'code_of_conduct') this.codeOfConduct,
    @JsonKey(name: 'refund_policy') this.refundPolicy,
    @JsonKey(name: 'cancellation_policy') this.cancellationPolicy,
    @JsonKey(name: 'requires_consent') this.requiresConsent = false,
    @JsonKey(name: 'consent_text') this.consentText,
  });

  factory _$EventLegalDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventLegalDtoImplFromJson(json);

  @override
  @JsonKey(name: 'terms_url')
  final String? termsUrl;
  @override
  @JsonKey(name: 'terms_text')
  final String? termsText;
  @override
  @JsonKey(name: 'code_of_conduct')
  final String? codeOfConduct;
  @override
  @JsonKey(name: 'refund_policy')
  final String? refundPolicy;
  @override
  @JsonKey(name: 'cancellation_policy')
  final String? cancellationPolicy;
  @override
  @JsonKey(name: 'requires_consent')
  final bool requiresConsent;
  @override
  @JsonKey(name: 'consent_text')
  final String? consentText;

  @override
  String toString() {
    return 'EventLegalDto(termsUrl: $termsUrl, termsText: $termsText, codeOfConduct: $codeOfConduct, refundPolicy: $refundPolicy, cancellationPolicy: $cancellationPolicy, requiresConsent: $requiresConsent, consentText: $consentText)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventLegalDtoImpl &&
            (identical(other.termsUrl, termsUrl) ||
                other.termsUrl == termsUrl) &&
            (identical(other.termsText, termsText) ||
                other.termsText == termsText) &&
            (identical(other.codeOfConduct, codeOfConduct) ||
                other.codeOfConduct == codeOfConduct) &&
            (identical(other.refundPolicy, refundPolicy) ||
                other.refundPolicy == refundPolicy) &&
            (identical(other.cancellationPolicy, cancellationPolicy) ||
                other.cancellationPolicy == cancellationPolicy) &&
            (identical(other.requiresConsent, requiresConsent) ||
                other.requiresConsent == requiresConsent) &&
            (identical(other.consentText, consentText) ||
                other.consentText == consentText));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    termsUrl,
    termsText,
    codeOfConduct,
    refundPolicy,
    cancellationPolicy,
    requiresConsent,
    consentText,
  );

  /// Create a copy of EventLegalDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventLegalDtoImplCopyWith<_$EventLegalDtoImpl> get copyWith =>
      __$$EventLegalDtoImplCopyWithImpl<_$EventLegalDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$EventLegalDtoImplToJson(this);
  }
}

abstract class _EventLegalDto implements EventLegalDto {
  const factory _EventLegalDto({
    @JsonKey(name: 'terms_url') final String? termsUrl,
    @JsonKey(name: 'terms_text') final String? termsText,
    @JsonKey(name: 'code_of_conduct') final String? codeOfConduct,
    @JsonKey(name: 'refund_policy') final String? refundPolicy,
    @JsonKey(name: 'cancellation_policy') final String? cancellationPolicy,
    @JsonKey(name: 'requires_consent') final bool requiresConsent,
    @JsonKey(name: 'consent_text') final String? consentText,
  }) = _$EventLegalDtoImpl;

  factory _EventLegalDto.fromJson(Map<String, dynamic> json) =
      _$EventLegalDtoImpl.fromJson;

  @override
  @JsonKey(name: 'terms_url')
  String? get termsUrl;
  @override
  @JsonKey(name: 'terms_text')
  String? get termsText;
  @override
  @JsonKey(name: 'code_of_conduct')
  String? get codeOfConduct;
  @override
  @JsonKey(name: 'refund_policy')
  String? get refundPolicy;
  @override
  @JsonKey(name: 'cancellation_policy')
  String? get cancellationPolicy;
  @override
  @JsonKey(name: 'requires_consent')
  bool get requiresConsent;
  @override
  @JsonKey(name: 'consent_text')
  String? get consentText;

  /// Create a copy of EventLegalDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventLegalDtoImplCopyWith<_$EventLegalDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EventScheduleDto _$EventScheduleDtoFromJson(Map<String, dynamic> json) {
  return _EventScheduleDto.fromJson(json);
}

/// @nodoc
mixin _$EventScheduleDto {
  @JsonKey(name: 'registration_opens_at')
  DateTime? get registrationOpensAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'registration_closes_at')
  DateTime? get registrationClosesAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'checkin_opens_at')
  DateTime? get checkinOpensAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'checkin_closes_at')
  DateTime? get checkinClosesAt => throw _privateConstructorUsedError;

  /// Serializes this EventScheduleDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventScheduleDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventScheduleDtoCopyWith<EventScheduleDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventScheduleDtoCopyWith<$Res> {
  factory $EventScheduleDtoCopyWith(
    EventScheduleDto value,
    $Res Function(EventScheduleDto) then,
  ) = _$EventScheduleDtoCopyWithImpl<$Res, EventScheduleDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'registration_opens_at') DateTime? registrationOpensAt,
    @JsonKey(name: 'registration_closes_at') DateTime? registrationClosesAt,
    @JsonKey(name: 'checkin_opens_at') DateTime? checkinOpensAt,
    @JsonKey(name: 'checkin_closes_at') DateTime? checkinClosesAt,
  });
}

/// @nodoc
class _$EventScheduleDtoCopyWithImpl<$Res, $Val extends EventScheduleDto>
    implements $EventScheduleDtoCopyWith<$Res> {
  _$EventScheduleDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventScheduleDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? registrationOpensAt = freezed,
    Object? registrationClosesAt = freezed,
    Object? checkinOpensAt = freezed,
    Object? checkinClosesAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            registrationOpensAt: freezed == registrationOpensAt
                ? _value.registrationOpensAt
                : registrationOpensAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            registrationClosesAt: freezed == registrationClosesAt
                ? _value.registrationClosesAt
                : registrationClosesAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            checkinOpensAt: freezed == checkinOpensAt
                ? _value.checkinOpensAt
                : checkinOpensAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            checkinClosesAt: freezed == checkinClosesAt
                ? _value.checkinClosesAt
                : checkinClosesAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EventScheduleDtoImplCopyWith<$Res>
    implements $EventScheduleDtoCopyWith<$Res> {
  factory _$$EventScheduleDtoImplCopyWith(
    _$EventScheduleDtoImpl value,
    $Res Function(_$EventScheduleDtoImpl) then,
  ) = __$$EventScheduleDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'registration_opens_at') DateTime? registrationOpensAt,
    @JsonKey(name: 'registration_closes_at') DateTime? registrationClosesAt,
    @JsonKey(name: 'checkin_opens_at') DateTime? checkinOpensAt,
    @JsonKey(name: 'checkin_closes_at') DateTime? checkinClosesAt,
  });
}

/// @nodoc
class __$$EventScheduleDtoImplCopyWithImpl<$Res>
    extends _$EventScheduleDtoCopyWithImpl<$Res, _$EventScheduleDtoImpl>
    implements _$$EventScheduleDtoImplCopyWith<$Res> {
  __$$EventScheduleDtoImplCopyWithImpl(
    _$EventScheduleDtoImpl _value,
    $Res Function(_$EventScheduleDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventScheduleDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? registrationOpensAt = freezed,
    Object? registrationClosesAt = freezed,
    Object? checkinOpensAt = freezed,
    Object? checkinClosesAt = freezed,
  }) {
    return _then(
      _$EventScheduleDtoImpl(
        registrationOpensAt: freezed == registrationOpensAt
            ? _value.registrationOpensAt
            : registrationOpensAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        registrationClosesAt: freezed == registrationClosesAt
            ? _value.registrationClosesAt
            : registrationClosesAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        checkinOpensAt: freezed == checkinOpensAt
            ? _value.checkinOpensAt
            : checkinOpensAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        checkinClosesAt: freezed == checkinClosesAt
            ? _value.checkinClosesAt
            : checkinClosesAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventScheduleDtoImpl implements _EventScheduleDto {
  const _$EventScheduleDtoImpl({
    @JsonKey(name: 'registration_opens_at') this.registrationOpensAt,
    @JsonKey(name: 'registration_closes_at') this.registrationClosesAt,
    @JsonKey(name: 'checkin_opens_at') this.checkinOpensAt,
    @JsonKey(name: 'checkin_closes_at') this.checkinClosesAt,
  });

  factory _$EventScheduleDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventScheduleDtoImplFromJson(json);

  @override
  @JsonKey(name: 'registration_opens_at')
  final DateTime? registrationOpensAt;
  @override
  @JsonKey(name: 'registration_closes_at')
  final DateTime? registrationClosesAt;
  @override
  @JsonKey(name: 'checkin_opens_at')
  final DateTime? checkinOpensAt;
  @override
  @JsonKey(name: 'checkin_closes_at')
  final DateTime? checkinClosesAt;

  @override
  String toString() {
    return 'EventScheduleDto(registrationOpensAt: $registrationOpensAt, registrationClosesAt: $registrationClosesAt, checkinOpensAt: $checkinOpensAt, checkinClosesAt: $checkinClosesAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventScheduleDtoImpl &&
            (identical(other.registrationOpensAt, registrationOpensAt) ||
                other.registrationOpensAt == registrationOpensAt) &&
            (identical(other.registrationClosesAt, registrationClosesAt) ||
                other.registrationClosesAt == registrationClosesAt) &&
            (identical(other.checkinOpensAt, checkinOpensAt) ||
                other.checkinOpensAt == checkinOpensAt) &&
            (identical(other.checkinClosesAt, checkinClosesAt) ||
                other.checkinClosesAt == checkinClosesAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    registrationOpensAt,
    registrationClosesAt,
    checkinOpensAt,
    checkinClosesAt,
  );

  /// Create a copy of EventScheduleDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventScheduleDtoImplCopyWith<_$EventScheduleDtoImpl> get copyWith =>
      __$$EventScheduleDtoImplCopyWithImpl<_$EventScheduleDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventScheduleDtoImplToJson(this);
  }
}

abstract class _EventScheduleDto implements EventScheduleDto {
  const factory _EventScheduleDto({
    @JsonKey(name: 'registration_opens_at') final DateTime? registrationOpensAt,
    @JsonKey(name: 'registration_closes_at')
    final DateTime? registrationClosesAt,
    @JsonKey(name: 'checkin_opens_at') final DateTime? checkinOpensAt,
    @JsonKey(name: 'checkin_closes_at') final DateTime? checkinClosesAt,
  }) = _$EventScheduleDtoImpl;

  factory _EventScheduleDto.fromJson(Map<String, dynamic> json) =
      _$EventScheduleDtoImpl.fromJson;

  @override
  @JsonKey(name: 'registration_opens_at')
  DateTime? get registrationOpensAt;
  @override
  @JsonKey(name: 'registration_closes_at')
  DateTime? get registrationClosesAt;
  @override
  @JsonKey(name: 'checkin_opens_at')
  DateTime? get checkinOpensAt;
  @override
  @JsonKey(name: 'checkin_closes_at')
  DateTime? get checkinClosesAt;

  /// Create a copy of EventScheduleDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventScheduleDtoImplCopyWith<_$EventScheduleDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EventEligibilityDto _$EventEligibilityDtoFromJson(Map<String, dynamic> json) {
  return _EventEligibilityDto.fromJson(json);
}

/// @nodoc
mixin _$EventEligibilityDto {
  @JsonKey(name: 'min_age')
  int? get minAge => throw _privateConstructorUsedError;
  @JsonKey(name: 'max_age')
  int? get maxAge => throw _privateConstructorUsedError;
  @JsonKey(name: 'gender_restriction')
  String? get genderRestriction => throw _privateConstructorUsedError;
  @JsonKey(name: 'max_teams')
  int? get maxTeams => throw _privateConstructorUsedError;

  /// Serializes this EventEligibilityDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventEligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventEligibilityDtoCopyWith<EventEligibilityDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventEligibilityDtoCopyWith<$Res> {
  factory $EventEligibilityDtoCopyWith(
    EventEligibilityDto value,
    $Res Function(EventEligibilityDto) then,
  ) = _$EventEligibilityDtoCopyWithImpl<$Res, EventEligibilityDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'min_age') int? minAge,
    @JsonKey(name: 'max_age') int? maxAge,
    @JsonKey(name: 'gender_restriction') String? genderRestriction,
    @JsonKey(name: 'max_teams') int? maxTeams,
  });
}

/// @nodoc
class _$EventEligibilityDtoCopyWithImpl<$Res, $Val extends EventEligibilityDto>
    implements $EventEligibilityDtoCopyWith<$Res> {
  _$EventEligibilityDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventEligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? minAge = freezed,
    Object? maxAge = freezed,
    Object? genderRestriction = freezed,
    Object? maxTeams = freezed,
  }) {
    return _then(
      _value.copyWith(
            minAge: freezed == minAge
                ? _value.minAge
                : minAge // ignore: cast_nullable_to_non_nullable
                      as int?,
            maxAge: freezed == maxAge
                ? _value.maxAge
                : maxAge // ignore: cast_nullable_to_non_nullable
                      as int?,
            genderRestriction: freezed == genderRestriction
                ? _value.genderRestriction
                : genderRestriction // ignore: cast_nullable_to_non_nullable
                      as String?,
            maxTeams: freezed == maxTeams
                ? _value.maxTeams
                : maxTeams // ignore: cast_nullable_to_non_nullable
                      as int?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EventEligibilityDtoImplCopyWith<$Res>
    implements $EventEligibilityDtoCopyWith<$Res> {
  factory _$$EventEligibilityDtoImplCopyWith(
    _$EventEligibilityDtoImpl value,
    $Res Function(_$EventEligibilityDtoImpl) then,
  ) = __$$EventEligibilityDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'min_age') int? minAge,
    @JsonKey(name: 'max_age') int? maxAge,
    @JsonKey(name: 'gender_restriction') String? genderRestriction,
    @JsonKey(name: 'max_teams') int? maxTeams,
  });
}

/// @nodoc
class __$$EventEligibilityDtoImplCopyWithImpl<$Res>
    extends _$EventEligibilityDtoCopyWithImpl<$Res, _$EventEligibilityDtoImpl>
    implements _$$EventEligibilityDtoImplCopyWith<$Res> {
  __$$EventEligibilityDtoImplCopyWithImpl(
    _$EventEligibilityDtoImpl _value,
    $Res Function(_$EventEligibilityDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventEligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? minAge = freezed,
    Object? maxAge = freezed,
    Object? genderRestriction = freezed,
    Object? maxTeams = freezed,
  }) {
    return _then(
      _$EventEligibilityDtoImpl(
        minAge: freezed == minAge
            ? _value.minAge
            : minAge // ignore: cast_nullable_to_non_nullable
                  as int?,
        maxAge: freezed == maxAge
            ? _value.maxAge
            : maxAge // ignore: cast_nullable_to_non_nullable
                  as int?,
        genderRestriction: freezed == genderRestriction
            ? _value.genderRestriction
            : genderRestriction // ignore: cast_nullable_to_non_nullable
                  as String?,
        maxTeams: freezed == maxTeams
            ? _value.maxTeams
            : maxTeams // ignore: cast_nullable_to_non_nullable
                  as int?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventEligibilityDtoImpl implements _EventEligibilityDto {
  const _$EventEligibilityDtoImpl({
    @JsonKey(name: 'min_age') this.minAge,
    @JsonKey(name: 'max_age') this.maxAge,
    @JsonKey(name: 'gender_restriction') this.genderRestriction,
    @JsonKey(name: 'max_teams') this.maxTeams,
  });

  factory _$EventEligibilityDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventEligibilityDtoImplFromJson(json);

  @override
  @JsonKey(name: 'min_age')
  final int? minAge;
  @override
  @JsonKey(name: 'max_age')
  final int? maxAge;
  @override
  @JsonKey(name: 'gender_restriction')
  final String? genderRestriction;
  @override
  @JsonKey(name: 'max_teams')
  final int? maxTeams;

  @override
  String toString() {
    return 'EventEligibilityDto(minAge: $minAge, maxAge: $maxAge, genderRestriction: $genderRestriction, maxTeams: $maxTeams)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventEligibilityDtoImpl &&
            (identical(other.minAge, minAge) || other.minAge == minAge) &&
            (identical(other.maxAge, maxAge) || other.maxAge == maxAge) &&
            (identical(other.genderRestriction, genderRestriction) ||
                other.genderRestriction == genderRestriction) &&
            (identical(other.maxTeams, maxTeams) ||
                other.maxTeams == maxTeams));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, minAge, maxAge, genderRestriction, maxTeams);

  /// Create a copy of EventEligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventEligibilityDtoImplCopyWith<_$EventEligibilityDtoImpl> get copyWith =>
      __$$EventEligibilityDtoImplCopyWithImpl<_$EventEligibilityDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventEligibilityDtoImplToJson(this);
  }
}

abstract class _EventEligibilityDto implements EventEligibilityDto {
  const factory _EventEligibilityDto({
    @JsonKey(name: 'min_age') final int? minAge,
    @JsonKey(name: 'max_age') final int? maxAge,
    @JsonKey(name: 'gender_restriction') final String? genderRestriction,
    @JsonKey(name: 'max_teams') final int? maxTeams,
  }) = _$EventEligibilityDtoImpl;

  factory _EventEligibilityDto.fromJson(Map<String, dynamic> json) =
      _$EventEligibilityDtoImpl.fromJson;

  @override
  @JsonKey(name: 'min_age')
  int? get minAge;
  @override
  @JsonKey(name: 'max_age')
  int? get maxAge;
  @override
  @JsonKey(name: 'gender_restriction')
  String? get genderRestriction;
  @override
  @JsonKey(name: 'max_teams')
  int? get maxTeams;

  /// Create a copy of EventEligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventEligibilityDtoImplCopyWith<_$EventEligibilityDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EventLocationDetailDto _$EventLocationDetailDtoFromJson(
  Map<String, dynamic> json,
) {
  return _EventLocationDetailDto.fromJson(json);
}

/// @nodoc
mixin _$EventLocationDetailDto {
  String? get building => throw _privateConstructorUsedError;
  String? get floor => throw _privateConstructorUsedError;
  String? get room => throw _privateConstructorUsedError;
  @JsonKey(name: 'google_maps_url')
  String? get googleMapsUrl => throw _privateConstructorUsedError;
  @JsonKey(name: 'meeting_platform')
  String? get meetingPlatform => throw _privateConstructorUsedError;

  /// Serializes this EventLocationDetailDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EventLocationDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EventLocationDetailDtoCopyWith<EventLocationDetailDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EventLocationDetailDtoCopyWith<$Res> {
  factory $EventLocationDetailDtoCopyWith(
    EventLocationDetailDto value,
    $Res Function(EventLocationDetailDto) then,
  ) = _$EventLocationDetailDtoCopyWithImpl<$Res, EventLocationDetailDto>;
  @useResult
  $Res call({
    String? building,
    String? floor,
    String? room,
    @JsonKey(name: 'google_maps_url') String? googleMapsUrl,
    @JsonKey(name: 'meeting_platform') String? meetingPlatform,
  });
}

/// @nodoc
class _$EventLocationDetailDtoCopyWithImpl<
  $Res,
  $Val extends EventLocationDetailDto
>
    implements $EventLocationDetailDtoCopyWith<$Res> {
  _$EventLocationDetailDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EventLocationDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? building = freezed,
    Object? floor = freezed,
    Object? room = freezed,
    Object? googleMapsUrl = freezed,
    Object? meetingPlatform = freezed,
  }) {
    return _then(
      _value.copyWith(
            building: freezed == building
                ? _value.building
                : building // ignore: cast_nullable_to_non_nullable
                      as String?,
            floor: freezed == floor
                ? _value.floor
                : floor // ignore: cast_nullable_to_non_nullable
                      as String?,
            room: freezed == room
                ? _value.room
                : room // ignore: cast_nullable_to_non_nullable
                      as String?,
            googleMapsUrl: freezed == googleMapsUrl
                ? _value.googleMapsUrl
                : googleMapsUrl // ignore: cast_nullable_to_non_nullable
                      as String?,
            meetingPlatform: freezed == meetingPlatform
                ? _value.meetingPlatform
                : meetingPlatform // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EventLocationDetailDtoImplCopyWith<$Res>
    implements $EventLocationDetailDtoCopyWith<$Res> {
  factory _$$EventLocationDetailDtoImplCopyWith(
    _$EventLocationDetailDtoImpl value,
    $Res Function(_$EventLocationDetailDtoImpl) then,
  ) = __$$EventLocationDetailDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String? building,
    String? floor,
    String? room,
    @JsonKey(name: 'google_maps_url') String? googleMapsUrl,
    @JsonKey(name: 'meeting_platform') String? meetingPlatform,
  });
}

/// @nodoc
class __$$EventLocationDetailDtoImplCopyWithImpl<$Res>
    extends
        _$EventLocationDetailDtoCopyWithImpl<$Res, _$EventLocationDetailDtoImpl>
    implements _$$EventLocationDetailDtoImplCopyWith<$Res> {
  __$$EventLocationDetailDtoImplCopyWithImpl(
    _$EventLocationDetailDtoImpl _value,
    $Res Function(_$EventLocationDetailDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EventLocationDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? building = freezed,
    Object? floor = freezed,
    Object? room = freezed,
    Object? googleMapsUrl = freezed,
    Object? meetingPlatform = freezed,
  }) {
    return _then(
      _$EventLocationDetailDtoImpl(
        building: freezed == building
            ? _value.building
            : building // ignore: cast_nullable_to_non_nullable
                  as String?,
        floor: freezed == floor
            ? _value.floor
            : floor // ignore: cast_nullable_to_non_nullable
                  as String?,
        room: freezed == room
            ? _value.room
            : room // ignore: cast_nullable_to_non_nullable
                  as String?,
        googleMapsUrl: freezed == googleMapsUrl
            ? _value.googleMapsUrl
            : googleMapsUrl // ignore: cast_nullable_to_non_nullable
                  as String?,
        meetingPlatform: freezed == meetingPlatform
            ? _value.meetingPlatform
            : meetingPlatform // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EventLocationDetailDtoImpl implements _EventLocationDetailDto {
  const _$EventLocationDetailDtoImpl({
    this.building,
    this.floor,
    this.room,
    @JsonKey(name: 'google_maps_url') this.googleMapsUrl,
    @JsonKey(name: 'meeting_platform') this.meetingPlatform,
  });

  factory _$EventLocationDetailDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EventLocationDetailDtoImplFromJson(json);

  @override
  final String? building;
  @override
  final String? floor;
  @override
  final String? room;
  @override
  @JsonKey(name: 'google_maps_url')
  final String? googleMapsUrl;
  @override
  @JsonKey(name: 'meeting_platform')
  final String? meetingPlatform;

  @override
  String toString() {
    return 'EventLocationDetailDto(building: $building, floor: $floor, room: $room, googleMapsUrl: $googleMapsUrl, meetingPlatform: $meetingPlatform)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EventLocationDetailDtoImpl &&
            (identical(other.building, building) ||
                other.building == building) &&
            (identical(other.floor, floor) || other.floor == floor) &&
            (identical(other.room, room) || other.room == room) &&
            (identical(other.googleMapsUrl, googleMapsUrl) ||
                other.googleMapsUrl == googleMapsUrl) &&
            (identical(other.meetingPlatform, meetingPlatform) ||
                other.meetingPlatform == meetingPlatform));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    building,
    floor,
    room,
    googleMapsUrl,
    meetingPlatform,
  );

  /// Create a copy of EventLocationDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EventLocationDetailDtoImplCopyWith<_$EventLocationDetailDtoImpl>
  get copyWith =>
      __$$EventLocationDetailDtoImplCopyWithImpl<_$EventLocationDetailDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EventLocationDetailDtoImplToJson(this);
  }
}

abstract class _EventLocationDetailDto implements EventLocationDetailDto {
  const factory _EventLocationDetailDto({
    final String? building,
    final String? floor,
    final String? room,
    @JsonKey(name: 'google_maps_url') final String? googleMapsUrl,
    @JsonKey(name: 'meeting_platform') final String? meetingPlatform,
  }) = _$EventLocationDetailDtoImpl;

  factory _EventLocationDetailDto.fromJson(Map<String, dynamic> json) =
      _$EventLocationDetailDtoImpl.fromJson;

  @override
  String? get building;
  @override
  String? get floor;
  @override
  String? get room;
  @override
  @JsonKey(name: 'google_maps_url')
  String? get googleMapsUrl;
  @override
  @JsonKey(name: 'meeting_platform')
  String? get meetingPlatform;

  /// Create a copy of EventLocationDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EventLocationDetailDtoImplCopyWith<_$EventLocationDetailDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
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
  EventContentDto? get content => throw _privateConstructorUsedError;
  EventLegalDto? get legal => throw _privateConstructorUsedError;
  EventScheduleDto? get schedule => throw _privateConstructorUsedError;
  EventEligibilityDto? get eligibility => throw _privateConstructorUsedError;
  @JsonKey(name: 'location_detail')
  EventLocationDetailDto? get locationDetail =>
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
    EventContentDto? content,
    EventLegalDto? legal,
    EventScheduleDto? schedule,
    EventEligibilityDto? eligibility,
    @JsonKey(name: 'location_detail') EventLocationDetailDto? locationDetail,
  });

  $VenueDtoCopyWith<$Res>? get venue;
  $EventRepresentationDtoCopyWith<$Res>? get representing;
  $EventContentDtoCopyWith<$Res>? get content;
  $EventLegalDtoCopyWith<$Res>? get legal;
  $EventScheduleDtoCopyWith<$Res>? get schedule;
  $EventEligibilityDtoCopyWith<$Res>? get eligibility;
  $EventLocationDetailDtoCopyWith<$Res>? get locationDetail;
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
    Object? content = freezed,
    Object? legal = freezed,
    Object? schedule = freezed,
    Object? eligibility = freezed,
    Object? locationDetail = freezed,
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
            content: freezed == content
                ? _value.content
                : content // ignore: cast_nullable_to_non_nullable
                      as EventContentDto?,
            legal: freezed == legal
                ? _value.legal
                : legal // ignore: cast_nullable_to_non_nullable
                      as EventLegalDto?,
            schedule: freezed == schedule
                ? _value.schedule
                : schedule // ignore: cast_nullable_to_non_nullable
                      as EventScheduleDto?,
            eligibility: freezed == eligibility
                ? _value.eligibility
                : eligibility // ignore: cast_nullable_to_non_nullable
                      as EventEligibilityDto?,
            locationDetail: freezed == locationDetail
                ? _value.locationDetail
                : locationDetail // ignore: cast_nullable_to_non_nullable
                      as EventLocationDetailDto?,
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

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $EventContentDtoCopyWith<$Res>? get content {
    if (_value.content == null) {
      return null;
    }

    return $EventContentDtoCopyWith<$Res>(_value.content!, (value) {
      return _then(_value.copyWith(content: value) as $Val);
    });
  }

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $EventLegalDtoCopyWith<$Res>? get legal {
    if (_value.legal == null) {
      return null;
    }

    return $EventLegalDtoCopyWith<$Res>(_value.legal!, (value) {
      return _then(_value.copyWith(legal: value) as $Val);
    });
  }

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $EventScheduleDtoCopyWith<$Res>? get schedule {
    if (_value.schedule == null) {
      return null;
    }

    return $EventScheduleDtoCopyWith<$Res>(_value.schedule!, (value) {
      return _then(_value.copyWith(schedule: value) as $Val);
    });
  }

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $EventEligibilityDtoCopyWith<$Res>? get eligibility {
    if (_value.eligibility == null) {
      return null;
    }

    return $EventEligibilityDtoCopyWith<$Res>(_value.eligibility!, (value) {
      return _then(_value.copyWith(eligibility: value) as $Val);
    });
  }

  /// Create a copy of EventDetailDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $EventLocationDetailDtoCopyWith<$Res>? get locationDetail {
    if (_value.locationDetail == null) {
      return null;
    }

    return $EventLocationDetailDtoCopyWith<$Res>(_value.locationDetail!, (
      value,
    ) {
      return _then(_value.copyWith(locationDetail: value) as $Val);
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
    EventContentDto? content,
    EventLegalDto? legal,
    EventScheduleDto? schedule,
    EventEligibilityDto? eligibility,
    @JsonKey(name: 'location_detail') EventLocationDetailDto? locationDetail,
  });

  @override
  $VenueDtoCopyWith<$Res>? get venue;
  @override
  $EventRepresentationDtoCopyWith<$Res>? get representing;
  @override
  $EventContentDtoCopyWith<$Res>? get content;
  @override
  $EventLegalDtoCopyWith<$Res>? get legal;
  @override
  $EventScheduleDtoCopyWith<$Res>? get schedule;
  @override
  $EventEligibilityDtoCopyWith<$Res>? get eligibility;
  @override
  $EventLocationDetailDtoCopyWith<$Res>? get locationDetail;
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
    Object? content = freezed,
    Object? legal = freezed,
    Object? schedule = freezed,
    Object? eligibility = freezed,
    Object? locationDetail = freezed,
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
        content: freezed == content
            ? _value.content
            : content // ignore: cast_nullable_to_non_nullable
                  as EventContentDto?,
        legal: freezed == legal
            ? _value.legal
            : legal // ignore: cast_nullable_to_non_nullable
                  as EventLegalDto?,
        schedule: freezed == schedule
            ? _value.schedule
            : schedule // ignore: cast_nullable_to_non_nullable
                  as EventScheduleDto?,
        eligibility: freezed == eligibility
            ? _value.eligibility
            : eligibility // ignore: cast_nullable_to_non_nullable
                  as EventEligibilityDto?,
        locationDetail: freezed == locationDetail
            ? _value.locationDetail
            : locationDetail // ignore: cast_nullable_to_non_nullable
                  as EventLocationDetailDto?,
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
    this.content,
    this.legal,
    this.schedule,
    this.eligibility,
    @JsonKey(name: 'location_detail') this.locationDetail,
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
  final EventContentDto? content;
  @override
  final EventLegalDto? legal;
  @override
  final EventScheduleDto? schedule;
  @override
  final EventEligibilityDto? eligibility;
  @override
  @JsonKey(name: 'location_detail')
  final EventLocationDetailDto? locationDetail;

  @override
  String toString() {
    return 'EventDetailDto(id: $id, title: $title, slug: $slug, subtitle: $subtitle, description: $description, tags: $tags, venue: $venue, startsAt: $startsAt, endsAt: $endsAt, status: $status, viewCount: $viewCount, bannerUrl: $bannerUrl, eventMode: $eventMode, onlineUrl: $onlineUrl, media: $media, representing: $representing, content: $content, legal: $legal, schedule: $schedule, eligibility: $eligibility, locationDetail: $locationDetail)';
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
                other.representing == representing) &&
            (identical(other.content, content) || other.content == content) &&
            (identical(other.legal, legal) || other.legal == legal) &&
            (identical(other.schedule, schedule) ||
                other.schedule == schedule) &&
            (identical(other.eligibility, eligibility) ||
                other.eligibility == eligibility) &&
            (identical(other.locationDetail, locationDetail) ||
                other.locationDetail == locationDetail));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hashAll([
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
    content,
    legal,
    schedule,
    eligibility,
    locationDetail,
  ]);

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
    final EventContentDto? content,
    final EventLegalDto? legal,
    final EventScheduleDto? schedule,
    final EventEligibilityDto? eligibility,
    @JsonKey(name: 'location_detail')
    final EventLocationDetailDto? locationDetail,
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
  @override
  EventContentDto? get content;
  @override
  EventLegalDto? get legal;
  @override
  EventScheduleDto? get schedule;
  @override
  EventEligibilityDto? get eligibility;
  @override
  @JsonKey(name: 'location_detail')
  EventLocationDetailDto? get locationDetail;

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
