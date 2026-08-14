import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../data/models/public_profile_dto.dart';

/// The Professional Journey (D-223) — Kurx's signature profile section, mirroring the web
/// `JourneyRail` one-for-one.
///
/// Renders in the order the API returns, which is strictly chronological by first attainment. A fixed
/// Participant→…→Mentor ladder would assert a progression the data does not support: people organize
/// before volunteering, and an unusual-but-real career must not read as incomplete.
class JourneyRail extends StatelessWidget {
  const JourneyRail({super.key, required this.nodes});

  final List<JourneyNodeDto> nodes;

  /// Tier → (label, blurb). An unknown tier (newer backend, older client) renders its raw key rather
  /// than being dropped, so a new tier is visible rather than silently missing.
  static const _tiers = <String, (String, String)>{
    'attendee': ('Attendee', 'First verified check-in'),
    'participant': ('Participant', 'First event taken part in'),
    'volunteer': ('Volunteer', 'First time serving an event'),
    'team_lead': ('Team Lead', 'First time leading a team'),
    'competition_winner': ('Competition Winner', 'First published first place'),
    'speaker': ('Speaker', 'First talk given'),
    'judge': ('Judge', 'First time judging'),
    'mentor': ('Mentor', 'First time mentoring'),
    'organizer': ('Organizer', 'First event run'),
    'host': ('Host', 'First event hosted'),
    'verified_member': ('Verified Member', 'First verified affiliation'),
  };

  static const _months = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
  ];

  static String _formatDate(DateTime d) => '${_months[d.month - 1]} ${d.year}';

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // An empty journey is the honest output for someone with no verified activity — an aspirational
    // placeholder ladder would be exactly the fabricated progress this system exists to avoid.
    if (nodes.isEmpty) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Professional Journey',
              style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w800)),
          const SizedBox(height: 2),
          Text(
            'The first time each milestone was reached, in order.',
            style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.3),
          ),
          const SizedBox(height: KSpace.md),
          for (var i = 0; i < nodes.length; i++) _node(context, nodes[i], isLast: i == nodes.length - 1),
        ],
      ),
    );
  }

  Widget _node(BuildContext context, JourneyNodeDto node, {required bool isLast}) {
    final c = context.kurx;
    final tier = _tiers[node.tier];
    final label = tier?.$1 ?? node.tier;
    final blurb = tier?.$2 ?? '';
    final evidence = node.evidence.detail ?? node.evidence.eventTitle ?? node.evidence.orgName;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // The rail: dot plus a connector to the next node, so the sequence reads as one path.
          Column(
            children: [
              Container(
                margin: const EdgeInsets.only(top: 4),
                height: 14,
                width: 14,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: c.cardSurface,
                  border: Border.all(color: c.accent, width: 2),
                ),
              ),
              if (!isLast) Expanded(child: Container(width: 1, color: c.border)),
            ],
          ),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(bottom: isLast ? 0 : KSpace.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.baseline,
                    textBaseline: TextBaseline.alphabetic,
                    children: [
                      Flexible(
                        child: Text(label,
                            style: TextStyle(color: c.text, fontSize: 15, fontWeight: FontWeight.w700)),
                      ),
                      const SizedBox(width: KSpace.sm),
                      Text(_formatDate(node.firstAttainedAt),
                          style: TextStyle(color: c.muted, fontSize: 12)),
                      if (node.occurrences > 1) ...[
                        const SizedBox(width: KSpace.sm),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                          decoration: BoxDecoration(
                            border: Border.all(color: c.border),
                            borderRadius: BorderRadius.circular(KRadius.pill),
                          ),
                          child: Text('×${node.occurrences}',
                              style: TextStyle(color: c.muted, fontSize: 11)),
                        ),
                      ],
                    ],
                  ),
                  if (blurb.isNotEmpty)
                    Text(blurb, style: TextStyle(color: c.muted, fontSize: 12.5)),
                  if (evidence != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 2),
                      child: Text(evidence, style: TextStyle(color: c.muted, fontSize: 12.5)),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
