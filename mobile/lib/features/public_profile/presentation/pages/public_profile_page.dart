import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../common/util/joined_month.dart';
import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../common/widgets/kurx_badge.dart';
import '../../../../common/widgets/kurx_card.dart';
import '../../../../common/widgets/kurx_chip.dart';
import '../../../../common/widgets/kurx_feedback.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/models/public_profile_dto.dart';
import '../providers/public_profile_providers.dart';
import '../../../orders/presentation/providers/attendee_providers.dart';
import '../../../settings/presentation/providers/account_providers.dart';
import '../widgets/about_panel.dart';
import '../widgets/ally_connect_button.dart';
import '../widgets/journey_rail.dart';
import '../widgets/contributions_heatmap.dart';
import '../widgets/metrics_panel.dart';
import '../widgets/profile_header.dart';
import '../widgets/profile_skeleton.dart';
import '../widgets/section_unavailable.dart';
import '../widgets/verification_section.dart';
import '../widgets/share_profile_sheet.dart';
import '../widgets/trust_panel.dart';

/// A user's verified professional identity — public profile view (D-201). NOT the own-account
/// hub (that stays `ProfilePage`); this renders someone's (possibly one's own) verified activity.
class PublicProfilePage extends ConsumerWidget {
  const PublicProfilePage({super.key, required this.username});

  final String username;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileAsync = ref.watch(publicProfileProvider(username));

    return Scaffold(
      appBar: AppBar(title: Text('@$username')),
      body: AsyncValueView<PublicProfileDto>(
        value: profileAsync,
        onRetry: () => ref.invalidate(publicProfileProvider(username)),
        // A skeleton shaped like the header, not a spinner: the screen fans out to a dozen
        // section reads, so the layout must not jump when they land.
        loading: const ProfileSkeleton(),
        data: (profile) => _ProfileBody(username: username, profile: profile),
      ),
    );
  }
}

/// Hands the resume URL to the OS browser rather than buffering the PDF in-app: the file then lands
/// through the platform's normal download path, with the server's filename and its own viewer.
Future<void> _openResume(BuildContext context, WidgetRef ref, String username) async {
  final url = ref.read(publicProfileSourceProvider).resumeUrl(username);
  final launched = await launchUrl(Uri.parse(url), mode: LaunchMode.externalApplication);
  if (!launched && context.mounted) {
    KurxFeedback.error(context, "Couldn't open the resume.");
  }
}

class _ProfileBody extends ConsumerWidget {
  const _ProfileBody({required this.username, required this.profile});

  final String username;
  final PublicProfileDto profile;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final me = ref.watch(currentUserProvider);
    final isOwnProfile = me?.id == profile.id;

    return ListView(
      padding: const EdgeInsets.symmetric(vertical: KSpace.lg),
      children: [
        // The header is its own widget so web and Flutter render the same composition from the
        // same fields — cover, overlapping avatar, both headlines with their provenance, derived
        // location, labels and the action row. It replaces an inline block that had drifted into a
        // different shape from web's.
        ContentWidth(
          child: ProfileHeader(
            profile: profile,
            // Derived from where their events happened — the profile carries no location field.
            cities: ref.watch(publicProfileMetricsProvider(username)).valueOrNull?.cities ?? const [],
            isOwnProfile: isOwnProfile,
            onShare: () => showShareProfileSheet(context, username: username, name: profile.name),
            onResume: () => _openResume(context, ref, username),
            onEdit: isOwnProfile ? () => context.push('/profile/edit') : null,
            // Owner-only, so completion stays private. `me` carries the caller's own record — the
            // public projection would omit exactly the fields completion has to measure.
            completion: isOwnProfile && me != null
                ? ProfileCompletion(
                    hasAvatar: (me.avatarKey ?? '').isNotEmpty,
                    hasCover: (me.coverKey ?? '').isNotEmpty,
                    hasHeadline: (me.headline ?? '').trim().isNotEmpty,
                    hasBio: (me.bio ?? '').trim().isNotEmpty,
                    hasSkills: me.skills.isNotEmpty,
                    hasLanguages: me.languages.isNotEmpty,
                    hasInterests: me.interests.isNotEmpty,
                    hasLinks: (me.linksJson ?? '').trim().isNotEmpty,
                    hasEducation: profile.college != null,
                    emailVerified: me.emailVerified,
                    identityVerified: profile.verification.identityVerified,
                  )
                : null,
            actions: [
              if (!isOwnProfile && me != null) ...[
                AllyConnectButton(targetUserId: profile.id, initialRelation: AllyRelation.none),
                // D-264: opening a DM is idempotent server-side, so this never has to ask whether a
                // conversation already exists.
                IconButton(
                  icon: const Icon(Icons.chat_bubble_outline),
                  tooltip: 'Message',
                  onPressed: () async {
                    try {
                      final roomId = await ref.read(accountDataSourceProvider).openDm(profile.id);
                      if (!context.mounted) return;
                      context.push('/chats/$roomId');
                    } catch (e) {
                      if (!context.mounted) return;
                      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
                    }
                  },
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: KSpace.lg),
        ContentWidth(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
            child: GridView.count(
              crossAxisCount: 3,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              mainAxisSpacing: KSpace.sm,
              crossAxisSpacing: KSpace.sm,
              childAspectRatio: 1.5,
              children: [
                // Adding two counts that may each be hidden would present a partial number as a
                // whole, so an em dash is shown unless both are visible (D-229/H2).
                KurxStatCard(
                  label: 'Events',
                  value: profile.stats.eventsConducted == null || profile.stats.participations == null
                      ? '—'
                      : '${profile.stats.eventsConducted! + profile.stats.participations!}',
                ),
                // From the canonical metric, never organizations.length (D-231): a hidden section
                // arrives as an empty list, and measuring it printed "0" — asserting the person
                // belongs to no organizations, which is a claim they never made.
                KurxStatCard(
                  label: 'Organizations',
                  value: ref
                          .watch(publicProfileMetricsProvider(username))
                          .valueOrNull
                          ?.organizations
                          ?.toString() ??
                      '—',
                ),
                KurxStatCard(label: 'Certificates', value: '${profile.stats.certificatesCount ?? "—"}'),
                KurxStatCard(label: 'Achievements', value: profile.stats.achievements?.toString() ?? '—'),
                KurxStatCard(label: 'Allies', value: profile.stats.allyCount?.toString() ?? '—'),
              ],
            ),
          ),
        ),
        const SizedBox(height: KSpace.xl),
        // Trust leads the body: "can I rely on this person" is the question a profile on an event
        // platform exists to answer, and only proved signals answer it. Positive signals only —
        // no KYC/PAN/bank state is ever public (D-221); that lives in Settings, behind auth.
        ContentWidth(child: TrustPanel(verification: profile.verification)),
        const SizedBox(height: KSpace.lg),
        // About — the self-declared half, kept visibly separate from everything derived.
        ContentWidth(
          child: AboutPanel(
            profile: profile,
            // Phase 2 Experience. A viewer not entitled to it gets no Experience block rather than
            // losing the whole panel — the section-level contract every read here follows.
            assignments:
                ref.watch(publicProfileAssignmentsProvider(username)).valueOrNull ?? const [],
          ),
        ),
        // Verification — owner only. Everything the public Trust panel deliberately withholds:
        // which components are pending, which were rejected, and what each unlocks. A component's
        // STATUS is as private as its value, so this never renders for another viewer.
        if (isOwnProfile) ...[
          ContentWidth(
            child: VerificationSection(
              identity: ref.watch(identityStatusProvider).valueOrNull,
              history: ref.watch(identityHistoryProvider).valueOrNull ?? const [],
              emailVerified: me?.emailVerified ?? false,
              // Straight from TrustService via /v1/me — never re-derived here, so this can never
              // disagree with the gate that actually refuses the payment.
              canOrganizePaid: me?.trust.canOrganizePaid ?? false,
              canReceivePayout: me?.trust.canReceivePayout ?? false,
              onVerifyEmail: () => context.push('/settings/account'),
              onVerifyIdentity: () => context.push('/profile/identity'),
            ),
          ),
          const SizedBox(height: KSpace.lg),
        ],
        const SizedBox(height: KSpace.lg),
        // The Journey leads the body: identity before activity. It renders nothing at all when the
        // person has no verified activity, or when the request fails — never a placeholder ladder.
        ContentWidth(
          child: ref.watch(publicProfileJourneyProvider(username)).maybeWhen(
                data: (nodes) => JourneyRail(nodes: nodes),
                orElse: () => const SizedBox.shrink(),
              ),
        ),
        // Metrics/Experience: each section is independently gated. A viewer who may not see one gets
        // nothing (403 is a normal answer), but a section that failed for any other reason now says
        // so rather than rendering as absence — `.valueOrNull` could not tell the two apart (D-235).
        ContentWidth(
          child: SectionPanel<ProfileMetricsDto>(
            value: ref.watch(publicProfileMetricsProvider(username)),
            label: 'Metrics',
            onData: (metrics) => MetricsPanel(
              metrics: metrics,
              experience: ref.watch(publicProfileExperienceProvider(username)).valueOrNull,
            ),
          ),
        ),
        ContentWidth(
          child: SectionPanel<ContributionsDto>(
            value: ref.watch(publicProfileContributionsProvider(username)),
            label: 'The contributions graph',
            onData: (contributions) => ContributionsHeatmap(contributions: contributions),
          ),
        ),
        // Posts open on their own screen rather than inline: the feed is paged and every card is
        // interactive (like, save, share), so embedding it inside this scroll would nest two
        // independently-paging lists.
        _Section(
          title: 'Posts',
          child: ListTile(
            contentPadding: EdgeInsets.zero,
            leading: Icon(Icons.article_outlined, color: context.kurx.accent),
            title: const Text('View posts'),
            trailing: Icon(Icons.chevron_right_rounded, color: context.kurx.muted),
            onTap: () => context.push('/u/$username/posts'),
          ),
        ),
        _Section(title: 'Timeline', child: _TimelineSection(username: username)),
        _Section(title: 'Events', child: _EventsSection(username: username)),
        _Section(
          title: 'Organizations',
          child: profile.organizations.isEmpty
              ? const EmptyState(icon: Icons.apartment_outlined, title: 'No verified organizations')
              : Column(children: profile.organizations.map(_OrgTile.new).toList()),
        ),
        _Section(title: 'Achievements', child: _AchievementsSection(achievements: profile.achievements)),
        _Section(title: 'Certificates', child: _CertificatesSection(username: username)),
        _Section(title: 'Allies', child: _AlliesSection(username: username)),
        // Account age closes the page. Metadata, not a profile feature, so it is quiet and last —
        // and month-precision, because the server never sends the exact signup instant publicly
        // (D-312). The owner reads the full timestamp in Settings → Account instead.
        if (profile.joinedAt != null) _JoinedFooter(yearMonth: profile.joinedAt!),
      ],
    );
  }
}

/// "Joined Kurx · August 2026" from an ISO year-month.
class _JoinedFooter extends StatelessWidget {
  const _JoinedFooter({required this.yearMonth});

  final String yearMonth;

  @override
  Widget build(BuildContext context) {
    final label = formatJoinedMonth(yearMonth);
    if (label == null) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.xl),
      child: Center(
        child: Text(
          'Joined Kurx · $label',
          style: TextStyle(color: context.kurx.muted, fontSize: 13),
        ),
      ),
    );
  }
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.child});
  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.xl),
      child: ContentWidth(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: TextStyle(color: c.text, fontSize: 16, fontWeight: FontWeight.w800)),
              const SizedBox(height: KSpace.md),
              child,
            ],
          ),
        ),
      ),
    );
  }
}

class _OrgTile extends StatelessWidget {
  const _OrgTile(this.org);
  final ProfileOrgDto org;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.sm),
      child: KurxCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              Expanded(child: Text(org.orgName, style: TextStyle(color: c.text, fontWeight: FontWeight.w700))),
              if (org.isVerified) const KurxBadge(label: 'Verified', tone: KurxBadgeTone.accent),
            ]),
            const SizedBox(height: KSpace.xs),
            Wrap(spacing: 6, runSpacing: 6, children: org.roles.map((r) => KurxChip(label: r)).toList()),
            const SizedBox(height: KSpace.xs),
            Text(
              '${org.orgEventsConducted} events · ${org.orgCertificatesCount} certificates · ${org.orgAchievementsCount} achievements',
              style: TextStyle(color: c.muted, fontSize: 12),
            ),
          ],
        ),
      ),
    );
  }
}

class _TimelineSection extends ConsumerWidget {
  const _TimelineSection({required this.username});
  final String username;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(publicProfileTimelineProvider(username));
    return AsyncValueView<List<TimelineEntryDto>>(
      value: async,
      onRetry: () => ref.invalidate(publicProfileTimelineProvider(username)),
      // A refused section renders as absence, not as "something went wrong · Retry" (D-235).
      hidden: const SizedBox.shrink(),
      isEmpty: (d) => d.isEmpty,
      empty: const EmptyState(icon: Icons.timeline_outlined, title: 'No milestones yet'),
      data: (entries) => Column(children: entries.map(_TimelineTile.new).toList()),
    );
  }
}

class _TimelineTile extends StatelessWidget {
  const _TimelineTile(this.entry);
  final TimelineEntryDto entry;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.sm),
      child: KurxCard(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(_iconFor(entry.kind), size: 18, color: c.accent),
            const SizedBox(width: KSpace.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(children: [
                    Expanded(child: Text(entry.title, style: TextStyle(color: c.text, fontWeight: FontWeight.w700))),
                    if (entry.isFirstEvent) const KurxBadge(label: 'First on Kurx', tone: KurxBadgeTone.accent),
                  ]),
                  if (entry.roles.isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Wrap(spacing: 6, runSpacing: 6, children: entry.roles.map((r) => KurxChip(label: r)).toList()),
                    ),
                  const SizedBox(height: 4),
                  Text([entry.orgName, entry.city].where((s) => s != null && s.isNotEmpty).join(' · '),
                      style: TextStyle(color: c.muted, fontSize: 12)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  IconData _iconFor(String kind) => switch (kind) {
        'org_joined' => Icons.apartment_outlined,
        'org_verified' => Icons.verified_outlined,
        'achievement' => Icons.emoji_events_outlined,
        'certificate' => Icons.workspace_premium_outlined,
        'organized' => Icons.auto_awesome_outlined,
        _ => Icons.event_outlined,
      };
}

class _EventsSection extends ConsumerWidget {
  const _EventsSection({required this.username});
  final String username;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(publicProfileEventsProvider(username));
    return AsyncValueView<List<PublicEventCardDto>>(
      value: async,
      onRetry: () => ref.invalidate(publicProfileEventsProvider(username)),
      // A refused section renders as absence, not as "something went wrong · Retry" (D-235).
      hidden: const SizedBox.shrink(),
      isEmpty: (d) => d.isEmpty,
      empty: const EmptyState(icon: Icons.event_busy_outlined, title: 'No events yet'),
      data: (events) => Column(
        children: events
            .map((e) => Padding(
                  padding: const EdgeInsets.only(bottom: KSpace.sm),
                  child: KurxCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(e.title, style: TextStyle(color: context.kurx.text, fontWeight: FontWeight.w700)),
                        const SizedBox(height: 4),
                        Text('${e.orgName} · ${e.city}', style: TextStyle(color: context.kurx.muted, fontSize: 12)),
                        const SizedBox(height: KSpace.xs),
                        Wrap(spacing: 6, runSpacing: 6, children: [
                          ...e.roles.map((r) => KurxChip(label: r)),
                          if (e.isAchievement) const KurxBadge(label: 'Achievement', tone: KurxBadgeTone.accent),
                          if (e.certificateVerifyCode != null)
                            const KurxBadge(label: 'Certificate', tone: KurxBadgeTone.success),
                        ]),
                      ],
                    ),
                  ),
                ))
            .toList(),
      ),
    );
  }
}

class _AchievementsSection extends StatelessWidget {
  const _AchievementsSection({required this.achievements});
  final List<AchievementCardDto> achievements;

  @override
  Widget build(BuildContext context) {
    if (achievements.isEmpty) {
      return const EmptyState(icon: Icons.emoji_events_outlined, title: 'No achievements yet');
    }
    final c = context.kurx;
    return Column(
      children: achievements
          .map((a) => Padding(
                padding: const EdgeInsets.only(bottom: KSpace.sm),
                child: KurxCard(
                  child: Row(
                    children: [
                      Icon(Icons.emoji_events_outlined, color: c.accent, size: 18),
                      const SizedBox(width: KSpace.md),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(a.name, style: TextStyle(color: c.text, fontWeight: FontWeight.w700)),
                            Text(
                              [a.eventTitle, a.orgName].where((s) => s != null && s.isNotEmpty).join(' · '),
                              style: TextStyle(color: c.muted, fontSize: 12),
                            ),
                          ],
                        ),
                      ),
                      KurxBadge(
                        label: a.source == 'certificate' ? 'Achievement' : 'Platform Recognition',
                        tone: a.source == 'certificate' ? KurxBadgeTone.accent : KurxBadgeTone.muted,
                      ),
                    ],
                  ),
                ),
              ))
          .toList(),
    );
  }
}

class _CertificatesSection extends ConsumerWidget {
  const _CertificatesSection({required this.username});
  final String username;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(publicProfileCertificatesProvider(username));
    return AsyncValueView<List<PublicCertificateCardDto>>(
      value: async,
      onRetry: () => ref.invalidate(publicProfileCertificatesProvider(username)),
      // A refused section renders as absence, not as "something went wrong · Retry" (D-235).
      hidden: const SizedBox.shrink(),
      isEmpty: (d) => d.isEmpty,
      empty: const EmptyState(icon: Icons.workspace_premium_outlined, title: 'No certificates yet'),
      data: (certs) => Column(
        children: certs
            .map((c) => Padding(
                  padding: const EdgeInsets.only(bottom: KSpace.sm),
                  child: KurxCard(
                    onTap: () => context.push('/certificates/${c.verifyCode}'),
                    child: Text(c.eventTitle, style: TextStyle(color: context.kurx.text, fontWeight: FontWeight.w700)),
                  ),
                ))
            .toList(),
      ),
    );
  }
}

class _AlliesSection extends ConsumerWidget {
  const _AlliesSection({required this.username});
  final String username;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(publicProfileAlliesProvider(username));
    return AsyncValueView<List<AllyProfileCardDto>>(
      value: async,
      onRetry: () => ref.invalidate(publicProfileAlliesProvider(username)),
      // A refused section renders as absence, not as "something went wrong · Retry" (D-235).
      hidden: const SizedBox.shrink(),
      isEmpty: (d) => d.isEmpty,
      empty: const EmptyState(icon: Icons.handshake_outlined, title: 'No allies yet'),
      data: (allies) => Column(
        children: allies
            .map((a) => Padding(
                  padding: const EdgeInsets.only(bottom: KSpace.sm),
                  child: KurxCard(
                    onTap: a.username != null ? () => context.push('/u/${a.username}') : null,
                    child: Row(
                      children: [
                        KurxAvatar(name: a.name, size: 40),
                        const SizedBox(width: KSpace.md),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(a.name, style: TextStyle(color: context.kurx.text, fontWeight: FontWeight.w700)),
                              if (a.username != null)
                                Text('@${a.username}', style: TextStyle(color: context.kurx.muted, fontSize: 12)),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ))
            .toList(),
      ),
    );
  }
}
