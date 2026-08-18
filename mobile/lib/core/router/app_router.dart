import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/data/repositories/trusted_device_repository.dart';
import '../../features/auth/presentation/pages/approve_login_page.dart';
import '../../features/auth/presentation/pages/guest_gate_page.dart';
import '../../features/auth/presentation/pages/otp_verify_page.dart';
import '../../features/auth/presentation/pages/password_login_page.dart';
import '../../features/auth/presentation/pages/password_page.dart';
import '../../features/auth/presentation/pages/password_reset_page.dart';
import '../../features/auth/presentation/pages/registration_flow_page.dart';
import '../../features/auth/presentation/pages/phone_entry_page.dart';
import '../../features/auth/presentation/pages/recovery_page.dart';
import '../../features/auth/presentation/pages/security_page.dart';
import '../../features/settings/presentation/pages/account_page.dart';
import '../../features/settings/presentation/pages/blocked_users_page.dart';
import '../../features/settings/presentation/pages/delete_account_page.dart';
import '../../features/settings/presentation/pages/notification_preferences_page.dart';
import '../../features/auth/presentation/providers/auth_providers.dart';
import '../../features/bookmarks/presentation/pages/saved_page.dart';
import '../../features/calendar/presentation/pages/calendar_page.dart';
import '../../features/events/domain/entities/event_section.dart';
import '../../features/events/presentation/pages/event_detail_page.dart';
import '../../features/events/presentation/pages/event_search_page.dart';
import '../../features/events/presentation/pages/event_section_list_page.dart';
import '../../features/events/presentation/pages/home_dashboard_page.dart';
import '../../features/gamification/presentation/pages/leaderboard_page.dart';
import '../../features/gamification/presentation/pages/points_badges_page.dart';
import '../../features/notifications/presentation/pages/notifications_page.dart';
import '../../features/orders/presentation/pages/group_detail_page.dart';
import '../../features/orders/presentation/pages/invitation_rsvp_page.dart';
import '../../features/orders/presentation/pages/my_groups_page.dart';
import '../../features/orders/presentation/pages/my_orders_page.dart';
import '../../features/orders/presentation/pages/my_refunds_page.dart';
import '../../features/events/presentation/pages/competition_page.dart';
import '../../features/events/presentation/pages/series_page.dart';
import '../../features/events/presentation/pages/team_formation_page.dart';
import '../../features/organizer/presentation/pages/kyc_page.dart';
import '../../features/organizer/presentation/pages/wallet_withdraw_page.dart';
import '../../features/orders/presentation/pages/checkout_page.dart';
import '../../features/orders/presentation/pages/event_reviews_page.dart';
import '../../features/orders/presentation/pages/my_waitlist_page.dart';
import '../../features/profile/presentation/pages/identity_verification_page.dart';
import '../../features/profile/presentation/pages/my_participations_page.dart';
import '../../features/profile/presentation/pages/my_assignments_page.dart';
import '../../features/profile/presentation/pages/org_invitations_page.dart';
import '../../features/posts/domain/entities/post.dart';
import '../../features/posts/presentation/pages/compose_post_page.dart';
import '../../features/posts/presentation/pages/edit_post_page.dart';
import '../../features/posts/presentation/pages/feed_page.dart';
import '../../features/posts/presentation/pages/hashtag_browse_page.dart';
import '../../features/posts/presentation/pages/post_detail_page.dart';
import '../../features/posts/presentation/pages/post_search_page.dart';
import '../../features/posts/presentation/providers/posts_providers.dart';
import '../../features/public_profile/presentation/pages/allies_page.dart';
import '../../features/workspace/presentation/pages/participant_workspace_page.dart';
import '../../features/workspace/presentation/pages/workspace_hub_page.dart';
import '../../features/public_profile/presentation/pages/people_search_page.dart';
import '../../features/public_profile/presentation/pages/public_profile_page.dart';
import '../../features/orders/presentation/pages/my_tickets_page.dart';
import '../../features/orders/presentation/pages/ticket_detail_page.dart';
import '../../features/orders/presentation/pages/transfer_claim_page.dart';
import '../../features/organizer/presentation/pages/announcements_page.dart';
import '../../features/organizer/presentation/pages/assignments_page.dart';
import '../../features/organizer/presentation/pages/attendees_page.dart';
import '../../features/organizer/presentation/pages/checkin_scanner_page.dart';
import '../../features/organizer/presentation/pages/create_event_gate_page.dart';
import '../../features/organizer/presentation/pages/create_event_page.dart';
import '../../features/organizer/presentation/pages/event_analytics_page.dart';
import '../../features/organizer/presentation/pages/event_manage_detail_page.dart';
import '../../features/organizer/presentation/pages/event_representation_page.dart';
import '../../features/organizer/presentation/pages/event_status_page.dart';
import '../../features/organizer/presentation/pages/invitations_page.dart';
import '../../features/organizer/presentation/pages/media_page.dart';
import '../../features/organizer/presentation/pages/representing_page.dart';
import '../../features/organizer/presentation/pages/request_representation_page.dart';
import '../../features/organizer/presentation/widgets/event_manage_scope.dart';
import '../../features/organizer/presentation/pages/org_verification_page.dart';
import '../../features/organizer/presentation/pages/org_wallet_page.dart';
import '../../features/organizer/presentation/pages/payment_readiness_page.dart';
import '../../features/organizer/presentation/pages/registrations_page.dart';
import '../../features/organizer/presentation/pages/schedule_page.dart';
import '../../features/organizer/presentation/pages/speakers_page.dart';
import '../../features/organizer/presentation/pages/sponsors_page.dart';
import '../../features/organizer/presentation/pages/ticket_types_page.dart';
import '../../features/organizer/presentation/pages/venue_page.dart';
import '../../features/profile/presentation/pages/edit_profile_page.dart';
import '../../features/profile/presentation/pages/membership_claims_page.dart';
import '../../features/profile/presentation/pages/profile_privacy_page.dart';
import '../../features/profile/presentation/pages/my_devices_page.dart';
import '../../features/profile/presentation/pages/profile_page.dart';
import '../../features/profile/presentation/pages/username_claim_page.dart';
import '../../features/settings/presentation/pages/help_support_page.dart';
import '../../features/settings/presentation/pages/legal_page.dart';
import '../../features/settings/presentation/pages/settings_page.dart';
import '../../features/shell/presentation/app_shell.dart';
import '../../features/shell/presentation/splash_page.dart';
import '../../features/social/presentation/pages/chat_room_page.dart';
import '../../features/social/presentation/pages/my_chats_page.dart';
import '../../features/taxonomy/presentation/pages/categories_page.dart';
import '../../features/taxonomy/presentation/pages/category_detail_page.dart';
import '../session/session_controller.dart';

EventSection _parseSection(String? name) =>
    EventSection.values.where((s) => s.name == name).firstOrNull ??
    EventSection.trending;

class Routes {
  static const splash = '/splash';
  static const login = '/login';
  /// The guest gate: what the router sends a signed-out visitor to instead of the bare login form.
  static const signInRequired = '/sign-in-required';
  static const loginOtp = '/login/otp';
  static const verify = '/login/verify';
  static const onboarding = '/onboarding';
  static const recover = '/recover';
  static const resetPassword = '/reset';
  static const approvals = '/approvals';
  static const security = '/settings/security';
  static const accountSettings = '/settings/account';                 // D-263
  static const notificationPreferences = '/settings/notifications';   // D-263
  static const blockedUsers = '/settings/blocked';                   // D-263
  static const deleteAccount = '/settings/delete-account';           // D-263
  static const password = '/settings/password';
  static const events = '/events';
  static const search = '/search';
  static const tickets = '/tickets';
  static const saved = '/saved';
  static const profile = '/profile';
  // Main Application areas from the product flow. Home reuses `events` above; Profile is reached
  // from the app bar (top-left), not the bottom nav.
  static const community = '/allies';
  static const posts = '/posts';
  static const messages = '/chats';
  static const workspace = '/workspace';
  static const calendar = '/calendar';
  static const notifications = '/notifications';
  static const categories = '/categories';
  static const usernameClaim = '/profile/username';
  static const settings = '/settings';
}

/// Whether a signed-out visitor may open [loc].
///
/// An allowlist, not a blocklist: anything absent needs a session. Only Home, Profile and Settings
/// render a real guest state — every other screen calls an authenticated endpoint on build, so
/// without this gate a guest reached them, got a 401 and saw a generic "something went wrong"
/// instead of the sign-in screen. Failing closed also means a new route is protected by default
/// rather than by remembering to add it here.
@visibleForTesting
bool isPublicRoute(String loc) {
  const exact = {
    Routes.splash,
    Routes.events,
    Routes.search,
    Routes.categories,
    Routes.settings,
    Routes.profile,
    Routes.recover,
    Routes.resetPassword,
    Routes.signInRequired,
    '/help',
  };
  if (exact.contains(loc)) return true;
  if (const ['/login', '/discover/', '/categories/', '/u/', '/legal/'].any(loc.startsWith)) {
    return true;
  }
  // Event detail (`/events/:slug`) is the public sales page. Everything else under `/events` —
  // the creation gate, checkout, `/manage/*` — is not, so match a single segment and exclude the
  // one literal that shares its shape.
  final detail = RegExp(r'^/events/([^/]+)$').firstMatch(loc);
  return detail != null && detail.group(1) != 'create';
}

CustomTransitionPage<void> _fadePage(Widget child, GoRouterState state) =>
    CustomTransitionPage<void>(
      key: state.pageKey,
      transitionDuration: const Duration(milliseconds: 220),
      child: child,
      transitionsBuilder: (context, animation, _, child) => FadeTransition(
        opacity: CurveTween(curve: Curves.easeOut).animate(animation),
        child: child,
      ),
    );

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = ValueNotifier<int>(0);
  ref.listen(sessionControllerProvider, (_, _) => refresh.value++);
  ref.onDispose(refresh.dispose);

  return GoRouter(
    initialLocation: Routes.splash,
    refreshListenable: refresh,
    redirect: (context, state) {
      final session = ref.read(sessionControllerProvider);
      final loc = state.matchedLocation;
      final loggingIn = loc.startsWith(Routes.login);

      if (session.status == AuthStatus.unknown) {
        return loc == Routes.splash ? null : Routes.splash;
      }
      if (loc == Routes.splash) {
        return session.status == AuthStatus.authenticated &&
                session.needsOnboarding
            ? Routes.onboarding
            : Routes.events;
      }

      switch (session.status) {
        case AuthStatus.unknown:
          return null;
        case AuthStatus.unauthenticated:
          if (isPublicRoute(loc)) return null;
          // Onboarding is the one protected location that is already mid-authentication, so it goes
          // to the form rather than to a gate explaining what an account is for.
          if (loc == Routes.onboarding) return Routes.login;
          // Everything else gets the gate, which names the feature and offers sign-in *or* a way
          // back to browsing. It carries the full URI, query included, so returning after sign-in
          // lands on the exact screen that was asked for.
          return '${Routes.signInRequired}?next=${Uri.encodeComponent(state.uri.toString())}';
        case AuthStatus.authenticated:
          if (session.needsOnboarding && loc != Routes.onboarding) {
            return Routes.onboarding;
          }
          if (!session.needsOnboarding &&
              (loggingIn || loc == Routes.onboarding)) {
            return ref.read(loginReturnToProvider) ?? Routes.events;
          }
          return null;
      }
    },
    routes: [
      // ── Auth ──────────────────────────────────────────────────────────────
      GoRoute(path: Routes.splash, builder: (_, _) => const SplashPage()),
      GoRoute(
        path: Routes.login,
        pageBuilder: (_, s) => _fadePage(const PasswordLoginPage(), s),
      ),
      // One-time-code sign-in: the bootstrap path for accounts with no password/device yet, reached
      // from the password screen's "Sign in with a code" link. Under `/login` so the redirect treats
      // it as part of the login flow.
      GoRoute(
        path: Routes.loginOtp,
        pageBuilder: (_, s) => _fadePage(const PhoneEntryPage(), s),
      ),
      GoRoute(
        path: Routes.verify,
        pageBuilder: (_, s) => _fadePage(const OtpVerifyPage(), s),
      ),
      // Registration ceremony (Phase 2D): the destination for a new user after OTP signup — verify
      // email, complete profile, success. Under the redirect's onboarding gate (needs_onboarding).
      GoRoute(
        path: Routes.onboarding,
        pageBuilder: (_, s) => _fadePage(const RegistrationFlowPage(), s),
      ),
      // Public by necessity: it is what a signed-out visitor is sent to, so gating it would loop.
      GoRoute(
        path: Routes.signInRequired,
        pageBuilder: (_, s) => _fadePage(
          GuestGatePage(next: s.uri.queryParameters['next'] ?? Routes.events),
          s,
        ),
      ),
      // Anonymous by necessity — the whole point is that the user cannot sign in (AM7).
      GoRoute(
        path: Routes.recover,
        pageBuilder: (_, s) => _fadePage(const RecoveryPage(), s),
      ),
      // Anonymous: forgot-password → reset ceremony (Phase 2C, INV-B). Ends signed in on success.
      GoRoute(
        path: Routes.resetPassword,
        pageBuilder: (_, s) => _fadePage(const PasswordResetPage(), s),
      ),
      // Authenticated. NOT under `/login`: this is the *approving* device, which already has a
      // session, and the login redirect would bounce it straight back out.
      GoRoute(
        path: Routes.approvals,
        pageBuilder: (_, s) => _fadePage(const _ApproveLoginGate(), s),
      ),
      GoRoute(
        path: Routes.security,
        pageBuilder: (_, s) => _fadePage(const _SecurityGate(), s),
      ),
      // Account settings (D-263).
      GoRoute(
        path: Routes.accountSettings,
        pageBuilder: (_, s) => _fadePage(const AccountPage(), s),
      ),
      GoRoute(
        path: Routes.notificationPreferences,
        pageBuilder: (_, s) => _fadePage(const NotificationPreferencesPage(), s),
      ),
      GoRoute(
        path: Routes.blockedUsers,
        pageBuilder: (_, s) => _fadePage(const BlockedUsersPage(), s),
      ),
      GoRoute(
        path: Routes.deleteAccount,
        pageBuilder: (_, s) => _fadePage(const DeleteAccountPage(), s),
      ),
      // Authenticated: create / change password (D-126/D-129), reached from the security screen.
      GoRoute(
        path: Routes.password,
        pageBuilder: (_, s) => _fadePage(const PasswordPage(), s),
      ),

      // ── Events (public) ───────────────────────────────────────────────────
      GoRoute(
        path: '/discover/:section',
        pageBuilder: (_, s) => _fadePage(
          EventSectionListPage(
              section: _parseSection(s.pathParameters['section'])),
          s,
        ),
      ),
      // Create Event is a top-level address — no organisation appears in it, because none is needed
      // to reach it. Representation is chosen inside the form (D-267).
      //
      // MUST stay above `/events/:slug`: go_router takes the first route that matches, not the most
      // specific one, so a literal declared after the wildcard is dead. This one was declared 370
      // lines later, so tapping Create Event opened the event *detail* page for a slug named
      // "create", which fetched `GET /v1/events/create`, took a 404 and rendered "We couldn't find
      // that." The gate was unreachable on mobile. Any future literal `/events/<word>` goes here too.
      GoRoute(
        // D-305 — `/events/create` is the GATE, not the form. The form sits behind it at
        // `/events/create/form?product=…`, so nothing can reach the eleven steps without having
        // answered eligibility and Public/Private first.
        path: '/events/create',
        pageBuilder: (_, s) => _fadePage(const CreateEventGatePage(), s),
        routes: [
          GoRoute(
            path: 'form',
            // The gate is ENFORCED, not merely offered (D-305). A deep link, a restored tab or a
            // hand-typed `/events/create/form` carries no product, so it is sent back to the gate
            // rather than opening the eleven steps.
            //
            // Defaulting to Public here — the first version of this — was safe but wrong: it made the
            // form reachable without ever answering eligibility or Public/Private, which is exactly the
            // behaviour D-305 exists to remove. Web needs no equivalent because there the gate *is* the
            // page: `/host/events/new` renders the gate and the form is a stage inside it, never a route.
            redirect: (_, s) {
              final product = s.uri.queryParameters['product'];
              return product == 'Public' || product == 'Private' ? null : '/events/create';
            },
            // `pricing` carries the gate's free/paid answer (D-354) — the pair that selected the
            // verification tier the caller just cleared. Unlike `product` it is not enforced in the
            // redirect: a missing value falls back to 'free', which is the closed position (the Paid
            // card in the form stays gated on canOrganizePaid regardless).
            pageBuilder: (_, s) => _fadePage(
              CreateEventPage(
                product: s.uri.queryParameters['product']!,
                initialPricing:
                    s.uri.queryParameters['pricing'] == 'paid' ? 'paid' : 'free',
              ),
              s,
            ),
          ),
        ],
      ),
      GoRoute(
        path: '/events/:slug',
        pageBuilder: (_, s) =>
            _fadePage(EventDetailPage(slug: s.pathParameters['slug']!), s),
      ),
      GoRoute(
        path: Routes.categories,
        pageBuilder: (_, s) => _fadePage(const CategoriesPage(), s),
      ),
      GoRoute(
        path: '/categories/:id',
        pageBuilder: (_, s) => _fadePage(
          CategoryDetailPage(categoryId: s.pathParameters['id']!),
          s,
        ),
      ),

      // ── Attendee: Tickets & Orders ─────────────────────────────────────────
      GoRoute(
        path: '/tickets/:code',
        pageBuilder: (_, s) => _fadePage(
          TicketDetailPage(ticketCode: s.pathParameters['code']!),
          s,
        ),
      ),
      GoRoute(
        path: '/orders',
        pageBuilder: (_, s) => _fadePage(const MyOrdersPage(), s),
      ),
      // Checkout carries the slug (to read the event page) *and* the event id (the order endpoint
      // is id-keyed), so the screen never has to resolve one from the other mid-purchase.
      GoRoute(
        path: '/events/:slug/checkout/:eventId/:ticketTypeId',
        pageBuilder: (_, s) => _fadePage(
          CheckoutPage(
            eventSlug: s.pathParameters['slug']!,
            eventId: s.pathParameters['eventId']!,
            ticketTypeId: s.pathParameters['ticketTypeId']!,
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/reviews',
        pageBuilder: (_, s) => _fadePage(
          EventReviewsPage(
            eventId: s.pathParameters['eventId']!,
            eventTitle: s.uri.queryParameters['title'] ?? 'this event',
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/series/:seriesId',
        pageBuilder: (_, s) => _fadePage(SeriesPage(seriesId: s.pathParameters['seriesId']!), s),
      ),
      GoRoute(
        path: '/events/:eventId/competition',
        pageBuilder: (_, s) =>
            _fadePage(CompetitionPage(eventId: s.pathParameters['eventId']!), s),
      ),
      GoRoute(
        path: '/events/:eventId/teams/:ticketTypeId',
        pageBuilder: (_, s) => _fadePage(
          TeamFormationPage(
            eventId: s.pathParameters['eventId']!,
            ticketTypeId: s.pathParameters['ticketTypeId']!,
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/representing/:orgId/wallet/withdraw',
        pageBuilder: (_, s) =>
            _fadePage(WalletWithdrawPage(orgId: s.pathParameters['orgId']!), s),
      ),
      GoRoute(
        path: '/representing/:orgId/kyc',
        pageBuilder: (_, s) => _fadePage(KycPage(orgId: s.pathParameters['orgId']!), s),
      ),
      GoRoute(
        path: '/waitlist',
        pageBuilder: (_, s) => _fadePage(const MyWaitlistPage(), s),
      ),
      GoRoute(
        path: '/participations',
        pageBuilder: (_, s) => _fadePage(const MyParticipationsPage(), s),
      ),
      // D-319 — the invitee's staff-assignment inbox. Kept apart from /org-invitations: that one is
      // read-only because accept/decline are token-keyed, while this one is directly actionable.
      // `staff.invited` notifications deep-link here via their `route` field.
      GoRoute(
        path: '/assignments',
        pageBuilder: (_, s) => _fadePage(const MyAssignmentsPage(), s),
      ),
      GoRoute(
        path: '/org-invitations',
        pageBuilder: (_, s) => _fadePage(const OrgInvitationsPage(), s),
      ),
      // Deep link target: the token is only ever in the invite URL, never in the inbox list.
      GoRoute(
        path: '/org-invitations/:token',
        pageBuilder: (_, s) => _fadePage(
          OrgInvitationDeepLinkPage(token: s.pathParameters['token']!),
          s,
        ),
      ),
      GoRoute(
        path: '/profile/identity',
        pageBuilder: (_, s) => _fadePage(const IdentityVerificationPage(), s),
      ),
      GoRoute(
        path: '/transfers/claim',
        pageBuilder: (_, s) => _fadePage(
          TransferClaimPage(code: s.uri.queryParameters['code']),
          s,
        ),
      ),
      GoRoute(
        path: '/groups',
        pageBuilder: (_, s) => _fadePage(const MyGroupsPage(), s),
      ),
      GoRoute(
        path: '/groups/:id',
        pageBuilder: (_, s) => _fadePage(
          GroupDetailPage(groupId: s.pathParameters['id']!),
          s,
        ),
      ),
      GoRoute(
        path: '/invitations/:id/rsvp',
        pageBuilder: (_, s) => _fadePage(
          InvitationRsvpPage(invitationId: s.pathParameters['id']!),
          s,
        ),
      ),


      // ── Social ────────────────────────────────────────────────────────────
      // `/chats` and `/allies` are shell branches (Messages / Community tabs), not top-level
      // routes — a second definition here would be a duplicate path.
      //
      // Keyed by roomId (D-292). GET /v1/chat/rooms/{roomId} is the lookup, and a direct room has no
      // event to key on — which is why keying this route on eventId made every DM row a dead link.
      // An eventId from an older deep link or the workspace tile still resolves: the controller falls
      // back to the event-addressed endpoint on a 404.
      GoRoute(
        path: '/chats/:roomId',
        pageBuilder: (_, s) => _fadePage(
          ChatRoomPage(
            roomId: s.pathParameters['roomId']!,
            title: s.uri.queryParameters['title'],
          ),
          s,
        ),
      ),

      // ── Gamification ──────────────────────────────────────────────────────
      GoRoute(
        path: '/points',
        pageBuilder: (_, s) => _fadePage(const PointsBadgesPage(), s),
      ),
      GoRoute(
        path: '/leaderboard',
        pageBuilder: (_, s) => _fadePage(const LeaderboardPage(), s),
      ),

      // ── Public profile (D-201) ───────────────────────────────────────────
      GoRoute(
        path: '/u/:username',
        pageBuilder: (_, s) =>
            _fadePage(PublicProfilePage(username: s.pathParameters['username']!), s),
      ),
      // Not `/search` — that path is the event-discovery search (Routes.search).
      GoRoute(
        path: '/people-search',
        pageBuilder: (_, s) => _fadePage(const PeopleSearchPage(), s),
      ),

      // ── Posts (D-262) ─────────────────────────────────────────────────────
      // `/posts` itself is the shell branch. Compose is declared before `/posts/:postId` so the
      // literal wins over the parameter — go_router matches in declaration order.
      GoRoute(
        path: '/posts/compose',
        pageBuilder: (_, s) => _fadePage(ComposePostPage(sharing: s.extra as Post?), s),
      ),
      GoRoute(
        path: '/refunds',
        pageBuilder: (_, s) => _fadePage(const MyRefundsPage(), s),
      ),
      GoRoute(
        path: '/u/:username/posts',
        pageBuilder: (_, s) {
          final username = s.pathParameters['username']!;
          return _fadePage(
            FeedPage(source: UserFeed(username), title: '@$username', showCompose: false),
            s,
          );
        },
      ),
      GoRoute(
        path: '/posts/mine',
        pageBuilder: (_, s) => _fadePage(
          const FeedPage(source: MyPostsFeed(), title: 'My posts', showCompose: false),
          s,
        ),
      ),
      GoRoute(
        path: '/posts/saved',
        pageBuilder: (_, s) => _fadePage(
          const FeedPage(source: SavedPostsFeed(), title: 'Saved posts', showCompose: false),
          s,
        ),
      ),
      GoRoute(
        path: '/posts/trending',
        pageBuilder: (_, s) => _fadePage(const HashtagBrowsePage(), s),
      ),
      // Declared before '/posts/:postId' so "search" is never read as a post id.
      GoRoute(
        path: '/posts/search',
        pageBuilder: (_, s) => _fadePage(const PostSearchPage(), s),
      ),
      GoRoute(
        path: '/posts/:postId/edit',
        pageBuilder: (_, s) => _fadePage(EditPostPage(post: s.extra! as Post), s),
      ),
      GoRoute(
        path: '/posts/tag/:tag',
        pageBuilder: (_, s) {
          final tag = s.pathParameters['tag']!;
          return _fadePage(
            FeedPage(source: HashtagFeed(tag), title: '#$tag', showCompose: false),
            s,
          );
        },
      ),
      GoRoute(
        path: '/posts/:postId',
        pageBuilder: (_, s) => _fadePage(PostDetailPage(postId: s.pathParameters['postId']!), s),
      ),

      GoRoute(
        path: '/posts/event/:eventId',
        pageBuilder: (_, s) {
          final eventId = s.pathParameters['eventId']!;
          return _fadePage(
            FeedPage(source: EventFeed(eventId), title: 'Event posts', showCompose: false),
            s,
          );
        },
      ),

      // ── Participant workspace (product flow: unlocked by a confirmed registration) ─────────
      GoRoute(
        path: '/workspace/:eventId',
        pageBuilder: (_, s) =>
            _fadePage(ParticipantWorkspacePage(eventId: s.pathParameters['eventId']!), s),
      ),

      // ── Reached through the app bar or a tab rather than being one (product flow: Profile is
      // top-left; My Events / Saved live under Profile and Home) ────────────
      GoRoute(
        path: Routes.profile,
        pageBuilder: (_, s) => _fadePage(const ProfilePage(), s),
      ),
      GoRoute(
        path: Routes.search,
        pageBuilder: (_, s) => _fadePage(const EventSearchPage(), s),
      ),
      GoRoute(
        path: Routes.tickets,
        pageBuilder: (_, s) => _fadePage(const MyTicketsPage(), s),
      ),
      GoRoute(
        path: Routes.saved,
        pageBuilder: (_, s) => _fadePage(const SavedPage(), s),
      ),

      // ── Profile ───────────────────────────────────────────────────────────
      GoRoute(
        path: Routes.usernameClaim,
        pageBuilder: (_, s) => _fadePage(const UsernameClaimPage(), s),
      ),
      GoRoute(
        path: '/profile/edit',
        pageBuilder: (_, s) => _fadePage(const EditProfilePage(), s),
      ),
      GoRoute(
        path: '/profile/privacy',
        pageBuilder: (_, s) => _fadePage(const ProfilePrivacyPage(), s),
      ),
      GoRoute(
        path: '/profile/membership-claims',
        pageBuilder: (_, s) => _fadePage(const MembershipClaimsPage(), s),
      ),
      GoRoute(
        path: '/profile/devices',
        pageBuilder: (_, s) => _fadePage(const MyDevicesPage(), s),
      ),

      // ── Settings & Legal ──────────────────────────────────────────────────
      GoRoute(
        path: Routes.settings,
        pageBuilder: (_, s) => _fadePage(const SettingsPage(), s),
      ),
      GoRoute(
        path: '/help',
        pageBuilder: (_, s) => _fadePage(const HelpSupportPage(), s),
      ),
      GoRoute(
        path: '/legal/privacy',
        pageBuilder: (_, s) => _fadePage(
          const LegalPage(title: 'Privacy Policy', body: LegalText.privacy),
          s,
        ),
      ),
      GoRoute(
        path: '/legal/terms',
        pageBuilder: (_, s) => _fadePage(
          const LegalPage(title: 'Terms of Service', body: LegalText.terms),
          s,
        ),
      ),
      GoRoute(
        path: '/legal/refund',
        pageBuilder: (_, s) => _fadePage(
          const LegalPage(
              title: 'Refund Policy',
              body:
                  'Refund policy is determined by the event organizer. Kurx processes refunds within 5–7 business days once the organizer approves. Contact the organizer or Kurx support for assistance.'),
          s,
        ),
      ),

      // ── Representing: the organisations a person represents ───────────────
      // Supporting surfaces only (verification, permissions, payouts, collaboration). They are
      // reached from Profile → Representing, never on the way to hosting an event: an organisation is
      // no longer a container events live inside (D-267). `/orgs`, `/org/create` and `/org/:orgId`
      // are gone with the org-first workflow they existed to serve.
      GoRoute(
        path: '/representing',
        pageBuilder: (_, s) => _fadePage(const RepresentingPage(), s),
      ),
      // D-382 — request an institution that is not on Kurx yet (D-074/D-075). Declared BEFORE
      // `/representing/:orgId/…` so `new` is a route, not an organization id.
      GoRoute(
        path: '/representing/new',
        pageBuilder: (_, s) => _fadePage(const RequestRepresentationPage(), s),
      ),
      GoRoute(
        path: '/representing/:orgId/verification',
        pageBuilder: (_, s) => _fadePage(
          OrgVerificationPage(orgId: s.pathParameters['orgId']!),
          s,
        ),
      ),
      GoRoute(
        path: '/representing/:orgId/wallet',
        pageBuilder: (_, s) => _fadePage(
          OrgWalletPage(orgId: s.pathParameters['orgId']!),
          s,
        ),
      ),

      // ── Hosting: a person's own events ────────────────────────────────────
      // Create Event is declared with the public event routes above, because it has to sit ahead of
      // `/events/:slug` to be reachable at all.
      //
      // Management is addressed by the event, which resolves its own organisation via
      // [EventManageScope]. The screens still take `orgId` because the management sub-resources are
      // org-scoped server-side; the difference is that nobody has to pick one to get here.
      GoRoute(
        path: '/events/:eventId/manage',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => EventManageDetailPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/status',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => EventStatusPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      // EDIT Event, not Manage: representation is collected once inside Create Event, and this route is
      // only how a reviewer's "changes requested" gets corrected. It was `/manage/representing`, a
      // standing workspace screen — a second permanent place to answer a question creation had already
      // answered. Reached from Event Status, where the verdict that needs acting on is shown.
      GoRoute(
        path: '/events/:eventId/edit/representing',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => EventRepresentationPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/payment-ready',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => PaymentReadinessPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/venue',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => VenuePage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/schedule',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => SchedulePage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/speakers',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => SpeakersPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/sponsors',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => SponsorsPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/media',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => MediaPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/ticket-types',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => TicketTypesPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),

      // ── Hosting: operations ───────────────────────────────────────────────
      GoRoute(
        path: '/events/:eventId/manage/team',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => AssignmentsPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/invitations',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => InvitationsPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/announcements',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => AnnouncementsPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/registrations',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => RegistrationsPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/attendees',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => AttendeesPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/checkin',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => CheckinScannerPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),
      GoRoute(
        path: '/events/:eventId/manage/analytics',
        pageBuilder: (_, s) => _fadePage(
          EventManageScope(
            eventId: s.pathParameters['eventId']!,
            builder: (orgId) => EventAnalyticsPage(
              orgId: orgId,
              eventId: s.pathParameters['eventId']!,
            ),
          ),
          s,
        ),
      ),


      // ── Misc ──────────────────────────────────────────────────────────────
      GoRoute(
        path: Routes.calendar,
        pageBuilder: (_, s) => _fadePage(const CalendarPage(), s),
      ),
      GoRoute(
        path: Routes.notifications,
        pageBuilder: (_, s) => _fadePage(const NotificationsPage(), s),
      ),
      // ── Shell (bottom nav) ────────────────────────────────────────────────
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) =>
            AppShell(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: Routes.events,
                builder: (_, _) => const HomeDashboardPage(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: Routes.community,
                builder: (_, _) => const AlliesPage(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: Routes.posts,
                builder: (_, _) => const FeedPage(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: Routes.messages,
                builder: (_, _) => const MyChatsPage(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: Routes.workspace,
                builder: (_, _) => const WorkspaceHubPage(),
              ),
            ],
          ),
        ],
      ),
    ],
  );
});

/// Both security screens need *this* device's trusted-device id, which lives in secure storage and
/// so can only be read asynchronously. These gates resolve it once and hand it down, rather than
/// making every screen below repeat the same null-and-loading dance.
class _SecurityGate extends ConsumerWidget {
  const _SecurityGate();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // A missing id is a legitimate state — this handset has simply never been enrolled — so it is
    // passed through as null rather than treated as an error. SecurityPage offers enrollment then.
    return ref.watch(thisDeviceIdProvider).when(
          loading: () => const Scaffold(body: Center(child: CircularProgressIndicator())),
          error: (_, _) => const SecurityPage(),
          data: (id) => SecurityPage(thisDeviceId: id),
        );
  }
}

class _ApproveLoginGate extends ConsumerWidget {
  const _ApproveLoginGate();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ref.watch(thisDeviceIdProvider).when(
          loading: () => const Scaffold(body: Center(child: CircularProgressIndicator())),
          error: (_, _) => const _NotEnrolled(),
          // Approving requires a key to sign with. Without an enrollment there is nothing this
          // screen can do, so it says so instead of failing at the signature step.
          data: (id) => id == null ? const _NotEnrolled() : ApproveLoginPage(deviceId: id),
        );
  }
}

class _NotEnrolled extends StatelessWidget {
  const _NotEnrolled();

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Approve sign-in')),
        body: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Text(
                'Set this device up first so it can approve sign-ins.',
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => context.go(Routes.security),
                child: const Text('Go to security settings'),
              ),
            ],
          ),
        ),
      );
}
