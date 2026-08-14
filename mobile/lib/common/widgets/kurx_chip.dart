import 'package:flutter/material.dart';

import '../../core/theme/design_tokens.dart';

/// A selectable pill used for filters and taxonomy facets. Selected chips fill
/// with the accent tint; unselected read as a bordered surface.
class KurxChip extends StatelessWidget {
  const KurxChip({
    super.key,
    required this.label,
    this.selected = false,
    this.onTap,
    this.icon,
    this.count,
    this.onDeleted,
  });

  final String label;
  final bool selected;
  final VoidCallback? onTap;
  final IconData? icon;

  /// Optional trailing count (e.g. filter result totals).
  final int? count;

  /// When set, renders a small trailing "×" that calls this instead of [onTap] — an active-filter chip
  /// the user can remove individually (mirrors Material's `Chip.onDeleted`).
  final VoidCallback? onDeleted;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final fg = selected ? c.accent : c.muted;
    return Material(
      color: selected ? c.accent.withValues(alpha: 0.14) : c.cardSurface,
      borderRadius: BorderRadius.circular(KRadius.pill),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(KRadius.pill),
        child: Ink(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(KRadius.pill),
            border: Border.all(color: selected ? c.accent : c.border),
          ),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.sm),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (icon != null) ...[Icon(icon, size: 15, color: fg), const SizedBox(width: KSpace.xs + 2)],
                Text(label,
                    style: TextStyle(color: selected ? c.text : c.muted, fontSize: 13, fontWeight: FontWeight.w600)),
                if (count != null) ...[
                  const SizedBox(width: KSpace.xs + 2),
                  Text('$count', style: TextStyle(color: fg, fontSize: 12, fontWeight: FontWeight.w700)),
                ],
                if (onDeleted != null) ...[
                  const SizedBox(width: KSpace.xs + 2),
                  // Was a bare GestureDetector around a 14px icon: unannounced to TalkBack and
                  // VoiceOver, and a tap target a fraction of the 48dp floor. Same defect Phase 33
                  // found on the tab bar, in the chip every filter and tag on mobile is built from.
                  Semantics(
                    button: true,
                    label: 'Remove $label',
                    child: GestureDetector(
                      onTap: onDeleted,
                      behavior: HitTestBehavior.opaque,
                      child: SizedBox(
                        width: 48,
                        height: 48,
                        child: Center(child: Icon(Icons.close, size: 14, color: fg)),
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
