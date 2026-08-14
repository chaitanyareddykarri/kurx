namespace Kurx.Application.Abstractions;

/// <summary>Inventory pools (V3 §8). <b>Authoritative from the Phase 9 cut-over (§17.1):</b> <c>Consumed</c>/
/// <c>Held</c> are owned by the conditional-decrement contract, never a mirror of the legacy <c>TicketType.Sold</c>
/// (which stays as a written legacy field). Every capacity change is a conditional UPDATE (never read-then-write),
/// pools are locked in ascending id order, and reconciliation proves <c>Consumed == count(active admissions)</c>.
/// Management authz reuses the Phase-6 event-permission union — no parallel model.</summary>
public interface IInventoryService
{
    /// <summary>Ensure the ticket type's general pool exists and set its <c>Total</c> to <paramref name="total"/>.
    /// On first materialisation (pool missing) it also seeds the authoritative <c>Consumed</c> from
    /// <paramref name="consumed"/> (backfill/create); on an existing pool it updates <b>only Total</b> and never
    /// touches the authoritative Consumed/Held. Operates on the caller's DbContext (added/updated, not saved).</summary>
    Task SyncPoolAsync(Guid ticketTypeId, Guid eventId, int total, int consumed, CancellationToken ct = default);

    /// <summary>The ticket type's general pool id (for re-pointing the waitlist), materialising nothing.</summary>
    Task<Guid?> GeneralPoolIdAsync(Guid ticketTypeId, CancellationToken ct = default);

    /// <summary>The ticket type's general pool id, <b>creating it if missing</b> (Total = <paramref name="total"/>,
    /// Consumed seeded from <paramref name="consumed"/>). The money path calls this so a ticket type that skipped
    /// pool creation (legacy/seeded data) still sells against an authoritative pool rather than hard-failing.
    /// Committed immediately (its own SaveChanges) — safe to call before opening the money transaction.</summary>
    Task<Guid> EnsureGeneralPoolAsync(Guid ticketTypeId, Guid eventId, int total, int consumed, CancellationToken ct = default);

    /// <summary>The <b>authoritative</b> remaining availability of a ticket type's general pool
    /// (<c>Total + oversell_allowance − Consumed − Held</c>, floored at 0) — the single source every availability
    /// read must use from Phase 9, never the legacy <c>TicketType.Sold</c> mirror. Falls back to the scalar only
    /// when no pool exists yet (a directly-seeded type before its first sale).</summary>
    Task<int> AvailableAsync(Guid ticketTypeId, CancellationToken ct = default);

    /// <summary>Batched authoritative <see cref="PoolCounts"/> (Sold = Consumed + Held; Available) per ticket type,
    /// so list/summary reads report the pool authority without an N+1. Types without a pool fall back to the scalar.</summary>
    Task<IReadOnlyDictionary<Guid, PoolCounts>> PoolCountsAsync(IReadOnlyList<Guid> ticketTypeIds, CancellationToken ct = default);

    // ── V3 §17.1 conditional-decrement contract (Phase 9). All operate on the caller's DbContext so they enlist
    //    in the caller's explicit money transaction; a false return means the caller must roll it back. ──────────

    /// <summary>Conditionally consume <c>n</c> from each pool, locking pools in <b>ascending id order</b> (§17.1
    /// deadlock-free). Each decrement is a single conditional UPDATE — <c>consumed = consumed + n WHERE
    /// consumed + held + n &lt;= total + oversell_allowance</c> — so it never oversells and never read-then-writes.
    /// Returns false (no capacity) on the first pool that can't take the draw; the caller rolls back.</summary>
    Task<bool> TryConsumeManyAsync(IReadOnlyList<PoolDraw> draws, CancellationToken ct = default);

    /// <summary>Reserve-first (§17.1): conditionally hold <c>n</c> in each pool (ascending id order), same capacity
    /// guard as consume but incrementing <c>held</c>. Returns false if any pool can't take the hold.</summary>
    Task<bool> TryHoldManyAsync(IReadOnlyList<PoolDraw> draws, CancellationToken ct = default);

    /// <summary>Capture: convert a hold to a consumption on one pool (<c>held -= n, consumed += n</c>). The hold
    /// already reserved the capacity, so this is unconditional beyond a non-negativity guard.</summary>
    Task ConvertHoldToConsumedAsync(Guid poolId, int n, CancellationToken ct = default);

    /// <summary>Release a hold (expiry/abandon): <c>held -= n</c>, guarded non-negative.</summary>
    Task ReleaseHoldAsync(Guid poolId, int n, CancellationToken ct = default);

    /// <summary>Return consumed inventory to stock on refund: <c>consumed -= n</c>, guarded non-negative.</summary>
    Task ReleaseConsumedAsync(Guid poolId, int n, CancellationToken ct = default);

    /// <summary>Consume <c>n</c> unconditionally (<c>consumed += n</c>, no capacity guard). Used only when a captured
    /// payment must be honoured after its hold already expired (§9 — a paid ticket is valid): the seat is recorded
    /// even if it pushes Consumed past Total, so <c>Consumed == active admissions</c> always holds. New sales are
    /// then blocked by the conditional decrement, so this never causes fresh oversell.</summary>
    Task ConsumeUnconditionalAsync(Guid poolId, int n, CancellationToken ct = default);

    /// <summary>Self-heal (review P4): set each drifting pool's <c>Consumed</c> to its authoritative count of active
    /// admissions. Idempotent, never oversells (it records the true admission count, which the conditional decrement
    /// already bounded), and preserves the authority (admissions are the ground truth). Returns the pools repaired.</summary>
    Task<int> RepairAsync(Guid? eventId = null, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<InventoryPoolView>>> GetForEventAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<InventoryPoolView>> SetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, InventoryPolicyInput input, CancellationToken ct = default);

    /// <summary>§17.1 reconciliation, <b>Phase-9 authority form</b>: proves each pool's authoritative
    /// <c>Consumed</c> equals the count of active admissions drawing on it (<c>Consumed == count(admissions where
    /// state != Void, pool_id = pool)</c>) and returns only the rows that drift. Held-but-uncaptured seats sit in
    /// <c>Held</c> (not Consumed) and have no admission yet, so they don't false-alarm.</summary>
    Task<IReadOnlyList<InventoryDrift>> ReconcileAsync(Guid? eventId = null, CancellationToken ct = default);

    /// <summary>One-time, idempotent backfill: a general pool per existing ticket type (Total=Quantity,
    /// Consumed=Sold), plus re-pointing existing waitlist rows onto those pools. Runs at startup.</summary>
    Task<int> BackfillPoolsAsync(CancellationToken ct = default);
}

public record InventoryPolicyInput(int? OversellAllowance, string? NoShowPolicy, int? NoShowReleaseMinutes,
    string? ReleasePolicyJson, string? WaitlistConfigJson);

public record InventoryPoolView(Guid Id, Guid EventId, Guid? TicketTypeId, string Scope, string Segment,
    string Channel, string Unit, int Total, int Held, int Allocated, int Consumed, int Available,
    int OversellAllowance, string NoShowPolicy);

/// <summary>A draw against a pool: consume/hold <see cref="N"/> units of <see cref="PoolId"/> (§17.1).</summary>
public record PoolDraw(Guid PoolId, int N);

/// <summary>Authoritative counts for a ticket type's general pool: <see cref="Sold"/> = Consumed + Held (the legacy
/// mirror's meaning, now read from the pool), <see cref="Available"/> = Total + oversell − Consumed − Held.</summary>
public record PoolCounts(int Sold, int Available);

/// <summary>A pool whose authoritative <see cref="Consumed"/> disagrees with <see cref="ActiveAdmissions"/>.</summary>
public record InventoryDrift(Guid PoolId, Guid EventId, Guid? TicketTypeId, int Consumed, int ActiveAdmissions);
