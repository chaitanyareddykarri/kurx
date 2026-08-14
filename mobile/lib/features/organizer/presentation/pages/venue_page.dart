import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_content_dto.dart';
import '../providers/event_content_providers.dart';
import '../widgets/organizer_form_sheet.dart';

/// Venues — `VenueEndpoints`, an **org-scoped catalogue** (all 7 routes are
/// `/v1/orgs/{orgId}/venues…`). A venue is created once and reused across the organisation's
/// events; there is no per-event venue write here, so this screen manages the catalogue and the
/// event picks from it when the event itself is edited.
class VenuePage extends ConsumerWidget {
  const VenuePage({super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Venues'),
        actions: [
          TextButton.icon(
            onPressed: () => _editVenue(context, ref),
            icon: const Icon(Icons.add_location_alt_outlined, size: 18),
            label: const Text('Add'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(orgVenuesProvider(orgId));
          await ref.read(orgVenuesProvider(orgId).future);
        },
        child: AsyncValueView(
          value: ref.watch(orgVenuesProvider(orgId)),
          onRetry: () => ref.invalidate(orgVenuesProvider(orgId)),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.place_outlined,
            title: 'No venues yet',
            message: 'Add a venue so your events have somewhere to happen.',
          ),
          data: (venues) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: venues.length,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
            itemBuilder: (_, i) => _VenueCard(
              venue: venues[i],
              onEdit: () => _editVenue(context, ref, existing: venues[i]),
              onDelete: () => confirmAndRun(
                context,
                title: 'Delete venue',
                message: 'Permanently delete ${venues[i].name} from your organisation?',
                confirmLabel: 'Delete',
                successMessage: 'Venue deleted.',
                action: () =>
                    ref.read(eventContentActionsProvider).deleteVenue(orgId, venues[i].id),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _editVenue(BuildContext context, WidgetRef ref, {VenueDto? existing}) async {
    final name = TextEditingController(text: existing?.name ?? '');
    final address = TextEditingController(text: existing?.address ?? '');
    final city = TextEditingController(text: existing?.city ?? '');
    final capacity = TextEditingController(text: existing?.capacity?.toString() ?? '');
    var hasParking = existing?.hasParking ?? false;
    var isAccessible = existing?.isAccessible ?? false;

    await OrganizerFormSheet.show(
      context,
      title: existing == null ? 'New venue' : 'Edit ${existing.name}',
      submitLabel: existing == null ? 'Create venue' : 'Save changes',
      successMessage: existing == null ? 'Venue created.' : 'Venue updated.',
      fields: (enabled) => [
        TextField(
          controller: name,
          enabled: enabled,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Name'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: address,
          enabled: enabled,
          maxLines: 2,
          decoration: const InputDecoration(labelText: 'Address'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: city,
          enabled: enabled,
          decoration: const InputDecoration(labelText: 'City'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: capacity,
          enabled: enabled,
          keyboardType: TextInputType.number,
          decoration: const InputDecoration(labelText: 'Capacity'),
        ),
        StatefulBuilder(
          builder: (_, setInner) => Column(
            children: [
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Parking available'),
                value: hasParking,
                onChanged: enabled ? (v) => setInner(() => hasParking = v) : null,
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Step-free access'),
                value: isAccessible,
                onChanged: enabled ? (v) => setInner(() => isAccessible = v) : null,
              ),
            ],
          ),
        ),
      ],
      onSubmit: () {
        final body = <String, dynamic>{
          'name': name.text.trim(),
          'address': address.text.trim().isEmpty ? null : address.text.trim(),
          'city': city.text.trim().isEmpty ? null : city.text.trim(),
          'capacity': ?int.tryParse(capacity.text.trim()),
          'hasParking': hasParking,
          'isAccessible': isAccessible,
        };
        final actions = ref.read(eventContentActionsProvider);
        return existing == null
            ? actions.createVenue(orgId, body)
            : actions.updateVenue(orgId, existing.id, body);
      },
    );
  }
}

class _VenueCard extends StatelessWidget {
  const _VenueCard({required this.venue, required this.onEdit, required this.onDelete});

  final VenueDto venue;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final where =
        [venue.address, venue.city].whereType<String>().where((s) => s.isNotEmpty).join(', ');

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(venue.name,
              style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 16)),
          if (where.isNotEmpty) ...[
            const SizedBox(height: KSpace.xs),
            Text(where, style: TextStyle(color: c.muted)),
          ],
          const SizedBox(height: KSpace.sm),
          Wrap(
            spacing: KSpace.sm,
            runSpacing: KSpace.xs,
            children: [
              if (venue.capacity != null)
                _Tag(icon: Icons.groups_outlined, label: 'Seats ${venue.capacity}'),
              if (venue.hasParking) const _Tag(icon: Icons.local_parking_outlined, label: 'Parking'),
              if (venue.isAccessible)
                const _Tag(icon: Icons.accessible_outlined, label: 'Step-free'),
            ],
          ),
          const SizedBox(height: KSpace.sm),
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              TextButton.icon(
                onPressed: onEdit,
                icon: const Icon(Icons.edit_outlined, size: 18),
                label: const Text('Edit'),
              ),
              TextButton.icon(
                onPressed: onDelete,
                icon: const Icon(Icons.delete_outline, size: 18),
                label: const Text('Delete'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag({required this.icon, required this.label});
  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 15, color: c.muted),
        const SizedBox(width: KSpace.xs),
        Text(label, style: TextStyle(color: c.muted, fontSize: 13)),
      ],
    );
  }
}
