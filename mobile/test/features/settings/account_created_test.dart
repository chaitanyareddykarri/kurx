import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:intl/intl.dart';
import 'package:kurx_mobile/features/auth/domain/entities/current_user.dart';
import 'package:kurx_mobile/features/auth/presentation/providers/auth_providers.dart';
import 'package:kurx_mobile/features/settings/presentation/pages/account_page.dart';
import 'package:kurx_mobile/features/settings/presentation/providers/account_providers.dart';

/// Settings → Account shows the exact account-creation time, converted to the device's zone.
///
/// The value comes from the already-loaded `/v1/me` record, so this also pins that the screen makes no
/// second request for something it is already holding.
void main() {
  setUpAll(() async => initializeDateFormatting('en_IN'));

  /// A fixed UTC instant — 09:12 UTC, which is 14:42 in IST. Chosen so the local rendering differs
  /// from the UTC one in both the hour AND the minute, making a missing `.toLocal()` unmistakable on
  /// a half-hour-offset machine rather than only on whole-hour ones.
  final createdAtUtc = DateTime.utc(2026, 8, 9, 9, 12, 30);

  Widget host(CurrentUser? user) => ProviderScope(
        overrides: [
          currentUserProvider.overrideWith((ref) => user),
          // The screen also lists previous usernames; stubbed so the test needs no network.
          usernameHistoryProvider.overrideWith((ref) async => []),
        ],
        child: const MaterialApp(home: AccountPage()),
      );

  CurrentUser userWith(DateTime? createdAt) => CurrentUser(
        id: 'u1',
        phone: '+919000000001',
        name: 'Asha',
        username: 'asha',
        needsOnboarding: false,
        createdAt: createdAt,
      );

  testWidgets('shows the exact creation time in the device timezone', (tester) async {
    await tester.pumpWidget(host(userWith(createdAtUtc)));
    await tester.pump();

    final expectedLocal =
        DateFormat('d MMMM yyyy, h:mm a', 'en_IN').format(createdAtUtc.toLocal());

    expect(find.text('Account created'), findsOneWidget);
    expect(find.text(expectedLocal), findsOneWidget);

    // On any machine that is not on UTC, the naive rendering (formatting the raw UTC value) is a
    // different string — so this assertion is what actually catches a dropped `.toLocal()`.
    if (createdAtUtc.toLocal().timeZoneOffset != Duration.zero) {
      final naiveUtc = DateFormat('d MMMM yyyy, h:mm a', 'en_IN').format(createdAtUtc);
      expect(find.text(naiveUtc), findsNothing);
    }
  });

  testWidgets('says so plainly when the backend sent no timestamp', (tester) async {
    // An older backend, or a response parsed before `created_at` existed. Rendering an empty string
    // or a fabricated "now" would both be worse than admitting it is unknown.
    await tester.pumpWidget(host(userWith(null)));
    await tester.pump();

    expect(find.text('Account created'), findsOneWidget);
    expect(find.text('Not available'), findsOneWidget);
  });
}
