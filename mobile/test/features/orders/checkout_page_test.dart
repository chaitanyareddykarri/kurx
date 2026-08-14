import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_detail.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_page.dart';
import 'package:kurx_mobile/features/events/domain/entities/ticket_type.dart';
import 'package:kurx_mobile/features/events/presentation/providers/events_providers.dart';
import 'package:kurx_mobile/features/orders/data/models/attendee_dtos.dart';
import 'package:kurx_mobile/features/orders/presentation/pages/checkout_page.dart';
import 'package:kurx_mobile/features/orders/presentation/providers/attendee_providers.dart';

/// Checkout is the money path, so the assertions here are about what must **not** happen as much as
/// what must. Paid tickets run against `MockPaymentGateway`; creating an order for one would leave a
/// real `Pending` row and a gateway id that settles nothing, so the screen has to stop *before* the
/// call — not after it, and not with a disabled button that a rebuild might re-enable.
void main() {
  setUpAll(() async => initializeDateFormatting('en_IN'));

  const slug = 'kurx-live';
  const eventId = 'evt-1';

  EventPage page({required int pricePaise}) => EventPage(
        detail: EventDetail(
          id: eventId,
          title: 'Kurx Live',
          slug: slug,
          status: 'published',
          startsAt: DateTime(2026, 9, 1, 10),
        ),
        ticketTypes: [
          TicketType(
            id: 'tt-1',
            name: pricePaise == 0 ? 'Free entry' : 'General admission',
            pricePaise: pricePaise,
            available: 10,
            quantity: 10,
          ),
        ],
        related: const [],
      );

  Widget host({required EventPage eventPage, required _RecordingActions actions}) => ProviderScope(
        overrides: [
          eventPageProvider(slug).overrideWith((ref) async => eventPage),
          attendeeActionsProvider.overrideWith((ref) => _FakeActions(ref, actions)),
        ],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const CheckoutPage(
            eventSlug: slug,
            eventId: eventId,
            ticketTypeId: 'tt-1',
          ),
        ),
      );

  testWidgets('a free ticket checks out for real and shows the issued code', (tester) async {
    final actions = _RecordingActions();
    await tester.pumpWidget(host(eventPage: page(pricePaise: 0), actions: actions));
    await tester.pumpAndSettle();

    expect(find.text('Confirm free ticket'), findsOneWidget);

    await tester.tap(find.text('Confirm free ticket'));
    await tester.pumpAndSettle();

    expect(actions.orders, 1, reason: 'a free ticket must actually place the order');
    expect(find.text('You’re going!'), findsOneWidget);
    expect(find.text('TCKT-9'), findsOneWidget);
  });

  testWidgets('a paid ticket never creates an order and says why', (tester) async {
    final actions = _RecordingActions();
    await tester.pumpWidget(host(eventPage: page(pricePaise: 50000), actions: actions));
    await tester.pumpAndSettle();

    expect(find.text('Paid tickets are not live yet'), findsOneWidget);
    // The guarantee that matters: no confirm affordance exists at all, so no order can be created.
    expect(find.text('Confirm free ticket'), findsNothing);
    expect(actions.orders, 0);
  });

  testWidgets('a refused order keeps the user on checkout with an actionable reason',
      (tester) async {
    final actions = _RecordingActions()
      ..failWith = const ApiError(status: 409, code: 'sold_out');
    await tester.pumpWidget(host(eventPage: page(pricePaise: 0), actions: actions));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Confirm free ticket'));
    await tester.pumpAndSettle();

    expect(find.text('Could not place your order'), findsOneWidget);
    expect(
      find.textContaining('sold out'),
      findsOneWidget,
      reason: 'the order-path error codes get a real sentence, not a generic failure',
    );
    // Still on checkout, and re-submittable.
    expect(find.text('Confirm free ticket'), findsOneWidget);
  });

  testWidgets('the idempotency key is stable across repeated submits', (tester) async {
    final actions = _RecordingActions()
      ..failWith = const ApiError(status: 500, code: 'server_error');
    await tester.pumpWidget(host(eventPage: page(pricePaise: 0), actions: actions));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Confirm free ticket'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirm free ticket'));
    await tester.pumpAndSettle();

    expect(actions.keys, hasLength(2));
    expect(
      actions.keys.toSet(),
      hasLength(1),
      reason: 'a retry is the same purchase intent — a fresh key per tap would let the server '
          'treat a retry as a second ticket',
    );
  });
}

/// What checkout asked the API to do. Held outside the provider so assertions survive the
/// container rebuilding the override.
class _RecordingActions {
  int orders = 0;
  final List<String> keys = [];
  ApiError? failWith;
}

/// Subclasses the real [AttendeeActions] so the override exercises the actual provider wiring
/// rather than a parallel fake graph. The `Ref` comes from the override callback.
class _FakeActions extends AttendeeActions {
  const _FakeActions(super.ref, this._rec);
  final _RecordingActions _rec;

  @override
  Future<OrderDto> createOrder(
    String eventId, {
    required String ticketTypeId,
    required String idempotencyKey,
    Map<String, String>? answers,
  }) async {
    _rec.keys.add(idempotencyKey);
    final failure = _rec.failWith;
    if (failure != null) throw failure;

    _rec.orders++;
    return OrderDto(
      id: 'ord-1',
      eventId: eventId,
      ticketTypeId: ticketTypeId,
      status: 'paid',
      tickets: const [TicketDto(id: 't-1', code: 'TCKT-9')],
    );
  }
}
