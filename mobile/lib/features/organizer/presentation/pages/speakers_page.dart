import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_badge.dart';
import '../../../../common/widgets/kurx_user_tile.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../public_profile/presentation/providers/public_profile_providers.dart';
import '../../../public_profile/presentation/widgets/ally_connect_button.dart';
import '../../data/models/event_content_dto.dart';
import '../providers/event_content_providers.dart';
import '../widgets/organizer_form_sheet.dart';

/// Event speaker lineup — the speakers assigned to this event, plus the org catalogue they are
/// drawn from (`SpeakerEndpoints`: org-scoped CRUD + per-event assign/unassign).
///
/// Two lists because the backend has two: a speaker is created once on the organisation and then
/// attached to any number of its events, so "remove from event" and "delete from catalogue" are
/// genuinely different actions and are kept visibly apart.
class SpeakersPage extends ConsumerWidget {
  const SpeakersPage({super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final e = (orgId: orgId, eventId: eventId);

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Speakers'),
        actions: [
          TextButton.icon(
            onPressed: () => _createSpeaker(context, ref),
            icon: const Icon(Icons.person_add_outlined, size: 18),
            label: const Text('New'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(eventSpeakersProvider(e));
          ref.invalidate(orgSpeakersProvider(orgId));
          await ref.read(eventSpeakersProvider(e).future);
        },
        child: AsyncValueView(
          value: ref.watch(eventSpeakersProvider(e)),
          onRetry: () => ref.invalidate(eventSpeakersProvider(e)),
          data: (assigned) => _Body(
            orgId: orgId,
            eventRef: e,
            assigned: assigned,
          ),
        ),
      ),
    );
  }

  Future<void> _createSpeaker(BuildContext context, WidgetRef ref) async {
    final name = TextEditingController();
    final role = TextEditingController();
    final company = TextEditingController();
    final bio = TextEditingController();

    await OrganizerFormSheet.show(
      context,
      title: 'New speaker',
      submitLabel: 'Create speaker',
      successMessage: 'Speaker added to your organisation.',
      fields: (enabled) => [
        TextField(
          controller: name,
          enabled: enabled,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Name', hintText: 'Full name'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: role,
          enabled: enabled,
          decoration: const InputDecoration(labelText: 'Title / role'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: company,
          enabled: enabled,
          decoration: const InputDecoration(labelText: 'Company'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: bio,
          enabled: enabled,
          maxLines: 3,
          decoration: const InputDecoration(labelText: 'Bio'),
        ),
      ],
      onSubmit: () => ref.read(eventContentActionsProvider).createSpeaker(orgId, {
        'name': name.text.trim(),
        'role': _orNull(role.text),
        'company': _orNull(company.text),
        'bio': _orNull(bio.text),
      }),
    );
  }
}

String? _orNull(String v) => v.trim().isEmpty ? null : v.trim();

AllyRelation _toRelation(String? status) => switch (status) {
      'accepted' => AllyRelation.accepted,
      'pending_outgoing' => AllyRelation.outgoing,
      'pending_incoming' => AllyRelation.incoming,
      _ => AllyRelation.none,
    };

class _Body extends ConsumerWidget {
  const _Body({required this.orgId, required this.eventRef, required this.assigned});

  final String orgId;
  final OrgEventRef eventRef;
  final List<SpeakerDto> assigned;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final assignedIds = assigned.map((s) => s.id).toSet();
    final myId = ref.watch(currentUserProvider)?.id;
    final assignedOtherIds = assigned
        .map((s) => s.userId)
        .whereType<String>()
        .where((id) => id != myId)
        .toSet()
        .toList();
    final assignedStatus = assignedOtherIds.isEmpty
        ? const <String, String>{}
        : ref.watch(allyStatusBatchProvider(assignedOtherIds)).valueOrNull ?? const {};

    return ListView(
      padding: const EdgeInsets.all(KSpace.lg),
      children: [
        _SectionLabel(
          'On this event',
          count: assigned.length,
        ),
        const SizedBox(height: KSpace.sm),
        if (assigned.isEmpty)
          const _InlineHint('No speakers on this event yet. Add one from your catalogue below.')
        else
          ...assigned.map(
            (s) => _SpeakerTile(
              speaker: s,
              myId: myId,
              relation: _toRelation(assignedStatus[s.userId]),
              trailingIcon: Icons.remove_circle_outline,
              trailingTooltip: 'Remove from event',
              onTrailing: () => confirmAndRun(
                context,
                title: 'Remove speaker',
                message: '${s.name} will no longer appear on this event. '
                    'They stay in your organisation catalogue.',
                confirmLabel: 'Remove',
                successMessage: 'Removed from event.',
                action: () =>
                    ref.read(eventContentActionsProvider).unassignSpeaker(eventRef, s.id),
              ),
            ),
          ),
        const SizedBox(height: KSpace.xl),
        const _SectionLabel('Organisation catalogue'),
        const SizedBox(height: KSpace.sm),
        AsyncValueView(
          value: ref.watch(orgSpeakersProvider(orgId)),
          onRetry: () => ref.invalidate(orgSpeakersProvider(orgId)),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.mic_none_rounded,
            title: 'No speakers yet',
            message: 'Create a speaker to build your lineup.',
          ),
          data: (catalogue) => _Catalogue(
            orgId: orgId,
            eventRef: eventRef,
            catalogue: catalogue,
            assignedIds: assignedIds,
            myId: myId,
          ),
        ),
      ],
    );
  }
}

class _Catalogue extends ConsumerWidget {
  const _Catalogue({
    required this.orgId,
    required this.eventRef,
    required this.catalogue,
    required this.assignedIds,
    required this.myId,
  });

  final String orgId;
  final OrgEventRef eventRef;
  final List<SpeakerDto> catalogue;
  final Set<String> assignedIds;
  final String? myId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final otherIds = catalogue
        .map((s) => s.userId)
        .whereType<String>()
        .where((id) => id != myId)
        .toSet()
        .toList();
    final status = otherIds.isEmpty
        ? const <String, String>{}
        : ref.watch(allyStatusBatchProvider(otherIds)).valueOrNull ?? const {};

    return Column(
      children: [
        for (final s in catalogue)
          _SpeakerTile(
            speaker: s,
            myId: myId,
            relation: _toRelation(status[s.userId]),
            subtitleSuffix: assignedIds.contains(s.id) ? ' · on this event' : null,
            trailingIcon:
                assignedIds.contains(s.id) ? Icons.check_circle : Icons.add_circle_outline,
            trailingTooltip:
                assignedIds.contains(s.id) ? 'Already on this event' : 'Add to this event',
            onTrailing: assignedIds.contains(s.id)
                ? null
                : () async {
                    try {
                      await ref.read(eventContentActionsProvider).assignSpeaker(eventRef, s.id);
                    } catch (_) {
                      // Surfaced by the tile's own snackbar below.
                    }
                  },
            onDelete: () => confirmAndRun(
              context,
              title: 'Delete speaker',
              message: 'Permanently delete ${s.name} from your organisation catalogue?',
              confirmLabel: 'Delete',
              successMessage: 'Speaker deleted.',
              action: () => ref.read(eventContentActionsProvider).deleteSpeaker(orgId, s.id),
            ),
          ),
      ],
    );
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text, {this.count});
  final String text;
  final int? count;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Row(
      children: [
        Text(
          text,
          style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15),
        ),
        if (count != null) ...[
          const SizedBox(width: KSpace.sm),
          Text('$count', style: TextStyle(color: c.muted)),
        ],
      ],
    );
  }
}

class _InlineHint extends StatelessWidget {
  const _InlineHint(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: KSpace.md),
      child: Text(text, style: TextStyle(color: c.muted)),
    );
  }
}

class _SpeakerTile extends StatelessWidget {
  const _SpeakerTile({
    required this.speaker,
    required this.myId,
    required this.relation,
    required this.trailingIcon,
    required this.trailingTooltip,
    required this.onTrailing,
    this.subtitleSuffix,
    this.onDelete,
  });

  final SpeakerDto speaker;
  final String? myId;
  final AllyRelation relation;
  final IconData trailingIcon;
  final String trailingTooltip;
  final VoidCallback? onTrailing;
  final String? subtitleSuffix;
  final VoidCallback? onDelete;

  @override
  Widget build(BuildContext context) {
    final roleCompany = [
      if (speaker.role != null && speaker.role!.isNotEmpty) speaker.role,
      if (speaker.company != null && speaker.company!.isNotEmpty) speaker.company,
    ].whereType<String>().join(' · ');
    final subtitle = '$roleCompany${subtitleSuffix ?? ''}';

    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.sm),
      child: KurxUserTile(
        name: speaker.name,
        username: speaker.username,
        avatarKey: speaker.avatarKey,
        subtitle: subtitle.isEmpty ? null : subtitle,
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (speaker.userId != null) ...[
              const KurxBadge(label: 'Linked', tone: KurxBadgeTone.accent),
              const SizedBox(width: KSpace.xs),
            ],
            if (speaker.userId != null && speaker.userId != myId)
              AllyConnectButton(targetUserId: speaker.userId!, initialRelation: relation),
            if (onDelete != null)
              IconButton(
                onPressed: onDelete,
                icon: const Icon(Icons.delete_outline),
                tooltip: 'Delete from catalogue',
              ),
            IconButton(
              onPressed: onTrailing,
              icon: Icon(trailingIcon),
              tooltip: trailingTooltip,
            ),
          ],
        ),
      ),
    );
  }
}
