// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'certificate_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$CertificateDtoImpl _$$CertificateDtoImplFromJson(Map<String, dynamic> json) =>
    _$CertificateDtoImpl(
      id: json['id'] as String,
      code: json['code'] as String,
      eventId: json['event_id'] as String,
      eventTitle: json['event_title'] as String,
      templateId: json['template_id'] as String?,
      issuedAt: DateTime.parse(json['issued_at'] as String),
      revokedAt: json['revoked_at'] == null
          ? null
          : DateTime.parse(json['revoked_at'] as String),
      isRevoked: json['is_revoked'] as bool? ?? false,
      pdfUrl: json['pdf_url'] as String?,
      verifyCode: json['verify_code'] as String?,
    );

Map<String, dynamic> _$$CertificateDtoImplToJson(
  _$CertificateDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'code': instance.code,
  'event_id': instance.eventId,
  'event_title': instance.eventTitle,
  'template_id': instance.templateId,
  'issued_at': instance.issuedAt.toIso8601String(),
  'revoked_at': instance.revokedAt?.toIso8601String(),
  'is_revoked': instance.isRevoked,
  'pdf_url': instance.pdfUrl,
  'verify_code': instance.verifyCode,
};
