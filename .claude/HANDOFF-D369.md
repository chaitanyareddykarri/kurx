# D-369 — what it is, and the one thing not to do to it

**Written:** 2026-08-17 · **Status: fully committed and verified.** Nothing is owed here.
This exists so the next person meets D-369 deliberately instead of discovering it in a diff.

---

## The one live warning: do NOT renumber D-369

`1f6eea5` renumbered `D-354..D-362 → D-370..D-377`. **D-369 is outside that range and must keep its
number.** It is referenced by name in committed source comments (`WalkInService.cs`,
`SeatBlockService.cs`), in `CHANGELOG.md`, and in `docs/api/README.md`. Renumbering it silently breaks
all four. Checked: D-369 sits at `DECISIONS.md:14780`; D-370 begins at `:14860` — no overlap.

## What it fixed

D-366 made `TicketType.PricePaise` **derived** — the cheapest price band. It updated `AmountFor`,
`CloneAsync` and every client, but never grepped the field. Two services still built an `OrderItem`
straight off the column:

- `WalkInService.CreateAsync` — the gate charged the cheapest band for **any** team size
- `SeatBlockService.CreateAsync` — the same shortfall, invoiced to an organisation under `DEFERRED`

A team owing ₹400 was admitted for ₹250, silently: the order, ticket and admission were all internally
consistent, and nothing compared them to the band that should have applied.

The money was the symptom. Neither channel can express a team **at all** — no team size, no roster, N
individual seats — so both now refuse `RegistrationMode.Group` with `group_ticket_not_supported`,
before the pool draw and before any mutation. Guard is on `Group`, not on "has bands", so the unbanded
sibling (a phantom team with an empty roster) closes with it. No schema change, no migration, no data
change. Neither endpoint has a client caller, so nothing shipped breaks.

**Where it landed:** the code, test and CHANGELOG entry went in with `590a25e`
(*"feat(events): lifecycle, soft-delete, and team-size pricing"* — a message that does not mention it,
worth knowing if you write release notes off it). The decision entry and API note followed via
`1f6eea5`/`ad54b14`.

## Verification — all of it, so nobody repeats it

| Gate | Result |
|---|---|
| Backend full suite | **1932 pass / 0 fail / 1 skip** (was 1912) · 17m26s · `-warnaserror` clean |
| D-369 test, by name | `Neither_a_walk_in_nor_a_seat_block_can_sell_a_team_ticket` **Passed [203 ms]** |
| D-369 live, unbanded team ticket | 9/9 — both refusals, both individual-ticket controls still 200, nothing created |
| D-369 live, **genuinely banded** ticket | 3/3 — refused at the gate and for a `DEFERRED` block, 0 orders |
| D-366 band validation (set checks) | 4/4 — overlap, gap, out-of-range, non-Group all refused |
| D-366 derived headline | client sent ₹9,999 with bands from ₹250 → server stored **₹250** |
| D-366 overlap is a DB guarantee | direct SQL INSERT rejected by `ex_ticket_price_tiers_no_overlap`; `btree_gist` present |
| D-367 E2E over HTTP | 6/6 |
| D-368 live | dashboard / analytics / orgs list all report **1** (was 3 vs 1) |
| Contract | live spec ⇄ committed identical — 441 paths, 442 schemas |
| contract-check | 36 client models, 0 errors · self-test 13/13 |
| web / admin / flutter | 646 +1 skip · 40 · 498 · `flutter analyze` clean |

The controls matter more than the refusals: a guard that refused *everything* would pass every negative
test. The individual-ticket 200s are what prove it isn't too broad.

## Left open on purpose — do not "fix" without a decision

`ApprovalService`'s `IfBudgetGt` reads `MAX(PricePaise × Quantity)`. On a banded ticket that is the
cheapest band, so it **understates** an event's ceiling and could skip an approval step. Left alone and
named in D-369 §"One more reader": no seed, no workflow and no test invokes it, so fixing it would be
building on scaffolding (D-018).

Every other reader of the column is a boolean (`PricePaise > 0` in `IsPaidEventAsync`, the discovery
filter, `AnalyticsFactSource`, `IfPaid`) and stays correct — a paid ticket's cheapest band is still > 0.

## Dev-environment notes

- The stack runs a **rebuilt** backend image (`9f01fb73fc31`) that includes D-369.
- Verification created 2 orgs and 1 event; both were **soft-deleted** afterwards (`DeletedAt`, the
  platform's own convention) and the admin org count is back to **1**, re-verified live.
- Not cleaned up: 4 orders / 2 seat blocks on `paid-flow-probe`'s ticket, minted by the D-369 *control*
  checks that correctly succeeded. Removing an order means unwinding
  `order → order_items → tickets → registrations → admissions → credentials` plus reconciling pool
  `Consumed` counters — deliberately left rather than done half-right.
- An orphaned `dotnet/sdk` container ran at ~1300% CPU for four hours (a `docker run` whose client was
  killed; `--rm` only cleans up on normal exit). Killed. **If you kill a suite run, check
  `docker ps` afterwards.**

## The transferable lesson

**Changing what a field means is only finished when every reader has been visited.** D-366 verified
`AmountFor` and all three clients and still shipped a money bug, because it never ran
`grep -r PricePaise`. Two sibling callers were four files away.
