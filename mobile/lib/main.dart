import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'app.dart';
import 'core/storage/event_cache.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('en_IN');
  await Hive.initFlutter();
  final cacheBox = await Hive.openBox('kurx_cache');

  // Firebase is initialised best-effort (D-096): a build without a valid google-services.json, a
  // handset with no Play Services, or a user who denied notifications must still get a fully
  // working app. Push only accelerates login approval — approvals also arrive by polling
  // `/v1/auth/login/pending`, so a failure here must never block startup.
  try {
    await Firebase.initializeApp();
  } catch (e) {
    debugPrint('Firebase unavailable; continuing without push: $e');
  }

  runApp(
    ProviderScope(
      overrides: [cacheBoxProvider.overrideWithValue(cacheBox)],
      child: const KurxApp(),
    ),
  );
}
