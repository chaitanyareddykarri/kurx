import 'package:flutter/material.dart';

import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../common/widgets/kurx_badge.dart';
import '../../../../common/widgets/kurx_chip.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/theme/motion.dart';
import '../../data/models/public_profile_dto.dart';

/// The profile header — the Flutter twin of `web/components/profile/profile-header.tsx`.
///
/// **The photo owns the optical centre.** The cover is a backdrop at a fixed aspect ratio with a
/// scrim beneath it: without the scrim a light cover upload puts pale text on pale pixels and the
/// name disappears, so the gradient is what makes an arbitrary user image safe to set type over.
///
/// **Two headlines, never merged** (D-225). The derived line is what Kurx can prove from verified
/// activity and leads; the self-declared one follows, visibly marked. Collapsing them would let a
/// claim borrow the authority of a proof.
class ProfileHeader extends StatelessWidget {
  const ProfileHeader({
    super.key,
    required this.profile,
    required this.cities,
    required this.isOwnProfile,
    required this.onShare,
    required this.onResume,
    this.onEdit,
    this.actions = const [],
    this.completion,
  });

  final PublicProfileDto profile;

  /// Derived from where this person's events actually happened — the profile carries no location
  /// field, and this is a stronger statement than a typed one would be.
  final List<String> cities;
  final bool isOwnProfile;
  final VoidCallback onShare;
  final VoidCallback onResume;
  final VoidCallback? onEdit;
  final List<Widget> actions;

  /// Owner-only. Null for every other viewer, which is what keeps completion private — publishing
  /// "this profile is 40% complete" would be a statement about someone derived from choices they
  /// are entitled to make (D-221 keeps the public profile to positive, verified signals).
  final ProfileCompletion? completion;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final username = profile.username ?? '';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Cover + avatar overlap. `clipBehavior: none` is required — the avatar deliberately
        // escapes the stack's bounds and would otherwise be cut in half.
        Stack(
          clipBehavior: Clip.none,
          children: [
            AspectRatio(
              aspectRatio: 4 / 1.6,
              child: DecoratedBox(
                decoration: BoxDecoration(color: c.elevated),
                child: profile.coverKey != null && profile.coverKey!.isNotEmpty
                    ? Image.network(
                        profile.coverKey!,
                        fit: BoxFit.cover,
                        // A broken cover must never take the header down with it; the plain
                        // surface is the same thing an absent cover renders.
                        errorBuilder: (_, _, _) => ColoredBox(color: c.elevated),
                      )
                    : DecoratedBox(
                        decoration: BoxDecoration(
                          gradient: LinearGradient(
                            begin: Alignment.topLeft,
                            end: Alignment.bottomRight,
                            colors: [c.elevated, c.cardSurface, c.elevated],
                          ),
                        ),
                      ),
              ),
            ),
            Positioned.fill(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.bottomCenter,
                    end: Alignment.topCenter,
                    colors: [
                      c.background,
                      c.background.withValues(alpha: 0.15),
                      Colors.transparent,
                    ],
                  ),
                ),
              ),
            ),
            Positioned(
              left: KSpace.lg,
              bottom: -44,
              // The ring is what separates the avatar from an arbitrary cover photo behind it.
              child: Container(
                padding: const EdgeInsets.all(4),
                decoration: BoxDecoration(color: c.background, shape: BoxShape.circle),
                child: KurxAvatar(name: profile.name, imageUrl: profile.avatarKey, size: 96),
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
              Wrap(
                spacing: KSpace.sm,
                runSpacing: KSpace.sm,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(
                    profile.name,
                    style: TextStyle(
                      color: c.text,
                      fontSize: 26,
                      fontWeight: FontWeight.w700,
                      letterSpacing: -0.4,
                    ),
                  ),
                  // Teal, not accent: verification attests (D-286).
                  if (profile.verification.identityVerified)
                    const KurxBadge(
                      label: 'Identity verified',
                      tone: KurxBadgeTone.teal,
                      icon: Icons.verified_user_outlined,
                    ),
                  if (profile.verification.verifiedMember)
                    const KurxBadge(label: 'Verified member', tone: KurxBadgeTone.teal),
                ],
              ),
              if (username.isNotEmpty) ...[
                const SizedBox(height: KSpace.xs),
                Text('@$username', style: TextStyle(color: c.muted, fontSize: 14)),
              ],

              if (profile.derivedHeadline.isNotEmpty) ...[
                const SizedBox(height: KSpace.md),
                _Provenance(
                  label: 'Derived',
                  tone: c.teal,
                  child: Text(
                    profile.derivedHeadline,
                    style: TextStyle(color: c.text, fontSize: 16, fontWeight: FontWeight.w600),
                  ),
                ),
              ],
              if ((profile.headline ?? '').isNotEmpty) ...[
                const SizedBox(height: KSpace.xs),
                _Provenance(
                  label: 'Self-declared',
                  tone: c.muted,
                  child: Text(profile.headline!, style: TextStyle(color: c.muted, fontSize: 14)),
                ),
              ],

              if (cities.isNotEmpty) ...[
                const SizedBox(height: KSpace.md),
                Row(
                  children: [
                    Icon(Icons.place_outlined, size: 15, color: c.muted),
                    const SizedBox(width: KSpace.xs),
                    Expanded(
                      child: Text(
                        // "Active in", not "Lives in": this is where their events happened, which
                        // is a fact the platform holds — a home address is not.
                        'Active in ${cities.take(3).join(' · ')}',
                        style: TextStyle(color: c.muted, fontSize: 13),
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
              ],

              if (profile.identityLabels.isNotEmpty) ...[
                const SizedBox(height: KSpace.md),
                Wrap(
                  spacing: KSpace.sm,
                  runSpacing: KSpace.sm,
                  children: [for (final l in profile.identityLabels) KurxChip(label: l)],
                ),
              ],

              if (profile.summary.isNotEmpty) ...[
                const SizedBox(height: KSpace.md),
                Text(profile.summary, style: TextStyle(color: c.muted, fontSize: 14, height: 1.5)),
              ],

              if (completion != null && completion!.percent < 100) ...[
                const SizedBox(height: KSpace.lg),
                _CompletionCard(completion: completion!),
              ],

              const SizedBox(height: KSpace.lg),
              Wrap(
                spacing: KSpace.sm,
                runSpacing: KSpace.sm,
                children: [
                  if (isOwnProfile && onEdit != null)
                    _HeaderAction(icon: Icons.edit_outlined, label: 'Edit profile', onTap: onEdit!),
                  _HeaderAction(icon: Icons.ios_share_outlined, label: 'Share', onTap: onShare),
                  _HeaderAction(
                    icon: Icons.download_outlined,
                    label: 'Résumé',
                    onTap: onResume,
                  ),
                  ...actions,
                ],
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// A line of profile text with its provenance stated beside it, so proof and claim are never
/// confused for one another (D-225).
class _Provenance extends StatelessWidget {
  const _Provenance({required this.label, required this.tone, required this.child});

  final String label;
  final Color tone;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: KSpace.sm,
      runSpacing: KSpace.xs,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        child,
        Container(
          padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: 2),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(KRadius.pill),
            border: Border.all(color: tone.withValues(alpha: 0.35)),
          ),
          child: Text(
            label,
            style: TextStyle(color: tone, fontSize: 10, fontWeight: FontWeight.w700),
          ),
        ),
      ],
    );
  }
}

class _HeaderAction extends StatelessWidget {
  const _HeaderAction({required this.icon, required this.label, required this.onTap});

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(KRadius.md),
        child: Container(
          // 44px floor — the platform touch-target minimum, met by the container rather than
          // assumed from the icon's own size.
          constraints: const BoxConstraints(minHeight: 44),
          padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(KRadius.md),
            border: Border.all(color: c.border),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 16, color: c.text),
              const SizedBox(width: KSpace.sm),
              Text(
                label,
                style: TextStyle(color: c.text, fontSize: 13.5, fontWeight: FontWeight.w600),
              ),
            ],
          ),
        ),
      ),
    );
  }
}


/// Profile completion — computed on the client from fields the profile already carries.
///
/// Deliberately not a backend field: it is a nudge to the owner about their own profile, needs no
/// server truth, and must never be visible to anyone else. Mirrors
/// `web/components/profile/profile-completion.ts` step for step so the two surfaces never disagree
/// about how complete the same profile is.
class ProfileCompletion {
  ProfileCompletion({
    required bool hasAvatar,
    required bool hasCover,
    required bool hasHeadline,
    required bool hasBio,
    required bool hasSkills,
    required bool hasLanguages,
    required bool hasInterests,
    required bool hasLinks,
    required bool hasEducation,
    required bool emailVerified,
    required bool identityVerified,
  }) : steps = [
          (label: 'Add a profile photo', done: hasAvatar),
          (label: 'Add a cover image', done: hasCover),
          (label: 'Write a headline', done: hasHeadline),
          (label: 'Write a short bio', done: hasBio),
          (label: 'List your skills', done: hasSkills),
          (label: 'Add the languages you speak', done: hasLanguages),
          (label: 'Add your interests', done: hasInterests),
          (label: 'Link your work', done: hasLinks),
          (label: 'Add your education', done: hasEducation),
          (label: 'Verify your email', done: emailVerified),
          (label: 'Verify your identity', done: identityVerified),
        ];

  final List<({String label, bool done})> steps;

  int get percent =>
      steps.isEmpty ? 100 : ((steps.where((s) => s.done).length / steps.length) * 100).round();

  /// The first unfinished step, so the card names one concrete next action rather than a checklist.
  ({String label, bool done})? get next =>
      steps.where((s) => !s.done).firstOrNull;
}

class _CompletionCard extends StatelessWidget {
  const _CompletionCard({required this.completion});

  final ProfileCompletion completion;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final percent = completion.percent;

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.elevated,
        borderRadius: BorderRadius.circular(KRadius.md),
        border: Border.all(color: c.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Profile strength',
                  style: TextStyle(color: c.text, fontSize: 14, fontWeight: FontWeight.w700),
                ),
              ),
              Text(
                '$percent%',
                style: TextStyle(color: c.accentText, fontSize: 14, fontWeight: FontWeight.w700),
              ),
            ],
          ),
          const SizedBox(height: KSpace.md),
          Semantics(
            value: '$percent percent complete',
            child: ClipRRect(
              borderRadius: BorderRadius.circular(KRadius.pill),
              child: TweenAnimationBuilder<double>(
                // Animates on first paint so the bar reads as a measurement being taken rather
                // than a static figure. Duration is the shared token, not a local number.
                tween: Tween(begin: 0, end: percent / 100),
                // Through context.motion, not the bare token: under reduced motion the bar must
                // arrive at its value rather than travel to it.
                duration: context.motion(KMotion.slow),
                curve: context.motionCurve,
                builder: (context, value, _) => LinearProgressIndicator(
                  value: value,
                  minHeight: 6,
                  backgroundColor: c.cardSurface,
                  valueColor: AlwaysStoppedAnimation<Color>(c.accent),
                ),
              ),
            ),
          ),
          if (completion.next != null) ...[
            const SizedBox(height: KSpace.md),
            Text(
              'Next: ${completion.next!.label}',
              style: TextStyle(color: c.muted, fontSize: 12.5),
            ),
          ],
        ],
      ),
    );
  }
}
