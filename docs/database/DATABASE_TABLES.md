# Kurx — Database Tables Reference

All tables in the `kurx` Postgres database. Enums are stored as text strings.  
Amounts are always in **paise** (₹1 = 100 paise). Timestamps are UTC.

> ⚠️ **Column names here are normalised; the real ones are quoted PascalCase.** Most sections below write
> `id`, `event_id`, `created_at` for readability, but EF maps properties without a naming convention, so the
> actual identifiers are `"Id"`, `"EventId"`, `"CreatedAt"`. `SELECT id FROM users` **fails**;
> `SELECT "Id" FROM users` works. Table names *are* snake_case as written. Sections added from 2026-08-12
> onward use the real column names. Verify against `information_schema.columns` before writing raw SQL.
>
> **Coverage, measured 2026-08-12:** the database has **158 tables**
> (`grep -c 'b.ToTable(' backend/Kurx.Infrastructure/Migrations/KurxDbContextModelSnapshot.cs`). Ten were
> undocumented until that date — the allies, gamification, chat-reaction and search-index tables added
> below. Some remaining tables are covered by group prose rather than an individual entry; a missing
> heading is not proof a table does not exist.

---

## Users & Auth

### `users`
One row per registered user. Historically phone-first and passwordless; passwords now live in the
separate `user_credentials` table (D-126), never on this entity.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| phone | text UNIQUE | E.164 format, primary identity |
| name | text | display name |
| email | text | optional; case-insensitive UNIQUE (functional index on `lower(email)`, filtered `WHERE email IS NOT NULL`, D-038) — no endpoint sets it yet |
| username | text UNIQUE, nullable | lowercase; mandatory to complete onboarding, enforced in the application layer (D-037) — column stays nullable so existing rows never violate a retroactive NOT NULL |
| headline | text | short tagline — **display-only, authority-zero (M1)** |
| bio | text | long description — **display-only, authority-zero (M1)** |
| education_json | jsonb | self-declared, **unverified** `[{institute, degree, branch, start_year, end_year}]`; migrated to `membership_claims` in M6 |
| skills | text[] | array of skill strings — display-only |
| links_json | jsonb | `{github, linkedin, website, instagram}` — display-only |
| avatar_key | text | storage object key |
| cover_key | text | storage object key |
| profile_public | bool | default true |
| show_attended | bool | default false |
| show_certificates | bool | default true |
| username_changed_at | timestamp | throttles username changes |
| created_at | timestamp | |

> `is_kurx_admin` was **removed in M1 (D-041)** — platform authority now lives in `platform_roles` (M2, D-040).

---

### `refresh_tokens`
Rotating refresh tokens (30-day, single-use). Revoked on use.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | |
| token_hash | text UNIQUE | SHA-256 of the raw token |
| expires_at | timestamp | |
| revoked_at | timestamp | null = still valid |
| created_at | timestamp | |

---

### `username_history`
Tracks released usernames so they can be reclaimed after 30 days.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| username | text | |
| user_id | uuid | previous owner |
| released_at | timestamp | reclaimable 30 days after this |

---

### `username_change_log`
Full audit trail of every username change. One row per change.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | |
| old_username | text | null on first username set |
| new_username | text | |
| changed_at | timestamp | |

---

### `otp_requests`

> ⚠️ **Retired from the write path (D-215, 2026-08-01) — no longer written or read by anything.**
> Login and the D-037 phone change now mint and verify codes through `IOtpService`, which stores
> them in **`otp_codes`** as `HMACSHA256(OTP_PEPPER)`. This table stored a **plain, unsalted
> SHA-256** of a 6-digit code — a 10⁶ keyspace that is fully precomputable, making `code_hash`
> effectively plaintext to anyone who could read the table. The rows are left in place (an empty,
> unreferenced table is harmless); dropping it is a destructive schema change and needs its own
> decision. **Do not add new readers or writers.**

Historical shape, one row per OTP send:

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| phone | text | |
| code_hash | text | **unsalted SHA-256** of the 6-digit code (not bcrypt, as this table previously claimed) — the defect D-215 retired |
| request_ip | text | for IP-level rate limiting |
| verify_attempts | int | max 5 before code is dead |
| consumed | bool | true after successful verify |
| expires_at | timestamp | 5 min window (`AuthService.OtpTtl`, not the 10 min previously documented) |
| created_at | timestamp | |

---

### `devices`
FCM push tokens — one row per device per user.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | |
| fcm_token | text UNIQUE | Firebase Cloud Messaging token |
| platform | text | `android` or `ios` |
| last_seen | timestamp | updated on each app open |

### `user_credentials`
Password credential — **factor 1** of the trusted-device architecture (D-126). One row per user, created
only once that user sets a password (existing OTP-era accounts have no row until they do).

Deliberately **not** columns on `users`: that entity is projected by dozens of queries across orgs,
events, orders and admin, and a hash there would sit one careless `Select` from a response body
(ADR-AM11 — auth is its own aggregate).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | UUID v7 |
| user_id | uuid FK → users UNIQUE | unique, not merely indexed, so a concurrent "set password" race cannot leave two credential rows and an ambiguous verifier |
| password_hash | text | full PHC string `$argon2id$v=19$m=19456,t=2,p=1$<salt>$<hash>` — parameters travel with the hash, so cost can be raised later without invalidating existing passwords |
| algorithm | text | `argon2id` |
| failed_attempts | int | lockout counter; in Postgres not cache — an in-process counter resets on every deploy, exactly when an attacker mid-spray benefits |
| locked_until | timestamp, nullable | set while locked; cleared lazily on next success (no sweeper job) |
| last_successful_at / last_failed_at | timestamp, nullable | |
| created_at / updated_at | timestamp | |

### `password_history`
Previous password hashes, so a "change" cannot silently be a reuse (D-126). Reuse is detected by
**verifying the candidate against each stored hash** — never by comparing hashes, which different salts
make meaningless. Bounded to the last 5 per user; older rows pruned on write.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | UUID v7 |
| user_id | uuid FK → users | indexed with created_at |
| password_hash | text | PHC string |
| created_at | timestamp | |

### `trusted_browsers`
A browser the user chose to trust. Satisfies **factor 2 only — never the password** (D-127): if it
skipped the password the cookie would be a bearer credential for the whole account.

Only the hash is stored; the raw token lives solely in the browser cookie, so a database disclosure
yields nothing replayable — the same reasoning as `refresh_tokens`.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | UUID v7 |
| user_id | uuid FK → users | indexed with revoked_at |
| token_hash | text UNIQUE | SHA-256 of the opaque cookie token — never the token |
| label | text, nullable | user-editable; defaults to a browser/OS summary |
| browser / operating_system | text, nullable | e.g. `Chrome 140`, `Windows 11` |
| user_agent | text, nullable | full UA, for forensics |
| ip | text, nullable | |
| approx_location | text, nullable | coarse, city-level at most |
| created_at / last_used_at | timestamp | |
| expires_at | timestamp | product default 30 days |
| revoked_at | timestamp, nullable | |
| revoke_reason | text, nullable | `user_revoked` \| `password_changed` \| `recovery` |

> **Known documentation gap (pre-existing):** the AM0–AM10 trusted-device tables — `trusted_devices`,
> `device_credentials`, `auth_challenges`, `auth_sessions`, `security_events`, `recovery_codes`,
> `otp_codes`, `outbox_messages`, `signing_keys` — are live in the schema but have never been documented
> in this file. They are specified in `docs/auth/AUTHENTICATION_DATABASE.md` §2. `auth_challenges` gained
> a `match_attempts int NOT NULL DEFAULT 0` column in D-126's migration (the 2-digit confirmation cap).

---

## Organizations

### `organizations`
Institutions a user may **represent** — never accounts anyone owns, and never the owner of an event
(D-268). An event's owner is `events.created_by`; `events."OrgId"` records which institution that event
*represents*, which affects branding, verification, trust, permissions and settlement only. The rows here
are the registry those representations point at, plus their seats (`memberships`) and payout account.

> **Column vs. field name (D-273a).** The entity property is `Event.RepresentingOrgId` and the wire field is
> `representing_org_id`; the **column is still `"OrgId"`**, pinned with `HasColumnName` so the rename needed
> no migration and the application stays rollback-independent of the database. Treat the column name as an
> internal storage detail — it is not the domain vocabulary and never reaches a client.

> A row with `is_personal = true` is **not** an organization in the domain. It is the persistence detail a
> self-represented event points at, because the representation FK is non-null (D-055/D-075/D-268/D-273a each
> weighed nullability and deferred it; D-273b owns it). It is resolved privately, never named in an API,
> DTO, route or label, and never listed among the organizations anyone represents.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| name | text | |
| slug | text UNIQUE | URL-friendly name |
| logo_key | text | storage object key |
| bio | text | |
| links_json | jsonb | social / website links |
| type | text | **(M4)** `College`/`School`/`University`/`Company`/`Startup`/`NGO`/`Club`/`Community`/`Government`/`Other` |
| legal_name | text, nullable | **(M4)** full legal/registered name |
| primary_domain | text, nullable | **(M4)** e.g. `nsrit.edu.in` — UNIQUE among active orgs (hard dedup) |
| canonical_org_id | uuid | **(M4)** = id normally; points at the survivor after a merge (M5) |
| normalized_name | text | **(M4)** lowercase, punctuation-collapsed; GIN pg_trgm index for fuzzy dedup |
| verification_status | text | **(M5)** `Unverified`/`PendingReview`/`ChangesRequested`/`Verified`/`Rejected`/`Suspended`/`Blacklisted` |
| verification_reviewed_by | uuid, nullable | **(M5)** platform VerificationReviewer who last decided |
| verification_reviewed_at | timestamp, nullable | **(M5)** |
| verification_notes | text, nullable | **(M5)** reviewer note (admin-facing) |
| razorpay_linked_account_id | text | Razorpay route account |
| payout_account_status | text | `None`, `Pending`, `Active` |
| bank_last4 | text | last 4 digits of bank account (never full number) |
| created_at | timestamp | |
| deleted_at | timestamp | soft-delete marker; null = active |

Indexes: unique slug (active), unique primary_domain (active), GIN trigram on
normalized_name, canonical_org_id.

### `organization_aliases`
Alternate names an org is known by (acronym, former name, expansion). Search resolves
any alias to the org's canonical row so future organizers reuse one entity (M4, D-043).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations (CASCADE) | |
| alias | text | display form (e.g. `NSRIT`) |
| normalized_alias | text | normalized for matching; GIN pg_trgm index |
| source | text | `Official` \| `User` \| `Import` |
| created_at | timestamp | |

Indexes: `org_id`, unique `(org_id, normalized_alias)`, GIN trigram on normalized_alias.

---

### `org_units`
An organisation's structural tree (**V3 §4.1**, Phase 4). Universities and companies are the *same* recursive
tree with different `kind` labels — one hierarchy powers permissions, rollup, discovery scoping and audience
rules. Every org has exactly one **root** unit (`parent_id` null); a solo organiser's tree is that single node
and costs nothing (the picker never renders, `events.org_unit_id` auto-sets). Ownership lives here; *who may
register* is a separate `AudienceRule` (§4.4, Phase 5), never conflated.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations (CASCADE) | |
| parent_id | uuid FK → org_units, nullable | null = the org's root unit |
| kind | varchar(40) | open list — `organization` (root default), `department`, `campus`, `team`, `committee`, … |
| name | text | |
| path | text | materialised ancestor path incl. self, `/{rootId}/{childId}/`; ancestors = ids in path except last |
| state | text | `Active` \| `Archived` \| `Tombstone` (§4.2; only `Active` used today) |
| created_at | timestamp | |

Indexes: `org_id`; `parent_id`; **partial unique** `ix_org_units_one_root_per_org` on `org_id` where
`parent_id IS NULL` (DB-enforces one root per org). No index on `path` yet — ancestor lookup parses the path
in-app and nothing runs a `LIKE`-prefix subtree query yet (Phase 5 evaluates unit eligibility at root
granularity in memory); the matching `text_pattern_ops` index lands when a later phase runs subtree queries (a
plain btree can't serve a left-anchored `LIKE` prefix under a UTF-8 collation). Backfilled
on migration: one root per existing org, and every existing event repointed to its org's root **in batches**
(a COMMIT-per-batch temp procedure) so a large `events` table is never rewritten under one long lock.

---

### `memberships`
User ↔ Org join with a role. Unique per user+org pair.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | |
| org_id | uuid FK → organizations | |
| role | text | `Owner`, `Manager`, `Staff`, `Finance`, `Representative` (**D-075** — verified institution rep; manage-capable, never Owner) |
| show_on_profile | bool | whether this membership is shown on the user's public profile (default true) |
| is_verified | bool | **(M6/D-075)** verified seat — set on an approved affiliation claim (M6) or when an admin approves the member's representation request (D-075). Default false; a `Representative` stays `false` until approval. M7 `CanRepresentOrg` reads this. |
| source_claim_id | uuid, nullable | **(M6)** the approved `membership_claims` row |
| valid_until | timestamp, nullable | **(M6)** e.g. student membership expiry |
| created_at | timestamp | |
| attributes | jsonb, nullable | **(V3 §4.3, Phase 5)** audience-rule match data: `{ cohort_year, section, roll_no, … }`. `cohort_year` is the joining batch, never an ordinal year |
| source | text | **(V3 §4.3, Phase 5)** `SelfDeclared` (default) \| `Invited` \| `Imported` \| `Provisioned`; rules with `require_verified` reject self-declared |

---

### `audience_rules`
Who may REGISTER for an event (**V3 §4.4**, Phase 5). **DENY BY DEFAULT** when a rule exists; no rule ⇒ the
event is open (backward compatible). Ownership (`events.org_unit_id`) and audience are always separate. Evaluated
server-side at registration and re-checked at admission. One rule per event.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (CASCADE), **unique** | one rule per event |
| unit_subtree_in | jsonb, nullable | `Guid[]` — org_unit subtrees the member must sit under (root granularity today) |
| role_in | jsonb, nullable | `string[]` — allowed `OrgRole` names (RBAC role; affiliate-role gating is future) |
| cohort_year_in | jsonb, nullable | `int[]` — allowed cohort (joining) years, matched against `memberships.attributes` |
| attribute_matches | jsonb, nullable | `{ key: value }` — all must equal the member's attributes |
| require_verified | bool | **enforced** — the membership must be `is_verified` **and** its `source` ≠ `SelfDeclared` (V3 §4.3) |
| external_orgs_allowed | bool | **enforced** — a non-member may still register |
| applies_to | text | **stored, not yet enforced** — `EveryMember` (default) \| `CaptainOnly` \| `AtLeastN` \| `TeamAttribute`; only the individual (`EveryMember`) case is evaluated, the team variants activate with Teams (Phase 10) |
| guests_allowed | bool | **enforced** — guest checkout permitted under the rule |
| guest_per_registrant_cap | int | **stored, not yet enforced** (aggregate pool deferred to Phase 7) |
| guests_require_approval | bool | **stored, not yet enforced** (guest-approval workflow is a future phase) |
| created_at / updated_at | timestamp | |

Indexes: unique `event_id`. **Enforced today:** `unit_subtree_in`, `role_in`, `cohort_year_in`, `attribute_matches`,
`require_verified`, `external_orgs_allowed`, `guests_allowed`. **Stored for a future phase (not yet enforced):**
`applies_to`, `guest_per_registrant_cap`, `guests_require_approval`.

---

### `membership_claims`
An evidence-backed claim that a user holds a role at an org (M6, D-045). Reviewed
independently of profile bio; on approval marks the user's membership verified (creating
a read-only Staff seat if none). Evidence in `verification_documents` (subject = Membership).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users (CASCADE) | |
| org_id | uuid FK → organizations (CASCADE) | |
| claimed_role | text | `Student`/`Faculty`/`Employee`/`Alumni`/`Founder`/`Director`/`Coordinator`/`Volunteer`/`ClubPresident`/`EventLead`/`Other` |
| status | text | `Submitted`/`UnderReview`/`OfficialContactVerification`/`Approved`/`Rejected`/`Appealed` |
| fast_track | bool | email domain matched org's primary_domain at submit (review hint, never auto-approve) |
| contact_verified_at | timestamp, nullable | official-contact verification (future workflow) |
| valid_until | timestamp, nullable | |
| reviewed_by | uuid FK → users (RESTRICT), nullable | platform VerificationReviewer |
| reviewed_at | timestamp, nullable | |
| notes | text, nullable | |
| created_at / updated_at | timestamp | |

Indexes: `user_id`, `(org_id, status)`, `status`.

---

### `org_bank_verifications`
Organization **bank** verification attempts (penny drop, PAN, Digilocker) — the org's
financial verification (D-016). **Renamed from `kyc_records` in M9 (D-048)**; "KYC" now
means *person* identity (`user_identity_verifications`, M3). Reached via `/v1/orgs/{orgId}/kyc/*`.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations | |
| kind | text | `PennyDrop`, `PanMatch`, `Digilocker` |
| status | text | `Pending`, `Approved`, `Rejected` |
| payload_json | jsonb | provider-specific verification data |
| reviewed_by | uuid | admin user id |
| reviewed_at | timestamp | |
| created_at | timestamp | |

---

### `org_invitations`
GitHub-style org membership invitations. Time-limited token (7 days), single-use, requires explicit accept/decline (D-027).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations (cascade) | |
| invited_by | uuid FK → users | org member sending the invitation |
| invited_user_id | uuid FK → users | null until the invitee creates an account |
| invited_phone | text | E.164; used to match to an existing or new user |
| role | text | role to grant on accept (`Owner`, `Manager`, `Staff`, `Finance`) |
| status | text | `Pending`, `Accepted`, `Declined`, `Expired`, `Revoked` |
| token | text UNIQUE | time-limited, single-use claim token |
| expires_at | timestamp | created_at + 7 days; claim rejected after this |
| accepted_at | timestamp | set when the invitee accepts |
| created_at | timestamp | |

---

### `organization_followers`
User follows an org to receive event discovery recommendations. Composite PK, no extra columns.

| Column | Type | Notes |
|--------|------|-------|
| user_id | uuid PK FK → users | composite PK |
| org_id | uuid PK FK → organizations | composite PK |
| created_at | timestamp | |

---

### `risk_flags`
Fraud / compliance flags raised against an org or event.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations | |
| event_id | uuid | optional — flags a specific event |
| kind | text | `UniqueBuyer`, `Velocity`, `Chargeback`, `Capacity` |
| severity | text | `low`, `medium`, `high` |
| details_json | jsonb | evidence / context |
| status | text | `Open`, `Cleared`, `Frozen` |
| created_at | timestamp | |

---

### `payout_schedules`
One row per org — their payout tier and advance rules.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid UNIQUE FK → organizations | |
| tier | int | 1 = new org, higher = trusted |
| advance_pct | int | % of collected funds released as advance |
| cap_paise | long | max advance amount |
| reserve_pct | int | % held back post-event |
| next_run_at | timestamp | scheduler trigger |
| clean_event_count | int | completed events with no flags (drives tier promotion) |
| created_at | timestamp | |

---

## Events & Taxonomy

### `event_categories`
3-level taxonomy: Audience → Category → Type (e.g. Tech → Conference → Hackathon).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| parent_id | uuid | self-ref FK |
| level | text | `Audience`, `Category`, `Type` |
| name | text | |
| slug | text UNIQUE | |
| sort | int | display order |
| is_visible | bool | hides from public browse if false |
| archetype_slug | text | D-266 — → `event_archetypes.slug` (by slug, no FK). Set on Type rows; the single join between the taxonomy and the behaviour model |
| product_class | text | D-266 — `Public` / `Private`. A Wedding can never be Public; a Fundraiser can never be Private. The constraint lives here as data, not as code |
| allowed_registration_policies | jsonb | D-266 — `EventRegistrationPolicy[]` this Type permits; null inherits the archetype default |

---

### `event_archetypes`
D-266 — the 14 behavioural archetypes that decide how an event actually behaves, superseding `event_kinds`
(archived, not dropped: existing events reference it). Seeded and then admin-owned, like the taxonomy itself
(D-188). Referenced by `events.archetype_slug` and `event_categories.archetype_slug` by slug, no FK.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| slug | text UNIQUE | stable, never changes once events reference it |
| name | text | |
| description | text | |
| product | text | `Public` / `Private`. No archetype spans both — that would reintroduce the "Both" classification the taxonomy audit removed |
| sort | int | display order |
| is_active | bool | |
| requires_representation | bool | D-266 M5 — D12 §6's *Representation: Required* column (A7 Recruitment · A12 Festival · A13 Ceremonial). These archetypes cannot be run in a personal capacity; a self-represented one reports the `representation_required` publish blocker. Stored rather than branched on, per D12's rule that no validation is hardcoded |

---

### `event_authorizations`
D-266 M5 — the represented institution's **consent to this one event**. **Not** `IEventAuthority` (D-269),
which resolves who may act and stores nothing; and not a replacement for `organizations.verification_status`
(D-044), which says the institution is real rather than that it agreed to this. One row per event: a second
would make "is this event authorised?" a query with more than one answer.

An approved row is what clears the `event_authorization_required` publish blocker for a Public event that
represents a non-personal organization. Resubmitting returns the row to `Submitted` and clears the prior
verdict — evidence replaced after review has not been reviewed.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid UNIQUE FK | -> `events.id`, cascade. Unique: one authorization per event |
| submitted_by | uuid FK | -> `users.id`. Who filed it — deliberately distinct from `events.created_by`; a manager may file on the owner's behalf |
| head_name | varchar(160) | the institutional signatory |
| head_designation | varchar(160) | |
| official_email | varchar(255) | |
| official_phone | varchar(20) NOT NULL | E.164. A signatory who cannot be reached is not a verifiable one — contacting them is the reviewer's only check independent of the letter |
| representative_role | varchar(60) NOT NULL | from the closed `RepresentativeRoles.All` vocabulary, validated server-side. Free text would make "who authorises institutional events" unanswerable |
| representative_role_other | varchar(80) | the typed title when the role is `Other`; cleared otherwise, so there are never two answers to one question |
| representative_user_id | uuid FK | -> `users.id`, `ON DELETE SET NULL`. **A link, never a grant** — it confers no authority over the event (D-269) |
| letterhead_document_key | varchar(400) | **storage key, never a URL** — presigned on read. Required on a FIRST filing; omitting it on a re-file keeps the one already stored, because a client is never given the key |
| signature_document_key | varchar(400) | nullable, same discipline |
| supporting_documents | jsonb | `string[]` of storage keys, max 10 |
| status | text | `Submitted` \| `Approved` \| `Rejected` \| `ChangesRequested`. No `Draft`: an authorization exists once filed, and a half-typed form is client state |
| reviewer_id | uuid | nullable until decided |
| reviewed_at | timestamptz | |
| reason_code | varchar(40) | an `EventReviewReason` name — the M4 vocabulary reused, not a second enum. Required to reject |
| notes | varchar(2000) | reviewer note; required for `ChangesRequested`, which exists so the organiser can act on it |
| created_at / updated_at | timestamptz | |

---

### `event_review_checklist_items`
D-266 M7 — one reviewer's tick against one checklist item on one event.

**The item list is not stored here.** It is a projection of `PolicyResolver.ReviewerChecklist`, the same
array the publish blockers come from. Persisting the items would freeze the list at the moment the event
entered review, so a rule added later would be invisible to whoever approves it — and they would tick a
complete-looking list while the publish still refused. This table records only *which of those items a
reviewer has ticked*.

`approve_review` returns `checklist_incomplete` until every current item is ticked **by that reviewer**.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| reviewer_id | uuid FK → users | Per reviewer, not per event: release-and-reclaim must not inherit another reviewer's sign-off |
| item_key | varchar(60) | The requirement code (`event_authorization_required`, `invitation_list_required`, …). A stable code, never display text — copy is the client's and changing it must not orphan ticks |
| checked | bool | |
| checked_at | timestamptz | Cleared on untick; a timestamp surviving one would read as a confirmation nobody stands behind |

Unique on `(event_id, reviewer_id, item_key)` — a second row would make "is the checklist complete?"
ambiguous, which is the only question the table exists to answer.

---

### `archetype_capability_defaults`
D-266 M2 - the D12 archetype x capability matrix, 420 rows (14 x 30). **Every cell is stored, including
`Unsupported`** - the retired `kind_capability_defaults` stored only positive cells and read absence as
"available but off", which cannot express "may never enable". Seeded from the D12 transcription, then
admin-owned (D-188); the seeder re-syncs drift and deletes orphans.

| Column | Type | Notes |
|--------|------|-------|
| archetype_slug | text PK | -> `event_archetypes.slug` |
| capability_slug | text PK | -> `capabilities.slug` |
| rule | text | `Required` \| `Optional` \| `Unsupported` |

---

### `event_kinds`
**Retired by D-266 M2** (archived, not dropped: existing events reference it).
V3 closed 20-Kind catalog (Event Architecture V3 §2, Phase 1). A Kind is an analysable bundle of capability
*defaults*; it never gates engine behaviour. Seeded; referenced by `events.kind_slug` (by slug, no FK).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| slug | text UNIQUE | stable id, e.g. `hackathon` |
| name | text | display, e.g. `Hackathon` |
| group_slug | text | one of the 7 V3 groups, e.g. `competitive` |
| group_name | text | display group |
| sort | int | catalog order |
| is_active | bool | |

---

### `kind_aliases`
The 145 legacy taxonomy Type names, each mapped to a Kind (Event Architecture V3 §2, Phase 1). Data-driven:
a mapping is corrected by editing a row (the seeder only inserts missing rows, never overwrites).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| alias | text | display, e.g. `Ideathon` |
| normalized_alias | text UNIQUE | the legacy Type slug — the resolution key for backfill/derivation |
| kind_slug | text | → `event_kinds.slug` |

---

### `capabilities`
V3 event-capability registry (Event Architecture V3 §11, Phase 2) — the ~45 capabilities. Distinct from
trust capabilities and the D-116 workspace-capabilities. Slug-referenced (no FK from the reference tables).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| slug | text UNIQUE | stable id, e.g. `scoring` |
| name | text | display |
| group_slug | text | universal / structure / people / commerce / output |
| is_universal | bool | §19 — universal to every Kind, always REQUIRED |
| workspace_tab | text | the tab it renders (drives the Phase-15 generated workspace); nullable |
| depends_on_json | jsonb | capability slugs this one requires (§11.4 shallow DAG) |
| available_modes_json | jsonb | event modes it's available in (§11.3 mode-gating) |
| sort | int | catalog order |
| is_active | bool | |

---

### `kind_capability_defaults`
The V3 §19 Kind×Capability matrix (Phase 2). Only REQUIRED/ON cells are stored; an absent row means OFF.

| Column | Type | Notes |
|--------|------|-------|
| kind_slug | text | → `event_kinds.slug` (PK part) |
| capability_slug | text | → `capabilities.slug` (PK part) |
| state | text | `Required` or `On` |

---

### `event_capabilities`
The resolved capability set for one event (Phase 2), materialized from its Kind × mode × dependencies.
Only non-OFF rows are stored; OFF = absence. Cascade-deleted with the event.

| Column | Type | Notes |
|--------|------|-------|
| event_id | uuid FK → events | PK part |
| capability_slug | text | → `capabilities.slug` (PK part) |
| state | text | `Required` / `On` / `Locked` |
| config_json | jsonb | per-event capability config (later phases); nullable |
| locked_reason | text | set when `state = Locked` (§11.2); nullable |

---

### `events`
The core event record. **A user owns an event; an organization never does** (D-268) — see `created_by`
and `org_id` below, which are frequently misread as the other way round.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations | **The organization this event REPRESENTS — not its owner** (D-268). Affects branding, verification, trust, permissions and settlement only. Non-null, so a self-represented ("Personal") event points at an internal `is_personal` row that is never surfaced; D-055/D-075/D-268 each weighed nullability and rejected it (400+ dereference sites across ~30 services). **The name is known debt** — it stores a representation and reads like ownership; the rename is tracked in D-268 |
| org_unit_id | uuid FK → org_units, nullable | **(V3 §4.1, Phase 4)** the unit within the represented org's tree that the event is filed under; auto-set to that org's root unit |
| parent_event_id | uuid | self-ref FK — sub-events (COMPOSITION); **(V3 §3.4, Phase 12)** composition depth is capped at **≤ 3**, enforced in the API on create (`max_composition_depth`) |
| series_id | uuid FK → event_series, nullable, **SetNull** | **(V3 §13.2, Phase 12)** LINEAGE — the event belongs to at most one series (§3.4 rule 5) |
| edition_ordinal | int, nullable | **(Phase 12)** EDITIONS member ordinal (e.g. year) |
| edition_label | text, nullable | **(Phase 12)** EDITIONS member label (e.g. "2026 Edition") |
| listed_standalone | bool, default **true** | **(V3 §3.4 rule 2, Phase 12)** structural discovery — roots/editions discoverable by default, a sub-event defaults false and opts in via update; filters the public discovery feeds only (direct access unaffected). Existing rows backfilled to true |
| created_by | uuid FK → users | **The event's OWNER** (D-268). Every event authorization checks this first and it stands alone — a creator needs no membership anywhere, which is what makes a self-represented event manageable. Resolved through `IEventAuthority` (D-269), never re-implemented per service |
| title | text | |
| slug | text UNIQUE | URL path |
| subtitle | text | |
| description | text | |
| language | text | default `en` |
| audience_level_id | uuid FK → event_categories | |
| category_id | uuid FK → event_categories | |
| type_id | uuid FK → event_categories | |
| kind_slug | text | V3 Kind (Phase 1), derived from type/category via `kind_aliases`; nullable, additive |
| settlement_currency | text(3) | V3 §9.1 (Phase 3), ISO-4217, default INR; bound from the org, immutable once money moves |
| template_id | uuid FK → event_templates | |
| venue_id | uuid FK → venues | null = ad-hoc venue |
| venue_name | text | ad-hoc or denormalized |
| venue_address | text | |
| city | text | denormalized for search |
| country | text | ISO country code (default `IN`); required; enables geo-filter |
| state | text | state/province for GST jurisdiction |
| district | text | district for fine-grained location filtering |
| postal_code | text | pin code |
| lat / lng | double | coordinates |
| starts_at | timestamp | |
| ends_at | timestamp | |
| timezone | text | default `Asia/Kolkata` |
| capacity | int | total seats (null = unlimited) |
| visibility | text | `Listed`, `Unlisted`, `Private`, `InviteOnly`. D-266 renamed `Public`→`Listed`; `Private` is deprecated and retires into `InviteOnly` in M3, when the public-exposure guards move onto `product` |
| archetype_slug | text | D-266 — → `event_archetypes.slug` (by slug, no FK). Snapshot at creation; re-derived on a type change |
| product | text NOT NULL | D-266 — `Public` / `Private`, default `Public`. Snapshot at creation and **not** re-derived on a type change: that would let an edit flip a Public event to Private |
| status | text | `Draft`, `Published`, `Closed`, `Archived`, `Cancelled`, the **(D-266 M4)** review states `PendingReview`/`UnderReview`/`ChangesRequested`/`Approved`/`Rejected`, and **(V3 §14.1, Phase 14)** `Scheduled`/`Live`/`Completed`. The pre-M4 `InReview` was retired and backfilled to `PendingReview` by `MigrateInReviewToPendingReview`. `Published` remains the **authoritative registration-open state** (money path + discovery gate on it, unchanged); the Phase-14 states are additive lifecycle markers around it (Draft→review→**Scheduled**→**Published**[reg-open]→**Live**→**Completed**). (**D-101** — Cancelled is terminal; distinct from Closed.) |
| review_claimed_by | uuid, nullable, **indexed** | **(D-266 M4)** the reviewer currently holding this item. Set by `claim_review`, cleared by release **and by any decision**. Null whenever the event is not `UnderReview`. **No FK to users** — a transient hold must not cascade into an event row when an account is deleted. One reviewer at a time: anyone else is refused with `claimed_by_another_reviewer` (an admin may override, so an offline reviewer cannot strand an item) |
| review_claimed_at | timestamp, nullable | **(D-266 M4)** when it was claimed — what an operator needs to spot a stale hold. Written in the same statement as `status`, so an item is never `UnderReview` unowned |
| refund_window_ends_at | timestamp, nullable | **(V3 §14.5, Phase 14)** a material change (date/venue/mode) after any Registration exists opens a refund window until this deadline (7 days or event start, whichever sooner); refunds within it are registrant-initiated via the existing RefundService |
| contact_email | text | |
| contact_phone | text | |
| website | text | |
| social_links_json | jsonb | |
| banner_key | text | primary banner image |
| is_paid | bool | has paid ticket types |
| certificates_enabled | bool | |
| certificate_template_id | uuid | FK → design_templates |
| invite_template_id | uuid | FK → design_templates |
| is_featured | bool | platform homepage placement |
| transfers_enabled | bool | whether ticket transfers are allowed for this event (default true) |
| view_count | int | engagement signal — **derived** from `event_views` by the daily aggregation job (D-130), never incremented on the read path. Still the sort key for `trending`/`popular`; real ranking replaces it in Phase 16 |
| created_at | timestamp | |
| published_at | timestamp | set on first publish |
| updated_at | timestamp | |
| deleted_at | timestamp | soft-delete marker; null = active |

---

### `approval_chains`, `approval_steps`, `approval_requests`, `approval_step_decisions`
The V3 §14.3 internal approval chains (**Phase 14**). **Additive** — the paid-event platform-review gate (§14.4) is
preserved separately; a chain gates publishing only when the event's OrgUnit tree declares one (unchained events are
unaffected). Order: internal chain → platform review → published.

**`approval_chains`** — `id`, `org_unit_id` FK CASCADE → `org_units` (the chain is **inherited down the tree** — an
event resolves the chain on its owning unit or the nearest ancestor, via the Phase-4 materialised path), `name`, `mode`
(`Sequential`/`Parallel`, text), timestamps, `deleted_at` (soft-delete).

**`approval_steps`** — one step of a chain. `id`, `chain_id` FK CASCADE, `sort`, `approver_role` (`OrgRole`, nullable —
approve as a role) OR `approver_user_id` (nullable — a named user), `condition`
(`Always`/`IfPaid`/`IfExternal`/`IfBudgetGt`/`IfMinors`, text — the step applies only when it matches the event),
`condition_param_paise` (IfBudgetGt threshold), `sla_hours` / `escalation_after_hours` (metadata — the auto-escalation
timer is a later phase; no schema change needed to add it).

**`approval_requests`** — the per-event approval run, created when an organiser first attempts to schedule/publish under
a chain. `id`, `event_id` FK CASCADE, `chain_id` FK RESTRICT, `state` (`Pending`/`Approved`/`Rejected`), `created_at`,
`decided_at`. **Unique** `(event_id, chain_id)`. The request is `Approved` once every applicable step is Approved or
Bypassed — which the publish gate requires.

**`approval_step_decisions`** — one applicable step's decision within a request. `id`, `request_id` FK CASCADE,
`step_id` FK CASCADE, `sort` (copied from the step, for ordered SEQUENTIAL evaluation), `state`
(`Pending`/`Approved`/`Rejected`/`Bypassed`), `decided_by`, `decided_at`, `note`. **Unique** `(request_id, step_id)`.
Bypass is a first-class state (org-Owner/admin only) and always written to the audit spine.

---

### `tags`
Freeform keyword tags. Unique by name and slug.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| name | text UNIQUE | |
| slug | text UNIQUE | |

---

### `event_tags`
Many-to-many join between events and tags.

| Column | Type | Notes |
|--------|------|-------|
| event_id | uuid PK FK → events | composite PK |
| tag_id | uuid PK FK → tags | composite PK |

---

### `venues`
Reusable venue profiles owned by an org.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations | |
| name | text | |
| address | text | |
| city | text | |
| lat / lng | double | |
| google_maps_url | text | |
| capacity | int | |
| has_parking | bool | |
| is_accessible | bool | wheelchair accessible |
| notes | text | internal notes |
| created_at | timestamp | |
| deleted_at | timestamp | soft-delete marker; null = active |

---

### `venue_images`
Photo gallery for a venue.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| venue_id | uuid FK → venues (cascade delete) | |
| key | text | storage object key |
| sort | int | display order |

---

### `event_templates`
Reusable event blueprints (system-seeded or org-custom).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid | null = system template available to all |
| name | text | |
| slug | text UNIQUE | |
| description | text | |
| default_sections_json | jsonb | which optional sections to pre-enable (UI hint only) |
| is_system | bool | |
| created_at | timestamp | |

---

## Event Content

### `speakers`
Speaker profiles — created per org, reused across events.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations | |
| name | text | |
| bio | text | |
| photo_key | text | storage object key |
| company | text | |
| role | text | job title |
| social_links_json | jsonb | |
| created_at | timestamp | |
| deleted_at | timestamp | soft-delete marker; null = active |

---

### `event_speakers`
Assigns a speaker to an event (independent of sessions).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| speaker_id | uuid FK → speakers (cascade) | unique per event+speaker |
| sort | int | display order |

---

### `event_sessions`
**AgendaItems** (V3 §3.1 CONTAINMENT, Phase 12) — talks or breaks within an event's schedule. An AgendaItem has no
Pass/Registration/Credential (attendance derives from the parent Credential + a scan), but it **may hold an
InventoryPool** for a seat limit (§3.4 rule 3). Historical type/table name `EventSession`/`event_sessions` retained for
backward compatibility.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| title | text | |
| description | text | |
| kind | text | `Session` or `Break` |
| starts_at | timestamp | |
| ends_at | timestamp | |
| sort | int | display order |
| inventory_pool_id | uuid FK → inventory_pools, nullable, **Restrict** | **(V3 §3.4 rule 3, Phase 12)** optional seat-limit pool; must belong to the same event; config-only (seat enforcement at scan is a later phase) |

---

### `event_series`, `event_series_followers`
The V3 §13.2 **EventSeries** (Phase 12) — LINEAGE, one entity with two modes. Additive over the event model; **series
never nest** and an event belongs to ≤1 series (§3.4 rule 5). Followers and brand assets live on the series and carry
across members.

**`event_series`** — `id`, `org_id` FK CASCADE, `name`, `slug` (**unique per org** `(org_id, slug)` — canonical series
URL), `mode` (`Recurring`/`Editions`, text), `description`, `banner_key`, `brand_assets_json` jsonb, `rrule`
(RECURRING only — RFC-5545, validated for a known `FREQ`), `exception_dates_json` jsonb (RECURRING skipped
occurrences), `created_at`/`updated_at`/`deleted_at` (soft-delete). RECURRING occurrences and EDITIONS members are
their own `events` rows linked by `events.series_id`; **per-occurrence timezone reuses `events.timezone`**. Occurrence
auto-expansion from the rrule is organiser-driven this phase.

**`event_series_followers`** — `id`, `series_id` FK CASCADE, `user_id` FK CASCADE, `created_at`. **Unique**
`(series_id, user_id)` — one follow per user per series (mirrors `organization_followers`).

---

### `event_session_speakers`
Many-to-many: which speakers present in which session.

| Column | Type | Notes |
|--------|------|-------|
| session_id | uuid PK FK → event_sessions (cascade) | composite PK |
| speaker_id | uuid PK FK → speakers (cascade) | composite PK |

---

### `sponsors`
Sponsor profiles — created per org, reused across events.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations | |
| name | text | |
| logo_key | text | storage object key |
| website | text | |
| tier | text | `Platinum`, `Gold`, `Silver`, `Bronze`, `Partner` |
| priority | int | display order within tier |
| created_at | timestamp | |
| deleted_at | timestamp | soft-delete marker; null = active |

---

### `event_sponsors`
Assigns a sponsor to an event.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| sponsor_id | uuid FK → sponsors (cascade) | unique per event+sponsor |
| sort | int | display order |

---

### `event_media`
Gallery images, documents, poster, brochure, rules PDF for an event.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| kind | text | `Banner`, `Gallery`, `Document`, `Poster`, `Brochure`, `RulesPdf` |
| key | text | storage object key |
| caption | text | |
| sort | int | display order |
| created_at | timestamp | |

---

### `saved_events`
User bookmarks an event for later. Composite PK, no extra columns.

| Column | Type | Notes |
|--------|------|-------|
| user_id | uuid PK FK → users | composite PK |
| event_id | uuid PK FK → events (cascade) | composite PK |
| created_at | timestamp | |

---

### `event_assignments`
Flexible staff / volunteer / judge role assignments for an event. Role stored as freeform text; accepted assignments appear on the user's public profile.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| org_id | uuid FK → organizations (cascade) | |
| user_id | uuid FK → users (cascade) | assignee |
| invited_by | uuid FK → users | org member who created the assignment |
| role | text | `Volunteer`, `Judge`, `Moderator`, `Registration Desk`, `Stage Manager`, `Security`, `Photographer`, `Videographer`, `Host`, `Media Team`, `Speaker Coordinator`, `Technical Team`, `Support Team`, `Custom` |
| custom_role | text | display name when role = `Custom` |
| status | text | `Invited`, `Accepted`, `Declined`, `Withdrawn` |
| show_on_profile | bool | whether the accepted assignment is visible on the user's profile |
| notes | text | internal notes for the org |
| accepted_at | timestamp | |
| completed_at | timestamp | |
| created_at | timestamp | |
| updated_at | timestamp | |

**Note (V3 §5, Phase 6):** this table is the V2 legacy shadow. The V3 participant model (`participant_roles`
+ `event_participants` below) is backfilled from it at startup; both coexist during the strangler window.

---

### `participant_roles`
The platform participant-role registry (**V3 §5.3**, Phase 6): one model replacing V2's EventAssignment
free-text roles, capability people-lists, and org RBAC. `class` is one of 7 hardcoded values (capacity/permission
logic branch on it); `slug` is the platform registry (org-extensible later). Seeded idempotently at startup.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| slug | text | `judge`, `volunteer`, `attendee`, … (unique among platform roles) |
| name | text | |
| class | text | `Organiser` \| `Operations` \| `Content` \| `Evaluation` \| `Participant` \| `Commercial` \| `Observer` |
| default_permissions | jsonb, nullable | event-scoped grant keys (§5.4), e.g. `["participants:manage"]` |
| default_access_zones | jsonb, nullable | credential access zones (§5.2), mostly consumed by later phases |
| is_public | bool | whether the role is publicly listed |
| counts_toward_capacity | bool | §5.2 — Competitors/Attendees do; Volunteers/Media don't |
| inventory_segment | text, nullable | `general` \| `vip` \| null (accounting wiring is Phase 7) |
| org_id | uuid, nullable | null = platform role; a value = org-extended (write path is a later phase) |
| sort | int | |

Indexes: **partial unique** `ix_participant_roles_platform_slug` on `slug` where `org_id IS NULL`; `org_id`.

---

### `event_participants`
A person/org-unit participating in an event in some capacity (**V3 §5.1**, Phase 6). PARTICIPATION only —
permission derives from the role (§5.4), credential is separate (§5.2). Team subjects are stored-for-Phase-10.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (CASCADE) | |
| subject_type | text | `Person` \| `Team` \| `OrgUnit` (Team is Phase 10) |
| subject_id | uuid | user id / team id / org_unit id per `subject_type` |
| role_slug | text | → `participant_roles.slug` |
| custom_label | text, nullable | free text (§5.3); never a permission/capacity input |
| state | text | `Invited` \| `Accepted` \| `Declined` \| `Active` \| `Removed` \| `Completed` (the accept path goes Invited→Active; `Accepted`/`Completed` are reserved) |
| scope | jsonb, nullable | **stored for a later phase** — `{ whole_event, sub_events[], stages[], agenda_items[] }`; null ⇒ whole event. Phase 6 grants match the exact event (never org-level, never up); sub-event/stage/agenda propagation lands with those phases (11/12) |
| visibility | text | `Public` \| `Internal` — **stored**; a public participant-listing read is a later surface |
| invited_by | uuid, nullable | |
| accepted_at / completed_at | timestamp, nullable | |
| created_at / updated_at | timestamp | |

Indexes: `event_id`; `(event_id, subject_type, subject_id)`; **unique** `ix_event_participants_unique` on
`(event_id, subject_type, subject_id, role_slug)` (idempotent assign; a removed row is reactivated, not duplicated).

---

### `event_checkin_devices`
Scanner devices registered to scan tickets at an event. `DeviceTokenHash` is a hashed secret — never plaintext.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| org_id | uuid FK → organizations (cascade) | |
| registered_by | uuid FK → users | org member who registered the device |
| device_name | text | human-readable label |
| device_token_hash | text | hashed device auth token (never plaintext) |
| is_active | bool | |
| last_seen_at | timestamp | updated on each scan request |
| created_at | timestamp | |

---

### `event_reviews`
Post-event attendee reviews. Soft-deletable. Requires a `ticket_id` unless anonymous.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| user_id | uuid FK → users (cascade) | |
| ticket_id | uuid FK → tickets (set null) | links review to a verified attendee |
| rating | int | 1–5 |
| title | text | optional short headline |
| body | text | optional long text |
| is_anonymous | bool | hides user identity in public display |
| is_verified | bool | true when ticket_id confirmed attendance |
| status | text | `Visible`, `Hidden`, `Flagged` |
| deleted_at | timestamp | soft-delete marker; null = active |
| created_at | timestamp | |

---

## Ticketing & Registration

### `ticket_types`
Defines one category of ticket for an event (e.g. General, VIP, Group Pack).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events | |
| name | text | e.g. "Early Bird", "VIP" |
| price_paise | long | 0 = free ticket |
| pricing_unit | text | `PerTicket` or `PerGroup` |
| registration_mode | text | `Individual` or `Group` |
| group_min | int | min group size (Group mode only) |
| group_max | int | max group size (Group mode only) |
| quantity | int | total seats available |
| sold | int | sold counter (incremented atomically on order confirm) |
| sale_starts | timestamp | public sale window open |
| sale_ends | timestamp | public sale window close |
| per_user_limit | int | max tickets one user can buy (default 5) |
| is_all_access | bool | festival pass valid at every child event gate |
| deleted_at | timestamp | soft-delete marker; null = active |

---

### `form_fields`
Custom registration form fields attached to a ticket type.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| ticket_type_id | uuid FK → ticket_types (cascade) | |
| key | text | snake_case field identifier; unique per ticket type |
| label | text | display label shown to buyer |
| type | text | `Text`, `Number`, `Select`, `Checkbox`, `Date`, `File` |
| scope | text | `PerRegistration` (once per order) or `PerParticipant` (once per attendee) |
| required | bool | |
| options_json | jsonb | for Select fields: `["S","M","L"]` |
| sort | int | display order |

---

### `field_presets`
Saved form-field bundles per event category/type slug (e.g. "Hackathon defaults").

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| category_or_type_slug | text | maps to an event_categories slug |
| name | text | preset display name |
| payload_json | jsonb | `{registration_mode, group_min, group_max, pricing_unit, fields:[...]}` |

---

### `seat_holds`
Short-lived inventory locks during checkout (10-min TTL). Expired holds are released by the scheduler.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| ticket_type_id | uuid FK → ticket_types | |
| order_id | uuid FK → orders | links to the in-progress order |
| qty | int | number of seats held |
| status | text | `Active`, `Confirmed`, `Expired`, `Released` |
| expires_at | timestamp | created_at + 10 min |
| created_at | timestamp | |

---

### `ticket_waitlist`
Per-ticket-type waitlist. Users join when a ticket type is sold out. Scheduler transitions `Notified → Expired` when `offer_expires_at` passes.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| ticket_type_id | uuid FK → ticket_types (cascade) | |
| pool_id | uuid FK → inventory_pools, nullable | **(V3 §8.5, Phase 7)** the pool this queue attaches to — the ticket type's general pool (a waitlist is per-pool, not per-event) |
| user_id | uuid FK → users (cascade) | |
| position | int | queue position (lower = earlier) |
| status | text | `Waiting`, `Notified`, `Expired`, `Purchased`, `Withdrawn` |
| notified_at | timestamp | when the offer notification was sent |
| offer_expires_at | timestamp | deadline to purchase after notification |
| created_at | timestamp | |

---

### `inventory_pools`
Capacity as a segmented inventory, not a scalar (**V3 §8**). Exactly one General/InPerson/PersonSlot/Event pool
per TicketType, `total` = TicketType.Quantity. **Authoritative for oversell from the Phase 9 cut-over (§17.1):**
`consumed`/`held` are owned by the **conditional decrement** (`consumed = consumed + n WHERE
consumed + held + n <= total + oversell_allowance`) under the pool row lock — never a mirror of `TicketType.Sold`
(which stays a written legacy field). A DB CHECK (`ck_inventory_pools_nonneg`) forbids a negative counter; the
upper bound is the conditional decrement's WHERE. Reconciliation proves `consumed == count(active admissions)`.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (CASCADE) | |
| ticket_type_id | uuid FK → ticket_types (CASCADE), nullable | the ticket type this general pool backs; null for future event/venue-scoped pools |
| scope | text | `Event` (Phase 7) \| `SubEvent` \| `Stage` \| `AgendaItem` \| `Venue` \| `Zone` |
| segment | text | `General` (Phase 7) \| `Vip` \| `QuotaOrgUnit` \| `QuotaRole` \| `Guest` \| `Accessible` \| `Press` \| `Staff` \| `WalkIn` |
| channel | text | `InPerson` (Phase 7) \| `Virtual` |
| unit | text | `PersonSlot` (Phase 7) \| `TeamSlot` |
| total, held, allocated, consumed | int | §8.2 lifecycle counters, **authoritative from Phase 9**. A free order **consumes** immediately; a paid order **holds** (Held+1), and capture converts held→consumed, expiry/refund releases. Reconcile `consumed` against **active admissions**, not `Sold`. CHECK `ck_inventory_pools_nonneg` keeps all counters ≥ 0 |
| oversell_allowance | int | §8.1 default 0 — explicit, never accidental |
| release_policy | jsonb, nullable | §8.1 `{ at, to }` — **stored config only**; the release automation is **deferred** (not implemented in Phase 9; a later phase) |
| no_show_policy | text | `None` (default) \| `ReleaseAfter` \| `Deposit` \| `Strike` — **stored config only**; the no-show automation is **deferred** (not implemented in Phase 9) |
| no_show_release_minutes | int | for `ReleaseAfter` |
| waitlist_config | jsonb, nullable | §8.5 `{ enabled, ordering, offer_ttl, auto_promote }` — **stored** |
| created_at / updated_at | timestamp | |

Indexes: `event_id`; **partial unique** `ix_inventory_pools_ticket_type_segment` on `(ticket_type_id, segment)`
where `ticket_type_id IS NOT NULL` (one general pool per ticket type; future non-general pools coexist).
Backfilled at startup: one general pool per existing ticket type, plus re-pointing existing waitlist rows.

---

### `registration_policies`, `registrations`, `admissions`, `credentials`
The registration → admission → credential chain (**V3 §7**), **authoritative from the Phase 9 cut-over**. On the
live money path each order produces the chain **in the money transaction** (§6.1), atomically with the conditional
pool decrement, so the admission never lags the seat. Out-of-band reconciliation/backfill re-project idempotently on
their own isolated DbContext (a fresh scope). **Order/Ticket are retained as written legacy mirrors** (still written,
no longer authoritative; removal is a later phase). Subject is Person (a party booking is one Registration with N
Admissions, §6.1). Backfilled from existing orders at startup; the daily reconciliation job **self-heals** drift by
re-projecting the affected orders (not just alerting).

**`registration_policies`** — the five axes per ticket type (the Pass analog). `id`, `event_id` FK→events
CASCADE, `ticket_type_id` FK→ticket_types CASCADE **unique**, `subject` (`Person`/`Team`/`OrgUnit`/`ExternalOrg`),
`gates` jsonb (`Open`/`Approval`/`Invite`/`Lottery`/`Prerequisite`/`Referral` — composable; only the derived
`Open` behaviour is enforced this phase), `identity_requirement` (`None`/`Contact`/`Account`/`Verified`),
`form_id`/`documents_required` jsonb/`application_id` (intake), `allocation` (`Fcfs`/`Lottery`/`Quota`/`Ranked`/
`Assigned`), `payment` (`Free`/`Self`/`Delegated`/`Sponsored`/`Deferred`), window fields (`opens_at`/`closes_at`
mirror the sale window; `late_window_minutes`/`edit_until`/`cancel_until`), timestamps. Gate/allocation values
needing later subsystems are **stored-but-not-enforced** until Phase 9.

**`registrations`** — the ACT, one per order. `id`, `event_id` FK CASCADE, `ticket_type_id`, `subject_type`,
`subject_id` (user id; null for a guest), `order_id` FK→orders CASCADE **unique**, `state`
(`Pending`/`Confirmed`/`Cancelled`, mirrors order status), `answers` jsonb, timestamps.

**`admissions`** — the RIGHT, one per ticket. `id`, `registration_id` FK CASCADE, `event_id`, `person_id`
(ticket holder; null for guest), `ticket_id` FK→tickets CASCADE **unique**, `pool_id` FK→inventory_pools (the
Phase-7 pool consumed), `credential_id` FK→credentials, `state` (`Active`/`CheckedIn`/`Void`, mirrors ticket
state), timestamps.

**`credentials`** — the ARTIFACT, **one per person per event tree** (V3 §9.3: "Never one QR per sub-event").
`id`, `event_id` FK CASCADE — this holds the **event-tree ROOT** (`Event.ParentEventId ?? Id`), so a person
registering for a fest and its sub-events shares a single credential — `person_id` (null for a guest,
per-admission credential), `code` (mirrors the scannable ticket code), `state` (`Active`/`Revoked`), timestamps.
**Partial-unique** `ix_credentials_person_event` on `(event_id, person_id)` where `person_id IS NOT NULL` —
because `event_id` is the tree root, this enforces one credential per account holder per event **tree**; guests
get one per admission. A credential is `Active` iff at least one non-void admission still references it (set-based
revocation pass), so a full refund revokes it and a partial refund keeps it. Under concurrency the per-person
credential creation is serialised by a Postgres transaction-scoped **advisory lock** on `(tree root, person)`, so
two concurrent same-person orders both commit and share the one credential (§9.3).

**Also written by the authoritative money path (Phase 9):** each order is created inside an explicit transaction
that (1) conditionally decrements the pool, (2) writes Order/OrderItem/Ticket (legacy), (3) projects the
registration/admission/credential, (4) writes the VAR, and (5) enqueues the chat-join outbox message — all
committed atomically. `orders.idempotency_key` is **scoped per caller** — two partial-unique indexes,
`(event_id, user_id, idempotency_key)` for authenticated callers and `(event_id, guest_phone, idempotency_key)` for
guests — so a retried create returns that caller's own original order and never another caller's (nor its guest
access token). A third partial-unique index **`ix_orders_walkin_idempotency`** `(event_id, idempotency_key)` **WHERE
`user_id IS NULL AND guest_phone IS NULL AND idempotency_key IS NOT NULL`** (migration `WalkInIdempotencyIndex`, Phase
13) closes the one gap the guest index leaves: a **NONE-identity walk-in** has both `user_id` and `guest_phone` null,
and Postgres treats those NULLs as distinct, so this index DB-enforces one-per-`(event, key)` for that case — a
concurrent offline replay then collides at the database, the loser's transaction (and its inventory consume) rolls
back, and the caller receives the original. It touches no other order (a normal guest order always has a `guest_phone`,
an account order a `user_id`). `refunds.order_id` is **non-unique**: full-refund idempotency is the atomic Paid→Refunded status
claim, and keeping it non-unique leaves V3 §9.6 partial refunds (multiple per-VAR-line refund rows) open.
`seat_holds.pool_id` ties a hold to the pool it reserves.

---

### `seat_blocks`, `seat_block_seats`
The V3 §7.5 delegated registration (**Phase 13**). **Additive** — the money path and the Registration→Admission→
Credential chain are untouched; a block is funded through the **authoritative Order/Ticket path** and mints its seats
via the existing projection. Payment is data/authz only (§9.7): FREE | DEFERRED, no live collection this phase.

**`seat_blocks`** — an org unit's reserved seat allocation. `id`, `event_id` FK CASCADE, `ticket_type_id` FK CASCADE
(the Pass analog), `registrant_org_unit_id` FK RESTRICT → `org_units` (who registers — §7.5), `payer_id` FK RESTRICT →
`users` nullable (may differ; org is invoiced, §9.7), `delegate_user_id` FK RESTRICT → `users` (the delegate-console
persona), `payment_mode` (`Free`/`Deferred`, text), `quantity`, `assignment_deadline` nullable, `reassign_limit`
(CHECK `quantity >= 1 AND reassign_limit >= 0`), `order_id` FK RESTRICT → `orders` **unique** (the authoritative funding
order that minted the seats), `state` (`Open`/`Closed`/`Cancelled`), `created_by`, timestamps.

**`seat_block_seats`** — one seat governing one (initially unassigned) admission. `id`, `seat_block_id` FK CASCADE,
`admission_id` FK CASCADE **unique**, `reassign_count`, `reassignable_until` nullable, `assigned_at` nullable
(null = UNASSIGNED; the assigned person is the admission's `person_id`), timestamps. Assignment sets the seat's ticket
holder + the admission's person and re-runs the projection to bind the one-per-tree credential; reassignment is
governed by `reassignable_until` + `reassign_count` vs the block's `reassign_limit`, every (re)assignment audited.
**Walk-in (§7.6) adds no table** — it produces a normal Registration/Admission/Credential drawing on the
`WalkIn`-segment InventoryPool, identifiable by the admission's pool segment.

### `passes`, `admission_rights`, `var_lines`
The V3 §9 commercial + revenue layer (**Phase 9 authority cut-over**, Option A). Additive; the legacy
`ticket_types`/`orders` stay as written mirrors.

**`passes`** — the commercial product (§9.2), one **per ticket type** (1:1 this phase). `id`, `event_id` FK
CASCADE, `ticket_type_id` FK CASCADE **unique**, `name`, `price_paise` (CHECK ≥ 0), `currency`, `quantity`,
`sale_starts`/`sale_ends`, `per_subject_limit`, `visibility` (`Public`/`Unlisted`/`CodeOnly`/`InviteOnly`),
timestamps. Commercial fields mirror the ticket type; synced on create/update and backfilled.

**`admission_rights`** — what a Pass grants (§9.2). Phase 9 Option A creates exactly one **SINGLE-scope, InPerson,
1-use** right per Pass. `id`, `pass_id` FK CASCADE, `scope` (`Single` — `Subtree`/`Set`/`Query` are the reserved
vocabulary, never created this phase), `event_id` FK CASCADE (the single admitted event), `channel`
(`InPerson` — `Virtual`/`Either` reserved), `uses` (CHECK ≥ 1), `created_at`.

**`var_lines`** — the Value Allocation Record (§9.5): the **immutable** snapshot of how an order line's price is
allocated across events, written **at purchase** and **never recomputed** — the sole basis for revenue and refunds.
Single-scope (Option A) ⇒ one line per order item = the full line value on its one event. `id`, `order_id` FK
CASCADE, `order_item_id` FK CASCADE, `event_id` FK, `allocated_paise` (CHECK ≥ 0), `currency`, `basis`
(`list_price` this phase; `allocation_weight`/`equal_share` are later-wave multi-scope), `computed_at`.

---

### `teams`, `team_memberships`, `team_invites`, `team_join_requests`, `team_policies`
The V3 §6 Team subsystem (**Phase 10**) — the ONLY group entity, and only where competition exists. **Additive**:
the purchase `groups`/`group_members` stay as legacy compatibility mirrors and the Phase-9 money path is untouched.
**Formation only** — team registration as a purchase subject (a `team_slot`, §6.5) is a later phase.

**`teams`** — the competition group (survives the event; a result record). `id`, `event_id` FK CASCADE,
`ticket_type_id` FK CASCADE (the competition type it forms under; its TeamPolicy lives there), `name`, `slug`
(**unique per event**), `logo_url`, `tagline`, `declared_org_unit_id` FK (§4.4 eligibility), `state`
(`Forming`→`Complete`→`Locked`→`Competing`→`{Eliminated|Disqualified|Withdrawn|Finalist}`; every transition audited,
disqualification requires reason+actor), `registration_id` (the team's own Registration when it registers as a unit
— null this phase, the purchase flow is deferred), `merged_into_team_id` (tombstone pointer after merge/split, §6.4),
timestamps.

**`team_memberships`** — a person's place on a team. `id`, `team_id` FK CASCADE, `person_id` FK (null until an
email/phone invite is claimed), `role` (`Captain`/`CoCaptain`/`Member`/`Substitute`/`Mentor`), `state`
(`Invited`/`Requested`/`Active`/`Replaced`/`Removed`/`Left`), `replaced_by_membership_id` (**substitution is an edge,
not a delete** — §6.2/§6.5), `joined_at`/`left_at`. **Partial-unique** `ix_team_memberships_active_person` on
`(team_id, person_id)` where the state is live (`Invited`/`Requested`/`Active`) — one live membership per person per
team; historical rows are retained.

**`team_invites`** — an outstanding invitation. `id`, `team_id` FK CASCADE, `invitee_person_id`/`invitee_email`/
`invitee_phone`, `token` (**unique** bearer), `role`, `state` (`Pending`/`Accepted`/`Declined`/`Revoked`/`Expired`),
`expires_at`, `created_at`.

**`team_join_requests`** — a person's request to join. `id`, `team_id` FK CASCADE, `person_id` FK CASCADE, `message`,
`state` (`Pending`/`Approved`/`Rejected`), `decided_by`, `decided_at`, `created_at`. **Partial-unique**
`ix_team_join_requests_pending` on `(team_id, person_id)` where `state='Pending'`.

**`team_policies`** — the `teams` capability config (§6.3), **one per competition ticket type** (`ticket_type_id`
**unique**, FK CASCADE; `event_id` FK CASCADE). `min_size`/`max_size` (CHECK `min>=1 AND max>=min`), `formation_mode`
(`Open`/`InviteOnly`/`OrganiserAssigned`/`RandomAllocation`), `join_approval` (`None`/`Captain`/`Organiser`),
`lock_at`, edit windows (`name`/`roster`/`mentor`), `max_teams_per_person_in_event`, `unlimited_teams_per_tree`,
`allow_solo_as_team`, `allow_cross_org_members`, `substitutes_allowed`/`substitution_deadline`,
`incomplete_team_policy` (`BlockAtLock`/`AutoMerge`/`AllowUndersized`/`Waitlist`), `waitlist_config` jsonb, timestamps.
Derived on competition-ticket-type create (min/max from its group bounds); organiser-owned thereafter.

### `stages`, `stage_participants`, `fixtures`, `fixture_participants`, `fixture_officials`, `scoring_policies`, `judge_scores`, `public_votes`, `stage_results`, `result_corrections`
The V3 §10 competition engine (**Phase 11**). **Additive** — the money path, InventoryPool authority,
Registration→Admission→Credential, Pass, VAR and §17.1 concurrency are untouched. Enums stored as text; JSON columns
`jsonb`. Spectator admission (§10.4) is **config-only** this phase.

**`stages`** — a competition round (§10.1), **not registerable** (competitors arrive by advancement, spectators by
admission). `id`, `event_id` FK CASCADE, `sequence` (**unique per event** `(event_id, sequence)`), `name`, `format`
(`SingleSubmission`/`JuryReview`/`Knockout`/`DoubleElim`/`RoundRobin`/`Swiss`/`League`/`TimeTrial`/`PublicVote`),
`participant_source` (`AllRegistered`/`AdvancedFrom`/`Seeded`/`Wildcard`), `advanced_from_stage_id` self-FK RESTRICT,
`advancement_rule` (`TopN`/`TopPercent`/`ScoreGte`/`Manual`) + `advancement_threshold`, `scoring_policy_id` FK,
`starts_at`/`ends_at`, `venue_id` FK, `mode` (`InPerson`/`Virtual` — a Final may differ from Quals), `results_visibility`
(`Live`/`OnStageClose`/`OnEventClose`), `spectator_pool_id` FK → `inventory_pools` (**§10.4 config-only link**), `state`
(`Draft`→`Live`→`Closed`), `closed_at`, timestamps.

**`stage_participants`** — the stage roster (§10.1). `id`, `stage_id` FK CASCADE, `subject_type` (`Person`/`Team`),
`subject_id`, `seed`, `advanced` (placed by advancement vs seeded/manual), `created_at`. **Unique** `(stage_id,
subject_type, subject_id)`. Populated by advancement, organiser add, or seed-from-registered (competition teams).

**`fixtures`** — a scheduled contest within a stage (§10.2). `id`, `stage_id` FK CASCADE, `round_no`, `label`,
`venue_id` FK, `slot_start`/`slot_end`, `state` (`Scheduled`/`Live`/`Complete`/`Walkover`/`Abandoned`/`Disputed` —
`Walkover`/`Abandoned` first-class so results are never falsified), `result_json` jsonb, timestamps. Manual scheduling;
an overlapping slot that double-books a venue/official/participant is **rejected at create** (conflict detection).

**`fixture_participants`** — a subject in a fixture. `id`, `fixture_id` FK CASCADE, `subject_type`, `subject_id`,
`seed`. **Unique** `(fixture_id, subject_type, subject_id)`.

**`fixture_officials`** — a judge/referee on a fixture. `id`, `fixture_id` FK CASCADE, `participant_id` FK CASCADE →
`event_participants`. **Unique** `(fixture_id, participant_id)`.

**`scoring_policies`** — a stage's scoring config (§10.3). `id`, `event_id` FK CASCADE, `name`, `sources_json` jsonb
(`[{type,weight,rubricId?}]` — `Judge`/`PublicVote`/`Automated`), `aggregation`
(`Sum`/`WeightedMean`/`TrimmedMean`/`Median`/`RankAggregation`), `normalisation` (`None`/`PerJudgeZscore`),
`tie_break_json` jsonb (ordered keys), `conflict_rules_json` jsonb (§5.5), and PublicVoteRules — `vote_identity_binding`
(`Account`/`VerifiedContact`), `vote_rate_limit_per_hour`, `vote_weight_cap_percent` (CHECK 0–100, caps the public-vote
share of the final score), timestamps.

**`judge_scores`** — a judge's score for a subject (§10.3). `id`, `stage_id` FK CASCADE, `fixture_id` FK,
`judge_participant_id` **FK RESTRICT** → `event_participants`, `subject_type`/`subject_id`, `score` (numeric),
`breakdown_json` jsonb, timestamps. **Unique `ix_judge_scores_unique` `(stage_id, judge_participant_id, subject_type,
subject_id)`** — one live score per judge/subject; a re-submission updates it (duplicate prevention). The judge FK is
**RESTRICT** (migration `RestrictScoreVoteFks`): a score is immutable evidence (§10.5), so removing the judge
participant cannot cascade-erase it — participants are soft-removed (`ParticipantState.Removed`); deleting the whole
stage still cascades.

**`public_votes`** — a public vote (§10.3), **immutable** (append-only tally). `id`, `stage_id` FK CASCADE,
`voter_user_id` **FK RESTRICT** → `users`, `subject_type`/`subject_id`, `created_at`. **Unique
`ix_public_votes_one_per_voter` `(stage_id, voter_user_id)`** — the enforceable one-vote-per-identity rule; a second
`(voter_user_id, created_at)` index scans the rate-limit window. IP is never the identity. The user FK is **RESTRICT**
(migration `RestrictScoreVoteFks`): the tally is immutable audit, so deleting a user cannot silently drop their vote;
deleting the whole stage still cascades.

**`stage_results`** — a computed result (§10.5). `id`, `stage_id` FK CASCADE, `subject_type`/`subject_id`, `rank`,
`final_score` (numeric), `score_breakdown_json` jsonb, `state` (`Provisional`/`Published`/`Disputed`/`Corrected`),
`published_at`, timestamps. **Unique** `(stage_id, subject_type, subject_id)`. Immutable once `Published` except via a
recorded correction; certificates/prerequisites read `Published`/`Corrected` only.

**`result_corrections`** — an append-only correction to a published result (§10.5). `id`, `result_id` FK CASCADE,
`corrected_by`, `reason`, `previous_value_json` jsonb (the pre-change snapshot), `created_at`. Never a silent edit.

---

## Registration Forms

**Retired in M11 (D-050).** The event-scoped four-table builder (`registration_forms` / `registration_fields` / `registration_responses` / `registration_response_values`, D-024) was schema-only and never wired. The single registration-form system is the ticket-scoped `form_fields` (D-020), which drives per-registration / per-participant answer capture on orders.

---

## Orders & Payments

### `orders`
One order per checkout session (one ticket type per order).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | buyer |
| event_id | uuid FK → events | |
| ticket_type_id | uuid FK → ticket_types | |
| status | text | `Pending`, `Paid`, `Failed`, `Refunded`, `PartiallyRefunded` |
| amount_paise | long | total charged |
| razorpay_order_id | text | Razorpay order reference |
| answers_json | jsonb | PerRegistration form answers |
| created_at | timestamp | |

---

### `order_items`
Line items within an order (quantity + price snapshot).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| order_id | uuid FK → orders | |
| ticket_type_id | uuid FK → ticket_types | |
| qty | int | number of tickets |
| unit_price_paise | long | price at time of purchase (snapshot) |

---

### `payments`
Payment attempt record linked to Razorpay. Unique per razorpay_payment_id (idempotency).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| order_id | uuid FK → orders | |
| razorpay_payment_id | text UNIQUE | set after Razorpay captures |
| method | text | upi, card, netbanking, etc. |
| status | text | `created`, `captured`, `failed` |
| captured_at | timestamp | |
| webhook_payload_json | jsonb | raw Razorpay webhook body (audit) |
| created_at | timestamp | |

---

### `refunds`
Refund request and tracking record.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| order_id | uuid FK → orders | |
| amount_paise | long | amount to refund |
| reason | text | |
| status | text | `Initiated`, `Processed`, `Failed` |
| razorpay_refund_id | text UNIQUE | set when Razorpay confirms |
| created_at | timestamp | |

---

## Groups & Tickets

### `groups`
A group registration unit — created when a group ticket is purchased.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events | |
| ticket_type_id | uuid FK → ticket_types | |
| order_id | uuid FK → orders | purchase that created this group |
| group_number | int | auto-incrementing per event (1, 2, 3…) |
| display_name | text | optional team/group name |
| join_code | text UNIQUE | 6-char code members use to join |
| leader_user_id | uuid FK → users | person who bought the ticket |
| created_at | timestamp | |

---

### `group_members`
Individual members within a group (slots filled when people join via join code).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| group_id | uuid FK → groups | |
| user_id | uuid | set when the member claims their slot |
| name | text | entered during join |
| phone | text | entered during join |
| ticket_id | uuid | issued after joining |
| answers_json | jsonb | PerParticipant form answers |
| joined_at | timestamp | |

---

### `tickets`
Individual entry ticket — one per seat. Contains the QR payload.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| order_item_id | uuid FK → order_items | |
| event_id | uuid FK → events | |
| user_id | uuid | ticket holder (null until claimed for group slots) |
| group_member_id | uuid | set for group tickets |
| code | uuid UNIQUE | QR payload (scanned at gate) |
| hmac_sig | text | HMAC-SHA256 of code for offline verification |
| state | text | `Issued`, `CheckedIn`, `Void` |
| answers_json | jsonb | consolidated form answers for this ticket |
| checked_in_at | timestamp | |
| checked_in_by | uuid | staff/volunteer who scanned it |
| created_at | timestamp | |

---

### `ticket_transfers`
Peer-to-peer ticket transfer requests — sender initiates, recipient claims within 72 h.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| ticket_id | uuid FK → tickets | |
| from_user_id | uuid FK → users | current holder initiating transfer |
| to_phone | text | recipient's E.164 phone number |
| to_user_id | uuid | set when the recipient claims the transfer |
| transfer_code | text UNIQUE | 8-char code sent to recipient |
| status | text | `Pending`, `Claimed`, `Cancelled`, `Expired` |
| expires_at | timestamp | created_at + 72 hours |
| created_at | timestamp | |
| claimed_at | timestamp | set on successful claim |

---

### `gate_entries`
Scan log — one row per successful check-in scan. Unique per ticket+event.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| ticket_id | uuid FK → tickets | unique per event+ticket |
| event_id | uuid FK → events | |
| scanned_by | uuid FK → users | staff who scanned |
| device_info | text | scanner device identifier |
| created_at | timestamp | scan timestamp |

---

## Money & Payouts

### `transfers`
Razorpay route transfer of collected funds to the org's linked account.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| payment_id | uuid FK → payments | |
| org_id | uuid FK → organizations | |
| razorpay_transfer_id | text UNIQUE | Razorpay transfer reference |
| amount_paise | long | |
| status | text | `OnHold`, `Released`, `Reversed` |
| hold_until | timestamp | earliest date funds can be released |
| created_at | timestamp | |

---

### `ledger_entries`
Append-only financial ledger per org+event. Balance = sum of all entries per state.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations | |
| event_id | uuid FK → events | |
| amount_paise | long | signed (negative = debit) |
| state | text | `Collected`, `Available`, `Advanced`, `Reserved`, `Settled` |
| ref_type | text | `payment`, `refund`, `advance`, `reserve`, `settlement`, `chargeback` |
| ref_id | uuid | ID of the referenced row |
| created_at | timestamp | |

---

### `withdrawals`
Org-initiated withdrawal request from their available ledger balance.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid FK → organizations | |
| amount_paise | long | |
| status | text | `requested`, `processing`, `paid`, `rejected` |
| created_at | timestamp | |

---

### `organization_wallet`
Cached balance summary per org. Updated atomically alongside every `ledger_entries` insert. Never recalculate via SUM on the hot path (D-028). `last_ledger_entry_id` ties the cache to its source for staleness detection.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid UNIQUE FK → organizations (cascade) | |
| collected_paise | long | funds received from orders not yet settled |
| available_paise | long | funds cleared and available to withdraw |
| advanced_paise | long | advance payments sent before event ends |
| reserved_paise | long | held-back reserve percentage |
| settled_paise | long | fully settled historical total |
| lifetime_earned_paise | long | cumulative gross across all time |
| lifetime_withdrawn_paise | long | cumulative withdrawals across all time |
| last_ledger_entry_id | uuid FK → ledger_entries | points to the last entry that updated this cache |
| updated_at | timestamp | |

---

## Certificates & Design

### `design_templates`
Certificate or invite design — system-provided layouts or org-uploaded custom files.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| org_id | uuid | null = system design shared by all |
| event_id | uuid | null = org-wide; set = scoped to one event (D-023) |
| kind | text | `Certificate` or `Invite` |
| mode | text | `System` or `Custom` |
| name | text | |
| base_layout | text | code key for system designs (e.g. `classic_portrait`) |
| file_key | text | org-uploaded PDF/PNG override |
| placements_json | jsonb | `[{field, x, y, w, h, align, size, enabled}]` — percent-based coords |
| accent_color | text | hex color |
| signatory_json | jsonb | `{name, title, signature_key}` |
| suggested_category_slugs | text[] | auto-select hint by event category |
| is_active | bool | |
| created_at | timestamp | |

---

### `generated_cards`
Rendered invite card or group card image/PDF for an event.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events | |
| kind | text | `EventInvite` or `GroupCard` |
| group_id | uuid | set for GroupCard kind |
| template_id | uuid FK → design_templates | |
| image_key | text | 1080×1350 portrait image |
| square_image_key | text | 1080×1080 square image |
| pdf_key | text | A4 PDF |
| created_at | timestamp | |

---

### `certificates`
Issued attendance certificate per ticket+event. Unique by event+ticket.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events | |
| ticket_id | uuid FK → tickets | unique per event+ticket |
| user_id | uuid FK → users | |
| template_id | uuid FK → design_templates | |
| verify_code | text UNIQUE | 10-char base32, printed on cert for public verification |
| pdf_key | text | storage object key |
| status | text | `Generated`, `Emailed`, `Failed` |
| is_public | bool | visible on user profile |
| emailed_at | timestamp | |
| email_retry_count | int | |
| created_at | timestamp | |

---

## Invitations & Announcements

### `event_invitations`
One row per invited guest. Tracks send status, RSVP, and conversion to order.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| invited_by | uuid FK → users | org member who sent the invite |
| name | text | guest display name |
| invited_user_id | uuid FK → users | **D-266 M6 (D9 Method A)** — the invited Kurx user, when invited by username. Unique per event (partial index). Exactly one of `invited_user_id` / `email` / `phone` is set: an invitation addressed to nobody cannot be delivered, one addressed two ways cannot say which identity accepted it |
| email | text | optional; unique per event (partial index) |
| phone | text | E.164; optional; unique per event (partial index) |
| channel | text | `Email`, `WhatsApp`, `Both` |
| invite_token | text UNIQUE | 12-char URL-safe token on the RSVP link |
| send_status | text | `Pending`, `Sent`, `Failed` |
| rsvp_status | text | `None`, `Accepted`, `Declined` |
| order_id | uuid FK → orders | set when guest completes registration |
| send_count | int | how many times the invite was sent/resent |
| status | text | `Active`, `Revoked` |
| sent_at | timestamp | last successful send time |
| responded_at | timestamp | when RSVP was submitted |
| created_at | timestamp | |

---

### `event_invite_links`
D-266 M6 (D9 Method B) — a **shareable** link granting permission to register. Deliberately not the
per-invitee `event_invitations.invite_token`, which names one person: a link with seats and an expiry is a
different object with its own accounting, and overloading the per-invitee token would make "how many seats
are left" unanswerable.

Grants **permission to register and nothing more** — an invited guest of a paid event still pays.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| created_by | uuid FK → users | |
| token | varchar(64) UNIQUE | 32 chars, not the per-invitee token's 12: a shareable bearer capability lives in group chats and inboxes, so guessing must be hopeless |
| max_seats | int | null = unlimited. The cap the SQL claim carries in its WHERE |
| used_count | int | **Claimed in SQL** (conditional `ExecuteUpdateAsync`), never read-modify-write — D-240/D-261, same bug class as coupon over-redemption |
| single_use | bool | dead after one redemption, whatever `max_seats` says |
| expires_at | timestamptz | |
| passcode_hash | varchar(400) | hashed, never stored in the clear — if the row leaks, possession of the link must not also hand over the second factor. Verified server-side |
| status | text | `Active` \| `Revoked`. Revocation stops new arrivals; it never retracts seats already claimed |
| created_at | timestamptz | |

---

### `event_invite_link_redemptions`
D-266 M6 — one user's redemption of one link. Exists so redeeming twice is idempotent (D9 rule 7): without
a row per (link, user) a refresh would consume a second seat and the organiser's seat count would measure
clicks rather than people. The unique index is the guard — a read-then-write would be passed by two
concurrent redemptions.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| invite_link_id | uuid FK → event_invite_links (cascade) | unique with `user_id` |
| user_id | uuid FK → users | |
| redeemed_at | timestamptz | |

---

### `event_announcements`
Broadcast message sent to ticket holders or all registered guests.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | |
| created_by | uuid FK → users | org member who created it |
| title | text | max 120 chars |
| body | text | max 2000 chars plain text |
| audience | text | `AllRegistrants`, `CheckedIn`, `NotCheckedIn`, `TicketType` |
| include_child_events | bool | fan out to sub-events if true |
| channels | text[] | subset of `push`, `email`, `whatsapp` |
| status | text | `Queued`, `Sending`, `Sent`, `Failed`, `Scheduled` |
| scheduled_at | timestamp | null = send immediately |
| total_recipients | int | fan-out count (filled on send) |
| sent_push | int | push notifications delivered |
| sent_email | int | emails delivered |
| sent_whatsapp | int | WhatsApp messages delivered |
| failed_count | int | delivery failures |
| sent_at | timestamp | time fan-out completed |
| created_at | timestamp | |

---

## Notifications & Messaging

### `whatsapp_messages`
Outbound WhatsApp message log — every send attempt regardless of status.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| to_phone | text | E.164 recipient |
| template | text | WhatsApp template name used |
| kind | text | `Invitation`, `Announcement`, `OtpFallback`, etc. |
| wamid | text UNIQUE (partial) | WhatsApp message ID; null until sent |
| status | text | `Queued`, `Sent`, `Delivered`, `Read`, `Failed` |
| error | text | failure detail |
| related_type | text | `order`, `ticket`, `event`, `group`, `certificate`, `invitation` |
| related_id | uuid | entity ID |
| payload_json | jsonb | raw send request + last webhook body |
| retry_count | int | |
| created_at | timestamp | |
| updated_at | timestamp | |

---

### `notifications`
In-app notifications for a user (shown in notification centre).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | |
| kind | text | e.g. `ticket_issued`, `event_reminder` |
| title | text | short heading |
| body | text | message body |
| data_json | jsonb | includes `deep_link` for mobile navigation |
| read_at | timestamp | null = unread |
| created_at | timestamp | |

---

### `email_logs`
Outbound email audit trail (every email sent, regardless of provider).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| to_email | text | |
| template | text | template name used |
| subject | text | |
| status | text | `Queued`, `Sent`, `Failed` |
| provider_message_id | text | SES / SendGrid message ID |
| error | text | failure reason |
| related_type | text | entity type this email relates to |
| related_id | uuid | entity ID |
| created_at | timestamp | |

---

## Chat

`chat_rooms`, `chat_members`, `chat_messages` — **documented in [`docs/EVENT_CHAT_ARCHITECTURE.md`](../EVENT_CHAT_ARCHITECTURE.md)**, which is the canonical source for their columns, indexes, and constraints (D-104).

The detail previously duplicated here contradicted the code in four places (`post_policy`, `status`, `role`, and `kind` all listed enum values that do not exist). It was migrated rather than corrected in place, so the schema now has a single owner.

Two later companion tables live here rather than in that document, because they hang off a message rather than describing the room model (D-295, added 2026-08-07).

### `chat_message_reactions`
One row per (message, user, emoji). Emoji reactions on a chat message.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid PK | |
| MessageId | uuid FK → chat_messages | |
| UserId | uuid FK → users | |
| Emoji | varchar | The emoji itself, stored as text — not an id into a table of allowed emoji, so the set is open |
| CreatedAt | timestamptz | |

### `chat_message_hides`
A **per-user** hide. Hiding a message removes it from *your* view only; it is not a delete and not moderation — the row stays and everyone else still sees it. Distinct from moderation removal, which acts on the message itself.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid PK | |
| MessageId | uuid FK → chat_messages | |
| UserId | uuid FK → users | The person who hid it, never the author |
| CreatedAt | timestamptz | |

---

## Moderation

### `reports`
Polymorphic user-submitted content reports. `entity_type` + `entity_id` reference any reportable entity; no FK enforcement (D-025 — polymorphic pattern intentionally avoids FK to keep the schema open).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| reporter_id | uuid FK → users (cascade) | user submitting the report |
| entity_type | text | type of the reported entity (e.g. `event`, `chat_message`, `user`) |
| entity_id | uuid | ID of the reported entity (no FK enforced) |
| reason | text | `Spam`, `Inappropriate`, `Misleading`, `Harassment`, `Other` |
| details | text | optional additional context from the reporter |
| status | text | `Open`, `Resolved`, `Dismissed` |
| resolved_by | uuid | admin user who handled the report |
| resolved_at | timestamp | |
| created_at | timestamp | |

---

## Audit

### `audit_log`
Immutable record of every significant action by a user, admin, or the system.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| actor_type | text | `user`, `admin`, or `system` |
| actor_id | uuid | null for system-initiated actions |
| action | text | e.g. `event.publish`, `order.refund` |
| entity | text | entity type affected |
| entity_id | uuid | entity row ID |
| details_json | jsonb | before/after or extra context |
| created_at | timestamp | |

---

## Platform Roles

### `platform_roles`
Platform-wide (not org-scoped) role grants — `SuperAdmin`, `VerificationReviewer`,
`FinanceOps`, `Support`, `ReadOnlyAuditor`. Read **live per request** (M2, D-040);
platform authority is never carried in a JWT, so a grant/revoke is effective on the
next request. Distinct from org-scoped `memberships` (Owner/Manager/Staff/Finance).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users (CASCADE) | |
| role | text | `SuperAdmin` \| `VerificationReviewer` \| `FinanceOps` \| `Support` \| `ReadOnlyAuditor` |
| granted_by | uuid FK → users (RESTRICT), nullable | null = system (e.g. IsKurxAdmin backfill) |
| granted_at | timestamp | |
| expires_at | timestamp, nullable | null = no expiry; past = inactive |

Indexes: unique `(user_id, role)`, `user_id`, `granted_by`.

---

## Person Identity Verification

### `user_identity_verifications`
KYC of a **person** (1:1 with `users`, M3, D-042) — distinct from organization bank
verification (`org_bank_verifications`, M9). Graduated ID0–ID4 ladder. **Only masked
last-4 values are stored**; the full government-ID/PAN/account number is never persisted.
Provider calls go through `IKycProvider` (mock in dev); every decision appends a
`verification_reviews` row.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users (CASCADE), UNIQUE | one record per user |
| level | text | `Phone` \| `Contact` \| `GovernmentId` \| `Bank` \| `Liveness` (highest achieved — display) |
| status | text | `NotStarted` \| `Submitted` \| `UnderReview` \| `Approved` \| `Rejected` \| `ChangesRequested` \| `Expired` \| `Revoked` |
| govt_id_kind | text, nullable | `digilocker` \| `aadhaar_offline` \| `passport` \| `dl` … |
| govt_id_last4 | text, nullable | masked — never the full number |
| pan_last4 | text, nullable | masked |
| bank_last4 | text, nullable | masked (person's own account, individual payouts) |
| provider_refs_json | jsonb, nullable | provider request/response refs (no PII) |
| risk_score | int | snapshot; fed by M13 |
| reviewed_by | uuid FK → users (RESTRICT), nullable | null = automated/provider decision |
| reviewed_at / expires_at | timestamp, nullable | |
| submit_count | int | resubmission attempt counter (capped) |
| created_at / updated_at | timestamp | |

Index: unique `user_id`. Capability gates (M7) read the `*_last4` presence flags, not `level`.

---

## Trust & Verification

Shared substrate for every verification subsystem (identity KYC, organization
verification, membership verification, event approval, fraud, admin console).
Both tables are **polymorphic**: `subject_type` + `subject_id` point at
`users` / `organizations` / `memberships` / `events`, and `subject_id` carries
**no foreign key** — referential integrity is enforced by the writing service,
not the database (M0, D-039).

### `verification_documents`
A single piece of evidence backing a verification subject. The file lives in
private storage; only the key, a content hash (forgery/duplicate detection),
and extracted fields are persisted. **Profile bio is never evidence.**

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| subject_type | text | `UserIdentity` \| `Organization` \| `Membership` \| `Event` |
| subject_id | uuid | polymorphic — no FK |
| doc_type | text | open vocabulary, e.g. `govt_id`, `pan`, `college_letter`, `registration_cert`, `domain_proof` |
| storage_key | text | private-bucket object key (presigned per view) |
| sha256 | text, nullable | content hash — forgery/duplicate detection (M13) |
| extracted_json | jsonb, nullable | OCR/provider-extracted fields |
| status | text | `Pending` \| `Accepted` \| `Rejected` \| `Superseded` |
| uploaded_by | uuid FK → users (RESTRICT) | |
| created_at / updated_at | timestamp | |

Indexes: `(subject_type, subject_id)`, `sha256`, `uploaded_by`.

### `verification_reviews`
Append-only decision record for any verification subject — the audit spine of
the admin verification console (M12). Every approve/reject/request-changes
transition in identity/org/membership verification writes one row.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| subject_type | text | as above |
| subject_id | uuid | polymorphic — no FK |
| decision | text | `Approve` \| `Reject` \| `RequestChanges` |
| reviewer_id | uuid FK → users (RESTRICT), nullable | null = system/automated decision |
| reason_code | text, nullable | controlled reason vocabulary |
| notes | text, nullable | reviewer note (admin-only, never public) |
| risk_score | int, nullable | 0–100 snapshot at decision time (M13) |
| created_at | timestamp | |

Indexes: `(subject_type, subject_id, created_at)`, `reviewer_id`.

### `blacklist_entries`
Hard blocks on known-bad identifiers (M13, D-052). Value is stored normalized so lookups
are exact. Enforced at the trust boundary and at org creation.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| kind | text | `Phone` \| `Email` \| `Device` \| `OrgName` \| `DocHash` |
| value | text | normalized (phone E.164, email lowercased, org name normalized) |
| reason | text, nullable | |
| created_by | uuid FK -> users (RESTRICT), nullable | |
| created_at | timestamp | |

Index: unique `(kind, value)`.

### `fraud_signals`
Polymorphic risk signals contributing to a subject's risk score (M13, D-052).

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| subject_type | text | `UserIdentity` \| `Organization` \| `Membership` \| `Event` (no FK) |
| subject_id | uuid | polymorphic |
| kind | text | `Device`/`Ip`/`Velocity`/`DuplicateAccount`/`DocHash`/`DisposableContact`/`GeoMismatch`/`Manual` |
| value | text, nullable | |
| score | int | contribution to the subject's risk (sum >= threshold = not fraud-clear) |
| details_json | jsonb, nullable | |
| created_at | timestamp | |

Index: `(subject_type, subject_id)`.

---

## Analytics

### `event_views`
Append-only stream of event page views — **one row per view** (D-130). Added by Phase 0 of the
Event Architecture V3 program.

This table exists because the two things that needed it were previously both broken: `AnalyticsService`
fabricated `Views` and `UniqueVisitors` with `Random.Shared`, and `GET /v1/events/{slug}` performed a
synchronous `UPDATE events SET view_count = view_count + 1` on the public read path. A counter cannot
answer a distinct-visitor question and cannot be reconstructed; a stream does both.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events | |
| visitor_key | text | pseudonymous per-visitor identifier; supports `COUNT(DISTINCT)` for unique visitors. **Not anonymous** — subject to the retention policy |
| viewed_at | timestamp | UTC |

**Never updated or deleted on the write path** — append only. `events.view_count` was, until Phase 17,
periodically re-derived from this table by a nightly aggregation job; that job was retired (D-192) along
with the two pre-aggregated tables it filled, so `view_count` is now a **frozen, unmaintained legacy
column** — it stops advancing at whatever value it held when the job was removed and must not be read for
current traffic. Every live read of "views"/"unique visitors" (org/event analytics, admin `top_events`,
CSV export) queries this table directly, at request time, through `IAnalyticsFactSource`
(`Kurx.Infrastructure/Analytics/AnalyticsFactSource.cs`) — not through `view_count`.

**Growth:** unbounded, on a public endpoint. Partition-by-time plus a declared retention policy is the
control (V3 §17.3) and is additive — not yet implemented.

**`event_analytics_daily` / `organization_analytics` (retired, D-192).** These were nightly-job
pre-aggregated rollups of this stream plus the other leaf-fact tables (VAR, Registration, Ticket, Refund).
Phase 17 (Analytics rebuild) dropped both tables and the job that filled them — every number they held is
reconstructable from the leaf facts, computed at request time instead of pre-aggregated. Neither table
exists in the schema anymore; do not re-add them to this file's Summary index below.

---

## Posts (D-262)

The social feed. Twelve tables, all added by one additive migration (`AddPostsModule`).

| Table | Stores | Notes |
|---|---|---|
| `posts` | One post | `Id` is **UUIDv7** so it doubles as the keyset cursor; ordering is the `(CreatedAt, Id)` pair. `Kind` is derived server-side from what is attached, never trusted from the client. `LikeCount`/`CommentCount`/`ShareCount` are contested — mutated in SQL only (D-240). `IsHidden` is admin moderation, mirroring `events.IsHidden`; `IsDeleted` is the author's soft delete. `SharedPostId` is a self-FK with **Restrict**, so deleting an original cannot delete other people's reshares. |
| `post_media` | One file on a post | `PostId` is **nullable** — the upload completes before the post exists, exactly as `chat_attachments` does, which is why the hourly `post-media-cleanup` orphan sweep is needed at all. Unique on `StorageKey`. |
| `post_polls` | One poll per post | Unique on `PostId`. `TotalVotes` counts votes **cast**, so it is exactly the sum of the option counts. |
| `post_poll_options` | One option | `VoteCount` mutated in SQL. |
| `post_poll_ballots` | One voter's claim on a poll | Unique `(PollId, UserId)`. Exists because a ballot is N vote rows and "at most one ballot" is not expressible over N rows — see the pattern note in `.claude/memory/database-conventions.md`. |
| `post_poll_votes` | One vote for one option | Unique `(PollId, UserId, OptionId)`. |
| `post_likes` | A like | Unique `(PostId, UserId)` — the index, not an application check, is what makes liking idempotent under a double tap. |
| `post_comments` | A comment or a reply | UUIDv7. Threading is exactly one level: a reply to a reply attaches to the same parent. |
| `post_comment_likes` | A comment like | Unique `(CommentId, UserId)`. |
| `post_saves` | A bookmark | Unique `(PostId, UserId)`; listed by save time, not post time. |
| `post_hashtags` | An extracted `#tag` | Lowercased, extracted **server-side** from the body. Unique `(PostId, Tag)`, plus an index on `Tag` for the per-tag feed and the trending roll-up. |
| `post_mentions` | A resolved `@mention` | Unique `(PostId, MentionedUserId)`. Unresolvable handles are dropped rather than stored. |

## Account settings (D-263)

| Table | Stores | Notes |
|---|---|---|
| `notification_preferences` | One user's choice for one category | Unique `(UserId, Category)`; four channel booleans. **Absence of a row is not "off"** — it means every channel on except WhatsApp, i.e. the behaviour that existed before the table, which is why no backfill was needed. |
| `user_blocks` | A one-way block | Unique `(BlockerId, BlockedId)`, plus an index on `BlockedId` because enforcement is **symmetric** and every read path checks both directions. |

`users` also gained `Language`, and the deletion lifecycle columns `DeletionRequestedAt`,
`DeletionScheduledFor`, `DeletionReason`, `AnonymizedAt`. The row is **never deleted**: it is
anonymised in place so orders, tickets, certificates and audit logs keep resolving (D-263).

## Direct messages (D-264)

No new tables. `chat_rooms` gained a nullable `EventId`, the participant pair
(`DirectLowUserId`/`DirectHighUserId`), `DmRequestState` and `DmInitiatedBy`; `chat_members` gained
`ArchivedAt`. Two index changes carry the weight:

- the existing unique `(EventId, Kind)` is now **filtered to `"EventId" IS NOT NULL`**, so "one General
  room per event" still means exactly that;
- a new unique `ix_chat_rooms_direct_pair` over the canonical pair is what makes DM creation
  idempotent — two simultaneous taps both insert and one loses.

---

## Summary

> **Legacy index (through M13 / table 70).** This numbered list predates the Event Architecture V3 program and is
> **not exhaustive** — the V3 tables added in Phases 1–11 (`event_kinds`, `kind_aliases`, `capabilities`,
> `kind_capability_defaults`, `event_capabilities`, `org_units`, `audience_rules`, `participant_roles`,
> `event_participants`, `inventory_pools`, `registration_policies`, `registrations`, `admissions`, `credentials`,
> `passes`, `admission_rights`, `var_lines`, `teams`, `team_memberships`, `team_invites`, `team_join_requests`,
> `team_policies`, `stages`, `stage_participants`, `fixtures`, `fixture_participants`, `fixture_officials`,
> `scoring_policies`, `judge_scores`, `public_votes`, `stage_results`, `result_corrections`, `event_series`,
> `event_series_followers`, `seat_blocks`, `seat_block_seats`, `approval_chains`, `approval_steps`, `approval_requests`,
> `approval_step_decisions`) are documented in the detailed sections above, not repeated here. A full re-index is a
> documentation-cleanup backlog item, not a per-phase task.

| # | Table | Stores |
|---|-------|--------|
| 1 | users | Registered accounts (phone-first) |
| 2 | refresh_tokens | Rotating JWT refresh tokens |
| 3 | username_history | Released usernames (30-day reclaim hold) |
| 4 | username_change_log | Full audit trail of username changes |
| 5 | otp_requests | ⚠️ **Retired (D-215)** — legacy unsalted-SHA-256 OTP store, no longer written or read; superseded by `otp_codes` |
| — | otp_codes | OTP codes for **every** flow incl. login — HMAC-peppered, `PepperVersion`-rotatable (D-215) |
| 6 | devices | FCM push tokens per device |
| 7 | organizations | Institutions a user may **represent** — not accounts, and not the owner of any event (D-268) |
| 8 | memberships | User ↔ Org role assignments |
| 9 | org_invitations | GitHub-style org membership invitations (time-limited, explicit accept) |
| 10 | organization_followers | Users following an org |
| 11 | org_bank_verifications | Org bank/PAN verification attempts (renamed from kyc_records, M9) |
| 12 | risk_flags | Fraud / compliance flags |
| 13 | payout_schedules | Advance/reserve/tier rules per org |
| 14 | event_categories | 3-level taxonomy (Audience/Category/Type) |
| — | event_kinds | V3 closed 20-Kind catalog (Phase 1) |
| — | kind_aliases | 145 legacy type names → Kind, data-driven (Phase 1) |
| 15 | events | Core event records |
| 16 | tags | Freeform keyword tags |
| 17 | event_tags | Event ↔ Tag join |
| 18 | venues | Reusable venue profiles |
| 19 | venue_images | Venue photo gallery |
| 20 | event_templates | Reusable event blueprints |
| 21 | speakers | Speaker profiles per org |
| 22 | event_speakers | Speaker ↔ Event assignment |
| 23 | event_sessions | Agenda items (talks + breaks) |
| 24 | event_session_speakers | Speaker ↔ Session assignment |
| 25 | sponsors | Sponsor profiles per org |
| 26 | event_sponsors | Sponsor ↔ Event assignment |
| 27 | event_media | Gallery / docs / posters per event |
| 28 | saved_events | User event bookmarks |
| 29 | event_assignments | Staff / volunteer / judge role assignments per event |
| 30 | event_checkin_devices | Scanner devices registered to an event |
| 31 | event_reviews | Post-event attendee ratings and reviews |
| 32 | ticket_types | Ticket categories per event |
| 33 | form_fields | Custom registration form fields (ticket-type-scoped) |
| 34 | field_presets | Saved form-field bundles by category |
| 35 | seat_holds | Short-lived inventory locks during checkout |
| 36 | ticket_waitlist | Per-ticket-type waitlist queue |
| 37 | orders | Checkout sessions |
| 38 | order_items | Line items within an order |
| 39 | payments | Razorpay payment records |
| 40 | refunds | Refund requests and status |
| 41 | groups | Group registration units |
| 42 | group_members | Individual slots within a group |
| 43 | tickets | Individual entry tickets with QR code |
| 44 | ticket_transfers | Peer-to-peer ticket transfer requests |
| 45 | gate_entries | Check-in scan log |
| 46 | transfers | Razorpay route transfers to org accounts |
| 47 | ledger_entries | Append-only financial ledger |
| 48 | withdrawals | Org payout withdrawal requests |
| 49 | organization_wallet | Cached balance summary per org (D-028) |
| 50 | design_templates | Certificate / invite design templates |
| 51 | generated_cards | Rendered invite / group card images |
| 52 | certificates | Issued attendance certificates |
| 53 | event_invitations | Per-guest invite with RSVP tracking |
| 54 | event_announcements | Broadcast messages to ticket holders |
| 55 | whatsapp_messages | Outbound WhatsApp message log |
| 56 | notifications | In-app notification inbox |
| 57 | email_logs | Outbound email audit trail |
| 58 | chat_rooms | One chat room per event |
| 59 | chat_members | Users joined to a chat room |
| 60 | chat_messages | Individual chat messages |
| 61 | reports | Polymorphic user-submitted content reports |
| 62 | audit_log | Immutable action history |
| 63 | verification_documents | Polymorphic evidence for any verification subject (M0) |
| 64 | verification_reviews | Append-only verification decisions / audit spine (M0) |
| 65 | platform_roles | Live-read platform role grants (SuperAdmin/Reviewer/Finance/Support) (M2) |
| 66 | user_identity_verifications | Person KYC — graduated ID0–ID4, masked last-4 only (M3) |
| 67 | organization_aliases | Alt names (acronym/former/expansion) → canonical org, fuzzy search (M4) |
| 68 | membership_claims | Evidence-backed affiliation claims (Student/Faculty/…) → verified membership (M6) |
| 69 | blacklist_entries | Hard blocks on known-bad phone/email/device/org-name/doc-hash (M13) |
| 70 | fraud_signals | Polymorphic risk signals → risk score (M13) |


## Event creation (D-265, 2026-08-03)

Additive migration `AddEventCreationFields` — 41 columns across `events`, `ticket_types` and
`sponsors`, plus three tables. No existing column changed type or was dropped.

| # | Table | Purpose |
|---|---|---|
| 71 | registration_consents | Evidence a registrant accepted the organiser terms. Stores the **SHA-256 of the exact text shown**, not the text: an organiser can edit `events.consent_text` afterwards, and a consent row that silently starts pointing at different wording is worse than none. Unique per registration. |
| 72 | coupons | Per-event discount codes. `code` is unique **per event**, not globally — two organisers both wanting `WELCOME10` is normal. `redeemed_count` is a shared counter and must be claimed in SQL when a service consumes it (D-240/D-261); no service exists yet. |
| 73 | coupon_redemptions | One row per redemption. Unique `(coupon_id, order_id)` so a retried checkout cannot double-count; indexed by `(coupon_id, user_id)` to enforce `max_per_user`. |

New `events` columns group as: **content** (tagline, short_description, logo_key, thumbnail_key,
promo_video_key, rules, faq_json) · **legal** (terms_url, terms_text, code_of_conduct, refund_policy,
cancellation_policy, requires_consent, consent_text) · **windows** (registration_opens_at/closes_at,
checkin_opens_at/closes_at, result_date, certificate_release_at, auto_close) · **location detail**
(building, floor, room, google_maps_url, meeting_platform, meeting_password) · **eligibility**
(min_age, max_age, gender_restriction, max_teams) · **commerce** (platform_fee_percent,
platform_fee_flat_paise, tax_percent, tax_inclusive, prize_pool_json).

`meeting_password` is a **secret**: it is never serialised by the event projection, because that
projection also serves the public `GET /v1/events/{slug}`.

`ticket_types` gains `kind`, `min_amount_paise`, `suggested_amounts_json`, `refund_policy`;
`sponsors` gains `booth`.

---

### `event_draft_snapshots`
D-266 M8 — the create/edit wizard's autosave. **Opaque client state**: never parsed server-side, never read
by any projection, policy or publish path. It is what the organiser has *typed*, not what the event *is* —
giving it columns would create a second, weaker definition of an event.

**jsonb preserves content, not bytes.** It reparses on write, so whitespace and key order are normalised
and duplicate keys collapse. Fields the server knows nothing about survive intact; the exact string does
not. A caller must not expect to get back the bytes it sent.

| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| event_id | uuid FK → events (cascade) | unique with `user_id` |
| user_id | uuid FK → users | Per user: two managers mid-edit each keep their own form. Merging produces something neither typed; last-write-wins discards work |
| payload_json | jsonb | The in-progress form. Deliberately includes half-filled steps that would fail real validation. **jsonb reparses on write** — content is preserved, but whitespace/key order are normalised, so a caller must not expect the exact bytes back |
| step_key | varchar(40) | Where to resume, so autosave restores position and not just data |
| saved_at | timestamptz | |

---

## Allies — professional connections (D-201, documented 2026-08-12)

### `ally_connections`
A **mutual** connection between two users. One row per pair, not one per direction — which is why the pair
is stored twice in two different shapes.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid PK | |
| UserLowId | uuid FK → users | The **smaller** of the two ids |
| UserHighId | uuid FK → users | The **larger**. Low/High is an ordering trick, not a role: a unique index on (Low, High) is what makes "A connects to B" and "B connects to A" collide into one row instead of creating two half-connections |
| RequesterId | uuid FK → users | Who asked. Equal to Low *or* High — this is where direction lives |
| AddresseeId | uuid FK → users | Who was asked |
| Status | text | `Pending` / `Accepted` / `Declined` / `Blocked` |
| Visibility | text | Per-connection visibility. **The more private of the two parties' choices wins** (D-230) — never assume the viewer's own setting decides |
| ConnectedVia | text, nullable | Provenance (e.g. a shared event) |
| FirstSharedEventId | uuid, nullable | |
| LastInteractionAt | timestamptz, nullable | |
| CreatedAt / RequestedAt | timestamptz | |
| RespondedAt | timestamptz, nullable | Null while `Pending` |
| UpdatedAt | timestamptz | |
| HiddenByLow / HiddenByHigh | boolean | Per-side hide. Two booleans rather than one because hiding is unilateral — either party may hide the connection from their own list without affecting the other's |

A decline is a **30-day cooldown**, not a permanent state (D-230).

---

## Gamification (documented 2026-08-12)

Points, badges and leaderboards. Entirely separate from money: `points_ledger` is not a financial ledger and
its `Points` are not paise.

### `points_ledger`
Append-only points history. Balances are summed from it.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid PK | |
| UserId | uuid FK → users | |
| Source | text | What earned them |
| Points | integer | Signed — a correction is a negative row, never an edit to an existing one |
| Reason | text, nullable | |
| CreatedAt | timestamptz | |

### `badges`
The badge catalogue. Platform-defined, not user-created.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid PK | |
| Name | text | |
| Type | text | |
| Description | text | |
| IconKey | text, nullable | Storage key, **not a URL** (D-302) — resolve through `IStorage` |
| CreatedAt | timestamptz | |

### `user_badges`
Award join table. One row per badge earned per user.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid PK | |
| UserId | uuid FK → users | |
| BadgeId | uuid FK → badges | |
| EarnedAt | timestamptz | |

### `leaderboards`
A **materialised ranking**, rebuilt hourly by `LeaderboardRefreshJob` — never computed on the request path.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid PK | |
| Scope | text | What the ranking is over (e.g. global, per-event) |
| ScopeId | text, nullable | Text, not uuid — scope keys are not all entity ids. Null for a global board |
| UserId | uuid FK → users | |
| Points | integer | Snapshot at refresh time; may lag `points_ledger` by up to an hour, by design |
| Rank | integer | Stored rather than derived, so paging a leaderboard is a plain index scan |
| UpdatedAt | timestamptz | |

### `referral_rewards`
| Column | Type | Notes |
|--------|------|-------|
| Id | uuid PK | |
| ReferrerUserId | uuid FK → users | |
| RefereeUserId | uuid FK → users | |
| Status | text | |
| AmountPaise | bigint | **Money** (D-004) — unlike everything else in this section. Real value, so it settles through the ledger, not through points |
| CreatedAt | timestamptz | |

---

## Search index (V3 §15 / Phase 16, documented 2026-08-12)

### `event_search_documents`
One denormalised row per event, keyed on `EventId` (**the PK is the event id — there is no separate id**).
Maintained by `SearchIndexService` through the outbox, so a write never blocks on indexing. It exists to let
discovery answer from one table instead of joining eight, and every column is a copy — **the event is
authoritative, this is a projection**. Stale rows are a reindex, never a source of truth.

| Column | Type | Notes |
|--------|------|-------|
| EventId | uuid PK, FK → events | |
| TitleText / BodyText / FuzzyText | text | Three separate corpora because they are weighted differently: title matches rank above body, and `FuzzyText` feeds trigram matching for typos |
| search_vector | tsvector, nullable | GIN-indexed. **The only lowercase column name in the schema** — it is written by a database trigger/expression rather than mapped from a property |
| OrgId | uuid | |
| OrgUnitId | uuid, nullable | |
| CategoryId | uuid | |
| TypeId | uuid, nullable | |
| KindSlug | text, nullable | Null for an unkinded event; kinding never blocks creation |
| EventMode | text | |
| IsPaid | boolean | |
| Language / City / Country | text | |
| Lat / Lng | double precision, nullable | Stored, but proximity filtering is **deferred** (named in D-190) — present is not wired |
| StartsAt / EndsAt | timestamptz | |
| ListedStandalone | boolean | Sub-events are not independently discoverable unless they opt in (V3 §3.4) |
| ParentEventId | uuid, nullable | |
| SeriesId | uuid, nullable | |
| SeriesMode | text, nullable | |
| IsSeriesPrimary | boolean | Collapses a RECURRING series to one listing |
| HasAudienceRule | boolean | A flag, **not the rule** — eligibility is still resolved server-side against `audience_rules`. Denormalising the rule itself would let the index grant access |
| IsFeatured | boolean | |
| RecentViewCount / ConversionCount | integer | Ranking signals, refreshed every 15 min by `SearchIndexRefreshJob` |
| EventCreatedAt | timestamptz | |
| IndexedAt | timestamptz | How index staleness is measured |

---

## Capability defaults by category (documented 2026-08-12)

### `category_capability_defaults`
Per-taxonomy-node capability presets — the category-scoped sibling of `archetype_capability_defaults` and
`kind_capability_defaults`. Slug-referenced, so there is **no FK** to `capabilities`.

| Column | Type | Notes |
|--------|------|-------|
| Id | uuid PK | |
| CategoryNodeId | uuid FK → event_categories | |
| CapabilitySlug | text | Slug, not an FK — the same loose-reference convention the other capability-default tables use |
| State | text | The default state this category implies for that capability |

Remember that a capability **describes** what an event supports and never authorizes anything (D-269); these
rows shape a new event's materialised capability set, not who may act on it.
