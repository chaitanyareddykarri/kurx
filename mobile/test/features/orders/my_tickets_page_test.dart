import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/orders/data/datasources/orders_remote_data_source.dart';
import 'package:kurx_mobile/features/orders/data/models/order_dto.dart';
import 'package:kurx_mobile/features/orders/presentation/pages/my_tickets_page.dart';
import 'package:kurx_mobile/features/orders/presentation/providers/orders_providers.dart';

/// Serves one order holding a ticket in each state the API actually emits:
/// `TicketState.ToLowerInvariant()` → issued / checkedin / void, and a free order
/// carrying Status=Paid (OrderEndpoints.ToOrderJson). These strings are the contract —
/// the page previously filtered on `valid` / `checked_in`, which match nothing, so every
/// ticket fell through to Expired.
class _FakeOrdersSource extends OrdersRemoteDataSource {
  _FakeOrdersSource() : super(Dio());

  TicketDto _t(String id, String state) => TicketDto(
        id: id,
        code: '0000$id-aaaa-bbbb-cccc-00000000000$id',
        state: state,
        createdAt: DateTime(2026, 3, 4),
      );

  @override
  Future<List<OrderDto>> myOrders() async => [
        OrderDto(
          id: 'o-1',
          eventId: 'e-1',
          ticketTypeId: 'tt-1',
          status: 'paid',
          amountPaise: 0,
          createdAt: DateTime(2026, 3, 4),
          tickets: [_t('1', 'issued'), _t('2', 'checkedin'), _t('3', 'void')],
        ),
      ];
}

Future<void> _pump(WidgetTester tester) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [ordersSourceProvider.overrideWithValue(_FakeOrdersSource())],
    child: const MaterialApp(home: MyTicketsPage()),
  ));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('sorts tickets into tabs by the states the API really sends', (tester) async {
    await _pump(tester);

    // Active holds the `issued` ticket — and only it.
    expect(find.text('Ticket #00001-AA'), findsOneWidget);
    expect(find.text('Ticket #00002-AA'), findsNothing);
    expect(find.text('Ticket #00003-AA'), findsNothing);

    await tester.tap(find.widgetWithText(Tab, 'Used'));
    await tester.pumpAndSettle();
    expect(find.text('Ticket #00002-AA'), findsOneWidget);
    expect(find.text('Ticket #00001-AA'), findsNothing);

    await tester.tap(find.widgetWithText(Tab, 'Expired'));
    await tester.pumpAndSettle();
    // The `void` ticket alone — not a catch-all that sweeps up the other two.
    expect(find.text('Ticket #00003-AA'), findsOneWidget);
    expect(find.text('Ticket #00001-AA'), findsNothing);
    expect(find.text('Ticket #00002-AA'), findsNothing);
  });

  testWidgets('badges a checked-in ticket Used rather than Active', (tester) async {
    await _pump(tester);
    await tester.tap(find.widgetWithText(Tab, 'Used'));
    await tester.pumpAndSettle();

    // 'Used' twice: the TabBar label plus the card's badge. 'Active' only once —
    // the tab label — i.e. the checked-in card is not wearing an Active badge.
    expect(find.text('Used'), findsNWidgets(2));
    expect(find.text('Active'), findsOneWidget);
  });

  testWidgets('offers a way into the transfer claim flow (D-063)', (tester) async {
    await _pump(tester);
    expect(find.byTooltip('Claim a transferred ticket'), findsOneWidget);
  });
}
