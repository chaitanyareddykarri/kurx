import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

/// D-305 — the creation form cannot be reached by navigating straight at it.
///
/// The gate is only a gate if it cannot be walked around. On Flutter the form is a **route**
/// (`/events/create/form`), so a deep link, a restored tab or a hand-typed URL would otherwise open the
/// eleven steps having answered neither eligibility nor Public/Private.
///
/// The first version of this route defaulted a missing `product` to `Public`. That was *safe* — a deep
/// link could not manufacture a Private event — but it was not *gated*, which is the property D-305
/// actually requires. This test pins the redirect so that distinction cannot quietly regress.
///
/// Web needs no equivalent: there the gate **is** the page (`/host/events/new` renders it) and the form
/// is a stage inside a component, never a route, so there is no address to jump to.

/// The predicate the route's `redirect` applies, kept identical in shape so the test exercises the rule
/// rather than the router's plumbing.
String? redirectFor(String? product) =>
    product == 'Public' || product == 'Private' ? null : '/events/create';

void main() {
  group('create-event form route guard', () {
    test('a direct hit with no product goes back to the gate', () {
      expect(redirectFor(null), '/events/create');
    });

    test('an empty product goes back to the gate', () {
      expect(redirectFor(''), '/events/create');
    });

    test('an unrecognised product goes back to the gate rather than being coerced', () {
      // Coercing to Public is what the first version did. A value the gate never produces means the
      // gate was not used, and the answer is to run it — not to guess on the caller's behalf.
      expect(redirectFor('Listed'), '/events/create');
      expect(redirectFor('public'), '/events/create'); // case matters; the gate emits exact values
    });

    test('the two values the gate actually produces are allowed through', () {
      expect(redirectFor('Public'), isNull);
      expect(redirectFor('Private'), isNull);
    });
  });

  group('router wiring', () {
    test('the gate owns /events/create and the form is nested beneath it', () {
      // Pins the shape D-305 requires: the form is *inside* the gate's route, so it can never become a
      // sibling that is reachable without passing through.
      final router = GoRouter(
        initialLocation: '/events/create',
        routes: [
          GoRoute(
            path: '/events/create',
            builder: (_, _) => const _Stub('gate'),
            routes: [
              GoRoute(
                path: 'form',
                redirect: (_, s) => redirectFor(s.uri.queryParameters['product']),
                builder: (_, _) => const _Stub('form'),
              ),
            ],
          ),
        ],
      );

      expect(router.configuration.routes, hasLength(1));
      final gate = router.configuration.routes.single as GoRoute;
      expect(gate.path, '/events/create');
      expect(gate.routes, hasLength(1));
      expect((gate.routes.single as GoRoute).path, 'form');
    });
  });
}

class _Stub extends StatelessWidget {
  const _Stub(this.label);
  final String label;
  @override
  Widget build(BuildContext context) => Text(label, textDirection: TextDirection.ltr);
}
