import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/organizer/presentation/pages/request_representation_page.dart';

/// D-382 — requesting an institution that is not on Kurx yet, on Flutter.
///
/// The screen exists because its absence was a closed loop: since D-379 an event cannot be created
/// without a verified organization, and Flutter offered no way to ask for one. What is pinned here is
/// that it cannot be submitted empty — a request with no evidence is a rejection with extra steps, and
/// the server refuses it anyway.
void main() {
  Widget host() => const ProviderScope(
        child: MaterialApp(home: RequestRepresentationPage()),
      );

  testWidgets('refuses to submit without a name, and says which field is missing', (tester) async {
    await tester.pumpWidget(host());
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(find.text('Register for verification'), 200,
        scrollable: find.byType(Scrollable).first);
    // `KurxButton` is an `InkWell`, not a `ButtonStyleButton` — a null `onTap` is what disabled means.
    final tap = tester.widget<InkWell>(
      find.ancestor(of: find.text('Register for verification'), matching: find.byType(InkWell)),
    );
    expect(tap.onTap, isNull);
    // The reason sits beside the control: a disabled button cannot explain itself.
    expect(find.text('Enter the organization name'), findsOneWidget);
  });

  testWidgets('with a name typed, the proof is what is still missing', (tester) async {
    await tester.pumpWidget(host());
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextField, 'Organization name'), 'NSRIT College');
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(find.text('Register for verification'), 200,
        scrollable: find.byType(Scrollable).first);
    expect(find.text('Enter the organization name'), findsNothing);
    // Evidence is not optional — an admin has to verify the claim against something.
    expect(find.text('Attach proof of affiliation'), findsOneWidget);
  });

  testWidgets('offers the registry type vocabulary the API accepts', (tester) async {
    await tester.pumpWidget(host());
    await tester.pumpAndSettle();

    // Same list web sends (`organizationTypes`); a client copy that drifts offers a type the API
    // refuses. `college` is the default because it is the common case in India.
    expect(find.text('college'), findsOneWidget);
  });

  testWidgets('applies the theme without throwing', (tester) async {
    await tester.pumpWidget(ProviderScope(
      child: MaterialApp(theme: AppTheme.light(), home: const RequestRepresentationPage()),
    ));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });
}
