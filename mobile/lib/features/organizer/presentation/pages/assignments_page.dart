import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_user_tile.dart';
import '../../../../common/widgets/phone_field.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../public_profile/presentation/providers/public_profile_providers.dart';
import '../../../public_profile/presentation/widgets/ally_connect_button.dart';
import '../../data/models/event_manage_dto.dart';
import '../providers/organizer_providers.dart';
import '../widgets/organizer_form_sheet.dart';

const _roles = [
  'Volunteer', 'Judge', 'Moderator', 'Registration Desk', 'Stage Manager',
  'Security', 'Photographer', 'Videographer', 'Host', 'Media Team',
  'Speaker Coordinator', 'Technical Team', 'Support Team',
];

/// Event staff/volunteer roster (D-064/D-212) — `EventAssignmentEndpoints`. Web equivalent:
/// `web/components/host/assignments-section.tsx`.
class AssignmentsPage extends ConsumerWidget {
  const AssignmentsPage({super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final param = (orgId: orgId, eventId: eventId);
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Team'),
        actions: [
          TextButton.icon(
            onPressed: () => _showAssignSheet(context, ref),
            icon: const Icon(Icons.person_add_outlined, size: 18),
            label: const Text('Assign'),
          ),
        ],
      ),
      body: AsyncValueView(
        value: ref.watch(assignmentsProvider(param)),
        onRetry: () => ref.invalidate(assignmentsProvider(param)),
        isEmpty: (list) => list.isEmpty,
        empty: const EmptyState(
          icon: Icons.badge_outlined,
          title: 'No staff assigned yet',
          message: 'Assign volunteers, judges, and other staff to this event.',
        ),
        data: (assignments) => _AssignmentList(
          assignments: assignments,
          orgId: orgId,
          eventId: eventId,
        ),
      ),
    );
  }

  Future<void> _showAssignSheet(BuildContext context, WidgetRef ref) async {
    var phoneE164 = '';
    final customRole = TextEditingController();
    final notes = TextEditingController();
    var role = _roles.first;

    await OrganizerFormSheet.show(
      context,
      title: 'Assign staff',
      submitLabel: 'Assign',
      successMessage: 'Assigned — they accept it from their own assignments.',
      fields: (enabled) => [
        // Country picker, not a bare text box with a ten-digit Indian hint: the backend reads digits
        // with no '+' in the legacy region, so an overseas staff member's own national format was
        // assigned against an unrelated Indian number. Mirrors the web assignments form.
        PhoneField(
          label: 'Phone',
          enabled: enabled,
          onChanged: (e164, _) => phoneE164 = e164,
        ),
        const SizedBox(height: KSpace.md),
        StatefulBuilder(
          builder: (_, setInner) => DropdownButtonFormField<String>(
            initialValue: role,
            decoration: const InputDecoration(labelText: 'Role'),
            items: [for (final r in _roles) DropdownMenuItem(value: r, child: Text(r))],
            onChanged: enabled ? (v) => setInner(() => role = v ?? role) : null,
          ),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: customRole,
          enabled: enabled,
          decoration: const InputDecoration(labelText: 'Custom role (optional)'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: notes,
          enabled: enabled,
          maxLines: 2,
          decoration: const InputDecoration(labelText: 'Notes (optional)'),
        ),
      ],
      onSubmit: () async {
        await ref.read(eventManageSourceProvider).assign(
              orgId,
              eventId,
              phone: phoneE164,
              role: role,
              customRole: customRole.text.trim().isEmpty ? null : customRole.text.trim(),
              notes: notes.text.trim().isEmpty ? null : notes.text.trim(),
            );
        ref.invalidate(assignmentsProvider((orgId: orgId, eventId: eventId)));
      },
    );
  }
}

class _AssignmentList extends ConsumerWidget {
  const _AssignmentList({required this.assignments, required this.orgId, required this.eventId});
  final List<AssignmentDto> assignments;
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final myId = ref.watch(currentUserProvider)?.id;
    final otherIds = assignments
        .map((a) => a.userId)
        .where((id) => id != myId)
        .toSet()
        .toList();
    final status = otherIds.isEmpty
        ? const <String, String>{}
        : ref.watch(allyStatusBatchProvider(otherIds)).valueOrNull ?? const {};

    return ListView.separated(
      padding: const EdgeInsets.all(KSpace.lg),
      itemCount: assignments.length,
      separatorBuilder: (_, _) => const SizedBox(height: KSpace.sm),
      itemBuilder: (_, i) {
        final a = assignments[i];
        return _AssignmentRow(
          assignment: a,
          myId: myId,
          relation: _toRelation(status[a.userId]),
          onRemove: () => confirmAndRun(
            context,
            title: 'Remove from team',
            message: '${a.assigneeName} will no longer be assigned to this event.',
            confirmLabel: 'Remove',
            successMessage: 'Removed.',
            action: () async {
              await ref.read(eventManageSourceProvider).removeAssignment(orgId, eventId, a.id);
              ref.invalidate(assignmentsProvider((orgId: orgId, eventId: eventId)));
            },
          ),
        );
      },
    );
  }

  AllyRelation _toRelation(String? status) => switch (status) {
        'accepted' => AllyRelation.accepted,
        'pending_outgoing' => AllyRelation.outgoing,
        'pending_incoming' => AllyRelation.incoming,
        _ => AllyRelation.none,
      };
}

class _AssignmentRow extends StatelessWidget {
  const _AssignmentRow({
    required this.assignment,
    required this.myId,
    required this.relation,
    required this.onRemove,
  });
  final AssignmentDto assignment;
  final String? myId;
  final AllyRelation relation;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final subtitle = [
      assignment.customRole?.isNotEmpty == true ? '${assignment.role} (${assignment.customRole})' : assignment.role,
      assignment.status,
    ].join(' · ');

    return KurxUserTile(
      name: assignment.assigneeName,
      username: assignment.assigneeUsername,
      avatarKey: assignment.assigneeAvatarKey,
      subtitle: subtitle,
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (assignment.userId != myId)
            AllyConnectButton(targetUserId: assignment.userId, initialRelation: relation),
          IconButton(
            onPressed: onRemove,
            icon: const Icon(Icons.remove_circle_outline),
            tooltip: 'Remove from event',
          ),
        ],
      ),
    );
  }
}
