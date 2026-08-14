# Kurx Professional Identity System — Architecture Spec

> Single source of truth for every profile metric introduced/changed by this feature. Written
> before implementation per the approving product-architect directive (2026-07-30). Supersedes
> nothing about V3 itself; extends `PublicProfileService` in place.

## Relationship to the Verified Identity kickoff brief (deleted 2026-08-08; see git history)

That doc (undated prior session) locks a heavier target architecture: an `IdentitySignal`
append-only event index with INGEST→COMPUTE→SERVE tiers, pull-adapters, and "no new
source-of-truth tables" for the identity/projection layer. It explicitly defers any
connections/graph work ("Discovery graph") to P4, "gated behind V3 Phase 16 + real data."

This session's directive is explicit and more direct: reuse and extend `PublicProfileService`
in place, avoid duplicated aggregation logic, only add schema where the data doesn't already
exist. That directive governs this implementation. Reconciliation:

- **Allies is not part of the Identity-Signal projection layer.** It is a new, small,
  first-class relationship — the same category of thing as `Membership` or
  `OrganizationFollower` (D-064), not a derived/rebuildable projection of existing V3 facts (a
  connection between two people doesn't exist anywhere in V3 today; nothing to project). So a
  new `AllyConnection` table does not violate the kickoff doc's "no new source-of-truth tables
  for the identity layer" rule — that rule governs the *read/aggregation* layer
  (`PublicProfileService`), which still owns zero tables of its own and stays a pure projection
  over `Membership`/`EventParticipant`/`Certificate`/`Badge`/`AllyConnection`.
- The new aggregation logic in `PublicProfileService` (achievements merge, per-org rollups,
  milestone timeline) is written as separable per-source methods (one method per fact source),
  so it could be lifted behind an `IdentitySignal` pull-adapter later without a rewrite — but no
  signal contract, outbox wiring, or INGEST/COMPUTE/SERVE tiering is built now. That remains
  future work if/when the kickoff doc's P1 is actually scheduled.
- Recorded as decision **D-205** (see `docs/DECISIONS.md`).

## 1. Events

**Definition:** the count of unique **public** events the user has real involvement in, across
three independent lanes that never merge into each other:

| Lane | Source | Stat field |
|---|---|---|
| Organized | `Event.CreatedBy == user` or verified `Membership` at the event's org | `EventsConducted` |
| Participated | `EventParticipant` (`SubjectType=Person`, `Visibility=Public`, state ∈ Accepted/Active/Completed) | `Participations` |
| Attended | `Ticket.State == CheckedIn` (opt-in via `User.ShowAttended`) | `EventsAttended` |

**Dedup rule (the fix):** within the Participated lane, one person may hold multiple
`EventParticipant` rows for the same event (unique constraint is on
`(EventId, SubjectType, SubjectId, RoleSlug)`, not `(EventId, SubjectType, SubjectId)`). Every
count and every per-event listing groups by `EventId` first — an event with Speaker + Judge
roles is **one** event, roles is a list on that one row. This applies independently within each
lane (an event you organized AND personally checked into is legitimately 2 real facts about
one event — organizer stats and attendance stats are different questions — but each lane itself
is deduped to unique events).

**Event card fields:** Event (id/title/slug/banner/city/date), `Roles[]`, `OrgName`,
`CertificateEarned?` (verify code, if the user holds a public cert for this event),
`AchievementEarned?` (bool + label, if that cert/badge qualifies as an achievement — see §5),
`Visibility` (the event's own visibility label, always `Public` by construction since private
events are filtered at the query boundary).

**Role vocabulary mapping** (`EventParticipant.RoleSlug` → product label), zero new seed rows:

| Product role | Real slug |
|---|---|
| Participant | `attendee` |
| Competitor | `competitor` |
| Volunteer | `volunteer` |
| Staff | `staff` |
| Coordinator | `coordinator` |
| Organizer | `manager` |
| **Host** | **`owner`** |
| Speaker | `speaker` |
| Judge | `judge` |
| Mentor | `mentor` |
| Sponsor | `sponsor` |

All other real slugs (security/medical/technical/registration_desk/artist/performer/panelist/
trainer/examiner/referee/scrutineer/team_member/delegate/exhibitor/recruiter/vendor/vip/media/
guest/chaperone) still count and still render, as a humanized fallback chip rather than one of
the 11 headline labels — nothing is hidden, only unstyled. Decision **D-202**.

## 2. Organizations

**Definition:** verified institutional involvement — never `OrganizationFollower` (D-064's
unrelated, unverified, one-directional "follow for notifications" feature).

**Role label per membership row:** a `Membership` carries two independent things — `OrgRole`
(platform RBAC: Owner/Manager/Staff/Finance/Representative) and, when verified via a claim,
`SourceClaimId → MembershipClaim.ClaimedRole` (the real-world affiliation: Student/Faculty/
Employee/Alumni/Founder/Director/Coordinator/Volunteer/ClubPresident/EventLead/Other). The
product's role vocabulary (Student/Employee/Organizer/Volunteer/Staff/Coordinator/Member) is
the **affiliation**, not the RBAC role. Label rule: if `SourceClaimId` is set, show the claim's
`ClaimedRole` label; otherwise fall back to the `OrgRole` label (`Representative` → "Organizer",
others verbatim). "Campus Ambassador" has no backing enum value anywhere (not in `OrgRole`, not
in `MembershipClaimRole`) — real gap, not invented; it renders as "Other" today via
`MembershipClaimRole.Other` until a real claim type is added. Decision **D-206**.

**Card fields (one card per org):** `OrgName/Slug/Logo`, `Roles[]` (per the label rule above),
`IsVerified`, `JoinedAt` (`CreatedAt`), `ValidUntil` (null = ongoing), `EventCount`
(dedup-fixed per §1), `CertificatesCount` (public certs whose event belongs to this org),
`AchievementsCount` (achievement-qualifying certs whose event belongs to this org — platform
`UserBadge`s are never attributed to an org; they carry no `EventId`/org linkage at all, so they
only ever appear in the global Achievements total, never a per-org one — stated explicitly
rather than guessed).

**Correction from the original draft (proven wrong against the real schema, 2026-07-30):**
`Membership` carries a DB-enforced unique constraint on `(UserId, OrgId)` — a person holds
**exactly one** membership row per org, ever; a role change (e.g. Volunteer → Coordinator)
updates that row in place rather than adding a second one. The service still groups by `OrgId`
for forward-compatibility (harmless — it degrades to a one-row group today), but `Roles[]` will
in practice always carry exactly one label per org under the current schema. This was caught by
`PublicProfileTests` attempting to seed two rows for one pair and hitting `23505` in CI.

Only `Membership.ShowOnProfile == true` rows are included, matching the existing convention.

## 3. Certificates

**Definition: unchanged.** Count of `Certificate` rows where `IsPublic == true` and
`IsRevoked == false`, belonging to the user. Already naturally one row per certificate — no
dedup issue existed here.

## 4. Achievements

**Definition:** recognitions, not certificates, not generic engagement badges — and explicitly
**not tightly coupled to certificate storage**, per direction. Implemented as a merge over two
independent, clearly-separated source methods, so a third source can be added later without
touching the merge shape or any consumer:

1. `AchievementsFromCertificates` — certs with `Kind ∈ {Winner, RunnerUp, Finalist,
   Appreciation}` (already-real recognition kinds; `Participation`/`Volunteer`/`Organizer`/
   `Judge`/`Speaker`/`Sponsor`/`Completion` are role-attendance records, not recognitions).
2. `AchievementsFromBadges` — `UserBadge` join `Badge` (generic platform gamification, e.g.
   "Early Adopter"). Kept as its own labeled lane ("Platform Recognition") rather than merged
   indistinguishably into the same list — a badge proves engagement, not competitive
   recognition, and conflating them would overclaim.

Both return the same shape (`AchievementCard{Name, Description, IconKey, EarnedAt, Source,
EventTitle?, EventSlug?, OrgName?}`), concatenated and sorted by `EarnedAt` descending.
`ProfileStats.Achievements` counts source (1) only — the certificate-backed recognitions are the
number that answers "how many times has this person won/placed" — badges are surfaced but not
folded into that headline count. Decision **D-203**.

**Extending later:** to add a new achievement source (e.g. an organizer-awarded recognition
table, if ever built), add one more `AchievementsFromX` method returning the same
`AchievementCard` shape and concatenate it in — no change to the DTO, the endpoint, or either
client.

## 5. Allies

**Definition:** a mutual, explicitly-consented professional relationship. Not a follow, not
one-directional, not derived/automatic from shared events — an event together is *context* for
why someone might send a request, never itself a connection.

**Domain model** — new `AllyConnection` (one row per **unordered** user pair, for its entire
lifetime):

```
Id, UserLowId, UserHighId          -- canonical pair (UserLowId < UserHighId), unique+check constraint
RequesterId, AddresseeId           -- who asked whom, for the CURRENT state
Status: Pending | Accepted | Declined | Revoked
Visibility: Public | Hidden        -- per-connection, default Public — lets a user hide one ally
                                        without turning off ShowAllies entirely
ConnectedVia: string?              -- "request" | "mutual_request", set on Accept
FirstSharedEventId: Guid?          -- best-effort, computed at Accept time (earliest EventId
                                        both users have a Public EventParticipant/attended-ticket
                                        row for); null if none found — never fabricated
LastInteractionAt: DateTimeOffset? -- reserved, NOT written by this feature (no existing
                                        single source of truth for cross-subsystem "interaction";
                                        wiring it is future work for whichever subsystem defines
                                        "interaction" — chat, event coincidence, etc.)
CreatedAt, RequestedAt, RespondedAt, UpdatedAt
```

`MutualEventCount` is deliberately **not** a stored column (repo convention: prefer computed
values over stored counters) — it's computed live, on read, as a distinct-`EventId` intersection
between the two users' Participated/Attended lanes, in the ally-list DTO only.

**State machine:**

| Action | Precondition | Result |
|---|---|---|
| Request | no row for pair | insert `Pending`, requester = caller |
| Request | row `Declined`/`Revoked` | reactivate same row: `Pending`, requester = caller, `RespondedAt=null` |
| Request | row `Pending`, caller is requester | no-op (idempotent) |
| Request | row `Pending`, caller is addressee (crossed request) | auto-accept: `Accepted`, `ConnectedVia="mutual_request"` |
| Request | row `Accepted` | no-op |
| Accept | `Pending`, caller is addressee | `Accepted`, `ConnectedVia="request"`, compute `FirstSharedEventId` |
| Decline | `Pending`, caller is addressee | `Declined` |
| Revoke | `Pending`/`Accepted`, caller is either party | `Revoked` (covers both "cancel my request" and "remove an existing ally" — one state, one less thing to test) |

**Authorization:** caller not a party to the row → **404** (hidden-resource convention, never
403 — don't confirm a connection row exists to a non-party). Caller is a party but the
transition is invalid for the current state → **409**.

**Privacy:**
- `User.ShowAllies` (new, default `true`, same convention as `ShowAttended`/`ShowCertificates`)
  gates whether the Allies section/count appears on that user's own public profile at all.
  **Writer added in D-219** (`PATCH /v1/me/privacy`) — this flag and its three siblings shipped here
  with read-side enforcement and no way to set them; `Visibility` below likewise
  (`PATCH /v1/allies/{id}/visibility`), where the shared-flag semantics are now decided explicitly:
  either party may hide the pair and it disappears from both profiles.
- An ally only appears in **someone else's** public list if **both** parties have
  `ProfilePublic == true` and the connection's own `Visibility == Public` — the relationship was
  mutually consented, but *displaying* it still exposes the other party's identity, so their own
  privacy choice gates it independently of the viewer-side flags. This is the one genuinely
  non-obvious privacy call in the feature. Decision **D-201**.

**Privacy tiers for identity exposure on internal list surfaces (D-211).** `ProfilePublic` gates
`Username`/`AvatarKey` on some real-user lists (`AttendeeRow`, `AssignmentView`, `Speaker`,
`LeaderboardEntryView`) but not on `OrgMember` — this is a deliberate two-tier rule, not drift:
- **Membership rosters** (`OrgMember`) show identity regardless of `ProfilePublic`. A `Membership`
  is itself an ongoing, mutual organizational relationship — a colleague already knows you're on
  the team whether or not your profile is public.
- **Transactional/derived surfaces** (`AttendeeRow`, `AssignmentView`, `Speaker`,
  `LeaderboardEntryView`) gate on `ProfilePublic`. A ticket buyer may have zero relationship with
  the org beyond one purchase; an event assignment or speaking slot can be a one-off invitation,
  not standing membership. There is no durable relationship to imply the identity is already known.

Apply this rule to any future real-user-list surface rather than re-deriving it per DTO.

**Endpoints:** `POST /v1/allies/requests`, `POST /v1/allies/requests/{id}/accept`,
`POST /v1/allies/requests/{id}/decline`, `DELETE /v1/allies/{id}`,
`GET /v1/allies/requests/incoming`, `GET /v1/allies/requests/outgoing`,
`GET /v1/public/users/{username}/allies` (anonymous-allowed public read).

**Abuse control:** reuses whatever rate-limit policy already guards `MembershipClaim`/
`OrgInvitation` submission; one additional application-level guard — reject a new outgoing
request if the requester already has ≥200 rows in `Pending` state as requester.

## 6. Identity Labels

**Definition:** a small set of derived display tags (e.g. "Verified Student", "Organizer",
"Public Speaker", "Hackathon Winner", "Mentor", "Coordinator") — never manually editable,
computed purely from data already loaded for the rest of the profile (verified claim roles,
`EventParticipant` role slugs held, achievement kinds) — **zero new queries**. A pure mapping
function over already-fetched aggregates, capped at a small top-N (6), priority order:
identity-verification-backed labels first, then achievement-backed, then role-backed.

## 7. Timeline

**Definition:** professional milestones, not a raw per-row activity log. Lanes (each already
deduped per §1/§4/§2):

- `org_joined` — earliest `Membership.CreatedAt` per org ("Joined {Org}")
- `org_verified` — `Membership.VerifiedAt` (**new field**, set at the two existing
  `IsVerified = true` call sites — `MembershipVerificationService`, `OrgVerificationService` —
  previously untracked; a boolean flip carried no timestamp) ("{Org} Verified")
- `participation` — one entry per distinct event, `Roles[]` attached, labelled by the highest-
  priority role held
- `achievement` — from the same merge as §4 ("Won {Event}", "Earned {Badge}")
- `certificate` — non-achievement certs (kept separate so a Participation cert doesn't read as
  a milestone)
- `organized`, `attended` — unchanged lanes, dedup-fixed per §1

The single earliest entry across all lanes is flagged `IsFirstEvent = true` rather than being a
separate synthetic lane — one flag on real data beats inventing a row that doesn't correspond to
a fact.

## Invariants preserved (unchanged from V3/D-041/D-018)

Self-declared `User.*` display fields stay authority-zero. Private events/certs never surface.
`EventParticipant.Visibility` opt-in honoured. Hidden resource → 404-not-403. Deny-by-default
for audience-gated content. Platform/staff roles never exposed on a public profile.

## Explicitly out of scope

Ally recommendations/"people you may know" (needs graph traversal, not requested). An
Allies-only content-visibility tier on events/certs. Admin app surfaces (no valid administrative
need identified). A new real-time notification pipeline for ally requests (reuse the existing
dispatch used by `OrgInvitation`/`MembershipClaim`, wire on top of it). Backfilling historical
duplicate `EventParticipant` rows (unnecessary — dedup is query-time only). `LastInteractionAt`
population (reserved column, no writer yet — see §5).

## §8. Deep integration pass (D-207..D-210, 2026-07-30) — full repo audit + repository-wide rollout

A second pass, prompted by a full-repository gap analysis against mature networking platforms
(LinkedIn/GitHub/Discord/Luma/Eventbrite), scoped down to what's real for Kurx rather than the
full wishlist. See the audit findings and scope confirmation earlier in this session for the
reasoning; this section records what shipped.

**Notification wiring (D-207).** `AllyService` now calls `INotificationService.NotifyAsync`
(the one pipeline that does DB row + SignalR + FCM push in one call) for all four transitions,
using a small `NotificationKinds` constants class (`ally.requested`/`ally.accepted`/
`ally.declined`/`ally.removed`) rather than another ad-hoc string. **Revoking a still-`Pending`
request stays silent** — the other party never engaged with it, so a quiet withdrawal beats a
"Connection Removed" notice for something they weren't yet aware of; only revoking an `Accepted`
connection notifies. Every notification's `data` carries a generic `route` (an in-app path) —
the first real use of a **repo-wide generic deep-link convention** (both web and mobile
previously only special-cased the *chat* notification shape; this makes `route` the one
convention any future notification kind can rely on) — plus a `connectionId` for the one kind
(`ally.requested`) whose card renders inline Accept/Decline instead of just being a tap target.

**Identity exposure on every real-user list (D-208).** Extended, not rebuilt:
- `AssignmentView` (`EventAssignment` — Team/Volunteer/Judge/Coordinator/Organizer/Host, the 14
  role catalogue) gained `AssigneeName/AssigneeUsername/AssigneeAvatarKey` — `UserId` was already
  a real, non-nullable FK; the DTO just didn't expose the identity behind it.
- `OrgMember` gained `AvatarKey`/`IsVerified` (from `Membership.IsVerified`, already the
  authoritative source — see §2).
- `AttendeeRow` gained `BuyerUserId/BuyerUsername/BuyerAvatarKey`, null for a guest checkout or a
  non-public profile.
- `Speaker` gained a genuinely new, nullable `UserId` (migration `AddSpeakerUserId`) — the
  **deliberate exception**: most speakers are curated content (name/bio/photo) with no Kurx
  account at all, so Connect correctly does not apply unless an organizer explicitly links one
  (via the new search endpoint) to a real account.
- **`Sponsor` is explicitly excluded** — it's a brand/company record, never a person; forcing a
  Connect affordance onto a company logo would be exactly the kind of placeholder functionality
  this system is built to avoid.
- **A real, pre-existing bug found and fixed as a direct consequence**: `AttendeeRow`'s wire
  format is camelCase (default ASP.NET Minimal API serialization on a raw record — every *other*
  endpoint in this API hand-builds a snake_case JSON object, but this one returns
  `Results.Ok(record)` directly) — both the web `attendeeSchema` (snake_case) and the mobile
  `AttendeeDto` (wrong field names *and* wrong case: `id`/`name`/`email`/`checked_in`, none of
  which exist on the real payload) had never actually matched the live API. The attendees screen
  on both platforms had likely never rendered a real attendee correctly. Fixed on both clients as
  part of adding the identity fields, not deferred.

**Allies suggestions v1, batch status, and mutual detail (D-209).** Confirmed scope: ranked from
exactly two real signals — shared event co-participation and shared verified-org membership —
naming the reason in plain language (`"3 shared events, same organization"`), never a bare
score. `GET /v1/me/allies/suggestions` gathers candidates via a handful of set-based queries (not
one per candidate) and excludes anyone already `Accepted`/`Pending` in either direction.
`POST /v1/me/allies/status-batch` is the one call every person-list surface now uses instead of
firing a request per card. `GET /v1/me/allies/mutual/{otherUserId}` reuses the existing
event/org-intersection helpers to return the actual shared events/orgs, not just a count.
`GET /v1/public/users?q=` is genuinely new — the foundation for people search, gated to public
profiles with a claimed username, a 2-character floor against a full-table scan.

**Explicitly rejected: 1:1 chat as an ally integration.** Kurx chat is deliberately
event-room-scoped only (a comment in the mobile code says so explicitly — *"There is deliberately
no 'Direct' tab... the product does not have this feature"*). "Message this ally" would reverse
that standing product decision, not integrate with something that exists — not built, flagged for
an explicit future decision if ever wanted.

**Shared components, not page-specific UI.** Web already had `UserCard`+`AllyConnectButton` from
the first pass (D-201) — this pass's work was applying them everywhere a person renders, plus
adding a `getAllyStatusBatch`-aware initial state per surface. Mobile gained the equivalent
primitive it was missing: `KurxUserTile` (`mobile/lib/common/widgets/kurx_user_tile.dart`) —
avatar/name/username/subtitle/trailing, tap-to-profile, mirroring `UserCard` one-for-one — now
used by attendees, org members, search, and suggestions on that platform.

**Formerly a disclosed gap, closed in D-212:** at the time this pass shipped, there was no Flutter
equivalent of the web "Team" (`EventAssignment`) management screen — organizer staff/volunteer/
judge assignment was web-only. A later repository-wide completion pass (D-212) built
`mobile/lib/features/organizer/presentation/pages/assignments_page.dart`, reusing the same
`KurxUserTile`/`AllyConnectButton` primitives this pass introduced — the identity-and-connect
wiring described above now applies on both platforms, not just web.

**Final repository-wide audit (D-210).** A dedicated 10-surface audit (chat, group/team rosters,
public event/org pages, leaderboard, certificate verify, blacklist/audit, invited-by, reviews,
judge-as-entity) run against the audit's own stated bar — every avatar/name either opens a profile
or has a stated privacy/product reason it doesn't — found and closed three real gaps: **group/team
rosters** (web `/groups`, mobile `group_detail_page.dart` — `GroupMember` already carried a real
`UserId`, this was a pure DTO/UI gap, closed with the same `UserCard`/`KurxUserTile` pattern used
everywhere else), **gamification leaderboard** (found a second instance of the D-208 camelCase-wire
bug — `LeaderboardEntryDto` requested snake_case fields against a camelCase `Results.Ok(record)`
endpoint, plus an inconsistent missing `ProfilePublic` gate on `Username` — both fixed; tap-to-profile
only, no Connect button, since a ranked list is a display context not a people directory), and
**event review author** (added `AuthorUsername`/`AuthorAvatarKey` gated by the exact same
`!IsAnonymous` check the existing `AuthorName` suppression already used). **Chat sender identity**
was confirmed as a real, remaining gap but deliberately left open — fixing it means changing the
live SignalR broadcast payload, not just a read DTO, which is a materially different risk profile
and was never in the Allies mandate to begin with ("skip 1:1 chat entirely"). Full reasoning and
file list: `docs/DECISIONS.md` D-210.
