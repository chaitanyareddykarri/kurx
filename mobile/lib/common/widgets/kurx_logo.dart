import 'package:flutter/material.dart';

import '../../core/theme/design_tokens.dart';

/// The Kurx wordmark: a coral "K" spark monogram tile + optional wordmark.
/// Mirrors `web/components/brand/logo.tsx` so the brand reads the same on both
/// surfaces. Set [compact] to render the tile alone (e.g. collapsed nav).
class KurxLogo extends StatelessWidget {
  const KurxLogo({super.key, this.compact = false, this.size = 28});

  final bool compact;
  final double size;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        _SparkTile(size: size),
        if (!compact) ...[
          SizedBox(width: size * 0.29),
          Text(
            'Kurx',
            style: TextStyle(
              color: c.text,
              fontSize: size * 0.62,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.5,
            ),
          ),
        ],
      ],
    );
  }
}

class _SparkTile extends StatelessWidget {
  const _SparkTile({required this.size});

  final double size;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      height: size,
      width: size,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [c.accent, Color.lerp(c.accent, Colors.black, 0.25)!],
        ),
        borderRadius: BorderRadius.circular(size * 0.28),
        boxShadow: [
          BoxShadow(color: c.accent.withValues(alpha: 0.35), blurRadius: size * 0.4, offset: Offset(0, size * 0.12)),
        ],
      ),
      alignment: Alignment.center,
      child: Text(
        'K',
        style: TextStyle(
          color: c.onAccent,
          fontSize: size * 0.6,
          fontWeight: FontWeight.w900,
          height: 1,
        ),
      ),
    );
  }
}
