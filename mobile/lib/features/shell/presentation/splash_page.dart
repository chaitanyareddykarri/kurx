import 'package:flutter/material.dart';

import '../../../core/app_info.dart';

/// Shown briefly while the session bootstraps (secure-storage token check).
class SplashPage extends StatelessWidget {
  const SplashPage({super.key});

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.celebration_rounded, size: 72, color: scheme.primary),
            const SizedBox(height: 16),
            Text(AppInfo.name, style: Theme.of(context).textTheme.headlineMedium),
            const SizedBox(height: 6),
            Text(AppInfo.tagline,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: scheme.onSurfaceVariant)),
            const SizedBox(height: 28),
            const SizedBox(width: 28, height: 28, child: CircularProgressIndicator(strokeWidth: 3)),
          ],
        ),
      ),
    );
  }
}
