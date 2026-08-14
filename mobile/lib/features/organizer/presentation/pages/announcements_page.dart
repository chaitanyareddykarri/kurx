import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_manage_dto.dart';
import '../providers/organizer_providers.dart';
import '../widgets/organizer_form_sheet.dart';

class AnnouncementsPage extends ConsumerWidget {
  const AnnouncementsPage(
      {super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final param = (orgId: orgId, eventId: eventId);
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Announcements'),
        actions: [
          TextButton.icon(
            onPressed: () => _showCreateSheet(context, ref),
            icon: const Icon(Icons.add_rounded, size: 18),
            label: const Text('New'),
          ),
        ],
      ),
      body: AsyncValueView(
        value: ref.watch(announcementsProvider(param)),
        onRetry: () => ref.invalidate(announcementsProvider(param)),
        isEmpty: (list) => list.isEmpty,
        empty: const EmptyState(
          icon: Icons.campaign_outlined,
          title: 'No announcements yet',
          message: 'Send announcements to keep attendees informed.',
        ),
        data: (items) => ListView.separated(
          padding: const EdgeInsets.all(KSpace.lg),
          itemCount: items.length,
          separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
          itemBuilder: (_, i) => _AnnouncementCard(item: items[i]),
        ),
      ),
    );
  }

  Future<void> _showCreateSheet(BuildContext context, WidgetRef ref) async {
    final title = TextEditingController();
    final body = TextEditingController();
    var audience = 'AllRegistrants';

    await OrganizerFormSheet.show(
      context,
      title: 'New announcement',
      submitLabel: 'Send now',
      successMessage: 'Announcement sent.',
      fields: (enabled) => [
        TextField(
          controller: title,
          enabled: enabled,
          textCapitalization: TextCapitalization.sentences,
          decoration: const InputDecoration(labelText: 'Title'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: body,
          enabled: enabled,
          maxLines: 4,
          textCapitalization: TextCapitalization.sentences,
          decoration: const InputDecoration(labelText: 'Message'),
        ),
        const SizedBox(height: KSpace.md),
        StatefulBuilder(
          builder: (_, setInner) => DropdownButtonFormField<String>(
            initialValue: audience,
            decoration: const InputDecoration(labelText: 'Audience'),
            items: const [
              DropdownMenuItem(value: 'AllRegistrants', child: Text('Everyone registered')),
              DropdownMenuItem(value: 'CheckedIn', child: Text('Checked in')),
              DropdownMenuItem(value: 'NotCheckedIn', child: Text('Not checked in')),
            ],
            onChanged: enabled ? (v) => setInner(() => audience = v ?? audience) : null,
          ),
        ),
      ],
      // Scheduling is deliberately not offered: `scheduledAt` exists on the API, but there is no
      // list/cancel surface for a scheduled send yet, so a queued announcement would be
      // unreachable and unrecallable. Send-now is the only action that can be undone by not doing.
      onSubmit: () async {
        await ref.read(eventManageSourceProvider).createAnnouncement(
              eventId,
              title: title.text.trim(),
              body: body.text.trim(),
              audience: audience,
              channels: const ['push'],
            );
        ref.invalidate(announcementsProvider((orgId: orgId, eventId: eventId)));
      },
    );
  }
}

class _AnnouncementCard extends StatelessWidget {
  const _AnnouncementCard({required this.item});
  final AnnouncementDto item;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.xl),
        boxShadow: kCardShadow(context),
      ),
      child: Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    item.title,
                    style: TextStyle(
                      color: c.text,
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                    ),
                  ),
                ),
                // An overflow menu that opened nothing: empty `onPressed`, no tooltip, and
                // `constraints: BoxConstraints()` stripping the 48dp minimum so it was a tiny
                // target as well. Removed rather than invented — what belongs in an announcement's
                // menu (edit? cancel? resend?) is a product question, and cancel already exists as
                // its own control on the web console.
              ],
            ),
            if (item.body != null)
              Text(
                item.body!,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(color: c.muted, fontSize: 13, height: 1.4),
              ),
            const SizedBox(height: KSpace.md),
            Row(
              children: [
                Icon(Icons.visibility_outlined,
                    color: c.muted, size: 14),
                const SizedBox(width: 4),
                Text('${item.openCount}',
                    style: TextStyle(color: c.muted, fontSize: 12)),
                const SizedBox(width: KSpace.md),
                Icon(Icons.touch_app_outlined,
                    color: c.muted, size: 14),
                const SizedBox(width: 4),
                Text('${item.clickCount}',
                    style: TextStyle(color: c.muted, fontSize: 12)),
                const Spacer(),
                if (item.sentAt != null)
                  Text(
                    DateFormat('dd MMM, hh:mm a').format(item.sentAt!),
                    style: TextStyle(color: c.muted, fontSize: 11.5),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
