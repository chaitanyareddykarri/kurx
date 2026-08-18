import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/event_status.dart';
import '../../../../core/utils/money.dart';
import '../providers/organizer_providers.dart';

class EventManageDetailPage extends ConsumerWidget {
  const EventManageDetailPage(
      {super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final param = eventId;
    return Scaffold(
      backgroundColor: c.background,
      body: AsyncValueView(
        value: ref.watch(eventManageDetailProvider(param)),
        onRetry: () => ref.invalidate(eventManageDetailProvider(param)),
        data: (event) => DefaultTabController(
          length: 15,
          child: NestedScrollView(
            headerSliverBuilder: (_, _) => [
              SliverAppBar(
                pinned: true,
                expandedHeight: 160,
                flexibleSpace: FlexibleSpaceBar(
                  title: Text(
                    event.title,
                    style: const TextStyle(fontSize: 14),
                  ),
                  // `banner_key` is a storage key, not a URL, and there is no event-banner presign
                  // for this screen yet — so there is nothing to render but the placeholder. The
                  // previous `cover_url` was never a field any projection sent.
                  background: Container(color: c.elevated),
                ),
                actions: [
                  Container(
                    margin: const EdgeInsets.only(right: KSpace.md),
                    padding: const EdgeInsets.symmetric(
                        horizontal: KSpace.md, vertical: 2),
                    decoration: BoxDecoration(
                      color: _statusColor(event.status, c)
                          .withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(KRadius.pill),
                    ),
                    child: Text(
                      eventStatusLabel(event.status),
                      style: TextStyle(
                        color: _statusColor(event.status, c),
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ],
              ),
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.all(KSpace.lg),
                  child: Row(
                    children: [
                      Expanded(
                          child: _Stat(
                              '${event.ticketsSold}/${event.capacity}',
                              'Sold')),
                      Expanded(
                          child: _Stat(
                              formatPaise(event.revenuePaise), 'Revenue')),
                      Expanded(
                          child:
                              _Stat('${event.viewCount}', 'Views')),
                    ],
                  ),
                ),
              ),
              const SliverToBoxAdapter(
                child: TabBar(
                  isScrollable: true,
                  tabAlignment: TabAlignment.start,
                  tabs: [
                    Tab(text: 'Overview'),
                    Tab(text: 'Tickets'),
                    Tab(text: 'Venue'),
                    Tab(text: 'Schedule'),
                    Tab(text: 'Speakers'),
                    Tab(text: 'Sponsors'),
                    Tab(text: 'Media'),
                    Tab(text: 'Team'),
                    Tab(text: 'Registrations'),
                    Tab(text: 'Attendees'),
                    Tab(text: 'Check-in'),
                    Tab(text: 'Invitations'),
                    Tab(text: 'Announcements'),
                    Tab(text: 'Analytics'),
                    Tab(text: 'Certificates'),
                  ],
                ),
              ),
            ],
            body: TabBarView(
              children: [
                // Overview
                ListView(
                  padding: const EdgeInsets.all(KSpace.lg),
                  children: [
                    if (event.startsAt != null)
                      _InfoRow(Icons.calendar_today_outlined,
                          DateFormat('EEE, dd MMM yyyy').format(event.startsAt!)),
                    ListTile(
                      leading: const Icon(Icons.flag_outlined),
                      title: const Text('Event Status'),
                      trailing: TextButton(
                        onPressed: () => context
                            .push('/events/$eventId/manage/status'),
                        child: const Text('Manage'),
                      ),
                    ),
                    // There is no Representing tile. Representation is answered once, inside Create
                    // Event, and the only thing that re-opens it is a reviewer asking for changes —
                    // which is an edit of the event, reached from Event Status where that verdict is
                    // shown. A standing entry here is what made organisers believe creation had not
                    // finished and go looking for a second form to fill in.
                    ListTile(
                      leading: const Icon(Icons.verified_outlined),
                      title: const Text('Payment Readiness'),
                      trailing: TextButton(
                        onPressed: () => context
                            .push('/events/$eventId/manage/payment-ready'),
                        child: const Text('Check'),
                      ),
                    ),
                  ],
                ),
                // Tickets
                Center(
                  child: TextButton(
                    onPressed: () => context
                        .push('/events/$eventId/manage/ticket-types'),
                    child: const Text('Manage ticket types'),
                  ),
                ),
                // Venue
                Center(
                  child: TextButton(
                    onPressed: () =>
                        context.push('/events/$eventId/manage/venue'),
                    child: const Text('Edit venue'),
                  ),
                ),
                // Schedule
                Center(
                  child: TextButton(
                    onPressed: () =>
                        context.push('/events/$eventId/manage/schedule'),
                    child: const Text('Edit schedule'),
                  ),
                ),
                // Speakers
                Center(
                  child: TextButton(
                    onPressed: () =>
                        context.push('/events/$eventId/manage/speakers'),
                    child: const Text('Manage speakers'),
                  ),
                ),
                // Sponsors
                Center(
                  child: TextButton(
                    onPressed: () =>
                        context.push('/events/$eventId/manage/sponsors'),
                    child: const Text('Manage sponsors'),
                  ),
                ),
                // Media
                Center(
                  child: TextButton(
                    onPressed: () =>
                        context.push('/events/$eventId/manage/media'),
                    child: const Text('Manage media'),
                  ),
                ),
                // Team
                Center(
                  child: TextButton(
                    onPressed: () =>
                        context.push('/events/$eventId/manage/team'),
                    child: const Text('Manage team'),
                  ),
                ),
                // Registrations — the act, including rows still pending or waitlisted. Separate
                // from Attendees below, which lists issued admissions only.
                Center(
                  child: TextButton(
                    onPressed: () => context
                        .push('/events/$eventId/manage/registrations'),
                    child: const Text('View registrations'),
                  ),
                ),
                // Attendees
                Center(
                  child: TextButton(
                    onPressed: () => context
                        .push('/events/$eventId/manage/attendees'),
                    child: const Text('View attendees'),
                  ),
                ),
                // Check-in
                Center(
                  child: TextButton(
                    onPressed: () =>
                        context.push('/events/$eventId/manage/checkin'),
                    child: const Text('Open check-in scanner'),
                  ),
                ),
                // Invitations
                Center(
                  child: TextButton(
                    onPressed: () => context
                        .push('/events/$eventId/manage/invitations'),
                    child: const Text('Manage invitations'),
                  ),
                ),
                // Announcements
                Center(
                  child: TextButton(
                    onPressed: () => context
                        .push('/events/$eventId/manage/announcements'),
                    child: const Text('Manage announcements'),
                  ),
                ),
                // Analytics
                Center(
                  child: TextButton(
                    onPressed: () => context
                        .push('/events/$eventId/manage/analytics'),
                    child: const Text('View analytics'),
                  ),
                ),
                // Certificates
                Center(
                  child: TextButton(
                    onPressed: () => context
                        .push('/events/$eventId/manage/certificates'),
                    child: const Text('Manage certificates'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Color _statusColor(String status, KurxColors c) => switch (status) {
        'published' || 'live' => c.success,
        'draft' => c.muted,
        'closed' => c.danger,
        _ => c.muted,
      };
}

class _Stat extends StatelessWidget {
  const _Stat(this.value, this.label);
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Column(
      children: [
        Text(value,
            style: TextStyle(
                color: c.text, fontWeight: FontWeight.w900, fontSize: 18)),
        Text(label, style: TextStyle(color: c.muted, fontSize: 12)),
      ],
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow(this.icon, this.text);
  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: KSpace.sm),
      child: Row(
        children: [
          Icon(icon, color: c.muted, size: 18),
          const SizedBox(width: KSpace.sm),
          Text(text, style: TextStyle(color: c.text, fontSize: 14)),
        ],
      ),
    );
  }
}
