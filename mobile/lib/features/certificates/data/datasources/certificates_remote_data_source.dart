import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../models/certificate_dto.dart';

class CertificatesRemoteDataSource {
  CertificatesRemoteDataSource(this._dio);
  final Dio _dio;

  /// Private list — GET /v1/me/certificates (D-058).
  Future<List<CertificateDto>> myCertificates() => guard(() async {
        final res = await _dio.get('/v1/me/certificates');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(CertificateDto.fromJson)
            .toList();
      });

  /// Public certificate detail by verify code.
  Future<CertificateDto> byCode(String code) => guard(() async {
        final res = await _dio.get('/v1/certificates/$code');
        return CertificateDto.fromJson((res.data as Map).cast<String, dynamic>());
      });
}
