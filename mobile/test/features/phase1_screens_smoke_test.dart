import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/bookmarks/presentation/pages/saved_page.dart';
import 'package:kurx_mobile/features/notifications/data/notifications_remote_data_source.dart';
import 'package:kurx_mobile/features/notifications/presentation/pages/notifications_page.dart';
import 'package:kurx_mobile/features/notifications/presentation/providers/notifications_providers.dart';

Widget _host(Widget page, {List<Override> overrides = const []}) => ProviderScope(
  overrides: overrides,
  child: MaterialApp(
    theme: AppTheme.light(),
    darkTheme: AppTheme.dark(),
    home: page,
  ),
);

/// Stands in for the network. The notifications feed is now a real API call, so the empty state
/// means "the server returned nothing" — it is no longer the only thing the screen can render, and
/// a test that cannot tell empty from failed would not catch the feed breaking.
class _FakeNotificationsSource implements NotificationsRemoteDataSource {
  _FakeNotificationsSource(this._page);
  final NotificationPageDto _page;

  @override
  Future<NotificationPageDto> list({int page = 1, int pageSize = 30}) async => _page;

  @override
  Future<void> markRead(String id) async {}

  @override
  Future<void> markAllRead() async {}
}

void main() {
  setUpAll(() async => initializeDateFormatting('en_IN'));

  testWidgets('Saved shows the empty state with no bookmarks', (tester) async {
    await tester.pumpWidget(_host(const SavedPage()));
    await tester.pumpAndSettle();
    expect(find.text('No saved events yet'), findsOneWidget);
  });

  testWidgets('Notifications shows the empty state when the server returns none', (tester) async {
    await tester.pumpWidget(
      _host(
        const NotificationsPage(),
        overrides: [
          notificationsSourceProvider.overrideWithValue(
            _FakeNotificationsSource(const NotificationPageDto()),
          ),
        ],
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('You’re all caught up'), findsOneWidget);
  });

  testWidgets('Notifications renders what the feed returns', (tester) async {
    await tester.pumpWidget(
      _host(
        const NotificationsPage(),
        overrides: [
          notificationsSourceProvider.overrideWithValue(
            _FakeNotificationsSource(
              NotificationPageDto(
                unreadCount: 1,
                items: [
                  NotificationDto(
                    id: 'n1',
                    kind: 'booking',
                    title: 'Your ticket is confirmed',
                    body: 'See you at Kurx Live.',
                    createdAt: DateTime.now(),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Your ticket is confirmed'), findsOneWidget);
    expect(find.text('You’re all caught up'), findsNothing);
    // Unread, so the mark-all affordance must be offered.
    expect(find.text('Mark all read'), findsOneWidget);
  });

  testWidgets('Notifications surfaces a failure as retryable, not as empty', (tester) async {
    await tester.pumpWidget(
      _host(
        const NotificationsPage(),
        overrides: [
          notificationsSourceProvider.overrideWithValue(_ThrowingNotificationsSource()),
        ],
      ),
    );
    await tester.pumpAndSettle();

    // The distinction that matters: a broken feed must never look like "all caught up".
    expect(find.text('You’re all caught up'), findsNothing);
    expect(find.text('Retry'), findsOneWidget);
  });
}

class _ThrowingNotificationsSource implements NotificationsRemoteDataSource {
  @override
  Future<NotificationPageDto> list({int page = 1, int pageSize = 30}) async =>
      throw Exception('network down');

  @override
  Future<void> markRead(String id) async {}

  @override
  Future<void> markAllRead() async {}
}
