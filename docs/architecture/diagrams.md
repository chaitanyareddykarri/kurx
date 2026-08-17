# Kurx — Architecture Diagrams

ER, sequence, and state diagrams for the current (post M0–M13 / D-039–D-052) system. Diagrams are
[Mermaid](https://mermaid.js.org) and render on GitHub. Grounded in
[`docs/DATABASE_TABLES.md`](../database/DATABASE_TABLES.md), [`docs/api/README.md`](../api/README.md), and
[`.claude/memory/trust-verification.md`](../../.claude/memory/trust-verification.md) — update those first,
then re-derive a diagram here if the shape changed.

> Money is `bigint` paise (`_paise`, D-004); every id is `uuid` (D-006). The two `verification_*`
> tables are **polymorphic** — `subject_id` carries no FK (D-039), shown below as dashed intent.

---

## 0. Flow — Hosting an event (user-first, D-267 / D-268)

**Users own events** (`Event.CreatedBy`, checked first by every event authorization). A **representation**
is an *attribute* of an event — branding, verification, trust, permissions, payout destination — never its
owner, never a container a person opens on the way to hosting, and never a prerequisite for creating one.
There is no "personal organization": representing yourself creates nothing.

```mermaid
flowchart LR
    U([User]) --> P[Profile<br/>owns the entry point, D-305]
    U --> W[User Workspace<br/>second door, D-333]
    P --> G{{Eligibility gate<br/>D-305 — runs BEFORE the form}}
    W --> G
    G -->|Public| PUB{Represents a<br/>verified org? D-353}
    G -->|Private| PRIV[Representing: Personal<br/>creates nothing]
    PUB -->|yes| F[Creation form]
    PUB -->|no| BLOCK[/refused — self-hosting<br/>is Private-only, D-353/]
    PRIV --> F
    F --> E[Draft Event<br/>owner = CreatedBy, D-268]
    E --> RV[Review / approval]
    RV --> M[Event Host Workspace<br/>tickets · people · check-in · analytics]

    subgraph optional [Optional, independent errand]
        RQ[Request to represent<br/>an institution] --> AV[Admin verifies]
    end
    AV -. adds a choice to .-> PUB

    classDef gone fill:#fee,stroke:#c00,stroke-dasharray:4 3;
    X["RETIRED: Workspace → Organizations → Organization → Events → Create Event<br/>RETIRED: User → Personal Organization → Event"]:::gone
```

**Corrected 2026-08-15.** This flow was drawn as `Workspace → Create Event → Representing? → Event` and
was missing three things that ship today:

- **The gate.** [D-305](../DECISIONS.md) put an eligibility/verification gate *in front of* the form,
  and it also chooses Public vs Private. It is real code on both clients —
  `web/components/host/create-event-gate.tsx` and Flutter's `create_event_gate_page.dart`.
- **The entry point.** D-305 moved it to **Profile**; [D-333](../DECISIONS.md) later added Workspace
  back as a *second door* to the same gate. Both link to `host/events/new`.
- **[D-353](../DECISIONS.md).** "Personal" is no longer an unconditional path to any event — a
  **Public** event must represent a verified organization; self-hosting is **Private-only**. The server
  states this as `requires_representation` on `/v1/me` so the clients do not each infer it.

The retired paths are drawn only to name what no longer exists: there is no organization list, no
organization detail page, no per-organization event list, no "current organization" — the routes
`/orgs`, `/org/create` and `/org/:id/events/create` are gone from every client — and no personal
organization, which is neither created nor named anywhere a user or an API can see (D-268).

`events.OrgId` remains a non-null FK, so a self-represented event points at an internal row. That row is
persistence, not domain: it is resolved privately, excluded from `GET /v1/me/representations`, and
reported as `representation.kind = "personal"` with a null organization id and name.

---

## 1. ER — Identity, Trust & Verification

```mermaid
erDiagram
    users ||--o| user_identity_verifications : "1:1 person KYC (M3)"
    users ||--o{ platform_roles : "live-read grants (M2)"
    users ||--o{ refresh_tokens : "rotating sessions"
    users ||--o{ memberships : "org seats"
    users ||--o{ membership_claims : "affiliation claims (M6)"
    organizations ||--o{ memberships : ""
    organizations ||--o{ membership_claims : ""
    organizations ||--o{ organization_aliases : "dedup (M4)"
    organizations ||--o| organizations : "canonical_org_id (merge, M12)"
    organizations ||--o{ org_bank_verifications : "org bank KYC (M9)"
    memberships }o--o| membership_claims : "source_claim_id"

    users {
        uuid   id PK
        string phone UK
        string username UK "nullable; lower() unique"
        string email "lower() unique (D-038)"
    }
    user_identity_verifications {
        uuid   id PK
        uuid   user_id FK "UNIQUE"
        text   level "Phone|Contact|GovernmentId|Bank|Liveness"
        text   status
        string govt_id_last4 "masked only"
        string pan_last4 "masked only"
        string bank_last4 "masked only"
        int    risk_score
    }
    platform_roles {
        uuid id PK
        uuid user_id FK
        text role "SuperAdmin|VerificationReviewer|FinanceOps|Support|ReadOnlyAuditor"
        timestamp expires_at "null=no expiry"
    }
    organizations {
        uuid   id PK
        string slug UK
        text   type "College|Company|... (M4)"
        string primary_domain "hard dedup"
        string normalized_name "soft/fuzzy dedup"
        uuid   canonical_org_id FK
        text   verification_status "Unverified..Blacklisted (M5)"
    }
    organization_aliases {
        uuid id PK
        uuid org_id FK
        string normalized_alias
    }
    memberships {
        uuid id PK
        uuid user_id FK
        uuid org_id FK
        text role "Owner|Manager|Staff|Finance"
        bool is_verified
        uuid source_claim_id FK
    }
    membership_claims {
        uuid id PK
        uuid user_id FK
        uuid org_id FK
        text claimed_role "Student|Faculty|Employee|Alumni|... (M6)"
        text status "Submitted|UnderReview|OfficialContactVerification|Approved|Rejected|Appealed"
        bool fast_track "email-domain hint"
    }
    org_bank_verifications {
        uuid id PK
        uuid org_id FK
        text kind "bank|pan"
        text status
        jsonb payload_json "last-4 only (D-016)"
    }
```

### Shared verification substrate (M0, D-039) — polymorphic, no FK on `subject_id`

```mermaid
erDiagram
    verification_documents }o..o{ SUBJECT : "subject_type + subject_id (no FK)"
    verification_reviews   }o..o{ SUBJECT : "subject_type + subject_id (no FK)"
    users ||--o{ verification_reviews : "reviewer_id (FK, null=system)"

    SUBJECT {
        text kind "UserIdentity | Organization | Membership | Event"
    }
    verification_documents {
        uuid   id PK
        text   subject_type
        uuid   subject_id "no FK"
        string doc_type "open string"
        string storage_key "private storage"
        string sha256 "forgery/dup detection (M13)"
        text   status "Pending|Accepted|Rejected|Superseded"
    }
    verification_reviews {
        uuid id PK
        text subject_type
        uuid subject_id "no FK"
        text decision "Approve|Reject|RequestChanges"
        uuid reviewer_id FK "null = system"
        string reason_code "open string"
        int  risk_score
    }
```

---

## 2. ER — Events, Commerce, Ledger & Fraud

```mermaid
erDiagram
    users ||--o{ events : "OWNS (created_by, D-268)"
    organizations ||--o{ events : "is REPRESENTED BY (org_id — not ownership)"
    organizations ||--|| organization_wallet : "cached balance (D-028)"
    organizations ||--o{ ledger_entries : ""
    organizations ||--o{ payout_schedules : "T1/T2/T3 (D-007)"
    events ||--o{ ticket_types : ""
    ticket_types ||--o{ form_fields : "registration form (D-020, D-050)"
    ticket_types ||--o{ seat_holds : "paid: 10-min hold (M10)"
    events ||--o{ orders : ""
    orders ||--o{ order_items : ""
    orders ||--o| payments : "on capture"
    orders ||--o| groups : "team/group"
    groups ||--o{ group_members : ""
    order_items ||--o{ tickets : "issued on capture/free"
    payments ||--o{ ledger_entries : "Collected on capture"
    tickets ||--o{ gate_entries : "check-in scans"

    events {
        uuid   id PK
        uuid   org_id FK "the org this event REPRESENTS — never its owner (D-268)"
        uuid   created_by FK "THE OWNER (D-268); also the paid-gate subject"
        string slug UK
        string short_code UK
        text   status "Draft..Archived"
    }
    ticket_types {
        uuid id PK
        uuid event_id FK
        bigint price_paise "0 = free"
        bool is_competition "account required (D-036)"
        text registration_mode "Individual|Group"
        int  quantity
        int  sold
    }
    orders {
        uuid id PK
        uuid event_id FK
        uuid user_id FK "nullable = guest (D-036)"
        bigint amount_paise
        text status "Pending|Paid|..."
        string razorpay_order_id
        string guest_access_token
    }
    payments {
        uuid id PK
        uuid order_id FK
        string razorpay_payment_id
        string method "razorpay"
        bigint amount_paise
    }
    ledger_entries {
        uuid id PK
        uuid org_id FK
        bigint amount_paise
        text state "Collected|Available|Advanced|Reserved|Settled"
        string ref_type
        uuid ref_id
    }
    organization_wallet {
        uuid org_id PK
        bigint collected_paise
        bigint available_paise
        bigint reserved_paise
        bigint settled_paise
    }
    blacklist_entries {
        uuid id PK
        text kind "Phone|Email|Device|OrgName|DocHash"
        string value "unique kind+value"
    }
    fraud_signals {
        uuid id PK
        text subject_type "polymorphic"
        uuid subject_id "no FK"
        text kind
        int  score
    }
```

---

## 3. Sequence — Phone OTP auth + refresh rotation (D-009 / D-014 / **D-281** / D-215)

> ⚠️ **This was titled "WhatsApp OTP" and drew a WhatsApp sender. That is not merely stale — it
> contradicts a deliberate security decision.** [D-281](../DECISIONS.md) centralised channel selection
> in `OtpChannelPolicy` and made **SMS** the rail for every phone-addressed purpose (login,
> registration, recovery, password reset, device enrollment, step-up). **WhatsApp is deliberately
> excluded from authentication**: a WhatsApp account is portable across devices and recoverable through
> a takeover chain Kurx does not control, which makes it a weaker credential path than SMS. The
> provider seam, the inbound webhook and the message log all remain — WhatsApp is simply never
> *selected* for a one-time code. Email carries only `EmailLogin`/`EmailVerification`.

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant API as Kurx API
    participant DB as Postgres
    participant SMS as ISmsProvider (console in dev, SNS in prod)

    C->>API: POST /v1/auth/otp/request { phone }
    API->>DB: store HMAC-peppered code (D-215), rate-limit (D-005)
    API->>SMS: send code via OtpChannelPolicy.For(purpose) = Sms (D-281)
    C->>API: POST /v1/auth/otp/verify { phone, code }
    API->>DB: verify hash, create/find user
    API-->>C: access(1h) + refresh(30d, rotated)
    Note over C,API: later — token refresh
    C->>API: POST /v1/auth/refresh { refresh_token }
    alt token already rotated (reuse)
        API->>DB: revoke ALL sessions for user (D-014)
        API-->>C: 401 invalid_token
    else valid
        API->>DB: rotate refresh, keep chain
        API-->>C: new access + refresh pair
    end
```

## 4. Sequence — Paid checkout → capture → ticket + ledger (M8 gate + M10, D-047/D-049)

```mermaid
sequenceDiagram
    autonumber
    participant C as Buyer
    participant API as Kurx API
    participant OS as OrderService
    participant TS as TrustService (live)
    participant GW as PaymentGateway (mock)
    participant RP as Razorpay (out-of-band)
    participant DB as Postgres

    C->>API: POST /v1/events/{id}/orders (priced ticket)
    API->>OS: CreateOrderAsync
    OS->>TS: organizer CanOrganizePaid? org IsOrgVerified? (LIVE, M8)
    alt gate fails (unverified / suspended / fraud)
        OS-->>C: 400 payments_not_enabled
    else gate passes
        OS->>GW: CreateOrderAsync(orderId, price)
        OS->>DB: Pending order + SeatHold(10 min)
        OS-->>C: order + razorpay_order_id
        C->>RP: complete payment (checkout)
        RP->>API: POST /v1/webhooks/razorpay (X-Razorpay-Signature)
        API->>GW: VerifyWebhookSignature (mock: always true)
        API->>OS: ConfirmPaymentAsync(order_id, payment_id)
        Note over OS,DB: idempotent — re-delivered webhook is a no-op
        OS->>DB: Order=Paid, Payment row, issue Ticket,<br/>Collected ledger_entry, update wallet, audit_log
    end
    Note over DB: if never captured, ExpireSeatHoldsJob reclaims the hold (D-029)
```

## 5. Sequence — Organization verification review (M5 / M12, D-044 / D-051)

```mermaid
sequenceDiagram
    autonumber
    participant O as User (prospective representative)
    participant API as Kurx API
    participant OV as OrgVerificationService
    participant R as VerificationReviewer
    participant DB as Postgres

    Note over O,API: Optional and independent of hosting — a user can create<br/>and run events personally without ever coming here (D-267).
    O->>API: submit org + evidence
    API->>DB: verification_documents (subject=Organization), status PendingReview
    R->>API: GET /v1/admin/orgs/pending
    R->>API: POST /v1/admin/orgs/{id}/verification/review { approve|reject|request-changes }
    API->>OV: apply decision
    alt approve
        OV->>DB: hard name-dedup check → Verified + verification_reviews row
    else reject / request-changes
        OV->>DB: Rejected|ChangesRequested + verification_reviews row
    end
    Note over OV,DB: org becomes eligible for CanOrganizePaid only when Verified (feeds M7 → M8/M10)
```

## 6. Sequence — Fraud-clear cascade (M13, D-052)

```mermaid
sequenceDiagram
    autonumber
    participant R as Reviewer
    participant API as Kurx API
    participant FS as FraudService
    participant TS as TrustService
    participant OS as OrderService

    R->>API: POST /v1/admin/blacklist { kind, value } (or fraud-signals)
    API->>FS: RecordSignalAsync / add blacklist_entry
    Note over TS: next request — capabilities recomputed LIVE
    TS->>FS: IsUserClearAsync(user)?
    FS-->>TS: false (blacklisted OR score ≥ 100)
    TS-->>OS: CanOrganizePaid = false
    OS-->>API: paid publish / checkout now blocked (cascades M8 + M10)
```

---

## 7. State — Event lifecycle (`EventStatusWorkflow`)

> Corrected 2026-08-15 against `EventStatusWorkflow.Actions`. This diagram predated **Phase 14**
> (V3 §14.1) and was missing four states — `Scheduled`, `Live`, `Completed`, `Cancelled` — and five
> transitions: `schedule`, `open_registration`, `go_live`, `complete`, `cancel`. It showed 15 of the
> 19 that exist.
>
> **`Published` is still the authoritative registration-open state** — the Phase-14 states were added
> *around* it, not in front of it, and the direct `publish` is preserved. `Cancelled` is terminal and
> deliberately has no path back to `Published`: a cancelled event is cloned, never resurrected (D-101).

```mermaid
stateDiagram-v2
    [*] --> Draft

    %% ── review leg (D-266 M4) ──
    Draft --> PendingReview : submit_for_review<br/>(submit_review = legacy alias)
    PendingReview --> UnderReview : claim_review
    PendingReview --> Draft : withdraw / reject (legacy)
    UnderReview --> PendingReview : release_review
    UnderReview --> Approved : approve_review
    UnderReview --> ChangesRequested : request_changes
    UnderReview --> Rejected : reject_review
    UnderReview --> Draft : reject (legacy)
    ChangesRequested --> PendingReview : submit_for_review
    Rejected --> PendingReview : submit_for_review

    %% ── publish doors ──
    Draft --> Published : publish (Private — never reviewed)
    Approved --> Published : publish_approved / publish
    PendingReview --> Published : publish (legacy one-step approve+publish)
    UnderReview --> Published : publish (legacy one-step approve+publish)

    %% ── Phase 14 granular lifecycle (V3 §14.1) ──
    Draft --> Scheduled : schedule
    Approved --> Scheduled : schedule / publish_approved
    Scheduled --> Published : open_registration
    Scheduled --> Draft : unpublish
    Published --> Live : go_live
    Published --> Completed : complete
    Live --> Completed : complete

    %% ── exits ──
    Published --> Closed : close
    Published --> Draft : unpublish
    Draft --> Cancelled : cancel
    PendingReview --> Cancelled : cancel
    UnderReview --> Cancelled : cancel
    Scheduled --> Cancelled : cancel
    Published --> Cancelled : cancel
    Live --> Cancelled : cancel
    Draft --> Archived : archive
    Closed --> Archived : archive
    Completed --> Archived : archive
    Cancelled --> Archived : archive
    Archived --> [*]
```

**Complete as of 2026-08-15** — all **19 actions / 37 `(From, To)` pairs** in
`EventStatusWorkflow.Actions`, deduplicated to 33 edges where two action names share a transition.
`publish` retains three legacy doors (from `Draft`, and the one-step approve-and-publish from
`PendingReview`/`UnderReview`) which are kept because clients still post them; `publish_approved` from
`Approved` is the M4 path, not their replacement. `Archived` is terminal; `Cancelled` reaches it but
never returns to `Published`.

**This is a map of which transitions exist, not of who may take them or when.** Three guards sit on top
of it and are not drawable as edges:

- **`→ Published` is gated by status *and* actor** (D-362). From `PendingReview`/`UnderReview` only a
  reviewer or admin may take it — a creator is refused `reviewer_required`. From `Approved` the creator
  takes it themselves, paid or free. The gate keys on the *target* state, so both `publish` and
  `publish_approved` pass through it. A paid event is refused `paid_event_requires_review` from `Draft`
  only, `Draft` being the one status reaching `Published` that has not been reviewed.
- **`unpublish` is refused with `event_has_history`** once the event has any order, ticket or
  registration (D-363) — from `Published` and `Scheduled` alike.
- **Only `Published` is publicly visible.** `Approved` and `Scheduled` are not; `EventExposure` carries
  the status as part of the one exposure rule.

## 8. State — Organization verification (M5, `Organization.verification_status`)

```mermaid
stateDiagram-v2
    [*] --> Unverified
    Unverified --> PendingReview : submit + evidence
    ChangesRequested --> PendingReview : resubmit
    Rejected --> PendingReview : resubmit

    PendingReview --> Verified : review approve (name/domain dedup passes)
    PendingReview --> Rejected : review reject
    PendingReview --> ChangesRequested : review request-changes
    ChangesRequested --> Verified : review approve
    ChangesRequested --> Rejected : review reject

    Verified --> Suspended : suspend (admin / fraud)
    Verified --> Blacklisted : blacklist
    PendingReview --> Blacklisted : blacklist
    Suspended --> Blacklisted : blacklist
    Blacklisted --> [*]
```

**Guards checked against `OrgVerificationService` 2026-08-15 — one edge removed, three added.**

- **`Suspended → Verified : reinstate` was drawn and does not exist.** There is no reinstate method or
  endpoint; `SuspendAsync` is one-way, `ReviewAsync` refuses anything but `PendingReview`/
  `ChangesRequested` (`not_pending`), and `SubmitAsync` explicitly refuses `Suspended`. **`Suspended` is
  terminal apart from blacklisting** — a suspended organization has no path back today.
- `ReviewAsync` accepts `ChangesRequested` as well as `PendingReview`, so an org in changes-requested can
  be approved or rejected directly without resubmitting; both edges were missing.
- `BlacklistAsync` has **no state guard at all** — any state can be blacklisted. The diagram shows the
  three reachable-in-practice sources; treat blacklist as available everywhere.
- `Unverified` is the entity default (`Orgs.cs`), never assigned by the service.

## 9. State — Person identity verification (M3, `user_identity_verifications.status`)

```mermaid
stateDiagram-v2
    [*] --> NotStarted
    NotStarted --> Submitted : submit government-id / pan / bank
    Submitted --> UnderReview : provider / reviewer picks up
    UnderReview --> Approved
    UnderReview --> Rejected
    Approved --> [*]
    Rejected --> [*]

    UnderReview --> ChangesRequested : modeled; unreachable
    ChangesRequested --> Submitted : modeled; unreachable
    Approved --> Expired : modeled; unreachable
    Approved --> Revoked : modeled; unreachable
```

**Reachability checked against code 2026-08-15.** `IdentityVerificationService` only ever assigns
`NotStarted · Submitted · UnderReview · Approved · Rejected`. **`ChangesRequested`, `Expired` and
`Revoked` exist on the `IdentityStatus` enum but have zero non-test assignment sites** — `ExpiresAt` is
projected into the response and read by nothing, and no job expires or revokes an identity. Those four
edges were drawn as if live; they are labelled here rather than deleted because the enum genuinely
carries the states, and the same convention already applies to §10's refund edges. **Reaching them is
unimplemented work, not a documentation gap.**

## 10. State — Order / payment (free vs paid, M10)

```mermaid
stateDiagram-v2
    [*] --> Paid : free ticket — issued immediately
    [*] --> Pending : paid ticket, M8 gate passed
    Pending --> Paid : Razorpay capture webhook → ConfirmPaymentAsync
    Pending --> Failed : payment failure (modeled)
    Paid --> Refunded : refund (modeled; flow deferred)
    Paid --> PartiallyRefunded : partial refund (modeled; flow deferred)
    Paid --> [*]
    Failed --> [*]
    note right of Pending : if never captured, ExpireSeatHoldsJob (D-029) reclaims the SeatHold; order stays Pending<br/>OrderStatus enum: Pending, Paid, Failed, Refunded, PartiallyRefunded
```

## 11. State — Membership claim (M6)

```mermaid
stateDiagram-v2
    [*] --> Submitted : POST membership-claims (user + evidence)
    Submitted --> UnderReview : reviewer picks up
    UnderReview --> OfficialContactVerification : verify via org's official contact
    OfficialContactVerification --> Approved
    UnderReview --> Approved : approve → verified read-only Staff seat
    UnderReview --> Rejected : reviewer reject
    Approved --> [*]
    Rejected --> [*]

    Rejected --> Appealed : modeled; unreachable
    Appealed --> UnderReview : modeled; unreachable
```

**Reachability checked against code 2026-08-15.** `MembershipVerificationService` assigns
`Submitted · UnderReview · OfficialContactVerification · Approved · Rejected`.
**`MembershipClaimStatus.Appealed` is assigned and read *nowhere* in the backend** — there is no appeal
endpoint, and the only mention of appeals is a comment in `AdminOrgEndpoints.cs` naming them as M12
work. The two edges above were drawn as live; `Rejected` is terminal in shipped code.

---

## 12. Flow — Event authority (D-269)

**Who may act on an event.** One resolution, one ordered level, every permission a threshold on it. This
is deliberately independent of the Capability Engine (D-266 M2), which answers a different question —
*what does this event support* — and the two never consult each other.

```mermaid
flowchart LR
    C([Caller]) --> R{ResolveAsync<br/>userId · eventId · isAdmin}
    R -->|KurxAdmin| L4[Admin]
    R -->|Event.CreatedBy == caller| L3[Manager]
    R -->|Representative / Owner / Manager seat| L3
    R -->|Staff seat| L2[Staff]
    R -->|active participation, or a live ticket| L1[Participant]
    R -->|Finance seat, or nothing| L0[None]
    L4 & L3 & L2 & L1 & L0 --> T[["EventAuthority.Requirements<br/>permission → minimum level"]]
    T --> M[["Management<br/>ViewAttendees · ViewAnalytics<br/>ManageContent · ManageLifecycle · Delete"]]
    T --> A[["Audience (D-272)<br/>Participate · ModerateAudience"]]
    M & A --> D([Can / cannot])
```

The creator branch needs **no membership** — that is what makes a personally-represented event manageable
by its owner (D-268). A new event capability adds an `EventPermission` value and one row in the
requirements table; it writes no authorization logic.

**Audience surfaces resolve here too (D-272)** — the event feed, attaching a post to an event, and chat
`Host` all threshold on this same ladder rather than querying `Memberships` themselves. An organization
membership is never, by itself, event access: every seat passes through the one `LevelFor` mapping, and
`Finance` maps to `None` on audience surfaces exactly as on management ones.
