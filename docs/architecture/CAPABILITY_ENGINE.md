# Capability Engine (D-266 M2)

> **The capability engine resolves capabilities. It performs no authorization.**
>
> It answers *"what does this event support?"* — never *"who may do this?"*. Authority, ownership,
> permissions and roles are D-269's concern and are never consulted here. The engine takes an archetype, a
> product, a mode and the persisted matrix, and returns capability rules. No user id ever reaches it.

## What a capability is

A capability is **event behaviour whose availability can differ between archetypes**. That is the whole
test. If the answer to *"would two archetypes reasonably differ here?"* is no, it is not a capability —
it belongs to the subsystem that owns it.

The catalog is **30 slugs**, exactly the modules of the D12 matrix.

## What a capability is not

M2 reduced the catalog from 57 to 30. The other 27 were not event behaviour: they were Registration,
Ticketing, Invitation, Scheduling, Eligibility, Finance or Infrastructure concerns carrying a capability
flag, and each already had a second source of truth in its own subsystem.

| Layer | Owns | Examples |
|---|---|---|
| **Infrastructure** | always-on platform services | `registration`, `reminders`, `audit` |
| **Registration** | who may register and how | waitlist, quotas, delegated, walk-in, slots |
| **Invitations (D9)** | invitation model | invitations, guest list |
| **Ticketing** | products, seating, inventory | seat map, passes |
| **Scheduling** | event structure | multi-day, multi-venue, series |
| **Eligibility** | who qualifies | age (D-265 `MinAge`/`MaxAge`) |
| **Finance** | money | donations |
| **Safety & Logistics** *(not yet built)* | operational requirements | travel, medical, waiver, route timing |

`officials` became participant roles on users; `interviews` moved to Recruitment; `stages`, `prizes` fold
into judging configuration; `exhibitors` into `booths`; `lineup` into `agenda`.

## Resolution pipeline

Fixed order. Every capability comes out of it; there is no special case for any individual slug.

```
Product  →  Archetype  →  Capability Matrix  →  Dependency DAG  →  Defaults  →  Final set
```

1. **Product** — a Private product can never take payment (D13 §0), so `paid` and `finance` are
   Unsupported before the matrix is consulted.
2. **Archetype** — no archetype resolves nothing. The engine never falls back to legacy Kind logic.
3. **Matrix** — `Required` / `Optional` / **absent means `Unsupported`**, never "off".
4. **DAG** — enabling pulls dependencies in; a forbidden dependency takes the dependent with it, so
   nothing is ever left half-enabled. `finance → paid` is why "Private cannot have Finance" falls out of
   the graph instead of being coded.
5. **Defaults** — formerly `Universal = true`. Convenience only: a default may enable a capability the
   archetype marks Optional, and may **never** enable one it marks Unsupported.

**Precedence:** Unsupported always wins. Required always wins. Defaults never override the matrix.

### Rule vs state

| Type | Meaning |
|---|---|
| `CapabilityRule` (Unsupported/Optional/Required) | what the archetype **permits** |
| `CapabilityState` (Required/On/Off/Locked) | what an event **has** |

Unsupported surfaces as `Locked`, not `Off`. `Off` invites a client to render a toggle the backend would
refuse; `Locked` says "not available here". `Locked` capabilities are never materialized.

## Storage

`archetype_capability_defaults` — **420 rows (14 archetypes × 30 capabilities)**, every cell stored
*including the negatives*. That completeness is what makes "a Workshop may never enable Leaderboard"
expressible; the retired Kind table stored only positive cells and read absence as "available but off".

Seeded from the D12 transcription, then admin-owned (D-188). The seeder re-syncs drift and deletes orphans.

## API

| Route | Returns |
|---|---|
| `GET /v1/capabilities` | the 30-slug registry |
| `GET /v1/archetypes/{slug}/capabilities?mode=` | what an archetype supports, no event needed |
| `GET /v1/events/{id}/capabilities` | the fully resolved set for one event |

**Clients render capabilities; they never decide them.** A hardcoded event-type name in `web/` or
`mobile/` is a defect.

## Workspace composition

The organiser workspace is **not** generated from the capability catalog. Every layer contributes its own
surfaces via `IWorkspaceContributor`, and `WorkspaceComposer` merges them.

This exists because it was got wrong first: when `registration` stopped being a capability, the
Registrations tab vanished — a subsystem had been pretending to be an event capability just to show a page.

Each `WorkspaceSurface` declares `TabId · DisplayName · Order · Owner · DependsOn`. There is
deliberately **no route** (D-310): web routes management at `/host/events/{id}/{tab}` and Flutter at
`/events/{id}/manage/{tab}`, so one server-side string could not be right for both. The server says
which surfaces exist; resolving a `TabId` to a URL is the client's concern. Composition is
deterministic **by design**: every surface must declare a **unique `Order`**, and a collision throws at
startup with both contributors named. There is no alphabetical fallback — ordering must not depend on what
something happens to be called. Duplicate tab ids, unresolvable dependencies and dependency cycles all fail
startup with a diagnostic rather than silently dropping a surface.

Capability tabs occupy the 1000+ band; subsystem tabs sit below.

## Retired

`EventKind`, `KindCapabilityDefault`, `KindDefaults`, `GetForKindAsync`, `/v1/kinds/{slug}/capabilities`
and the name-matched `KindAlias` chain D-188 flagged as fragile. `EventKind` is archived, not dropped —
existing events reference it.
