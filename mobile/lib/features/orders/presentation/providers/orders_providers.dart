import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../data/datasources/orders_remote_data_source.dart';
import '../../data/models/order_dto.dart';

final ordersSourceProvider = Provider(
  (ref) => OrdersRemoteDataSource(ref.watch(dioProvider)),
);

final myOrdersProvider = FutureProvider.autoDispose<List<OrderDto>>((ref) =>
    ref.watch(ordersSourceProvider).myOrders());

final myGroupsProvider = FutureProvider.autoDispose<List<GroupDto>>((ref) =>
    ref.watch(ordersSourceProvider).myGroups());

final groupDetailProvider =
    FutureProvider.autoDispose.family<GroupDto, String>((ref, id) =>
        ref.watch(ordersSourceProvider).group(id));

/// The ticket's real QR, as PNG bytes (D-302). `autoDispose` because a ticket QR is bearer-grade —
/// it should not outlive the screen showing it in memory any longer than it has to.
final ticketQrProvider =
    FutureProvider.autoDispose.family<Uint8List, String>((ref, code) =>
        ref.watch(ordersSourceProvider).ticketQrPng(code));
