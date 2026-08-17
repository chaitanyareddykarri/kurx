# Kurx UI/UX Redesign — Mobile (Flutter) Screen Inventory

Baseline generated in Phase 0.1. `Status` vocabulary: `Legacy` · `Foundation applied` · `Partially migrated` · `Redesigned` · `Verified` · `Blocked` · `N/A`. Every row starts `Legacy`. This file is regenerated only by hand — edit rows in place as work lands.

**Total: 95 pages** (`find mobile/lib -name '*_page.dart' | wc -l`, re-measured 2026-08-15 — this said
91, and four pages that shipped after the Phase 0.1 baseline were never added; they are listed at the
bottom with no fabricated status).

> Widget tests under `mobile/test/features/**` assert against several of these pages — change finders,
> never assertions.

| Page | Area | Phase | File | Status | Responsive | A11y | Functional |
|---|---|---|---|---|---|---|---|
| `approve_login` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/approve_login_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `otp_verify` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/otp_verify_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `password_login` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/password_login_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `password` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/password_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `password_reset` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/password_reset_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `phone_entry` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/phone_entry_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `recovery` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/recovery_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `registration_flow` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/registration_flow_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `security` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/security_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `step_up` | Authentication | 36 | `mobile/lib/features/auth/presentation/pages/step_up_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `saved` | Saved | 38 | `mobile/lib/features/bookmarks/presentation/pages/saved_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `calendar` | Calendar | 38 | `mobile/lib/features/calendar/presentation/pages/calendar_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `certificate_detail` | Certificates | 38B | `mobile/lib/features/certificates/presentation/pages/certificate_detail_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `my_certificates` | Certificates | 38B | `mobile/lib/features/certificates/presentation/pages/my_certificates_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `event` | Discovery & detail | 34 / 35 | `mobile/lib/features/events/domain/entities/event_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `get_event` | Discovery & detail | 34 / 35 | `mobile/lib/features/events/domain/usecases/get_event_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `competition` | Discovery & detail | 34 / 35 | `mobile/lib/features/events/presentation/pages/competition_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `event_detail` | Discovery & detail | 34 / 35 | `mobile/lib/features/events/presentation/pages/event_detail_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `event_search` | Discovery & detail | 34 / 35 | `mobile/lib/features/events/presentation/pages/event_search_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `event_section_list` | Discovery & detail | 34 / 35 | `mobile/lib/features/events/presentation/pages/event_section_list_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `home_dashboard` | Discovery & detail | 34 / 35 | `mobile/lib/features/events/presentation/pages/home_dashboard_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `series` | Discovery & detail | 34 / 35 | `mobile/lib/features/events/presentation/pages/series_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `team_formation` | Discovery & detail | 34 / 35 | `mobile/lib/features/events/presentation/pages/team_formation_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `leaderboard` | Gamification | 38B | `mobile/lib/features/gamification/presentation/pages/leaderboard_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `points_badges` | Gamification | 38B | `mobile/lib/features/gamification/presentation/pages/points_badges_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `notifications` | Notifications | 38 | `mobile/lib/features/notifications/presentation/pages/notifications_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `checkout` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/checkout_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `event_reviews` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/event_reviews_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `group_detail` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/group_detail_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `invitation_rsvp` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/invitation_rsvp_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `my_groups` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/my_groups_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `my_orders` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/my_orders_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `my_refunds` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/my_refunds_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `my_tickets` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/my_tickets_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `my_waitlist` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/my_waitlist_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `ticket_detail` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/ticket_detail_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `transfer_claim` | Tickets & registration | 37 | `mobile/lib/features/orders/presentation/pages/transfer_claim_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `announcements` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/announcements_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `assignments` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/assignments_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `attendees` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/attendees_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `checkin_scanner` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/checkin_scanner_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `create_event` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/create_event_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `event_analytics` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/event_analytics_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `event_manage_detail` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/event_manage_detail_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `event_status` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/event_status_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `invitations` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/invitations_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `kyc` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/kyc_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `media` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/media_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `org_certificates` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/org_certificates_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `org_verification` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/org_verification_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `org_wallet` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/org_wallet_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `payment_readiness` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/payment_readiness_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `registrations` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/registrations_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `representing` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/representing_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `schedule` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/schedule_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `speakers` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/speakers_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `sponsors` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/sponsors_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `ticket_types` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/ticket_types_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `venue` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/venue_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `wallet_withdraw` | Organizer screens | 39 | `mobile/lib/features/organizer/presentation/pages/wallet_withdraw_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `compose_post` | Posts | 38A | `mobile/lib/features/posts/presentation/pages/compose_post_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `edit_post` | Posts | 38A | `mobile/lib/features/posts/presentation/pages/edit_post_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `feed` | Posts | 38A | `mobile/lib/features/posts/presentation/pages/feed_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `hashtag_browse` | Posts | 38A | `mobile/lib/features/posts/presentation/pages/hashtag_browse_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `post_detail` | Posts | 38A | `mobile/lib/features/posts/presentation/pages/post_detail_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `edit_profile` | Profile | 38 | `mobile/lib/features/profile/presentation/pages/edit_profile_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `identity_verification` | Profile | 38 | `mobile/lib/features/profile/presentation/pages/identity_verification_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `membership_claims` | Profile | 38 | `mobile/lib/features/profile/presentation/pages/membership_claims_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `my_devices` | Profile | 38 | `mobile/lib/features/profile/presentation/pages/my_devices_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `my_participations` | Profile | 38 | `mobile/lib/features/profile/presentation/pages/my_participations_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `org_invitations` | Profile | 38 | `mobile/lib/features/profile/presentation/pages/org_invitations_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `profile` | Profile | 38 | `mobile/lib/features/profile/presentation/pages/profile_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `profile_privacy` | Profile | 38 | `mobile/lib/features/profile/presentation/pages/profile_privacy_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `username_claim` | Profile | 38 | `mobile/lib/features/profile/presentation/pages/username_claim_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `allies` | Public identity | 38B | `mobile/lib/features/public_profile/presentation/pages/allies_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `people_search` | Public identity | 38B | `mobile/lib/features/public_profile/presentation/pages/people_search_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `public_profile` | Public identity | 38B | `mobile/lib/features/public_profile/presentation/pages/public_profile_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `account` | Settings | 38 | `mobile/lib/features/settings/presentation/pages/account_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `blocked_users` | Settings | 38 | `mobile/lib/features/settings/presentation/pages/blocked_users_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `delete_account` | Settings | 38 | `mobile/lib/features/settings/presentation/pages/delete_account_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `help_support` | Settings | 38 | `mobile/lib/features/settings/presentation/pages/help_support_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `legal` | Settings | 38 | `mobile/lib/features/settings/presentation/pages/legal_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `notification_preferences` | Settings | 38 | `mobile/lib/features/settings/presentation/pages/notification_preferences_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `settings` | Settings | 38 | `mobile/lib/features/settings/presentation/pages/settings_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `splash` | Shell / splash | 33 | `mobile/lib/features/shell/presentation/splash_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `chat_room` | Chat | 38A | `mobile/lib/features/social/presentation/pages/chat_room_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `my_chats` | Chat | 38A | `mobile/lib/features/social/presentation/pages/my_chats_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `categories` | Categories | 34 | `mobile/lib/features/taxonomy/presentation/pages/categories_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `category_detail` | Categories | 34 | `mobile/lib/features/taxonomy/presentation/pages/category_detail_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `participant_workspace` | Workspace | 38B | `mobile/lib/features/workspace/presentation/pages/participant_workspace_page.dart` | Legacy | ☐ | ☐ | ☐ |
| `workspace_hub` | Workspace | 38B | `mobile/lib/features/workspace/presentation/pages/workspace_hub_page.dart` | Legacy | ☐ | ☐ | ☐ |

## Shipped after the Phase 0.1 baseline — not yet assessed

Four pages exist under `mobile/lib` that the table above never listed. They carry **no status**: the
redesign/responsive/a11y pass has not been run against them, and inventing a value would be worse
than an empty cell.

| Page | Area | File | Status | Responsive | A11y | Functional |
|---|---|---|---|---|---|---|
| `guest_gate` | Authentication | `mobile/lib/features/auth/presentation/pages/guest_gate_page.dart` | *Not assessed* | ☐ | ☐ | ☐ |
| `create_event_gate` | Organizer (D-305 gate) | `mobile/lib/features/organizer/presentation/pages/create_event_gate_page.dart` | *Not assessed* | ☐ | ☐ | ☐ |
| `post_search` | Posts (D-297) | `mobile/lib/features/posts/presentation/pages/post_search_page.dart` | *Not assessed* | ☐ | ☐ | ☐ |
| `my_assignments` | Profile | `mobile/lib/features/profile/presentation/pages/my_assignments_page.dart` | *Not assessed* | ☐ | ☐ | ☐ |
