import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/empty_state.dart';
import '../providers/organizer_providers.dart';

/// Resolves an event's organisation from the event itself, for the management routes (D-267).
///
/// The manage screens are addressed as `/events/:eventId/manage/...` — an event is a thing a person
/// owns, not a folder inside an organisation, and the old `/org/:orgId/events/:eventId/...` shape made
/// every deep link carry an organisation the caller had to have picked first. The screens themselves
/// still need `orgId` because the management sub-resources are org-scoped on the server
/// (`/v1/orgs/{orgId}/events/{eventId}/…`), so exactly one place resolves it: here, from
/// `GET /v1/events/{eventId}`.
///
/// This is a route wrapper rather than a change to every screen's constructor: the screens keep taking
/// `orgId` and know nothing about how it was found.
class EventManageScope extends ConsumerWidget {
  const EventManageScope({super.key, required this.eventId, required this.builder});

  final String eventId;
  final Widget Function(String orgId) builder;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final event = ref.watch(eventManageDetailProvider(eventId));
    return event.when(
      loading: () => const Scaffold(body: Center(child: CircularProgressIndicator())),
      error: (_, _) => Scaffold(
        appBar: AppBar(),
        body: EmptyState(
          icon: Icons.cloud_off_outlined,
          title: 'Could not open this event',
          message: 'It may have been removed, or you may no longer manage it.',
          actionLabel: 'Retry',
          onAction: () => ref.invalidate(eventManageDetailProvider(eventId)),
        ),
      ),
      // The organisation the event REPRESENTS (D-268), which is what the org-scoped management
      // sub-resource URLs are keyed on. Prefers the D-273a name and falls back to the deprecated
      // `org_id` so the app still works against a server deployed before the rename.
      data: (e) => builder(e.representingOrgId ?? e.orgId ?? ''),
    );
  }
}
