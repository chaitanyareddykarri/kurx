import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../../../core/theme/motion.dart';
import '../../../../core/utils/money.dart';
import '../../domain/entities/event_summary.dart';
import 'event_visuals.dart';

/// A swipeable featured carousel with peeking neighbours and page dots.
class FeaturedCarousel extends StatefulWidget {
  const FeaturedCarousel({super.key, required this.events, required this.onTap});

  final List<EventSummary> events;
  final void Function(EventSummary event) onTap;

  @override
  State<FeaturedCarousel> createState() => _FeaturedCarouselState();
}

class _FeaturedCarouselState extends State<FeaturedCarousel> {
  final _controller = PageController(viewportFraction: 0.9);
  int _page = 0;

  @override
  void initState() {
    super.initState();
    _controller.addListener(() {
      final p = _controller.page?.round() ?? 0;
      if (p != _page) setState(() => _page = p);
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Column(
      children: [
        SizedBox(
          height: 196,
          child: PageView.builder(
            controller: _controller,
            itemCount: widget.events.length,
            itemBuilder: (context, i) =>
                _FeaturedCard(event: widget.events[i], onTap: () => widget.onTap(widget.events[i])),
          ),
        ),
        if (widget.events.length > 1) ...[
          const SizedBox(height: KSpace.md),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              for (var i = 0; i < widget.events.length; i++)
                AnimatedContainer(
                  duration: context.motion(KMotion.fast),
                  margin: const EdgeInsets.symmetric(horizontal: 3),
                  height: 6,
                  width: i == _page ? 18 : 6,
                  decoration: BoxDecoration(
                    color: i == _page ? c.accent : c.border,
                    borderRadius: BorderRadius.circular(KRadius.pill),
                  ),
                ),
            ],
          ),
        ],
      ],
    );
  }
}

class _FeaturedCard extends StatelessWidget {
  const _FeaturedCard({required this.event, required this.onTap});

  final EventSummary event;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final date = event.startsAt == null
        ? null
        : DateFormat('EEE, d MMM · h:mm a', 'en_IN').format(event.startsAt!.toLocal());

    return Semantics(
      button: true,
      label: 'Featured event: ${event.title}',
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 6),
        child: Material(
          borderRadius: BorderRadius.circular(KRadius.lg),
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: onTap,
            child: EventHero(
              seed: event.slug,
              category: event.categoryName,
              overlay: Container(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [Colors.transparent, Colors.black.withValues(alpha: 0.55)],
                  ),
                ),
                padding: const EdgeInsets.all(KSpace.lg),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    if (event.categoryName != null)
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: 3),
                        decoration: BoxDecoration(
                          color: Colors.white.withValues(alpha: 0.22),
                          borderRadius: BorderRadius.circular(KRadius.pill),
                        ),
                        child: Text(event.categoryName!,
                            style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700)),
                      ),
                    const Spacer(),
                    Text(event.title,
                        style: const TextStyle(color: Colors.white, fontSize: 19, fontWeight: FontWeight.w800, height: 1.15),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis),
                    const SizedBox(height: KSpace.xs),
                    Row(
                      children: [
                        if (date != null)
                          Expanded(
                            child: Text(date,
                                style: TextStyle(color: Colors.white.withValues(alpha: 0.92), fontSize: 12.5),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis),
                          ),
                        Text(_priceLabel(event),
                            style: const TextStyle(color: Colors.white, fontSize: 13, fontWeight: FontWeight.w800)),
                      ],
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
  if (e.priceFromPaise == null) return '';
  if (e.isFree) return 'Free';
  return 'From ${Money.fromMinor(e.priceFromPaise!)}';
}
