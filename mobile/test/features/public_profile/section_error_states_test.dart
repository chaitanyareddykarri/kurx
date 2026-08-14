import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/common/widgets/async_value_view.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/features/public_profile/presentation/widgets/section_unavailable.dart';

/// D-235 — a privacy refusal and an outage must not render the same way.
///
/// Before this, the two failure modes were mirror images of each other on the two clients: web
/// swallowed *everything* into an empty list, so an outage looked like "this person has nothing";
/// Flutter surfaced everything through the retry view, so a section the owner had chosen to hide
/// looked like the app was broken and invited the viewer to retry an endpoint that would keep
/// refusing. Both were wrong in the same way — they conflated a statement about the *person* with a
/// statement about the *system*.
void main() {
  Widget host(Widget child) => ProviderScope(
        child: MaterialApp(home: Scaffold(body: child)),
      );

  group('AsyncValueView', () {
    testWidgets('a 403 renders the hidden widget, never the retry view', (tester) async {
      await tester.pumpWidget(host(
        AsyncValueView<List<String>>(
          value: const AsyncValue.error(
            ApiError(status: 403, code: 'forbidden'),
            StackTrace.empty,
          ),
          onRetry: () {},
          hidden: const SizedBox.shrink(),
          data: (d) => const Text('data'),
        ),
      ));
      await tester.pump();

      expect(find.text('Retry'), findsNothing);
      expect(find.text('data'), findsNothing);
    });

    testWidgets('a 500 still renders the retry view even when a hidden widget is supplied',
        (tester) async {
      await tester.pumpWidget(host(
        AsyncValueView<List<String>>(
          value: const AsyncValue.error(
            ApiError(status: 500, code: 'server_error'),
            StackTrace.empty,
          ),
          onRetry: () {},
          hidden: const SizedBox.shrink(),
          data: (d) => const Text('data'),
        ),
      ));
      await tester.pump();

      // The distinction is the whole point: hidden is supplied, but this is not a refusal.
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('an offline failure (status 0) renders the retry view', (tester) async {
      await tester.pumpWidget(host(
        AsyncValueView<List<String>>(
          value: const AsyncValue.error(
            ApiError(status: 0, code: 'network_error'),
            StackTrace.empty,
          ),
          onRetry: () {},
          hidden: const SizedBox.shrink(),
          data: (d) => const Text('data'),
        ),
      ));
      await tester.pump();

      expect(find.text('Retry'), findsOneWidget);
    });
  });

  group('SectionPanel', () {
    testWidgets('a 403 renders nothing at all', (tester) async {
      await tester.pumpWidget(host(
        SectionPanel<String>(
          value: const AsyncValue.error(
            ApiError(status: 403, code: 'forbidden'),
            StackTrace.empty,
          ),
          label: 'Metrics',
          onData: (d) => const Text('panel'),
        ),
      ));
      await tester.pump();

      expect(find.byType(SectionUnavailable), findsNothing);
      expect(find.text('panel'), findsNothing);
    });

    testWidgets('a server error says so instead of rendering an empty panel', (tester) async {
      await tester.pumpWidget(host(
        SectionPanel<String>(
          value: const AsyncValue.error(
            ApiError(status: 503, code: 'unavailable'),
            StackTrace.empty,
          ),
          label: 'Metrics',
          onData: (d) => const Text('panel'),
        ),
      ));
      await tester.pump();

      expect(find.byType(SectionUnavailable), findsOneWidget);
      expect(find.textContaining("couldn't be loaded"), findsOneWidget);
    });

    testWidgets('a parse failure is an outage, not a privacy choice', (tester) async {
      // response_parse_failed carries status 0 and means the contract drifted — precisely the case
      // that used to be repainted as "no connection" (see api_guard.dart).
      await tester.pumpWidget(host(
        SectionPanel<String>(
          value: const AsyncValue.error(
            ApiError(status: 0, code: 'response_parse_failed'),
            StackTrace.empty,
          ),
          label: 'Metrics',
          onData: (d) => const Text('panel'),
        ),
      ));
      await tester.pump();

      expect(find.byType(SectionUnavailable), findsOneWidget);
    });

    testWidgets('data renders through onData', (tester) async {
      await tester.pumpWidget(host(
        SectionPanel<String>(
          value: const AsyncValue.data('x'),
          label: 'Metrics',
          onData: (d) => Text(d ?? 'null'),
        ),
      ));
      await tester.pump();

      expect(find.text('x'), findsOneWidget);
    });
  });
}
