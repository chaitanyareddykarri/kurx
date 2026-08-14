import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/competition_dtos.dart';
import '../providers/competition_providers.dart';

/// Team formation — `GET/POST /v1/events/{id}/teams`, join requests, invites, leave
/// (V3 Phase 10).
///
/// Team formation is heavily policy-gated server-side (`TeamPolicy`: min/max size, formation mode,
/// join approval, lock deadlines, cross-org rules). None of that is re-implemented here — the
/// client offers the action and renders whichever rule the server invokes when it refuses, so a
/// policy change needs no client release.
class TeamFormationPage extends ConsumerWidget {
  const TeamFormationPage({
    super.key,
    required this.eventId,
    required this.ticketTypeId,
  });

  final String eventId;

  /// Teams are scoped to a competition ticket type — `POST …/teams` takes it as a query parameter.
  final String ticketTypeId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Teams'),
        actions: [
          TextButton.icon(
            onPressed: () => _createTeam(context, ref),
            icon: const Icon(Icons.group_add_outlined, size: 18),
            label: const Text('Create'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(eventTeamsProvider(eventId));
          await ref.read(eventTeamsProvider(eventId).future);
        },
        child: AsyncValueView(
          value: ref.watch(eventTeamsProvider(eventId)),
          onRetry: () => ref.invalidate(eventTeamsProvider(eventId)),
          isEmpty: (list) => list.isEmpty,
          empty: EmptyState(
            icon: Icons.groups_outlined,
            title: 'No teams yet',
            message: 'Be the first — create a team and invite your people.',
            actionLabel: 'Create a team',
            onAction: () => _createTeam(context, ref),
          ),
          data: (teams) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: teams.length,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
            itemBuilder: (_, i) => _TeamCard(
              team: teams[i],
              onJoin: () => _requestJoin(context, ref, teams[i]),
              onLeave: () => _leave(context, ref, teams[i]),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _createTeam(BuildContext context, WidgetRef ref) async {
    final name = TextEditingController();
    final tagline = TextEditingController();

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: context.kurx.cardSurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(KRadius.xl)),
      ),
      builder: (_) => _CreateTeamSheet(
        nameController: name,
        taglineController: tagline,
        onSubmit: (n, t) => ref.read(competitionActionsProvider).createTeam(
              eventId,
              ticketTypeId: ticketTypeId,
              name: n,
              tagline: t,
            ),
      ),
    );
  }

  Future<void> _requestJoin(BuildContext context, WidgetRef ref, TeamDto team) async {
    try {
      await ref.read(competitionActionsProvider).requestToJoin(eventId, team.id);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Request sent to ${team.name}.')),
        );
      }
    } on ApiError catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(_explain(e))));
      }
    }
  }

  Future<void> _leave(BuildContext context, WidgetRef ref, TeamDto team) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Leave team'),
        content: Text('Leave ${team.name}? You may not be able to rejoin once the roster locks.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(ctx).pop(false), child: const Text('Stay')),
          FilledButton(onPressed: () => Navigator.of(ctx).pop(true), child: const Text('Leave')),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;

    try {
      await ref.read(competitionActionsProvider).leaveTeam(eventId, team.id);
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('You left the team.')));
      }
    } on ApiError catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(_explain(e))));
      }
    }
  }

  static String _explain(ApiError e) => switch (e.code) {
        'team_locked' || 'roster_locked' =>
          'The roster for this event is locked — teams can no longer change.',
        'team_full' => 'That team is already at its maximum size.',
        'max_teams_reached' => 'You are already in the maximum number of teams for this event.',
        'already_member' => 'You are already on this team.',
        'requires_ticket' => 'You need a ticket for this event before joining a team.',
        _ => e.userMessage,
      };
}

class _CreateTeamSheet extends StatefulWidget {
  const _CreateTeamSheet({
    required this.nameController,
    required this.taglineController,
    required this.onSubmit,
  });

  final TextEditingController nameController;
  final TextEditingController taglineController;
  final Future<TeamDto> Function(String name, String? tagline) onSubmit;

  @override
  State<_CreateTeamSheet> createState() => _CreateTeamSheetState();
}

class _CreateTeamSheetState extends State<_CreateTeamSheet> {
  bool _busy = false;
  String? _error;

  Future<void> _submit() async {
    final name = widget.nameController.text.trim();
    if (name.isEmpty) {
      setState(() => _error = 'Give your team a name.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final tagline = widget.taglineController.text.trim();
      await widget.onSubmit(name, tagline.isEmpty ? null : tagline);
      if (!mounted) return;
      Navigator.of(context).pop();
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text('Created $name.')));
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = TeamFormationPage._explain(e);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: EdgeInsets.fromLTRB(
        KSpace.lg,
        KSpace.lg,
        KSpace.lg,
        MediaQuery.viewInsetsOf(context).bottom + KSpace.lg,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Create a team',
              style: TextStyle(color: c.text, fontWeight: FontWeight.w800, fontSize: 17)),
          const SizedBox(height: KSpace.lg),
          TextField(
            controller: widget.nameController,
            enabled: !_busy,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(labelText: 'Team name'),
          ),
          const SizedBox(height: KSpace.md),
          TextField(
            controller: widget.taglineController,
            enabled: !_busy,
            textCapitalization: TextCapitalization.sentences,
            decoration: const InputDecoration(labelText: 'Tagline (optional)'),
          ),
          if (_error != null) ...[
            const SizedBox(height: KSpace.md),
            Semantics(
              liveRegion: true,
              child: Text(_error!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ),
          ],
          const SizedBox(height: KSpace.lg),
          FilledButton(
            onPressed: _busy ? null : _submit,
            child: _busy
                ? const SizedBox(
                    height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Create team'),
          ),
        ],
      ),
    );
  }
}

class _TeamCard extends StatelessWidget {
  const _TeamCard({required this.team, required this.onJoin, required this.onLeave});

  final TeamDto team;
  final VoidCallback onJoin;
  final VoidCallback onLeave;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final locked = team.state == 'locked';

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(team.name,
                    style: TextStyle(
                        color: c.text, fontWeight: FontWeight.w700, fontSize: 16)),
              ),
              Text('${team.activeMemberCount}',
                  style: TextStyle(color: c.accent, fontWeight: FontWeight.w700)),
              const SizedBox(width: KSpace.xs),
              Icon(Icons.person_outline, size: 16, color: c.accent),
            ],
          ),
          if (team.tagline != null && team.tagline!.isNotEmpty) ...[
            const SizedBox(height: KSpace.xs),
            Text(team.tagline!, style: TextStyle(color: c.muted)),
          ],
          if (team.members.isNotEmpty) ...[
            const SizedBox(height: KSpace.sm),
            Wrap(
              spacing: KSpace.sm,
              runSpacing: KSpace.xs,
              children: [
                for (final m in team.members.where((m) => m.state == 'active'))
                  Chip(
                    label: Text(m.name.isEmpty ? 'Member' : m.name),
                    visualDensity: VisualDensity.compact,
                  ),
              ],
            ),
          ],
          const SizedBox(height: KSpace.sm),
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              TextButton(onPressed: onLeave, child: const Text('Leave')),
              const SizedBox(width: KSpace.sm),
              FilledButton.tonal(
                onPressed: locked ? null : onJoin,
                child: Text(locked ? 'Locked' : 'Ask to join'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
