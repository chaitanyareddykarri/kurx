# Kurx UI/UX Redesign — Web Screen Inventory

Baseline generated in Phase 0.1. `Status` vocabulary: `Legacy` · `Foundation applied` · `Partially migrated` · `Redesigned` · `Verified` · `Blocked` · `N/A`. Every row starts `Legacy`. This file is regenerated only by hand — edit rows in place as work lands.

**Total: 93 routes** (`find web/app -name page.tsx | wc -l`, re-measured 2026-08-18). D-389 deleted
`/host/events/[id]/representing` — representation is answered inside Create Event, and its correction
surface is a section of `/host/events/[id]/details`, not a route of its own. (D-382 had added that route;
the figure before it was 93, measured 2026-08-15, so neither arithmetic on the old numbers reproduces this
one — it is the measurement.) Plus **2** genuine non-visual route handlers (`route.ts`) listed at the
bottom.

> ⚠️ **This header said "88 routes, plus 90 non-visual route handlers" and both halves were wrong.**
> The bottom section titled *"Non-visual route handlers (no UI — excluded from redesign scope)"* held
> 90 rows of which **88 were `page.tsx`** — it was a second copy of this very table, so every page in
> the inventory was simultaneously listed as in scope and as a no-UI handler excluded from it. Only
> `/api/realtime-token` and `/host/admin/doc/[id]` were ever real route handlers, and the second no
> longer exists. The duplicate has been removed; the table below is the only inventory.
>
> Also corrected in the same pass: 9 rows pointed at pages deleted weeks earlier (`/host/admin/*`
> migrated to the admin console by [D-195](../DECISIONS.md), `/host/risk` and `/host/templates/*`
> removed), `/chats/[eventId]` was renamed `/chats/[roomId]` when a room stopped being event-bound
> ([D-264](../DECISIONS.md)), and **14 pages that shipped after the Phase 0.1 baseline were never
> added at all**. Re-derive the route list rather than trusting this count; the diff that found all
> of this is one `find` against the table.

| Route | Area | Phase | File | Status | Responsive | A11y | Functional |
|---|---|---|---|---|---|---|---|
| `/` | Public marketing | 13A · 25 | `web/app/(public)/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/about` | Public marketing | 13A · 25 | `web/app/(public)/about/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/allies` | Allies | 18A.4 | `web/app/(app)/allies/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/blog` | Public marketing | 13A · 25 | `web/app/(public)/blog/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/book/[slug]` | Booking | 19 | `web/app/(app)/book/[slug]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ (REG-009 — hand-off, not checkout) |
| `/certificates` | Certificates | 20C | `web/app/(app)/certificates/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/chats` | Messaging | 20B | `web/app/(app)/chats/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/chats/[roomId]` | Messaging | 20B | `web/app/(app)/chats/[roomId]/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ (renamed from `[eventId]`, D-264 — a room is no longer event-bound) |
| `/contact` | Public marketing | 13A · 25 | `web/app/(public)/contact/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/discover` | Discover | 13 | `web/app/(app)/discover/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/e/[slug]` | Event detail | 15 | `web/app/e/[slug]/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/features` | Public marketing | 13A · 25 | `web/app/(public)/features/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/groups` | Groups | 20C | `web/app/(app)/groups/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
| `/host` | Host dashboard | 21.1 | `web/app/(app)/host/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
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
| `/host/settings` | Host top-level | 21 | `web/app/(app)/host/settings/page.tsx` | **Redesigned** | ☑ | ☑ | ☑ |
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
| `/recover` | Auth | 16 | `web/app/(public)/recover/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/register` | Auth | 16 | `web/app/(public)/register/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/reset` | Auth | 16 | `web/app/(public)/reset/page.tsx` | Legacy | ☐ | ☐ | ☐ |
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

## Shipped after the Phase 0.1 baseline — not yet assessed

These 13 routes exist in `web/app` and were absent from the table above. They are listed with **no
status**: nobody has run the redesign/responsive/a11y pass against them, and inventing a value here
would be worse than an empty cell. `/chats/[roomId]` is the 14th missing route but carries its
predecessor's assessment, so it stays in the main table.

| Route | Area | File | Status | Responsive | A11y | Functional |
|---|---|---|---|---|---|---|
| `/assignments` | Event assignments | `web/app/(app)/assignments/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/categories/[id]` | Discover | `web/app/(app)/categories/[id]/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/id-cards` | ID cards (D-331) | `web/app/(app)/id-cards/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/id-cards/[cardId]` | ID cards (D-331) | `web/app/(app)/id-cards/[cardId]/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/login` | Auth | `web/app/(public)/login/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/org-invitations` | Organizations | `web/app/(app)/org-invitations/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/participations` | User events | `web/app/(app)/participations/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/points` | Gamification | `web/app/(app)/points/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/settings/blocked` | Settings (D-263) | `web/app/(app)/settings/blocked/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/settings/help` | Settings (D-263) | `web/app/(app)/settings/help/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/settings/legal` | Settings (D-263) | `web/app/(app)/settings/legal/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/settings/profile` | Settings (D-263) | `web/app/(app)/settings/profile/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |
| `/waitlist` | Waitlist | `web/app/(app)/waitlist/page.tsx` | *Not assessed* | ☐ | ☐ | ☐ |

## Non-visual route handlers (no UI — excluded from redesign scope)

There are **two**, and only these two are genuinely non-visual. The 90-row list that used to sit
here was a duplicate of the main table (88 of its rows were `page.tsx`); see the warning at the top.

| Route | File |
|---|---|
| `/api/realtime-token` | `web/app/api/realtime-token/route.ts` |
| `/api/ticket-qr/[code]` | `web/app/api/ticket-qr/[code]/route.ts` |

*(`/host/admin/doc/[id]/route.ts`, the other handler this section once listed, was removed with the
rest of `host/admin/*` when D-195 migrated trust/safety to the admin console.)*
