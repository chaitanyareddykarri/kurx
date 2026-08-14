using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Ticketing;

/// <summary>Inventory pools (V3 §8, Phase 7). Additive dual-write shadow: <see cref="SyncPoolAsync"/> keeps a
/// general pool per TicketType in lock-step with the scalar Quantity/Sold, committed atomically with the
/// scalar change it mirrors. The scalar stays the oversell authority this phase (§17.1 conditional-decrement
/// authority is Phase 9). Management authz reuses the Phase-6 event-permission union — no parallel model.</summary>
public class InventoryService(KurxDbContext db, IEventPermissionService permissions) : IInventoryService
{
    public async Task SyncPoolAsync(Guid ticketTypeId, Guid eventId, int total, int consumed, CancellationToken ct = default)
    {
        var pool = await db.InventoryPools
            .FirstOrDefaultAsync(p => p.TicketTypeId == ticketTypeId && p.Segment == InventorySegment.General, ct);
        if (pool is null)
        {
            // First materialisation (create/backfill): seed the authoritative Consumed from the legacy scalar.
            // Unreachable as a race in normal operation — a new ticket type mints its pool in the SAME
            // transaction (TicketTypeService) and existing types are backfilled at startup before serving.
            pool = new InventoryPool { EventId = eventId, TicketTypeId = ticketTypeId, Total = total, Consumed = consumed };
            db.InventoryPools.Add(pool);
            return;
        }
        // Existing pool (Phase 9): Total tracks the ticket type's configured Quantity, but Consumed/Held are
        // AUTHORITATIVE — owned by the conditional decrement, never overwritten by a mirror of the legacy scalar.
        pool.Total = total;
        pool.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<int> AvailableAsync(Guid ticketTypeId, CancellationToken ct = default)
    {
        var pool = await db.InventoryPools.AsNoTracking()
            .Where(p => p.TicketTypeId == ticketTypeId && p.Segment == InventorySegment.General)
            .Select(p => new { p.Total, p.OversellAllowance, p.Consumed, p.Held }).FirstOrDefaultAsync(ct);
        if (pool is not null) return Math.Max(0, pool.Total + pool.OversellAllowance - pool.Consumed - pool.Held);
        // No pool yet (directly-seeded type, pre-first-sale): the scalar is the only source and equals the authority.
        var tt = await db.TicketTypes.AsNoTracking().Where(t => t.Id == ticketTypeId)
            .Select(t => new { t.Quantity, t.Sold }).FirstOrDefaultAsync(ct);
        return tt is null ? 0 : Math.Max(0, tt.Quantity - tt.Sold);
    }

    public async Task<IReadOnlyDictionary<Guid, PoolCounts>> PoolCountsAsync(IReadOnlyList<Guid> ticketTypeIds, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, PoolCounts>();
        if (ticketTypeIds.Count == 0) return result;
        var pools = await db.InventoryPools.AsNoTracking()
            .Where(p => p.TicketTypeId != null && ticketTypeIds.Contains(p.TicketTypeId!.Value) && p.Segment == InventorySegment.General)
            .Select(p => new { TtId = p.TicketTypeId!.Value, p.Total, p.OversellAllowance, p.Consumed, p.Held }).ToListAsync(ct);
        foreach (var p in pools)
            result[p.TtId] = new PoolCounts(p.Consumed + p.Held, Math.Max(0, p.Total + p.OversellAllowance - p.Consumed - p.Held));
        // Fall back to the scalar for any type that has no pool yet.
        var missing = ticketTypeIds.Where(id => !result.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            foreach (var t in await db.TicketTypes.AsNoTracking().Where(t => missing.Contains(t.Id))
                .Select(t => new { t.Id, t.Quantity, t.Sold }).ToListAsync(ct))
                result[t.Id] = new PoolCounts(t.Sold, Math.Max(0, t.Quantity - t.Sold));
        return result;
    }

    // ── V3 §17.1 conditional-decrement contract (Phase 9) ────────────────────────
    public async Task<bool> TryConsumeManyAsync(IReadOnlyList<PoolDraw> draws, CancellationToken ct = default)
    {
        // Deterministic lock order (§17.1): always ascending pool id — this is what prevents deadlock between a
        // parent-first and a child-first path. Conditional decrement: the WHERE proves capacity atomically under
        // the row lock, so two racing consumes on the last seat cannot both succeed.
        foreach (var draw in draws.OrderBy(d => d.PoolId))
        {
            var poolId = draw.PoolId; var n = draw.N;
            var affected = await db.InventoryPools
                .Where(p => p.Id == poolId && p.Consumed + p.Held + n <= p.Total + p.OversellAllowance)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Consumed, p => p.Consumed + n)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
            if (affected == 0) return false;   // would breach capacity — caller rolls back the whole transaction
        }
        return true;
    }

    public async Task<bool> TryHoldManyAsync(IReadOnlyList<PoolDraw> draws, CancellationToken ct = default)
    {
        foreach (var draw in draws.OrderBy(d => d.PoolId))
        {
            var poolId = draw.PoolId; var n = draw.N;
            var affected = await db.InventoryPools
                .Where(p => p.Id == poolId && p.Consumed + p.Held + n <= p.Total + p.OversellAllowance)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Held, p => p.Held + n)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
            if (affected == 0) return false;
        }
        return true;
    }

    public Task ConvertHoldToConsumedAsync(Guid poolId, int n, CancellationToken ct = default)
        => db.InventoryPools.Where(p => p.Id == poolId && p.Held >= n)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Held, p => p.Held - n)
                .SetProperty(p => p.Consumed, p => p.Consumed + n)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

    public Task ReleaseHoldAsync(Guid poolId, int n, CancellationToken ct = default)
        => db.InventoryPools.Where(p => p.Id == poolId && p.Held >= n)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Held, p => p.Held - n)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

    public Task ReleaseConsumedAsync(Guid poolId, int n, CancellationToken ct = default)
        => db.InventoryPools.Where(p => p.Id == poolId && p.Consumed >= n)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Consumed, p => p.Consumed - n)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

    public Task ConsumeUnconditionalAsync(Guid poolId, int n, CancellationToken ct = default)
        => db.InventoryPools.Where(p => p.Id == poolId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Consumed, p => p.Consumed + n)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

    public async Task<int> RepairAsync(Guid? eventId = null, CancellationToken ct = default)
    {
        // Ground truth = active admissions (each is a real consumed seat, created atomically with its decrement).
        // Set every drifting pool's Consumed to that count. Idempotent; a re-run over an in-sync pool changes nothing.
        var drift = await ReconcileAsync(eventId, ct);
        foreach (var d in drift)
            await db.InventoryPools.Where(p => p.Id == d.PoolId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Consumed, d.ActiveAdmissions)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
        return drift.Count;
    }

    public async Task<Guid?> GeneralPoolIdAsync(Guid ticketTypeId, CancellationToken ct = default)
        => await db.InventoryPools.AsNoTracking()
            .Where(p => p.TicketTypeId == ticketTypeId && p.Segment == InventorySegment.General)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);

    public async Task<Guid> EnsureGeneralPoolAsync(Guid ticketTypeId, Guid eventId, int total, int consumed, CancellationToken ct = default)
    {
        var existing = await db.InventoryPools.AsNoTracking()
            .Where(p => p.TicketTypeId == ticketTypeId && p.Segment == InventorySegment.General)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        if (existing is { } id) return id;
        // Self-heal a ticket type that never got a pool (legacy/directly-seeded). Seed Consumed from the scalar.
        var pool = new InventoryPool { EventId = eventId, TicketTypeId = ticketTypeId, Total = total, Consumed = consumed };
        db.InventoryPools.Add(pool);
        await db.SaveChangesAsync(ct);
        return pool.Id;
    }

    public async Task<ServiceResult<IReadOnlyList<InventoryPoolView>>> GetForEventAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        if (!isAdmin && !await permissions.HasAsync(actorId, eventId, "event:manage", ct))
            return ServiceResult<IReadOnlyList<InventoryPoolView>>.Fail("forbidden");
        var pools = await db.InventoryPools.AsNoTracking().Where(p => p.EventId == eventId).ToListAsync(ct);
        IReadOnlyList<InventoryPoolView> views = pools.Select(ToView).ToList();
        return ServiceResult<IReadOnlyList<InventoryPoolView>>.Success(views);
    }

    public async Task<ServiceResult<InventoryPoolView>> SetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, InventoryPolicyInput input, CancellationToken ct = default)
    {
        if (!isAdmin && !await permissions.HasAsync(actorId, eventId, "event:manage", ct))
            return ServiceResult<InventoryPoolView>.Fail("forbidden");

        var pool = await db.InventoryPools.FirstOrDefaultAsync(p => p.EventId == eventId
            && p.TicketTypeId == ticketTypeId && p.Segment == InventorySegment.General, ct);
        if (pool is null) return ServiceResult<InventoryPoolView>.Fail("not_found");

        if (input.OversellAllowance is < 0) return ServiceResult<InventoryPoolView>.Fail("invalid_oversell");
        if (input.NoShowReleaseMinutes is < 0) return ServiceResult<InventoryPoolView>.Fail("invalid_release_minutes");
        var noShow = pool.NoShowPolicy;
        if (input.NoShowPolicy is not null && !Enum.TryParse(input.NoShowPolicy, ignoreCase: true, out noShow))
            return ServiceResult<InventoryPoolView>.Fail("invalid_no_show_policy");

        if (input.OversellAllowance is { } o) pool.OversellAllowance = o;
        pool.NoShowPolicy = noShow;
        if (input.NoShowReleaseMinutes is { } m) pool.NoShowReleaseMinutes = m;
        if (input.ReleasePolicyJson is not null) pool.ReleasePolicyJson = input.ReleasePolicyJson;
        if (input.WaitlistConfigJson is not null) pool.WaitlistConfigJson = input.WaitlistConfigJson;
        pool.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog { ActorType = "user", ActorId = actorId, Action = "inventory.set_policy", Entity = "inventory_pools", EntityId = pool.Id });
        await db.SaveChangesAsync(ct);
        return ServiceResult<InventoryPoolView>.Success(ToView(pool));
    }

    public async Task<IReadOnlyList<InventoryDrift>> ReconcileAsync(Guid? eventId = null, CancellationToken ct = default)
    {
        var pools = await db.InventoryPools.AsNoTracking()
            .Where(p => p.TicketTypeId != null && (eventId == null || p.EventId == eventId))
            .Select(p => new { p.Id, p.EventId, p.TicketTypeId, p.Consumed }).ToListAsync(ct);
        if (pools.Count == 0) return [];

        // Phase-9 authority invariant (§17.1): the pool's authoritative Consumed must equal the number of active
        // admissions drawing on it. Held-but-uncaptured seats sit in Held (not Consumed) and have no admission
        // yet, so they don't false-alarm. One grouped read for all pools; no N+1.
        var poolIds = pools.Select(p => p.Id).ToList();
        var activeByPool = (await db.Admissions.AsNoTracking()
            .Where(a => a.PoolId != null && poolIds.Contains(a.PoolId!.Value) && a.State != AdmissionState.Void)
            .GroupBy(a => a.PoolId!.Value)
            .Select(g => new { PoolId = g.Key, Count = g.Count() })
            .ToListAsync(ct)).ToDictionary(x => x.PoolId, x => x.Count);

        var drift = new List<InventoryDrift>();
        foreach (var p in pools)
        {
            var active = activeByPool.GetValueOrDefault(p.Id, 0);
            if (p.Consumed != active) drift.Add(new InventoryDrift(p.Id, p.EventId, p.TicketTypeId, p.Consumed, active));
        }
        return drift;
    }

    public async Task<int> BackfillPoolsAsync(CancellationToken ct = default)
    {
        var ticketTypes = await db.TicketTypes.AsNoTracking()
            .Select(t => new { t.Id, t.EventId, t.Quantity, t.Sold }).ToListAsync(ct);

        var existing = (await db.InventoryPools.AsNoTracking()
                .Where(p => p.TicketTypeId != null && p.Segment == InventorySegment.General)
                .Select(p => p.TicketTypeId!.Value).ToListAsync(ct)).ToHashSet();

        var created = 0;
        foreach (var tt in ticketTypes)
        {
            if (!existing.Add(tt.Id)) continue;
            db.InventoryPools.Add(new InventoryPool { EventId = tt.EventId, TicketTypeId = tt.Id, Total = tt.Quantity, Consumed = tt.Sold });
            created++;
        }
        if (created > 0) await db.SaveChangesAsync(ct);

        // Re-point any waitlist rows onto their ticket type's general pool (§8.5). Skipped once all are pointed.
        if (await db.TicketWaitlists.AnyAsync(w => w.PoolId == null, ct))
        {
            var poolByType = await db.InventoryPools.AsNoTracking()
                .Where(p => p.TicketTypeId != null && p.Segment == InventorySegment.General)
                .ToDictionaryAsync(p => p.TicketTypeId!.Value, p => p.Id, ct);
            foreach (var (ttId, poolId) in poolByType)
                await db.TicketWaitlists.Where(w => w.TicketTypeId == ttId && w.PoolId == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(w => w.PoolId, poolId), ct);
        }
        return created;
    }

    private static InventoryPoolView ToView(InventoryPool p) => new(
        p.Id, p.EventId, p.TicketTypeId, p.Scope.ToString(), p.Segment.ToString(), p.Channel.ToString(), p.Unit.ToString(),
        p.Total, p.Held, p.Allocated, p.Consumed, Math.Max(0, p.Total + p.OversellAllowance - p.Consumed - p.Held),
        p.OversellAllowance, p.NoShowPolicy.ToString());
}
