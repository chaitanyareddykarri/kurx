# Database Conventions

## Local environment

- Postgres 17, port 5432, user/pass `kurx`/`kurx`, DBs `kurx` (app) + `kurx_test` (tests). Docker Compose also provides Postgres/Redis.
- **No `psql` binary assumed** — verify schema exclusively via EF migrations or integration tests.
- `.NET 10 SDK` at `~/.dotnet` (or system-wide install) — `DOTNET_ROOT` must point to the SDK root before any `dotnet`/`dotnet-ef` command. `global.json` pins version 10.0.301.

## Soft-delete pattern

Soft-deletable tables (`events`, `organizations`, `venues`, `speakers`, `sponsors`, `ticket_types`, `event_reviews`) use `deleted_at TIMESTAMPTZ NULL` (null = active). EF Core `OnModelCreating` pattern:
```csharp
e.HasIndex(x => x.OrgId)
    .HasDatabaseName("ix_venues_org_active")
    .HasFilter("\"DeletedAt\" IS NULL");  // must double-quote PascalCase column names
```
The `\"..\"` escaping in C# string literals produces `"ColName" IS NULL` in SQL. Never use `HasFilter("DeletedAt IS NULL")` — that fails silently on PascalCase columns.

## Migrations

- Always add a new migration (`dotnet ef migrations add <Name>`) for schema changes — never edit an already-applied one.
- Check whether an existing migration already covers the need before adding one; a full feature slice (Event Management) once needed zero new migrations beyond `Initial`.
- Migrations auto-apply at API startup (`db.Database.MigrateAsync()`); a failed migration aborts the host rather than serving traffic.

### Generating migrations (D-128 — supersedes the hand-written-migration workaround)

`dotnet ef` cannot run on the Windows host (Smart App Control blocks loading `Kurx.Infrastructure.dll`,
`0x800711C7`). Earlier work therefore hand-wrote migrations **and** hand-synced
`KurxDbContextModelSnapshot.cs`. **Stop doing that** — it works in a Linux container, so the snapshot is
machine-generated and an entire class of hand-sync error disappears:

```bash
MSYS_NO_PATHCONV=1 docker run --rm --network container:kurx-postgres \
  -v "/d/event/kurx:/src" -w /src/backend \
  -e ConnectionStrings__Default="Host=localhost;Port=5432;Database=kurx;Username=kurx;Password=kurx" \
  -e JWT_SECRET=... -e TICKET_HMAC_SECRET=... -e OTP_PEPPER=... \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  'dotnet tool install --global dotnet-ef --version 10.* >/dev/null 2>&1; \
   export PATH="$PATH:/root/.dotnet/tools"; \
   dotnet restore Kurx.sln && dotnet ef migrations add <Name> \
     --project Kurx.Infrastructure --startup-project Kurx.Api'
```

`dotnet restore` inside the container is required first — `obj/` otherwise holds Windows NuGet paths and
EF fails with `NETSDK1064`. Do **not** pass `-p:ArtifactsPath` to `dotnet ef` (`-p` is its `--project`).

## EF gotchas

- EF Core can't translate `OrderBy` on record-constructor projections. Project to an anonymous type, order/materialize, then map to the record afterward.
- EF Core 10 + Npgsql 10.0.2 (targeting `net10.0`) — don't bump without checking `docs/DECISIONS.md`.
- ImageSharp must stay pinned at 3.1.x (4.0 requires a paid license, D-011).

## Shared counters: mutate in SQL, never read-modify-write (D-240)

Any column more than one request can touch — wallet balances, inventory pools, usage counters — is
updated with a single statement whose new value the **database** computes, and any precondition on it
goes in the `WHERE` so it is evaluated under the row lock:

```csharp
await db.OrganizationWallets
    .Where(w => w.OrgId == orgId && w.CollectedPaise >= amount)   // guard: evaluated under the lock
    .ExecuteUpdateAsync(s => s
        .SetProperty(w => w.CollectedPaise, w => w.CollectedPaise - amount)
        .SetProperty(w => w.UpdatedAt, DateTime.UtcNow), ct);
```

Loading the row and writing `entity.Counter += n` makes EF emit `SET "Counter" = <literal>`. Postgres
READ COMMITTED does **not** save you: the second writer blocks on the row lock, then overwrites with a
value computed before the first one committed. This is a *lost update*, not a deadlock — it fails
silently and the row simply ends up wrong. It cost the wallet five of six concurrent payments in a
measured run (D-240); `InventoryService.TryConsumeManyAsync` has always done it correctly.

Three things that bite when converting a site:

- **Do not also track the entity.** A tracked copy still in the change tracker makes `SaveChangesAsync`
  emit a second, stale `UPDATE` that undoes the increment. Drop the `First…Async` load entirely, or
  read it `AsNoTracking()`.
- **`ExecuteUpdateAsync` runs immediately**, ahead of pending tracked changes. If the new value
  references a row from the same unit of work (an FK such as `LastLedgerEntryId`), that row must be
  saved first.
- **0 affected rows is a real outcome.** It means the guard failed *or* the row is missing. Decide
  which, and fail loudly for the latter — a money write that silently affects nothing is the bug.

Same idiom for claiming a state transition (`Pending → Paid`, `Collected → Available`, rotating a
refresh token, an event's review status): put the current state in the `WHERE`, and treat 0 rows as
"someone else won". `ChatService` (room status), `OrderService`/`RefundService` (both name the result
`claimed`), `ChallengeService`, `LoginApprovalService` and `EventService.TransitionAsync`
(`transition_conflict`) all do exactly this — **reuse it rather than adding a second mechanism.** EF
`RowVersion` and `xmin` were both weighed for event transitions (D-266 M4 addendum) and rejected for that
reason alone.

### Do not reach for `SELECT … FOR UPDATE` on a request path (D-231)

It was tried on the profile-visibility write and reverted: the suite went from **~7 minutes to 5h42m**.
Postgres applies **no default lock timeout**, so one transaction that takes the lock and does not commit
blocks *every* later writer indefinitely — turning a lost update into a hang, which is the worse failure.
The fix was a single atomic statement, whose row-level lock is taken and released **inside** the statement
and never held across an `await`.

This is not an absolute ban: `WalletService` holds one inside a short, tightly-scoped withdrawal
transaction (D-031), and `pg_advisory_xact_lock` is used deliberately in the seat/order paths and around
the boot-time seeders (D-344). The rule is narrower and worth stating exactly — **never hold a row lock
across an `await` on a request path**, and prefer the conditional `UPDATE` whenever the guard can be
expressed in a `WHERE`.

**Advisory locks are per-database, and that is load-bearing here.** Measured, not assumed: the same key
held in database A is freely acquirable in database B and refused in A. It is what makes the seeding lock
free in the suite — each test class runs on its own `kurx_test_<guid>`, so ~176 `WebApplicationFactory`
boots never contend — while production replicas, all on `kurx`, serialize. Anything reasoning about
advisory-lock contention must account for which database the connection is on.

**Read-then-insert is not idempotent just because it checks first.** The boot seeders each SELECT what
exists, diff a catalog, and insert the difference. That is a check-then-act race the moment two processes
run it: both observe a row missing, both insert, and the unique index turns the loser into a `23505`. A
guard that reads in one statement and writes in another needs a lock, `ON CONFLICT DO NOTHING`, or a
conditional `UPDATE` — the "already populated" check alone is decoration under concurrency.

### "At most one group of N rows per key" needs its own anchor row (D-262)

A unique index constrains rows, not groups. When one logical action writes N rows — a poll ballot is
one row per selected option — "at most one ballot per voter" is **not** expressible over those rows:
two concurrent requests from one account naming *different* options both pass an application-level
"have you done this?" check, and a unique index on `(poll, user, option)` only forbids the same option
twice, so both land.

The fix is a one-row-per-group table carrying the unique key (`post_poll_ballots`, unique
`(PollId, UserId)`), inserted **first** in the same `SaveChangesAsync`. The loser fails on the index
instead of getting through. Reach for this whenever a unique constraint has to span a set rather than
a row; the alternatives (a synthetic slot column, an advisory lock, `SERIALIZABLE`) are all cleverer
and none of them is smaller.

## Bulk deletes are batched and database-side, never materialized (D-324)

`ToListAsync()` + `RemoveRange()` + `SaveChangesAsync()` over a retention predicate is the shape to
recognise and never write. It loads every matching row into the change tracker and makes EF emit **one
DELETE per row** inside a single transaction — at a million rows, an out-of-memory failure or an
hours-long lock, faithfully replayed by `[AutomaticRetry]`. Use `ExecuteDeleteAsync` with an explicit
batch bound:

```csharp
while (batches < MaxBatchesPerRun)
{
    ct.ThrowIfCancellationRequested();
    var deleted = await db.Notifications
        .Where(n => db.Notifications.Where(x => x.CreatedAt < cutoff)
            .Select(x => x.Id).Take(BatchSize).Contains(n.Id))
        .ExecuteDeleteAsync(ct);
    if (deleted == 0) break;
    batches++;
}
```

The inner `Take` is what makes each statement bounded — a bare `ExecuteDeleteAsync` on the predicate
deletes everything in one transaction and reintroduces the lock problem. Always carry a
`MaxBatchesPerRun` ceiling so a wrong predicate cannot become an infinite loop holding a worker and a
connection, and always make sure the predicate has an index (a retention sweep filters on `CreatedAt`
alone, which a composite led by `UserId` cannot serve).

## A paginated `ORDER BY` must be a TOTAL order (D-325)

`OrderByDescending(x => x.CreatedAt)` followed by `Skip/Take` is the shape to recognise. `CreatedAt` comes
from `DateTime.UtcNow`, whose resolution is coarser than a bulk write — so group ticket issuance,
announcement fan-out, certificate generation, CSV invitation import and ledger settlement all write rows
sharing one instant. The database is then free to order those ties differently per query, which repeats a
row on one page and silently drops another. Neither shows up as an error.

```csharp
// wrong — ties are ordered by whatever the plan happens to produce
.OrderByDescending(t => t.CreatedAt).Skip(skip).Take(take)

// right — the second key makes the order total
.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Skip(skip).Take(take)
```

The tie-breaker does not have to be `Id`, and blindly reaching for it is its own mistake — use the key that
is genuinely unique *for that projection*. A competition-results list ties on `Rank` (joint placements) and
on `PublishedAt` (one publish action stamps a whole stage), so it needs `Rank → PublishedAt → result id`.
A list merged in memory from several queries has no row id at all and ties on `Slug` then `Title`.

This applies to in-memory sorts too. LINQ-to-Objects' sort is stable, but stability only preserves an input
order that was itself deterministic — and if each contributing query ordered on a non-unique key, it was not.

## Index decisions are made from a query plan, not from the column list (D-325)

Three indexes were proposed by an audit; all three were refused, each for a different reason that only
inspection could produce:

- **Already existed.** `notifications.CreatedAt` was live in `pg_indexes` from D-324. Read the database, not
  the audit.
- **Proven inert.** A GIN trigram index on `events.Title` was built and measured on 200k rows. The plan did
  **not change** — the predicate is `ILIKE '%t%' OR org.Name ILIKE '%t%'`, and an `OR` spanning two tables is
  evaluated after the join, so neither branch can be pushed to an index. The same predicate against one
  column: 138 ms → 1.8 ms. When the query shape is what blocks the index, fix the shape or add nothing.
- **Wrong problem.** `tags.Name` looked like a substring search; the predicate is `ILike(t.Name, name)` with
  **no wildcards** — case-insensitive equality — running inside a `foreach`. The cost was N round trips, not
  the scan. Batching removed it; no index was added, because `tags` is small enough that a sequential scan is
  correctly the cheaper plan.

Corollary: `idx_scan = 0` on a development database is evidence of nothing. Removing an index needs
`pg_stat_user_indexes` over a representative production period, a check that it is not required for
uniqueness / FK / concurrency, and one removal at a time.

## Notification idempotency: a nullable key with a PARTIAL unique index (D-324)

When a notification must not repeat, give it a `DedupKey` (`{kind}:{subjectId}`, built by
`NotificationDedup`) and let `ix_notifications_dedup` — `UNIQUE (DedupKey, UserId) WHERE DedupKey IS NOT
NULL` — be the authority. Not a pre-check: two replicas racing one fan-out both pass a pre-check.

**Two rules, both learned the hard way.**

*Keep it partial.* Almost every notification kind repeats legitimately — an event changes materially more
than once, an invitation is re-sent after a decline, an ally request may be re-made after D-230's cooldown,
an authorization is resubmitted and re-reviewed. Only `event_announcement` and `EventReminder` populate the
key; everything else leaves it null and is unconstrained. A blanket `UNIQUE (UserId, Kind, subject)` would
suppress real user-facing messages, and a wrongly-deduplicated notification is one the user never receives.

*Column order is `DedupKey` first.* Uniqueness is identical either way, reads are not: the fan-out asks
`WHERE DedupKey = x` with no user predicate, which a `UserId`-led index cannot seek — measured on 500k
rows, 4,380 buffers / 37.7ms versus 557 / 18.6ms the right way round.

Callers pass it through `NotifyAsync(..., dedupKey:)`, which swallows a conflict on **that named index
only** and detaches the failed entity — leaving it tracked makes the next `SaveChangesAsync` retry and
throw, turning one skipped duplicate into a failed batch of unrelated writes.

## Connection pooling is configured, not inherited (D-324)

`DatabaseOptions` owns pool and timeout values; never call `UseNpgsql(connectionString)` bare. Npgsql's
`MaxPoolSize` default of 100 *per process* against a 10-task autoscaling ceiling demands ~1,000 connections
from a database offering ~439. API traffic and Hangfire get **separate pools**, distinguished by
`Application Name` — that differing string is the mechanism, not a label. Full arithmetic and the
still-open staging validation: `docs/deployment/CONNECTION_POOLING.md`.

Two traps if you touch that file. `NpgsqlConnectionStringBuilder.ContainsKey` returns **true for every
keyword it knows**, so it cannot answer "did the operator set this?" — parse with a plain
`DbConnectionStringBuilder`, which keeps only supplied keys. And Npgsql refuses an idle lifetime below its
10s pruning interval, at data-source *build* time, i.e. on the first connection rather than at startup;
`DatabaseOptions` checks it up front so that is a failed deploy, not a failed request.

## Test isolation

**Each test class gets its own database.** `KurxApiFactory` creates `kurx_test_<guid>` per class
(`IClassFixture` → one factory) as a `CREATE DATABASE … TEMPLATE` clone of a single migrated template
built once per run — so the ~50-migration schema is applied once, not once per class, and one class's
teardown can never race another's migration. The template name is per-process unique, so a concurrent
run from another workstream cannot drop it mid-clone.

Cross-class parallelization is still disabled in `AssemblyInfo.cs`, and the suite remains
order-sensitive by design — **only a full-suite run is evidence**, a `--filter` subset is not.

`ResetDatabase()` uses `DROP DATABASE ... WITH (FORCE)` (D-128). The previous `pg_terminate_backend`-then-`DROP`
pair left a gap in which the *previous* test class's Hangfire workers reconnected, which failed **36 of 489
tests** on database-lifecycle errors alone (`23505`/`3D000`/`55006`/`42P04`) while every affected class passed
in isolation. If those SQLSTATEs reappear, the cause is a reset racing a teardown — not the code under test.

**Never edit the working tree while a suite run is in progress.** `dotnet test` builds first, so an in-flight
edit compiles a DbContext against a database migrated without it; this once produced a spurious 435-failure
run that looked like catastrophic regression and was purely operator error.


## Enums are stored as text — but nullable ones are not, unless you say so

`KurxDbContext.OnModelCreating` converts every enum property to text (the member name) so SQL stays
readable. The loop tests `prop.ClrType.IsEnum`, which is **false for a nullable enum** — `EventProduct?`
has a ClrType of `Nullable<EventProduct>`. A nullable enum therefore lands as `integer` while the
non-nullable copies of the same enum land as `text`, and nothing warns you.

Configure nullable enums explicitly: `e.Property(x => x.Foo).HasConversion<string>()` (D-266).
Do **not** "fix" the loop to catch them — `ApproverRole` and `DmRequestState` are already integer columns
with live data, so widening the loop turns an unrelated model change into a data migration on their tables.

## Renaming an enum member is a data migration

Because members are stored by name, renaming one changes no schema and produces an **empty migration** —
while every existing row still holds the old string, and the first read of such a row throws on conversion.
Write the `UPDATE` by hand in the generated migration body, and scope it to the owning table: several tables
have a `Visibility` column and each uses a different enum (D-266 renamed `EventVisibility.Public`→`Listed`,
touching `events` only).

Prefer renames that stay reversible. Merging two live members into one (the planned
`Private`→`InviteOnly`) is a one-way door: `Down()` cannot tell the original rows apart. That is a reason
to sequence it deliberately, not to do it opportunistically alongside a rename.

## A state CHECK constraint is DERIVED from the enum, and constrains vocabulary only (D-328)

Four columns carry one — `orders."Status"`, `tickets."State"`, `events."Status"`, `ledger_entries."State"`.
Because the section above stores enums as text, the column will otherwise accept **any string**. The guard
is worthless against C# (the converter cannot express an invalid value); it exists for the write paths
nobody reviews — a migration, a psql session, an `ExecuteUpdateAsync` with a typo'd literal.

**Never transcribe the member list.** Use the helper at the foot of `KurxDbContext`:

```csharp
e.ToTable("orders", t => t.HasCheckConstraint("ck_orders_status", StateVocabulary<OrderStatus>("Status")));
```

A hand-written `IN (...)` is a second declaration of the enum, and the two drift the first time a member is
added — at which point the constraint rejects a value the application has already started writing, in
production, on the one path that reaches the new member. That is strictly worse than no constraint.

**Vocabulary, never transitions or authorization.** A CHECK answers *may this value exist*. `Draft →
PendingReview` is `EventStatusWorkflow`'s job and *who may transition* is `IEventAuthority`'s; neither
belongs in the schema. `StateVocabularyConstraintTests` asserts this by walking each enum in declaration
order — an order reaching `Refunded` having never been `Paid` must be **accepted**.

Two things to expect when adding one:

- **`ADD CONSTRAINT ... CHECK` validates every existing row**, so a legacy value fails the migration and
  stops the deploy. That is correct, not a bug — but it means you must query the live distinct values
  *before* writing the migration, and resolve any stragglers in their own data migration first (as
  `MigrateInReviewToPendingReview` did for `InReview`). Never map an unknown value to a guess.
- **Test against a populated table, not an empty one.** Applying to a fresh database proves nothing about
  the upgrade path; `StateVocabularyConstraintTests` re-applies the migration's own SQL over seeded rows and
  over a deliberately invalid one, asserting `23514` on the `ALTER TABLE`.

Not every status column gets one. ~80 exist; four are constrained, because those are the ones where an
invalid value would be both silent and expensive.

## `dotnet ef migrations add` diffs against `KurxDbContextModelSnapshot`, not against the migration files

So a snapshot that is behind the migrations on disk silently produces a migration that re-adds columns
those migrations already create. This bites hardest with **untracked** migrations from a parallel session:
`git checkout -- KurxDbContextModelSnapshot.cs` looks like a clean revert and instead rewinds the baseline
past four other migrations. If the snapshot must be rebased, take the last migration's `*.Designer.cs`
`BuildTargetModel` body — it *is* the model at that point — and re-wrap it as `BuildModel` in the
`KurxDbContextModelSnapshot : ModelSnapshot` shell. Then regenerate and **read the generated `Up()`**: an
M1-sized change should be a dozen lines, so anything sweeping in unrelated tables means a wrong baseline.

## Phones: two columns, and only one of them is safe to re-normalize (D-089 · D-290)

`users` carries both the legacy `Phone` (bare digits, country code **without** the '+') and the canonical
`PhoneE164` (`+6591234567`). Every write goes through `AuthService.ApplyCanonicalPhone`, so they never drift;
a null in the canonical column means "not backfilled yet", never "no phone".

**Read `u.PhoneE164 ?? u.Phone`. Never `u.Phone` alone.** Passing the legacy column back through
`AuthService.NormalizePhone` *changes the number's country*: it has no '+', so it is re-read in the legacy
`IN` region, and plenty of foreign national numbers are also well-formed Indian mobiles — a Singapore number
stored as `6591234567` comes back `916591234567`. This has already shipped two live defects (an
international owner unable to claim their own ticket transfer; a blacklist that **failed open** for
international numbers), so treat it as a rule, not a preference.

Applies to three situations: comparing a stored phone against a freshly normalized one, re-normalizing a
stored phone at all, and projecting a phone to a client (exports and admin views must carry the country
code). User *input* going through `NormalizePhone` is fine — clients send E.164.

When two phone values of different provenance must be compared, put both through
`PhoneCanonicalizer.ToE164OrUnchanged` first.
