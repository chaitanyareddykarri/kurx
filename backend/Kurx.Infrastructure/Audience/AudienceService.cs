using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Audience;

/// <summary>Audience / eligibility subsystem (V3 §4.4, Phase 5). Additive: a new rule per event + a member
/// attributes write-path + a pure server-side evaluator. It does NOT touch the organiser RBAC — rule
/// management reuses the same org-level Owner/Manager check every event service uses; eligibility is a
/// separate registration gate, not a change to who may manage.</summary>
/// <param name="invitations">D-266 M6 — asked whether a person was let in, for either D9 method. Injected
/// rather than queried inline so "invited" has one definition; <c>IInvitationService</c> owns it and this
/// engine owns when it matters. No cycle: the invitation service never asks about eligibility.</param>
public class AudienceService(KurxDbContext db, IEventAuthority authority, IInvitationService invitations) : IAudienceService
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    public async Task<ServiceResult<AudienceRuleView?>> GetRuleAsync(Guid actorUserId, Guid orgId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        if (!await EventInOrgAsync(eventId, orgId, ct)) return ServiceResult<AudienceRuleView?>.Fail("not_found");
        if (!(await authority.ResolveAsync(actorUserId, eventId, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<AudienceRuleView?>.Fail("forbidden");
        var rule = await db.AudienceRules.AsNoTracking().FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        return ServiceResult<AudienceRuleView?>.Success(rule is null ? null : ToView(rule));
    }

    public async Task<ServiceResult<AudienceRuleView>> SetRuleAsync(Guid actorUserId, Guid orgId, Guid eventId, bool isAdmin, AudienceRuleInput input, CancellationToken ct = default)
    {
        if (!await EventInOrgAsync(eventId, orgId, ct)) return ServiceResult<AudienceRuleView>.Fail("not_found");
        if (!(await authority.ResolveAsync(actorUserId, eventId, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<AudienceRuleView>.Fail("forbidden");

        var appliesTo = AudienceAppliesTo.EveryMember;
        if (input.AppliesTo is not null && !Enum.TryParse(input.AppliesTo, ignoreCase: true, out appliesTo))
            return ServiceResult<AudienceRuleView>.Fail("invalid_applies_to");

        // Canonicalise role names to the enum spelling so evaluation's Role.ToString() comparison holds.
        var roles = new List<string>();
        foreach (var r in input.RoleIn ?? [])
        {
            if (!Enum.TryParse<OrgRole>(r, ignoreCase: true, out var parsed)) return ServiceResult<AudienceRuleView>.Fail("invalid_role");
            roles.Add(parsed.ToString());
        }

        if (input.UnitSubtreeIn is { Count: > 0 })
        {
            var valid = await db.OrgUnits.CountAsync(u => u.OrgId == orgId && input.UnitSubtreeIn.Contains(u.Id), ct);
            if (valid != input.UnitSubtreeIn.Distinct().Count()) return ServiceResult<AudienceRuleView>.Fail("invalid_unit");
        }
        if (input.GuestPerRegistrantCap < 0) return ServiceResult<AudienceRuleView>.Fail("invalid_guest_cap");

        var rule = await db.AudienceRules.FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        if (rule is null) { rule = new AudienceRule { EventId = eventId }; db.AudienceRules.Add(rule); }

        rule.UnitSubtreeInJson = Serialize(input.UnitSubtreeIn);
        rule.RoleInJson = roles.Count > 0 ? JsonSerializer.Serialize(roles, J) : null;
        rule.CohortYearInJson = Serialize(input.CohortYearIn);
        rule.AttributeMatchesJson = input.AttributeMatches is { Count: > 0 } ? JsonSerializer.Serialize(input.AttributeMatches, J) : null;
        rule.RequireVerified = input.RequireVerified;
        rule.ExternalOrgsAllowed = input.ExternalOrgsAllowed;
        rule.AppliesTo = appliesTo;
        rule.GuestsAllowed = input.GuestsAllowed;
        rule.GuestPerRegistrantCap = input.GuestPerRegistrantCap;
        rule.GuestsRequireApproval = input.GuestsRequireApproval;
        rule.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog { ActorType = "user", ActorId = actorUserId, Action = "audience.set", Entity = "events", EntityId = eventId });
        db.OutboxMessages.Add(Search.SearchReindex.Message(eventId));   // V3 §15 (Phase 16): HasAudienceRule changed → reindex so the eligibility feed re-evaluates it
        await db.SaveChangesAsync(ct);
        return ServiceResult<AudienceRuleView>.Success(ToView(rule));
    }

    public async Task<ServiceResult<bool>> DeleteRuleAsync(Guid actorUserId, Guid orgId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        if (!await EventInOrgAsync(eventId, orgId, ct)) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(actorUserId, eventId, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");
        var rule = await db.AudienceRules.FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        if (rule is null) return ServiceResult<bool>.Success(true);   // already open — idempotent
        db.AudienceRules.Remove(rule);
        db.AuditLogs.Add(new AuditLog { ActorType = "user", ActorId = actorUserId, Action = "audience.delete", Entity = "events", EntityId = eventId });
        db.OutboxMessages.Add(Search.SearchReindex.Message(eventId));   // V3 §15 (Phase 16): rule removed → reindex (event becomes open)
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<EligibilityResult> EvaluateAsync(Guid? userId, Guid eventId, CancellationToken ct = default)
    {
        // D-266 M6 (D9 rule 1) — the invitation gate, evaluated BEFORE the no-rule early return below.
        // An `Invite Only` event almost never has an AudienceRule as well, so checking this after the
        // `rule is null` return would leave every invite-only event wide open — the policy would be a label
        // with no behaviour, which is the exact failure D-266 exists to remove.
        //
        // This is the ONE eligibility engine (every ticket-issuing, admission and discovery path calls it),
        // so putting the rule here is what stops each caller growing its own copy. Whether the person is
        // invited is asked of IInvitationService, which owns that definition for both D9 methods.
        var policy = await db.Events.AsNoTracking().Where(e => e.Id == eventId)
            .Select(e => e.RegistrationPolicy).FirstOrDefaultAsync(ct);
        if (policy == EventRegistrationPolicy.InviteOnly)
        {
            // A guest has no identity to match an invitation against. Invite-only is by definition a named
            // guest list, so anonymous checkout cannot satisfy it.
            if (userId is null) return new EligibilityResult(false, "not_invited");
            if (!await invitations.HasAccessAsync(userId.Value, eventId, ct))
                return new EligibilityResult(false, "not_invited");
        }

        var rule = await db.AudienceRules.AsNoTracking().FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        if (rule is null) return new EligibilityResult(true, null);          // no rule ⇒ open (backward compatible)

        var orgId = await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.RepresentingOrgId).FirstOrDefaultAsync(ct);

        // Guest (no account): only the guests allowance decides.
        if (userId is null)
            return rule.GuestsAllowed ? new EligibilityResult(true, null) : new EligibilityResult(false, "guests_not_allowed");

        var m = await db.Memberships.AsNoTracking().FirstOrDefaultAsync(x => x.OrgId == orgId && x.UserId == userId, ct);
        if (m is null)
            return rule.ExternalOrgsAllowed ? new EligibilityResult(true, null) : new EligibilityResult(false, "not_a_member");

        // Membership present — every specified predicate condition must hold (deny by default).
        // require_verified needs verified_at AND rejects SELF_DECLARED (V3 §4.3): a self-attested membership,
        // even if marked verified, never satisfies a consequential gate.
        if (rule.RequireVerified && (!m.IsVerified || m.Source == MembershipSource.SelfDeclared))
            return new EligibilityResult(false, "require_verified");

        var roleIn = Deser<List<string>>(rule.RoleInJson);
        if (roleIn is { Count: > 0 } && !roleIn.Contains(m.Role.ToString())) return new EligibilityResult(false, "role_not_eligible");

        var cohortIn = Deser<List<int>>(rule.CohortYearInJson);
        if (cohortIn is { Count: > 0 })
        {
            var cohort = GetCohortYear(m.AttributesJson);
            if (cohort is null || !cohortIn.Contains(cohort.Value)) return new EligibilityResult(false, "cohort_not_eligible");
        }

        var attrMatch = Deser<Dictionary<string, string>>(rule.AttributeMatchesJson);
        if (attrMatch is { Count: > 0 } && !AttributesMatch(m.AttributesJson, attrMatch)) return new EligibilityResult(false, "attribute_not_eligible");

        var unitSubtree = Deser<List<Guid>>(rule.UnitSubtreeInJson);
        if (unitSubtree is { Count: > 0 })
        {
            // The member's effective unit is the org root today (no unit-scoped memberships yet). Eligible if
            // the root sits under one of the listed subtrees (path prefix). A rule naming a deeper sub-unit
            // therefore matches no one until unit-scoped memberships exist — the honest current-granularity behaviour.
            var rootPath = await db.OrgUnits.AsNoTracking()
                .Where(u => u.OrgId == orgId && u.ParentId == null).Select(u => u.Path).FirstOrDefaultAsync(ct);
            var subtreePaths = await db.OrgUnits.AsNoTracking()
                .Where(u => u.OrgId == orgId && unitSubtree.Contains(u.Id)).Select(u => u.Path).ToListAsync(ct);
            if (rootPath is null || !subtreePaths.Any(p => !string.IsNullOrEmpty(p) && rootPath.StartsWith(p, StringComparison.Ordinal)))
                return new EligibilityResult(false, "unit_not_eligible");
        }

        return new EligibilityResult(true, null);
    }

    public async Task<ServiceResult<bool>> SetMemberAttributesAsync(Guid actorUserId, Guid orgId, Guid membershipId, bool isAdmin, MemberAttributesInput input, CancellationToken ct = default)
    {
        var m = await db.Memberships.FirstOrDefaultAsync(x => x.Id == membershipId && x.OrgId == orgId, ct);
        if (m is null) return ServiceResult<bool>.Fail("not_found");
        // Genuinely org-scoped: this edits a *membership's* attributes, not anything belonging to an event,
        // so it resolves against the organization rather than through the event authority.
        if (!(await authority.ResolveOrgAsync(actorUserId, orgId, isAdmin, ct)).CanManage)
            return ServiceResult<bool>.Fail("forbidden");

        if (input.Source is not null)
        {
            if (!Enum.TryParse<MembershipSource>(input.Source, ignoreCase: true, out var src)) return ServiceResult<bool>.Fail("invalid_source");
            m.Source = src;
        }

        var attrs = new Dictionary<string, object>();
        foreach (var (k, v) in input.Attributes ?? new Dictionary<string, string>()) attrs[k] = v;
        if (input.CohortYear is not null) attrs["cohort_year"] = input.CohortYear.Value;   // stored as a number
        m.AttributesJson = attrs.Count > 0 ? JsonSerializer.Serialize(attrs, J) : null;

        db.AuditLogs.Add(new AuditLog { ActorType = "user", ActorId = actorUserId, Action = "audience.member_attributes", Entity = "memberships", EntityId = m.Id });
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // ── helpers ──────────────────────────────────────────────────────────────


    private async Task<bool> EventInOrgAsync(Guid eventId, Guid orgId, CancellationToken ct)
        => await db.Events.AsNoTracking().AnyAsync(e => e.Id == eventId && e.RepresentingOrgId == orgId, ct);

    private static string? Serialize<T>(IReadOnlyList<T>? list) => list is { Count: > 0 } ? JsonSerializer.Serialize(list, J) : null;
    private static T? Deser<T>(string? json) => string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json!, J);

    private static int? GetCohortYear(string? attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson)) return null;
        using var doc = JsonDocument.Parse(attributesJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("cohort_year", out var el)) return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s)) return s;
        return null;
    }

    private static bool AttributesMatch(string? memberAttrsJson, Dictionary<string, string> required)
    {
        var attrs = Deser<Dictionary<string, JsonElement>>(memberAttrsJson) ?? new();
        foreach (var (k, v) in required)
        {
            if (!attrs.TryGetValue(k, out var el)) return false;
            var actual = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
            if (actual != v) return false;
        }
        return true;
    }

    private static AudienceRuleView ToView(AudienceRule r) => new(
        r.EventId,
        Deser<List<Guid>>(r.UnitSubtreeInJson) ?? new(),
        Deser<List<string>>(r.RoleInJson) ?? new(),
        Deser<List<int>>(r.CohortYearInJson) ?? new(),
        Deser<Dictionary<string, string>>(r.AttributeMatchesJson) ?? new(),
        r.RequireVerified, r.ExternalOrgsAllowed, r.AppliesTo.ToString(),
        r.GuestsAllowed, r.GuestPerRegistrantCap, r.GuestsRequireApproval);
}
