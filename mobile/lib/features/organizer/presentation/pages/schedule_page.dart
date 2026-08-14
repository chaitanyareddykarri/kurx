import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_content_dto.dart';
import '../providers/event_content_providers.dart';
import '../widgets/organizer_form_sheet.dart';

/// Event schedule — `ScheduleEndpoints` sessions CRUD, grouped by day.
///
/// Sessions carry real start/end instants, so the list is grouped by calendar day and ordered by
/// start time rather than by the server's `sort` column: `sort` is a manual ordering hint, and
/// showing a 3pm talk above a 10am one because someone dragged it would be wrong.
class SchedulePage extends ConsumerWidget {
  const SchedulePage({super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  static const _kinds = ['session', 'keynote', 'workshop', 'break'];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final e = (orgId: orgId, eventId: eventId);

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Schedule'),
        actions: [
          TextButton.icon(
            onPressed: () => _editSession(context, ref, e),
            icon: const Icon(Icons.add_rounded, size: 18),
            label: const Text('Add'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(eventSessionsProvider(e));
          await ref.read(eventSessionsProvider(e).future);
        },
        child: AsyncValueView(
          value: ref.watch(eventSessionsProvider(e)),
          onRetry: () => ref.invalidate(eventSessionsProvider(e)),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.schedule_outlined,
            title: 'No sessions yet',
            message: 'Build your agenda by adding talks, workshops and breaks.',
          ),
          data: (sessions) => _Agenda(
            sessions: sessions,
            onEdit: (s) => _editSession(context, ref, e, existing: s),
            onDelete: (s) => confirmAndRun(
              context,
              title: 'Delete session',
              message: 'Permanently delete "${s.title}" from the agenda?',
              confirmLabel: 'Delete',
              successMessage: 'Session deleted.',
              action: () => ref.read(eventContentActionsProvider).deleteSession(e, s.id),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _editSession(
    BuildContext context,
    WidgetRef ref,
    OrgEventRef e, {
    SessionDto? existing,
  }) async {
    final title = TextEditingController(text: existing?.title ?? '');
    final description = TextEditingController(text: existing?.description ?? '');
    var kind = existing?.kind ?? _kinds.first;
    final now = DateTime.now();
    var startsAt = existing?.startsAt ?? DateTime(now.year, now.month, now.day, 10);
    var endsAt = existing?.endsAt ?? startsAt.add(const Duration(hours: 1));

    final fmt = DateFormat('EEE d MMM, h:mm a');

    await OrganizerFormSheet.show(
      context,
      title: existing == null ? 'New session' : 'Edit session',
      submitLabel: existing == null ? 'Add to agenda' : 'Save changes',
      successMessage: existing == null ? 'Session added.' : 'Session updated.',
      fields: (enabled) => [
        TextField(
          controller: title,
          enabled: enabled,
          textCapitalization: TextCapitalization.sentences,
          decoration: const InputDecoration(labelText: 'Title'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: description,
          enabled: enabled,
          maxLines: 2,
          decoration: const InputDecoration(labelText: 'Description'),
        ),
        const SizedBox(height: KSpace.md),
        StatefulBuilder(
          builder: (innerCtx, setInner) => Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              DropdownButtonFormField<String>(
                initialValue: kind,
                decoration: const InputDecoration(labelText: 'Kind'),
                items: [
                  for (final k in _kinds)
                    DropdownMenuItem(
                      value: k,
                      child: Text(k[0].toUpperCase() + k.substring(1)),
                    ),
                ],
                onChanged: enabled ? (v) => setInner(() => kind = v ?? kind) : null,
              ),
              const SizedBox(height: KSpace.sm),
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.play_arrow_rounded),
                title: const Text('Starts'),
                subtitle: Text(fmt.format(startsAt)),
                onTap: !enabled
                    ? null
                    : () async {
                        final picked = await _pickDateTime(innerCtx, startsAt);
                        if (picked != null) {
                          setInner(() {
                            startsAt = picked;
                            if (!endsAt.isAfter(startsAt)) {
                              endsAt = startsAt.add(const Duration(hours: 1));
                            }
                          });
                        }
                      },
              ),
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.stop_rounded),
                title: const Text('Ends'),
                subtitle: Text(fmt.format(endsAt)),
                onTap: !enabled
                    ? null
                    : () async {
                        final picked = await _pickDateTime(innerCtx, endsAt);
                        if (picked != null) setInner(() => endsAt = picked);
                      },
              ),
            ],
          ),
        ),
      ],
      onSubmit: () {
        final body = <String, dynamic>{
          'title': title.text.trim(),
          'description': description.text.trim().isEmpty ? null : description.text.trim(),
          'kind': kind,
          'startsAt': startsAt.toUtc().toIso8601String(),
          'endsAt': endsAt.toUtc().toIso8601String(),
        };
        final actions = ref.read(eventContentActionsProvider);
        return existing == null
            ? actions.createSession(e, body)
            : actions.updateSession(e, existing.id, body);
      },
    );
  }

  static Future<DateTime?> _pickDateTime(BuildContext context, DateTime initial) async {
    final date = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(initial.year - 1),
      lastDate: DateTime(initial.year + 3),
    );
    if (date == null || !context.mounted) return null;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initial),
    );
    if (time == null) return null;
    return DateTime(date.year, date.month, date.day, time.hour, time.minute);
  }
}

class _Agenda extends StatelessWidget {
  const _Agenda({required this.sessions, required this.onEdit, required this.onDelete});

  final List<SessionDto> sessions;
  final void Function(SessionDto) onEdit;
  final void Function(SessionDto) onDelete;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final ordered = [...sessions]..sort((a, b) => a.startsAt.compareTo(b.startsAt));

    final byDay = <DateTime, List<SessionDto>>{};
    for (final s in ordered) {
      final local = s.startsAt.toLocal();
      byDay.putIfAbsent(DateTime(local.year, local.month, local.day), () => []).add(s);
    }
    final days = byDay.keys.toList()..sort();
    final dayFmt = DateFormat('EEEE, d MMMM');

    return ListView(
      padding: const EdgeInsets.all(KSpace.lg),
      children: [
        for (final day in days) ...[
          Padding(
            padding: const EdgeInsets.only(top: KSpace.sm, bottom: KSpace.sm),
            child: Text(
              dayFmt.format(day),
              style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15),
            ),
          ),
          for (final s in byDay[day]!)
            _SessionTile(session: s, onEdit: () => onEdit(s), onDelete: () => onDelete(s)),
          const SizedBox(height: KSpace.md),
        ],
      ],
    );
  }
}

class _SessionTile extends StatelessWidget {
  const _SessionTile({required this.session, required this.onEdit, required this.onDelete});

  final SessionDto session;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final time = DateFormat('h:mm a');
    final isBreak = session.kind == 'break';

    return Container(
      margin: const EdgeInsets.only(bottom: KSpace.sm),
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.md),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 72,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(time.format(session.startsAt.toLocal()),
                    style: TextStyle(color: c.text, fontWeight: FontWeight.w600, fontSize: 13)),
                Text(time.format(session.endsAt.toLocal()),
                    style: TextStyle(color: c.muted, fontSize: 12)),
              ],
            ),
          ),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  session.title,
                  style: TextStyle(
                    color: isBreak ? c.muted : c.text,
                    fontWeight: FontWeight.w600,
                    fontStyle: isBreak ? FontStyle.italic : FontStyle.normal,
                  ),
                ),
                if (session.description != null && session.description!.isNotEmpty) ...[
                  const SizedBox(height: KSpace.xs),
                  Text(session.description!, style: TextStyle(color: c.muted, fontSize: 13)),
                ],
              ],
            ),
          ),
          IconButton(
            onPressed: onEdit,
            icon: const Icon(Icons.edit_outlined, size: 20),
            tooltip: 'Edit session',
          ),
          IconButton(
            onPressed: onDelete,
            icon: const Icon(Icons.delete_outline, size: 20),
            tooltip: 'Delete session',
          ),
        ],
      ),
    );
  }
}
