import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/features/orders/data/datasources/orders_remote_data_source.dart';
import 'package:kurx_mobile/features/orders/data/models/order_dto.dart';
import 'package:kurx_mobile/features/orders/presentation/pages/transfer_claim_page.dart';
import 'package:kurx_mobile/features/orders/presentation/providers/orders_providers.dart';

const _ticketId = 'tk-1';
const _rotatedCode = 'rotated-code-9999';

/// Stands in for the API. `myOrders` only starts returning the claimed ticket once
/// the claim has happened — mirroring the backend, where the rotated code exists
/// only in a post-claim list and never in the claim response itself (D-062).
class _FakeOrdersSource extends OrdersRemoteDataSource {
  _FakeOrdersSource() : super(Dio());

  String? claimedWith;
  ApiError? failWith;
  bool _claimed = false;

  @override
  Future<TransferDto> claimTransfer(String transferCode) async {
    claimedWith = transferCode;
    if (failWith != null) throw failWith!;
    _claimed = true;
    return TransferDto(
      id: 'tr-1',
      ticketId: _ticketId,
      toPhone: '+919000000000',
      transferCode: transferCode,
      status: 'claimed',
      expiresAt: DateTime(2030),
      createdAt: DateTime(2026),
    );
  }

  @override
  Future<List<OrderDto>> myOrders() async => [
        OrderDto(
          id: 'o-1',
          eventId: 'e-1',
          ticketTypeId: 'tt-1',
          status: 'paid',
          amountPaise: 0,
          createdAt: DateTime(2026),
          tickets: [
            if (_claimed)
              TicketDto(
                id: _ticketId,
                code: _rotatedCode,
                state: 'issued',
                createdAt: DateTime(2026),
              ),
          ],
        ),
      ];
}

/// A real GoRouter — the page routes with `pushReplacement`, so a bare Navigator
/// would not exercise what it actually does. `/tickets/:code` is stubbed to echo the
/// code, keeping the assertion on routing rather than on TicketDetailPage's network QR.
Future<void> _pump(WidgetTester tester, _FakeOrdersSource src, {String? code}) async {
  final router = GoRouter(
    initialLocation: '/transfers/claim',
    routes: [
      GoRoute(
        path: '/transfers/claim',
        builder: (_, _) => TransferClaimPage(code: code),
      ),
      GoRoute(
        path: '/tickets/:code',
        builder: (_, s) => Scaffold(
          body: Text('ticket:${s.pathParameters['code']}'),
        ),
      ),
    ],
  );
  await tester.pumpWidget(ProviderScope(
    overrides: [ordersSourceProvider.overrideWithValue(src)],
    child: MaterialApp.router(routerConfig: router),
  ));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('claims, then routes to the claimed ticket by its rotated code', (tester) async {
    final src = _FakeOrdersSource();
    await _pump(tester, src, code: 'TRF-ABC123');

    await tester.tap(find.text('Accept & Claim'));
    await tester.pumpAndSettle();

    expect(src.claimedWith, 'TRF-ABC123');
    // Landed on the ticket, keyed by the rotated code the claim response never carried.
    expect(find.text('ticket:$_rotatedCode'), findsOneWidget);
    // Replaced, not stacked: the spent claim form is gone.
    expect(find.text('Accept & Claim'), findsNothing);
  });

  testWidgets('surfaces the API error and stays put', (tester) async {
    final src = _FakeOrdersSource()
      ..failWith = const ApiError(status: 403, code: 'phone_mismatch');
    await _pump(tester, src, code: 'TRF-WRONG');

    await tester.tap(find.text('Accept & Claim'));
    await tester.pumpAndSettle();

    expect(find.text('This ticket was sent to a different mobile number.'), findsOneWidget);
    expect(find.text('Accept & Claim'), findsOneWidget);
  });

  testWidgets('does not call the API on an empty code', (tester) async {
    final src = _FakeOrdersSource();
    await _pump(tester, src);

    await tester.tap(find.text('Accept & Claim'));
    await tester.pumpAndSettle();

    expect(src.claimedWith, isNull);
  });
}
