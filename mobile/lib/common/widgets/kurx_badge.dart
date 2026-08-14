import 'package:flutter/material.dart';

import '../../core/theme/design_tokens.dart';

/// `teal` attests — provenance, verification, certificates — and is never generic success (D-286).
/// The token has existed in [KurxColors] since that decision; the badge simply had no case for it,
/// so every verification pill on this surface fell back to `accent` while web rendered teal. Same
/// meaning, two colours, one design system.
enum KurxBadgeTone { neutral, accent, teal, success, warning, danger, muted }

/// A small status pill (event state, role, count). Tone drives the color; the
/// fill is a soft tint of that tone so many badges can sit together calmly.
class KurxBadge extends StatelessWidget {
  const KurxBadge({super.key, required this.label, this.tone = KurxBadgeTone.neutral, this.icon});

  final String label;
  final KurxBadgeTone tone;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final color = switch (tone) {
      KurxBadgeTone.neutral => c.text,
      KurxBadgeTone.accent => c.accent,
      KurxBadgeTone.teal => c.teal,
      KurxBadgeTone.success => c.success,
      KurxBadgeTone.warning => c.warning,
      KurxBadgeTone.danger => c.danger,
      KurxBadgeTone.muted => c.muted,
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(KRadius.pill),
        border: Border.all(color: color.withValues(alpha: 0.35)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[Icon(icon, size: 12, color: color), const SizedBox(width: KSpace.xs)],
          Text(label, style: TextStyle(color: color, fontSize: 11.5, fontWeight: FontWeight.w700, letterSpacing: 0.1)),
        ],
      ),
    );
  }
}

/// Host trust tiers (D-015 world): Tier 1 new · Tier 2 verified · Tier 3 power.
/// A visual-only badge — no payout logic. Reused in organizer & admin UI.
enum HostTier { tier1, tier2, tier3 }

class HostTierBadge extends StatelessWidget {
  const HostTierBadge({super.key, required this.tier});

  final HostTier tier;

  @override
  Widget build(BuildContext context) {
    final (label, tone, icon) = switch (tier) {
      HostTier.tier1 => ('New host', KurxBadgeTone.muted, Icons.spa_outlined),
      HostTier.tier2 => ('Verified', KurxBadgeTone.accent, Icons.verified_outlined),
      HostTier.tier3 => ('Power host', KurxBadgeTone.success, Icons.workspace_premium_outlined),
    };
    return KurxBadge(label: label, tone: tone, icon: icon);
  }
}
