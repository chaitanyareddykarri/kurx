# Kurx UI/UX Redesign — Information Architecture

Phase 2 output. Defines the structural map every later phase designs against, so structure is
settled once rather than re-decided per screen.

**Routes are preserved.** This document changes *how a surface is reached and grouped*, not what it
is addressed as. The only route changes proposed are additions that fix links which currently 404,
plus deletions of proven-dead routes in Phase 49.

---

## 1. Reachability audit — the finding that drives this phase

A static link analysis over all 88 web routes (every `href` / `redirect` / `router.push` in
`web/app`, `web/components`, `web/lib`, with dynamic segments normalised) produced two lists.

### 1.1 Confirmed broken internal links — these 404 today

| Link | Emitted by | Correct target | Severity |
|---|---|---|---|
| `/account/security?recovered=1` | `web/components/auth/recovery-panel.tsx:50` | `/settings/security` | **S1** |
| `/events/{event_slug}` ×3 | `invitations/page.tsx:72`, `:100`, `redeem-invite-link.tsx:40` | `/e/{slug}` | **S1** |
| `/host/templates` | `workspace/page.tsx` | no index page exists | **S2** |

Both S1s land on primary journeys at their worst possible moment:

* **Account recovery.** A user who has just proved their identity through the recovery flow is
  pushed to a route that does not exist. The last step of a security-critical journey is a 404.
* **Invitation → event.** Every link from an invitation to the event it invites you to is broken.
  `event_slug` is a real, non-null field on the invitation schema (`web/lib/api.ts:1086`); the
  route is simply wrong — web serves event detail at `/e/[slug]`, not `/events/[slug]`.

These are pre-existing defects, not redesign regressions. Recorded in the Regression Ledger as
`REG-001`–`REG-003` and fixed in their owning phases (16, 20D, 21) rather than as a drive-by.

### 1.2 Routes with no navigation path (18 confirmed)

The event-workspace tabs initially appeared orphaned; they are not — `host/events/[id]/layout.tsx`
builds them dynamically from live capabilities. **False positive, verified and dismissed.**

After that correction, these remain genuinely unreachable through any UI:

| Group | Routes | Verdict |
|---|---|---|
| Host dashboard | `/host` | Orphaned — `/workspace` replaced it (D-267) but `/host` was never removed |
| Host org-wide pages | `/host/{announcements,attendees,certificates,forms,invitations,notifications,payouts,risk,settings,tickets}` (10) | Orphaned — superseded by the per-event workspace tabs |
| Host trust & safety | `/host/admin/{claims,fraud,orgs,verifications}` (4) | Orphaned by D-195 — migrated to `admin/` |
| Host templates | `/host/templates/{certificates,editor,invites}` (3) | Placeholders; `/templates` is backend-blocked |
| Auth | `/onboarding` | **Dead** — `session.ts:49` and `otp-panel.tsx:40` both route to `/register` instead |

`/i/[token]` also appears unlinked and is a **false positive**: invite links arrive by email/SMS,
so no internal link is expected.

**Consequence for the roadmap:** Phase 21's real scope is smaller than its route count suggests —
17 of its 37 routes are orphans or placeholders. Phase 17 (Onboarding) has **no live surface of its
own**; the first-run experience is `/register`. Both are corrected in the phase records.

---

## 2. Primary navigation

Web and mobile already teach the same five areas, and that alignment is deliberate and correct
(`app-shell.tsx` and `app_shell.dart` both say so). **Keep it.**

| # | Area | Web | Mobile | Purpose |
|---|---|---|---|---|
| 1 | **Home** | `/discover` | Home tab | Event discovery — the default surface |
| 2 | **Community** | `/allies` | Community tab | People: allies, suggestions, search |
| 3 | **Posts** | `/posts` | Posts tab | Social feed |
| 4 | **Messages** | `/chats` | Messages tab | Event chat rooms |
| 5 | **Workspace** | `/workspace` | Workspace tab | The user's own events — attending *and* hosting |

**Fixed corners** (not nav entries, on both surfaces): **Profile** top-left, **Notifications**
top-right. These are places you visit and return from, not places you dwell.

### Changes this phase makes

1. **Notifications gets an unread indicator.** Today it is an unlabelled icon with no state, so
   there is no way to know something is waiting without visiting (audit S3-4). Phase 11 / 33.
2. **Both corners meet the 44 px touch floor.** Currently `p-2` around an 18 px icon = 32 px.
3. **Workspace absorbs hosting.** It already lists the caller's own events and links to
   `/host/events/{id}`. Making that explicit is what retires the 11 orphaned `/host/*` org-wide
   pages without inventing a sixth nav area.

---

## 3. Secondary navigation

Reached *through* an area, so ranked below the rule rather than competing with the five.

| Item | Route | Belongs to |
|---|---|---|
| Invitations | `/invitations` | Workspace |
| My Tickets | `/tickets` | Workspace |
| Saved | `/saved` | Home |
| Groups | `/groups` | Community |
| Create event | `/host/events/new` | **Profile** (D-305) |
| Settings | `/settings` | Profile |

**Change (superseded on one point by [D-305](../DECISIONS.md), 2026-08-08):** *Create event* is the only
host entry point in the entire shell, which is why 11 host pages went orphaned. Phase 21 makes
**Workspace** the host home — event list, then per-event workspace.

> ⚠️ **D-305 moves the *Create event* entry point to Profile**, and puts a verification/eligibility gate
> — including the Public/Private decision — in front of the creation form. Workspace keeps the event
> **list** (participated + approved created) and remains the route to each **Event Host Workspace**, but
> it is an activity hub, **not** the creation door and **never** an organizer dashboard. The three
> surfaces are defined canonically in
> [`../architecture/TERMINOLOGY.md`](../architecture/TERMINOLOGY.md) §"The three surfaces".

---

## 4. Page hierarchy

Three levels, no deeper. Anything that wants a fourth is a tab or a sheet.

```
L0  Area            /discover · /allies · /posts · /chats · /workspace
L1  Section         /workspace/[eventId] · /posts/saved · /settings/security
L2  Detail          /host/events/[id]/tickets  (tabbed workspace)
```

The event workspace is **L2 with tabs**, not L3 pages — it already works this way and the
capability-driven tab list is the right model. Sheets (`Sheet`) carry detail that must not lose the
list behind it; that is the admin workspace pattern and it stays.

---

## 5. Event taxonomy

Governed by the backend and by **D-188 (Platform Taxonomy Management)** — admin manages
Audience / Category / Type through `/platform/event-taxonomy`. The redesign **consumes** this
taxonomy and does not invent a parallel one.

Client-side surfaces of it:

| Concept | Web | Mobile | Source |
|---|---|---|---|
| Category | `kind-chips.tsx`, `/discover` | `category_chips.dart`, `categories_page.dart` | `GET` taxonomy |
| Kind | `kind-chips.tsx` | `kind_chips.dart` | same |
| Section | discovery rails | `event_section.dart`, `event_section_list_page.dart` | `home_feed` |

**Rule for Phases 12–14 and 34:** filter chips render taxonomy the server returns. No hardcoded
category list may enter the clients.

---

## 6. Search & discovery structure

One search concept, three depths:

```
Quick      header search field        → suggestions + recents        (Phase 12)
Results    /discover with filters     → grid/list + facets           (Phase 14)
Browse     /categories, sections      → taxonomy-led entry           (Phase 12)
```

Facets, in priority order for an India-facing event product: **date → location → category →
price → availability**. Date first because event intent is overwhelmingly time-anchored.

Filter state lives in the URL so a filtered search is shareable and back-navigable — this is an
explicit requirement for Phase 14 and today is not guaranteed.

---

## 7. Account & profile structure

| Surface | Route | Audience |
|---|---|---|
| Own profile | `/profile` | self |
| Public profile | `/u/[username]` | anyone |
| Organization profile | `/o/[slug]` | anyone |
| Settings hub | `/settings` | self |
| ├ Account | `/settings/account` | |
| ├ Identity | `/settings/identity` | |
| ├ Security | `/settings/security` | ← **recovery must land here** (§1.1) |
| ├ Privacy | `/settings/privacy` | |
| ├ Notifications | `/settings/notifications` | |
| └ Representing | `/settings/representing` | |

`/settings/representing` **is a redirect to `/host/representing`, not a second door** — corrected
2026-08-18 against `web/app/(app)/settings/representing/page.tsx`, which is a bare `redirect()`.

The proposal above was to keep both and make the distinction visible in the copy; the code took the
other option and collapsed them, on the reasoning written into that file: representing is the
*authority to act for an organization*, which belongs with hosting rather than with how your account
behaves. The route survives only so existing links and bookmarks keep working. One concept, one
surface — there is no copy to disambiguate.

---

## 8. Host structure (not "organizer" — D-271)

```
Workspace  /workspace                    my events, attending + hosting
  └ Event  /workspace/[eventId]          attendee view of one event
Host
  ├ Create        /host/events/new       the wizard
  ├ Event         /host/events/[id]      tabbed workspace, capability-driven
  │   └ tabs      overview · readiness · representing · details · schedule · people · media ·
  │               tickets · registrations · attendees · check-in · badges ·
  │               certificates · announcements · invitations · chat · team ·
  │               analytics · reviews
  └ Representing  /host/representing     which institutions I represent
      ├ new       /host/representing/new  request one Kurx has not verified yet (D-074/D-075)
      └ finance   /host/representing/[orgId]/finance
```

There is **no Representing tab** (D-389). Representation — the organization the event is run on behalf
of, its registration if it is not on Kurx yet, and that event's own authorization letter — is entered once
on **Create Event ▸ Representing** and nowhere else. **Readiness** reports the resulting state and
collects nothing; when a reviewer rejects the letter or asks for changes, the form appears on **Details**
(Edit Event) at `#representing`, gated on that verdict. Mobile mirrors it exactly: the wizard's step,
and `/events/:eventId/edit/representing` reached from Event Status for the same verdict — `/manage/representing`
is deleted. `badges` is D-362.

The tab list is **derived from live capabilities**, never hardcoded — `layout.tsx` gates each group
on `events:update`, `attendees:view`, `analytics:view`, `volunteers:view`. Phase 21 preserves that
exactly; a redesign that hardcodes tabs would silently grant navigation the backend denies.

Everything else under `/host/*` is orphaned or placeholder (§1.2).

---

## 9. Admin / reviewer structure

`admin/components/layout/nav-config.ts` encodes a role-gated IA. The shape is sound. Phase 26
resolved the three items this section raised:

**1. The five `ready: false` items are removed** ✅ *(Phase 26)*

Payments, Payout Approvals, Refunds, Templates and Background Jobs had rendered as disabled
`<span>`s since Phase 1 — `text-muted/50` (well under any contrast floor), a permanent "soon"
badge, and `title="Coming in a later phase"`, a note between engineers delivered to operators
through a tooltip no keyboard or touch user can reach. None could ship: none has a route, and
Templates is backend-blocked (`GET /v1/templates` lists only `Published`, so drafts are unlistable
and the screen cannot exist until an admin inventory endpoint does).

The map is preserved here rather than in the sidebar, which is where a plan belongs:

| Planned area | Roles | Blocked on |
|---|---|---|
| Payments | FinanceOps | screen not built |
| Payout Approvals | FinanceOps | screen not built |
| Refunds | FinanceOps | screen not built |
| Templates | SuperAdmin | **backend** — no admin inventory endpoint (see above) |
| Background Jobs | SuperAdmin | screen not built |

**2. Two doors onto event review** — unchanged, and still Phase 30's. `/verification`,
`/events/pending`, `/events/review` and `/events` overlap; D-186 kept the focused pending-only queue
beside the full workspace deliberately, so 30 must make which-to-use obvious rather than merge them.

**3. Breadcrumbs** — the topbar already resolves an active crumb by longest-href match. Phase 27
(Admin Shell) owns whether that is enough for a console three levels deep.

### The finding Phase 26 added — the IA was declared and never enforced

Role gating is `PlatformRole` + a `SuperAdmin` bypass. `NAV` gates every destination by role, with
per-item reasoning (Certificates is SuperAdmin because those endpoints check `kurx_admin`, and "a
Reviewer would see the screen and get a 403 they cannot resolve"). **Every console page guarded with
`requireStaffSession()` alone**, which asks only whether the caller holds *some* platform role — so
a Support admin who typed `/staff` reached it, and a Reviewer who typed `/certificates` reached a
screen whose every call the backend then refuses.

Nothing leaked. The backend is the authority and refuses those calls, which is exactly the principle
below. But hiding a destination and then serving it is the contradiction those comments were written
to prevent.

`requiredRolesFor(pathname)` now resolves a route's roles **from `NAV` itself** — one source of truth
for what an operator can see and what they can reach — and the console layout renders `RoleRequired`
in place of the page when they do not match. Longest-href match, so `/users/[id]` inherits `/users`
and `/events/review` inherits `/events`. Measured across all 24 console routes: **20 are now
role-scoped; before, every one was open to any staff.**

`RoleRequired` is deliberately distinct from `/forbidden`, which answers "this account is not Kurx
staff" and signs you out. This one answers "you are staff, but not for this", keeps the shell so the
operator can go somewhere they *can* use, and names the role needed. It does not hide behind D-018's
404-not-403 rule: that exists to stop resource *existence* leaking to outsiders, and every reader
here is already staff looking at a sidebar that openly lists these areas.

**The redesign renders what the backend permits; it never decides permission** (D-283 for auth; same
principle here). This change does not move that line — the backend still decides, and this only stops
the console contradicting itself about where it will take you.

---

## 10. Mobile navigation

Five tabs, matching web's five areas (§2), in a floating pill bar (D-066). Keep.

Structural fixes owned by Phase 33:

* **The 80 px content inset is a magic number.** `app_shell.dart` pads content by a hardcoded
  `EdgeInsets.only(bottom: 80)` to clear a bar whose real height depends on safe-area inset and
  text scale. Must be measured, not assumed (audit S2-7).
* **Back behavior** across nested routes and tab switches needs one stated rule.
* **18 `SafeArea` for 98 `Scaffold`** — the shell should own insets so pages need not.

---

## 11. Terminology

`docs/architecture/TERMINOLOGY.md` is canonical (D-271). Load-bearing for this redesign:

| Use | Never |
|---|---|
| A **User** owns an Event | "organizer account", "owner" |
| The organization an event **represents** | "the event's organization" |
| **Workspace** — a person's own events | "organizer dashboard" |
| **Host** — the acting-on-an-event surface | "organizer area" |
| **Representative** | "org admin", "org owner" |

Phase 43 audits every visible string against this table. It does not introduce new vocabulary.

---

## 12. What this phase deliberately does **not** change

* No route renamed or removed. (Deletions are proposed for Phase 49, after proof.)
* The five-area model — web and mobile already agree, and that is the redesign's strongest existing
  asset.
* The capability-driven event tab list.
* Admin's role-gated group structure.
* The taxonomy, which is backend-owned.
