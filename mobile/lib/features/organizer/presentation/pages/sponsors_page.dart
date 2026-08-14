import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_content_dto.dart';
import '../providers/event_content_providers.dart';
import '../widgets/organizer_form_sheet.dart';

/// Event sponsors — `SponsorEndpoints`: org-scoped catalogue CRUD plus per-event assignment.
/// Same two-list shape as speakers, because the backend models them identically.
class SponsorsPage extends ConsumerWidget {
  const SponsorsPage({super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  static const _tiers = ['title', 'gold', 'silver', 'bronze', 'partner'];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final e = (orgId: orgId, eventId: eventId);

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Sponsors'),
        actions: [
          TextButton.icon(
            onPressed: () => _createSponsor(context, ref),
            icon: const Icon(Icons.add_business_outlined, size: 18),
            label: const Text('New'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(eventSponsorsProvider(e));
          ref.invalidate(orgSponsorsProvider(orgId));
          await ref.read(eventSponsorsProvider(e).future);
        },
        child: AsyncValueView(
          value: ref.watch(eventSponsorsProvider(e)),
          onRetry: () => ref.invalidate(eventSponsorsProvider(e)),
          data: (assigned) => _Body(orgId: orgId, eventRef: e, assigned: assigned),
        ),
      ),
    );
  }

  Future<void> _createSponsor(BuildContext context, WidgetRef ref) async {
    final name = TextEditingController();
    final website = TextEditingController();
    var tier = _tiers.first;

    await OrganizerFormSheet.show(
      context,
      title: 'New sponsor',
      submitLabel: 'Create sponsor',
      successMessage: 'Sponsor added to your organisation.',
      fields: (enabled) => [
        TextField(
          controller: name,
          enabled: enabled,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Name'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: website,
          enabled: enabled,
          keyboardType: TextInputType.url,
          decoration: const InputDecoration(labelText: 'Website', hintText: 'https://'),
        ),
        const SizedBox(height: KSpace.md),
        StatefulBuilder(
          builder: (_, setInner) => DropdownButtonFormField<String>(
            initialValue: tier,
            decoration: const InputDecoration(labelText: 'Tier'),
            items: [
              for (final t in _tiers) DropdownMenuItem(value: t, child: Text(_titleCase(t))),
            ],
            onChanged: enabled ? (v) => setInner(() => tier = v ?? tier) : null,
          ),
        ),
      ],
      onSubmit: () => ref.read(eventContentActionsProvider).createSponsor(orgId, {
        'name': name.text.trim(),
        'website': website.text.trim().isEmpty ? null : website.text.trim(),
        'tier': tier,
      }),
    );
  }
}

String _titleCase(String s) => s.isEmpty ? s : s[0].toUpperCase() + s.substring(1);

class _Body extends ConsumerWidget {
  const _Body({required this.orgId, required this.eventRef, required this.assigned});

  final String orgId;
  final OrgEventRef eventRef;
  final List<SponsorDto> assigned;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final assignedIds = assigned.map((s) => s.id).toSet();

    return ListView(
      padding: const EdgeInsets.all(KSpace.lg),
      children: [
        Text('On this event  ${assigned.length}',
            style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15)),
        const SizedBox(height: KSpace.sm),
        if (assigned.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: KSpace.md),
            child: Text('No sponsors on this event yet. Add one from your catalogue below.',
                style: TextStyle(color: c.muted)),
          )
        else
          ...assigned.map(
            (s) => _SponsorTile(
              sponsor: s,
              trailingIcon: Icons.remove_circle_outline,
              trailingTooltip: 'Remove from event',
              onTrailing: () => confirmAndRun(
                context,
                title: 'Remove sponsor',
                message: '${s.name} will no longer appear on this event. '
                    'They stay in your organisation catalogue.',
                confirmLabel: 'Remove',
                successMessage: 'Removed from event.',
                action: () =>
                    ref.read(eventContentActionsProvider).unassignSponsor(eventRef, s.id),
              ),
            ),
          ),
        const SizedBox(height: KSpace.xl),
        Text('Organisation catalogue',
            style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15)),
        const SizedBox(height: KSpace.sm),
        AsyncValueView(
          value: ref.watch(orgSponsorsProvider(orgId)),
          onRetry: () => ref.invalidate(orgSponsorsProvider(orgId)),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.handshake_outlined,
            title: 'No sponsors yet',
            message: 'Create a sponsor to feature them on your events.',
          ),
          data: (catalogue) => Column(
            children: [
              for (final s in catalogue)
                _SponsorTile(
                  sponsor: s,
                  onThisEvent: assignedIds.contains(s.id),
                  trailingIcon:
                      assignedIds.contains(s.id) ? Icons.check_circle : Icons.add_circle_outline,
                  trailingTooltip:
                      assignedIds.contains(s.id) ? 'Already on this event' : 'Add to this event',
                  onTrailing: assignedIds.contains(s.id)
                      ? null
                      : () => confirmAndRun(
                            context,
                            title: 'Add sponsor',
                            message: 'Feature ${s.name} on this event?',
                            confirmLabel: 'Add',
                            successMessage: 'Added to event.',
                            action: () => ref
                                .read(eventContentActionsProvider)
                                .assignSponsor(eventRef, s.id),
                          ),
                  onDelete: () => confirmAndRun(
                    context,
                    title: 'Delete sponsor',
                    message: 'Permanently delete ${s.name} from your organisation catalogue?',
                    confirmLabel: 'Delete',
                    successMessage: 'Sponsor deleted.',
                    action: () =>
                        ref.read(eventContentActionsProvider).deleteSponsor(orgId, s.id),
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }
}

class _SponsorTile extends StatelessWidget {
  const _SponsorTile({
    required this.sponsor,
    required this.trailingIcon,
    required this.trailingTooltip,
    required this.onTrailing,
    this.onThisEvent = false,
    this.onDelete,
  });

  final SponsorDto sponsor;
  final IconData trailingIcon;
  final String trailingTooltip;
  final VoidCallback? onTrailing;
  final bool onThisEvent;
  final VoidCallback? onDelete;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final bits = [
      if (sponsor.tier != null && sponsor.tier!.isNotEmpty) _titleCase(sponsor.tier!),
      if (sponsor.website != null && sponsor.website!.isNotEmpty) sponsor.website,
      if (onThisEvent) 'on this event',
    ].whereType<String>().join(' · ');

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: CircleAvatar(
        backgroundColor: c.accent.withValues(alpha: 0.15),
        child: Icon(Icons.business_outlined, color: c.accent, size: 20),
      ),
      title: Text(sponsor.name, style: TextStyle(color: c.text)),
      subtitle: bits.isEmpty ? null : Text(bits, style: TextStyle(color: c.muted)),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (onDelete != null)
            IconButton(
              onPressed: onDelete,
              icon: const Icon(Icons.delete_outline),
              tooltip: 'Delete from catalogue',
            ),
          IconButton(onPressed: onTrailing, icon: Icon(trailingIcon), tooltip: trailingTooltip),
        ],
      ),
    );
  }
}
