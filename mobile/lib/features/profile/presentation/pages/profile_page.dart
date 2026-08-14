import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../common/widgets/kurx_feedback.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/session/session_providers.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../bookmarks/presentation/providers/bookmarks_providers.dart';
import '../../../notifications/presentation/providers/notifications_providers.dart';
import '../providers/profile_providers.dart';

/// The account hub: identity + username claim, quick stats, and links to the
/// user's tickets, saved events, calendar, notifications, and settings.
class ProfilePage extends ConsumerWidget {
  const ProfilePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final isGuest = ref.watch(isGuestProvider);
    final profile = ref.watch(profileControllerProvider);
    final savedCount = ref.watch(bookmarksControllerProvider).length;
    final unread = ref.watch(unreadNotificationsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Profile')),
      body: ContentWidth(
        child: ListView(
          padding: const EdgeInsets.symmetric(vertical: KSpace.md),
          children: [
            if (isGuest)
              _GuestHeader(onSignIn: () => _signIn(context, ref))
            else
              _ProfileHeader(
                name: profile.name,
                username: profile.username,
                phone: profile.phone,
                avatarKey: profile.avatarKey,
                onClaim: () => context.push('/profile/username'),
                onEdit: () => context.push('/profile/edit'),
              ),
            const SizedBox(height: KSpace.md),
            if (!isGuest)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
                child: Row(
                  children: [
                    Expanded(
                      child: _StatPill(
                        value: '$savedCount',
                        label: 'Saved',
                        icon: Icons.bookmark_outline_rounded,
                      ),
                    ),
                  ],
                ),
              ),
            const SizedBox(height: KSpace.lg),
            // Create Event is the primary action on Profile (D-305) — it moved here from the Workspace
            // hub, and it opens the eligibility **gate**, not the form. Emphasised rather than listed as
            // a plain row because it is the one thing on this screen that starts something new; web
            // gives it the same treatment.
            if (!isGuest)
              Padding(
                padding: const EdgeInsets.fromLTRB(KSpace.lg, 0, KSpace.lg, KSpace.md),
                child: Material(
                  color: c.accent.withValues(alpha: 0.10),
                  borderRadius: BorderRadius.circular(KRadius.md),
                  child: InkWell(
                    onTap: () => context.push('/events/create'),
                    borderRadius: BorderRadius.circular(KRadius.md),
                    child: Padding(
                      padding: const EdgeInsets.all(KSpace.md),
                      child: Row(
                        children: [
                          Icon(Icons.add_circle_outline_rounded, color: c.accent),
                          const SizedBox(width: KSpace.md),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text('Create event',
                                    style: TextStyle(
                                        color: c.text,
                                        fontSize: 15,
                                        fontWeight: FontWeight.w700)),
                                const SizedBox(height: 2),
                                Text('Host something on Kurx — free or ticketed',
                                    style: TextStyle(color: c.muted, fontSize: 12.5)),
                              ],
                            ),
                          ),
                          Icon(Icons.chevron_right_rounded, color: c.muted),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            _MenuTile(
              icon: Icons.bookmark_outline_rounded,
              label: 'Saved events',
              onTap: () => context.go(Routes.saved),
            ),
            _MenuTile(
              icon: Icons.calendar_month_outlined,
              label: 'Calendar',
              onTap: () => context.push(Routes.calendar),
            ),
            _MenuTile(
              icon: Icons.notifications_none_rounded,
              label: 'Notifications',
              badge: unread > 0 ? '$unread' : null,
              onTap: () => context.push(Routes.notifications),
            ),
            _MenuTile(
              icon: Icons.category_outlined,
              label: 'Browse categories',
              onTap: () => context.push(Routes.categories),
            ),
            const Divider(
              height: KSpace.xl,
              indent: KSpace.lg,
              endIndent: KSpace.lg,
            ),
            _MenuTile(
              icon: Icons.settings_outlined,
              label: 'Settings',
              onTap: () => context.push(Routes.settings),
            ),
            _MenuTile(
              icon: Icons.visibility_outlined,
              label: 'Privacy',
              onTap: () => context.push('/profile/privacy'),
            ),
            _MenuTile(
              icon: Icons.workspace_premium_outlined,
              label: 'My certificates',
              onTap: () => context.push('/certificates'),
            ),
            _MenuTile(
              icon: Icons.leaderboard_outlined,
              label: 'Points & leaderboard',
              onTap: () => context.push('/points'),
            ),
            // `/orders` was reachable ONLY from the checkout success screen, so once a buyer left
            // that screen their order history — and the refunds hanging off it — were unreachable.
            _MenuTile(
              icon: Icons.receipt_long_outlined,
              label: 'My orders & refunds',
              onTap: () => context.push('/orders'),
            ),
            // A participation is an event ROLE (speaker, judge, mentor, volunteer). Invited roles
            // are actionable and had no entry point, so a pending invitation was unanswerable.
            _MenuTile(
              icon: Icons.badge_outlined,
              label: 'My participations',
              onTap: () => context.push('/participations'),
            ),
            _MenuTile(
              icon: Icons.groups_outlined,
              label: 'My groups',
              onTap: () => context.push('/groups'),
            ),
            _MenuTile(
              icon: Icons.hourglass_empty_rounded,
              label: 'My waitlist',
              onTap: () => context.push('/waitlist'),
            ),
            _MenuTile(
              icon: Icons.forum_outlined,
              label: 'Chats',
              onTap: () => context.push('/chats'),
            ),
            if (profile.hasUsername)
              _MenuTile(
                icon: Icons.badge_outlined,
                label: 'My public profile',
                onTap: () => context.push('/u/${profile.username}'),
              ),
            // Was "My organizations" → `/orgs`, the door into the org-first workflow (list orgs,
            // open one, open its events, create an event). Organisations are not containers for
            // hosting any more (D-267): this is the authority you hold to act for one, and hosting
            // lives in Workspace.
            _MenuTile(
              icon: Icons.verified_user_outlined,
              label: 'Representing',
              onTap: () => context.push('/representing'),
            ),
            // D-319 — invitations to staff an event. Actionable here, unlike the org inbox below:
            // assignments are keyed by id, so Accept and Decline are real buttons. Until this
            // existed the invitee had no way to see an invite at all, and §14.2's go-live gate
            // waits on their acceptance.
            _MenuTile(
              icon: Icons.assignment_ind_outlined,
              label: 'Assignments',
              onTap: () => context.push('/assignments'),
            ),
            // Read-only inbox: accept/decline are token-keyed and the token only arrives in the
            // invite message, so this shows what is pending. It had no entry point.
            _MenuTile(
              icon: Icons.mark_email_unread_outlined,
              label: 'Organization invitations',
              onTap: () => context.push('/org-invitations'),
            ),
            _MenuTile(
              icon: Icons.handshake_outlined,
              label: 'My allies',
              onTap: () => context.push('/allies'),
            ),
            const Divider(
              height: KSpace.xl,
              indent: KSpace.lg,
              endIndent: KSpace.lg,
            ),
            _MenuTile(
              icon: Icons.devices_outlined,
              label: 'My devices',
              onTap: () => context.push('/profile/devices'),
            ),
            _MenuTile(
              icon: Icons.help_outline_rounded,
              label: 'Help & support',
              onTap: () => context.push('/help'),
            ),
            if (!isGuest) ...[
              const SizedBox(height: KSpace.lg),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
                child: KurxButton(
                  label: 'Sign out',
                  variant: KurxButtonVariant.secondary,
                  icon: Icons.logout_rounded,
                  expand: true,
                  onPressed: () => _signOut(context, ref),
                ),
              ),
            ],
            const SizedBox(height: KSpace.xl),
            Center(
              child: Text(
                'Kurx • made in India',
                style: TextStyle(color: c.muted, fontSize: 12),
              ),
            ),
            const SizedBox(height: KSpace.xl),
          ],
        ),
      ),
    );
  }

  void _signIn(BuildContext context, WidgetRef ref) {
    ref.read(loginReturnToProvider.notifier).state = Routes.profile;
    context.push(Routes.login);
  }

  Future<void> _signOut(BuildContext context, WidgetRef ref) async {
    final ok = await KurxFeedback.confirm(
      context,
      title: 'Sign out?',
      message: 'You can browse events as a guest and sign back in anytime.',
      confirmLabel: 'Sign out',
      destructive: true,
    );
    if (ok) await ref.read(authControllerProvider.notifier).logout();
  }
}

class _ProfileHeader extends StatelessWidget {
  const _ProfileHeader({
    required this.name,
    required this.username,
    required this.phone,
    required this.avatarKey,
    required this.onClaim,
    required this.onEdit,
  });

  final String name;
  final String? username;
  final String phone;
  final String? avatarKey;
  final VoidCallback onClaim;
  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
      child: Column(
        children: [
          Row(
            children: [
              KurxAvatar(name: name, imageUrl: avatarKey, size: 64),
              const SizedBox(width: KSpace.lg),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      name,
                      style: TextStyle(
                        color: c.text,
                        fontSize: 20,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 2),
                    if (username != null)
                      Text(
                        '@$username',
                        style: TextStyle(
                          color: c.accent,
                          fontSize: 14,
                          fontWeight: FontWeight.w700,
                        ),
                      )
                    else
                      // A text-only tap target announcing as nothing, on the action that gives a
                      // person their public identity. `c.accent` is the ember FILL besides — 2.80:1
                      // as text on light, the same token defect swept across web and admin.
                      Semantics(
                        button: true,
                        label: 'Claim your username',
                        child: GestureDetector(
                        onTap: onClaim,
                        child: Text(
                          '+ Claim your username',
                          style: TextStyle(
                            color: c.accentText,
                            fontSize: 13.5,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        ),
                      ),
                    const SizedBox(height: 2),
                    Text(
                      // Already canonical E.164 from /auth/me — it carries its own country code. The
                      // hardcoded '+91 ' prefix that used to sit here printed "+91 +919876543210".
                      phone.startsWith('+') ? phone : '+$phone',
                      style: TextStyle(color: c.muted, fontSize: 12.5),
                    ),
                  ],
                ),
              ),
              IconButton(
                icon: const Icon(Icons.edit_outlined),
                tooltip: 'Edit profile',
                onPressed: onEdit,
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _GuestHeader extends StatelessWidget {
  const _GuestHeader({required this.onSignIn});

  final VoidCallback onSignIn;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.all(KSpace.lg),
      child: Container(
        padding: const EdgeInsets.all(KSpace.xl),
        decoration: BoxDecoration(
          color: c.cardSurface,
          borderRadius: BorderRadius.circular(KRadius.lg),
          border: Border.all(color: c.border),
        ),
        child: Column(
          children: [
            Icon(Icons.account_circle_outlined, size: 56, color: c.muted),
            const SizedBox(height: KSpace.md),
            Text(
              'Browsing as guest',
              style: TextStyle(
                color: c.text,
                fontSize: 17,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: KSpace.xs),
            Text(
              'Sign in to book tickets, save events, and claim your username.',
              textAlign: TextAlign.center,
              style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35),
            ),
            const SizedBox(height: KSpace.lg),
            KurxButton(
              label: 'Sign in with phone',
              icon: Icons.login_rounded,
              expand: true,
              onPressed: onSignIn,
            ),
          ],
        ),
      ),
    );
  }
}

class _StatPill extends StatelessWidget {
  const _StatPill({
    required this.value,
    required this.label,
    required this.icon,
  });

  final String value;
  final String label;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
        border: Border.all(color: c.border),
      ),
      child: Row(
        children: [
          Icon(icon, color: c.accent, size: 22),
          const SizedBox(width: KSpace.md),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                value,
                style: TextStyle(
                  color: c.text,
                  fontSize: 20,
                  fontWeight: FontWeight.w800,
                ),
              ),
              Text(label, style: TextStyle(color: c.muted, fontSize: 12.5)),
            ],
          ),
        ],
      ),
    );
  }
}

class _MenuTile extends StatelessWidget {
  const _MenuTile({
    required this.icon,
    required this.label,
    required this.onTap,
    this.badge,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final String? badge;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return ListTile(
      leading: Icon(icon, color: c.text),
      title: Text(
        label,
        style: TextStyle(
          color: c.text,
          fontSize: 15,
          fontWeight: FontWeight.w600,
        ),
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (badge != null)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
              decoration: BoxDecoration(
                color: c.accent,
                borderRadius: BorderRadius.circular(KRadius.pill),
              ),
              child: Text(
                badge!,
                style: TextStyle(
                  color: c.onAccent,
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          const SizedBox(width: KSpace.sm),
          Icon(Icons.chevron_right_rounded, color: c.muted),
        ],
      ),
      onTap: onTap,
    );
  }
}
