import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/organizer/presentation/widgets/organizer_form_sheet.dart';

/// These tests exist because of a specific, shipped defect: nine organiser editors had a submit
/// button whose `onPressed` was `() => Navigator.pop(context)`. The form closed, nothing was sent,
/// and the user was told nothing — silent data loss that looked like success.
///
/// [OrganizerFormSheet] is the fix, so the guarantees it makes are what these tests pin down:
/// submit really calls the API, a failure keeps the user's typing on screen with the server's
/// reason, and the sheet closes *only* when the write actually succeeded.
void main() {
  Widget host(Widget child) => MaterialApp(
        theme: AppTheme.light(),
        home: Scaffold(body: child),
      );

  /// Opens a sheet and returns a builder that reports whether onSubmit ran.
  Future<void> openSheet(
    WidgetTester tester, {
    required Future<void> Function() onSubmit,
    TextEditingController? controller,
  }) async {
    await tester.pumpWidget(
      host(
        Builder(
          builder: (context) => TextButton(
            onPressed: () => OrganizerFormSheet.show(
              context,
              title: 'New speaker',
              submitLabel: 'Create speaker',
              successMessage: 'Speaker added.',
              fields: (enabled) => [
                TextField(
                  controller: controller,
                  enabled: enabled,
                  decoration: const InputDecoration(labelText: 'Name'),
                ),
              ],
              onSubmit: onSubmit,
            ),
            child: const Text('open'),
          ),
        ),
      ),
    );
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
  }

  testWidgets('submitting calls onSubmit — it does not just close the sheet', (tester) async {
    var called = 0;
    await openSheet(tester, onSubmit: () async => called++);

    await tester.tap(find.text('Create speaker'));
    await tester.pumpAndSettle();

    expect(called, 1, reason: 'the submit button must reach the API, not Navigator.pop');
  });

  testWidgets('a successful submit closes the sheet', (tester) async {
    await openSheet(tester, onSubmit: () async {});

    await tester.tap(find.text('Create speaker'));
    await tester.pumpAndSettle();

    expect(find.text('New speaker'), findsNothing);
  });

  testWidgets('a server refusal keeps the sheet open and shows the reason', (tester) async {
    final name = TextEditingController(text: 'Ada Lovelace');
    await openSheet(
      tester,
      controller: name,
      onSubmit: () async => throw const ApiError(status: 403, code: 'forbidden'),
      // 'forbidden' maps to "You don't have access to that." in ApiError.userMessage.
    );

    await tester.enterText(find.byType(TextField), 'Ada Lovelace');
    await tester.tap(find.text('Create speaker'));
    await tester.pumpAndSettle();

    // Still open — the user's typing must survive a failure.
    expect(find.text('New speaker'), findsOneWidget);
    expect(find.text("You don't have access to that."), findsOneWidget);
    expect(name.text, 'Ada Lovelace');
  });

  testWidgets('an unmapped failure still reports, never silently succeeds', (tester) async {
    await openSheet(tester, onSubmit: () async => throw StateError('boom'));

    await tester.tap(find.text('Create speaker'));
    await tester.pumpAndSettle();

    expect(find.text('New speaker'), findsOneWidget);
    expect(find.text('Something went wrong. Please try again.'), findsOneWidget);
  });

  testWidgets('inputs lock while the write is in flight, so a double-tap cannot double-submit',
      (tester) async {
    var called = 0;
    final gate = Completer<void>();
    await openSheet(
      tester,
      onSubmit: () async {
        called++;
        await gate.future;
      },
    );

    await tester.tap(find.text('Create speaker'));
    await tester.pump(); // start the request, do not settle

    // The label is replaced by a spinner, and the field is disabled.
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(tester.widget<TextField>(find.byType(TextField)).enabled, isFalse);

    gate.complete();
    await tester.pumpAndSettle();
    expect(called, 1);
  });
}
