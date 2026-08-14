import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_card.dart';
import '../../../../common/widgets/kurx_shell_app_bar.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../orders/presentation/providers/attendee_providers.dart';
import '../../../organizer/data/models/event_manage_dto.dart';
import '../../../organizer/presentation/providers/organizer_providers.dart';

/// The **Workspace** tab — every event this person has a role in.
///
/// Workspaces are unlocked, never navigated to speculatively (product flow, "Workspace Access"):
///
/// * **Hosting** — an event you run. The full host workspace only opens once the event is past admin
///   approval, so a Draft/Under-review row shows its state and is not presented as a working
///   workspace. Staff use this same workspace; permissions differ, the surface does not.
/// * **Participating** — an event you registered for. Opens the participant workspace.
///
/// A person is routinely both, for different events, which is why this is one list keyed by role
/// per event rather than a mode the whole app is in.
///
/// **Events are the organising principle here, not organisations (D-267).** This page used to read
/// the caller's organisations and then fetch each one's events, so it rendered as a list of
/// organisations with events nested inside — and with no organisation it said "You are not part of any
/// organization yet", which told someone who simply had not made an event yet to go and register an
/// institution. It now reads `GET /v1/me/events` once; the organisation an event represents is a line
/// on its row.
class WorkspaceHubPage extends ConsumerWidget {
  const WorkspaceHubPage({super.key});

  /// Statuses where the host workspace is genuinely open. Everything else is pre-approval and gets
  /// a state chip instead of a working link (flow: "Admin Review → Approved → Host Workspace Opens").
  static const _openHostStates = {'published', 'scheduled', 'live', 'completed', 'closed'};

  /// Views over the caller's own events. Status filters on one list, never separate containers.
  static const _views = <(String, String)>[
    ('hosted', 'Hosted'),
    ('drafts', 'Drafts'),
    ('pending', 'Pending approval'),
    ('archived', 'Archived'),
  ];

  static bool _matches(String view, String status) => switch (view) {
        'drafts' => status == 'draft',
        // D-266 M4: with the platform, awaiting a decision. `changesrequested`/`rejected` are decided
        // outcomes the host must act on, not a pending queue. Kept in step with the web workspace.
        'pending' => status == 'pendingreview' || status == 'underreview',
        'archived' => status == 'archived' || status == 'cancelled',
        _ => true,
      };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final hosted = ref.watch(myEventsProvider);
    final participations = ref.watch(myParticipationsProvider);
    final view = ref.watch(_workspaceViewProvider);

    return Scaffold(
      appBar: const KurxShellAppBar(title: 'Workspace'),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(myEventsProvider);
          ref.invalidate(myParticipationsProvider);
        },
        child: ListView(
          padding: const EdgeInsets.fromLTRB(KSpace.lg, KSpace.lg, KSpace.lg, KSpace.xxxl),
          children: [
            // "Create event" was here until D-305, which moved it to **Profile**. Workspace is the
            // user's event *activity* hub — participated plus approved created — and a primary creation
            // card at the top of it is what made it read as an organizer dashboard. What stands from
            // D-267 is that creation still needs no organisation; only the door moved.
            //
            // Kept as a pointer rather than deleted outright: someone who learned the old position finds
            // the new one instead of finding nothing.
            KurxCard(
              onTap: () => context.push('/profile'),
              child: Row(
                children: [
                  Icon(Icons.person_outline_rounded, color: context.kurx.muted),
                  const SizedBox(width: KSpace.md),
                  Expanded(
                    child: Text(
                      'Create an event from your profile',
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                            color: context.kurx.muted,
                          ),
                    ),
                  ),
                  Icon(Icons.chevron_right_rounded, color: context.kurx.muted),
                ],
              ),
            ),
            const SizedBox(height: KSpace.xl),
            const _SectionHeader(
              title: 'Hosting',
              subtitle: 'Events you run. Opens after admin approval.',
            ),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  for (final (key, label) in _views)
                    Padding(
                      padding: const EdgeInsets.only(right: KSpace.sm),
                      child: ChoiceChip(
                        label: Text(label),
                        selected: view == key,
                        onSelected: (_) =>
                            ref.read(_workspaceViewProvider.notifier).state = key,
                      ),
                    ),
                ],
              ),
            ),
            const SizedBox(height: KSpace.md),
            hosted.when(
              loading: () => const _Loading(),
              error: (_, _) => const _Unavailable(what: 'your events'),
              data: (list) {
                final shown =
                    list.where((e) => _matches(view, e.status.toLowerCase())).toList();
                if (shown.isEmpty) {
                  return _Hint(view == 'hosted'
                      ? 'No events yet. Create one to get started.'
                      : 'Nothing here.');
                }
                return Column(
                  children: [for (final e in shown) _HostEventRow(event: e)],
                );
              },
            ),
            const SizedBox(height: KSpace.xl),
            const _SectionHeader(
              title: 'Participating',
              subtitle: 'Events you registered for.',
            ),
            participations.when(
              loading: () => const _Loading(),
              error: (_, _) => const _Unavailable(what: 'registrations'),
              data: (list) {
                // `invited`/`declined`/`removed` are not participation — only an accepted role
                // unlocks a workspace.
                final active = list.where((p) => p.state == 'accepted').toList();
                if (active.isEmpty) {
                  return const _Hint('Register for an event and its workspace appears here.');
                }
                return Column(
                  children: [
                    for (final p in active)
                      KurxCard(
                        onTap: () => context.push('/workspace/${p.eventId}'),
                        child: Row(
                          children: [
                            Icon(Icons.groups_outlined, color: context.kurx.accent),
                            const SizedBox(width: KSpace.md),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    p.customLabel ?? _roleLabel(p.roleSlug),
                                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                          fontWeight: FontWeight.w600,
                                          color: context.kurx.text,
                                        ),
                                  ),
                                  Text(
                                    'Participant workspace',
                                    style: Theme.of(context)
                                        .textTheme
                                        .bodySmall
                                        ?.copyWith(color: context.kurx.muted),
                                  ),
                                ],
                              ),
                            ),
                            Icon(Icons.chevron_right_rounded, color: context.kurx.muted),
                          ],
                        ),
                      ),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  static String _roleLabel(String slug) {
    final spaced = slug.replaceAll('_', ' ');
    return spaced.isEmpty ? 'Participant' : spaced[0].toUpperCase() + spaced.substring(1);
  }
}

/// Which status filter the Hosting list is showing. Page-local UI state, not server state.
final _workspaceViewProvider = StateProvider<String>((ref) => 'hosted');

class _HostEventRow extends StatelessWidget {
  const _HostEventRow({required this.event});

  final EventManageDto event;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final status = event.status.toLowerCase();
    final isOpen = WorkspaceHubPage._openHostStates.contains(status);
    // Representation is a property of the event, shown on its row — never a folder it lives in.
    final representing = event.representation?.kind == 'organization'
        ? event.representation?.organizationName
        : null;

    return KurxCard(
      onTap: () => context.push('/events/${event.id}/manage'),
      child: Row(
        children: [
          Icon(
            isOpen ? Icons.dashboard_rounded : Icons.hourglass_empty_rounded,
            color: isOpen ? c.accent : c.muted,
          ),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  event.title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context)
                      .textTheme
                      .bodyMedium
                      ?.copyWith(fontWeight: FontWeight.w600, color: c.text),
                ),
                Text(
                  [
                    isOpen ? 'Host workspace' : 'Opens after approval · ${event.status}',
                    if (representing != null) 'representing $representing',
                  ].join(' · '),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
                ),
              ],
            ),
          ),
          Icon(Icons.chevron_right_rounded, color: c.muted),
        ],
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title, required this.subtitle});

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: Theme.of(context)
                .textTheme
                .titleMedium
                ?.copyWith(fontWeight: FontWeight.w700, color: c.text),
          ),
          Text(subtitle, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted)),
        ],
      ),
    );
  }
}

class _Loading extends StatelessWidget {
  const _Loading();

  @override
  Widget build(BuildContext context) => const Padding(
        padding: EdgeInsets.all(KSpace.lg),
        child: Center(child: CircularProgressIndicator()),
      );
}

class _Hint extends StatelessWidget {
  const _Hint(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: KSpace.md),
        child: Text(text, style: TextStyle(color: context.kurx.muted)),
      );
}

class _Unavailable extends StatelessWidget {
  const _Unavailable({required this.what});

  final String what;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: KSpace.md),
        child: EmptyState(
          icon: Icons.cloud_off_outlined,
          title: 'Could not load $what',
          message: 'Pull to refresh.',
        ),
      );
}
