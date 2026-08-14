import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/content_width.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../events/domain/entities/event_category.dart';
import '../../../events/presentation/providers/search_providers.dart';
import '../../../events/presentation/widgets/event_visuals.dart';

/// Browse categories supplied by the API taxonomy.
class CategoriesPage extends ConsumerWidget {
  const CategoriesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final categories = ref.watch(categoriesProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Categories')),
      body: AsyncValueView<List<EventCategory>>(
        value: categories,
        onRetry: () => ref.invalidate(categoriesProvider),
        data: (nodes) {
          final items = nodes
              .where((node) => node.level == 'category')
              .toList();
          final typeCount = nodes.where((node) => node.level == 'type').length;
          return ContentWidth(
            maxWidth: 720,
            child: CustomScrollView(
              slivers: [
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(
                      KSpace.lg,
                      KSpace.md,
                      KSpace.lg,
                      KSpace.xs,
                    ),
                    child: Text(
                      '$typeCount event types across ${items.length} categories',
                      style: TextStyle(color: context.kurx.muted, fontSize: 13),
                    ),
                  ),
                ),
                SliverPadding(
                  padding: const EdgeInsets.all(KSpace.lg),
                  sliver: SliverGrid(
                    gridDelegate:
                        const SliverGridDelegateWithMaxCrossAxisExtent(
                          maxCrossAxisExtent: 220,
                          mainAxisSpacing: KSpace.md,
                          crossAxisSpacing: KSpace.md,
                          childAspectRatio: 1.35,
                        ),
                    delegate: SliverChildBuilderDelegate(
                      (context, index) => _CategoryTile(
                        category: items[index],
                        typeCount: nodes
                            .where((node) => node.parentId == items[index].id)
                            .length,
                      ),
                      childCount: items.length,
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _CategoryTile extends StatelessWidget {
  const _CategoryTile({required this.category, required this.typeCount});

  final EventCategory category;
  final int typeCount;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Material(
      color: c.cardSurface,
      borderRadius: BorderRadius.circular(KRadius.lg),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/categories/${category.id}'),
        child: Ink(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(KRadius.lg),
            border: Border.all(color: c.border),
          ),
          padding: const EdgeInsets.all(KSpace.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                height: 40,
                width: 40,
                decoration: BoxDecoration(
                  gradient: EventVisuals.linearFor(category.id),
                  borderRadius: BorderRadius.circular(KRadius.md),
                ),
                child: Icon(
                  EventVisuals.iconFor(category.name),
                  color: Colors.white,
                  size: 22,
                ),
              ),
              const Spacer(),
              Text(
                category.name,
                style: TextStyle(
                  color: c.text,
                  fontSize: 15.5,
                  fontWeight: FontWeight.w800,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 2),
              Text(
                '$typeCount types',
                style: TextStyle(color: c.muted, fontSize: 12.5),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
