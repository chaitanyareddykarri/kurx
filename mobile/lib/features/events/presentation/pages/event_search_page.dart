import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/error_retry.dart';
import '../../../../common/widgets/kurx_chip.dart';
import '../../../../common/widgets/shimmer.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/search_providers.dart';
import '../widgets/event_card.dart';
import '../widgets/event_filter_bar.dart';

class EventSearchPage extends ConsumerStatefulWidget {
  const EventSearchPage({super.key});

  @override
  ConsumerState<EventSearchPage> createState() => _EventSearchPageState();
}

class _EventSearchPageState extends ConsumerState<EventSearchPage> {
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (!_scrollController.hasClients) return;
    final position = _scrollController.position;
    if (position.pixels >= position.maxScrollExtent - 300) {
      ref.read(eventSearchControllerProvider.notifier).loadMore();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(eventSearchControllerProvider);
    final controller = ref.read(eventSearchControllerProvider.notifier);

    return Scaffold(
      // Explicit leading: the automatic one appears only when the route can pop, and `/search` is
      // both a deep-link target and public, so it can be the app's very first screen. Falling back
      // to Home keeps it from being a dead end on that entry path.
      //
      // `Navigator.canPop`, not go_router's `context.canPop()` — the latter asserts outright when no
      // GoRouter is above it, which is every widget test that pumps this page on its own.
      appBar: AppBar(
        title: const Text('Search events'),
        leading: Builder(
          builder: (context) {
            final canPop = Navigator.canPop(context);
            return IconButton(
              icon: Icon(canPop ? Icons.arrow_back : Icons.home_outlined),
              tooltip: canPop ? 'Back' : 'Home',
              onPressed: () => canPop ? Navigator.pop(context) : context.go(Routes.events),
            );
          },
        ),
      ),
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: controller.retry,
          // One scroll view for the filters AND the results, where a Column of fixed-height header
          // widgets above an Expanded list used to be. That Column could not shrink: when the
          // keyboard opened it took ~40% of the viewport, the header kept its intrinsic height and
          // the Expanded child was handed a negative constraint — the "Bottom overflowed by N
          // pixels" stripe. Slivers have no such fixed division; the header simply scrolls away.
          child: CustomScrollView(
            controller: _scrollController,
            // The results can be shorter than the viewport (empty/error), and a pull-to-refresh
            // still has to start from a scrollable that accepts the gesture.
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              const SliverToBoxAdapter(child: _SearchScopes()),
              const SliverToBoxAdapter(child: EventFilterBar()),
              const SliverToBoxAdapter(child: SizedBox(height: KSpace.sm)),
              ..._resultSlivers(context, state, controller),
              // Clears the floating pill nav and the gesture bar, so the last card is never
              // trapped underneath either.
              const SliverToBoxAdapter(child: SizedBox(height: KSpace.xxxl)),
            ],
          ),
        ),
      ),
    );
  }

  /// Loading, error, zero-results and results are four distinct states — never one standing in for
  /// another. In particular the empty state is only ever reached on a *successful* response.
  List<Widget> _resultSlivers(
    BuildContext context,
    EventSearchState state,
    EventSearchController controller,
  ) {
    switch (state.status) {
      case SearchStatus.loading:
        // `hasScrollBody: true` gives the skeleton the remaining viewport height. It is a ListView
        // internally, so a SliverToBoxAdapter would hand it unbounded height and assert.
        return const [SliverFillRemaining(hasScrollBody: true, child: ListSkeleton())];
      case SearchStatus.error:
        return [
          SliverFillRemaining(
            hasScrollBody: false,
            child: ErrorRetry.fromError(error: state.error, onRetry: controller.retry),
          ),
        ];
      case SearchStatus.success:
        if (state.items.isEmpty) {
          final filtered = state.query.hasActiveFilters;
          return [
            SliverFillRemaining(
              hasScrollBody: false,
              child: EmptyState(
                icon: Icons.search_off,
                title: filtered ? 'No events match your filters' : 'No events yet',
                message: filtered
                    ? 'Try widening the date range, or removing a filter or two.'
                    : 'Check back soon — new events are added all the time.',
                // Only offered when there is something to clear; a button that does nothing is
                // worse than no button.
                actionLabel: filtered ? 'Clear filters' : null,
                onAction: filtered ? controller.clearFilters : null,
              ),
            ),
          ];
        }
        return [
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(KSpace.lg, 0, KSpace.lg, KSpace.sm),
              child: Text(
                '${state.total} result${state.total == 1 ? '' : 's'}',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          ),
          SliverList.builder(
            itemCount: state.items.length,
            itemBuilder: (context, i) {
              final event = state.items[i];
              return EventCard(
                event: event,
                onTap: () => context.push('/events/${event.slug}'),
              );
            },
          ),
          SliverToBoxAdapter(child: _Footer(state: state, onLoadMore: controller.loadMore)),
        ];
    }
  }
}

/// Search covers three things, and until now only events were reachable from here. People and posts
/// each already own a working search screen, so these route to those rather than duplicating either
/// into a combined results list. Mirrors the same three chips on web's `/discover`.
class _SearchScopes extends StatelessWidget {
  const _SearchScopes();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(KSpace.lg, KSpace.md, KSpace.lg, 0),
      // Wrap, not Row: three fixed chips overflowed a 360dp screen by 15px, and would overflow any
      // screen once the text scale is turned up.
      child: Wrap(
        spacing: KSpace.sm,
        runSpacing: KSpace.sm,
        children: [
          const KurxChip(label: 'Events', selected: true),
          KurxChip(label: 'People', onTap: () => context.push('/people-search')),
          KurxChip(label: 'Posts', onTap: () => context.push('/posts/search')),
        ],
      ),
    );
  }
}

class _Footer extends StatelessWidget {
  const _Footer({required this.state, required this.onLoadMore});

  final EventSearchState state;
  final VoidCallback onLoadMore;

  @override
  Widget build(BuildContext context) {
    if (state.loadingMore) {
      return const Padding(
        padding: EdgeInsets.all(KSpace.lg),
        child: Center(
            child: SizedBox(height: 24, width: 24, child: CircularProgressIndicator(strokeWidth: 2))),
      );
    }
    if (state.loadMoreFailed) {
      return Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Center(
          child: OutlinedButton.icon(
            onPressed: onLoadMore,
            icon: const Icon(Icons.refresh),
            label: const Text("Couldn't load more — Retry"),
          ),
        ),
      );
    }
    if (!state.hasMore) {
      return Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Center(
            child: Text('No more results', style: Theme.of(context).textTheme.bodySmall)),
      );
    }
    return Padding(
      padding: const EdgeInsets.all(KSpace.lg),
      child: Center(child: OutlinedButton(onPressed: onLoadMore, child: const Text('Load more'))),
    );
  }
}
