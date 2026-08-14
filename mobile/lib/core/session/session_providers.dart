import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'session_controller.dart';

/// Whether the current user is browsing as a guest (not authenticated). Guests
/// can browse everything; only booking/saving/profile actions prompt sign-in.
final isGuestProvider = Provider<bool>(
  (ref) => ref.watch(sessionControllerProvider).status != AuthStatus.authenticated,
);
