import 'package:flutter/material.dart';

/// A thin, persistent guest-mode indicator + sign-in CTA for app-bar bottoms.
/// Never a modal, so it doesn't interrupt browsing.
class GuestBanner extends StatelessWidget implements PreferredSizeWidget {
  const GuestBanner({super.key, required this.onSignIn});

  final VoidCallback onSignIn;

  @override
  Size get preferredSize => const Size.fromHeight(44);

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Material(
      color: scheme.secondaryContainer,
      child: SafeArea(
        top: false,
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 2),
          child: Row(
            children: [
              Icon(Icons.person_outline, size: 18, color: scheme.onSecondaryContainer),
              const SizedBox(width: 8),
              Expanded(
                child: Text('Browsing as guest',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(color: scheme.onSecondaryContainer)),
              ),
              TextButton(onPressed: onSignIn, child: const Text('Sign in')),
            ],
          ),
        ),
      ),
    );
  }
}
