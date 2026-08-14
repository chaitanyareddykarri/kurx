import 'package:flutter/material.dart';

import '../../core/theme/design_tokens.dart';

/// Warm card surface — 24 px radius, soft shadow, no border (D-056).
/// Provide [onTap] to make it tappable (adds a ripple).
class KurxCard extends StatelessWidget {
  const KurxCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(KSpace.lg),
    this.onTap,
    this.elevated = false,
    this.shadow = true,
  });

  final Widget child;
  final EdgeInsets padding;
  final VoidCallback? onTap;

  /// Use the raised surface token (e.g. sheets over cards).
  final bool elevated;

  /// Set to false to suppress the shadow (e.g. when inside another card).
  final bool shadow;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final radius = BorderRadius.circular(KRadius.xl);
    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: radius,
        boxShadow: shadow ? kCardShadow(context) : null,
      ),
      child: Material(
        color: elevated ? c.elevated : c.cardSurface,
        borderRadius: radius,
        child: InkWell(
          onTap: onTap,
          borderRadius: radius,
          child: Padding(padding: padding, child: child),
        ),
      ),
    );
  }
}

/// A compact metric tile (label + value + optional trend), used on dashboards.
class KurxStatCard extends StatelessWidget {
  const KurxStatCard({
    super.key,
    required this.label,
    required this.value,
    this.trend,
    this.trendUp = true,
    this.icon,
  });

  final String label;
  final String value;
  final String? trend;
  final bool trendUp;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return KurxCard(
      padding: const EdgeInsets.all(KSpace.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              if (icon != null) ...[
                Icon(icon, size: 16, color: c.muted),
                const SizedBox(width: KSpace.sm),
              ],
              Expanded(
                child: Text(
                  label,
                  style: TextStyle(color: c.muted, fontSize: 13, fontWeight: FontWeight.w600),
                ),
              ),
            ],
          ),
          const SizedBox(height: KSpace.md),
          Row(
            crossAxisAlignment: CrossAxisAlignment.baseline,
            textBaseline: TextBaseline.alphabetic,
            children: [
              Text(value,
                  style: TextStyle(color: c.text, fontSize: 24, fontWeight: FontWeight.w800)),
              if (trend != null) ...[
                const SizedBox(width: KSpace.sm),
                Icon(
                  trendUp ? Icons.trending_up_rounded : Icons.trending_down_rounded,
                  size: 16,
                  color: trendUp ? c.success : c.danger,
                ),
                const SizedBox(width: 2),
                Text(
                  trend!,
                  style: TextStyle(
                    color: trendUp ? c.success : c.danger,
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}
