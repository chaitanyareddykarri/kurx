import 'package:freezed_annotation/freezed_annotation.dart';

part 'certificate_dto.freezed.dart';
part 'certificate_dto.g.dart';

/// Private certificate list item — GET /v1/me/certificates (D-058).
@freezed
class CertificateDto with _$CertificateDto {
  const factory CertificateDto({
    required String id,
    required String code,
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'event_title') required String eventTitle,
    @JsonKey(name: 'template_id') String? templateId,
    @JsonKey(name: 'issued_at') required DateTime issuedAt,
    @JsonKey(name: 'revoked_at') DateTime? revokedAt,
    @JsonKey(name: 'is_revoked') @Default(false) bool isRevoked,
    @JsonKey(name: 'pdf_url') String? pdfUrl,
    @JsonKey(name: 'verify_code') String? verifyCode,
  }) = _CertificateDto;

  factory CertificateDto.fromJson(Map<String, dynamic> json) =>
      _$CertificateDtoFromJson(json);
}
