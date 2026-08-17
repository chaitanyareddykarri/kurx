using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Internal approval chains (V3 §14.3, Phase 14) — chain configuration on an OrgUnit (inherited down the tree)
/// + the per-event approve/reject/bypass workflow that gates publishing. Reuses the OrgUnit materialised path (Phase 4),
/// the org role check, and the typed audit spine. Additive; the paid-event platform-review gate (§14.4) is untouched.</summary>
public class ApprovalService(KurxDbContext db, IEventAuthority authority, IAuditWriter audit) : IApprovalService
{
    // ── Chain configuration ────────────────────────────────────────────────────
    public async Task<ServiceResult<ApprovalChainView>> CreateChainAsync(Guid actorId, Guid orgUnitId, bool isAdmin, ApprovalChainInput input, CancellationToken ct = default)
    {
        var unit = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(u => u.Id == orgUnitId, ct);
        if (unit is null) return ServiceResult<ApprovalChainView>.Fail("not_found");
        if (!await CanManageOrgAsync(actorId, unit.OrgId, isAdmin, ct)) return ServiceResult<ApprovalChainView>.Fail("forbidden");
        if (string.IsNullOrWhiteSpace(input.Name)) return ServiceResult<ApprovalChainView>.Fail("name_required");
        if (!TryEnum(input.Mode, out ApprovalMode mode, ApprovalMode.Sequential)) return ServiceResult<ApprovalChainView>.Fail("invalid_mode");

        var chain = new ApprovalChain { OrgUnitId = orgUnitId, Name = input.Name!.Trim(), Mode = mode };
        var stepsError = BuildSteps(chain.Id, input.Steps, out var steps);
        if (stepsError is not null) return ServiceResult<ApprovalChainView>.Fail(stepsError);
        db.ApprovalChains.Add(chain);
        db.ApprovalSteps.AddRange(steps);
        audit.Write(new AuditEvent("approval_chain.create", "approval_chains", chain.Id, "user", actorId, null, new { orgUnitId, chain.Name, steps = steps.Count }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<ApprovalChainView>.Success(await ToChainViewAsync(chain, ct));
    }

    public async Task<ServiceResult<ApprovalChainView>> UpdateChainAsync(Guid actorId, Guid chainId, bool isAdmin, ApprovalChainInput input, CancellationToken ct = default)
    {
        var chain = await db.ApprovalChains.FirstOrDefaultAsync(c => c.Id == chainId && c.DeletedAt == null, ct);
        if (chain is null) return ServiceResult<ApprovalChainView>.Fail("not_found");
        var unit = await db.OrgUnits.AsNoTracking().FirstAsync(u => u.Id == chain.OrgUnitId, ct);
        if (!await CanManageOrgAsync(actorId, unit.OrgId, isAdmin, ct)) return ServiceResult<ApprovalChainView>.Fail("forbidden");
        if (!string.IsNullOrWhiteSpace(input.Name)) chain.Name = input.Name!.Trim();
        if (input.Mode is not null) { if (!TryEnum(input.Mode, out ApprovalMode mode, chain.Mode)) return ServiceResult<ApprovalChainView>.Fail("invalid_mode"); chain.Mode = mode; }
        if (input.Steps is not null)
        {
            var stepsError = BuildSteps(chain.Id, input.Steps, out var steps);
            if (stepsError is not null) return ServiceResult<ApprovalChainView>.Fail(stepsError);
            db.ApprovalSteps.RemoveRange(db.ApprovalSteps.Where(s => s.ChainId == chain.Id));   // replace the step set
            db.ApprovalSteps.AddRange(steps);
        }
        chain.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<ApprovalChainView>.Success(await ToChainViewAsync(chain, ct));
    }

    public async Task<ServiceResult<bool>> DeleteChainAsync(Guid actorId, Guid chainId, bool isAdmin, CancellationToken ct = default)
    {
        var chain = await db.ApprovalChains.FirstOrDefaultAsync(c => c.Id == chainId && c.DeletedAt == null, ct);
        if (chain is null) return ServiceResult<bool>.Fail("not_found");
        var unit = await db.OrgUnits.AsNoTracking().FirstAsync(u => u.Id == chain.OrgUnitId, ct);
        if (!await CanManageOrgAsync(actorId, unit.OrgId, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        chain.DeletedAt = DateTime.UtcNow;
        audit.Write(new AuditEvent("approval_chain.delete", "approval_chains", chain.Id, "user", actorId, null, null));
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<ApprovalChainView>>> ListForOrgUnitAsync(Guid actorId, Guid orgUnitId, bool isAdmin, CancellationToken ct = default)
    {
        var unit = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(u => u.Id == orgUnitId, ct);
        if (unit is null) return ServiceResult<IReadOnlyList<ApprovalChainView>>.Fail("not_found");
        if (!await CanManageOrgAsync(actorId, unit.OrgId, isAdmin, ct)) return ServiceResult<IReadOnlyList<ApprovalChainView>>.Fail("forbidden");
        var chains = await db.ApprovalChains.AsNoTracking().Where(c => c.OrgUnitId == orgUnitId && c.DeletedAt == null).ToListAsync(ct);
        var views = new List<ApprovalChainView>(chains.Count);
        foreach (var c in chains) views.Add(await ToChainViewAsync(c, ct));
        return ServiceResult<IReadOnlyList<ApprovalChainView>>.Success(views);
    }

    // ── Per-event workflow ──────────────────────────────────────────────────────
    public async Task<ServiceResult<ApprovalRequestView>> GetRequestAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<ApprovalRequestView>.Fail("not_found");
        if (!await CanManageOrgAsync(actorId, ev.RepresentingOrgId, isAdmin, ct)) return ServiceResult<ApprovalRequestView>.Fail("forbidden");
        await EnsureAndCheckCompleteAsync(eventId, ct);   // materialise the request if a chain applies
        var req = await db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        return req is null ? ServiceResult<ApprovalRequestView>.Fail("no_approval_required") : ServiceResult<ApprovalRequestView>.Success(await ToRequestViewAsync(req, ct));
    }

    public async Task<ServiceResult<ApprovalRequestView>> DecideAsync(Guid actorId, Guid stepDecisionId, bool isAdmin, string action, string? note, CancellationToken ct = default)
    {
        var act = action?.Trim().ToLowerInvariant();
        if (act is not ("approve" or "reject" or "bypass")) return ServiceResult<ApprovalRequestView>.Fail("invalid_action");
        var decision = await db.ApprovalStepDecisions.FirstOrDefaultAsync(d => d.Id == stepDecisionId, ct);
        if (decision is null) return ServiceResult<ApprovalRequestView>.Fail("not_found");
        var req = await db.ApprovalRequests.FirstAsync(r => r.Id == decision.RequestId, ct);
        var step = await db.ApprovalSteps.AsNoTracking().FirstAsync(s => s.Id == decision.StepId, ct);
        var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == req.EventId, ct);
        if (req.State != ApprovalRequestState.Pending) return ServiceResult<ApprovalRequestView>.Fail("request_not_pending");
        if (decision.State != ApprovalStepState.Pending) return ServiceResult<ApprovalRequestView>.Fail("step_already_decided");

        if (act == "bypass")
        {
            // Governed (§14.3): only an org Owner or admin may bypass — always audited.
            if (!isAdmin && await RoleAsync(actorId, ev.RepresentingOrgId, ct) is not OrgRole.Owner) return ServiceResult<ApprovalRequestView>.Fail("forbidden");
        }
        else
        {
            if (!await IsStepApproverAsync(actorId, step, ev.RepresentingOrgId, isAdmin, ct)) return ServiceResult<ApprovalRequestView>.Fail("forbidden");
            // SEQUENTIAL: every earlier applicable step must be settled first.
            var chain = await db.ApprovalChains.AsNoTracking().FirstAsync(c => c.Id == req.ChainId, ct);
            if (chain.Mode == ApprovalMode.Sequential)
            {
                // N1: a total order on (Sort, StepId) so steps sharing a Sort still settle deterministically.
                var earlierPending = await db.ApprovalStepDecisions.AnyAsync(d => d.RequestId == req.Id && d.State == ApprovalStepState.Pending
                    && (d.Sort < decision.Sort || (d.Sort == decision.Sort && d.StepId < decision.StepId)), ct);
                if (earlierPending) return ServiceResult<ApprovalRequestView>.Fail("earlier_step_pending");
            }
        }

        decision.State = act switch { "approve" => ApprovalStepState.Approved, "reject" => ApprovalStepState.Rejected, _ => ApprovalStepState.Bypassed };
        decision.DecidedBy = actorId; decision.DecidedAt = DateTime.UtcNow; decision.Note = note?.Trim();
        audit.Write(new AuditEvent($"approval.{act}", "approval_step_decisions", decision.Id, isAdmin ? "admin" : "user", actorId,
            new { step = decision.StepId }, new { eventId = req.EventId, decision.State }));

        // Re-evaluate completeness IN MEMORY over the tracked decisions — a DB query here would still see this
        // just-decided row as Pending (it isn't saved yet). The current decision is the same tracked instance.
        var siblings = await db.ApprovalStepDecisions.Where(d => d.RequestId == req.Id).ToListAsync(ct);
        if (decision.State == ApprovalStepState.Rejected) { req.State = ApprovalRequestState.Rejected; req.DecidedAt = DateTime.UtcNow; }
        else if (siblings.All(d => d.State != ApprovalStepState.Pending)) { req.State = ApprovalRequestState.Approved; req.DecidedAt = DateTime.UtcNow; }   // all settled Approved/Bypassed
        await db.SaveChangesAsync(ct);
        return ServiceResult<ApprovalRequestView>.Success(await ToRequestViewAsync(req, ct));
    }

    public async Task<ServiceResult<ApprovalRequestView>> ResubmitAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<ApprovalRequestView>.Fail("not_found");
        if (!await CanManageOrgAsync(actorId, ev.RepresentingOrgId, isAdmin, ct)) return ServiceResult<ApprovalRequestView>.Fail("forbidden");
        var req = await db.ApprovalRequests.FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        if (req is null) return ServiceResult<ApprovalRequestView>.Fail("no_approval_required");
        if (req.State != ApprovalRequestState.Rejected) return ServiceResult<ApprovalRequestView>.Fail("not_rejected");

        // H2: reject → fix event → resubmit → a fresh approval cycle. The prior decisions' full history is preserved in
        // the append-only audit spine (every approve/reject/bypass was written there); the transient current-cycle
        // decision rows are cleared and re-materialised for the event's CURRENT state. The resubmit itself is audited.
        var prior = await db.ApprovalStepDecisions.AsNoTracking().Where(d => d.RequestId == req.Id)
            .Select(d => new { d.StepId, State = d.State.ToString(), d.DecidedBy }).ToListAsync(ct);
        db.ApprovalStepDecisions.RemoveRange(db.ApprovalStepDecisions.Where(d => d.RequestId == req.Id));
        req.State = ApprovalRequestState.Pending; req.DecidedAt = null;
        audit.Write(new AuditEvent("approval.resubmit", "approval_requests", req.Id, isAdmin ? "admin" : "user", actorId,
            new { priorDecisions = prior }, new { eventId, state = "Pending" }));
        await db.SaveChangesAsync(ct);
        await EnsureAndCheckCompleteAsync(eventId, ct);   // re-materialise fresh decisions for the current event state
        return ServiceResult<ApprovalRequestView>.Success(await ToRequestViewAsync(await db.ApprovalRequests.AsNoTracking().FirstAsync(r => r.Id == req.Id, ct), ct));
    }

    // ── Publish gate (called by the lifecycle) ─────────────────────────────────
    public async Task<bool> EnsureAndCheckCompleteAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return true;
        var chain = await ResolveEffectiveChainAsync(ev, ct);
        if (chain is null) return true;   // no chain ⇒ unchained events publish exactly as before

        var req = await db.ApprovalRequests.FirstOrDefaultAsync(r => r.EventId == eventId && r.ChainId == chain.Id, ct);
        if (req is null)
        {
            req = new ApprovalRequest { EventId = eventId, ChainId = chain.Id };
            db.ApprovalRequests.Add(req);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)   // M2: a concurrent insert won the unique (event, chain) index — use its row
            {
                db.ChangeTracker.Clear();
                req = await db.ApprovalRequests.FirstAsync(r => r.EventId == eventId && r.ChainId == chain.Id, ct);
            }
        }
        if (req.State == ApprovalRequestState.Rejected) return false;   // stays blocked until an explicit resubmit (H2)

        // Evaluate the chain against the CURRENT event state (same rule the read-only preview uses — one source of
        // truth). H1: materialise a Pending decision for any step whose condition now applies but has none yet (e.g. an
        // event that became paid after the request was materialised); steps that no longer apply keep their decisions.
        var (complete, missing) = await EvaluateCompletenessAsync(chain, ev, req.Id, ct);
        var dirty = false;
        foreach (var s in missing)
        {
            db.ApprovalStepDecisions.Add(new ApprovalStepDecision { RequestId = req.Id, StepId = s.Id, Sort = s.Sort });
            dirty = true;
        }
        var desired = complete ? ApprovalRequestState.Approved : ApprovalRequestState.Pending;
        if (req.State != desired) { req.State = desired; req.DecidedAt = complete ? DateTime.UtcNow : null; dirty = true; }
        if (dirty) await db.SaveChangesAsync(ct);
        return complete;
    }

    public async Task<bool> IsCompleteAsync(Guid eventId, CancellationToken ct = default)
    {
        // Read-only mirror of EnsureAndCheckCompleteAsync — evaluates the SAME rule without materialising the request
        // or its decisions (used by the workspace publish-checklist projection, which must never mutate state).
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return true;
        var chain = await ResolveEffectiveChainAsync(ev, ct);
        if (chain is null) return true;
        var req = await db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(r => r.EventId == eventId && r.ChainId == chain.Id, ct);
        if (req is { State: ApprovalRequestState.Rejected }) return false;
        var (complete, _) = await EvaluateCompletenessAsync(chain, ev, req?.Id, ct);
        return complete;
    }

    /// <summary>The single source of truth for approval completeness (read-only): the applicable steps that still lack
    /// a decision, and whether the request is complete. M1: derived from the CURRENT decision set, so a concurrent
    /// final approval can never strand it Pending. Complete ⇔ ≥1 decision, no applicable step undecided, none Pending.</summary>
    private async Task<(bool Complete, List<ApprovalStep> MissingApplicable)> EvaluateCompletenessAsync(
        ApprovalChain chain, Event ev, Guid? requestId, CancellationToken ct)
    {
        var steps = await db.ApprovalSteps.AsNoTracking().Where(s => s.ChainId == chain.Id).OrderBy(s => s.Sort).ToListAsync(ct);
        var decisions = requestId is { } rid
            ? await db.ApprovalStepDecisions.AsNoTracking().Where(d => d.RequestId == rid).ToListAsync(ct)
            : [];
        var missing = new List<ApprovalStep>();
        foreach (var s in steps)
            if (!decisions.Any(d => d.StepId == s.Id) && await ConditionAppliesAsync(s, ev, ct))
                missing.Add(s);
        var complete = missing.Count == 0 && decisions.Count > 0
            && decisions.All(d => d.State is ApprovalStepState.Approved or ApprovalStepState.Bypassed);
        return (complete, missing);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
    private async Task<ApprovalChain?> ResolveEffectiveChainAsync(Event ev, CancellationToken ct)
    {
        if (ev.OrgUnitId is not { } unitId) return null;
        var path = await db.OrgUnits.AsNoTracking().Where(u => u.Id == unitId).Select(u => u.Path).FirstOrDefaultAsync(ct);
        // Nearest-first: the event's own unit, then ancestors up to the root (Path is "/root/.../self/").
        var candidates = string.IsNullOrEmpty(path) ? [unitId] : path.Trim('/').Split('/').Where(s => s.Length > 0).Select(Guid.Parse).Reverse().ToArray();
        foreach (var candidate in candidates)
        {
            var chain = await db.ApprovalChains.AsNoTracking().FirstOrDefaultAsync(c => c.OrgUnitId == candidate && c.DeletedAt == null, ct);
            if (chain is not null) return chain;
        }
        return null;
    }

    private async Task<bool> ConditionAppliesAsync(ApprovalStep step, Event ev, CancellationToken ct) => step.Condition switch
    {
        ApprovalCondition.Always => true,
        ApprovalCondition.IfPaid => await db.TicketTypes.AnyAsync(t => t.EventId == ev.Id && t.PricePaise > 0, ct),
        // D-377 — the pre-publication question, deliberately: this runs while the event is still in
            // review, so it asks whether the event WILL face an external audience, not whether it is live.
            ApprovalCondition.IfExternal => EventExposure.IsExternallyExposed(ev),
        ApprovalCondition.IfBudgetGt => (await db.TicketTypes.Where(t => t.EventId == ev.Id).Select(t => (long?)((long)t.PricePaise * t.Quantity)).MaxAsync(ct) ?? 0) > (step.ConditionParamPaise ?? 0),
        ApprovalCondition.IfMinors => ev.AudienceLevelId is { } al && await db.EventCategories.AnyAsync(c => c.Id == al && (c.Slug.Contains("minor") || c.Slug.Contains("school") || c.Slug.Contains("child")), ct),
        _ => false,
    };

    private async Task<bool> IsStepApproverAsync(Guid actorId, ApprovalStep step, Guid orgId, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin) return true;
        if (step.ApproverUserId is { } uid) return uid == actorId;
        if (step.ApproverRole is { } role) return await RoleAsync(actorId, orgId, ct) == role;
        return false;
    }

    private string? BuildSteps(Guid chainId, IReadOnlyList<ApprovalStepInput>? inputs, out List<ApprovalStep> steps)
    {
        steps = [];
        if (inputs is null || inputs.Count == 0) return "steps_required";
        foreach (var s in inputs)
        {
            if (!TryEnum(s.Condition, out ApprovalCondition cond, ApprovalCondition.Always)) return "invalid_condition";
            OrgRole? role = null;
            if (s.ApproverRole is not null) { if (!Enum.TryParse<OrgRole>(s.ApproverRole, true, out var r)) return "invalid_approver_role"; role = r; }
            if (role is null && s.ApproverUserId is null) return "approver_required";
            steps.Add(new ApprovalStep
            {
                ChainId = chainId, Sort = s.Sort, ApproverRole = role, ApproverUserId = s.ApproverUserId,
                Condition = cond, ConditionParamPaise = s.ConditionParamPaise, SlaHours = s.SlaHours, EscalationAfterHours = s.EscalationAfterHours,
            });
        }
        return null;
    }

    /// <summary>D-269: delegates the manage rule to the single authority.</summary>
    private async Task<bool> CanManageOrgAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct)
        => (await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct)).CanManage;

    /// <summary>Kept deliberately (D-269). An approval step names an <b>exact</b> <see cref="OrgRole"/> to
    /// match against — "this step is approved by Finance" — which is a different question from "how much
    /// authority does this caller hold". Collapsing it onto the authority ladder would make Finance, which
    /// sits at <c>None</c> there, unable to approve the step it was named for.</summary>
    private async Task<OrgRole?> RoleAsync(Guid userId, Guid orgId, CancellationToken ct)
        => await db.Memberships.AsNoTracking().Where(m => m.OrgId == orgId && m.UserId == userId).Select(m => (OrgRole?)m.Role).FirstOrDefaultAsync(ct);

    private async Task<ApprovalChainView> ToChainViewAsync(ApprovalChain c, CancellationToken ct)
    {
        var steps = await db.ApprovalSteps.AsNoTracking().Where(s => s.ChainId == c.Id).OrderBy(s => s.Sort)
            .Select(s => new ApprovalStepView(s.Id, s.Sort, s.ApproverRole == null ? null : s.ApproverRole.ToString(), s.ApproverUserId, s.Condition.ToString(), s.ConditionParamPaise, s.SlaHours, s.EscalationAfterHours))
            .ToListAsync(ct);
        return new ApprovalChainView(c.Id, c.OrgUnitId, c.Name, c.Mode.ToString(), steps);
    }

    private async Task<ApprovalRequestView> ToRequestViewAsync(ApprovalRequest r, CancellationToken ct)
    {
        var decisions = await (from d in db.ApprovalStepDecisions.AsNoTracking().Where(d => d.RequestId == r.Id)
                               join s in db.ApprovalSteps.AsNoTracking() on d.StepId equals s.Id
                               orderby d.Sort
                               select new ApprovalDecisionView(d.Id, d.StepId, d.Sort, d.State.ToString(), d.DecidedBy, d.DecidedAt, d.Note,
                                   s.ApproverRole == null ? null : s.ApproverRole.ToString(), s.ApproverUserId, s.Condition.ToString())).ToListAsync(ct);
        return new ApprovalRequestView(r.Id, r.EventId, r.ChainId, r.State.ToString(), decisions);
    }

    private static bool TryEnum<T>(string? s, out T value, T current) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(s)) { value = current; return true; }
        return Enum.TryParse(s.Replace("_", ""), true, out value);
    }
}
