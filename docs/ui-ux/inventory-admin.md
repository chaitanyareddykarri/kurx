# Kurx UI/UX Redesign — Admin Screen Inventory

Baseline generated in Phase 0.1. `Status` vocabulary: `Legacy` · `Foundation applied` · `Partially migrated` · `Redesigned` · `Verified` · `Blocked` · `N/A`. Every row starts `Legacy`. This file is regenerated only by hand — edit rows in place as work lands.

**Total: 27 routes** (`admin/app/**/page.tsx`), plus 29 non-visual route handlers.

> ✅ **Phase 26:** every console route is now role-scoped from `NAV` (20 of 24; the other four are the
> dashboard, analytics and the operator's own pages). Reachability audited in both directions — no
> orphans, no broken links. The five unbuilt nav items were removed; their map lives in
> `information-architecture.md` §9.
>
> ✅ **Phase 27 gave admin a test suite** — `npm test` (vitest + RTL, the same runner web uses; D-109 still excludes Playwright/Cypress/Storybook). It starts at 9 tests covering the shell and the Phase 26 role resolution. Screen-level coverage arrives with Phases 28–31; a recorded browser check is still what moves a row past `Redesigned`.

| Route | Area | Phase | File | Status | Responsive | A11y | Functional |
|---|---|---|---|---|---|---|---|
| `/` | Dashboard | 29.1 | `admin/app/(console)/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/account` | Admin auth & account | 29.8 | `admin/app/(console)/account/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/analytics` | Ops overview | 29.1 | `admin/app/(console)/analytics/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/audit` | System | 29.5 | `admin/app/(console)/audit/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/blacklist` | Moderation & comms | 29.7 | `admin/app/(console)/blacklist/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/broadcast` | Moderation & comms | 29.7 | `admin/app/(console)/broadcast/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/certificates` | Event catalog | 29.6 | `admin/app/(console)/certificates/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/competitions` | Event catalog | 29.6 | `admin/app/(console)/competitions/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/competitions/[stageId]` | Event catalog | 29.6 | `admin/app/(console)/competitions/[stageId]/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/events` | Events & review | 29.3 / 30 | `admin/app/(console)/events/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/events/pending` | Events & review | 29.3 / 30 | `admin/app/(console)/events/pending/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/events/review` | Events & review | 29.3 / 30 | `admin/app/(console)/events/review/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/forbidden` | Admin auth & account | 29.8 | `admin/app/forbidden/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/health` | Ops overview | 29.1 | `admin/app/(console)/health/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/login` | Admin auth & account | 29.8 | `admin/app/login/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/organizations` | People & orgs | 29.2 | `admin/app/(console)/organizations/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/platform/event-taxonomy` | Taxonomy | 29.4 | `admin/app/(console)/platform/event-taxonomy/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/reports` | Moderation & comms | 29.7 | `admin/app/(console)/reports/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/reset` | Admin auth & account | 29.8 | `admin/app/reset/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/risk` | Moderation & comms | 29.7 | `admin/app/(console)/risk/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/security` | Admin auth & account | 29.8 | `admin/app/(console)/security/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/speakers` | Event catalog | 29.6 | `admin/app/(console)/speakers/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/sponsors` | Event catalog | 29.6 | `admin/app/(console)/sponsors/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/staff` | System | 29.5 | `admin/app/(console)/staff/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/users` | People & orgs | 29.2 | `admin/app/(console)/users/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/users/[id]` | People & orgs | 29.2 | `admin/app/(console)/users/[id]/page.tsx` | Legacy | ☐ | ☐ | ☐ |
| `/verification` | Review queue | 30 | `admin/app/(console)/verification/page.tsx` | Legacy | ☐ | ☐ | ☐ |

## Non-visual route handlers

| Route | File |
|---|---|
| `/account` | `admin/app/(console)/account/page.tsx` |
| `/analytics` | `admin/app/(console)/analytics/page.tsx` |
| `/audit` | `admin/app/(console)/audit/page.tsx` |
| `/blacklist` | `admin/app/(console)/blacklist/page.tsx` |
| `/broadcast` | `admin/app/(console)/broadcast/page.tsx` |
| `/certificates` | `admin/app/(console)/certificates/page.tsx` |
| `/competitions/[stageId]` | `admin/app/(console)/competitions/[stageId]/page.tsx` |
| `/competitions` | `admin/app/(console)/competitions/page.tsx` |
| `/events` | `admin/app/(console)/events/page.tsx` |
| `/events/pending` | `admin/app/(console)/events/pending/page.tsx` |
| `/events/review` | `admin/app/(console)/events/review/page.tsx` |
| `/health` | `admin/app/(console)/health/page.tsx` |
| `/organizations` | `admin/app/(console)/organizations/page.tsx` |
| `/` | `admin/app/(console)/page.tsx` |
| `/platform/event-taxonomy` | `admin/app/(console)/platform/event-taxonomy/page.tsx` |
| `/reports` | `admin/app/(console)/reports/page.tsx` |
| `/risk` | `admin/app/(console)/risk/page.tsx` |
| `/security` | `admin/app/(console)/security/page.tsx` |
| `/speakers` | `admin/app/(console)/speakers/page.tsx` |
| `/sponsors` | `admin/app/(console)/sponsors/page.tsx` |
| `/staff` | `admin/app/(console)/staff/page.tsx` |
| `/users/[id]` | `admin/app/(console)/users/[id]/page.tsx` |
| `/users` | `admin/app/(console)/users/page.tsx` |
| `/verification/doc/[id]` | `admin/app/(console)/verification/doc/[id]/route.ts` |
| `/verification` | `admin/app/(console)/verification/page.tsx` |
| `/api/realtime-token` | `admin/app/api/realtime-token/route.ts` |
| `/forbidden` | `admin/app/forbidden/page.tsx` |
| `/login` | `admin/app/login/page.tsx` |
| `/reset` | `admin/app/reset/page.tsx` |
