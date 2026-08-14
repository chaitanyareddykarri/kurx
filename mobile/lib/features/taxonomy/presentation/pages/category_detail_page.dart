import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../events/domain/entities/event_category.dart';
import '../../../events/presentation/providers/search_providers.dart';
import '../../../events/presentation/widgets/event_visuals.dart';

/// Shows the event types currently returned by the API for one category.
class CategoryDetailPage extends ConsumerWidget {
  const CategoryDetailPage({super.key, required this.categoryId});

  final String categoryId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(title: const Text('Category')),
      body: AsyncValueView<List<EventCategory>>(
        value: ref.watch(categoriesProvider),
        onRetry: () => ref.invalidate(categoriesProvider),
        data: (nodes) {
          final category = nodes
              .where((node) => node.id == categoryId)
              .firstOrNull;
          if (category == null) {
            return const Center(child: Text('Category not found'));
          }
          final types = nodes
              .where((node) => node.parentId == category.id)
              .toList();
          return _CategoryDetail(category: category, types: types);
        },
      ),
    );
  }
}

class _CategoryDetail extends ConsumerWidget {
  const _CategoryDetail({required this.category, required this.types});

  final EventCategory category;
  final List<EventCategory> types;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    void searchCategory() {
      ref.read(eventSearchControllerProvider.notifier)
        ..clearFilters()
        ..setCategory(category.id);
      context.push(Routes.search);
    }

    void searchType(String type) {
      ref.read(eventSearchControllerProvider.notifier)
        ..clearFilters()
        ..setSearchText(type);
      context.push(Routes.search);
    }

    return ContentWidth(
      child: ListView(
        padding: const EdgeInsets.all(KSpace.lg),
        children: [
          Container(
            padding: const EdgeInsets.all(KSpace.lg),
            decoration: BoxDecoration(
              gradient: EventVisuals.linearFor(category.id),
              borderRadius: BorderRadius.circular(KRadius.lg),
            ),
            child: Row(
              children: [
                Icon(
                  EventVisuals.iconFor(category.name),
                  color: Colors.white,
                  size: 32,
                ),
                const SizedBox(width: KSpace.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        category.name,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 20,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      Text(
                        '${types.length} event types',
                        style: TextStyle(
                          color: Colors.white.withValues(alpha: 0.9),
                          fontSize: 12.5,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: KSpace.md),
          KurxButton(
            label: 'See all ${category.name} events',
            icon: Icons.arrow_forward_rounded,
            variant: KurxButtonVariant.secondary,
            expand: true,
            onPressed: searchCategory,
          ),
          const SizedBox(height: KSpace.lg),
          Text(
            'EVENT TYPES',
            style: TextStyle(
              color: c.muted,
              fontSize: 11,
              fontWeight: FontWeight.w800,
              letterSpacing: 1,
            ),
          ),
          const SizedBox(height: KSpace.sm),
          for (final type in types)
            Padding(
              padding: const EdgeInsets.only(bottom: KSpace.sm),
              child: Material(
                color: c.cardSurface,
                borderRadius: BorderRadius.circular(KRadius.md),
                clipBehavior: Clip.antiAlias,
                child: InkWell(
                  onTap: () => searchType(type.name),
                  child: Ink(
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(KRadius.md),
                      border: Border.all(color: c.border),
                    ),
                    padding: const EdgeInsets.symmetric(
                      horizontal: KSpace.lg,
                      vertical: KSpace.md,
                    ),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            type.name,
                            style: TextStyle(
                              color: c.text,
                              fontSize: 14.5,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                        Icon(
                          Icons.chevron_right_rounded,
                          color: c.muted,
                          size: 20,
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
