import 'package:flutter/material.dart';

import '../../../../common/widgets/kurx_chip.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/event_kind.dart';

/// A horizontal rail of Kind chips (design-system [KurxChip]) — quick discoverability for the V3 Kind
/// registry (Phase 16), mirroring [CategoryChips]' rail-into-filtered-search pattern.
class KindChips extends StatelessWidget {
  const KindChips({super.key, required this.kinds, required this.onTap});

  final List<EventKind> kinds;
  final void Function(EventKind kind) onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 40,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
        itemCount: kinds.length,
        separatorBuilder: (_, _) => const SizedBox(width: KSpace.sm),
        itemBuilder: (context, i) => KurxChip(
          label: kinds[i].name,
          onTap: () => onTap(kinds[i]),
        ),
      ),
    );
  }
}
