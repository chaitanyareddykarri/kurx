import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/error_retry.dart';
import '../../../../common/widgets/guest_banner.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/session/session_controller.dart';
import '../../../../common/widgets/shimmer.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../domain/entities/event_category.dart';
import '../../domain/entities/event_kind.dart';
import '../../domain/entities/event_section.dart';
import '../../domain/entities/event_summary.dart';
import '../../../../common/widgets/kurx_shell_app_bar.dart';
import '../../domain/entities/home_feed.dart';
import '../providers/home_providers.dart';
import '../providers/search_providers.dart';
import '../widgets/category_chips.dart';
import '../widgets/compact_event_card.dart';
import '../widgets/featured_carousel.dart';
import '../widgets/home_skeleton.dart';
import '../widgets/kind_chips.dart';
import '../widgets/section_header.dart';

class HomeDashboardPage extends ConsumerWidget {
  const HomeDashboardPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final feedAsync = ref.watch(homeFeedProvider);
    final isGuest = ref.watch(sessionControllerProvider).status != AuthStatus.authenticated;

    return Scaffold(
      // Profile (top-left) and Notifications (top-right) come from the shared shell bar; only
      // Home's own controls are declared here.
      appBar: KurxShellAppBar(
        title: 'Home',
        actions: [
          IconButton(
            tooltip: 'Search events',
            icon: const Icon(Icons.search_rounded),
            onPressed: () => context.push(Routes.search),
          ),
          IconButton(
            tooltip: 'Saved',
            icon: const Icon(Icons.bookmark_border_rounded),
            onPressed: () => context.push(Routes.saved),
          ),
        ],
        bottom: isGuest ? GuestBanner(onSignIn: () => _startSignIn(context, ref)) : null,
      ),
      body: RefreshIndicator(
        onRefresh: () async => ref.refresh(homeFeedProvider.future),
        child: feedAsync.when(
          loading: () => const HomeSkeleton(),
          error: (err, _) => ListView(
            children: [
              const SizedBox(height: 120),
              ErrorRetry.fromError(error: err, onRetry: () => ref.invalidate(homeFeedProvider)),
            ],
          ),
          data: (feed) => _Discovery(feed: feed),
        ),
      ),
    );
  }

  void _startSignIn(BuildContext context, WidgetRef ref) {
    ref.read(loginReturnToProvider.notifier).state = GoRouterState.of(context).matchedLocation;
    context.push(Routes.login);
  }
}

class _Discovery extends ConsumerWidget {
  const _Discovery({required this.feed});

  final HomeFeed feed;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    void openEvent(EventSummary e) => context.push('/events/${e.slug}');
    void viewAll(EventSection s) => context.push('/discover/${s.name}');
    void openCategory(EventCategory c) {
      ref.read(eventSearchControllerProvider.notifier).setCategory(c.id);
      context.push(Routes.search);
    }

    void openKind(EventKind k) {
      ref.read(eventSearchControllerProvider.notifier).setKind(k.slug);
      context.push(Routes.search);
    }

    // V3 §15 (Phase 16, D-184): /for-you is auth-required — never watched for a guest, so no auth-error UI
    // is needed here, same as how the sign-out button above is simply absent for guests.
    final isSignedIn = ref.watch(sessionControllerProvider).status == AuthStatus.authenticated;
    final kinds = ref.watch(kindsProvider).valueOrNull ?? const <EventKind>[];

    if (feed.isEmpty) {
      return ListView(
        children: [
          const SizedBox(height: 12),
          _SearchBar(onTap: () => context.push(Routes.search)),
          const SizedBox(height: 64),
          const EmptyState(
            icon: Icons.event_busy_outlined,
            title: 'No events to discover yet',
            message: 'Pull down to refresh or check back soon.',
          ),
        ],
      );
    }

    return ListView(
      padding: const EdgeInsets.only(bottom: 24),
      children: [
        const SizedBox(height: 12),
        _SearchBar(onTap: () => context.push(Routes.search)),
        if (feed.featured.isNotEmpty) ...[
          const SectionHeader(title: 'Featured'),
          FeaturedCarousel(events: feed.featured, onTap: openEvent),
        ],
        if (isSignedIn) _ForYouSection(onTap: openEvent),
        if (feed.trending.isNotEmpty)
          _HorizontalSection(
            title: 'Trending',
            events: feed.trending,
            onViewAll: () => viewAll(EventSection.trending),
            onTap: openEvent,
            badge: 'Trending',
          ),
        if (feed.upcoming.isNotEmpty)
          _HorizontalSection(
            title: 'Upcoming',
            events: feed.upcoming,
            onViewAll: () => viewAll(EventSection.upcoming),
            onTap: openEvent,
          ),
        if (feed.latest.isNotEmpty)
          _HorizontalSection(
            title: 'Latest',
            events: feed.latest,
            onViewAll: () => viewAll(EventSection.latest),
            onTap: openEvent,
          ),
        if (feed.categories.isNotEmpty) ...[
          SectionHeader(title: 'Browse categories', onViewAll: () => context.push(Routes.categories)),
          CategoryChips(categories: feed.categories, onTap: openCategory),
        ],
        if (kinds.isNotEmpty) ...[
          const SectionHeader(title: 'Browse by type'),
          KindChips(kinds: kinds, onTap: openKind),
        ],
        const SizedBox(height: 8),
      ],
    );
  }
}

/// V3 §15 (Phase 16, D-184) recommended rail — only ever built while signed in (see the `isSignedIn`
/// gate above). Hides itself on error or an empty response rather than showing a failure state, since
/// this is a secondary/optional feed, not the primary discovery content.
class _ForYouSection extends ConsumerWidget {
  const _ForYouSection({required this.onTap});

  final void Function(EventSummary event) onTap;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final forYouAsync = ref.watch(forYouProvider);
    return forYouAsync.when(
      loading: () => const _ForYouSkeleton(),
      error: (_, _) => const SizedBox.shrink(),
      data: (events) => events.isEmpty
          ? const SizedBox.shrink()
          : _HorizontalSection(title: 'Recommended for you', events: events, onTap: onTap, badge: 'For you'),
    );
  }
}

class _ForYouSkeleton extends StatelessWidget {
  const _ForYouSkeleton();

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const ShimmerBox(height: 22, width: 180, margin: EdgeInsets.symmetric(horizontal: 16)),
        const SizedBox(height: 12),
        SizedBox(
          height: 172,
          child: ListView(
            scrollDirection: Axis.horizontal,
            physics: const NeverScrollableScrollPhysics(),
            padding: const EdgeInsets.symmetric(horizontal: 10),
            children: const [
              ShimmerBox(height: 160, width: 224, margin: EdgeInsets.symmetric(horizontal: 6), radius: 16),
              ShimmerBox(height: 160, width: 224, margin: EdgeInsets.symmetric(horizontal: 6), radius: 16),
            ],
          ),
        ),
        const SizedBox(height: 20),
      ],
    );
  }
}

class _HorizontalSection extends StatelessWidget {
  const _HorizontalSection({
    required this.title,
    required this.events,
    this.onViewAll,
    required this.onTap,
    this.badge,
  });

  final String title;
  final List<EventSummary> events;
  final VoidCallback? onViewAll;
  final void Function(EventSummary event) onTap;
  final String? badge;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionHeader(title: title, onViewAll: onViewAll),
        SizedBox(
          height: 222,
          child: ListView.builder(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 10),
            itemCount: events.length,
            itemBuilder: (context, i) =>
                CompactEventCard(event: events[i], onTap: () => onTap(events[i]), badge: badge),
          ),
        ),
      ],
    );
  }
}

class _SearchBar extends StatelessWidget {
  const _SearchBar({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Semantics(
        button: true,
        label: 'Search events',
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(14),
          child: Container(
            height: 48,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            decoration: BoxDecoration(
              color: scheme.surfaceContainerHighest,
              borderRadius: BorderRadius.circular(14),
            ),
            child: Row(
              children: [
                Icon(Icons.search, color: scheme.onSurfaceVariant),
                const SizedBox(width: 12),
                Text('Search events', style: TextStyle(color: scheme.onSurfaceVariant)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

