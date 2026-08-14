import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/router/app_router.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/auth_providers.dart';

/// Shown when a signed-out visitor opens a screen that needs an account.
///
/// The router used to send them straight to the sign-in form, which reads as the app throwing them
/// into authentication for no stated reason — they tapped "Messages" and got a password field. This
/// names the feature, says what signing in gets them, and offers a way back out, so browsing as a
/// guest stays a deliberate mode rather than a series of dead ends.
///
/// Not a second auth system: both actions hand off to the existing login routes, and the return
/// location goes through the same [loginReturnToProvider] the guest-aware screens already use.
class GuestGatePage extends ConsumerWidget {
  const GuestGatePage({super.key, required this.next});

  /// Where the visitor was heading — restored after a successful sign-in.
  final String next;

  /// What each protected area is called, and what an account unlocks there. Matched longest-prefix
  /// first so `/profile/devices` beats `/profile`.
  static const _features = <({String prefix, String title, String blurb})>[
    (
      prefix: '/events/create',
      title: 'Creating events',
      blurb: 'Publish and manage your own events, sell tickets, and see who registered.',
    ),
    (
      prefix: '/chats',
      title: 'Messages',
      blurb: 'Chat with organisers and the people attending events with you.',
    ),
    (
      prefix: '/workspace',
      title: 'Workspace',
      blurb: 'Track the events you have joined, with schedules, updates and materials in one place.',
    ),
    (
      prefix: '/allies',
      title: 'Community',
      blurb: 'Connect with people you meet at events and follow what they are attending.',
    ),
    (
      prefix: '/posts',
      title: 'Posts',
      blurb: 'Follow event conversations, post updates, and join the discussion.',
    ),
    (
      prefix: '/tickets',
      title: 'Your tickets',
      blurb: 'Keep every ticket and QR code together, ready to scan at the gate.',
    ),
    (
      prefix: '/orders',
      title: 'Your orders',
      blurb: 'Review past bookings, payments and refunds.',
    ),
    (
      prefix: '/saved',
      title: 'Saved events',
      blurb: 'Bookmark events you are interested in and come back to them later.',
    ),
    (
      prefix: '/notifications',
      title: 'Notifications',
      blurb: 'Get reminders and updates about the events you have joined.',
    ),
    (
      prefix: '/calendar',
      title: 'Your calendar',
      blurb: 'See everything you have registered for laid out by date.',
    ),
    (
      prefix: '/certificates',
      title: 'Certificates',
      blurb: 'Collect and share the certificates you earn from events you attend.',
    ),
    (
      prefix: '/profile',
      title: 'Your profile',
      blurb: 'Build a profile, claim a username, and let organisers know who you are.',
    ),
    (
      prefix: '/settings',
      title: 'Account settings',
      blurb: 'Manage your account, security and notification preferences.',
    ),
  ];

  @visibleForTesting
  static ({String title, String blurb}) featureFor(String location) {
    var best = (prefix: '', title: 'This feature', blurb: 'Sign in to unlock the rest of Kurx.');
    for (final f in _features) {
      if (location.startsWith(f.prefix) && f.prefix.length > best.prefix.length) best = f;
    }
    return (title: best.title, blurb: best.blurb);
  }

  void _dismiss(BuildContext context) =>
      Navigator.canPop(context) ? Navigator.pop(context) : context.go(Routes.events);

  void _go(BuildContext context, WidgetRef ref, String route) {
    // The destination the visitor originally wanted, consumed by the router once authenticated.
    ref.read(loginReturnToProvider.notifier).state = next;
    context.push(route);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final feature = featureFor(next);
    final colors = context.kurx;
    final text = Theme.of(context).textTheme;

    return PopScope(
      // Arriving here by redirect leaves nothing on the stack, so an uninterrupted system back would
      // close the app rather than return to browsing.
      canPop: Navigator.canPop(context),
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) context.go(Routes.events);
      },
      child: Scaffold(
        appBar: AppBar(
          leading: IconButton(
            icon: const Icon(Icons.close),
            tooltip: 'Continue browsing',
            onPressed: () => _dismiss(context),
          ),
          title: Text(feature.title),
        ),
        body: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(KSpace.xl),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Icon(Icons.lock_outline_rounded, size: 56, color: colors.muted),
                    const SizedBox(height: KSpace.lg),
                    Text(
                      'Sign in to use this feature',
                      style: text.headlineSmall,
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: KSpace.sm),
                    Text(
                      feature.blurb,
                      style: text.bodyMedium?.copyWith(color: colors.muted),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: KSpace.xl),
                    FilledButton(
                      onPressed: () => _go(context, ref, Routes.login),
                      child: const Text('Sign in'),
                    ),
                    const SizedBox(height: KSpace.sm),
                    OutlinedButton(
                      // The one-time-code route is the bootstrap path for an account with no
                      // password or trusted device yet — i.e. one that does not exist. Registration
                      // continues from there through the existing onboarding gate.
                      onPressed: () => _go(context, ref, Routes.loginOtp),
                      child: const Text('Create account'),
                    ),
                    const SizedBox(height: KSpace.sm),
                    TextButton(
                      onPressed: () => _dismiss(context),
                      child: const Text('Not now — keep browsing'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
