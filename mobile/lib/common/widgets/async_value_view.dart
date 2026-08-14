import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_error.dart';
import 'empty_state.dart';
import 'error_retry.dart';
import 'fill_viewport.dart';

/// The one loading / error / (empty) / retry pattern every data-driven screen uses,
/// so error handling and retry are never reinvented per screen.
class AsyncValueView<T> extends StatelessWidget {
  const AsyncValueView({
    super.key,
    required this.value,
    required this.data,
    required this.onRetry,
    this.isEmpty,
    this.empty,
    this.loading,
    this.hidden,
  });

  final AsyncValue<T> value;
  final Widget Function(T data) data;
  final VoidCallback onRetry;

  /// Optional emptiness check + empty widget for list-like data.
  final bool Function(T data)? isEmpty;
  final Widget? empty;

  /// Optional custom loading widget (e.g. a skeleton); defaults to a spinner.
  final Widget? loading;

  /// Shown instead of the error view when the server answered **403 — you may not see this** (D-235).
  ///
  /// A refused section is not a failure: nothing went wrong, the owner simply chose not to show it,
  /// and offering "Something went wrong · Retry" invites the viewer to hammer an endpoint that will
  /// keep refusing while implying the app is broken. Screens that pass nothing here keep the previous
  /// behaviour, so this is additive.
  final Widget? hidden;

  static bool _isForbidden(Object err) => err is ApiError && err.status == 403;

  @override
  Widget build(BuildContext context) {
    return value.when(
      loading: () => loading ?? const Center(child: CircularProgressIndicator()),
      error: (err, _) => hidden != null && _isForbidden(err)
          ? hidden!
          : FillViewport(child: ErrorRetry.fromError(error: err, onRetry: onRetry)),
      data: (d) {
        if (isEmpty != null && isEmpty!(d)) {
          return FillViewport(
            child: empty ??
                const EmptyState(
                  icon: Icons.event_busy_outlined,
                  title: 'Nothing here yet',
                  message: 'Check back soon.',
                ),
          );
        }
        return data(d);
      },
    );
  }
}
