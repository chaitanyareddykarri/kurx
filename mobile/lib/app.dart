import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/push/push_bootstrap.dart';
import 'core/router/app_router.dart';
import 'core/session/session_controller.dart';
import 'core/theme/app_theme.dart';
import 'core/theme/theme_mode_controller.dart';

class KurxApp extends ConsumerStatefulWidget {
  const KurxApp({super.key});

  @override
  ConsumerState<KurxApp> createState() => _KurxAppState();
}

class _KurxAppState extends ConsumerState<KurxApp> {
  @override
  void initState() {
    super.initState();
    // Resolve auth status from stored tokens on cold start.
    ref.read(sessionControllerProvider.notifier).bootstrap();
  }

  @override
  Widget build(BuildContext context) {
    // Start push once the session is authenticated (D-096/D-107). Without this the whole push
    // stack — FCM token registration, login-approval delivery, chat deep links — was built and
    // never reached: nothing outside push_bootstrap.dart referenced PushBootstrap, so
    // `start()` never ran and no device token was ever registered.
    //
    // `start()` is idempotent and degrades to polling when push is unavailable, so listening for
    // every transition into `authenticated` (cold-start bootstrap *and* fresh sign-in) is safe.
    ref.listen(sessionControllerProvider, (previous, next) {
      if (next.status == AuthStatus.authenticated &&
          previous?.status != AuthStatus.authenticated) {
        ref.read(pushBootstrapProvider).start();
      }
    });

    final router = ref.watch(routerProvider);
    final themeMode = ref.watch(themeModeControllerProvider);
    return MaterialApp.router(
      title: 'Kurx',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: themeMode,
      routerConfig: router,
      supportedLocales: const [Locale('en', 'IN'), Locale('hi', 'IN')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
    );
  }
}
