import 'package:flutter/material.dart';

import '../../../../common/widgets/shimmer.dart';

/// Skeleton placeholder for the discovery home while the feed loads.
class HomeSkeleton extends StatelessWidget {
  const HomeSkeleton({super.key});

  @override
  Widget build(BuildContext context) {
    return ListView(
      physics: const NeverScrollableScrollPhysics(),
      children: [
        const SizedBox(height: 12),
        const ShimmerBox(height: 48, margin: EdgeInsets.symmetric(horizontal: 16), radius: 14),
        const SizedBox(height: 16),
        const ShimmerBox(height: 170, margin: EdgeInsets.symmetric(horizontal: 16), radius: 16),
        const SizedBox(height: 20),
        for (var s = 0; s < 2; s++) ...[
          const ShimmerBox(height: 22, width: 140, margin: EdgeInsets.symmetric(horizontal: 16)),
          const SizedBox(height: 12),
          SizedBox(
            height: 172,
            child: ListView(
              scrollDirection: Axis.horizontal,
              physics: const NeverScrollableScrollPhysics(),
              padding: const EdgeInsets.symmetric(horizontal: 10),
              children: const [
                ShimmerBox(height: 160, width: 224, margin: EdgeInsets.symmetric(horizontal: 6), radius: 16),
                ShimmerBox(height: 160, width: 224, margin: EdgeInsets.symmetric(horizontal: 6), radius: 16),
                ShimmerBox(height: 160, width: 224, margin: EdgeInsets.symmetric(horizontal: 6), radius: 16),
              ],
            ),
          ),
          const SizedBox(height: 20),
        ],
      ],
    );
  }
}
