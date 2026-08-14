import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../../../bookmarks/presentation/providers/bookmarks_providers.dart';
import '../../domain/entities/event_summary.dart';
import 'event_visuals.dart';

/// The full-width event row used in search results, related lists, and saved
/// events. Gradient thumb keyed by slug, category, date, location, price-from,
/// and a working bookmark toggle.
class EventCard extends ConsumerWidget {
  const EventCard({super.key, required this.event, this.onTap, this.showBookmark = true});

  final EventSummary event;
  final VoidCallback? onTap;
  final bool showBookmark;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final saved = ref.watch(isBookmarkedProvider(event.slug));
    final date = event.startsAt == null
        ? null
        : DateFormat('EEE, d MMM · h:mm a', 'en_IN').format(event.startsAt!.toLocal());

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.sm),
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
            padding: const EdgeInsets.all(KSpace.md),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(
                  width: 68,
                  height: 68,
                  child: EventHero(
                    seed: event.slug,
                    category: event.categoryName,
                    imageUrl: event.bannerUrl,
                    borderRadius: BorderRadius.circular(KRadius.md),
                  ),
                ),
                const SizedBox(width: KSpace.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (event.categoryName != null)
                            Expanded(
                              child: Text(event.categoryName!.toUpperCase(),
                                  style: TextStyle(
                                      color: c.accent, fontSize: 10.5, fontWeight: FontWeight.w800, letterSpacing: 0.5),
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis),
                            )
                          else
                            const Spacer(),
                          if (showBookmark)
                            _BookmarkButton(event: event, saved: saved),
                        ],
                      ),
                      const SizedBox(height: 2),
                      Text(event.title,
                          style: TextStyle(color: c.text, fontSize: 15.5, fontWeight: FontWeight.w700, height: 1.2),
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis),
                      const SizedBox(height: KSpace.sm),
                      if (date != null) _MetaRow(icon: Icons.schedule_rounded, text: date),
                      if (event.location != null) ...[
                        const SizedBox(height: 3),
                        _MetaRow(icon: Icons.location_on_outlined, text: event.location!),
                      ],
                      const SizedBox(height: KSpace.sm),
                      Text(_priceLabel(event),
                          style: TextStyle(color: c.text, fontSize: 13.5, fontWeight: FontWeight.w800)),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

String _priceLabel(EventSummary e) {
  if (e.priceFromPaise == null) return 'View details';
  if (e.isFree) return 'Free entry';
  return 'From ${Money.fromMinor(e.priceFromPaise!)}';
}

class _BookmarkButton extends ConsumerWidget {
  const _BookmarkButton({required this.event, required this.saved});

  final EventSummary event;
  final bool saved;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return InkResponse(
      onTap: () => ref.read(bookmarksControllerProvider.notifier).toggle(event),
      radius: 22,
      child: Padding(
        padding: const EdgeInsets.only(left: KSpace.sm),
        child: Icon(
          saved ? Icons.bookmark_rounded : Icons.bookmark_border_rounded,
          size: 20,
          color: saved ? c.accent : c.muted,
          semanticLabel: saved ? 'Remove bookmark' : 'Bookmark',
        ),
      ),
    );
  }
}

class _MetaRow extends StatelessWidget {
  const _MetaRow({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Row(
      children: [
        Icon(icon, size: 14, color: c.muted),
        const SizedBox(width: KSpace.xs + 2),
        Expanded(
          child: Text(text, style: TextStyle(color: c.muted, fontSize: 12.5), maxLines: 1, overflow: TextOverflow.ellipsis),
        ),
      ],
    );
  }
}
