import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/utils/money.dart';
import '../../../../common/widgets/shimmer.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../bookmarks/presentation/providers/bookmarks_providers.dart';
import '../../domain/entities/event_detail.dart';
import '../../domain/entities/event_page.dart';
import '../../domain/entities/event_summary.dart';
import '../providers/events_providers.dart';
import '../widgets/event_card.dart';
import '../widgets/event_visuals.dart';
import '../widgets/ticket_type_tile.dart';

/// A summary snapshot from a detail record, so the detail page can bookmark
/// through the same store the lists use.
EventSummary _summaryOf(
  EventDetail d, {
  int? priceFromPaise,
  String? category,
}) => EventSummary(
  id: d.id,
  title: d.title,
  slug: d.slug,
  subtitle: d.subtitle,
  venueName: d.venueName,
  city: d.city,
  startsAt: d.startsAt,
  status: d.status,
  categoryName: category,
  priceFromPaise: priceFromPaise,
);

class EventDetailPage extends ConsumerWidget {
  const EventDetailPage({super.key, required this.slug});

  final String slug;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pageAsync = ref.watch(eventPageProvider(slug));

    return Scaffold(
      appBar: AppBar(title: const Text('Event')),
      body: AsyncValueView<EventPage>(
        value: pageAsync,
        loading: const _DetailSkeleton(),
        onRetry: () => ref.invalidate(eventPageProvider(slug)),
        // `CheckoutPage` is implemented and calls `createOrder` correctly, and its route
        // `/events/:slug/checkout/:eventId/:ticketTypeId` has existed all along — with **nothing in
        // the app navigating to it**. It was reachable only by typing the URL, so on mobile a person
        // could read an event and had no way to register for it from the page describing it.
        //
        // The mirror of what REG-009 records for web: web has the entry point and no working
        // checkout; mobile had the working checkout and no entry point.
        data: (page) => Column(
          children: [
            Expanded(
              child: RefreshIndicator(
          onRefresh: () async => ref.refresh(eventPageProvider(slug).future),
          child: ListView(
            children: [
              _Header(page: page),
              // Who the host represents (D-302). The API returned only a bare org id, so no screen could
              // name the institution behind an event — the one fact an attendee uses to judge it.
              if (page.detail.representing != null)
                Padding(
                  padding: const EdgeInsets.fromLTRB(KSpace.lg, KSpace.lg, KSpace.lg, 0),
                  child: Row(
                    children: [
                      Icon(Icons.apartment_rounded, size: 18, color: context.kurx.muted),
                      const SizedBox(width: KSpace.sm),
                      Expanded(
                        child: Text(
                          'Hosted on behalf of ${page.detail.representing!.name}',
                          style: TextStyle(color: context.kurx.muted),
                        ),
                      ),
                      if (page.detail.representing!.isVerified)
                        Icon(Icons.verified_rounded, size: 18, color: context.kurx.accent),
                    ],
                  ),
                ),
              // The gallery. Stored and returned all along; never rendered until D-302.
              if (page.detail.gallery.isNotEmpty)
                SizedBox(
                  height: 140,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.fromLTRB(KSpace.lg, KSpace.lg, KSpace.lg, 0),
                    itemCount: page.detail.gallery.length,
                    separatorBuilder: (_, _) => const SizedBox(width: KSpace.md),
                    itemBuilder: (context, i) {
                      final item = page.detail.gallery[i];
                      return ClipRRect(
                        borderRadius: BorderRadius.circular(KRadius.md),
                        child: Image.network(
                          item.url,
                          width: 200,
                          fit: BoxFit.cover,
                          errorBuilder: (_, _, _) => const SizedBox.shrink(),
                        ),
                      );
                    },
                  ),
                ),
              if (page.detail.description != null &&
                  page.detail.description!.isNotEmpty)
                _Section(
                  title: 'About',
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: Text(page.detail.description!),
                  ),
                ),
              if (page.detail.tags.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                  child: Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: page.detail.tags
                        .map((t) => Chip(label: Text(t)))
                        .toList(),
                  ),
                ),
              _Section(
                title: 'Tickets',
                child: page.ticketTypes.isEmpty
                    ? const Padding(
                        padding: EdgeInsets.symmetric(horizontal: 16),
                        child: Text('No tickets on sale right now.'),
                      )
                    : Column(
                        children: page.ticketTypes
                            .map(
                              (t) => TicketTypeTile(
                                ticket: t,
                                onBook: () => context.push(
                                  '/events/${page.detail.slug}/checkout/${page.detail.id}/${t.id}',
                                ),
                              ),
                            )
                            .toList(),
                      ),
              ),
              // Reviews apply to every event (GET /v1/events/{id}/reviews is ungated), so this needs
              // no capability field on the detail DTO — the screen existed with no way to reach it.
              _Section(
                title: 'Reviews',
                child: ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.rate_review_outlined),
                  title: const Text('Ratings & reviews'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () =>
                      context.push('/events/${page.detail.id}/reviews'),
                ),
              ),
              if (page.related.isNotEmpty)
                _Section(
                  title: 'You might also like',
                  child: Column(
                    children: page.related
                        .map(
                          (e) => EventCard(
                            event: e,
                            onTap: () => context.push('/events/${e.slug}'),
                          ),
                        )
                        .toList(),
                  ),
                ),
              const SizedBox(height: 24),
            ],
          ),
              ),
            ),
            _RegisterBar(page: page),
          ],
        ),
      ),
    );
  }
}

/// The register action, built from the ticket types the page has already loaded.
///
/// Nothing here is invented: the cheapest still-available type is preselected because that is the
/// one a "Register" button means, and when several exist the label says so rather than pretending
/// the choice was already made. An event with no available type says why instead of offering a
/// button that would fail at the next screen.
class _RegisterBar extends StatelessWidget {
  const _RegisterBar({required this.page});

  final EventPage page;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final sellable = page.ticketTypes.where((t) => t.available > 0).toList()
      ..sort((a, b) => a.pricePaise.compareTo(b.pricePaise));

    if (page.ticketTypes.isEmpty) return const SizedBox.shrink();

    final cheapest = sellable.isEmpty ? null : sellable.first;
    final priceLabel = cheapest == null
        ? null
        : cheapest.pricePaise == 0
            ? 'Free'
            : formatPaise(cheapest.pricePaise);

    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
        child: cheapest == null
            ? Semantics(
                liveRegion: true,
                child: Text(
                  'Registration is closed — no tickets are available.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: c.muted, fontSize: 13),
                ),
              )
            : KurxButton(
                label: sellable.length > 1
                    ? 'Choose a ticket · from $priceLabel'
                    : 'Register · $priceLabel',
                onPressed: () => context.push(
                  '/events/${page.detail.slug}/checkout/${page.detail.id}/${cheapest.id}',
                ),
              ),
      ),
    );
  }
}

class _DetailSkeleton extends StatelessWidget {
  const _DetailSkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      physics: const NeverScrollableScrollPhysics(),
      padding: const EdgeInsets.all(16),
      children: const [
        ShimmerBox(height: 160, radius: 16),
        SizedBox(height: 16),
        ShimmerBox(height: 28, width: 240),
        SizedBox(height: 10),
        ShimmerBox(height: 18, width: 160),
        SizedBox(height: 24),
        ShimmerBox(height: 14),
        SizedBox(height: 8),
        ShimmerBox(height: 14),
        SizedBox(height: 8),
        ShimmerBox(height: 14, width: 200),
      ],
    );
  }
}

class _Header extends ConsumerWidget {
  const _Header({required this.page});

  final EventPage page;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final detail = page.detail;
    final minPrice = page.ticketTypes.isEmpty
        ? null
        : page.ticketTypes
              .map((t) => t.pricePaise)
              .reduce((a, b) => a < b ? a : b);
    final summary = _summaryOf(detail, priceFromPaise: minPrice);
    final saved = ref.watch(isBookmarkedProvider(detail.slug));
    final when = detail.startsAt == null
        ? null
        : DateFormat(
            'EEEE, d MMMM y · h:mm a',
            'en_IN',
          ).format(detail.startsAt!.toLocal());

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        EventHero(
          seed: detail.slug,
          category: summary.categoryName,
          imageUrl: detail.bannerUrl,
          height: 210,
          overlay: Align(
            alignment: Alignment.topRight,
            child: Padding(
              padding: const EdgeInsets.all(KSpace.md),
              child: Material(
                color: Colors.black.withValues(alpha: 0.35),
                shape: const CircleBorder(),
                child: IconButton(
                  onPressed: () => ref
                      .read(bookmarksControllerProvider.notifier)
                      .toggle(summary),
                  icon: Icon(
                    saved
                        ? Icons.bookmark_rounded
                        : Icons.bookmark_border_rounded,
                    color: Colors.white,
                  ),
                  tooltip: saved ? 'Remove bookmark' : 'Save event',
                ),
              ),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(KSpace.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                detail.title,
                style: TextStyle(
                  color: c.text,
                  fontSize: 24,
                  fontWeight: FontWeight.w800,
                  height: 1.15,
                ),
              ),
              if (detail.subtitle != null && detail.subtitle!.isNotEmpty) ...[
                const SizedBox(height: KSpace.xs),
                Text(
                  detail.subtitle!,
                  style: TextStyle(color: c.muted, fontSize: 15),
                ),
              ],
              const SizedBox(height: KSpace.lg),
              if (when != null)
                _InfoRow(icon: Icons.schedule_rounded, text: when),
              if (detail.location != null) ...[
                const SizedBox(height: KSpace.sm),
                _InfoRow(
                  icon: Icons.location_on_outlined,
                  text: detail.location!,
                ),
              ],
              if (detail.address != null && detail.address!.isNotEmpty) ...[
                const SizedBox(height: KSpace.sm),
                _InfoRow(icon: Icons.map_outlined, text: detail.address!),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(
          icon,
          size: 18,
          color: Theme.of(context).colorScheme.onSurfaceVariant,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(text, style: Theme.of(context).textTheme.bodyMedium),
        ),
      ],
    );
  }
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
          child: Text(title, style: Theme.of(context).textTheme.titleLarge),
        ),
        child,
      ],
    );
  }
}
