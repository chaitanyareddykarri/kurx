import 'package:flutter/material.dart';

import '../../core/theme/motion.dart';

/// A single pulsing placeholder block for skeleton loaders. Reused by every
/// skeleton in the app so loading looks consistent.
class ShimmerBox extends StatefulWidget {
  const ShimmerBox({
    super.key,
    required this.height,
    this.width,
    this.margin = EdgeInsets.zero,
    this.radius = 12,
  });

  final double height;
  final double? width;
  final EdgeInsets margin;
  final double radius;

  @override
  State<ShimmerBox> createState() => _ShimmerBoxState();
}

class _ShimmerBoxState extends State<ShimmerBox> with SingleTickerProviderStateMixin {
  late final AnimationController _controller =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 900));

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // A skeleton pulses until its data arrives, which on a slow connection is unbounded — precisely
    // the automatically-starting motion lasting over five seconds that WCAG 2.2.2 asks to be
    // stoppable, and it is on screen during every load in the app. Under reduced motion the block
    // holds still at mid-pulse rather than going faster.
    if (context.reduceMotion) {
      _controller.stop();
      _controller.value = 0.5;
    } else if (!_controller.isAnimating) {
      _controller.repeat(reverse: true);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final base = Theme.of(context).colorScheme.onSurface;
    return FadeTransition(
      opacity: Tween<double>(begin: 0.06, end: 0.16).animate(_controller),
      child: Container(
        height: widget.height,
        width: widget.width,
        margin: widget.margin,
        decoration: BoxDecoration(color: base, borderRadius: BorderRadius.circular(widget.radius)),
      ),
    );
  }
}

/// A vertical list skeleton for card lists (search results, section lists).
class ListSkeleton extends StatelessWidget {
  const ListSkeleton({super.key, this.rows = 6});

  final int rows;

  @override
  Widget build(BuildContext context) {
    return ListView.builder(
      physics: const NeverScrollableScrollPhysics(),
      padding: const EdgeInsets.symmetric(vertical: 8),
      itemCount: rows,
      itemBuilder: (_, _) => const ShimmerBox(
        height: 88,
        margin: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        radius: 16,
      ),
    );
  }
}
