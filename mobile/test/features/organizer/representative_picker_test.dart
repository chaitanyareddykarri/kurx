import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/organizer/data/models/event_content_dto.dart';
import 'package:kurx_mobile/features/organizer/presentation/widgets/representative_picker.dart';

/// The authorization's optional account link, on Flutter.
///
/// The gap this closes was cross-surface: web's authorization form has had a signatory picker
/// throughout, and Flutter had no user search of ANY kind — no data source method, no widget — so the
/// field existed on one platform and on neither screen of the other. Representation moving inside
/// Create Event is what made that visible: the field lived only on the workspace page being removed.
///
/// **A link, never a grant** — the picker sends `representativeUserId` and nothing else; authority is
/// `IEventAuthority`'s alone (D-269). Optional everywhere, which is why a failed lookup must never
/// block filing the letter.
void main() {
  const alice = UserSearchDto(id: 'u1', name: 'Alice Rao', username: 'alicer');

  Widget host({UserSearchDto? linked, ValueChanged<UserSearchDto?>? onChanged}) => ProviderScope(
        child: MaterialApp(
          theme: AppTheme.light(),
          home: Scaffold(
            body: RepresentativePicker(
              linked: linked,
              onChanged: onChanged ?? (_) {},
            ),
          ),
        ),
      );

  testWidgets('offers a search field when nothing is linked', (tester) async {
    await tester.pumpWidget(host());
    await tester.pump();

    expect(find.text('Their Kurx account (optional)'), findsOneWidget);
    // Nothing is linked, so there is no account chip and nothing to unlink.
    expect(find.byTooltip('Unlink this account'), findsNothing);
  });

  testWidgets('shows the linked account instead of the search field, and can unlink it',
      (tester) async {
    UserSearchDto? received;
    var called = false;
    await tester.pumpWidget(host(linked: alice, onChanged: (u) {
      received = u;
      called = true;
    }));
    await tester.pump();

    expect(find.text('@alicer'), findsOneWidget);
    // The search field is gone once an account is linked: there is one answer, not a list plus a choice.
    expect(find.text('Their Kurx account (optional)'), findsNothing);

    await tester.tap(find.byTooltip('Unlink this account'));
    await tester.pump();
    expect(called, isTrue);
    expect(received, isNull, reason: 'unlinking must clear the value, not leave the old id attached');
  });

  testWidgets('does not query the index on a one-character term', (tester) async {
    // Debounced AND floored at two characters: a request per keystroke rate-limits the caller out of
    // their own form, and a one-letter term matches most of the index anyway. No provider override is
    // needed precisely because nothing should be dispatched — a lookup here would throw on the real
    // data source and fail this test.
    await tester.pumpWidget(host());
    await tester.enterText(find.byType(TextField), 'a');
    await tester.pump(const Duration(milliseconds: 500));

    expect(tester.takeException(), isNull);
    expect(find.byType(ListTile), findsNothing);
  });
}
