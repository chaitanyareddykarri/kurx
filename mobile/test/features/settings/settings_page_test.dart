import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/theme/theme_mode_controller.dart';
import 'package:kurx_mobile/features/settings/presentation/pages/settings_page.dart';

void main() {
  testWidgets('renders appearance + about, and the theme toggle updates the mode', (tester) async {
    final container = ProviderContainer();
    addTearDown(container.dispose);

    await tester.pumpWidget(UncontrolledProviderScope(
      container: container,
      child: const MaterialApp(home: SettingsPage()),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Appearance'), findsOneWidget);

    // Theme toggle (the segmented control is near the top, always visible).
    expect(container.read(themeModeControllerProvider), ThemeMode.system);
    await tester.tap(find.text('Dark'));
    await tester.pumpAndSettle();
    expect(container.read(themeModeControllerProvider), ThemeMode.dark);

    // Lower tiles are below the fold in the test viewport — scroll them into view.
    await tester.scrollUntilVisible(
      find.text('Open source licenses'),
      200,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('Open source licenses'), findsOneWidget);
    expect(find.text('Version'), findsOneWidget);
    expect(find.text('1.0.0'), findsOneWidget);
  });
}
