import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/event_cache.dart';

/// The user's theme preference, persisted in the (non-secret) Hive cache box.
/// Falls back to system when no box is available (e.g. in unit tests).
class ThemeModeController extends Notifier<ThemeMode> {
  static const _key = 'theme_mode';

  @override
  ThemeMode build() {
    try {
      final stored = ref.read(cacheBoxProvider).get(_key) as String?;
      return ThemeMode.values.where((m) => m.name == stored).firstOrNull ?? ThemeMode.system;
    } catch (_) {
      return ThemeMode.system;
    }
  }

  void setMode(ThemeMode mode) {
    state = mode;
    try {
      ref.read(cacheBoxProvider).put(_key, mode.name);
    } catch (_) {
      // No persistent box (tests) — in-memory only.
    }
  }
}

final themeModeControllerProvider =
    NotifierProvider<ThemeModeController, ThemeMode>(ThemeModeController.new);
