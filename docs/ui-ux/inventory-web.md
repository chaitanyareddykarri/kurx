# Kurx UI/UX Redesign — Web Screen Inventory

Baseline generated in Phase 0.1. `Status` vocabulary: `Legacy` · `Foundation applied` · `Partially migrated` · `Redesigned` · `Verified` · `Blocked` · `N/A`. Every row starts `Legacy`. This file is regenerated only by hand — edit rows in place as work lands.

**Total: 88 routes** (`web/app/**/page.tsx`), plus 90 non-visual route handlers listed at the bottom.

| Route | Area | Phase | File | Status | Responsive | A11y | Functional |
|---|---|---|---|---|---|---|---|
| `/` | Public marketing | 13A · 25 | `web/app/(public)/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/about` | Public marketing | 13A · 25 | `web/app/(public)/about/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/allies` | Allies | 18A.4 | `web/app/(app)/allies/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/blog` | Public marketing | 13A · 25 | `web/app/(public)/blog/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/book/[slug]` | Booking | 19 | `web/app/(app)/book/[slug]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ (REG-009 — hand-off, not checkout) |
| `/certificates` | Certificates | 20C | `web/app/(app)/certificates/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/chats` | Messaging | 20B | `web/app/(app)/chats/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/chats/[eventId]` | Messaging | 20B | `web/app/(app)/chats/[eventId]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/contact` | Public marketing | 13A · 25 | `web/app/(public)/contact/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/discover` | Discover | 13 | `web/app/(app)/discover/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/e/[slug]` | Event detail | 15 | `web/app/e/[slug]/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/features` | Public marketing | 13A · 25 | `web/app/(public)/features/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/groups` | Groups | 20C | `web/app/(app)/groups/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host` | Host dashboard | 21.1 | `web/app/(app)/host/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/admin/claims` | D-195 orphan candidate | 49 | `web/app/(app)/host/admin/claims/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/host/admin/fraud` | D-195 orphan candidate | 49 | `web/app/(app)/host/admin/fraud/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/host/admin/orgs` | D-195 orphan candidate | 49 | `web/app/(app)/host/admin/orgs/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/host/admin/verifications` | D-195 orphan candidate | 49 | `web/app/(app)/host/admin/verifications/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/host/analytics` | Host top-level | 21 | `web/app/(app)/host/analytics/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/announcements` | Host top-level | 21 | `web/app/(app)/host/announcements/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/attendees` | Host top-level | 21 | `web/app/(app)/host/attendees/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/certificates` | Host top-level | 21 | `web/app/(app)/host/certificates/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/analytics` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/analytics/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/announcements` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/announcements/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/attendees` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/attendees/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/certificates` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/certificates/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/chat` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/chat/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/checkin` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/checkin/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/details` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/details/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/invitations` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/invitations/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/media` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/media/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/people` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/people/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/readiness` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/readiness/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/registrations` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/registrations/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/reviews` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/reviews/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/schedule` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/schedule/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/team` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/team/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/[id]/tickets` | Event workspace tab | 21.2 | `web/app/(app)/host/events/[id]/tickets/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/events/new` | Event creation wizard | 22 | `web/app/(app)/host/events/new/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/forms` | Host top-level | 21 | `web/app/(app)/host/forms/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/invitations` | Host top-level | 21 | `web/app/(app)/host/invitations/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/notifications` | Host top-level | 21 | `web/app/(app)/host/notifications/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/payouts` | Host top-level | 21 | `web/app/(app)/host/payouts/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/representing` | Representation & finance | 21.9 / 24 | `web/app/(app)/host/representing/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/representing/[orgId]/finance` | Representation & finance | 21.9 / 24 | `web/app/(app)/host/representing/[orgId]/finance/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/representing/new` | Representation & finance | 21.9 / 24 | `web/app/(app)/host/representing/new/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/risk` | Host top-level | 21 | `web/app/(app)/host/risk/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ (honest unavailable state) |
| `/host/settings` | Host top-level | 21 | `web/app/(app)/host/settings/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/templates/certificates` | WorkflowPage placeholder | 21 / 49 | `web/app/(app)/host/templates/certificates/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ (honest unavailable state) |
| `/host/templates/editor` | WorkflowPage placeholder | 21 / 49 | `web/app/(app)/host/templates/editor/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ (honest unavailable state) |
| `/host/templates/invites` | WorkflowPage placeholder | 21 / 49 | `web/app/(app)/host/templates/invites/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ (honest unavailable state) |
| `/host/tickets` | Host top-level | 21 | `web/app/(app)/host/tickets/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host/verification` | Host top-level | 21 | `web/app/(app)/host/verification/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/i/[token]` | Invite link | 20D | `web/app/(app)/i/[token]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/invitations` | Invitations | 20D | `web/app/(app)/invitations/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/notifications` | Notifications | 20D | `web/app/(app)/notifications/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/o/[slug]` | Org public profile | 18A.6 | `web/app/o/[slug]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/onboarding` | Onboarding | 17 | `web/app/onboarding/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/posts` | Posts & social | 20A | `web/app/(app)/posts/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/posts/[postId]` | Posts & social | 20A | `web/app/(app)/posts/[postId]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/posts/event/[eventId]` | Posts & social | 20A | `web/app/(app)/posts/event/[eventId]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/posts/mine` | Posts & social | 20A | `web/app/(app)/posts/mine/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/posts/saved` | Posts & social | 20A | `web/app/(app)/posts/saved/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/posts/tag/[tag]` | Posts & social | 20A | `web/app/(app)/posts/tag/[tag]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/posts/user/[username]` | Posts & social | 20A | `web/app/(app)/posts/user/[username]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/pricing` | Public marketing | 13A · 25 | `web/app/(public)/pricing/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/privacy` | Public marketing | 13A · 25 | `web/app/(public)/privacy/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/profile` | Own profile | 18.1 | `web/app/(app)/profile/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/recover` | Auth | 16 | `web/app/recover/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/register` | Auth | 16 | `web/app/register/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/reset` | Auth | 16 | `web/app/reset/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/saved` | Saved | 18.4 | `web/app/(app)/saved/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/settings` | Account settings | 18.2 | `web/app/(app)/settings/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/settings/account` | Account settings | 18.2 | `web/app/(app)/settings/account/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/settings/identity` | Account settings | 18.2 | `web/app/(app)/settings/identity/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/settings/notifications` | Account settings | 18.2 | `web/app/(app)/settings/notifications/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/settings/privacy` | Account settings | 18.2 | `web/app/(app)/settings/privacy/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/settings/representing` | Account settings | 18.2 | `web/app/(app)/settings/representing/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/settings/security` | Account settings | 18.2 | `web/app/(app)/settings/security/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/support` | Public marketing | 13A · 25 | `web/app/(public)/support/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/terms` | Public marketing | 13A · 25 | `web/app/(public)/terms/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/tickets` | Tickets | 19 / 20 | `web/app/(app)/tickets/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/tickets/refunds` | Tickets | 19 / 20 | `web/app/(app)/tickets/refunds/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/u/[username]` | Public profile | 18A.1 | `web/app/u/[username]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/verify/[code]` | Verification link | 20D | `web/app/verify/[code]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/workspace` | User events | 20 | `web/app/(app)/workspace/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/workspace/[eventId]` | User events | 20 | `web/app/(app)/workspace/[eventId]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |

## Non-visual route handlers (no UI — excluded from redesign scope)

| Route | File |
|---|---|
| `/book/[slug]` | `web/app/(app)/book/[slug]/page.tsx` |
| `/certificates` | `web/app/(app)/certificates/page.tsx` |
| `/chats/[eventId]` | `web/app/(app)/chats/[eventId]/page.tsx` |
| `/chats` | `web/app/(app)/chats/page.tsx` |
| `/discover` | `web/app/(app)/discover/page.tsx` |
| `/groups` | `web/app/(app)/groups/page.tsx` |
| `/host/admin/claims` | `web/app/(app)/host/admin/claims/page.tsx` |
| `/host/admin/doc/[id]` | `web/app/(app)/host/admin/doc/[id]/route.ts` |
| `/host/admin/fraud` | `web/app/(app)/host/admin/fraud/page.tsx` |
| `/host/admin/orgs` | `web/app/(app)/host/admin/orgs/page.tsx` |
| `/host/admin/verifications` | `web/app/(app)/host/admin/verifications/page.tsx` |
| `/host/analytics` | `web/app/(app)/host/analytics/page.tsx` |
| `/host/announcements` | `web/app/(app)/host/announcements/page.tsx` |
| `/host/attendees` | `web/app/(app)/host/attendees/page.tsx` |
| `/host/certificates` | `web/app/(app)/host/certificates/page.tsx` |
| `/host/events/[id]/analytics` | `web/app/(app)/host/events/[id]/analytics/page.tsx` |
| `/host/events/[id]/announcements` | `web/app/(app)/host/events/[id]/announcements/page.tsx` |
| `/host/events/[id]/attendees` | `web/app/(app)/host/events/[id]/attendees/page.tsx` |
| `/host/events/[id]/certificates` | `web/app/(app)/host/events/[id]/certificates/page.tsx` |
| `/host/events/[id]/chat` | `web/app/(app)/host/events/[id]/chat/page.tsx` |
| `/host/events/[id]/checkin` | `web/app/(app)/host/events/[id]/checkin/page.tsx` |
| `/host/events/[id]/details` | `web/app/(app)/host/events/[id]/details/page.tsx` |
| `/host/events/[id]/invitations` | `web/app/(app)/host/events/[id]/invitations/page.tsx` |
| `/host/events/[id]/media` | `web/app/(app)/host/events/[id]/media/page.tsx` |
| `/host/events/[id]` | `web/app/(app)/host/events/[id]/page.tsx` |
| `/host/events/[id]/people` | `web/app/(app)/host/events/[id]/people/page.tsx` |
| `/host/events/[id]/readiness` | `web/app/(app)/host/events/[id]/readiness/page.tsx` |
| `/host/events/[id]/registrations` | `web/app/(app)/host/events/[id]/registrations/page.tsx` |
| `/host/events/[id]/reviews` | `web/app/(app)/host/events/[id]/reviews/page.tsx` |
| `/host/events/[id]/schedule` | `web/app/(app)/host/events/[id]/schedule/page.tsx` |
| `/host/events/[id]/team` | `web/app/(app)/host/events/[id]/team/page.tsx` |
| `/host/events/[id]/tickets` | `web/app/(app)/host/events/[id]/tickets/page.tsx` |
| `/host/events/new` | `web/app/(app)/host/events/new/page.tsx` |
| `/host/forms` | `web/app/(app)/host/forms/page.tsx` |
| `/host/invitations` | `web/app/(app)/host/invitations/page.tsx` |
| `/host/notifications` | `web/app/(app)/host/notifications/page.tsx` |
| `/host` | `web/app/(app)/host/page.tsx` |
| `/host/payouts` | `web/app/(app)/host/payouts/page.tsx` |
| `/host/representing/[orgId]/finance` | `web/app/(app)/host/representing/[orgId]/finance/page.tsx` |
| `/host/representing/new` | `web/app/(app)/host/representing/new/page.tsx` |
| `/host/representing` | `web/app/(app)/host/representing/page.tsx` |
| `/host/risk` | `web/app/(app)/host/risk/page.tsx` |
| `/host/settings` | `web/app/(app)/host/settings/page.tsx` |
| `/host/templates/certificates` | `web/app/(app)/host/templates/certificates/page.tsx` |
| `/host/templates/editor` | `web/app/(app)/host/templates/editor/page.tsx` |
| `/host/templates/invites` | `web/app/(app)/host/templates/invites/page.tsx` |
| `/host/tickets` | `web/app/(app)/host/tickets/page.tsx` |
| `/host/verification` | `web/app/(app)/host/verification/page.tsx` |
| `/i/[token]` | `web/app/(app)/i/[token]/page.tsx` |
| `/invitations` | `web/app/(app)/invitations/page.tsx` |
| `/notifications` | `web/app/(app)/notifications/page.tsx` |
| `/posts/[postId]` | `web/app/(app)/posts/[postId]/page.tsx` |
| `/posts/event/[eventId]` | `web/app/(app)/posts/event/[eventId]/page.tsx` |
| `/posts/mine` | `web/app/(app)/posts/mine/page.tsx` |
| `/posts` | `web/app/(app)/posts/page.tsx` |
| `/posts/saved` | `web/app/(app)/posts/saved/page.tsx` |
| `/posts/tag/[tag]` | `web/app/(app)/posts/tag/[tag]/page.tsx` |
| `/posts/user/[username]` | `web/app/(app)/posts/user/[username]/page.tsx` |
| `/profile` | `web/app/(app)/profile/page.tsx` |
| `/saved` | `web/app/(app)/saved/page.tsx` |
| `/settings/account` | `web/app/(app)/settings/account/page.tsx` |
| `/settings/identity` | `web/app/(app)/settings/identity/page.tsx` |
| `/settings/notifications` | `web/app/(app)/settings/notifications/page.tsx` |
| `/settings` | `web/app/(app)/settings/page.tsx` |
| `/settings/privacy` | `web/app/(app)/settings/privacy/page.tsx` |
| `/settings/representing` | `web/app/(app)/settings/representing/page.tsx` |
| `/settings/security` | `web/app/(app)/settings/security/page.tsx` |
| `/tickets` | `web/app/(app)/tickets/page.tsx` |
| `/tickets/refunds` | `web/app/(app)/tickets/refunds/page.tsx` |
| `/workspace/[eventId]` | `web/app/(app)/workspace/[eventId]/page.tsx` |
| `/workspace` | `web/app/(app)/workspace/page.tsx` |
| `/about` | `web/app/(public)/about/page.tsx` |
| `/blog` | `web/app/(public)/blog/page.tsx` |
| `/contact` | `web/app/(public)/contact/page.tsx` |
| `/features` | `web/app/(public)/features/page.tsx` |
| `/` | `web/app/(public)/page.tsx` |
| `/pricing` | `web/app/(public)/pricing/page.tsx` |
| `/privacy` | `web/app/(public)/privacy/page.tsx` |
| `/support` | `web/app/(public)/support/page.tsx` |
| `/terms` | `web/app/(public)/terms/page.tsx` |
| `/allies` | `web/app/(app)/allies/page.tsx` |
| `/api/realtime-token` | `web/app/api/realtime-token/route.ts` |
| `/e/[slug]` | `web/app/e/[slug]/page.tsx` |
| `/o/[slug]` | `web/app/o/[slug]/page.tsx` |
| `/onboarding` | `web/app/onboarding/page.tsx` |
| `/recover` | `web/app/recover/page.tsx` |
| `/register` | `web/app/register/page.tsx` |
| `/reset` | `web/app/reset/page.tsx` |
| `/u/[username]` | `web/app/u/[username]/page.tsx` |
| `/verify/[code]` | `web/app/verify/[code]/page.tsx` |
