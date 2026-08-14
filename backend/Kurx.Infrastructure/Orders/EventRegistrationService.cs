using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Infrastructure.Orders;

/// <summary>Event registration → admission → credential shadow (V3 §7, Phase 8). Projects the V3-shaped chain
/// from the authoritative Order/Ticket, post-commit and idempotently, so it can never break the money path.
/// Order/Ticket stay authoritative; the cut-over is Phase 9. Management authz reuses the Phase-6
/// event-permission union.</summary>
public class EventRegistrationService(KurxDbContext db, IEventPermissionService permissions, IServiceScopeFactory scopeFactory) : IEventRegistrationService
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    // ── Policy ───────────────────────────────────────────────────────────────

    public async Task SyncPolicyAsync(Guid ticketTypeId, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.FindAsync([ticketTypeId], ct);   // tracked (pending) values if mid-transaction
        if (tt is null) return;

        var policy = await db.RegistrationPolicies.FirstOrDefaultAsync(p => p.TicketTypeId == ticketTypeId, ct);
        var isNew = policy is null;
        policy ??= new RegistrationPolicy { EventId = tt.EventId, TicketTypeId = ticketTypeId };

        // Windows always mirror the sale window; the axes are derived once and thereafter organiser-owned.
        policy.OpensAt = tt.SaleStarts;
        policy.ClosesAt = tt.SaleEnds;
        if (isNew)
        {
            policy.Subject = RegistrationSubject.Person;   // a party/group booking is a Person registration with N admissions (§6.1); TEAM is Phase 10
            policy.Payment = tt.PricePaise > 0 ? PaymentPolicy.Self : PaymentPolicy.Free;
            policy.IdentityRequirement = tt.IsCompetition ? IdentityRequirement.Verified
                : tt is { PricePaise: 0, RegistrationMode: RegistrationMode.Individual } ? IdentityRequirement.Contact
                : IdentityRequirement.Account;
            policy.Allocation = AllocationPolicy.Fcfs;
            policy.GatesJson = JsonSerializer.Serialize(new[] { nameof(RegistrationGate.Open) }, J);
            db.RegistrationPolicies.Add(policy);
        }
        policy.UpdatedAt = DateTime.UtcNow;
    }

    public async Task SyncPassAsync(Guid ticketTypeId, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.FindAsync([ticketTypeId], ct);   // tracked (pending) values if mid-transaction
        if (tt is null) return;

        var pass = await db.Passes.FirstOrDefaultAsync(p => p.TicketTypeId == ticketTypeId, ct);
        if (pass is null)
        {
            pass = new Pass { EventId = tt.EventId, TicketTypeId = tt.Id };
            db.Passes.Add(pass);
            // Phase 9 Option A: a Pass grants exactly one SINGLE-scope, in-person AdmissionRight to its own event.
            db.AdmissionRights.Add(new AdmissionRight { PassId = pass.Id, Scope = AdmissionScope.Single, EventId = tt.EventId, Channel = AdmissionChannel.InPerson, Uses = 1 });
        }
        // Commercial fields mirror the (legacy) ticket type — the Pass is the authoritative product surface.
        pass.Name = tt.Name;
        pass.PricePaise = tt.PricePaise;
        pass.Currency = tt.Currency;
        pass.Quantity = tt.Quantity;
        pass.SaleStarts = tt.SaleStarts;
        pass.SaleEnds = tt.SaleEnds;
        pass.PerSubjectLimit = tt.PerUserLimit;
        pass.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<ServiceResult<RegistrationPolicyView>> GetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, CancellationToken ct = default)
    {
        if (!isAdmin && !await permissions.HasAsync(actorId, eventId, "event:manage", ct))
            return ServiceResult<RegistrationPolicyView>.Fail("forbidden");
        var policy = await db.RegistrationPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.EventId == eventId && p.TicketTypeId == ticketTypeId, ct);
        return policy is null ? ServiceResult<RegistrationPolicyView>.Fail("not_found")
            : ServiceResult<RegistrationPolicyView>.Success(ToPolicyView(policy));
    }

    public async Task<ServiceResult<RegistrationPolicyView>> SetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, RegistrationPolicyInput input, CancellationToken ct = default)
    {
        if (!isAdmin && !await permissions.HasAsync(actorId, eventId, "event:manage", ct))
            return ServiceResult<RegistrationPolicyView>.Fail("forbidden");
        var policy = await db.RegistrationPolicies.FirstOrDefaultAsync(p => p.EventId == eventId && p.TicketTypeId == ticketTypeId, ct);
        if (policy is null) return ServiceResult<RegistrationPolicyView>.Fail("not_found");

        if (!TryEnum(input.Subject, out RegistrationSubject subject, policy.Subject)) return ServiceResult<RegistrationPolicyView>.Fail("invalid_subject");
        if (!TryEnum(input.IdentityRequirement, out IdentityRequirement idReq, policy.IdentityRequirement)) return ServiceResult<RegistrationPolicyView>.Fail("invalid_identity_requirement");
        if (!TryEnum(input.Allocation, out AllocationPolicy alloc, policy.Allocation)) return ServiceResult<RegistrationPolicyView>.Fail("invalid_allocation");
        if (!TryEnum(input.Payment, out PaymentPolicy pay, policy.Payment)) return ServiceResult<RegistrationPolicyView>.Fail("invalid_payment");

        var gates = new List<string>();
        foreach (var g in input.Gates ?? [])
        {
            if (!Enum.TryParse<RegistrationGate>(g, ignoreCase: true, out var gate)) return ServiceResult<RegistrationPolicyView>.Fail("invalid_gate");
            gates.Add(gate.ToString());
        }

        policy.Subject = subject;
        policy.IdentityRequirement = idReq;
        policy.Allocation = alloc;
        policy.Payment = pay;
        if (gates.Count > 0) policy.GatesJson = JsonSerializer.Serialize(gates, J);
        if (input.DocumentsRequired is { Count: > 0 }) policy.DocumentsRequiredJson = JsonSerializer.Serialize(input.DocumentsRequired, J);
        if (input.OpensAt is not null) policy.OpensAt = input.OpensAt;
        if (input.ClosesAt is not null) policy.ClosesAt = input.ClosesAt;
        if (input.LateWindowMinutes is not null) policy.LateWindowMinutes = input.LateWindowMinutes;
        if (input.EditUntil is not null) policy.EditUntil = input.EditUntil;
        if (input.CancelUntil is not null) policy.CancelUntil = input.CancelUntil;
        policy.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog { ActorType = "user", ActorId = actorId, Action = "registration.set_policy", Entity = "registration_policies", EntityId = policy.Id });
        await db.SaveChangesAsync(ct);
        return ServiceResult<RegistrationPolicyView>.Success(ToPolicyView(policy));
    }

    // ── Shadow projection ──────────────────────────────────────────────────────

    public async Task MirrorOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        // Isolation (§17.1, review P1): the post-commit projection runs on its OWN DbContext resolved from a
        // fresh scope — never the request's scoped context. A projection failure can therefore never leave the
        // request context dirty nor turn a committed order into a 500. Also keeps per-order backfill/repair
        // change-tracking bounded (a fresh context per order, no cross-order accumulation).
        using var scope = scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // Own transaction so the per-person credential advisory lock (in the core) is held until commit, serialising
        // this out-of-band projection against any concurrent in-transaction money-path projection for the same person.
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        await MirrorOrderCoreAsync(ctx, orderId, ct);
        await tx.CommitAsync(ct);
    }

    // Authoritative in-transaction projection (Phase 9): same engine, but on the caller's shared context so it
    // commits atomically with the order/ticket/decrement. No fresh scope — the money path owns the transaction.
    public Task ProjectOrderInTransactionAsync(Guid orderId, CancellationToken ct = default)
        => MirrorOrderCoreAsync(db, orderId, ct);

    // Two 32-bit keys for a Postgres advisory lock, derived from (tree root, person) GUIDs. A collision only makes
    // two unrelated persons serialise occasionally — harmless; never a correctness issue.
    private static (int, int) CredentialLockKey(Guid root, Guid person)
        => (BitConverter.ToInt32(root.ToByteArray(), 0), BitConverter.ToInt32(person.ToByteArray(), 0));

    // The same shape and the same harmlessness, keyed on the order (D-336). Two 32-bit halves of one GUID rather
    // than the first half of two, so it cannot alias a CredentialLockKey unless a tree root and a person GUID
    // happen to share those bytes — and a collision costs only occasional extra serialisation.
    private static (int, int) OrderLockKey(Guid order)
        => (BitConverter.ToInt32(order.ToByteArray(), 0), BitConverter.ToInt32(order.ToByteArray(), 4));

    private static async Task MirrorOrderCoreAsync(KurxDbContext db, Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return;

        // Credentials are keyed on the event-tree ROOT (V3 §7.2, review P2): a person holds ONE credential
        // across a fest and all its sub-events. For standalone events root == the event itself.
        var parentId = await db.Events.AsNoTracking().Where(e => e.Id == order.EventId).Select(e => e.ParentEventId).FirstOrDefaultAsync(ct);
        var rootEventId = parentId ?? order.EventId;

        // D-336 — serialise the resolve-or-insert below, which is a read-then-insert and has THREE concurrent
        // producers for one order: the in-transaction money path (order create, capture, refund, group join),
        // DataBackfillJob's convergence pass, and RegistrationReconciliationJob's repair. The backfill's own
        // selector is the racing read — it picks orders `!db.Registrations.Any(r => r.OrderId == o.Id)`, so an
        // order whose capture has not yet committed is selected and projected in parallel with that capture.
        // Both then read null and both insert; IX_registrations_OrderId refuses the loser with 23505, which
        // surfaced as an unhandled DbUpdateException — a 500 where §17.1 promises an idempotent no-op, and on a
        // gateway webhook a 500 is answered with a retry. Holding the lock to commit means the loser reads the
        // winner's committed row and updates it instead.
        //
        // Deliberately the same transaction-scoped advisory lock the credential block below already uses, for
        // the identical reason ("the later reuses the credential the earlier committed instead of colliding on
        // the unique index and rolling back its money") — this just covers the row that was left out. Acquired
        // BEFORE the per-person credential locks and never after, so the global order is order-then-persons and
        // no pair of callers can deadlock. Requires a transaction to be meaningful, which all three producers
        // hold: the money path owns one, and MirrorOrderAsync wraps the other two.
        var (orderLock1, orderLock2) = OrderLockKey(orderId);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({orderLock1}, {orderLock2})", ct);

        var reg = await db.Registrations.FirstOrDefaultAsync(r => r.OrderId == orderId, ct);
        if (reg is null)
        {
            reg = new Registration
            {
                EventId = order.EventId, TicketTypeId = order.TicketTypeId,
                SubjectType = RegistrationSubject.Person, SubjectId = order.UserId, OrderId = orderId,
                AnswersJson = order.AnswersJson,
            };
            db.Registrations.Add(reg);
        }
        reg.State = order.Status switch
        {
            OrderStatus.Paid => RegistrationState.Confirmed,
            OrderStatus.Refunded => RegistrationState.Cancelled,
            _ => RegistrationState.Pending,
        };
        reg.UpdatedAt = DateTime.UtcNow;

        // V3 §9.5 VAR (Phase 9): one immutable allocation line per order item, written at purchase and NEVER
        // recomputed — the sole basis for both revenue and refunds. Single-scope (Option A): the whole line value
        // is allocated to the order's one event, basis list_price. Idempotent: written once per order.
        if (!await db.ValueAllocationRecords.AnyAsync(v => v.OrderId == orderId, ct))
        {
            var items = await db.OrderItems.AsNoTracking().Where(oi => oi.OrderId == orderId).ToListAsync(ct);
            foreach (var oi in items)
                db.ValueAllocationRecords.Add(new ValueAllocationRecord
                {
                    OrderId = orderId, OrderItemId = oi.Id, EventId = order.EventId,
                    AllocatedPaise = oi.UnitPricePaise * oi.Qty, Currency = oi.Currency, Basis = "list_price",
                });
        }

        var tickets = await db.Tickets.AsNoTracking()
            .Where(t => db.OrderItems.Where(oi => oi.OrderId == orderId).Select(oi => oi.Id).Contains(t.OrderItemId))
            .ToListAsync(ct);
        var ticketIds = tickets.Select(t => t.Id).ToList();
        var admByTicket = (await db.Admissions.Where(a => ticketIds.Contains(a.TicketId)).ToListAsync(ct)).ToDictionary(a => a.TicketId);
        var poolId = await db.InventoryPools.AsNoTracking()
            .Where(p => p.TicketTypeId == order.TicketTypeId && p.Segment == InventorySegment.General)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        var personIds = tickets.Where(t => t.UserId != null).Select(t => t.UserId!.Value).Distinct().ToList();
        // Serialize concurrent same-person credential creation (§9.3: one per person per event tree). A
        // transaction-scoped advisory lock per (tree root, person), acquired in sorted order (deadlock-free),
        // means two concurrent orders for the same person both commit — the later reuses the credential the earlier
        // committed instead of colliding on the unique index and rolling back its money. Requires a transaction:
        // the money path is already in one, and the out-of-band MirrorOrderAsync wraps the projection in one.
        foreach (var pid in personIds.OrderBy(x => x))
        {
            var (k1, k2) = CredentialLockKey(rootEventId, pid);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({k1}, {k2})", ct);
        }
        var credByPerson = (await db.Credentials.Where(c => c.EventId == rootEventId && c.PersonId != null && personIds.Contains(c.PersonId!.Value)).ToListAsync(ct))
            .ToDictionary(c => c.PersonId!.Value);
        var now = DateTime.UtcNow;

        foreach (var t in tickets)
        {
            if (!admByTicket.TryGetValue(t.Id, out var adm))
            {
                adm = new Admission { RegistrationId = reg.Id, EventId = order.EventId, PersonId = t.UserId, TicketId = t.Id, PoolId = poolId };
                db.Admissions.Add(adm);
                admByTicket[t.Id] = adm;
            }
            adm.State = t.State switch
            {
                TicketState.CheckedIn => AdmissionState.CheckedIn,
                TicketState.Void => AdmissionState.Void,
                _ => AdmissionState.Active,
            };
            adm.UpdatedAt = now;

            if (t.UserId is { } pid)   // ONE credential per person per event TREE (§7.2) — keyed on the root
            {
                if (!credByPerson.TryGetValue(pid, out var cred))
                {
                    cred = new Credential { EventId = rootEventId, PersonId = pid, Code = t.Code };
                    db.Credentials.Add(cred);
                    credByPerson[pid] = cred;
                }
                adm.CredentialId = cred.Id;
            }
            else if (adm.CredentialId is null)   // guest — one credential per admission (no person to dedup on)
            {
                var gcred = new Credential { EventId = rootEventId, PersonId = null, Code = t.Code };
                db.Credentials.Add(gcred);
                adm.CredentialId = gcred.Id;
            }
        }
        await db.SaveChangesAsync(ct);

        // A credential is Active iff at least one non-void admission (across any order) still references it —
        // so a full refund revokes it, a partial one keeps it. Set-based (review P4): one query for the
        // credential ids this order touches, one for which of them still have a live admission, one load.
        var credIds = await db.Admissions.AsNoTracking()
            .Where(a => ticketIds.Contains(a.TicketId) && a.CredentialId != null)
            .Select(a => a.CredentialId!.Value).Distinct().ToListAsync(ct);
        if (credIds.Count > 0)
        {
            var activeCredIds = (await db.Admissions.AsNoTracking()
                .Where(a => credIds.Contains(a.CredentialId!.Value) && a.State != AdmissionState.Void)
                .Select(a => a.CredentialId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
            var creds = await db.Credentials.Where(c => credIds.Contains(c.Id)).ToListAsync(ct);
            foreach (var cred in creds)
            {
                var desired = activeCredIds.Contains(cred.Id) ? CredentialState.Active : CredentialState.Revoked;
                if (cred.State != desired) { cred.State = desired; cred.UpdatedAt = now; }
            }
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<ServiceResult<IReadOnlyList<RegistrationView>>> GetForEventAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        if (!isAdmin && !await permissions.HasAsync(actorId, eventId, "event:manage", ct))
            return ServiceResult<IReadOnlyList<RegistrationView>>.Fail("forbidden");
        var regs = await db.Registrations.AsNoTracking().Where(r => r.EventId == eventId)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var counts = await db.Admissions.AsNoTracking().Where(a => a.EventId == eventId)
            .GroupBy(a => a.RegistrationId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        IReadOnlyList<RegistrationView> views = regs.Select(r => new RegistrationView(r.Id, r.EventId, r.TicketTypeId,
            r.SubjectType.ToString(), r.SubjectId, r.OrderId, r.State.ToString(), counts.GetValueOrDefault(r.Id, 0), r.CreatedAt)).ToList();
        return ServiceResult<IReadOnlyList<RegistrationView>>.Success(views);
    }

    // ── Reconciliation + backfill ──────────────────────────────────────────────

    public async Task<IReadOnlyList<RegistrationDrift>> ReconcileAsync(Guid? eventId = null, CancellationToken ct = default)
    {
        List<Guid> eventIds = eventId is not null ? [eventId.Value]
            : await db.Orders.AsNoTracking().Select(o => o.EventId).Distinct().ToListAsync(ct);
        if (eventIds.Count == 0) return [];

        // DB-3: four grouped aggregates for the whole set, instead of five COUNTs per event.
        //
        // The previous shape issued 5 × N round trips — at 100,000 events with orders that is ~500,000
        // queries in one daily job run. Every filter below is byte-identical to the COUNT it replaces; the
        // only thing that changed is where the grouping happens.
        //
        // ── THE TRAP THIS CODE EXISTS TO AVOID ──────────────────────────────────────────────────────
        // GROUP BY returns NO ROW for an event with no matching records. An event with 5 orders and 0
        // registrations is REAL DRIFT — precisely what this job is for — and reading these dictionaries
        // with a "skip if absent" would make that event vanish from the report. Every lookup below is
        // therefore GetValueOrDefault(eid, 0): absence means zero, never "skip". The iteration also stays
        // over eventIds, not over the dictionaries' keys, so the set of events examined is unchanged.
        var orderCounts = await db.Orders.AsNoTracking()
            .Where(o => eventIds.Contains(o.EventId))
            .GroupBy(o => o.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, ct);

        var regCounts = await db.Registrations.AsNoTracking()
            .Where(r => eventIds.Contains(r.EventId))
            .GroupBy(r => r.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, ct);

        var ticketCounts = await db.Tickets.AsNoTracking()
            .Where(t => eventIds.Contains(t.EventId) && t.State != TicketState.Void)
            .GroupBy(t => t.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, ct);

        // Both admission figures come from one grouped pass, since they differ only by predicate.
        // Count(predicate) inside a GroupBy translates to COUNT(CASE WHEN … THEN 1 END) — a conditional
        // aggregate, evaluated in the same scan rather than a second query.
        var admissionCounts = await db.Admissions.AsNoTracking()
            .Where(a => eventIds.Contains(a.EventId) && a.State != AdmissionState.Void)
            .GroupBy(a => a.EventId)
            .Select(g => new
            {
                EventId = g.Key,
                Active = g.Count(),
                // A live account-holder admission with no credential is a projection gap (e.g. a
                // credential-race partial write) — flag it so RepairAsync re-links it (review P3).
                MissingCred = g.Count(a => a.PersonId != null && a.CredentialId == null),
            })
            .ToDictionaryAsync(x => x.EventId, x => (x.Active, x.MissingCred), ct);

        var drift = new List<RegistrationDrift>();
        foreach (var eid in eventIds)
        {
            var orders = orderCounts.GetValueOrDefault(eid, 0);
            var regs = regCounts.GetValueOrDefault(eid, 0);
            var activeTickets = ticketCounts.GetValueOrDefault(eid, 0);
            var (activeAdms, missingCred) = admissionCounts.GetValueOrDefault(eid, (0, 0));
            if (orders != regs || activeTickets != activeAdms || missingCred != 0)
                drift.Add(new RegistrationDrift(eid, orders, regs, activeTickets, activeAdms, missingCred));
        }
        return drift;
    }


    public async Task<int> RepairAsync(Guid? eventId = null, CancellationToken ct = default)
    {
        // Heal by re-projecting: every order in a drifting event (fixes missing/partial rows and unlinked
        // credentials, idempotently) plus any order that has no registration at all. Each re-projection runs on
        // its own isolated context via MirrorOrderAsync.
        var driftEvents = (await ReconcileAsync(eventId, ct)).Select(d => d.EventId).ToList();
        var orders = await db.Orders.AsNoTracking()
            .Where(o => (eventId == null || o.EventId == eventId)
                && (driftEvents.Contains(o.EventId) || !db.Registrations.Any(r => r.OrderId == o.Id)))
            .Select(o => o.Id).ToListAsync(ct);
        foreach (var oid in orders) await MirrorOrderAsync(oid, ct);
        return orders.Count;
    }

    public async Task<int> BackfillAsync(CancellationToken ct = default)
    {
        var ttWithoutPolicy = await db.TicketTypes.AsNoTracking()
            .Where(t => !db.RegistrationPolicies.Any(p => p.TicketTypeId == t.Id)).Select(t => t.Id).ToListAsync(ct);
        foreach (var id in ttWithoutPolicy) await SyncPolicyAsync(id, ct);
        // Phase 9: a Pass + AdmissionRight(SINGLE) per ticket type that lacks one.
        var ttWithoutPass = await db.TicketTypes.AsNoTracking()
            .Where(t => !db.Passes.Any(p => p.TicketTypeId == t.Id)).Select(t => t.Id).ToListAsync(ct);
        foreach (var id in ttWithoutPass) await SyncPassAsync(id, ct);
        if (ttWithoutPolicy.Count > 0 || ttWithoutPass.Count > 0) await db.SaveChangesAsync(ct);

        // Project every order missing a registration OR a VAR line — the union covers both un-mirrored orders and
        // Phase-8 orders that predate the VAR. Re-projection is idempotent and now also writes the VAR (§9.5).
        var toProject = await db.Orders.AsNoTracking()
            .Where(o => !db.Registrations.Any(r => r.OrderId == o.Id) || !db.ValueAllocationRecords.Any(v => v.OrderId == o.Id))
            .Select(o => o.Id).ToListAsync(ct);
        foreach (var oid in toProject) await MirrorOrderAsync(oid, ct);
        return toProject.Count;
    }

    // ── helpers ─────────────────────────────────────────────────────────────
    private static bool TryEnum<T>(string? s, out T value, T current) where T : struct, Enum
    {
        value = current;
        if (s is null) return true;
        if (!Enum.TryParse<T>(s, ignoreCase: true, out var v)) return false;
        value = v; return true;
    }

    private static RegistrationPolicyView ToPolicyView(RegistrationPolicy p) => new(
        p.EventId, p.TicketTypeId, p.Subject.ToString(),
        p.GatesJson is null ? [nameof(RegistrationGate.Open)] : JsonSerializer.Deserialize<List<string>>(p.GatesJson, J) ?? [],
        p.IdentityRequirement.ToString(), p.Allocation.ToString(), p.Payment.ToString(),
        p.OpensAt, p.ClosesAt, p.LateWindowMinutes, p.EditUntil, p.CancelUntil,
        p.DocumentsRequiredJson is null ? [] : JsonSerializer.Deserialize<List<string>>(p.DocumentsRequiredJson, J) ?? []);
}
