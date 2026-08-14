import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../models/order_dto.dart';

class OrdersRemoteDataSource {
  OrdersRemoteDataSource(this._dio);
  final Dio _dio;

  /// The ticket's scannable QR, as PNG bytes (D-302).
  ///
  /// Fetched through Dio rather than handed to `Image.network`, because `GET /v1/tickets/{code}/qr.png`
  /// is `RequireAuthorization()` and `Image.network` uses its own HTTP client — it carries no
  /// `Authorization` header, so every request 401'd and the ticket screen showed its "QR unavailable"
  /// fallback permanently. Dio also runs [AuthInterceptor], so an expired access token refreshes and
  /// retries instead of turning the QR blank until the app is restarted.
  Future<Uint8List> ticketQrPng(String ticketCode) => guard(
        () async {
          final res = await _dio.get<List<int>>(
            '/v1/tickets/$ticketCode/qr.png',
            options: Options(responseType: ResponseType.bytes),
          );
          return Uint8List.fromList(res.data ?? const []);
        },
        endpoint: 'GET /v1/tickets/{code}/qr.png',
      );

  Future<List<OrderDto>> myOrders() => guard(() async {
        final res = await _dio.get('/v1/orders');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(OrderDto.fromJson)
            .toList();
      });

  Future<List<GroupDto>> myGroups() => guard(() async {
        final res = await _dio.get('/v1/groups');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(GroupDto.fromJson)
            .toList();
      });

  Future<GroupDto> group(String groupId) => guard(() async {
        final res = await _dio.get('/v1/groups/$groupId');
        return GroupDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<TransferDto> initiateTransfer(String ticketId, String toPhone) =>
      guard(() async {
        final res = await _dio.post(
          '/v1/tickets/$ticketId/transfer',
          data: {'toPhone': toPhone},
        );
        return TransferDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<void> cancelTransfer(String transferId) =>
      guard(() async => _dio.delete('/v1/transfers/$transferId'));

  Future<TransferDto> claimTransfer(String transferCode) => guard(() async {
        final res = await _dio.post(
          '/v1/transfers/claim',
          data: {'transferCode': transferCode},
        );
        return TransferDto.fromJson((res.data as Map).cast<String, dynamic>());
      });
}
