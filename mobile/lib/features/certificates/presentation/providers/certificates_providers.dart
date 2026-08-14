import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../data/datasources/certificates_remote_data_source.dart';
import '../../data/models/certificate_dto.dart';

final _certsSourceProvider = Provider(
  (ref) => CertificatesRemoteDataSource(ref.watch(dioProvider)),
);

final myCertificatesProvider =
    FutureProvider.autoDispose<List<CertificateDto>>(
        (ref) => ref.watch(_certsSourceProvider).myCertificates());

final certificateDetailProvider =
    FutureProvider.autoDispose.family<CertificateDto, String>(
        (ref, code) => ref.watch(_certsSourceProvider).byCode(code));
