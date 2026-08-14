// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'certificate_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

CertificateDto _$CertificateDtoFromJson(Map<String, dynamic> json) {
  return _CertificateDto.fromJson(json);
}

/// @nodoc
mixin _$CertificateDto {
  String get id => throw _privateConstructorUsedError;
  String get code => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_title')
  String get eventTitle => throw _privateConstructorUsedError;
  @JsonKey(name: 'template_id')
  String? get templateId => throw _privateConstructorUsedError;
  @JsonKey(name: 'issued_at')
  DateTime get issuedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'revoked_at')
  DateTime? get revokedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_revoked')
  bool get isRevoked => throw _privateConstructorUsedError;
  @JsonKey(name: 'pdf_url')
  String? get pdfUrl => throw _privateConstructorUsedError;
  @JsonKey(name: 'verify_code')
  String? get verifyCode => throw _privateConstructorUsedError;

  /// Serializes this CertificateDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of CertificateDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $CertificateDtoCopyWith<CertificateDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $CertificateDtoCopyWith<$Res> {
  factory $CertificateDtoCopyWith(
    CertificateDto value,
    $Res Function(CertificateDto) then,
  ) = _$CertificateDtoCopyWithImpl<$Res, CertificateDto>;
  @useResult
  $Res call({
    String id,
    String code,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'event_title') String eventTitle,
    @JsonKey(name: 'template_id') String? templateId,
    @JsonKey(name: 'issued_at') DateTime issuedAt,
    @JsonKey(name: 'revoked_at') DateTime? revokedAt,
    @JsonKey(name: 'is_revoked') bool isRevoked,
    @JsonKey(name: 'pdf_url') String? pdfUrl,
    @JsonKey(name: 'verify_code') String? verifyCode,
  });
}

/// @nodoc
class _$CertificateDtoCopyWithImpl<$Res, $Val extends CertificateDto>
    implements $CertificateDtoCopyWith<$Res> {
  _$CertificateDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of CertificateDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? code = null,
    Object? eventId = null,
    Object? eventTitle = null,
    Object? templateId = freezed,
    Object? issuedAt = null,
    Object? revokedAt = freezed,
    Object? isRevoked = null,
    Object? pdfUrl = freezed,
    Object? verifyCode = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            code: null == code
                ? _value.code
                : code // ignore: cast_nullable_to_non_nullable
                      as String,
            eventId: null == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String,
            eventTitle: null == eventTitle
                ? _value.eventTitle
                : eventTitle // ignore: cast_nullable_to_non_nullable
                      as String,
            templateId: freezed == templateId
                ? _value.templateId
                : templateId // ignore: cast_nullable_to_non_nullable
                      as String?,
            issuedAt: null == issuedAt
                ? _value.issuedAt
                : issuedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            revokedAt: freezed == revokedAt
                ? _value.revokedAt
                : revokedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            isRevoked: null == isRevoked
                ? _value.isRevoked
                : isRevoked // ignore: cast_nullable_to_non_nullable
                      as bool,
            pdfUrl: freezed == pdfUrl
                ? _value.pdfUrl
                : pdfUrl // ignore: cast_nullable_to_non_nullable
                      as String?,
            verifyCode: freezed == verifyCode
                ? _value.verifyCode
                : verifyCode // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$CertificateDtoImplCopyWith<$Res>
    implements $CertificateDtoCopyWith<$Res> {
  factory _$$CertificateDtoImplCopyWith(
    _$CertificateDtoImpl value,
    $Res Function(_$CertificateDtoImpl) then,
  ) = __$$CertificateDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String code,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'event_title') String eventTitle,
    @JsonKey(name: 'template_id') String? templateId,
    @JsonKey(name: 'issued_at') DateTime issuedAt,
    @JsonKey(name: 'revoked_at') DateTime? revokedAt,
    @JsonKey(name: 'is_revoked') bool isRevoked,
    @JsonKey(name: 'pdf_url') String? pdfUrl,
    @JsonKey(name: 'verify_code') String? verifyCode,
  });
}

/// @nodoc
class __$$CertificateDtoImplCopyWithImpl<$Res>
    extends _$CertificateDtoCopyWithImpl<$Res, _$CertificateDtoImpl>
    implements _$$CertificateDtoImplCopyWith<$Res> {
  __$$CertificateDtoImplCopyWithImpl(
    _$CertificateDtoImpl _value,
    $Res Function(_$CertificateDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of CertificateDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? code = null,
    Object? eventId = null,
    Object? eventTitle = null,
    Object? templateId = freezed,
    Object? issuedAt = null,
    Object? revokedAt = freezed,
    Object? isRevoked = null,
    Object? pdfUrl = freezed,
    Object? verifyCode = freezed,
  }) {
    return _then(
      _$CertificateDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        code: null == code
            ? _value.code
            : code // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: null == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String,
        eventTitle: null == eventTitle
            ? _value.eventTitle
            : eventTitle // ignore: cast_nullable_to_non_nullable
                  as String,
        templateId: freezed == templateId
            ? _value.templateId
            : templateId // ignore: cast_nullable_to_non_nullable
                  as String?,
        issuedAt: null == issuedAt
            ? _value.issuedAt
            : issuedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        revokedAt: freezed == revokedAt
            ? _value.revokedAt
            : revokedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        isRevoked: null == isRevoked
            ? _value.isRevoked
            : isRevoked // ignore: cast_nullable_to_non_nullable
                  as bool,
        pdfUrl: freezed == pdfUrl
            ? _value.pdfUrl
            : pdfUrl // ignore: cast_nullable_to_non_nullable
                  as String?,
        verifyCode: freezed == verifyCode
            ? _value.verifyCode
            : verifyCode // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$CertificateDtoImpl implements _CertificateDto {
  const _$CertificateDtoImpl({
    required this.id,
    required this.code,
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'event_title') required this.eventTitle,
    @JsonKey(name: 'template_id') this.templateId,
    @JsonKey(name: 'issued_at') required this.issuedAt,
    @JsonKey(name: 'revoked_at') this.revokedAt,
    @JsonKey(name: 'is_revoked') this.isRevoked = false,
    @JsonKey(name: 'pdf_url') this.pdfUrl,
    @JsonKey(name: 'verify_code') this.verifyCode,
  });

  factory _$CertificateDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$CertificateDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String code;
  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'event_title')
  final String eventTitle;
  @override
  @JsonKey(name: 'template_id')
  final String? templateId;
  @override
  @JsonKey(name: 'issued_at')
  final DateTime issuedAt;
  @override
  @JsonKey(name: 'revoked_at')
  final DateTime? revokedAt;
  @override
  @JsonKey(name: 'is_revoked')
  final bool isRevoked;
  @override
  @JsonKey(name: 'pdf_url')
  final String? pdfUrl;
  @override
  @JsonKey(name: 'verify_code')
  final String? verifyCode;

  @override
  String toString() {
    return 'CertificateDto(id: $id, code: $code, eventId: $eventId, eventTitle: $eventTitle, templateId: $templateId, issuedAt: $issuedAt, revokedAt: $revokedAt, isRevoked: $isRevoked, pdfUrl: $pdfUrl, verifyCode: $verifyCode)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$CertificateDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.code, code) || other.code == code) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.eventTitle, eventTitle) ||
                other.eventTitle == eventTitle) &&
            (identical(other.templateId, templateId) ||
                other.templateId == templateId) &&
            (identical(other.issuedAt, issuedAt) ||
                other.issuedAt == issuedAt) &&
            (identical(other.revokedAt, revokedAt) ||
                other.revokedAt == revokedAt) &&
            (identical(other.isRevoked, isRevoked) ||
                other.isRevoked == isRevoked) &&
            (identical(other.pdfUrl, pdfUrl) || other.pdfUrl == pdfUrl) &&
            (identical(other.verifyCode, verifyCode) ||
                other.verifyCode == verifyCode));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    code,
    eventId,
    eventTitle,
    templateId,
    issuedAt,
    revokedAt,
    isRevoked,
    pdfUrl,
    verifyCode,
  );

  /// Create a copy of CertificateDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$CertificateDtoImplCopyWith<_$CertificateDtoImpl> get copyWith =>
      __$$CertificateDtoImplCopyWithImpl<_$CertificateDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$CertificateDtoImplToJson(this);
  }
}

abstract class _CertificateDto implements CertificateDto {
  const factory _CertificateDto({
    required final String id,
    required final String code,
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'event_title') required final String eventTitle,
    @JsonKey(name: 'template_id') final String? templateId,
    @JsonKey(name: 'issued_at') required final DateTime issuedAt,
    @JsonKey(name: 'revoked_at') final DateTime? revokedAt,
    @JsonKey(name: 'is_revoked') final bool isRevoked,
    @JsonKey(name: 'pdf_url') final String? pdfUrl,
    @JsonKey(name: 'verify_code') final String? verifyCode,
  }) = _$CertificateDtoImpl;

  factory _CertificateDto.fromJson(Map<String, dynamic> json) =
      _$CertificateDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get code;
  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'event_title')
  String get eventTitle;
  @override
  @JsonKey(name: 'template_id')
  String? get templateId;
  @override
  @JsonKey(name: 'issued_at')
  DateTime get issuedAt;
  @override
  @JsonKey(name: 'revoked_at')
  DateTime? get revokedAt;
  @override
  @JsonKey(name: 'is_revoked')
  bool get isRevoked;
  @override
  @JsonKey(name: 'pdf_url')
  String? get pdfUrl;
  @override
  @JsonKey(name: 'verify_code')
  String? get verifyCode;

  /// Create a copy of CertificateDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$CertificateDtoImplCopyWith<_$CertificateDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
