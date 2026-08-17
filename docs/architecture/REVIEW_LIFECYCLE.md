# Event Review Lifecycle (D-266 M4)

> **The review engine decides *what state an event is in*. It decides nothing about who may act.**
> Reviewer authorization is `IEventAuthority` (D-269); publish validation is `PolicyResolver.PublishBlockers`
> (M3); review history is `VerificationReview`. M4 consumes all three and duplicates none.

## States

| State | Meaning |
|---|---|
| `Draft` | Being written. Editable. |
| `PendingReview` | Submitted, waiting for a reviewer to claim it. **Edit-locked.** |
| `UnderReview` | A reviewer has claimed it — and `review_claimed_by` says which one. **Edit-locked.** |
| `ChangesRequested` | Returned with notes. **Editable — that is the point of the state.** |
| `Approved` | Review passed. Publication is now the **creator's** call, not the reviewer's (D-377) — the event is permitted to go live and is not yet public. |
| `Rejected` | Refused with a reason code. Resubmittable. |

`InReview` was retired in Stage 4. It conflated *waiting for a reviewer* with *a reviewer has it*, which is
exactly the distinction a claim flow needs: without it, releasing a claim has nowhere to return to.

## Who holds an item

The state split says an item **is** claimed; it never said **by whom**. Two reviewers could therefore both
work one event — the checklist is per-reviewer, so the second signed off against ticks they never made.

`events.review_claimed_by` / `review_claimed_at` close that. **One reviewer holds an item at a time:**

- `claim_review` records the holder; every other leg clears it, **including a decision** — the verdict lives
  in `verification_reviews`, and a decided event is nobody's work in progress.
- `release_review`, `approve_review`, `reject_review` and `request_changes` refuse anyone else with
  **`claimed_by_another_reviewer`**.
- **An admin may override.** A reviewer who goes offline holding an item must not strand it — a release
  valve, not a general exemption.
- The holder moves in the *same statement* as the status, so an item is never `UnderReview` unowned, nor
  owned while sitting in the queue.
- `GET /v1/admin/events/pending` returns the holder so the queue reads as taken; the console shows
  "You're reviewing this" or "Held by …".

## Concurrent transitions

Every gate runs against the status the request read. The write is therefore **claimed in SQL** against that
same status — a conditional `UPDATE … WHERE Status = <the one we read>`. Zero rows updated means the event
moved underneath the request, which answers with **`transition_conflict`**: a request that decided about a
state that no longer exists must not record a verdict.

Without it, two reviewers deciding in the same moment both passed the gates and both wrote — the status
became whoever committed last while **both** verdicts landed in `verification_reviews` (which has no unique
index), so the history could say one event was approved *and* rejected.

This is a compare-and-swap, not a lock, and not a new mechanism: `ChatService` already uses the same
predicate for room status, `OrderService`/`RefundService` for orders. Row locks are ruled out by
[D-231](../DECISIONS.md) — `SELECT … FOR UPDATE` took the suite from ~7 minutes to 5h42m, because Postgres
applies no default lock timeout and a lock held across an `await` blocks every later writer.

## Transitions

```
Draft ──submit_for_review──▶ PendingReview ──claim_review──▶ UnderReview
  ▲                              │                               │
  └──────withdraw────────────────┘                               ├──approve_review──▶ Approved ──publish_approved──▶ Published
                                 ▲                               ├──request_changes─▶ ChangesRequested ──submit_for_review──┐
                                 │                               ├──reject_review───▶ Rejected ──submit_for_review──────────┤
                                 └──release_review───────────────┘                                                          │
                                 ◀──────────────────────────────────────────────────────────────────────────────────────────┘
```

**`withdraw` works from `PendingReview` and from `Approved` (D-363 §1)** — never from `UnderReview`.
Once a reviewer has claimed an item, withdrawing would discard their in-flight work; once they have
finished with it, taking it back is the organiser's call again.

**A Public product cannot self-publish** — `publish_approved` is reachable only from `Approved`. Private
products are never reviewed, so `publish` direct from `Draft` remains for them.

### Who presses publish (D-377)

**Approval grants permission to publish. It does not publish.** The two acts belong to two different
people, and the transition table alone does not say which:

| Status | Creator may publish | Publicly visible |
|---|---|---|
| `PendingReview` / `UnderReview` | **no** — `reviewer_required` | no |
| `Approved` | **yes**, free or paid | **no** |
| `Published` | — | **yes** |

The gate sits on `target == Published`, so both doors — the legacy `publish` and the reviewed
`publish_approved` — pass through it. It previously sat inside `if (isPaid)`, which meant a free Public
event could be submitted for review and then published by its own creator, out of the queue and with no
approval; and a paid event could not be published by its creator even *after* a reviewer approved it.

`Approved` is therefore a state an event genuinely sits in — reviewed, permitted, not yet public. It is
also the state at which the **host workspace opens** (web `OPEN_HOST_STATES`, Flutter `openHostStates`).
### Leaving `Approved`, and editing after it (D-363)

`Approved` is no longer forward-only, and approval no longer floats free of what it approved:

| Action from `Approved` | Goes to | Why it is allowed |
|---|---|---|
| `publish_approved` | `Published`/`Scheduled` | approval granted the permission; the creator spends it |
| `withdraw` (§1) | `Draft` | never public, so it can hold no orders — nothing is lost by reworking it |
| `cancel` (§2) | `Cancelled` | abandoning it outright; terminal, never resurrected (D-101) |

**A material edit re-opens the review (§4).** `EventStatusWorkflow.RequiresFreshReview` names the fields
a reviewer actually assessed — title, category/type, dates and timezone, venue/mode/URL, capacity,
visibility, eligibility, legal, commerce. Changing any of them on an `Approved` event returns it to
`PendingReview`, clears the claim, and audits `event.review.reopened_by_edit`. Cosmetic fields (banner,
tagline, prose, FAQ, rules, contact, registration windows) are free. Product (Public/Private) and the
representing organization are not editable at all: changing either invalidates the eligibility gate the
event was created under (D-323).

The predicate tests *presence in the payload*, not *change of value*, so a client that posts the whole
record on every save re-reviews on every save from that form.

**Two of the material fields are not on the event row**, and are governed by their own services calling
the same shared reopen (`EventReviewReopen.IfApprovedAsync`):

| Surface | Endpoint | Under review | After approval |
|---|---|---|---|
| the event | `PATCH .../events/{id}` | `event_under_review` (409) | material field → `PendingReview` |
| ticket price / quantity | `POST·PATCH·DELETE .../ticket-types` | `event_under_review` (409) | any change → `PendingReview` |
| eligibility (audience rule) | `PUT·DELETE .../audience` | `event_under_review` (409) | any change → `PendingReview` |
| legal / authorization letter | `POST /v1/events/{id}/authorization` | `event_under_review` (409) | resubmission → `PendingReview` |

Adding a ticket type counts as much as editing one — otherwise a free approved event could acquire its
entire price list the moment it cleared review.

## Legacy actions

Both are retained because clients still post them, and both were retargeted in Stage 4 so that **no
transition anywhere targets the retired `InReview`**.

| Action | Before | Now | Note |
|---|---|---|---|
| `submit_review` | `Draft → InReview` | `Draft → PendingReview` | Alias for `submit_for_review`. **API behaviour change:** the returned status string differs. |
| `reject` | `InReview → Draft` | `PendingReview`/`UnderReview` → `Draft` | *Send back to the organiser.* |
| `reject_review` | — | `UnderReview → Rejected` | *Formal rejection*, requires a reason code. |

`reject` and `reject_review` are deliberately separate. One means "needs work", the other means "refused".
Collapsing them loses the difference between a queue item and an outcome.

## Decisions and evidence

Reviewer decisions are written to **`VerificationReview`** — the store that already existed, with
`VerificationSubjectType.Event` already in its vocabulary. No second review table.

Only the three real verdicts write rows:

| Action | Row written |
|---|---|
| `approve_review` | `Approve` |
| `reject_review` | `Reject` + `ReasonCode` |
| `request_changes` | `RequestChanges` + `Notes` |
| `claim_review`, `release_review` | **none** — queue mechanics, not verdicts |

Recording claims would bury the decisions in workflow noise, making *"what did the reviewer decide"* a
filtered query rather than a read.

**The decision and the status change share one save.** A status with no recorded decision behind it — or a
decision with no status change — is an audit gap in precisely the surface an approval trail exists to cover.

`EventReviewReason` is a closed vocabulary (`Incomplete`, `ProhibitedContent`, `UnverifiedOrganiser`,
`MisrepresentedAffiliation`, `InvalidCommerce`, `Duplicate`, `Other`). Free-form rejection strings cannot be
analysed or localised.

## Edit lock

`EventStatusWorkflow.IsEditLocked` is the single definition. `PendingReview` and `UnderReview` return
**409 `event_under_review`** on PATCH. An edit landing between a reviewer reading an event and approving it
means they approved something other than what they read.

`ChangesRequested` is **not** locked — editing is the entire purpose of that state.

## Publish gate

`TransitionGateAsync` calls `PolicyBlockerAsync`, which delegates to `IEventPolicyService` and returns the
first `PolicyResolver.PublishBlockers` entry.

Before M4 it did not: the gate had its own validation while `/policy-requirements` reported the policy
engine's blockers, so the list a reviewer read could differ from the rule that actually refused the publish.
A test asserts the two agree; if they diverge again the build fails.

## Admin API

| Route | Purpose |
|---|---|
| `GET /v1/admin/events/pending` | The review queue (`PendingReview` + `UnderReview`) |
| `GET /v1/admin/events/review-counts` | Queue tab counts, derived in one pass |
| `GET /v1/admin/events/{id}/review-history` | That event's decisions, newest first |

**There is no admin-side workflow route.** Every reviewer action goes through the org-scoped
`POST /v1/orgs/{orgId}/events/{eventId}/transition`, which a reviewer may drive on any org's event. A
duplicate admin action endpoint is how two workflows drift apart.

`review_history.reviewer_name` is nullable and the client falls back to *unknown reviewer*, but **a null
cannot occur for an event review today**: `ReviewerId` is always set, its FK to `users` is `ON DELETE
RESTRICT`, and account deletion (D-263) *anonymises* rather than removes — it sets `Name = "Deleted user"`.
So a departed reviewer's decisions render as **"Deleted user"** and are never dropped, which is the
property that matters: an audit trail that loses entries when staff leave is not an audit trail. The null
handling stays as defence, not as a described behaviour.

`review-counts.legacy_in_review` reports the retired `InReview` bucket, now always `0`. Retained because
removing a field is a client break; it goes when clients have dropped it.

`dashboard/summary.pending_events` counts `PendingReview + UnderReview` — the same pair `/events/pending`
lists. D-058 requires a tile to agree with the queue it links to.

## Clients

The status is serialized as the lowercased member name (`pendingreview`, `underreview`, `changesrequested`,
`approved`, `rejected`) — no underscores. Every surface that filters or switches on it must move together
with the enum; nothing here fails loudly when it drifts.

| Surface | Reads the status in |
|---|---|
| Web | `app/(app)/workspace/page.tsx` (`AWAITING_REVIEW_STATES`), `components/host/event-status-actions.tsx` |
| Admin | `lib/event-tabs.ts`, `components/admin/events-workspace/{events-table,event-workspace-sheet}.tsx`, `app/(console)/events/review/**` |
| Mobile | `workspace/…/workspace_hub_page.dart`, `organizer/…/event_status_page.dart` |

**"Pending approval" means `PendingReview + UnderReview` on every surface** — with the platform, awaiting a
decision. `ChangesRequested` and `Rejected` are decided outcomes the host must act on, not a queue.

Client action lists offer only what `EventStatusWorkflow.Actions` declares valid from the current state —
notably no `cancel` from `ChangesRequested`/`Approved`/`Rejected`, and no reviewer actions on host surfaces.

## Migration

`MigrateInReviewToPendingReview` maps every stored `InReview` row to `PendingReview`. Deterministic, not a
guess: the pre-M4 model never recorded a reviewer claim, so no row can be shown to have been `UnderReview`,
and inventing one would strand the item on a reviewer who does not exist.

Reversible, with a caveat noted in the migration: `Down()` returns *all* `PendingReview` rows to `InReview`,
which is behaviourally lossless against the pre-M4 runtime but discards the post-M4 distinction.
