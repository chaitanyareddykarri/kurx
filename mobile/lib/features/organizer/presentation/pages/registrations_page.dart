import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_card.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_manage_dto.dart';
import '../providers/organizer_providers.dart';

/// Registrations for one event, filterable by state.
///
/// Deliberately separate from Attendees: an attendee is an *issued admission*, a registration is the
/// act that may still be pending, waitlisted or cancelled. Collapsing the two would hide exactly the
/// rows an organiser running manual approval needs to see.
///
/// Read-only for now — the backend exposes the list but no approve/reject transition on a
/// registration, so a button here would have nothing to call.
class RegistrationsPage extends ConsumerStatefulWidget {
  const RegistrationsPage({super.key, required this.orgId, required this.eventId});

  final String orgId;
  final String eventId;

  @override
  ConsumerState<RegistrationsPage> createState() => _RegistrationsPageState();
}

class _RegistrationsPageState extends ConsumerState<RegistrationsPage> {
  static const _filters = ['all', 'pending', 'confirmed', 'waitlisted', 'cancelled'];
  String _filter = 'all';

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final key = (orgId: widget.orgId, eventId: widget.eventId);
    final registrations = ref.watch(registrationsProvider(key));

    return Scaffold(
      appBar: AppBar(title: const Text('Registrations')),
      body: Column(
        children: [
          SizedBox(
            height: 56,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.sm),
              children: [
                for (final f in _filters)
                  Padding(
                    padding: const EdgeInsets.only(right: KSpace.sm),
                    child: ChoiceChip(
                      label: Text(f[0].toUpperCase() + f.substring(1)),
                      selected: _filter == f,
                      onSelected: (_) => setState(() => _filter = f),
                    ),
                  ),
              ],
            ),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: () async => ref.invalidate(registrationsProvider(key)),
              child: AsyncValueView(
                value: registrations,
                onRetry: () => ref.invalidate(registrationsProvider(key)),
                data: (all) {
                  final rows = _filter == 'all'
                      ? all
                      : all.where((r) => r.state.toLowerCase() == _filter).toList();
                  if (rows.isEmpty) {
                    return EmptyState(
                      icon: Icons.how_to_reg_outlined,
                      title: _filter == 'all' ? 'No registrations yet' : 'Nothing $_filter',
                      message: 'Registrations for this event appear here.',
                    );
                  }
                  return ListView.separated(
                    padding: const EdgeInsets.fromLTRB(KSpace.lg, 0, KSpace.lg, KSpace.xl),
                    itemCount: rows.length,
                    separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
                    itemBuilder: (context, i) => _RegistrationCard(registration: rows[i], colors: c),
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _RegistrationCard extends StatelessWidget {
  const _RegistrationCard({required this.registration, required this.colors});

  final RegistrationDto registration;
  final KurxColors colors;

  @override
  Widget build(BuildContext context) {
    final (icon, colour) = switch (registration.state.toLowerCase()) {
      'confirmed' => (Icons.check_circle_outline_rounded, colors.success),
      'cancelled' => (Icons.cancel_outlined, colors.danger),
      'waitlisted' => (Icons.hourglass_empty_rounded, colors.accent),
      _ => (Icons.schedule_rounded, colors.muted),
    };

    return KurxCard(
      child: Row(
        children: [
          Icon(icon, color: colour),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  registration.subjectType == 'Team' ? 'Team registration' : 'Individual',
                  style: Theme.of(context)
                      .textTheme
                      .bodyMedium
                      ?.copyWith(fontWeight: FontWeight.w600, color: colors.text),
                ),
                const SizedBox(height: KSpace.xs),
                Text(
                  [
                    registration.state,
                    '${registration.admissionCount} '
                        '${registration.admissionCount == 1 ? 'admission' : 'admissions'}',
                    if (registration.createdAt != null)
                      '${registration.createdAt!.day}/${registration.createdAt!.month}/${registration.createdAt!.year}',
                  ].join(' · '),
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
