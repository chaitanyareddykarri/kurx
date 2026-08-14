import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_error.dart';

/// Shown when a profile section could not be loaded (D-235).
///
/// The distinction this widget exists to draw: a section refused with 403 renders as *nothing*,
/// because nothing is wrong — the owner chose not to publish it. A section that failed for any other
/// reason renders this, because "we could not load it" and "this person has none" are different
/// claims and only one of them is ever true. Collapsing both into an empty panel let an outage
/// silently make someone's profile look emptier than it is.
class SectionUnavailable extends StatelessWidget {
  const SectionUnavailable({super.key, required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        border: Border.all(color: theme.dividerColor),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        children: [
          Icon(Icons.cloud_off_outlined, size: 16, color: theme.hintColor),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              "$label couldn't be loaded. Try again shortly.",
              style: theme.textTheme.bodySmall?.copyWith(color: theme.hintColor),
            ),
          ),
        ],
      ),
    );
  }
}

/// Renders [onData] when the section loaded, nothing when the server refused it (403), and a
/// [SectionUnavailable] notice for every other failure.
///
/// Used by the panel-style sections (metrics, experience, contributions) that read `.valueOrNull` and
/// therefore could not tell "hidden" from "broken" — both arrived as null.
class SectionPanel<T> extends StatelessWidget {
  const SectionPanel({
    super.key,
    required this.value,
    required this.label,
    required this.onData,
  });

  final AsyncValue<T> value;
  final String label;
  final Widget Function(T? data) onData;

  @override
  Widget build(BuildContext context) => value.when(
        // Panels are secondary content; a spinner per panel would make the profile flicker on every
        // load, so the panel simply renders its own null/empty treatment until data arrives.
        loading: () => onData(null),
        data: onData,
        error: (err, _) => err is ApiError && err.status == 403
            ? const SizedBox.shrink()
            : SectionUnavailable(label: label),
      );
}
