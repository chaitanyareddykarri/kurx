import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../../../core/theme/motion.dart';

/// The profile's loading state — the Flutter twin of `web/app/u/[username]/loading.tsx`.
///
/// **Shaped like the header it replaces, not a spinner.** The screen fans out to a dozen independent
/// section reads, so the wait is real. A skeleton matching the final geometry — cover band,
/// overlapping avatar, name, two headlines, stat row — means the layout does not jump when data
/// lands, which is the cost a spinner leaves unpaid.
///
/// The shapes are hidden from screen readers and a single polite announcement carries the wait:
/// reading out eleven empty boxes is worse than silence.
class ProfileSkeleton extends StatefulWidget {
  const ProfileSkeleton({super.key});

  @override
  State<ProfileSkeleton> createState() => _ProfileSkeletonState();
}

class _ProfileSkeletonState extends State<ProfileSkeleton>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1100),
    );
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Same rule, and the same implementation, as ShimmerBox: a skeleton pulses until its data
    // arrives, which on a slow connection is unbounded — the automatically-starting motion over
    // five seconds WCAG 2.2.2 asks to be stoppable. Under reduced motion it holds at mid-pulse
    // rather than running faster.
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
    return Semantics(
      label: 'Loading profile',
      liveRegion: true,
      child: ExcludeSemantics(
        child: AnimatedBuilder(
          animation: _controller,
          builder: (context, _) {
            // A single opacity drive for every shape: one animation, not one per box, so the pulse
            // reads as one surface breathing rather than a grid of independent flickers.
            final t = 0.45 + (_controller.value * 0.25);
            return _SkeletonBody(opacity: t);
          },
        ),
      ),
    );
  }
}

class _SkeletonBody extends StatelessWidget {
  const _SkeletonBody({required this.opacity});

  final double opacity;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    Widget box({double? width, double height = 16, double radius = KRadius.sm}) => Opacity(
          opacity: opacity,
          child: Container(
            width: width,
            height: height,
            decoration: BoxDecoration(
              color: c.elevated,
              borderRadius: BorderRadius.circular(radius),
            ),
          ),
        );

    return ListView(
      padding: const EdgeInsets.only(bottom: KSpace.xl),
      children: [
        // Cover + avatar, at the same aspect ratio and offset the real header uses.
        Stack(
          clipBehavior: Clip.none,
          children: [
            AspectRatio(aspectRatio: 4 / 1.6, child: box(radius: 0)),
            Positioned(
              left: KSpace.lg,
              bottom: -44,
              child: Container(
                padding: const EdgeInsets.all(4),
                decoration: BoxDecoration(color: c.background, shape: BoxShape.circle),
                child: box(width: 96, height: 96, radius: KRadius.pill),
              ),
            ),
          ],
        ),
        const SizedBox(height: 52),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              box(width: 200, height: 26),
              const SizedBox(height: KSpace.sm),
              box(width: 120, height: 14),
              const SizedBox(height: KSpace.lg),
              box(width: double.infinity, height: 14),
              const SizedBox(height: KSpace.sm),
              box(width: 240, height: 14),
              const SizedBox(height: KSpace.xl),
              // Stat row.
              Row(
                children: [
                  for (var i = 0; i < 3; i++) ...[
                    Expanded(child: box(height: 64, radius: KRadius.md)),
                    if (i < 2) const SizedBox(width: KSpace.sm),
                  ],
                ],
              ),
              const SizedBox(height: KSpace.xl),
              box(height: 140, radius: KRadius.lg),
              const SizedBox(height: KSpace.lg),
              box(height: 200, radius: KRadius.lg),
            ],
          ),
        ),
      ],
    );
  }
}
