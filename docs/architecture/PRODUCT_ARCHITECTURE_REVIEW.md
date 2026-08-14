# Kurx — Product Architecture Review

> **Status: PROPOSAL. Nothing here is shipped.** This document does not describe current reality and
> must not be read as if it does — `docs/ui-ux/information-architecture.md` (structure),
> `docs/architecture/PLATFORM_FLOW_MAP.md` (flow) and `docs/DECISIONS.md` (why) remain authoritative
> for what exists. Every claim about *current* behaviour below carries a `file:line` citation and was
> read in source, not inferred from documentation. Every claim about a *proposed* state is marked as
> such. Items requiring a `D-NNN` before implementation are listed in §12 and were deliberately **not**
> written into `DECISIONS.md` — a concurrent session owns that file and D-numbers collide.

**Reviewed:** `docs/ui-ux/information-architecture.md` (the Product Flow document), against
`web/`, `admin/`, `mobile/` and `backend/` as built on `feat/messages-settings`, 2026-08-07.

---

## 0. Method

The review question was not "is the document internally consistent" — it is, which is why its defects
survived. The question was **"does the document describe the system, and is the system it describes
correct?"** Both were tested by reading the implementing code for every claim.

That distinction produced the central finding: **three of the seven concerns raised are premised on
the document, and the document disagrees with the code.** Answering them as asked would have
codified the document's error into the build. Those three are challenged in §5, §6 and §8 before
being answered.

---

## 1. Verdict on the current document

The document is a **competent structural audit mis-scoped as an architecture**. Its §1 reachability
audit is genuinely good work — it found real S1 defects on real journeys. But from §2 onward it
commits one repeated error:

> **It documents where screens currently sit, and then ratifies that placement as the architecture.**

§12 states this outright: *"What this phase deliberately does not change — no route renamed or
removed."* A structural document that may not restructure is an inventory. The consequence is that
every misplacement in the codebase was promoted to a documented decision, and the document now
*defends* the defects it should have found.

Three examples, all confirmed in code:

| §  | Document says | Code says |
|---|---|---|
| §3 | Settings *belongs to* Profile | `web/app/(app)/profile/page.tsx` **redirects to** `/settings`; `settings/page.tsx:27` renders `<h1>Profile</h1>`. Settings absorbed Profile — the opposite relationship. |
| §7 | `/settings/representing` and `/host/representing` are "two doors onto the same concept. **Keep both**" | Two routes, one responsibility, and the document's own remedy is *copywriting* ("make the distinction visible in the copy"). Prose cannot fix duplicated ownership. |
| §2 | The five-area model is "the redesign's strongest existing asset" | True, and untouched by this review. The five areas are correct. Everything *reached through* them is not. |

**The document is structurally correct at L0 and unsound at L1.** The proposal below keeps §2 and §5
(taxonomy) essentially intact and rebuilds §3, §7 and §8.

---

## 2. The three root defects

Everything in §3–§9 reduces to these. They are stated first because fixing the symptoms
individually — which is what the seven concerns describe — would leave all three intact.

### RD-1 · There is no stated rule for what owns a surface

Placement is historical, not principled. `/tickets` sits in `secondaryNav` on web
(`nav-config.ts:48`) and at a top-level `Routes.tickets` on mobile (`app_router.dart:123`), reached
from Profile per `app_shell.dart:15`. Neither is wrong against any written rule, because there is no
written rule. **Every navigation question in this review is unanswerable until one exists.**

The rule this proposal adopts, and applies without exception:

> **Ownership follows the question the surface answers, and each question has exactly one owner.**
>
> - **"Who am I, to others?"** → Profile *(public projection, derived, read-only)*
> - **"What am I doing?"** → Workspace *(my events and my participation, operational)*
> - **"How does my account behave?"** → Settings *(self-only control, authored)*
> - **"What is happening now?"** → Home / Community / Posts / Messages *(the live surfaces)*

A surface answering two questions is split. A question with two surfaces is merged. No exceptions,
because the exceptions are what produced `secondaryNav`.

### RD-2 · "Capability" names two unrelated systems

This is the most dangerous defect in the codebase and the document does not mention it once.

| | Capability **Engine** | **Entitlements** |
|---|---|---|
| Code | `Kurx.Infrastructure/Events/CapabilityResolver.cs` | `Kurx.Infrastructure/Trust/TrustService.cs` |
| Question | *What does this event archetype support?* | *What is this person allowed to do?* |
| Input | Product → Archetype → Matrix → Dependencies | Identity verification + bank + fraud |
| Enforces | **Nothing, by design** (D-266) | The paid-event and payout gates |

Both are called "capabilities" in code, in the API (`/v1/capabilities`,
`/v1/orgs/{id}/workspace-capabilities`) and in the clients (`web/lib/capabilities.ts`). They are
opposite in kind: one *describes*, one *authorizes*. Concern #6 — "capabilities must depend on
verification" — is correct for entitlements and **would be a defect if applied to the engine**
(§9).

### RD-3 · Event management exists three times under three names

| Surface | Route | Scope |
|---|---|---|
| Web | `/workspace`, `/workspace/[eventId]` | my events; attendee view of one |
| Web | `/host/events/[id]` + 17 tabs | management of one |
| Mobile | `/workspace`, `/workspace/:eventId` | my events |
| Mobile | `/events/:eventId/manage/*` (18 routes) | management of one |

One responsibility, three vocabularies (`workspace` / `host` / `manage`), split differently on each
surface. This — not Profile — is what Workspace actually conflicts with (§7).

---

## 3. Q1 · "Secondary" navigation

### Finding: it is not a navigation section, and none of the offered names fit

`secondaryNav` (`web/components/layout/nav-config.ts:43-53`) holds six items:

```
Invitations · My Tickets · Saved · Groups · Create event · Settings
```

Three facts decide this:

1. **It already has two different names on two viewports.** `sidebar-nav.tsx:55` renders it as a
   list labelled `"Secondary"`; `mobile-nav.tsx` renders the identical array behind a button
   labelled **"More"**, inside a `Sheet` titled **"More"**, under `aria-label="Secondary"`. A screen
   reader user on a phone hears "Secondary"; a sighted user reads "More". Same list, three labels.

2. **The document's own §3 table assigns the six items to four different owners** — Workspace, Home,
   Community, Profile. A group whose members belong to four other groups is not a group.

3. **"Create event" is a verb in a list of nouns.** The document (§3) already identifies this as the
   cause of 11 orphaned `/host/*` pages.

So the question "what should Secondary be called?" is the wrong question. Every candidate name —
Secondary Navigation, More Menu, Drawer, Quick Access — is a *container* name, and naming a
container is what let unrelated items accumulate in it. **Profile Menu** is the only candidate that
names a *responsibility*, and it fits exactly one of the six items.

### Proposal: delete the group; distribute by RD-1

| Item | Goes to | Why |
|---|---|---|
| **My Tickets** | Workspace → *Attending* | A ticket is participation. It is already what `/workspace` counts as `participating` (`workspace/page.tsx:48`) but does not list. |
| **Invitations** | Workspace → *Invitations* | Pre-participation. Adjacent to a ticket, which is the document's own reasoning at `nav-config.ts:44-46`. |
| **Groups** | Workspace → *Groups* | A group is an event registration unit (`Group.EventId`, `Group.OrderId` — `Domain/Entities/Orders.cs`). It is not a Community/social object; the document put it under Community on the strength of the word alone. |
| **Saved** | Home | Saved events are discovery state. `SavedEvent` is `(UserId, EventId)` — a bookmark on the discovery surface. |
| **Create event** | Workspace → primary action | Already the document's §3 intent; this makes it structural rather than a list row. |
| **Settings** | Account menu (top-left) | §6. |

**Result: `secondaryNav` ceases to exist.** The desktop sidebar renders the five areas; the mobile
bar renders the five areas and drops the "More" button entirely. The top-left corner — already
reserved for Profile on both surfaces (IA §2, `app_shell.dart:11`) — becomes the **Account menu**,
which is a menu with a stated membership rule rather than an overflow bucket.

**Account menu contents** (the complete list, no overflow permitted):

```
[avatar] ─┬─ View public profile      → /u/{username}
          ├─ Edit profile             → /settings/profile
          ├─ Settings                 → /settings
          ├─ Verification & limits    → /settings/verification     (§8)
          └─ Sign out
```

---

## 4. Q2 · Settings vs Profile

### The premise is inverted, and both surfaces are wrong in opposite directions

The concern states *"Settings currently exists outside Profile."* On web the reverse is true:

- `web/app/(app)/profile/page.tsx` is a **redirect to `/settings`**, with the comment: *"This page
  was a byte-for-byte duplicate of /settings … no nav link points here (D-212)."*
- `web/app/(app)/settings/page.tsx:27` renders `<h1>Profile</h1>` and hosts `ProfileForm` +
  `PrivacyForm`.

**Web already merged them — into Settings.** Mobile made the opposite choice, and split
inconsistently:

| Concern | Web | Mobile |
|---|---|---|
| Profile edit | `/settings` | `/profile/edit` |
| Identity verification | `/settings/identity` | `/profile/identity` |
| Privacy | `/settings/privacy` | `/profile/privacy` |
| Membership claims | — | `/profile/membership-claims` |
| Devices | `/settings/security` | `/profile/devices` |
| Account | `/settings/account` | `/settings/account` |
| Security | `/settings/security` | `/settings/security` |
| Notifications | `/settings/notifications` | `/settings/notifications` |
| Blocked users | — | `/settings/blocked` |
| Delete account | *(inside account)* | `/settings/delete-account` |
| Representing | `/settings/representing` | `/representing` |

*(mobile paths from `mobile/lib/core/router/app_router.dart:106-136` and its `GoRoute` literals)*

Identity, privacy and devices land under **Profile on mobile and Settings on web**. Everything else
agrees. This directly violates IA §2's founding principle that the two surfaces teach one map — and
the document does not notice, because §7 documents web only.

### Proposal: neither merge. Split on visibility, which is the only durable line

Merging in either direction is what produced the duplication. The stable rule:

> **Profile is what others can see. Settings is what only I can see.**
>
> Profile is therefore *derived and read-only*. Settings is *authored*. A screen that writes is
> never Profile, even when it writes profile fields.

| | Profile | Settings |
|---|---|---|
| Route | `/u/{username}` — one canonical profile, the public one | `/settings/*` |
| Audience | anyone | self only |
| Content | derived: attended events, certificates, allies, achievements, verified affiliations | account, security, privacy, notifications, verification, representing |
| Writes | none | all |

This resolves the duplication rather than relocating it: there is **one profile view** (public,
derived) and **one profile editor** (`/settings/profile`, authored). `/profile` keeps redirecting —
to `/u/{username}` for a user who has claimed one, `/settings/profile` for one who has not.

**Consequences**

- `settings/page.tsx` stops being titled "Profile" and becomes a real hub; its form moves to
  `/settings/profile`.
- Mobile moves `identity`, `privacy`, `devices`, `membership-claims` from `/profile/*` to
  `/settings/*`, matching web and giving mobile one settings container instead of two.
- `PrivacyForm` moves out of the profile editor to `/settings/privacy`, where the same controls
  already exist on both surfaces — today web renders privacy in **two** places
  (`settings/page.tsx` and `settings/privacy/`).

---

## 5. Q3 · Workspace vs Profile

### Partly agreed — but the stated conflict is not the real one

**Web's Workspace is not polluted by Profile.** `web/app/(app)/workspace/page.tsx:23-28` already
implements exactly the requested model — status filters over one flat event list, with an explicit
comment that this is the point of D-267:

```
VIEWS = Hosted Events · Drafts · Pending Approval · Archived
```

The genuine gaps against the requested list are four: **Live**, **Completed**, **Participated** (a
count at `:48`, never a list) and **Certificates/Analytics**.

**The real conflict is RD-3: Workspace vs Host vs Manage.** Concern #3 asks to move operational
management *out of Profile*; it is not in Profile. It is in `/host/*`, in `/workspace/*`, and on
mobile in `/events/:id/manage/*` — three names for one thing.

### Proposal: Workspace is the only operational surface; `/host` is retired

```
/workspace                        my events — one list, filtered
  ├── ?view=hosted|drafts|pending|live|completed|archived
  ├── ?view=attending             ← My Tickets folds in here
  ├── ?view=invitations
  ├── ?view=groups
  └── /workspace/{eventId}        ONE event, tabs derived from live capabilities
        ├── overview · readiness · details · schedule · people · media
        ├── tickets · registrations · attendees · check-in
        ├── announcements · invitations · certificates · chat
        └── team · analytics · reviews
```

`/workspace/{eventId}` replaces `/host/events/{id}` and mobile's `/events/{id}/manage/*`. The tab
list stays **capability-derived** — `host/events/[id]/layout.tsx` builds it from
`getWorkspaceCapabilities` (`web/lib/capabilities.ts:34`), and IA §8 is right that hardcoding it
would grant navigation the backend denies. That property is preserved exactly.

**On the two items the concern places at Workspace root:**

- **Certificates Management** is per-event (issuing) and already correct as a tab. The *other*
  certificates surface — `/certificates`, "certificates I have earned" — is **derived identity** and
  belongs to Profile under RD-1. Two different things share one word; keep both, at different
  owners.
- **Analytics** is per-event and already a tab. The org-wide `/host/analytics` is one of the 11
  orphans and should be deleted, not relocated — no navigation has ever pointed at it.

**Retired by this move** (all already orphaned per IA §1.2, so the deletion is bounded):
`/host`, and the 10 org-wide `/host/{announcements,attendees,certificates,forms,invitations,
notifications,payouts,risk,settings,tickets}`. `/host/representing` merges into
`/settings/representing` (§10), and `/host/templates/*` is backend-blocked and stays parked.

---

## 6. Q4 · Account verification architecture

### The proposed chain is not implementable as specified, on three counts

The requested sequence is:

```
Registration → Email Verification → Phone OTP → Profile Completion → Identity
→ KYC → PAN → Bank → Penny Drop → Capability Eligibility → Feature Unlock → Home
```

**(a) Email-before-phone inverts the identity model.** Phone OTP *is* registration:
`AuthService.VerifyOtpAsync` (`Kurx.Infrastructure/Auth/AuthService.cs:82-90`) creates the `User`
row on first successful verification. Email is optional and explicitly **not** an auth factor —
`Domain/Entities/Users.cs:39`: *"A verified email is a recovery-and-notification channel, never on
its own an authentication factor."* Email verification cannot precede phone OTP because there is no
account to attach it to.

**(b) "Identity Verification" and "KYC" are the same step.** D-048 renamed them apart deliberately:
`UserIdentity` (`Domain/Entities/Identity.cs:14`) *is* person KYC, at `/v1/me/identity`; "KYC" now
means only the org's financial verification at `/v1/orgs/{id}/kyc/*`. Listing both as sequential
steps would build two screens for one aggregate.

**(c) A linear chain to Home destroys the attendee funnel.** `TrustService.cs:25` sets
`canOrganizeFree = true` for *any* account — the design intent is that a person can attend and host
free events with nothing but a phone number. PAN and bank exist to unlock **taking money**. Placing
them before Home would gate the majority of users behind financial KYC they will never need.

### Proposal: verification is a just-in-time ladder, not an onboarding pipeline

Two flows, not one. This is the shape the backend already implements; only the document and the
client surfaces are missing.

**Flow A — Onboarding (mandatory, ~30 seconds, terminates at Home)**

```
Phone entry → OTP verify ─┬─ existing user ──────────────► Home
                          └─ new user → Onboarding ──────► Home
                                        (name, username)
                            optional, skippable, prompted later:
                            email · avatar · headline
```

Grounding: `AuthService.VerifyOtpAsync` returns `IsNewUser`; `User { Phone, Name = "" }` at
`AuthService.cs:87` with the comment *"Name/username are collected by the onboarding screen after
first login (D-011/D-037)"*. Mobile implements this (`Routes.onboarding`, redirect at
`app_router.dart:168-170`). **Web's `/onboarding` is dead** — IA §1.2 confirms `session.ts:49` and
`otp-panel.tsx:40` both route to `/register` instead. That is a real gap this proposal closes.

**Flow B — Verification ladder (optional, lazy, entered from the capability that needs it)**

```
                     ┌──────────────────────────────────────────┐
  L1  Phone          │ every account. can_organize_free = true   │
                     └────────────────────┬─────────────────────┘
                                          │ triggered by: "create a PAID event"
                     ┌────────────────────▼─────────────────────┐
  L2  Person KYC     │ Govt ID  →  PAN  →  Bank                 │
                     │ POST /v1/me/identity/{government-id,pan,bank}
                     │ stores last-4 ONLY (Identity.cs:22)      │
                     └────────────────────┬─────────────────────┘
                                          │ AND (not OR)
                     ┌────────────────────▼─────────────────────┐
  L3  Org financial  │ PAN match → Penny drop → reviewer approve │
                     │ POST /v1/orgs/{id}/kyc/{pan,bank}         │
                     │ KycKind { PennyDrop, PanMatch, Digilocker }
                     └────────────────────┬─────────────────────┘
                                          ▼
                            paid events · wallet · payouts
```

**The ladder is entered from the blocked action, never from a menu.** A user meets it the moment
they choose a paid ticket type, with the reason stated. `/settings/verification` (§3) exists as a
status page — "here is what you have proved and what it unlocks" — not as the entry point.

---

## 7. Q5 · Financial verification as a module

### Agreed that it needs to be a module. It must be **two**, because the money has two owners

A single flattened Financial Verification module would model the wrong owner. The code has two
aggregates, and the distinction is load-bearing:

| | Person financial | Organization financial |
|---|---|---|
| Aggregate | `UserIdentity` (`Identity.cs:14`) | `OrgBankVerification` (`Orgs.cs:135`) |
| Route | `/v1/me/identity/{pan,bank}` | `/v1/orgs/{id}/kyc/{pan,bank}` |
| Stores | `PanLast4`, `BankLast4`, `GovtIdLast4` | `KycKind`, `KycStatus`, `PayloadJson` |
| Penny drop | **absent** | `KycKind.PennyDrop` (`Enums.cs:149`) |
| Gates | `CanOrganizePaid`, `CanReceivePayout` | `IsOrgVerified` |

**Money settles to an organization, never to a person.** `LedgerEntry.OrgId`, `OrganizationWallet`
and `PayoutSchedule` are all keyed non-nullably on an org — this is exactly the reason D-273a
records for `Event.RepresentingOrgId` being non-nullable (`Domain/Entities/Events.cs:82-93`): *"a
null representation would leave a captured payment with nowhere to settle."* So org financial
verification is the one that actually unlocks settlement, and collapsing it into a person-level
module would misplace the payout gate.

### Proposed module: `Financial Verification`, two tracks, one status surface

```
Financial Verification
├── Person track   → /settings/verification/identity
│   ├── Government ID    (DigiLocker | Aadhaar offline | passport | DL)
│   ├── PAN              → PanLast4
│   ├── Bank account     → BankLast4
│   └── ⚠ Penny drop     GAP — no person-level equivalent of KycKind.PennyDrop
│
├── Organization track → /settings/representing/{orgId}/financial
│   ├── PAN match        KycKind.PanMatch
│   ├── Account holder   ⚠ GAP — name-match is not modelled separately
│   ├── Penny drop       KycKind.PennyDrop
│   └── Reviewer approval → OrgVerificationStatus.Verified
│
└── Status & remediation (shared)  → /settings/verification
    ├── per-component state, per-track
    ├── failure reason + the specific action that clears it
    ├── retry           → UserIdentity.SubmitCount (Identity.cs:33) already rate-limits
    └── what each unlock grants (rendered from the §9 dependency table)
```

**Two real gaps this exposes**, both in the concern's list and neither in the code:

1. **Person-level penny drop does not exist.** `UserIdentity.BankLast4` is set on submission; there
   is no `PennyDrop` equivalent for a person's own account, while the org track has one. A person
   receiving an individual payout is verified more weakly than an org.
2. **Account-holder-name validation is not modelled anywhere.** Neither aggregate stores a
   name-match result. `KycKind` has no value for it. This is the check that catches a PAN and a bank
   account belonging to different people — the highest-value control in the set.

**Also true and unavoidable:** `MockKycProvider` is the only `IKycProvider` in the codebase. Every
state above is reachable only through a mock today. The module design is correct to build; the
verification it performs is not real until a provider ships.

---

## 8. Q6 · Capability dependencies

### The dependency already exists. Applying the concern literally would break a locked decision

Per RD-2, "capability" names two systems, and the concern is right about one and dangerous for the
other.

**Already implemented — `TrustService.cs:25-31`, verbatim:**

```csharp
var canOrganizeFree  = true;                                        // any account
var canOrganizePaid  = identityVerified && bankVerified && fraudClear;
var canReceivePayout = bankVerified;
```

with `identityVerified = PanLast4 != null || GovtIdLast4 != null` and
`bankVerified = BankLast4 != null` (`:23-25`). Enforced at both money gates:

- `OrderService.cs:154-157` — `!caps.CanOrganizePaid || !orgCaps.IsOrgVerified` → `payments_not_enabled`
- `EventService.cs:1251` — same pair, for payment readiness

**Must NOT be changed — the Capability Engine.** `CapabilityResolver` describes what an event
archetype supports and enforces nothing, by design (D-266). Adding verification gating there would
break archetype-less events and duplicate the entitlement check in a second, divergent place. This
is a standing constraint, not a preference.

### The real defect: the dependency is invisible to users

The backend computes it; the clients barely consume it.

- `can_organize_paid` is read in exactly **one** place in web — `host/events/new/page.tsx:31`.
- `can_receive_payout` has **no web consumer at all** (grep across `web/lib`, `web/app`,
  `web/components`).
- Nothing renders *why* a capability is locked, or what clears it.

So a user hits `payments_not_enabled` at checkout — the buyer's error, for the organiser's missing
KYC — with no route to the fix.

### Proposal: publish the dependency graph and render it

```
                 ┌─────────────┐
                 │ Phone (L1)  │──────────────► Free events · Attend · Chat · Posts
                 └──────┬──────┘                Certificates (receive)
                        │
          ┌─────────────┴─────────────┐
          ▼                           ▼
  ┌───────────────┐          ┌─────────────────┐
  │ Person KYC    │          │ Org verified    │
  │ PAN|GovtID    │          │ (reviewer)      │
  │ + Bank        │          └────────┬────────┘
  │ + fraud-clear │                   │
  └───────┬───────┘                   │
          │         ┌─────────────────┘
          ▼         ▼
     ┌──────────────────────┐
     │ can_organize_paid    │──────► Paid events · Paid workshops · Coupons
     └──────────┬───────────┘
                │  + org financial (PAN match · penny drop)
                ▼
     ┌──────────────────────┐
     │ can_receive_payout   │──────► Wallet withdrawal · Payout schedule
     └──────────────────────┘
```

| Feature | Requires | Enforced today at |
|---|---|---|
| Attend, chat, post, free events | phone | — (open) |
| Receive a certificate | ticket + attendance | `CertificateService` |
| **Create a paid event** | `can_organize_paid` **and** `IsOrgVerified` | `EventService.cs:1251` |
| **Sell a ticket** | same pair, re-checked live at purchase | `OrderService.cs:154` |
| Issue certificates | event `ManageContent` | `IEventAuthority` |
| **Wallet balance** | org seat (incl. `Finance`) | `WalletService` |
| **Withdraw** | `can_receive_payout` + org financial | `PayoutAccountStatus.Active` |

**Client work this implies** (none of it backend): every locked capability renders its
precondition and a link to the step that clears it; `/host/events/new` stops being the only surface
that knows; `/settings/verification` renders this table as live state.

**One caution.** `canOrganizePaid` is computed from `ev.CreatedBy` — the event's *owner*
(`OrderService.cs:154`). A co-manager with a `Manager` seat cannot make an unverified owner's event
sellable, which is correct, but produces a confusing message for the manager. Worth a `D-NNN` on
whose verification the gate should read.

---

## 9. Q7 · Everything else

Findings not covered above, ordered by consequence. **S1** = breaks a primary journey.

| # | Finding | Severity | Evidence |
|---|---|---|---|
| 1 | `/v1/assignments/{id}/accept` has **no caller on any surface**, and `go_live` requires an accepted assignment → an event may be unable to reach **Live** through any UI | **S1** | `EventService.cs:1608` `no_staff_assigned`; PLATFORM_FLOW_MAP §5 |
| 2 | Account recovery lands on `/account/security?recovered=1`, which **does not exist** | **S1** | `recovery-panel.tsx:50`; IA §1.1 |
| 3 | Every invitation → event link points at `/events/{slug}`; web serves `/e/{slug}` | **S1** | `invitations/page.tsx:72,100`; IA §1.1 |
| 4 | Web's `/onboarding` is dead — new users are sent to `/register`, so web has **no profile-completion step**. Mobile has one. | **S1** | IA §1.2; `app_router.dart:168` |
| 5 | Web detects step-up (`step-up/status`) but calls neither `/start` nor `/verify` → any web flow behind step-up dead-ends | **S1** | PLATFORM_FLOW_MAP §5 |
| 6 | Web has no `login/pending`/`approve`/`reject` → a browser-only user cannot approve a new-device sign-in | S2 | PLATFORM_FLOW_MAP §5 |
| 7 | Privacy controls render in **two** places on web (`settings/page.tsx` + `settings/privacy/`) | S2 | read both |
| 8 | Coupons complete server-side, **no UI on any surface** — and coupons are a paid-event feature, so they belong in the §9 graph | S2 | PLATFORM_FLOW_MAP §5 |
| 9 | Guest checkout, public invitation RSVP and delegated registration are server-complete with no client — all three are *acquisition* paths | S2 | PLATFORM_FLOW_MAP §4 |
| 10 | Admin Finance section unbuilt; `GET /v1/admin/refunds` has no caller. There is **no operator surface for the money spine** | S2 | PLATFORM_FLOW_MAP §5 |
| 11 | `/workspace` → Templates card → `/host/templates` **404s** (only children exist) | S2 | IA §1.1 |
| 12 | 18 orphaned routes on web; 17 of Phase 21's 37 routes are orphans or placeholders | S2 | IA §1.2 |
| 13 | Admin has two doors onto event review (`/verification`, `/events/pending`, `/events/review`, `/events`); the document defers this to "make which-to-use obvious" | S3 | IA §9 |
| 14 | Admin nav renders `ready: false` items as permanent dead doors | S3 | IA §9 |
| 15 | No breadcrumbs on a console 3 levels deep | S3 | IA §9 |
| 16 | Mobile pads content by a hardcoded `EdgeInsets.only(bottom: 80)` for a bar whose height is dynamic | S3 | IA §10 |
| 17 | Nothing validates **spec ↔ client**. CI proves backend ↔ spec only; all three clients hand-maintain models — the exact bug class that produced D-292's silent chat outage | S2 | PLATFORM_FLOW_MAP §6 |

Item 17 deserves emphasis. D-292 found that `web/lib/chat-api.ts` parsed camelCase against a
snake_case API, so **every parse threw and the Events tab rendered "No event chats" for everyone**.
That is the second occurrence of this class (D-245 was the first). It is a structural gap, not a
bug: generate the clients, or gate them.

---

## 10. Proposed target architecture

```
L0  AREAS  (five, unchanged — IA §2 is correct)
    Home · Community · Posts · Messages · Workspace

    fixed corners:  [avatar] Account menu (top-left)   [bell] Notifications (top-right)

┌── Home ────────────────── discovery
│     /discover · /e/{slug} · /categories · Saved
│
├── Community ───────────── people
│     /allies · people search · /u/{username}
│
├── Posts ───────────────── feed
│
├── Messages ────────────── conversations
│     event rooms · direct messages          (D-292 unified these on ChatRoom.Id)
│
└── Workspace ───────────── MY ACTIVITY  ◄── the only operational surface
      /workspace
        ├ hosted · drafts · pending · live · completed · archived
        ├ attending  (← My Tickets)
        ├ invitations · groups
        └ [Create event]
      /workspace/{eventId}                  ◄── replaces /host/events/{id}
        └ 17 capability-derived tabs

  Account menu (top-left)
    ├ View public profile → /u/{username}   ◄── PROFILE = derived, public, read-only
    │    attended · certificates earned · allies · achievements · affiliations
    └ /settings                             ◄── SETTINGS = authored, self-only
         ├ profile          (the editor)
         ├ account · security · privacy · notifications
         ├ verification     ◄── §7 status + remediation, both tracks
         └ representing     ◄── /host/representing merges here
              └ {orgId}/financial · wallet · payouts
```

**Ownership, stated once, no exceptions:**

| Question | Owner | Nature |
|---|---|---|
| Who am I, to others? | `/u/{username}` | derived, read-only |
| What am I doing? | `/workspace` | operational |
| How does my account behave? | `/settings` | authored |
| What's happening? | Home · Community · Posts · Messages | live |

**Deleted:** `secondaryNav`, `/host` and its 10 org-wide pages, `/host/admin/*` (4, superseded by
D-195), the "More" button on mobile, the duplicate privacy form, mobile's `/profile/*` settings
split.

**Preserved deliberately:** the five areas; the capability-derived tab list; backend-owned taxonomy;
admin's role-gated groups; every route as a *redirect* rather than a deletion until proven dead.

---

## 11. Migration order

Sequenced so each step is independently shippable and no step depends on a later one.

| # | Step | Risk | Unblocks |
|---|---|---|---|
| 0 | Fix the three S1 broken links (#2, #3) | none | recovery + invitation journeys |
| 1 | Verify #1 by hand (assignment accept / `go_live`) | none | may be the single worst defect on the platform |
| 2 | Adopt the RD-1 ownership rule; write it into IA §0 | none | every step below |
| 3 | Build `/settings/verification` (status only, no new backend) | low | makes §8 visible |
| 4 | Render capability preconditions at every locked action | low | closes the `payments_not_enabled` dead end |
| 5 | Dissolve `secondaryNav`; move items to owners; add Account menu | medium | §3 |
| 6 | Split Profile/Settings on the visibility rule; align mobile | medium | §6 |
| 7 | Add `live`/`completed`/`attending` views to Workspace | low | §7 |
| 8 | Rename `/host/events/{id}` → `/workspace/{id}`, redirect old | medium | RD-3 |
| 9 | Align mobile `/events/{id}/manage/*` to the same shape | medium | RD-3 |
| 10 | Delete orphans after redirect telemetry proves them dead | low | IA §1.2 |
| 11 | Person-level penny drop + account-holder name match | **high — needs a real `IKycProvider`** | §7 gaps |
| 12 | Generate clients from `openapi.json`, or gate spec↔client in CI | medium | finding #17 |

---

## 12. Decisions required before implementation

These are ambiguous architectural calls. Per `CLAUDE.md` §3.1 each needs a `D-NNN` in
`docs/DECISIONS.md` **before** anything is built on it. Numbers are deliberately unassigned —
a concurrent session owns that file and D-numbers have collided before.

| # | Decision |
|---|---|
| A | **Ownership rule** (RD-1). The four questions, one owner each, no exceptions. Everything else derives from it. |
| B | **Profile is derived and read-only; Settings is authored.** Adopting it means web stops titling Settings "Profile" and mobile moves four route groups. |
| C | **Workspace is the only operational surface.** `/host/*` and `/events/{id}/manage/*` retire into it. |
| D | **Verification is a just-in-time ladder, not an onboarding pipeline** — with the explicit consequence that PAN/bank never appear before Home. |
| E | **Financial Verification is two tracks** (person, organization) behind one status surface, because settlement is org-owned. |
| F | **Entitlements gate; the Capability Engine describes.** Restates D-266's boundary so a future pass cannot "fix" the engine into an authorizer. |
| G | **Whose verification does the paid gate read?** — owner (`ev.CreatedBy`) or acting manager. §9 caution. |
| H | **Person-level penny drop and account-holder name match**: add, or accept the asymmetry with the org track and record why. |
| I | **Client models: generated or gated.** Finding #17 — the D-245/D-292 bug class stays reachable until this is decided. |

---

## 13. What this review did not do

- **Nothing was executed.** No build, no test run, no page rendered, no flow clicked. Every finding
  is from source reading, and the four S1 items — especially #1 — must be confirmed by hand before
  anyone acts on them.
- **No code was changed** and no existing document was edited. This file is additive.
- **`docs/DECISIONS.md` was not touched**, for the concurrency reason in §12.
- **Backend architecture was not challenged.** `IEventAuthority`, the money spine, and the
  entitlement model are sound; every defect above is in navigation, ownership and client
  consumption. The backend is consistently ahead of its interfaces — 414 spec paths, 323 with any
  client caller — and this review does not propose adding to it.
