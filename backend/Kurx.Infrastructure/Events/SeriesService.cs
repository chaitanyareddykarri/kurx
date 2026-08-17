using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>EventSeries (V3 §13.2, Phase 12) — RECURRING/EDITIONS lineage over the existing event model. Additive: an
/// event links to at most one series via <see cref="Event.SeriesId"/> (§3.4 rule 5). The RRULE is stored and validated
/// (RFC 5545 sanity); occurrence expansion into events is organiser-driven this phase (each occurrence is its own event
/// with its own inventory and timezone). Organiser gates reuse the org role (Owner/Manager/Representative).</summary>
public class SeriesService(KurxDbContext db, IEventAuthority authority) : ISeriesService
{
    public async Task<ServiceResult<SeriesView>> CreateAsync(Guid userId, Guid orgId, bool isAdmin, SeriesInput input, CancellationToken ct = default)
    {
        if (!await CanManageAsync(userId, orgId, isAdmin, ct)) return ServiceResult<SeriesView>.Fail("forbidden");
        if (string.IsNullOrWhiteSpace(input.Name)) return ServiceResult<SeriesView>.Fail("name_required");
        if (!TryEnum(input.Mode, out SeriesMode mode)) return ServiceResult<SeriesView>.Fail("invalid_mode");

        var series = new EventSeries { OrgId = orgId, Name = input.Name!.Trim(), Mode = mode, Slug = await UniqueSlugAsync(orgId, input.Name!, ct) };
        var error = ApplyInput(series, input, mode);
        if (error is not null) return ServiceResult<SeriesView>.Fail(error);
        db.EventSeries.Add(series);
        Audit(userId, isAdmin, "series.create", series.Id, JsonSerializer.Serialize(new { orgId, series.Name, mode = mode.ToString() }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<SeriesView>.Success(await ToViewAsync(series, ct));
    }

    public async Task<ServiceResult<SeriesView>> UpdateAsync(Guid userId, Guid seriesId, bool isAdmin, SeriesInput input, CancellationToken ct = default)
    {
        var series = await db.EventSeries.FirstOrDefaultAsync(s => s.Id == seriesId && s.DeletedAt == null, ct);
        if (series is null) return ServiceResult<SeriesView>.Fail("not_found");
        if (!await CanManageAsync(userId, series.OrgId, isAdmin, ct)) return ServiceResult<SeriesView>.Fail("forbidden");
        var oldMode = series.Mode;
        var mode = series.Mode;
        if (input.Mode is not null && (!TryEnum(input.Mode, out mode))) return ServiceResult<SeriesView>.Fail("invalid_mode");
        series.Mode = mode;
        if (!string.IsNullOrWhiteSpace(input.Name)) series.Name = input.Name!.Trim();
        var error = ApplyInput(series, input, mode);
        if (error is not null) return ServiceResult<SeriesView>.Fail(error);
        series.UpdatedAt = DateTime.UtcNow;
        // V3 §15 (Phase 16): a RECURRING↔EDITIONS mode change flips the one-listing collapse for EVERY member — reindex all.
        if (mode != oldMode)
            foreach (var mid in await db.Events.Where(e => e.SeriesId == seriesId).Select(e => e.Id).ToListAsync(ct))
                db.OutboxMessages.Add(Search.SearchReindex.Message(mid));
        await db.SaveChangesAsync(ct);
        return ServiceResult<SeriesView>.Success(await ToViewAsync(series, ct));
    }

    public async Task<ServiceResult<SeriesView>> GetAsync(Guid seriesId, CancellationToken ct = default)
    {
        var series = await db.EventSeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seriesId && s.DeletedAt == null, ct);
        return series is null ? ServiceResult<SeriesView>.Fail("not_found") : ServiceResult<SeriesView>.Success(await ToViewAsync(series, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<SeriesView>>> ListForOrgAsync(Guid orgId, CancellationToken ct = default)
    {
        var list = await db.EventSeries.AsNoTracking().Where(s => s.OrgId == orgId && s.DeletedAt == null).OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
        var views = new List<SeriesView>(list.Count);
        foreach (var s in list) views.Add(await ToViewAsync(s, ct));
        return ServiceResult<IReadOnlyList<SeriesView>>.Success(views);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid seriesId, bool isAdmin, CancellationToken ct = default)
    {
        var series = await db.EventSeries.FirstOrDefaultAsync(s => s.Id == seriesId && s.DeletedAt == null, ct);
        if (series is null) return ServiceResult<bool>.Fail("not_found");
        if (!await CanManageAsync(userId, series.OrgId, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        // Detach members (SetNull FK also handles this) then soft-delete the series.
        var members = await db.Events.Where(e => e.SeriesId == seriesId).ToListAsync(ct);
        foreach (var m in members) { m.SeriesId = null; m.EditionOrdinal = null; m.EditionLabel = null; }
        series.DeletedAt = DateTime.UtcNow;
        Audit(userId, isAdmin, "series.delete", seriesId, null);
        // V3 §15 (Phase 16): every detached member leaves the collapse group — reindex each.
        foreach (var m in members) db.OutboxMessages.Add(Search.SearchReindex.Message(m.Id));
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // ── Membership ─────────────────────────────────────────────────────────────
    public async Task<ServiceResult<SeriesMemberView>> AttachEventAsync(Guid userId, Guid seriesId, bool isAdmin, SeriesMemberInput input, CancellationToken ct = default)
    {
        var series = await db.EventSeries.FirstOrDefaultAsync(s => s.Id == seriesId && s.DeletedAt == null, ct);
        if (series is null) return ServiceResult<SeriesMemberView>.Fail("not_found");
        if (!await CanManageAsync(userId, series.OrgId, isAdmin, ct)) return ServiceResult<SeriesMemberView>.Fail("forbidden");
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == input.EventId && e.DeletedAt == null, ct);
        if (ev is null) return ServiceResult<SeriesMemberView>.Fail("event_not_found");
        if (ev.RepresentingOrgId != series.OrgId) return ServiceResult<SeriesMemberView>.Fail("cross_org_event");
        if (ev.SeriesId is { } sid && sid != seriesId) return ServiceResult<SeriesMemberView>.Fail("already_in_series");   // §3.4 rule 5

        ev.SeriesId = seriesId;
        // EDITIONS members carry an ordinal/label; RECURRING occurrences derive from their own StartsAt + Timezone.
        ev.EditionOrdinal = series.Mode == SeriesMode.Editions ? input.EditionOrdinal : null;
        ev.EditionLabel = series.Mode == SeriesMode.Editions ? input.EditionLabel?.Trim() : null;
        ev.UpdatedAt = DateTime.UtcNow;
        Audit(userId, isAdmin, "series.attach", seriesId, JsonSerializer.Serialize(new { eventId = ev.Id }));
        db.OutboxMessages.Add(Search.SearchReindex.Message(ev.Id));   // V3 §15 (Phase 16): joining a series changes its collapse eligibility → reindex
        await db.SaveChangesAsync(ct);
        return ServiceResult<SeriesMemberView>.Success(ToMemberView(ev));
    }

    public async Task<ServiceResult<bool>> DetachEventAsync(Guid userId, Guid seriesId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var series = await db.EventSeries.FirstOrDefaultAsync(s => s.Id == seriesId && s.DeletedAt == null, ct);
        if (series is null) return ServiceResult<bool>.Fail("not_found");
        if (!await CanManageAsync(userId, series.OrgId, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.SeriesId == seriesId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_a_member");
        ev.SeriesId = null; ev.EditionOrdinal = null; ev.EditionLabel = null; ev.UpdatedAt = DateTime.UtcNow;
        Audit(userId, isAdmin, "series.detach", seriesId, JsonSerializer.Serialize(new { eventId }));
        db.OutboxMessages.Add(Search.SearchReindex.Message(ev.Id));   // V3 §15 (Phase 16): leaving a series changes its collapse eligibility → reindex
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<SeriesMemberView>> ListMembersAsync(Guid seriesId, Guid? viewerUserId, bool isAdmin, CancellationToken ct = default)
    {
        var series = await db.EventSeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seriesId && s.DeletedAt == null, ct);
        if (series is null) return [];
        // H1: never leak non-public editions. A manager of the series' org (reusing the same org-role check as every
        // mutation) sees every member; everyone else sees only what the public discovery feed would list — the same
        // Published + Public rule the Event subsystem enforces — so Draft/Private/Unlisted editions never surface.
        var canManage = await CanManageAsync(viewerUserId ?? Guid.Empty, series.OrgId, isAdmin, ct);
        var q = db.Events.AsNoTracking().Where(e => e.SeriesId == seriesId && e.DeletedAt == null);
        // Composed, not restated: this was a hand-written copy of `EventExposure.PubliclyVisible`, which is
        // the drift that class exists to stop. D-377 added the status axis to the canonical rule; a copy is
        // a place the next axis does not reach.
        if (!canManage) q = q.Where(EventExposure.PubliclyVisible);
        return await q.OrderBy(e => e.EditionOrdinal).ThenBy(e => e.StartsAt)
            .Select(e => new SeriesMemberView(e.Id, e.Title, e.Slug, e.EditionOrdinal, e.EditionLabel, e.StartsAt, e.Timezone, e.Status.ToString()))
            .ToListAsync(ct);
    }

    // ── Followers ──────────────────────────────────────────────────────────────
    public async Task<ServiceResult<bool>> FollowAsync(Guid userId, Guid seriesId, CancellationToken ct = default)
    {
        if (!await db.EventSeries.AnyAsync(s => s.Id == seriesId && s.DeletedAt == null, ct)) return ServiceResult<bool>.Fail("not_found");
        if (!await db.EventSeriesFollowers.AnyAsync(f => f.SeriesId == seriesId && f.UserId == userId, ct))
        {
            db.EventSeriesFollowers.Add(new EventSeriesFollower { SeriesId = seriesId, UserId = userId });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { /* already following — the unique index won the race */ }
        }
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> UnfollowAsync(Guid userId, Guid seriesId, CancellationToken ct = default)
    {
        var follow = await db.EventSeriesFollowers.FirstOrDefaultAsync(f => f.SeriesId == seriesId && f.UserId == userId, ct);
        if (follow is not null) { db.EventSeriesFollowers.Remove(follow); await db.SaveChangesAsync(ct); }
        return ServiceResult<bool>.Success(true);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    /// <summary>D-269: a series belongs to the organization that runs it rather than to any one event, so
    /// this resolves against the org through the single authority instead of a private copy of the rule.</summary>
    private async Task<bool> CanManageAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct)
        => (await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct)).CanManage;

    private static string? ApplyInput(EventSeries series, SeriesInput input, SeriesMode mode)
    {
        if (input.Description is not null) series.Description = input.Description.Trim();
        if (input.BannerKey is not null) series.BannerKey = input.BannerKey;
        if (input.BrandAssetsJson is not null) series.BrandAssetsJson = input.BrandAssetsJson;
        if (mode == SeriesMode.Recurring)
        {
            if (input.Rrule is not null)
            {
                if (input.Rrule.Length == 0) series.Rrule = null;
                else if (!IsValidRrule(input.Rrule)) return "invalid_rrule";
                else series.Rrule = input.Rrule.Trim();
            }
            if (input.ExceptionDates is not null) series.ExceptionDatesJson = JsonSerializer.Serialize(input.ExceptionDates);
        }
        else { series.Rrule = null; series.ExceptionDatesJson = null; }   // EDITIONS carry no recurrence
        return null;
    }

    /// <summary>RFC-5545 sanity: the rule must declare a FREQ with a recognised value. Full occurrence expansion is
    /// deferred (organiser-driven this phase), so we validate rather than parse.</summary>
    private static bool IsValidRrule(string rrule)
    {
        var freq = rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(p => p.StartsWith("FREQ=", StringComparison.OrdinalIgnoreCase));
        if (freq is null) return false;
        return freq["FREQ=".Length..].ToUpperInvariant() is "SECONDLY" or "MINUTELY" or "HOURLY" or "DAILY" or "WEEKLY" or "MONTHLY" or "YEARLY";
    }

    private async Task<string> UniqueSlugAsync(Guid orgId, string name, CancellationToken ct)
    {
        var root = Slugify(name);
        var slug = root;
        var n = 1;
        while (await db.EventSeries.AnyAsync(s => s.OrgId == orgId && s.Slug == slug, ct)) slug = $"{root}-{++n}";
        return slug;
    }

    private static string Slugify(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Trim().ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '-' or '_' && sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "series-" + Guid.NewGuid().ToString("N")[..6] : slug.Length > 60 ? slug[..60].Trim('-') : slug;
    }

    private void Audit(Guid userId, bool isAdmin, string action, Guid entityId, string? detailsJson)
        => db.AuditLogs.Add(new AuditLog { ActorType = isAdmin ? "admin" : "user", ActorId = userId, Action = action, Entity = "event_series", EntityId = entityId, DetailsJson = detailsJson });

    private async Task<SeriesView> ToViewAsync(EventSeries s, CancellationToken ct) => new(s.Id, s.OrgId, s.Name, s.Slug, s.Mode.ToString(),
        s.Description, s.BannerKey, s.BrandAssetsJson, s.Rrule, s.ExceptionDatesJson,
        await db.EventSeriesFollowers.CountAsync(f => f.SeriesId == s.Id, ct),
        await db.Events.CountAsync(e => e.SeriesId == s.Id && e.DeletedAt == null, ct), s.CreatedAt);

    private static SeriesMemberView ToMemberView(Event e) => new(e.Id, e.Title, e.Slug, e.EditionOrdinal, e.EditionLabel, e.StartsAt, e.Timezone, e.Status.ToString());

    private static bool TryEnum(string? s, out SeriesMode value)
    {
        value = SeriesMode.Recurring;
        return !string.IsNullOrWhiteSpace(s) && Enum.TryParse(s.Replace("_", ""), true, out value);
    }
}
