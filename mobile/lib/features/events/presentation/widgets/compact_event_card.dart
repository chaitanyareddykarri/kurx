import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../../domain/entities/event_summary.dart';
import 'event_visuals.dart';

/// A compact card for horizontal discovery rows. Fixed width so rows scroll
/// smoothly; gradient hero keyed by slug, with category, date, and price-from.
class CompactEventCard extends StatelessWidget {
  const CompactEventCard({super.key, required this.event, this.onTap, this.width = 248, this.badge});

  final EventSummary event;
  final VoidCallback? onTap;
  final double width;

  /// Optional presentation-only label ("Trending", "Recommended") for rails that want to show why an
  /// event is listed here — no new data, used only by those specific sections.
  final String? badge;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final date = event.startsAt == null
        ? null
        : DateFormat('EEE, d MMM', 'en_IN').format(event.startsAt!.toLocal());
    final meta = date ?? event.location;
    final metaIcon = date != null ? Icons.schedule_rounded : Icons.location_on_outlined;

    return Semantics(
      button: true,
      label: 'Event: ${event.title}',
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
        child: SizedBox(
          width: width,
          child: Material(
            color: c.cardSurface,
            borderRadius: BorderRadius.circular(KRadius.lg),
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: onTap,
              child: Ink(
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(KRadius.lg),
                  border: Border.all(color: c.border),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    EventHero(
                      seed: event.slug,
                      category: event.categoryName,
                      height: 96,
                      overlay: Padding(
                        padding: const EdgeInsets.all(KSpace.sm),
                        child: Row(
                          children: [
                            if (event.categoryName != null) _Tag(event.categoryName!),
                            const Spacer(),
                            if (badge != null) _Tag(badge!),
                            if (event.isFeatured) ...[
                              const SizedBox(width: KSpace.xs),
                              const Icon(Icons.star_rounded, size: 18, color: Colors.white),
                            ],
                          ],
                        ),
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.all(KSpace.md),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(event.title,
                              style: TextStyle(color: c.text, fontSize: 14.5, fontWeight: FontWeight.w700, height: 1.2),
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis),
                          if (meta != null) ...[
                            const SizedBox(height: KSpace.sm),
                            Row(children: [
                              Icon(metaIcon, size: 13, color: c.muted),
                              const SizedBox(width: KSpace.xs + 2),
                              Expanded(
                                child: Text(meta,
                                    style: TextStyle(color: c.muted, fontSize: 12.5),
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis),
                              ),
                            ]),
                          ],
                          const SizedBox(height: KSpace.sm),
                          Text(_priceLabel(event),
                              style: TextStyle(color: c.accent, fontSize: 13.5, fontWeight: FontWeight.w800)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

String _priceLabel(EventSummary e) {
  if (e.priceFromPaise == null) return 'View details';
  if (e.isFree) return 'Free';
  return 'From ${Money.fromMinor(e.priceFromPaise!)}';
}

class _Tag extends StatelessWidget {
  const _Tag(this.label);
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: 3),
      decoration: BoxDecoration(
        color: Colors.black.withValues(alpha: 0.32),
        borderRadius: BorderRadius.circular(KRadius.pill),
      ),
      child: Text(label,
          style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700)),
    );
  }
}
