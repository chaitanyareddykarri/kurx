# Performance Rules

Enforceable performance baseline. The public discovery API is the hot surface — treat it as the default worst case. Review gate: `.claude/reviews/performance-review.md`; checklist: `.claude/checklists/performance.md`.

## Queries

- **Every list endpoint is paginated.** No unbounded result set reaches a client — the discovery endpoints (search/upcoming/trending/featured/latest/related) all paginate; new list endpoints follow suit.
- **No N+1.** Load related data with a single projection/`Include`, not a query per row. EF can't translate `OrderBy` on record-constructor projections — project to an anonymous type, order+materialize, then map to the record (`database-conventions`). This limitation is a common source of accidental client-side evaluation; watch for it.
- **Filter/sort columns are index-backed.** A new `WHERE`/`ORDER BY` column on a table that grows (events, orders, tickets) ships with an index in the same migration. `City` is denormalized onto `Event` precisely to avoid a join on search (D-018) — prefer a measured denormalization over a hot-path join.
- **Read only what's returned.** Don't `SELECT *` an entity to return three fields; project.
- **Every paginated `ORDER BY` is a TOTAL order (D-325).** `ORDER BY CreatedAt` alone is not one: `DateTime.UtcNow`
  is coarser than a bulk write, so group ticket issuance, announcement fan-out, certificate generation, CSV
  invitation import and ledger settlement all produce rows sharing an instant. The database may then break
  those ties differently per query — repeating a row on one page and dropping another. Add a unique
  tie-breaker (`ThenByDescending(x => x.Id)`, or whatever key is genuinely unique for that projection).
  Applies to in-memory sorts over merged sources too, where "stable sort" only preserves an input order that
  was itself untied.
- **An index is justified by a query plan, not by a column appearing in `WHERE` (D-325).** A GIN trigram
  index on `events.Title` was proposed, built and measured against 200k rows: the plan did **not change**,
  because the predicate is `ILIKE … OR org.Name ILIKE …` and an `OR` spanning two tables is evaluated after
  the join. Same predicate as a single column: 138 ms → 1.8 ms. The lesson is the general one — measure the
  actual predicate shape before adding an index, and prefer fixing the query when the shape is what blocks it.

## Large lists and exports

- **An export that scales with the data streams; one that is server-capped need not (D-325).** Of Kurx's
  three CSV exports only the attendee roster was unbounded — admin events is capped at 1,000 rows and
  analytics is bounded by day-rows. The roster now writes through a delegate in 500-row keyset batches, so
  peak memory is a function of the batch size rather than the event's size.
- **Authorize before the first byte.** A streaming endpoint decides its status line up front: once the body
  starts there is no taking a 404 back. Resolve the gate eagerly and hand the endpoint a writer only on
  success.
- **Batch an export with keyset, not OFFSET.** A batched export is the one place deep paging is guaranteed
  rather than hypothetical — batch N of a 50k roster would otherwise carry `OFFSET 49500` and re-walk the
  index every batch.
- **Don't convert an interactive list to cursor pagination without checking the clients.** Reviews met every
  textbook keyset criterion and were still left on OFFSET, because no shipped client can request page 2
  (D-325). A cursor contract that no caller exercises is complexity, not performance.

## Request hot path

- **No synchronous external/IO call on a request path** that the response doesn't strictly need. Email/SMS/push/payout work belongs in background jobs (Hangfire, Phase 6) — not inline in an HTTP handler.
- **View counting** (trending signal, D-018) increments once per public detail fetch — keep it a single cheap write, not a read-modify-write with contention.
- Rate limits (D-005) are themselves a protection: OTP limits are enforced in Postgres so they survive restarts and can't be bypassed by hammering.

## Caching

- No response/query cache layer exists yet. Resource-role authorization is deliberately **not** cached — it's queried live per request for correctness (D-015). Don't add a cache to "fix" a perf problem you haven't measured; measure first, and if org-role lookups become hot, that's a design decision (`D-NNN`), not a silent cache.

## Frontend

- Server Components fetch on the server; don't waterfall client-side fetches for data a Server Component already has. Paginate/virtualize long organizer lists.
- Ship images through the `IStorage` presign path; don't inline large media.

## Rule of thumb

Correctness and the "smallest change" ethos (`coding-standards`) win over speculative optimization. Optimize a measured hot path, index a column you actually filter on, paginate every list — but don't build a cache or a read-replica story for load that doesn't exist yet.
