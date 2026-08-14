import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../../../events/data/models/ticket_type_dto.dart';
import '../providers/event_content_providers.dart';
import '../widgets/organizer_form_sheet.dart';

/// Ticket types — `TicketTypeEndpoints` org-scoped CRUD.
///
/// Prices are entered in rupees and sent as **paise** (D-004: money is `long` paise end to end).
/// The backend refuses a delete once `sold > 0` and refuses a quantity below `sold`; both come
/// back as error codes, which the sheet surfaces verbatim rather than pre-guessing client-side.
class TicketTypesPage extends ConsumerWidget {
  const TicketTypesPage({super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final e = (orgId: orgId, eventId: eventId);

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Ticket Types'),
        actions: [
          TextButton.icon(
            onPressed: () => _editTicketType(context, ref, e),
            icon: const Icon(Icons.add_rounded, size: 18),
            label: const Text('Add'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(orgTicketTypesProvider(e));
          await ref.read(orgTicketTypesProvider(e).future);
        },
        child: AsyncValueView(
          value: ref.watch(orgTicketTypesProvider(e)),
          onRetry: () => ref.invalidate(orgTicketTypesProvider(e)),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.local_activity_outlined,
            title: 'No ticket types yet',
            message: 'Add at least one ticket type before publishing.',
          ),
          data: (types) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: types.length,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
            itemBuilder: (_, i) => _TicketTypeCard(
              type: types[i],
              onEdit: () => _editTicketType(context, ref, e, existing: types[i]),
              onDelete: () => confirmAndRun(
                context,
                title: 'Delete ticket type',
                message: types[i].sold > 0
                    ? '${types[i].name} has ${types[i].sold} sold. '
                        'The server will refuse this — sold tickets cannot be orphaned.'
                    : 'Permanently delete ${types[i].name}?',
                confirmLabel: 'Delete',
                successMessage: 'Ticket type deleted.',
                action: () => ref
                    .read(eventContentActionsProvider)
                    .deleteTicketType(e, types[i].id),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _editTicketType(
    BuildContext context,
    WidgetRef ref,
    OrgEventRef e, {
    TicketTypeDto? existing,
  }) async {
    final name = TextEditingController(text: existing?.name ?? '');
    final price =
        TextEditingController(text: existing == null ? '' : (existing.pricePaise / 100).toString());
    final quantity = TextEditingController(text: existing?.quantity.toString() ?? '');
    final perUser = TextEditingController(text: existing?.perUserLimit?.toString() ?? '');

    await OrganizerFormSheet.show(
      context,
      title: existing == null ? 'New ticket type' : 'Edit ${existing.name}',
      submitLabel: existing == null ? 'Create' : 'Save changes',
      successMessage: existing == null ? 'Ticket type created.' : 'Ticket type updated.',
      fields: (enabled) => [
        TextField(
          controller: name,
          enabled: enabled,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Name', hintText: 'General admission'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: price,
          enabled: enabled,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          decoration: const InputDecoration(
            labelText: 'Price (₹)',
            hintText: '0 for a free ticket',
            prefixText: '₹ ',
          ),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: quantity,
          enabled: enabled,
          keyboardType: TextInputType.number,
          decoration: InputDecoration(
            labelText: 'Quantity',
            helperText: existing != null && existing.sold > 0
                ? 'Cannot go below ${existing.sold} already sold'
                : null,
          ),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: perUser,
          enabled: enabled,
          keyboardType: TextInputType.number,
          decoration: const InputDecoration(
            labelText: 'Per-user limit',
            hintText: 'Leave blank for no limit',
          ),
        ),
      ],
      onSubmit: () {
        final rupees = double.tryParse(price.text.trim()) ?? 0;
        final body = <String, dynamic>{
          'name': name.text.trim(),
          'pricePaise': (rupees * 100).round(),
          'quantity': int.tryParse(quantity.text.trim()) ?? 0,
          'perUserLimit': ?int.tryParse(perUser.text.trim()),
        };
        final actions = ref.read(eventContentActionsProvider);
        return existing == null
            ? actions.createTicketType(e, body)
            : actions.updateTicketType(e, existing.id, body);
      },
    );
  }
}

class _TicketTypeCard extends StatelessWidget {
  const _TicketTypeCard({required this.type, required this.onEdit, required this.onDelete});

  final TicketTypeDto type;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // Central formatter (D-257); ticket types do not carry a currency yet, so INR default applies.
    final soldOut = type.quantity > 0 && type.available <= 0;

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
                child: Text(
                  type.name,
                  style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 16),
                ),
              ),
              Text(
                type.pricePaise == 0 ? 'Free' : formatPaise(type.pricePaise),
                style: TextStyle(color: c.accent, fontWeight: FontWeight.w700),
              ),
            ],
          ),
          const SizedBox(height: KSpace.sm),
          Wrap(
            spacing: KSpace.md,
            runSpacing: KSpace.xs,
            children: [
              _Stat(label: 'Sold', value: '${type.sold}'),
              _Stat(label: 'Available', value: '${type.available}'),
              _Stat(label: 'Capacity', value: type.quantity == 0 ? '∞' : '${type.quantity}'),
              if (type.perUserLimit != null) _Stat(label: 'Per user', value: '${type.perUserLimit}'),
            ],
          ),
          if (soldOut) ...[
            const SizedBox(height: KSpace.sm),
            Text('Sold out', style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ],
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

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Text('$label: $value', style: TextStyle(color: c.muted, fontSize: 13));
  }
}
