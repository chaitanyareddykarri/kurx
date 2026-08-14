import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_user_tile.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../public_profile/presentation/providers/public_profile_providers.dart';
import '../../../public_profile/presentation/widgets/ally_connect_button.dart';
import '../../data/models/event_manage_dto.dart';
import '../providers/organizer_providers.dart';

class AttendeesPage extends ConsumerStatefulWidget {
  const AttendeesPage(
      {super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  ConsumerState<AttendeesPage> createState() => _AttendeesPageState();
}

class _AttendeesPageState extends ConsumerState<AttendeesPage> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final param = (orgId: widget.orgId, eventId: widget.eventId);
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Attendees'),
        // An "Export CSV" button with an empty `onPressed`. Named, so it announced itself
        // perfectly, and did nothing — which is worse than being unnamed. Removed rather than
        // built: there is no export endpoint in the Flutter data layer, and writing a file to
        // device storage is a feature with its own permissions story, not a redesign.
      ),
      body: AsyncValueView(
        value: ref.watch(attendeesProvider(param)),
        onRetry: () => ref.invalidate(attendeesProvider(param)),
        data: (attendees) {
          final checkedIn =
              attendees.where((a) => a.state == 'CheckedIn').length;
          final filtered = attendees
              .where((a) =>
                  _query.isEmpty ||
                  a.buyerName.toLowerCase().contains(_query.toLowerCase()) ||
                  a.buyerPhone.contains(_query))
              .toList();

          return Column(
            children: [
              // Stats row
              Padding(
                padding: const EdgeInsets.all(KSpace.lg),
                child: Row(
                  children: [
                    _Pill('Total', '${attendees.length}', c.muted),
                    const SizedBox(width: KSpace.sm),
                    _Pill('Checked in', '$checkedIn', c.success),
                    const SizedBox(width: KSpace.sm),
                    _Pill('No-show',
                        '${attendees.length - checkedIn}', c.danger),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(
                    horizontal: KSpace.lg),
                child: TextField(
                  onChanged: (v) => setState(() => _query = v),
                  decoration: const InputDecoration(
                    hintText: 'Search attendees…',
                    prefixIcon: Icon(Icons.search_rounded),
                  ),
                ),
              ),
              const SizedBox(height: KSpace.md),
              if (filtered.isEmpty)
                const Expanded(
                  child: EmptyState(
                    icon: Icons.people_outline_rounded,
                    title: 'No attendees yet',
                    message: 'Attendees will appear here once tickets are sold.',
                  ),
                )
              else
                Expanded(
                  child: _AttendeeList(attendees: filtered),
                ),
            ],
          );
        },
      ),
    );
  }
}

class _AttendeeList extends ConsumerWidget {
  const _AttendeeList({required this.attendees});
  final List<AttendeeDto> attendees;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final myId = ref.watch(currentUserProvider)?.id;
    final otherIds = attendees
        .map((a) => a.buyerUserId)
        .whereType<String>()
        .where((id) => id != myId)
        .toSet()
        .toList();
    final status = otherIds.isEmpty
        ? const <String, String>{}
        : ref.watch(allyStatusBatchProvider(otherIds)).valueOrNull ?? const {};

    return ListView.separated(
      itemCount: attendees.length,
      separatorBuilder: (_, _) => Divider(indent: 16, color: c.border),
      itemBuilder: (_, i) {
        final a = attendees[i];
        return _AttendeeRow(a: a, myId: myId, relation: _toRelation(status[a.buyerUserId]));
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

class _Pill extends StatelessWidget {
  const _Pill(this.label, this.value, this.color);
  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
          horizontal: KSpace.md, vertical: KSpace.xs),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(KRadius.pill),
      ),
      child: Text(
        '$label: $value',
        style: TextStyle(
            color: color, fontSize: 12, fontWeight: FontWeight.w700),
      ),
    );
  }
}

class _AttendeeRow extends StatelessWidget {
  const _AttendeeRow({required this.a, required this.myId, required this.relation});
  final AttendeeDto a;
  final String? myId;
  final AllyRelation relation;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final checkedIn = a.state == 'CheckedIn';
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.xs),
      child: KurxUserTile(
        name: a.buyerName.isNotEmpty ? a.buyerName : a.buyerPhone,
        username: a.buyerUsername,
        avatarKey: a.buyerAvatarKey,
        subtitle: a.ticketTypeName,
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (a.buyerUserId != null && a.buyerUserId != myId)
              AllyConnectButton(targetUserId: a.buyerUserId!, initialRelation: relation),
            const SizedBox(width: KSpace.sm),
            checkedIn
                ? Icon(Icons.check_circle_rounded, color: c.success, size: 22)
                : Icon(Icons.radio_button_unchecked_rounded, color: c.muted, size: 22),
          ],
        ),
      ),
    );
  }
}
