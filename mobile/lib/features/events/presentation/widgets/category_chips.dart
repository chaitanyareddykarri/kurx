import 'package:flutter/material.dart';

import '../../../../common/widgets/kurx_chip.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/event_category.dart';
import 'event_visuals.dart';

/// A horizontal rail of category chips (design-system [KurxChip]) with a glyph
/// per category, linking into filtered search.
class CategoryChips extends StatelessWidget {
  const CategoryChips({super.key, required this.categories, required this.onTap});

  final List<EventCategory> categories;
  final void Function(EventCategory category) onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 40,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
        itemCount: categories.length,
        separatorBuilder: (_, _) => const SizedBox(width: KSpace.sm),
        itemBuilder: (context, i) => KurxChip(
          label: categories[i].name,
          icon: EventVisuals.iconFor(categories[i].name),
          onTap: () => onTap(categories[i]),
        ),
      ),
    );
  }
}
