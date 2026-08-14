using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Authoritative Template system (V3 §13.1, activated Phase 15). Every write runs ConfigJson through
/// <see cref="ValidateConfigAsync"/> — the single closed-registry validation pipeline (a future AI generator submits
/// its draft through the same door). Templates are declarative: they never carry dates/inventory/financials, and
/// applying one to an event only overlays the capability preset + declarative defaults, never money/runtime rows.</summary>
public class TemplateService(KurxDbContext db, IEventAuthority authority, ICapabilityService capabilities) : ITemplateService
{
    public async Task<ServiceResult<TemplateView>> CreateAsync(Guid userId, bool isAdmin, TemplateInput input, CancellationToken ct = default)
    {
        if (!Enum.TryParse<TemplateScope>(input.Scope, ignoreCase: true, out var scope) || !Enum.IsDefined(scope))
            return ServiceResult<TemplateView>.Fail("invalid_scope");

        var name = input.Name.Trim();
        if (name.Length is < 2 or > 100) return ServiceResult<TemplateView>.Fail("invalid_name");

        var authError = await AuthorizeScopeAsync(userId, isAdmin, scope, input.OrgId, input.OrgUnitId, ct);
        if (authError is not null) return ServiceResult<TemplateView>.Fail(authError);

        var configJson = input.ConfigJson ?? "{}";
        var validation = await ValidateConfigAsync(configJson, ct);
        if (!validation.Ok) return ServiceResult<TemplateView>.Fail(validation.Error!);

        var template = new EventTemplate
        {
            Scope = scope,
            OrgId = scope is TemplateScope.Org or TemplateScope.Unit ? input.OrgId : null,
            OrgUnitId = scope == TemplateScope.Unit ? input.OrgUnitId : null,
            OwnerUserId = scope == TemplateScope.Personal ? userId : null,
            Name = name,
            Slug = await UniqueSlugAsync(name, ct),
            Description = input.Description ?? "",
            KindSlug = input.KindSlug,
            ConfigJson = configJson,
            DefaultSectionsJson = input.DefaultSectionsJson ?? "[]",
            IsSystem = false,
            Version = 1,
            State = TemplateState.Draft,
        };
        template.RootTemplateId = template.Id;   // v1 is the family root
        db.EventTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return ServiceResult<TemplateView>.Success(ToView(template));
    }

    public async Task<ServiceResult<TemplateView>> UpdateAsync(Guid userId, Guid templateId, bool isAdmin,
        string? name, string? description, string? kindSlug, string? configJson, string? defaultSectionsJson, CancellationToken ct = default)
    {
        var t = await db.EventTemplates.FirstOrDefaultAsync(x => x.Id == templateId && x.DeletedAt == null, ct);
        if (t is null) return ServiceResult<TemplateView>.Fail("not_found");
        if (t.IsSystem) return ServiceResult<TemplateView>.Fail("system_template");
        if (t.State != TemplateState.Draft) return ServiceResult<TemplateView>.Fail("not_draft");   // a Published version is immutable
        var authError = await AuthorizeOwnedAsync(userId, isAdmin, t, ct);
        if (authError is not null) return ServiceResult<TemplateView>.Fail(authError);

        if (name is not null)
        {
            var trimmed = name.Trim();
            if (trimmed.Length is < 2 or > 100) return ServiceResult<TemplateView>.Fail("invalid_name");
            t.Name = trimmed;
        }
        if (description is not null) t.Description = description;
        if (kindSlug is not null) t.KindSlug = kindSlug.Length == 0 ? null : kindSlug;
        if (defaultSectionsJson is not null) t.DefaultSectionsJson = defaultSectionsJson;
        if (configJson is not null)
        {
            var validation = await ValidateConfigAsync(configJson, ct);
            if (!validation.Ok) return ServiceResult<TemplateView>.Fail(validation.Error!);
            t.ConfigJson = configJson;
        }
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<TemplateView>.Success(ToView(t));
    }

    public async Task<ServiceResult<TemplateView>> PublishAsync(Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default)
    {
        var t = await db.EventTemplates.FirstOrDefaultAsync(x => x.Id == templateId && x.DeletedAt == null, ct);
        if (t is null) return ServiceResult<TemplateView>.Fail("not_found");
        if (t.State != TemplateState.Draft) return ServiceResult<TemplateView>.Fail("not_draft");
        var authError = await AuthorizeOwnedAsync(userId, isAdmin, t, ct);
        if (authError is not null) return ServiceResult<TemplateView>.Fail(authError);

        // Re-validate at publish — the registry is closed but could have changed since the draft was written.
        var validation = await ValidateConfigAsync(t.ConfigJson, ct);
        if (!validation.Ok) return ServiceResult<TemplateView>.Fail(validation.Error!);

        t.State = TemplateState.Published;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<TemplateView>.Success(ToView(t));
    }

    public async Task<ServiceResult<TemplateView>> NewVersionAsync(Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default)
    {
        var t = await db.EventTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == templateId && x.DeletedAt == null, ct);
        if (t is null) return ServiceResult<TemplateView>.Fail("not_found");
        if (t.IsSystem) return ServiceResult<TemplateView>.Fail("system_template");
        var authError = await AuthorizeOwnedAsync(userId, isAdmin, t, ct);
        if (authError is not null) return ServiceResult<TemplateView>.Fail(authError);

        // Fail if a Draft already exists for this family — one editable head at a time.
        if (await db.EventTemplates.AnyAsync(x => x.RootTemplateId == t.RootTemplateId && x.State == TemplateState.Draft && x.DeletedAt == null, ct))
            return ServiceResult<TemplateView>.Fail("draft_exists");

        var nextVersion = await db.EventTemplates.Where(x => x.RootTemplateId == t.RootTemplateId).MaxAsync(x => x.Version, ct) + 1;
        var draft = new EventTemplate
        {
            RootTemplateId = t.RootTemplateId,
            Version = nextVersion,
            State = TemplateState.Draft,
            Scope = t.Scope,
            OrgId = t.OrgId,
            OrgUnitId = t.OrgUnitId,
            OwnerUserId = t.OwnerUserId,
            Name = t.Name,
            Slug = await UniqueSlugAsync(t.Name, ct),
            Description = t.Description,
            KindSlug = t.KindSlug,
            ConfigJson = t.ConfigJson,
            DefaultSectionsJson = t.DefaultSectionsJson,
            IsSystem = false,
        };
        db.EventTemplates.Add(draft);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)   // a concurrent version-creation won the unique (RootTemplateId, Version) index —
        {                           // the DB constraint stays authoritative; report the same conflict, no 500.
            return ServiceResult<TemplateView>.Fail("draft_exists");
        }
        return ServiceResult<TemplateView>.Success(ToView(draft));
    }

    public async Task<ServiceResult<TemplateView>> ArchiveAsync(Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default)
    {
        var t = await db.EventTemplates.FirstOrDefaultAsync(x => x.Id == templateId && x.DeletedAt == null, ct);
        if (t is null) return ServiceResult<TemplateView>.Fail("not_found");
        if (t.IsSystem) return ServiceResult<TemplateView>.Fail("system_template");
        var authError = await AuthorizeOwnedAsync(userId, isAdmin, t, ct);
        if (authError is not null) return ServiceResult<TemplateView>.Fail(authError);

        t.State = TemplateState.Archived;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<TemplateView>.Success(ToView(t));
    }

    public async Task<ServiceResult<TemplateView>> CloneAsync(Guid userId, Guid templateId, bool isAdmin, string? newName, CancellationToken ct = default)
    {
        var src = await db.EventTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == templateId && x.DeletedAt == null, ct);
        // A caller may clone only a template it may VIEW — return not_found otherwise (hide existence), so a Personal
        // template or another org's Draft config can't be exfiltrated by guessing an id.
        if (src is null || !await CanViewAsync(userId, isAdmin, src, ct)) return ServiceResult<TemplateView>.Fail("not_found");
        // A clone lands in the caller's own space: platform→admin, else a Personal template. Never writes into
        // someone else's org/unit.
        var scope = isAdmin ? TemplateScope.Platform : TemplateScope.Personal;
        var name = string.IsNullOrWhiteSpace(newName) ? $"{src.Name} (Copy)" : newName.Trim();
        if (name.Length is < 2 or > 100) return ServiceResult<TemplateView>.Fail("invalid_name");

        var clone = new EventTemplate
        {
            Scope = scope,
            OwnerUserId = scope == TemplateScope.Personal ? userId : null,
            Name = name,
            Slug = await UniqueSlugAsync(name, ct),
            Description = src.Description,
            KindSlug = src.KindSlug,
            ConfigJson = src.ConfigJson,
            DefaultSectionsJson = src.DefaultSectionsJson,
            IsSystem = false,
            Version = 1,
            State = TemplateState.Draft,
        };
        clone.RootTemplateId = clone.Id;   // a clone is a NEW family
        db.EventTemplates.Add(clone);
        await db.SaveChangesAsync(ct);
        return ServiceResult<TemplateView>.Success(ToView(clone));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default)
    {
        var t = await db.EventTemplates.FirstOrDefaultAsync(x => x.Id == templateId, ct);
        if (t is null) return ServiceResult<bool>.Fail("not_found");
        if (t.IsSystem) return ServiceResult<bool>.Fail("system_template");
        var authError = await AuthorizeOwnedAsync(userId, isAdmin, t, ct);
        if (authError is not null) return ServiceResult<bool>.Fail(authError);
        // Events reference the family root — never delete a family any event snapshot from.
        if (await db.Events.AnyAsync(e => e.TemplateId == t.RootTemplateId, ct)) return ServiceResult<bool>.Fail("template_in_use");

        // Delete the whole family (every version), so a partial family can never linger.
        await db.EventTemplates.Where(x => x.RootTemplateId == t.RootTemplateId).ExecuteDeleteAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<TemplateView>> ListForContextAsync(Guid? orgId, Guid? orgUnitId, Guid? ownerUserId, CancellationToken ct = default)
    {
        // Every Published version visible in this context; then reduce to the latest version per family and rank by
        // specificity (Personal > Unit > Org > Platform) so the picker shows the most-specific template first (§13.1).
        var rows = await db.EventTemplates.AsNoTracking()
            .Where(t => t.State == TemplateState.Published && t.DeletedAt == null && (
                t.Scope == TemplateScope.Platform
                || (orgId != null && t.Scope == TemplateScope.Org && t.OrgId == orgId)
                || (orgUnitId != null && t.Scope == TemplateScope.Unit && t.OrgUnitId == orgUnitId)
                || (ownerUserId != null && t.Scope == TemplateScope.Personal && t.OwnerUserId == ownerUserId)))
            .ToListAsync(ct);

        return rows
            .GroupBy(t => t.RootTemplateId)
            .Select(g => g.OrderByDescending(t => t.Version).First())
            .OrderByDescending(t => Specificity(t.Scope)).ThenBy(t => t.Name)
            .Select(ToView).ToList();
    }

    public async Task<ServiceResult<int>> ApplyToEventAsync(Guid eventId, Guid rootTemplateId, CancellationToken ct = default)
    {
        var t = await db.EventTemplates.AsNoTracking()
            .Where(x => x.RootTemplateId == rootTemplateId && x.State == TemplateState.Published && x.DeletedAt == null)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (t is null) return ServiceResult<int>.Fail("template_not_published");

        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<int>.Fail("not_found");

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(t.ConfigJson) ? "{}" : t.ConfigJson);
        var root = doc.RootElement;

        // Overlay the capability preset onto event_capabilities (upsert). Validation guarantees every declared slug
        // exists in the registry and its state is a non-Off CapabilityState — so this never invents rows or removes
        // the Kind-materialised ones; it only sets/adds the template's declared capabilities. §11.3 mode-gating stays
        // authoritative: a capability unavailable in the event's mode is skipped, never persisted (CapabilityService's
        // AvailableModes is the source of truth — the same check its resolver makes).
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("capabilities", out var caps) && caps.ValueKind == JsonValueKind.Object)
        {
            var modeName = ev.EventMode.ToString();
            var availableModes = (await capabilities.ListRegistryAsync(ct)).ToDictionary(c => c.Slug, c => c.AvailableModes, StringComparer.Ordinal);
            var existing = await db.EventCapabilities.Where(x => x.EventId == eventId).ToDictionaryAsync(x => x.CapabilitySlug, ct);
            foreach (var cap in caps.EnumerateObject())
            {
                if (!availableModes.TryGetValue(cap.Name, out var modes) || !modes.Contains(modeName)) continue;   // §11.3: mode wins
                var state = cap.Value.TryGetProperty("state", out var s) && s.ValueKind == JsonValueKind.String
                    ? Enum.Parse<CapabilityState>(s.GetString()!, ignoreCase: true) : CapabilityState.On;
                var cfg = cap.Value.TryGetProperty("config", out var c) ? c.GetRawText() : null;
                if (existing.TryGetValue(cap.Name, out var row)) { row.State = state; if (cfg is not null) row.ConfigJson = cfg; }
                else db.EventCapabilities.Add(new EventCapability { EventId = eventId, CapabilitySlug = cap.Name, State = state, ConfigJson = cfg });
            }
        }

        // Declarative default: timezone (only when the template declares one and the event still holds the platform
        // default — an explicit organiser choice always wins). Branding / default-audience are carried by the
        // template and versioned, but wiring them into their own subsystems is deferred to those organiser surfaces.
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("timezone", out var tz)
            && tz.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tz.GetString())
            && ev.Timezone == "Asia/Kolkata")
            ev.Timezone = tz.GetString()!;

        ev.CreatedFromTemplateVersion = t.Version;
        await db.SaveChangesAsync(ct);
        return ServiceResult<int>.Success(t.Version);
    }

    public async Task<ServiceResult<bool>> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(configJson) || configJson.Trim() == "{}") return ServiceResult<bool>.Success(true);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(configJson); }
        catch (JsonException) { return ServiceResult<bool>.Fail("invalid_config_json"); }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return ServiceResult<bool>.Fail("invalid_config_json");
            if (!root.TryGetProperty("capabilities", out var caps)) return ServiceResult<bool>.Success(true);
            if (caps.ValueKind != JsonValueKind.Object) return ServiceResult<bool>.Fail("invalid_config_json");

            var registry = (await capabilities.ListRegistryAsync(ct)).Select(c => c.Slug).ToHashSet(StringComparer.Ordinal);
            foreach (var cap in caps.EnumerateObject())
            {
                if (!registry.Contains(cap.Name)) return ServiceResult<bool>.Fail("unknown_capability");   // closed registry (§11)
                if (cap.Value.ValueKind != JsonValueKind.Object) return ServiceResult<bool>.Fail("invalid_config_json");
                if (cap.Value.TryGetProperty("state", out var st))
                {
                    if (st.ValueKind != JsonValueKind.String || !Enum.TryParse<CapabilityState>(st.GetString(), ignoreCase: true, out var state) || !Enum.IsDefined(state))
                        return ServiceResult<bool>.Fail("invalid_capability_state");
                    if (state == CapabilityState.Off) return ServiceResult<bool>.Fail("capability_disabled");   // a template turns capabilities on, never references a disabled one
                }
                // workspace_tab is registry-governed (§20) — a template may not redeclare it.
                if (cap.Value.TryGetProperty("config", out var cfg) && cfg.ValueKind == JsonValueKind.Object && cfg.TryGetProperty("workspace_tab", out _))
                    return ServiceResult<bool>.Fail("invalid_workspace_declaration");
            }
            return ServiceResult<bool>.Success(true);
        }
    }

    // ── scope authority ──────────────────────────────────────────────────────────
    private async Task<string?> AuthorizeScopeAsync(Guid userId, bool isAdmin, TemplateScope scope, Guid? orgId, Guid? orgUnitId, CancellationToken ct)
        => scope switch
        {
            TemplateScope.Platform => isAdmin ? null : "forbidden",
            TemplateScope.Personal => null,   // any authenticated user owns their own templates
            TemplateScope.Org => orgId is null ? "invalid_org"
                : (await authority.ResolveOrgAsync(userId, orgId.Value, isAdmin, ct)).CanManage ? null : "forbidden",
            TemplateScope.Unit => orgId is null || orgUnitId is null ? "invalid_org"
                : !await db.OrgUnits.AnyAsync(u => u.Id == orgUnitId && u.OrgId == orgId, ct) ? "invalid_org_unit"
                : (await authority.ResolveOrgAsync(userId, orgId.Value, isAdmin, ct)).CanManage ? null : "forbidden",
            _ => "invalid_scope",
        };

    // Who may VIEW/copy a template: admin anyone · Platform is the shared catalog · Personal is owner-only · an
    // Org/Unit template is visible to a member of its org (any role). Mirrors the scope model — used to gate clone.
    private async Task<bool> CanViewAsync(Guid userId, bool isAdmin, EventTemplate t, CancellationToken ct) => t.Scope switch
    {
        _ when isAdmin => true,
        TemplateScope.Platform => true,
        TemplateScope.Personal => t.OwnerUserId == userId,
        _ => t.OrgId is not null && (await authority.ResolveOrgAsync(userId, t.OrgId.Value, isAdmin, ct)).IsMember,
    };

    private async Task<string?> AuthorizeOwnedAsync(Guid userId, bool isAdmin, EventTemplate t, CancellationToken ct)
        => t.Scope switch
        {
            TemplateScope.Platform => isAdmin ? null : "forbidden",
            TemplateScope.Personal => isAdmin || t.OwnerUserId == userId ? null : "forbidden",
            _ => t.OrgId is not null && (await authority.ResolveOrgAsync(userId, t.OrgId.Value, isAdmin, ct)).CanManage ? null : "forbidden",
        };

    private static int Specificity(TemplateScope s) => s switch
    { TemplateScope.Personal => 3, TemplateScope.Unit => 2, TemplateScope.Org => 1, _ => 0 };

    private static TemplateView ToView(EventTemplate t) => new(
        t.Id, t.RootTemplateId, t.Version, t.State.ToString(), t.Scope.ToString(),
        t.OrgId, t.OrgUnitId, t.OwnerUserId, t.Name, t.Slug, t.Description, t.KindSlug,
        t.ConfigJson, t.DefaultSectionsJson, t.IsSystem, t.CreatedAt, t.UpdatedAt);

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var slug = Slugify(name);
        while (await db.EventTemplates.AnyAsync(t => t.Slug == slug, ct))
            slug = $"{Slugify(name)}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant()}";
        return slug;
    }

    private static string Slugify(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "template" : slug[..Math.Min(slug.Length, 60)];
    }
}
