import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../common/widgets/kurx_card.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/public_profile_dto.dart';

/// About — Phase 2. The Flutter twin of `web/components/profile/about-panel.tsx`.
///
/// **The panel's organising rule is provenance, not topic.** Bio, education, skills, languages,
/// interests and links are things the person *says*; experience is the one block here the platform
/// can *prove*, because an assignment only exists once an organizer created it and the person
/// accepted it. Each block states which it is, for the same reason the header keeps the derived
/// headline apart from the self-declared one (D-225) — a claim must never inherit a proof's
/// credibility by sitting beside it.
///
/// Education is marked self-declared deliberately (D-220): it was never migrated into
/// evidence-backed membership claims, so it is a claim like any other.
///
/// **Interests are declared; Event DNA is derived and is not here.** Event DNA belongs to the
/// Professional Journey with the rest of the derived material.
class AboutPanel extends StatelessWidget {
  const AboutPanel({super.key, required this.profile, this.assignments = const []});

  final PublicProfileDto profile;

  /// Accepted event assignments — verified experience, not a self-declared job history.
  final List<ProfileAssignmentDto> assignments;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final college = profile.college;
    final collegeParts = <String>[
      if ((college?.degree ?? '').trim().isNotEmpty) college!.degree!,
      if ((college?.branch ?? '').trim().isNotEmpty) college!.branch!,
      if ((college?.institute ?? '').trim().isNotEmpty) college!.institute!,
    ];
    final links = _parseLinks(profile.links);
    final hasBio = (profile.bio ?? '').trim().isNotEmpty;

    final hasAnything = hasBio ||
        collegeParts.isNotEmpty ||
        profile.skills.isNotEmpty ||
        profile.languages.isNotEmpty ||
        profile.interests.isNotEmpty ||
        assignments.isNotEmpty ||
        links.isNotEmpty;

    // An empty About card would be a heading over nothing. Absence renders as absence.
    if (!hasAnything) return const SizedBox.shrink();

    return KurxCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('About', style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w700)),
          if (hasBio)
            _Group(
              title: 'Bio',
              icon: Icons.auto_awesome_outlined,
              derived: false,
              child: Text(
                profile.bio!,
                style: TextStyle(color: c.muted, fontSize: 14, height: 1.55),
              ),
            ),
          if (assignments.isNotEmpty)
            _Group(
              title: 'Experience',
              icon: Icons.work_outline_rounded,
              derived: true,
              child: Column(
                children: [
                  for (final a in assignments)
                    Container(
                      margin: const EdgeInsets.only(bottom: KSpace.sm),
                      padding: const EdgeInsets.symmetric(
                        horizontal: KSpace.md,
                        vertical: KSpace.sm,
                      ),
                      decoration: BoxDecoration(
                        color: c.elevated,
                        borderRadius: BorderRadius.circular(KRadius.md),
                        border: Border.all(color: c.border),
                      ),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  a.role,
                                  style: TextStyle(
                                    color: c.text,
                                    fontSize: 14,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                                const SizedBox(height: 2),
                                Text(
                                  a.eventTitle,
                                  overflow: TextOverflow.ellipsis,
                                  style: TextStyle(color: c.muted, fontSize: 12.5),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(width: KSpace.sm),
                          Text(
                            '${a.startsAt.year}',
                            style: TextStyle(color: c.muted, fontSize: 12.5),
                          ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          if (collegeParts.isNotEmpty)
            _Group(
              title: 'Education',
              icon: Icons.school_outlined,
              derived: false,
              child: _Pills(items: collegeParts),
            ),
          if (profile.skills.isNotEmpty)
            _Group(
              title: 'Skills',
              icon: Icons.auto_awesome_outlined,
              derived: false,
              child: _Pills(items: profile.skills),
            ),
          if (profile.languages.isNotEmpty)
            _Group(
              title: 'Languages',
              icon: Icons.translate_rounded,
              derived: false,
              child: _Pills(items: profile.languages),
            ),
          if (profile.interests.isNotEmpty)
            _Group(
              title: 'Interests',
              icon: Icons.favorite_outline_rounded,
              derived: false,
              child: _Pills(items: profile.interests),
            ),
          if (links.isNotEmpty)
            _Group(
              title: 'Links',
              icon: Icons.link_rounded,
              derived: false,
              child: Wrap(
                spacing: KSpace.sm,
                runSpacing: KSpace.sm,
                children: [
                  for (final entry in links.entries)
                    ActionChip(
                      avatar: Icon(_linkIcon(entry.key), size: 15, color: c.accentText),
                      label: Text(entry.key),
                      onPressed: () => _open(entry.value),
                    ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// `links` is a jsonb object of slot → URL. A malformed value yields no links rather than throwing
/// the panel away — decoration must never take a profile down.
Map<String, String> _parseLinks(String? linksJson) {
  if (linksJson == null || linksJson.trim().isEmpty) return const {};
  try {
    final decoded = jsonDecode(linksJson);
    if (decoded is! Map) return const {};
    return {
      for (final e in decoded.entries)
        if (e.value is String && (e.value as String).trim().isNotEmpty)
          e.key.toString(): (e.value as String).trim(),
    };
  } catch (_) {
    return const {};
  }
}

IconData _linkIcon(String slot) => switch (slot.toLowerCase()) {
      'github' => Icons.code_rounded,
      'linkedin' => Icons.business_center_outlined,
      'instagram' => Icons.camera_alt_outlined,
      _ => Icons.public_rounded,
    };

Future<void> _open(String url) async {
  final uri = Uri.tryParse(url);
  if (uri == null) return;
  await launchUrl(uri, mode: LaunchMode.externalApplication);
}

class _Group extends StatelessWidget {
  const _Group({
    required this.title,
    required this.icon,
    required this.derived,
    required this.child,
  });

  final String title;
  final IconData icon;

  /// Derived content is marked in the attesting colour; self-declared stays muted, so the two are
  /// distinguishable at a glance rather than only by reading the label.
  final bool derived;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final tone = derived ? c.teal : c.muted;

    return Padding(
      padding: const EdgeInsets.only(top: KSpace.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 16, color: c.muted),
              const SizedBox(width: KSpace.sm),
              Text(title, style: TextStyle(color: c.text, fontSize: 14, fontWeight: FontWeight.w600)),
              const SizedBox(width: KSpace.sm),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: 2),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(KRadius.pill),
                  border: Border.all(color: tone.withValues(alpha: 0.35)),
                ),
                child: Text(
                  derived ? 'Derived' : 'Self-declared',
                  style: TextStyle(color: tone, fontSize: 10, fontWeight: FontWeight.w700),
                ),
              ),
            ],
          ),
          const SizedBox(height: KSpace.md),
          child,
        ],
      ),
    );
  }
}

class _Pills extends StatelessWidget {
  const _Pills({required this.items});

  final List<String> items;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Wrap(
      spacing: KSpace.sm,
      runSpacing: KSpace.sm,
      children: [
        for (final item in items)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: 6),
            decoration: BoxDecoration(
              color: c.elevated,
              borderRadius: BorderRadius.circular(KRadius.pill),
              border: Border.all(color: c.border),
            ),
            child: Text(item, style: TextStyle(color: c.muted, fontSize: 13)),
          ),
      ],
    );
  }
}
