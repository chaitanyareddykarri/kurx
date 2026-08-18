using System.Security.Cryptography;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Analytics;
using Kurx.Infrastructure.Orgs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Events;

/// <summary>
/// Event content management (D-018): CRUD, status workflow (EventStatusWorkflow), taxonomy/venue/tag
/// associations, and the public discovery surfaces (search/upcoming/trending/featured/latest/related).
/// **Who may act is decided by <see cref="IEventAuthority"/> and nowhere else** (D-269): the event's
/// creator owns it (D-268), a Representative or an Owner/Manager seat in the represented organization is
/// an additional grant, and a platform admin outranks both. This service never re-implements that rule.
/// Ticketing (TicketType/FormField) is out of scope for this phase and untouched here.
/// </summary>
public class EventService(KurxDbContext db, ILogger<EventService> log, ITrustService trust, IAuditWriter audit,
    IChatService chat, IKindService kinds, ICapabilityService capabilities, IOrgUnitService orgUnits,
    IApprovalService approvals, ITemplateService templates, ISearchService search,
    INotificationService notifications, IAnalyticsFactSource analytics, IPlatformRoleService roles,
    WorkspaceComposer workspace, IEventAuthority authority, IEventPolicyService policy,
    IEventReviewChecklistService checklist, IStorage storage,
    Configuration.IdentityVerificationOptions identityOptions) : IEventService
{
    // V3 §15 (Phase 16): the discovery index is outbox-fed — an event write enqueues a reindex message in its OWN
    // transaction (never a synchronous dual-write); OutboxDispatchJob projects it. Enqueued on the document-affecting
    // writes (create/update/transition/feature); the projector rebuilds or removes the row from the event's live state.
    private void EnqueueReindex(Guid eventId) => db.OutboxMessages.Add(Search.SearchReindex.Message(eventId));
    public async Task<IReadOnlyList<PendingEventView>> ListInReviewAsync(int limit, CancellationToken ct = default)
    {
        // Project to an anonymous type (not the record) so EF can ORDER BY the column; map in memory.
        var rows = await db.Events.AsNoTracking()
            // D-266 M4: the queue spans both review states, so an admin sees claimed and unclaimed items
            // together rather than losing the claimed ones. The dashboard's pending_events tile counts the
            // same pair — D-058 requires a tile to agree with the queue it links to.
            .Where(e => e.Status == EventStatus.PendingReview || e.Status == EventStatus.UnderReview)
            // D-266 M7: enriched so a reviewer can triage the queue without a call per row. MeetingPassword
            // is not selected and has no field on PendingEventView — excluded by construction, not filtered.
            .Join(db.Organizations.AsNoTracking(), e => e.RepresentingOrgId, o => o.Id,
                (e, o) => new
                {
                    e.Id, e.RepresentingOrgId, OrgName = o.Name, e.Title, e.Slug, e.StartsAt, e.CreatedAt,
                    e.Status, e.Product, e.ArchetypeSlug, e.IsPaid, e.City,
                    AuthorizationStatus = db.EventAuthorizations
                        .Where(a => a.EventId == e.Id).Select(a => (EventAuthorizationStatus?)a.Status).FirstOrDefault(),
                    // D-266 M4 — so the queue shows an item is already someone's work, rather than inviting
                    // a second reviewer to start it. Name resolved here; null if that account is gone.
                    e.ReviewClaimedBy, e.ReviewClaimedAt,
                    ReviewClaimedByName = db.Users.Where(u => u.Id == e.ReviewClaimedBy)
                        .Select(u => u.Name).FirstOrDefault(),
                })
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);
        return rows.Select(x => new PendingEventView(
            x.Id, x.RepresentingOrgId, x.OrgName, x.Title, x.Slug, x.StartsAt, x.CreatedAt,
            x.Status.ToString(), x.Product.ToString(), x.ArchetypeSlug, x.IsPaid,
            x.AuthorizationStatus?.ToString(), x.City,
            x.ReviewClaimedBy, x.ReviewClaimedByName, x.ReviewClaimedAt)).ToList();
    }

    private record EventStatsRow(int Sold, int CheckedIn, long RevenuePaise, bool IsPaid);

    /// <summary>Per-event ticket/revenue aggregate shared by <see cref="ListForOrgAsync"/> and
    /// <see cref="ListForAdminAsync"/> — one grouped-query implementation, not two copies of the same SQL.
    /// Bounded to the caller's candidate set (never the whole table), each a single grouped query, no N+1.
    /// Revenue is <see cref="IAnalyticsFactSource.RevenueByEventAsync"/> (V3 §9.5, Phase 17) — the same
    /// canonical VAR-sourced number every other analytics surface shows, not a second, independently
    /// computed figure. Authorization for these rows is already done by the caller (per-row, since an
    /// admin list can span many orgs), so this reaches the internal fact source directly rather than through
    /// <c>IAnalyticsService</c>'s per-call authz.</summary>
    private async Task<Dictionary<Guid, EventStatsRow>> EventStatsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
    {
        var checkedIn = await analytics.CheckedInByEventAsync(eventIds, ct);
        var sold = await db.Tickets.AsNoTracking().Where(t => eventIds.Contains(t.EventId))
            .GroupBy(t => t.EventId)
            .Select(g => new { EventId = g.Key, Sold = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Sold, ct);
        var revenue = await analytics.RevenueByEventAsync(eventIds, ct);
        var paidEventIds = (await db.TicketTypes.AsNoTracking()
            .Where(tt => eventIds.Contains(tt.EventId) && tt.DeletedAt == null && tt.PricePaise > 0)
            .Select(tt => tt.EventId).Distinct().ToListAsync(ct)).ToHashSet();
        return eventIds.ToDictionary(id => id, id =>
            new EventStatsRow(sold.GetValueOrDefault(id), checkedIn.GetValueOrDefault(id), revenue.GetValueOrDefault(id), paidEventIds.Contains(id)));
    }

    /// <summary>D-187: fixed a real bug — the previous version had no <c>Page</c> and hard-capped the SQL
    /// query itself at <c>Math.Clamp(filter.Limit, 1, 100)</c> rows, so a platform with more than 100 events
    /// could never show row 101 no matter what page the caller asked for; every request silently returned the
    /// newest ≤100 and nothing else. Two paths now: when the sort key and every active filter are plain Event/
    /// Organization columns (the common case), pagination and the total count are done entirely in SQL and are
    /// correct at any scale. When the sort key or a filter depends on the per-event stats aggregate (revenue/
    /// registrations/attendance — not a stored column), a bounded in-memory candidate window is unavoidable
    /// (computing revenue for literally every event on every request doesn't scale); that window is now 2000
    /// rows (was 500) and pagination within it is real, not silently dropped past row 100.</summary>
    public async Task<(IReadOnlyList<AdminEventView> Items, int Total)> ListForAdminAsync(AdminEventListFilter filter, CancellationToken ct = default)
    {
        var query = db.Events.AsNoTracking().Where(e => e.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<EventStatus>(filter.Status.Trim(), ignoreCase: true, out var st))
            query = query.Where(e => e.Status == st);
        if (filter.CategoryId is { } catId) query = query.Where(e => e.CategoryId == catId || e.TypeId == catId);
        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var cityTerm = filter.City.Trim();
            query = query.Where(e => EF.Functions.ILike(e.City, $"%{cityTerm}%"));
        }
        if (!string.IsNullOrWhiteSpace(filter.Visibility) && Enum.TryParse<EventVisibility>(filter.Visibility.Trim(), ignoreCase: true, out var vis))
            query = query.Where(e => e.Visibility == vis);
        if (filter.DateFrom is { } from) query = query.Where(e => e.StartsAt >= from);
        if (filter.DateTo is { } to) query = query.Where(e => e.StartsAt <= to);

        // Search matches an exact event id, or the title/org name — same "search the row" shape as every
        // other admin list screen. Applied to the EVENTS query, before the organization join, and split
        // into two UNIONed branches rather than one OR (D-325).
        //
        // The OR is why: `Title ILIKE '%t%' OR o.Name ILIKE '%t%'` spans two tables, so PostgreSQL can only
        // evaluate it after the join and cannot push either side to an index. Measured on 200k events, a
        // trigram index on Title made **no difference to the plan at all** — same Parallel Seq Scan, 138ms.
        // Split into branches each filtering one column, both use their index: 1.8ms for the title branch,
        // and the org branch becomes an IN over a handful of ids. Moving it before the join is safe because
        // the join is an inner join on a non-null FK, so it neither adds nor removes events.
        if (Guid.TryParse(filter.Q, out var idMatch))
            query = query.Where(e => e.Id == idMatch);
        else if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var qTerm = filter.Q.Trim();
            var orgIds = db.Organizations.AsNoTracking()
                .Where(o => EF.Functions.ILike(o.Name, $"%{qTerm}%"))
                .Select(o => o.Id);
            // Union, not Concat: an event whose title AND representing organization both match must appear
            // once. Concat would duplicate it and inflate the total.
            query = query.Where(e => EF.Functions.ILike(e.Title, $"%{qTerm}%"))
                .Union(query.Where(e => orgIds.Contains(e.RepresentingOrgId)));
        }

        var joined = query.Join(db.Organizations.AsNoTracking(), e => e.RepresentingOrgId, o => o.Id, (e, o) => new { e, o });

        if (filter.VerifiedOrgOnly == true)
            joined = joined.Where(x => x.o.VerificationStatus == OrgVerificationStatus.Verified);

        var pageSize = Math.Clamp(filter.Limit, 1, 100);
        var page = Math.Max(filter.Page, 1);

        // Revenue/registration RANGE filters, and sorting by a stats-derived key, both need the per-event
        // aggregate — computed after the row set materializes, so they force the bounded in-memory path.
        var needsStatsFilter = filter.RevenueMinPaise is not null || filter.RevenueMaxPaise is not null
            || filter.RegistrationsMin is not null || filter.RegistrationsMax is not null || filter.IsPaid is not null;
        var statsScopedSort = filter.Sort is "revenue" or "registrations" or "attendance";

        // EF cannot translate an ORDER BY on a property of a freshly-constructed record
        // (`.Select(x => new AdminEventRow(...)).OrderBy(row => row.Foo)` tries to push the order-by past
        // the projection and fails at runtime: "could not be translated"). The safe, standard shape is
        // order → paginate → project, all applied to the raw joined entity, project last.
        List<AdminEventView> pageItems;
        int total;

        if (!needsStatsFilter && !statsScopedSort)
        {
            // Every active filter and the sort key are plain columns — real SQL pagination, correct at any scale.
            total = await joined.CountAsync(ct);
            var orderedJoined = filter.Sort switch
            {
                "updated" => joined.OrderByDescending(x => x.e.UpdatedAt),
                "date" => joined.OrderBy(x => x.e.StartsAt),
                _ => joined.OrderByDescending(x => x.e.CreatedAt),
            };
            var rows = await orderedJoined.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AdminEventRow(
                x.e.Id, x.e.RepresentingOrgId, x.o.Name, x.e.Title, x.e.Slug, x.e.Status, x.e.IsFeatured,
                x.e.StartsAt, x.e.EndsAt, x.e.CreatedAt, x.e.UpdatedAt, x.e.CategoryId, x.e.TypeId,
                x.e.Visibility, x.e.City, x.e.VenueName, x.e.Capacity, x.e.IsSuspended, x.e.SuspendedReason,
                x.e.IsHidden, x.e.HiddenReason, x.e.BannerKey, x.e.SettlementCurrency, x.o.VerificationStatus))
                .ToListAsync(ct);
            var catNames = await CategoryNamesAsync(rows.Select(r => r.CategoryId).Concat(rows.Where(r => r.TypeId is not null).Select(r => r.TypeId!.Value)), ct);
            var stats = await EventStatsAsync(rows.Select(r => r.Id).ToList(), ct);
            pageItems = rows.Select(r => ToView(r, catNames, stats[r.Id])).ToList();
        }
        else
        {
            // Bounded candidate window — the per-event stats aggregate can't be filtered/sorted in SQL, and
            // computing it for the whole table on every request doesn't scale. 2000 is generous for admin-scale
            // platforms (thousands of events); beyond that this is a known, documented approximation, not a bug.
            const int candidateCap = 2000;
            var rows = await joined.OrderByDescending(x => x.e.CreatedAt).Take(candidateCap).Select(x => new AdminEventRow(
                x.e.Id, x.e.RepresentingOrgId, x.o.Name, x.e.Title, x.e.Slug, x.e.Status, x.e.IsFeatured,
                x.e.StartsAt, x.e.EndsAt, x.e.CreatedAt, x.e.UpdatedAt, x.e.CategoryId, x.e.TypeId,
                x.e.Visibility, x.e.City, x.e.VenueName, x.e.Capacity, x.e.IsSuspended, x.e.SuspendedReason,
                x.e.IsHidden, x.e.HiddenReason, x.e.BannerKey, x.e.SettlementCurrency, x.o.VerificationStatus))
                .ToListAsync(ct);
            var catNames = await CategoryNamesAsync(rows.Select(r => r.CategoryId).Concat(rows.Where(r => r.TypeId is not null).Select(r => r.TypeId!.Value)), ct);
            var stats = await EventStatsAsync(rows.Select(r => r.Id).ToList(), ct);

            IEnumerable<AdminEventView> views = rows.Select(r => ToView(r, catNames, stats[r.Id]));

            if (filter.IsPaid is { } paidFilter) views = views.Where(v => v.IsPaid == paidFilter);
            if (filter.RevenueMinPaise is { } rMin) views = views.Where(v => v.RevenuePaise >= rMin);
            if (filter.RevenueMaxPaise is { } rMax) views = views.Where(v => v.RevenuePaise <= rMax);
            if (filter.RegistrationsMin is { } regMin) views = views.Where(v => v.RegistrationsCount >= regMin);
            if (filter.RegistrationsMax is { } regMax) views = views.Where(v => v.RegistrationsCount <= regMax);

            views = filter.Sort switch
            {
                "revenue" => views.OrderByDescending(v => v.RevenuePaise),
                "registrations" => views.OrderByDescending(v => v.RegistrationsCount),
                "attendance" => views.OrderByDescending(v => v.CheckedIn),
                "updated" => views.OrderByDescending(v => v.UpdatedAt),
                "date" => views.OrderBy(v => v.StartsAt),
                _ => views.OrderByDescending(v => v.CreatedAt),
            };

            var filtered = views.ToList();
            total = filtered.Count;
            pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        return (pageItems, total);
    }

    private async Task<Dictionary<Guid, string>> CategoryNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var rows = await db.EventCategories.AsNoTracking().Where(c => distinct.Contains(c.Id))
            .Select(c => new { c.Id, c.Name }).ToListAsync(ct);

        // D-266 M3 Step 6 Phase 3 — the runtime alias layer. Every type name leaves the backend canonical,
        // whatever the row still says, so an event created against a legacy type keeps working and no client
        // ever sees a retired name. This is the single projection point for type names, which is why the
        // alias belongs here rather than at each call site.
        return rows.ToDictionary(r => r.Id, r => TaxonomyAliasMap.Resolve(r.Name));
    }

    /// <summary>Plain-column projection shared by both the SQL-paginated and in-memory-candidate branches of
    /// <see cref="ListForAdminAsync"/> — one shape, so <see cref="ToView"/> isn't duplicated per branch.</summary>
    private record AdminEventRow(Guid Id, Guid OrgId, string OrgName, string Title, string Slug, EventStatus Status,
        bool IsFeatured, DateTime StartsAt, DateTime EndsAt, DateTime CreatedAt, DateTime UpdatedAt,
        Guid CategoryId, Guid? TypeId, EventVisibility Visibility, string City, string VenueName, int? Capacity,
        bool IsSuspended, string? SuspendedReason, bool IsHidden, string? HiddenReason, string? BannerKey,
        string SettlementCurrency, OrgVerificationStatus OrgVerificationStatus);

    private static AdminEventView ToView(AdminEventRow r, Dictionary<Guid, string> catNames, EventStatsRow s)
        => new(r.Id, r.OrgId, r.OrgName, r.Title, r.Slug, r.Status.ToString(), r.IsFeatured,
            r.StartsAt, r.CreatedAt, catNames.GetValueOrDefault(r.CategoryId),
            r.TypeId is { } tid ? catNames.GetValueOrDefault(tid) : null,
            r.Visibility.ToString(), r.City, r.VenueName, r.Capacity, r.EndsAt, s.IsPaid, r.OrgVerificationStatus.ToString(),
            r.UpdatedAt, r.IsSuspended, r.SuspendedReason, r.IsHidden, r.HiddenReason, r.BannerKey,
            s.Sold, s.CheckedIn, s.Sold, s.RevenuePaise, r.SettlementCurrency);

    public async Task<ServiceResult<AdminEventView>> SetFeaturedAsync(Guid eventId, bool featured, CancellationToken ct = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<AdminEventView>.Fail("not_found");
        ev.IsFeatured = featured;
        ev.UpdatedAt = DateTime.UtcNow;
        EnqueueReindex(ev.Id);   // curation change → refresh the featured feed
        await db.SaveChangesAsync(ct);
        return ServiceResult<AdminEventView>.Success(await ToAdminEventViewAsync(ev, ct));
    }

    // D-186: admin moderation overrides, same shape as SetFeaturedAsync — orthogonal to Status, each
    // enqueues a reindex so a suspended/hidden event drops out of public discovery on the next projection
    // (SearchIndexService.ProjectAsync excludes IsSuspended/IsHidden), and writes an audit event.
    public Task<ServiceResult<AdminEventView>> SuspendAsync(Guid eventId, string? reason, CancellationToken ct = default)
        => SetModerationFlagAsync(eventId, "admin.event.suspend", ev => { ev.IsSuspended = true; ev.SuspendedReason = reason; }, ct);

    public Task<ServiceResult<AdminEventView>> UnsuspendAsync(Guid eventId, CancellationToken ct = default)
        => SetModerationFlagAsync(eventId, "admin.event.unsuspend", ev => { ev.IsSuspended = false; ev.SuspendedReason = null; }, ct);

    public Task<ServiceResult<AdminEventView>> HideAsync(Guid eventId, string? reason, CancellationToken ct = default)
        => SetModerationFlagAsync(eventId, "admin.event.hide", ev => { ev.IsHidden = true; ev.HiddenReason = reason; }, ct);

    public Task<ServiceResult<AdminEventView>> UnhideAsync(Guid eventId, CancellationToken ct = default)
        => SetModerationFlagAsync(eventId, "admin.event.unhide", ev => { ev.IsHidden = false; ev.HiddenReason = null; }, ct);

    private async Task<ServiceResult<AdminEventView>> SetModerationFlagAsync(Guid eventId, string auditAction,
        Action<Event> apply, CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<AdminEventView>.Fail("not_found");
        apply(ev);
        ev.UpdatedAt = DateTime.UtcNow;
        EnqueueReindex(ev.Id);
        audit.Write(new AuditEvent(auditAction, "events", ev.Id, ActorType: "admin"));
        await db.SaveChangesAsync(ct);
        return ServiceResult<AdminEventView>.Success(await ToAdminEventViewAsync(ev, ct));
    }

    public async Task<ServiceResult<bool>> NotifyOrganizerAsync(Guid eventId, Guid actorId, string kind, string title, string message, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().Where(e => e.Id == eventId)
            .Select(e => new { e.CreatedBy, e.Title }).FirstOrDefaultAsync(ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        await notifications.NotifyAsync(ev.CreatedBy, kind, title, message, new { event_id = eventId }, ct);
        audit.Write(new AuditEvent(kind == "admin.warning" ? "admin.event.warning" : "admin.event.message",
            "events", eventId, ActorType: "admin", ActorId: actorId, After: new { message }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    /// <summary>Single-event admin view assembly, reusing <see cref="EventStatsAsync"/> so a moderation
    /// action's response carries the same stats the list already shows — never a second computation.</summary>
    private async Task<AdminEventView> ToAdminEventViewAsync(Event ev, CancellationToken ct)
    {
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == ev.RepresentingOrgId)
            .Select(o => new { o.Name, o.VerificationStatus }).FirstAsync(ct);
        var catNames = await db.EventCategories.AsNoTracking()
            .Where(c => c.Id == ev.CategoryId || c.Id == ev.TypeId)
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var stats = (await EventStatsAsync([ev.Id], ct))[ev.Id];
        return new AdminEventView(ev.Id, ev.RepresentingOrgId, org.Name, ev.Title, ev.Slug, ev.Status.ToString(), ev.IsFeatured,
            ev.StartsAt, ev.CreatedAt, catNames.GetValueOrDefault(ev.CategoryId),
            ev.TypeId is { } tid ? catNames.GetValueOrDefault(tid) : null,
            ev.Visibility.ToString(), ev.City, ev.VenueName, ev.Capacity, ev.EndsAt, ev.IsPaid || stats.IsPaid,
            org.VerificationStatus.ToString(), ev.UpdatedAt, ev.IsSuspended, ev.SuspendedReason,
            ev.IsHidden, ev.HiddenReason, ev.BannerKey, stats.Sold, stats.CheckedIn, stats.Sold,
            stats.RevenuePaise, ev.SettlementCurrency);
    }

    public async Task<ServiceResult<EventDetail>> CreateAsync(Guid userId, Guid? representingOrgId, bool isAdmin, CreateEventInput input, CancellationToken ct = default)
    {
        // Representation authority — NOT ownership. Representing an institution requires a manage-capable
        // seat in it; representing yourself requires nothing, because you are not asking to act for anyone
        // but yourself. Ownership is established below by CreatedBy (D-268).
        if (representingOrgId is { } orgToRepresent)
        {
            // Representation authority is an ORGANIZATION question ("may I act for this institution?"),
            // resolved against the org because there is no event yet to resolve against (D-269).
            if (!(await authority.ResolveOrgAsync(userId, orgToRepresent, isAdmin, ct)).CanManage)
                return ServiceResult<EventDetail>.Fail("forbidden");
        }

        /*
         * D-353 — a PUBLIC event must represent a real organization. Self-hosting is a Private-only
         * affordance now.
         *
         * The axis is public exposure, the same one D-307 established and D-343 refined: a public event
         * carries the platform's name into discovery whether or not money moves, so there must be a
         * named, admin-verified institution answerable for it. A private event reaches no discovery
         * surface and can never sell, so there is nobody to be answerable TO and self-hosting stands.
         *
         * Resolved from the Type's ProductClass here rather than trusting a client field — `Product` is
         * derived and snapshotted (D-266 M1), and the taxonomy read is hoisted from its old position
         * further down so this refusal happens BEFORE any row is written. `taxonomy` is reused there.
         *
         * Enforced server-side because §11 of the requirement is explicit: the client filtering the
         * Personal card away is presentation, and presentation is not a security boundary.
         */
        var taxonomy = await ResolveArchetypeAsync(input.TypeId, ct);
        // D-352 — lifted outside Production, because satisfying it needs an admin-approved organization
        // and no such thing exists in dev: without this the whole public/paid flow is untestable, which is
        // the same reason the identity proofs and the consent blockers are bypassable. `RequiresRepresentation`
        // on the trust payload tells the clients the same thing, so the gate stops adding a condition the
        // server has already lifted.
        /*
         * D-379 — every event represents a real organization. This is THE creation boundary.
         *
         * Two conditions were removed from this guard, and each was load-bearing:
         *
         *   · `Product == Public` — D-353 required a real organization only of public events, so a
         *     private or unlisted one could be created representing nobody. The rule now has no product
         *     arm: visibility does not change who is answerable for an event.
         *   · `!identityOptions.Bypass` — the dev bypass lifted this along with the identity proofs.
         *     It no longer does. The bypass exists because KYC and consent are mock-backed; whether an
         *     event names an organization is neither, and a development environment that needs one seeds
         *     a real test organization instead of getting an unrepresented event.
         *
         * Two ways to arrive with no institution, and the second is the forgeable one. Omitting
         * `representingOrgId` is the honest case. NAMING the caller's own self-representation row is the
         * attack: they are its Owner, so the `CanManage` check above returns true and a non-null id
         * sails through a null check. `IsPersonal` is therefore read from the database, never trusted
         * from the client, and both refusals share one code because to the organiser they are one rule.
         */
        if (representingOrgId is not { } orgId)
            return ServiceResult<EventDetail>.Fail("representation_required");
        if (await db.Organizations.AsNoTracking()
                .AnyAsync(o => o.Id == orgId && (o.IsPersonal || o.DeletedAt != null), ct))
            return ServiceResult<EventDetail>.Fail("representation_required");

        // `ResolveSelfRepresentationAsync` used to supply a row here when the caller named none, because
        // `events.OrgId` is a non-null FK (D-055/D-075 weighed making it nullable and rejected it — 300+
        // read sites across 30 services). The FK is still non-null and that reasoning still holds; what
        // changed is that nothing may now be minted to satisfy it. The guard above is the only way past
        // this line, so a new event cannot acquire a self-representation row by any path.

        var title = input.Title.Trim();
        if (title.Length is < 2 or > 200) return ServiceResult<EventDetail>.Fail("invalid_title");
        if (input.EndsAt <= input.StartsAt) return ServiceResult<EventDetail>.Fail("invalid_dates");

        // D-188 system-impact audit: a new event may only be filed under an Active taxonomy node — Disabled/
        // Archived nodes are meant to be unselectable for anything new, and this is where that was previously
        // unenforced (the picker filtered what's *offered*, but nothing stopped a client that already held a
        // disabled node's id from submitting it anyway).
        if (!await db.EventCategories.AnyAsync(c => c.Id == input.CategoryId && c.Level == CategoryLevel.Category && c.Status == CategoryStatus.Active, ct))
            return ServiceResult<EventDetail>.Fail("invalid_category");
        if (input.TypeId is not null && !await db.EventCategories.AnyAsync(c => c.Id == input.TypeId && c.Level == CategoryLevel.Type && c.Status == CategoryStatus.Active, ct))
            return ServiceResult<EventDetail>.Fail("invalid_type");
        if (input.AudienceLevelId is not null && !await db.EventCategories.AnyAsync(c => c.Id == input.AudienceLevelId && c.Level == CategoryLevel.Audience && c.Status == CategoryStatus.Active, ct))
            return ServiceResult<EventDetail>.Fail("invalid_audience_level");
        // V3 §13.1 (Phase 15): an event snapshots from a template FAMILY (its root) — usable only if that family has
        // a Published version. Checked up front so a Draft-only template can never create a half-applied event.
        if (input.TemplateId is not null && !await db.EventTemplates.AnyAsync(
                t => t.RootTemplateId == input.TemplateId && t.State == TemplateState.Published && t.DeletedAt == null, ct))
            return ServiceResult<EventDetail>.Fail("invalid_template");
        if (input.ParentEventId is not null)
        {
            if (!await db.Events.AnyAsync(e => e.Id == input.ParentEventId && e.RepresentingOrgId == orgId, ct))
                return ServiceResult<EventDetail>.Fail("invalid_parent_event");
            if (await CompositionDepthAsync(input.ParentEventId.Value, ct) >= 3)   // §3.4 rule 1: composition depth ≤ 3
                return ServiceResult<EventDetail>.Fail("max_composition_depth");
        }

        var visibility = EventVisibility.Listed;
        if (input.Visibility is not null && !Enum.TryParse(input.Visibility, true, out visibility))
            return ServiceResult<EventDetail>.Fail("invalid_visibility");

        var ev = new Event
        {
            RepresentingOrgId = orgId,
            ParentEventId = input.ParentEventId,
            ListedStandalone = input.ParentEventId is null,   // §3.4 rule 2: roots discoverable; a sub-event opts in later
            CreatedBy = userId,
            Title = title,
            Slug = await UniqueEventSlugAsync(title, ct),
            ShortCode = await UniqueEventShortCodeAsync(ct),
            Subtitle = input.Subtitle?.Trim() ?? "",
            Description = input.Description ?? "",
            CategoryId = input.CategoryId,
            TypeId = input.TypeId,
            AudienceLevelId = input.AudienceLevelId,
            TemplateId = input.TemplateId,
            // Npgsql rejects Kind=Unspecified against a timestamptz column, and a JSON-deserialized DateTime
            // with no offset/'Z' suffix (exactly what a browser's <input type="datetime-local"> sends) always
            // comes in Unspecified. SpecifyKind marks it UTC without shifting the clock value — found live via
            // the create-event wizard (a DbUpdateException on every submit); no prior test exercised this path
            // with a real JSON body, only in-process DateTime.UtcNow values that were already Kind=Utc.
            StartsAt = DateTime.SpecifyKind(input.StartsAt, DateTimeKind.Utc),
            EndsAt = DateTime.SpecifyKind(input.EndsAt, DateTimeKind.Utc),
            Timezone = string.IsNullOrWhiteSpace(input.Timezone) ? "Asia/Kolkata" : input.Timezone,
            Capacity = input.Capacity,
            Visibility = visibility,
            Language = string.IsNullOrWhiteSpace(input.Language) ? "en" : input.Language,
            ContactEmail = input.ContactEmail ?? "",
            ContactPhone = input.ContactPhone ?? "",
            Website = input.Website ?? "",
            SocialLinksJson = input.SocialLinksJson,
            EventMode = ParseMode(input.EventMode),
            OnlineUrl = input.OnlineUrl?.Trim(),
        };

        var modeError = ValidateMode(ev.EventMode, ev.OnlineUrl);
        if (modeError is not null) return ServiceResult<EventDetail>.Fail(modeError);

        var groupError = ApplyFieldGroups(ev, input.Content, input.Legal, input.Schedule,
            input.Location, input.Eligibility, input.Commerce);
        if (groupError is not null) return ServiceResult<EventDetail>.Fail(groupError);

        var venueError = await ApplyVenueAsync(ev, orgId, input.VenueId, input.VenueName, input.VenueAddress, input.City, input.Lat, input.Lng, ct);
        if (venueError is not null) return ServiceResult<EventDetail>.Fail(venueError);

        // V3 Kind (Phase 1): stamp the analysable Kind from the chosen taxonomy. Additive — a null result
        // (unmapped) leaves the event unkinded, it never blocks creation.
        ev.KindSlug = await kinds.ResolveKindSlugAsync(ev.TypeId, ev.CategoryId, ct);

        // D-266 M1: snapshot the behaviour axis from the chosen Type. Snapshot, not resolved-on-read, so a
        // later taxonomy edit can never change how a live event behaves. Resolved at the top of this method
        // (D-353) so the public-representation rule can refuse before anything is written; reused here.
        ev.ArchetypeSlug = taxonomy.Slug;
        ev.Product = taxonomy.Product;

        // V3 §9.1 (Phase 3): bind the event's settlement currency from its Org (§3.5), validated so a bad
        // code can never propagate onto the event or its money rows. INR today.
        var orgCurrency = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == orgId).Select(o => o.SettlementCurrency).FirstOrDefaultAsync(ct);
        ev.SettlementCurrency = Kurx.Domain.Money.IsValidCurrency(orgCurrency) ? orgCurrency! : Kurx.Domain.Money.DefaultCurrency;

        // V3 §4.1 (Phase 4): bind the owning OrgUnit — the org's root unit, materialised on first need.
        // A one-node tree auto-sets with no picker (§4.1); Phase 5 lets an organiser pick a sub-unit.
        ev.OrgUnitId = await orgUnits.EnsureRootAsync(orgId, ct);

        db.Events.Add(ev);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId,
            Action = "event.create", Entity = "events", EntityId = ev.Id,
            DetailsJson = $"{{\"org_id\":\"{orgId}\",\"slug\":\"{ev.Slug}\"}}",
        });

        // V3 §13.1 (Phase 15): event insert + capability materialization + template snapshot are ONE transaction, so a
        // template-application failure can never leave a half-created event (M3). The row insert, tags, capability
        // preset, and snapshot all roll back together on any failure; nothing is observable until the commit.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true)
        {
            return ServiceResult<EventDetail>.Fail("slug_conflict");   // tx rolls back on dispose
        }

        if (input.Tags is { Count: > 0 })
            await SyncTagsAsync(ev.Id, input.Tags, ct);

        // V3 Capability preset (Phase 2): materialize the event's capability set from its Kind. Additive —
        // an unkinded event still gets the universal capabilities.
        await capabilities.MaterializeForEventAsync(ev.Id, ct);

        // V3 §13.1 (Phase 15): snapshot the template — overlay its capability preset onto the just-materialised set,
        // apply declarative defaults, and record created_from_template_version. Runs after materialization so the
        // template wins; it never creates inventory/financial rows.
        if (input.TemplateId is not null)
        {
            var applied = await templates.ApplyToEventAsync(ev.Id, input.TemplateId.Value, ct);
            if (!applied.Ok) return ServiceResult<EventDetail>.Fail(applied.Error!);   // tx rolls back on dispose
        }

        EnqueueReindex(ev.Id);   // V3 §15 (Phase 16): outbox-fed index — same transaction as the event insert
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        log.LogInformation("Event {EventId} ({Slug}) created for org {OrgId}", ev.Id, ev.Slug, orgId);
        return await GetAsync(ev.Id, userId, isAdmin, ct: ct);
    }

    public async Task<ServiceResult<EventDetail>> CloneAsync(Guid userId, Guid eventId, bool isAdmin, string? newTitle,
        CancellationToken ct = default)
    {
        var src = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.DeletedAt == null, ct);
        if (src is null) return ServiceResult<EventDetail>.Fail("not_found");
        if (!await CanManageEventAsync(userId, src, isAdmin, ct)) return ServiceResult<EventDetail>.Fail("forbidden");

        var title = string.IsNullOrWhiteSpace(newTitle) ? $"{src.Title} (Copy)" : newTitle.Trim();
        if (title.Length is < 2 or > 200) return ServiceResult<EventDetail>.Fail("invalid_title");

        // Same org → same root unit; ensure it even if the source predates the Phase-4 backfill (V3 §4.1).
        var orgUnitId = src.OrgUnitId ?? await orgUnits.EnsureRootAsync(src.RepresentingOrgId, ct);

        // D-340: the organiser's configuration travels by DEFAULT and the exceptions are named below.
        // The allowlist this replaced copied 47 of 110 columns; every column D-265 and D-266 added
        // after it was written — the legal terms, the consent gate, the age/gender limits, the tax
        // treatment, RegistrationPolicy — silently landed on its C# default in the copy. The reset list
        // is asserted by EventCloneTests, which enumerates Event's properties by reflection, so a new
        // column is inherited unless someone deliberately adds it here.
        var clone = src.ShallowCopy();

        // Fresh identity — a clone is its own event, in Draft, owned by whoever asked for it (D-268).
        clone.Id = Guid.NewGuid();
        clone.CreatedBy = userId;
        clone.Title = title;
        clone.Slug = await UniqueEventSlugAsync(title, ct);
        clone.ShortCode = await UniqueEventShortCodeAsync(ct);
        clone.Status = EventStatus.Draft;
        clone.OrgUnitId = orgUnitId;                       // V3 §4.1 (Phase 4)

        // Runtime state belongs to the event that actually ran — as do its tickets, orders, attendees
        // and analytics, which hang off the source event id and are never carried across.
        clone.CreatedAt = clone.UpdatedAt = DateTime.UtcNow;
        clone.PublishedAt = null;
        clone.DeletedAt = null;
        clone.ViewCount = 0;
        clone.IsFeatured = false;
        clone.RefundWindowEndsAt = null;                   // V3 §14.5 — opened by a change to the SOURCE

        // Admin moderation is a judgement about the source, not about a draft nobody has seen (D-186).
        clone.IsSuspended = false;
        clone.SuspendedReason = null;
        clone.IsHidden = false;
        clone.HiddenReason = null;

        // Review outcomes are the source's (D-266 M4/M7). A copy re-enters the queue unreviewed and
        // unclaimed; inheriting a Passed financial review would clear a publish blocker nobody checked.
        clone.FinancialReviewStatus = null;
        clone.FinancialReviewedBy = null;
        clone.FinancialReviewedAt = null;
        clone.FinancialReviewNotes = null;
        clone.ReviewClaimedBy = null;
        clone.ReviewClaimedAt = null;

        // Lineage: a clone is a new root, not another edition of the source's series (V3 §3.4 rule 5).
        // Adding it to a series is a later, explicit act.
        clone.SeriesId = null;
        clone.EditionOrdinal = null;
        clone.EditionLabel = null;

        db.Events.Add(clone);

        foreach (var tagId in await db.EventTags.AsNoTracking().Where(t => t.EventId == eventId).Select(t => t.TagId).ToListAsync(ct))
            db.EventTags.Add(new EventTag { EventId = clone.Id, TagId = tagId });

        // Ticket types carry their form fields. Sold ALWAYS resets to 0 — inventory is never inherited.
        foreach (var tt in await db.TicketTypes.AsNoTracking().Where(t => t.EventId == eventId && t.DeletedAt == null).ToListAsync(ct))
        {
            var copy = new TicketType
            {
                EventId = clone.Id, Currency = clone.SettlementCurrency, Name = tt.Name, PricePaise = tt.PricePaise, PricingUnit = tt.PricingUnit,
                RegistrationMode = tt.RegistrationMode, GroupMin = tt.GroupMin, GroupMax = tt.GroupMax,
                Quantity = tt.Quantity, Sold = 0, SaleStarts = tt.SaleStarts, SaleEnds = tt.SaleEnds,
                PerUserLimit = tt.PerUserLimit, IsAllAccess = tt.IsAllAccess, IsCompetition = tt.IsCompetition,
            };
            db.TicketTypes.Add(copy);
            /*
             * D-366 — the price BANDS travel with the ticket type.
             *
             * Without this a cloned team event keeps `PricePaise` — which is only the cheapest band — and
             * loses every other one, so a team of five that paid ₹400 on the original pays ₹250 on the
             * clone and nothing on any screen says the price list changed. That is the D-340 failure
             * repeating: a copy that names the fields it carries silently drops whatever is added later.
             */
            foreach (var band in await db.TicketPriceTiers.AsNoTracking().Where(b => b.TicketTypeId == tt.Id).ToListAsync(ct))
                db.TicketPriceTiers.Add(new TicketPriceTier
                {
                    TicketTypeId = copy.Id, MinSize = band.MinSize, MaxSize = band.MaxSize,
                    PricePaise = band.PricePaise,
                });
            foreach (var f in await db.FormFields.AsNoTracking().Where(f => f.TicketTypeId == tt.Id).ToListAsync(ct))
                db.FormFields.Add(new FormField
                {
                    TicketTypeId = copy.Id, Key = f.Key, Label = f.Label, Type = f.Type,
                    Scope = f.Scope, Required = f.Required, OptionsJson = f.OptionsJson, Sort = f.Sort,
                });
        }

        // Media rows reference the SAME stored object rather than duplicating it — storage objects are
        // immutable, so sharing is safe. Whether a clone should own its own objects is an M6 (media
        // governance) decision, deliberately not pre-empted here.
        foreach (var m in await db.EventMedia.AsNoTracking().Where(m => m.EventId == eventId).ToListAsync(ct))
            db.EventMedia.Add(new EventMedia { EventId = clone.Id, Kind = m.Kind, Key = m.Key, Caption = m.Caption, Sort = m.Sort });

        // Speaker/sponsor links point at org-level entities, so the links are simply re-pointed.
        foreach (var s in await db.EventSpeakers.AsNoTracking().Where(s => s.EventId == eventId).ToListAsync(ct))
            db.EventSpeakers.Add(new EventSpeaker { EventId = clone.Id, SpeakerId = s.SpeakerId, Sort = s.Sort });
        foreach (var s in await db.EventSponsors.AsNoTracking().Where(s => s.EventId == eventId).ToListAsync(ct))
            db.EventSponsors.Add(new EventSponsor { EventId = clone.Id, SponsorId = s.SponsorId, Sort = s.Sort });

        foreach (var s in await db.EventSessions.AsNoTracking().Where(s => s.EventId == eventId).ToListAsync(ct))
        {
            var copy = new EventSession
            {
                EventId = clone.Id, Title = s.Title, Description = s.Description,
                Kind = s.Kind, StartsAt = s.StartsAt, EndsAt = s.EndsAt, Sort = s.Sort,
            };
            db.EventSessions.Add(copy);
            foreach (var link in await db.EventSessionSpeakers.AsNoTracking().Where(x => x.SessionId == s.Id).ToListAsync(ct))
                db.EventSessionSpeakers.Add(new EventSessionSpeaker { SessionId = copy.Id, SpeakerId = link.SpeakerId });
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId,
            Action = "event.clone", Entity = "events", EntityId = clone.Id,
            DetailsJson = $"{{\"source_event_id\":\"{eventId}\"}}",
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true)
        {
            return ServiceResult<EventDetail>.Fail("slug_conflict");
        }

        await capabilities.MaterializeForEventAsync(clone.Id, ct);   // V3 Capability preset (Phase 2)

        log.LogInformation("Event {SourceEventId} cloned to {CloneEventId}", eventId, clone.Id);
        return await GetAsync(clone.Id, userId, isAdmin, ct: ct);
    }

    public async Task<ServiceResult<EventDetail>> UpdateAsync(Guid userId, Guid eventId, bool isAdmin, UpdateEventInput input, CancellationToken ct = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<EventDetail>.Fail("not_found");
        // D-191: content edit is organizer-owned — the admin bypass granted everywhere else is
        // deliberately NOT extended here. isAdmin still reaches the moderation methods (feature/suspend/
        // hide/notify/transition), just never this one. D-268: "organizer-owned" means the event's owner
        // or a manage-capable member of the organization it represents — passing isAdmin: false keeps the
        // admin exclusion exactly as D-191 set it.
        // D-266 M4: content is frozen while the event is with a reviewer. Delegated to
        // EventStatusWorkflow.IsEditLocked so the rule has one definition — an edit landing between a
        // reviewer reading the event and approving it means they approved something else.
        if (EventStatusWorkflow.IsEditLocked(ev.Status))
            return ServiceResult<EventDetail>.Fail("event_under_review");

        if (!await CanManageEventAsync(userId, ev, isAdmin: false, ct))
            return ServiceResult<EventDetail>.Fail("forbidden");

        /*
         * D-363 §4 — approval binds to what was reviewed.
         *
         * `IsEditLocked` covers the review states only, so an APPROVED event was fully editable with no
         * re-review: approved, then retitled, re-dated, re-venued, and published as something no
         * reviewer had seen. The approval was real; what it applied to was not.
         *
         * A material edit sends it back to the queue rather than being refused — refusing would mean an
         * approved event can never be corrected, and the organiser's only route would be to cancel and
         * start again. Cosmetic edits (banner, FAQ, contact, registration windows) stay free, because a
         * re-review to fix a typo is a rule people route around rather than respect.
         */
        var returnsToQueue = ev.Status == EventStatus.Approved && EventStatusWorkflow.RequiresFreshReview(input);

        var statusBefore = ev.Status;
        var result = await ApplyUpdateAsync(ev, userId, isAdmin, input, ct);
        if (!result.Ok || !returnsToQueue) return result;

        // Applied first, moved second: an edit that the update path REFUSES must not leave the event in
        // the queue for a change that never landed.
        if (!await EventReviewReopen.IfApprovedAsync(db, ev.Id, statusBefore, userId, "a material field", ct))
            return result;

        // The reopen writes past the change tracker, so `ev` still reads Approved — project the response
        // from the row, not from the stale copy, or the client is told the edit kept its approval.
        await db.Entry(ev).ReloadAsync(ct);
        log.LogInformation("Event {EventId} returned to review: material edit after approval", ev.Id);
        return ServiceResult<EventDetail>.Success(await ToDetailAsync(ev, ct));
    }

    /// <summary>D-191: the one and only event-update implementation. <see cref="UpdateAsync"/> (organizer,
    /// real org role) and <see cref="EmergencyUpdateAsync"/> (Super Admin, mandatory reason, audited) both
    /// call this — every validation rule, side effect, and invariant lives here exactly once.</summary>
    /// <summary>Applies the D-265 wizard field groups. One implementation, two callers (create and
    /// <see cref="ApplyUpdateAsync"/>) — the alternative was the same twenty-odd assignments written
    /// twice, drifting the first time one side gained a validation the other did not.
    ///
    /// <para>A null group is untouched. A null field inside a supplied group is also untouched, so a
    /// client can PATCH one field without clearing its neighbours; clearing is done by sending an
    /// empty string, which normalises to null here.</para></summary>
    /// <returns>An error code, or null on success.</returns>
    private static string? ApplyFieldGroups(Event ev, EventContentInput? content, EventLegalInput? legal,
        EventScheduleInput? schedule, EventLocationInput? location, EventEligibilityInput? eligibility,
        EventCommerceInput? commerce)
    {
        static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        static DateTime? Utc(DateTime? v) => v is null ? null : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc);

        if (content is not null)
        {
            if (content.Tagline is not null) ev.Tagline = Norm(content.Tagline);
            if (content.ShortDescription is not null) ev.ShortDescription = Norm(content.ShortDescription);
            if (content.LogoKey is not null) ev.LogoKey = Norm(content.LogoKey);
            if (content.ThumbnailKey is not null) ev.ThumbnailKey = Norm(content.ThumbnailKey);
            if (content.PromoVideoKey is not null) ev.PromoVideoKey = Norm(content.PromoVideoKey);
            if (content.Rules is not null) ev.Rules = Norm(content.Rules);
            if (content.FaqJson is not null) ev.FaqJson = Norm(content.FaqJson);

            if (ev.Tagline?.Length > 160) return "tagline_too_long";
            if (ev.ShortDescription?.Length > 300) return "short_description_too_long";
        }

        if (legal is not null)
        {
            if (legal.TermsUrl is not null) ev.TermsUrl = Norm(legal.TermsUrl);
            if (legal.TermsText is not null) ev.TermsText = Norm(legal.TermsText);
            if (legal.CodeOfConduct is not null) ev.CodeOfConduct = Norm(legal.CodeOfConduct);
            if (legal.RefundPolicy is not null) ev.RefundPolicy = Norm(legal.RefundPolicy);
            if (legal.CancellationPolicy is not null) ev.CancellationPolicy = Norm(legal.CancellationPolicy);
            if (legal.RequiresConsent is not null) ev.RequiresConsent = legal.RequiresConsent.Value;
            if (legal.ConsentText is not null) ev.ConsentText = Norm(legal.ConsentText);

            // Demanding consent to text nobody wrote would have every registrant accept an empty
            // string, and the stored hash would then be evidence of nothing.
            if (ev.RequiresConsent && string.IsNullOrWhiteSpace(ev.ConsentText)) return "consent_text_required";
        }

        if (schedule is not null)
        {
            if (schedule.RegistrationOpensAt is not null) ev.RegistrationOpensAt = Utc(schedule.RegistrationOpensAt);
            if (schedule.RegistrationClosesAt is not null) ev.RegistrationClosesAt = Utc(schedule.RegistrationClosesAt);
            if (schedule.CheckinOpensAt is not null) ev.CheckinOpensAt = Utc(schedule.CheckinOpensAt);
            if (schedule.CheckinClosesAt is not null) ev.CheckinClosesAt = Utc(schedule.CheckinClosesAt);
            if (schedule.ResultDate is not null) ev.ResultDate = Utc(schedule.ResultDate);
            if (schedule.CertificateReleaseAt is not null) ev.CertificateReleaseAt = Utc(schedule.CertificateReleaseAt);
            if (schedule.AutoClose is not null) ev.AutoClose = schedule.AutoClose.Value;

            if (ev.RegistrationOpensAt is not null && ev.RegistrationClosesAt is not null
                && ev.RegistrationClosesAt <= ev.RegistrationOpensAt) return "invalid_registration_window";
            if (ev.CheckinOpensAt is not null && ev.CheckinClosesAt is not null
                && ev.CheckinClosesAt <= ev.CheckinOpensAt) return "invalid_checkin_window";
        }

        if (location is not null)
        {
            if (location.Building is not null) ev.Building = Norm(location.Building);
            if (location.Floor is not null) ev.Floor = Norm(location.Floor);
            if (location.Room is not null) ev.Room = Norm(location.Room);
            if (location.GoogleMapsUrl is not null) ev.GoogleMapsUrl = Norm(location.GoogleMapsUrl);
            if (location.MeetingPlatform is not null) ev.MeetingPlatform = Norm(location.MeetingPlatform);
            if (location.MeetingPassword is not null) ev.MeetingPassword = Norm(location.MeetingPassword);
        }

        if (eligibility is not null)
        {
            // A negative bound is a clear, and the only sane reading of a negative cap.
            if (eligibility.MinAge is not null) ev.MinAge = eligibility.MinAge < 0 ? null : eligibility.MinAge;
            if (eligibility.MaxAge is not null) ev.MaxAge = eligibility.MaxAge < 0 ? null : eligibility.MaxAge;
            if (eligibility.MaxTeams is not null) ev.MaxTeams = eligibility.MaxTeams <= 0 ? null : eligibility.MaxTeams;
            if (eligibility.GenderRestriction is not null)
            {
                if (!Enum.TryParse<GenderRestriction>(eligibility.GenderRestriction, true, out var g))
                    return "invalid_gender_restriction";
                ev.GenderRestriction = g;
            }
            if (ev.MinAge is not null && ev.MaxAge is not null && ev.MaxAge < ev.MinAge) return "invalid_age_range";
        }

        if (commerce is not null)
        {
            if (commerce.PlatformFeePercent is not null) ev.PlatformFeePercent = commerce.PlatformFeePercent;
            if (commerce.PlatformFeeFlatPaise is not null) ev.PlatformFeeFlatPaise = commerce.PlatformFeeFlatPaise;
            if (commerce.TaxPercent is not null) ev.TaxPercent = commerce.TaxPercent;
            if (commerce.TaxInclusive is not null) ev.TaxInclusive = commerce.TaxInclusive.Value;
            if (commerce.PrizePoolJson is not null) ev.PrizePoolJson = Norm(commerce.PrizePoolJson);

            if (ev.PlatformFeePercent is < 0 or > 100) return "invalid_platform_fee";
            if (ev.TaxPercent is < 0 or > 100) return "invalid_tax_percent";
            if (ev.PlatformFeeFlatPaise is < 0) return "invalid_platform_fee";
        }

        return null;
    }

    private async Task<ServiceResult<EventDetail>> ApplyUpdateAsync(Event ev, Guid userId, bool isAdmin, UpdateEventInput input, CancellationToken ct)
    {
        if (ev.Status == EventStatus.Archived) return ServiceResult<EventDetail>.Fail("event_archived");

        /*
         * D-367 — a Type change may not strand a team registration on an archetype that has none.
         *
         * Checked HERE, before a single field is assigned, so a refusal leaves nothing staged: the new
         * archetype must not be half-applied to a tracked entity that some later save in the same scope
         * could commit. Nothing below this point runs on the rejected path.
         *
         * Refused rather than converted. Converting would delete a `TeamPolicy`, its roster rules and its
         * D-366 price bands as a side effect of a dropdown — data the organiser entered and never asked
         * to lose. This repo refuses in exactly these situations (`event_has_history`,
         * `tickets_already_sold`, `ambiguous_price_rule`) instead of mutating on the user's behalf; the
         * organiser changes the registration first, and decides for themselves what happens to it.
         */
        if (input.TypeId is not null && input.TypeId != ev.TypeId)
        {
            var incoming = await ResolveArchetypeAsync(input.TypeId, ct);
            if (incoming.Slug != ev.ArchetypeSlug
                && await db.TicketTypes.AnyAsync(t => t.EventId == ev.Id && t.DeletedAt == null
                                                      && t.RegistrationMode == RegistrationMode.Group, ct)
                // A Type with no archetype supports nothing (CapabilityResolver.StateOf, null archetype),
                // so it cannot receive a team registration either — asked of the same service every read
                // surface uses rather than resolved a second way here.
                && (incoming.Slug is null || !await ArchetypeSupportsTeamsAsync(incoming.Slug, ev.EventMode, ct)))
                return ServiceResult<EventDetail>.Fail("type_conflicts_with_team_ticket");
        }

        // V3 §14.5 material change — snapshot the material fields before applying the edit.
        var mcBeforeStart = ev.StartsAt; var mcBeforeEnd = ev.EndsAt; var mcBeforeVenue = ev.VenueId;
        var mcBeforeMode = ev.EventMode; var mcBeforeVenueName = ev.VenueName;

        if (input.Title is not null)
        {
            var title = input.Title.Trim();
            if (title.Length is < 2 or > 200) return ServiceResult<EventDetail>.Fail("invalid_title");
            ev.Title = title; // slug intentionally immutable once created (D-010)
        }
        if (input.Subtitle is not null) ev.Subtitle = input.Subtitle.Trim();
        if (input.Description is not null) ev.Description = input.Description;
        if (input.Language is not null) ev.Language = input.Language;
        if (input.ContactEmail is not null) ev.ContactEmail = input.ContactEmail;
        if (input.ContactPhone is not null) ev.ContactPhone = input.ContactPhone;
        if (input.Website is not null) ev.Website = input.Website;
        if (input.SocialLinksJson is not null) ev.SocialLinksJson = input.SocialLinksJson;
        if (input.Capacity is not null) ev.Capacity = input.Capacity;
        // Empty string CLEARS the banner; null means "untouched", the convention every field here follows.
        // Without this an organiser could replace a banner but never remove one — `null` is indistinguishable
        // from "this PATCH did not mention the banner", so there was no value that meant "no banner" (D-302).
        if (input.BannerKey is not null)
            ev.BannerKey = string.IsNullOrWhiteSpace(input.BannerKey) ? null : input.BannerKey;
        if (input.IsFeatured is not null) ev.IsFeatured = input.IsFeatured.Value;
        if (input.ListedStandalone is not null) ev.ListedStandalone = input.ListedStandalone.Value;   // §3.4 rule 2 opt-in
        if (input.Timezone is not null) ev.Timezone = input.Timezone;

        // D-188 system-impact audit: only fires when the caller is actually CHANGING to a new node (the
        // `is not null` guard already scopes this) — an event already filed under a since-disabled category
        // is never blocked from being edited for anything else; only *newly selecting* a non-Active node is refused.
        if (input.CategoryId is not null)
        {
            if (!await db.EventCategories.AnyAsync(c => c.Id == input.CategoryId && c.Level == CategoryLevel.Category && c.Status == CategoryStatus.Active, ct))
                return ServiceResult<EventDetail>.Fail("invalid_category");
            ev.CategoryId = input.CategoryId.Value;
        }
        if (input.TypeId is not null)
        {
            if (!await db.EventCategories.AnyAsync(c => c.Id == input.TypeId && c.Level == CategoryLevel.Type && c.Status == CategoryStatus.Active, ct))
                return ServiceResult<EventDetail>.Fail("invalid_type");
            ev.TypeId = input.TypeId;
        }
        if (input.AudienceLevelId is not null)
        {
            if (!await db.EventCategories.AnyAsync(c => c.Id == input.AudienceLevelId && c.Level == CategoryLevel.Audience && c.Status == CategoryStatus.Active, ct))
                return ServiceResult<EventDetail>.Fail("invalid_audience_level");
            ev.AudienceLevelId = input.AudienceLevelId;
        }
        if (input.TemplateId is not null)
        {
            if (!await db.EventTemplates.AnyAsync(t => t.Id == input.TemplateId, ct))
                return ServiceResult<EventDetail>.Fail("invalid_template");
            ev.TemplateId = input.TemplateId;
        }
        if (input.Visibility is not null)
        {
            if (!Enum.TryParse<EventVisibility>(input.Visibility, true, out var vis))
                return ServiceResult<EventDetail>.Fail("invalid_visibility");
            ev.Visibility = vis;
        }
        if (input.EventMode is not null) ev.EventMode = ParseMode(input.EventMode);
        if (input.OnlineUrl is not null)
            ev.OnlineUrl = string.IsNullOrWhiteSpace(input.OnlineUrl) ? null : input.OnlineUrl.Trim();
        if (input.EventMode is not null || input.OnlineUrl is not null)
        {
            var modeErr = ValidateMode(ev.EventMode, ev.OnlineUrl);
            if (modeErr is not null) return ServiceResult<EventDetail>.Fail(modeErr);
        }

        var groupErr = ApplyFieldGroups(ev, input.Content, input.Legal, input.Schedule,
            input.Location, input.Eligibility, input.Commerce);
        if (groupErr is not null) return ServiceResult<EventDetail>.Fail(groupErr);

        if (input.StartsAt is not null) ev.StartsAt = DateTime.SpecifyKind(input.StartsAt.Value, DateTimeKind.Utc);
        if (input.EndsAt is not null) ev.EndsAt = DateTime.SpecifyKind(input.EndsAt.Value, DateTimeKind.Utc);
        if (ev.EndsAt <= ev.StartsAt) return ServiceResult<EventDetail>.Fail("invalid_dates");

        if (input.VenueId is not null || input.VenueName is not null || input.VenueAddress is not null
            || input.City is not null || input.Lat is not null || input.Lng is not null)
        {
            var venueError = await ApplyVenueAsync(ev, ev.RepresentingOrgId, input.VenueId,
                input.VenueName ?? ev.VenueName, input.VenueAddress ?? ev.VenueAddress,
                input.City ?? ev.City, input.Lat ?? ev.Lat, input.Lng ?? ev.Lng, ct);
            if (venueError is not null) return ServiceResult<EventDetail>.Fail(venueError);
        }

        // V3 Kind (Phase 1): re-derive when the taxonomy changed. Additive; null leaves it unchanged-null.
        if (input.CategoryId is not null || input.TypeId is not null)
        {
            ev.KindSlug = await kinds.ResolveKindSlugAsync(ev.TypeId, ev.CategoryId, ct);
            // Archetype follows the type, but Product deliberately does not: re-deriving it here would let a
            // type change silently flip a Public event to Private, which D-267 forbids outright. Product only
            // ever moves Private→Public, through the explicit conversion built in M3.
            ev.ArchetypeSlug = (await ResolveArchetypeAsync(ev.TypeId, ct)).Slug ?? ev.ArchetypeSlug;
        }

        await ApplyMaterialChangeAsync(ev, userId, isAdmin, mcBeforeStart, mcBeforeEnd, mcBeforeVenue, mcBeforeMode, mcBeforeVenueName, ct);
        ev.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (input.Tags is not null)
            await SyncTagsAsync(ev.Id, input.Tags, ct);

        // V3 Capability preset (Phase 2): a Kind or mode change re-derives the event's capability set.
        if (input.CategoryId is not null || input.TypeId is not null || input.EventMode is not null)
            await capabilities.MaterializeForEventAsync(ev.Id, ct);

        EnqueueReindex(ev.Id);   // V3 §15 (Phase 16): title/venue/tags/kind/mode may have changed — reindex
        await db.SaveChangesAsync(ct);
        return await GetAsync(ev.Id, userId, isAdmin, ct: ct);
    }

    /// <summary>D-191: Super Admin emergency edit. Authorization (Super Admin only) is enforced by the
    /// route's <c>kurx_admin</c> policy — this method's own contract is the mandatory reason and the audit
    /// snapshot; the actual mutation is <see cref="ApplyUpdateAsync"/>, unchanged, shared with the normal
    /// organizer path.</summary>
    public async Task<ServiceResult<EventDetail>> EmergencyUpdateAsync(Guid actorId, Guid eventId, string reason, UpdateEventInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return ServiceResult<EventDetail>.Fail("reason_required");

        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<EventDetail>.Fail("not_found");

        var before = EventSnapshot(ev);
        var result = await ApplyUpdateAsync(ev, actorId, isAdmin: true, input, ct);
        if (!result.Ok) return result;

        audit.Write(new AuditEvent("admin.event.emergency_edit", "events", ev.Id, ActorType: "admin", ActorId: actorId,
            Before: before, After: new { reason, @event = EventSnapshot(ev) }));
        await db.SaveChangesAsync(ct);   // IAuditWriter stages on the unit of work; ApplyUpdateAsync already
                                          // saved its own changes, so the audit row needs this one more commit.
        return result;
    }

    private static object EventSnapshot(Event e) => new
    {
        e.Id, e.RepresentingOrgId, e.Title, e.Subtitle, e.Description, e.CategoryId, e.TypeId, e.AudienceLevelId,
        e.TemplateId, e.VenueId, e.VenueName, e.VenueAddress, e.City, e.Lat, e.Lng,
        e.StartsAt, e.EndsAt, e.Timezone, e.Capacity, Visibility = e.Visibility.ToString(), e.Language,
        e.ContactEmail, e.ContactPhone, e.Website, e.BannerKey, e.IsFeatured,
        EventMode = e.EventMode.ToString(), e.OnlineUrl, e.UpdatedAt,
    };

        public async Task<ServiceResult<IReadOnlyList<EventReviewEntry>>> GetReviewHistoryAsync(Guid eventId, CancellationToken ct = default)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(e => e.Id == eventId, ct))
            return ServiceResult<IReadOnlyList<EventReviewEntry>>.Fail("not_found");

        var rows = await db.VerificationReviews.AsNoTracking()
            .Where(r => r.SubjectType == VerificationSubjectType.Event && r.SubjectId == eventId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        // Reviewer names resolved separately and mapped in memory. A join would silently DROP any review
        // whose reviewer row is missing — a deleted admin account would erase its decisions from the
        // history, which is the opposite of what an audit trail is for.
        var reviewerIds = rows.Where(r => r.ReviewerId is not null).Select(r => r.ReviewerId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => reviewerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var entries = rows.Select(r => new EventReviewEntry(
            r.Id, r.Decision.ToString(), r.ReviewerId,
            r.ReviewerId is { } rid && names.TryGetValue(rid, out var n) ? n : null,
            r.ReasonCode, r.Notes, r.CreatedAt)).ToList();

        return ServiceResult<IReadOnlyList<EventReviewEntry>>.Success(entries);
    }

    public async Task<EventReviewCounts> GetReviewCountsAsync(CancellationToken ct = default)
    {
        // One grouped pass rather than six counts: the numbers must describe the same instant, or the tabs
        // can disagree with each other while a reviewer is working.
        var byStatus = await db.Events.AsNoTracking()
            .Where(e => e.DeletedAt == null)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        int N(EventStatus s) => byStatus.TryGetValue(s, out var c) ? c : 0;
        return new EventReviewCounts(
            N(EventStatus.PendingReview), N(EventStatus.UnderReview), N(EventStatus.ChangesRequested),
            N(EventStatus.Approved), N(EventStatus.Rejected), Legacy: 0);
    }

    /// <summary>D-266 M8 — wizard autosave. Upsert per (event, caller); the payload is stored verbatim and
    /// never parsed, because it is what the organiser has typed rather than what the event is.</summary>
    public async Task<ServiceResult<EventDraftView>> SaveDraftAsync(Guid userId, Guid eventId, bool isAdmin,
        string payloadJson, string? stepKey, CancellationToken ct = default)
    {
        var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
        if (!access.EventExists || !access.HasStanding) return ServiceResult<EventDraftView>.Fail("not_found");
        if (!access.Can(EventPermission.ManageContent)) return ServiceResult<EventDraftView>.Fail("forbidden");

        var row = await db.EventDraftSnapshots.FirstOrDefaultAsync(d => d.EventId == eventId && d.UserId == userId, ct);
        if (row is null)
        {
            row = new EventDraftSnapshot { EventId = eventId, UserId = userId };
            db.EventDraftSnapshots.Add(row);
        }
        row.PayloadJson = payloadJson;
        row.StepKey = stepKey;
        row.SavedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (db.Entry(row).State == EntityState.Added)
        {
            // Autosave is the one call that genuinely races with itself — the same organiser with the wizard
            // open in two tabs fires it on a timer from both. Both see no row, both insert, and the unique
            // (EventId, UserId) index rejects the loser. Adopt the winner and write onto it: losing the
            // insert is not a reason to lose the organiser's typing, which is all this row exists to keep.
            db.Entry(row).State = EntityState.Detached;
            var winner = await db.EventDraftSnapshots
                .FirstOrDefaultAsync(d => d.EventId == eventId && d.UserId == userId, ct);
            if (winner is null) throw;   // not the race — a genuine failure, and it must not be swallowed
            winner.PayloadJson = payloadJson;
            winner.StepKey = stepKey;
            winner.SavedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            row = winner;
        }

        return ServiceResult<EventDraftView>.Success(new EventDraftView(eventId, row.PayloadJson, row.StepKey, row.SavedAt));
    }

    public async Task<ServiceResult<EventDraftView?>> GetDraftAsync(Guid userId, Guid eventId, bool isAdmin,
        CancellationToken ct = default)
    {
        var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
        if (!access.EventExists || !access.HasStanding) return ServiceResult<EventDraftView?>.Fail("not_found");
        if (!access.Can(EventPermission.ManageContent)) return ServiceResult<EventDraftView?>.Fail("forbidden");

        // Only the caller's own draft — see EventDraftSnapshot's remarks on why these are per user.
        var row = await db.EventDraftSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EventId == eventId && d.UserId == userId, ct);
        return ServiceResult<EventDraftView?>.Success(
            row is null ? null : new EventDraftView(eventId, row.PayloadJson, row.StepKey, row.SavedAt));
    }

    /// <summary>D-266 M7 — FinanceOps' verdict on a fundraising event's money path. Recorded on the event
    /// rather than in a table: one decision, no documents, and "is this event financially cleared?" is a
    /// property of the event. The audit spine carries the trail.</summary>
    public async Task<ServiceResult<EventDetail>> RecordFinancialReviewAsync(Guid reviewerId, Guid eventId,
        bool passed, string? notes, CancellationToken ct = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.DeletedAt == null, ct);
        if (ev is null) return ServiceResult<EventDetail>.Fail("not_found");

        // A refusal the organiser cannot act on is a dead end, not a review outcome.
        if (!passed && string.IsNullOrWhiteSpace(notes))
            return ServiceResult<EventDetail>.Fail("notes_required");

        var before = ev.FinancialReviewStatus;
        ev.FinancialReviewStatus = passed ? FinancialReviewStatus.Passed : FinancialReviewStatus.Failed;
        ev.FinancialReviewedBy = reviewerId;
        ev.FinancialReviewedAt = DateTime.UtcNow;
        ev.FinancialReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ev.UpdatedAt = DateTime.UtcNow;

        audit.Write(new AuditEvent("event.financial_review", "events", ev.Id,
            ActorType: "admin", ActorId: reviewerId,
            Before: new { status = before?.ToString() },
            After: new { status = ev.FinancialReviewStatus.ToString() }));

        await db.SaveChangesAsync(ct);
        return await GetAsync(ev.Id, reviewerId, isAdmin: true, ct: ct);
    }

public async Task<ServiceResult<EventDetail>> TransitionAsync(Guid userId, Guid eventId, bool isAdmin, bool isReviewer,
        string action, string? reasonCode = null, string? notes = null, CancellationToken ct = default)
    {
        if (!EventStatusWorkflow.IsKnownAction(action)) return ServiceResult<EventDetail>.Fail("invalid_action");

        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<EventDetail>.Fail("not_found");
        var canManage = await CanManageEventAsync(userId, ev, isAdmin, ct);
        // A platform reviewer may publish/reject a paid event under review even without org membership;
        // all other actions (submit_review/unpublish/close/archive) stay org Owner/Manager/admin.
        // D-266 M4: the reviewer-scoped set is declared by the workflow, not spelled out here, so adding a
        // reviewer action cannot forget to authorize it. "publish"/"reject" remain for the legacy path.
        var isReviewAction = action is "publish" or "reject"
                          || EventStatusWorkflow.ReviewerActions.Contains(action);
        if (!canManage && !(isReviewer && isReviewAction))
            return ServiceResult<EventDetail>.Fail("forbidden");

        // A reviewer decision is a reviewer's to make: an organiser who can manage the event may not
        // approve or reject their own submission.
        if (EventStatusWorkflow.ReviewerActions.Contains(action) && !isReviewer && !isAdmin)
            return ServiceResult<EventDetail>.Fail("reviewer_required");

        // D-266 M4 — an item under review belongs to the reviewer who claimed it. Deciding on someone
        // else's claim is how two reviewers end up working one event: the checklist is per-reviewer, so the
        // second one signs off against ticks they never made. An admin may still act, because a reviewer
        // who goes offline holding an item must not strand it — a release valve, not a general exemption.
        // The M4 decisions, plus the LEGACY `publish`/`reject` when they leave `UnderReview` — the bulk admin
        // path and the workspace sheet still post those, and they finish someone else's review just as
        // surely. Scoped to the review state on purpose: `publish` from Draft/Approved is an organiser's own
        // action and must stay untouched.
        var endsAnotherReview =
            action is "release_review" or "approve_review" or "reject_review" or "request_changes"
            || (ev.Status == EventStatus.UnderReview && action is "publish" or "reject");
        if (endsAnotherReview && ev.ReviewClaimedBy is { } holder && holder != userId && !isAdmin)
            return ServiceResult<EventDetail>.Fail("claimed_by_another_reviewer");

        // Structured reason and organiser-facing notes are required where the workflow says so. Checked
        // before the transition so a rejection can never land without a recorded why.
        if (EventStatusWorkflow.ActionsRequiringReason.Contains(action))
        {
            if (string.IsNullOrWhiteSpace(reasonCode) || !Enum.TryParse<EventReviewReason>(reasonCode, out _))
                return ServiceResult<EventDetail>.Fail("reason_code_required");
        }
        if (EventStatusWorkflow.ActionsRequiringNotes.Contains(action) && string.IsNullOrWhiteSpace(notes))
            return ServiceResult<EventDetail>.Fail("notes_required");

        // D-266 M7 — approval is blocked until this reviewer has worked the whole checklist. The list is a
        // projection of PolicyResolver.ReviewerChecklist, so "complete" means every rule that gates the
        // publish has been looked at by the person signing it off. Per-reviewer by design: releasing and
        // reclaiming an item must not let one reviewer inherit another's ticks.
        if (action == "approve_review" && !await checklist.IsCompleteAsync(userId, eventId, ct))
            return ServiceResult<EventDetail>.Fail("checklist_incomplete");

        if (!EventStatusWorkflow.TryGetTarget(action, ev.Status, out var target))
            return ServiceResult<EventDetail>.Fail("invalid_transition");

        // V3 §14.2/§14.3 (Phase 14) — the five validation gates + the internal approval-chain gate. Additive: the
        // existing `publish` path keeps its own readiness/org/paid checks below; this only ADDS the approval gate
        // (a no-op when the event's OrgUnit tree declares no chain) and gates the new Scheduled/Live/Completed states.
        var gateError = await TransitionGateAsync(ev, target, ct);
        if (gateError is not null) return ServiceResult<EventDetail>.Fail(gateError);

        /*
         * D-363 — you cannot un-publish an event people have already bought into.
         *
         * `unpublish` returns a Published event to Draft, and Draft is the one status `DeleteDraftAsync`
         * accepts. That made `Published → unpublish → Draft → delete` a working route to hard-deleting a
         * sold event together with every order, ticket and registration cascading off it. Closing it at
         * the entrance is what keeps it shut for paths nobody has written yet.
         *
         * It also happens to be the right rule on its own terms: an event with attendees is withdrawn by
         * `cancel` — which is terminal and obliges refunds (D-101) — or ended by `close`. Quietly pulling
         * it back to Draft would strand everyone holding a ticket with no refund and no notification.
         */
        if (action == "unpublish" && await HasCommerceOrAttendanceAsync(ev.Id, ct))
            return ServiceResult<EventDetail>.Fail("event_has_history");

        // D-101 (M7): cancelling pulls a live event out of sale/discovery and, per the ratified refund
        // policy, obliges a full refund. An organizer may only cancel BEFORE the event starts; afterwards
        // it is an admin/reviewer call. Issuing the reverse-ledger refunds is M4's contract — this records
        // the state transition + audit trail that M4 reacts to.
        if (target == EventStatus.Cancelled && !isAdmin && !isReviewer && ev.StartsAt <= DateTime.UtcNow)
            return ServiceResult<EventDetail>.Fail("event_already_started");

        // Set only on the leg that publishes, and folded into the status claim below so the row is written
        // once. `??=` semantics are preserved: a republish keeps the original publication moment.
        DateTime? publishedAt = null;

        var isPaid = await IsPaidEventAsync(ev.Id, ct);

        /*
         * D-378 — an event entering the review queue carries the copy its listing is made of.
         *
         * Placed on `submit_review` and nowhere else, deliberately. Create/update must stay permissive
         * because the wizard writes a Draft step by step and `EventDraftBodyValidator` exists to preserve
         * exactly the half-filled state a stricter rule would refuse (D-266 M8). The completeness question
         * is only meaningful at the moment the organiser says "this is ready for a reviewer".
         *
         * This is the server half of the client rule: both wizards now block Continue on the Content step,
         * and a disabled button is a rendering state, not an authorization — the API is reachable without
         * it.
         */
        if (action == "submit_review")
        {
            /*
             * D-379 — the representation invariant, checked at the submission boundary as well as at
             * creation. Creation is where an event acquires its organization; this is where it stops
             * being possible to have lost it since. A row can reach here unrepresented in exactly two
             * ways, and neither is hypothetical: it predates D-379, or its organization was soft-deleted
             * after the event was created.
             *
             * Deliberately NOT a retroactive validity sweep — existing events still load, still read and
             * still appear. What they cannot do is enter the review queue on evidence they never carried.
             */
            var org = await db.Organizations.AsNoTracking()
                .Where(o => o.Id == ev.RepresentingOrgId)
                .Select(o => new { o.IsPersonal, o.DeletedAt })
                .FirstOrDefaultAsync(ct);
            if (org is null || org.IsPersonal || org.DeletedAt is not null)
                return ServiceResult<EventDetail>.Fail("representation_required");

            /*
             * And its own authorization. `EventAuthorization` is keyed on `EventId` with one row per
             * event, so "Event A's letter authorises Event B" is structurally impossible rather than
             * merely refused — this query cannot return another event's row. Unconditional now: the old
             * `Public && RepresentsInstitution` arms are gone, so a private, free or personal-scale event
             * carries the same evidence as a public paid one.
             */
            var auth = await db.EventAuthorizations.AsNoTracking()
                .Where(a => a.EventId == ev.Id)
                .Select(a => new
                {
                    a.HeadName, a.HeadDesignation, a.OfficialEmail, a.OfficialPhone,
                    a.RepresentativeRole, a.RepresentativeRoleOther, a.LetterheadDocumentKey
                })
                .FirstOrDefaultAsync(ct);
            if (auth is null) return ServiceResult<EventDetail>.Fail("event_authorization_required");
            if (string.IsNullOrWhiteSpace(auth.HeadName)
                || string.IsNullOrWhiteSpace(auth.HeadDesignation)
                || string.IsNullOrWhiteSpace(auth.OfficialEmail)
                || string.IsNullOrWhiteSpace(auth.OfficialPhone)
                || string.IsNullOrWhiteSpace(auth.RepresentativeRole)
                || (auth.RepresentativeRole == "Other" && string.IsNullOrWhiteSpace(auth.RepresentativeRoleOther)))
                return ServiceResult<EventDetail>.Fail("authorization_fields_required");
            // The letter is the evidence. Without it the row records a claim and proves nothing.
            if (string.IsNullOrWhiteSpace(auth.LetterheadDocumentKey))
                return ServiceResult<EventDetail>.Fail("letterhead_required");

            var completeness = ValidateSubmissionReadiness(ev);
            if (completeness is not null) return ServiceResult<EventDetail>.Fail(completeness);
        }

        // Paid events fail fast at submission if the organizer isn't paid-verified (+ the org verified).
        if (isPaid && action == "submit_review")
        {
            var gate = await PaidOrganizerGateAsync(ev, ct);
            if (gate is not null) return ServiceResult<EventDetail>.Fail(gate);
        }

        if (target == EventStatus.Published)
        {
            // D-075: an event created under an institution can't publish (free or paid) until that
            // institution is Verified. Personal orgs are exempt. Derived live — no stored "pending" state.
            var orgVerification = await db.Organizations.AsNoTracking()
                .Where(o => o.Id == ev.RepresentingOrgId)
                .Select(o => new { o.IsPersonal, o.VerificationStatus })
                .FirstOrDefaultAsync(ct);
            if (orgVerification is { IsPersonal: false })
            {
                // D-352 — the last publish gate the bypass did not reach, because it reads
                // `VerificationStatus` straight off the row instead of going through `TrustService`. In dev
                // nobody has an admin-approved organization, so a staged one could be selected and the event
                // created, then publish refused — the bypass opened three doors and left the fourth shut.
                // Same flag, same Production guard; the stored status is untouched.
                if (!identityOptions.Bypass
                    && orgVerification.VerificationStatus != OrgVerificationStatus.Verified)
                    return ServiceResult<EventDetail>.Fail("pending_org_verification");

                // D-101 (M7): representation vacancy — derived live, never stored (same discipline as the
                // pending-org check above). A verified institution with no verified Representative AND no
                // Owner has nobody authorised to represent it, so it can't put new events live until an
                // admin verifies a successor. The Owner fallback keeps legacy (pre-D-074) orgs unchanged.
                var represented = await db.Memberships.AsNoTracking().AnyAsync(m => m.OrgId == ev.RepresentingOrgId
                    && ((m.Role == OrgRole.Representative && m.IsVerified) || m.Role == OrgRole.Owner), ct);
                if (!represented) return ServiceResult<EventDetail>.Fail("representation_vacant");
            }

            var readinessError = ValidatePublishReadiness(ev);
            if (readinessError is not null) return ServiceResult<EventDetail>.Fail(readinessError);

            /*
             * D-377 — approval and publication are two acts, by two different people.
             *
             *   PendingReview / UnderReview → the decision is the REVIEWER's. The creator may not publish.
             *   Approved                    → the decision is the CREATOR's. Approval already granted the
             *                                 permission; when to go live is theirs, paid or free.
             *
             * The reviewer gate used to sit inside `if (isPaid)`, which got this wrong in both directions:
             *
             *   · A FREE event never reached it, so an organiser could submit for review and immediately
             *     publish the same event themselves — live, with no approval, and out of the queue while a
             *     reviewer may have been holding it. Reproduced over HTTP: submit_for_review → 200, then
             *     publish as the creator → 200, status `published`.
             *   · A PAID event reached it even from `Approved`, so a creator whose event a reviewer had
             *     already approved still could not publish it. Approval meant nothing they could act on.
             *
             * One gate replaces both. `Approved` is a real state — reviewed, permitted, and NOT yet public —
             * and the creator is who ends it.
             */
            if (ev.Status is EventStatus.PendingReview or EventStatus.UnderReview && !isReviewer && !isAdmin)
                return ServiceResult<EventDetail>.Fail("reviewer_required");

            if (isPaid)
            {
                // A paid event must have been through review before it can go live — `Draft → Published` is
                // refused here, which is D-047 unchanged. What is no longer required is that a REVIEWER
                // presses publish: reaching `Approved` is the reviewer's act, and publishing it is the
                // organiser's. The financial gates below still run on every publish.
                //
                // Stated as "not from Draft" rather than as a list of reviewed states, because the list was
                // wrong: it omitted `Scheduled`, and `Approved → schedule → open_registration` is a real
                // path (D-319/V3 §14.1). A paid event routed that way was refused `paid_event_requires_review`
                // *after* a reviewer had approved it — a dead end. Draft is the only status reaching Published
                // that has not been through review, so it is the only one to name.
                if (ev.Status == EventStatus.Draft)
                    return ServiceResult<EventDetail>.Fail("paid_event_requires_review");
                var gate = await PaidOrganizerGateAsync(ev, ct);
                if (gate is not null) return ServiceResult<EventDetail>.Fail(gate);
            }

            // D-266 M5 — the policy engine runs LAST on this leg, after org verification, readiness and the
            // paid checks above. Precedence is the point: an unverified institution or a missing description
            // is a more immediate and more fixable problem than a missing authorization letter, and an
            // institution that is not yet verified cannot meaningfully have authorised anything. Reporting
            // the letter first sends the organiser to fix the wrong thing.
            if (await PolicyBlockerAsync(ev, ct) is { } policyBlocker)
                return ServiceResult<EventDetail>.Fail(policyBlocker);

            publishedAt = ev.PublishedAt ?? DateTime.UtcNow;
        }

        var previousStatus = ev.Status;

        // Claim the transition in SQL, against the status this request actually read.
        //
        // Every gate above ran on a snapshot. Without this, two reviewers deciding within the same moment
        // both pass those gates and both write: the status is whoever committed last, while BOTH decisions
        // are recorded below — leaving a review history that says an event was approved and rejected. The
        // predicate is what makes the read meaningful: 0 rows updated means the event moved underneath us,
        // so this request decided about a state that no longer exists and must not record a verdict.
        //
        // A conditional UPDATE rather than a row lock, deliberately: D-231 established that
        // `SELECT … FOR UPDATE` here is worse than the race it fixes (no default lock timeout, so one slow
        // transaction blocks every later writer — the suite went from ~7 minutes to 5h42m). This is the
        // same compare-and-swap `ChatService` already uses for room status and `OrderService` for orders.
        // The holder moves in the SAME statement as the status, so an item can never be UnderReview with no
        // owner (or owned while sitting in the queue). `claim_review` takes it; every other leg releases it,
        // including a decision — the verdict is recorded in verification_reviews, and a decided event is no
        // longer anybody's work in progress.
        var newHolder = action == "claim_review" ? userId : (Guid?)null;
        var newHolderAt = action == "claim_review" ? DateTime.UtcNow : (DateTime?)null;

        var claimed = await db.Events
            .Where(e => e.Id == ev.Id && e.Status == previousStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, target)
                .SetProperty(e => e.UpdatedAt, DateTime.UtcNow)
                .SetProperty(e => e.PublishedAt, e => publishedAt ?? e.PublishedAt)
                .SetProperty(e => e.ReviewClaimedBy, newHolder)
                .SetProperty(e => e.ReviewClaimedAt, newHolderAt), ct);
        if (claimed == 0) return ServiceResult<EventDetail>.Fail("transition_conflict");

        /*
         * D-379 — ONE review, ONE decision, and the evidence records the same verdict.
         *
         * The authorization letter is EVIDENCE the reviewer weighs inside the event review, never a
         * second approval they must perform first. So approving the event marks its letter approved, and
         * rejecting it marks the letter rejected — one click, both records, no dead end where an Approved
         * event still cannot publish because nobody separately blessed its letter.
         *
         * The `Status` field is kept rather than deleted because it IS the audit trail: it carries the
         * reviewer, the timestamp and the reason code for the decision on this specific document. What it
         * no longer does is gate anything on its own — `PolicyResolver` reads existence, not status.
         */
        if (action is "approve_review" or "reject_review")
        {
            var verdict = action == "approve_review"
                ? EventAuthorizationStatus.Approved
                : EventAuthorizationStatus.Rejected;
            await db.EventAuthorizations
                .Where(a => a.EventId == ev.Id && a.Status == EventAuthorizationStatus.Submitted)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.Status, verdict)
                    .SetProperty(a => a.ReviewerId, userId)
                    .SetProperty(a => a.ReviewedAt, DateTime.UtcNow)
                    .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), ct);
        }

        // ExecuteUpdateAsync writes past the change tracker, so the tracked `ev` still holds the OLD status.
        // A caller that reuses this DbContext for a second transition — a job, or anything composing two
        // steps in one scope — would then read the stale value back out of the identity map, compute its
        // predicate from it, and lose the claim against a row that had in fact moved exactly as asked.
        // Reloading costs one SELECT on an operation that happens a handful of times per event.
        await db.Entry(ev).ReloadAsync(ct);
        // D-102 (M3a): written through the typed audit spine so the before/after shape is uniform.
        audit.Write(new AuditEvent("event.status_change", "events", ev.Id,
            ActorType: isReviewer && !canManage ? "admin" : "user",
            ActorId: userId,
            Before: new { status = previousStatus.ToString() },
            After: new { status = target.ToString(), action }));
        // V3 §14.5 (Phase 14): cancelling a SUB-event (its parent event continues) is a material change for that
        // sub-event's registrants — open the refund window + notify + audit, reusing the shared material-change core.
        // (Cancelling a top-level event already obliges a full refund via the D-101 cancel path.)
        if (target == EventStatus.Cancelled && ev.ParentEventId is not null)
            await OpenRefundWindowAndNotifyAsync(ev, userId, isAdmin,
                new { status = previousStatus.ToString() }, new { status = "Cancelled", reason = "sub_event_cancelled" }, ct);
        EnqueueReindex(ev.Id);   // V3 §15 (Phase 16): status change includes/excludes the event from discovery
                // D-266 M4: every reviewer decision is written to VerificationReview — the existing review-history
        // store — in the SAME save as the status change. Two writes could leave a status with no recorded
        // decision behind it, which is exactly the audit gap a review lifecycle exists to close. No second
        // review table: VerificationReview already carries reviewer, reason code, notes and timestamp.
        // Only the three DECISIONS are recorded. Claim and release are queue mechanics, not verdicts —
        // writing rows for them would bury the actual decisions in workflow noise and make "what did the
        // reviewer decide" a filtered query instead of a read.
        var decision = action switch
        {
            "approve_review" => (VerificationDecision?)VerificationDecision.Approve,
            "reject_review" => VerificationDecision.Reject,
            "request_changes" => VerificationDecision.RequestChanges,
            _ => null,
        };
        if (decision is { } d)
        {
            db.VerificationReviews.Add(new VerificationReview
            {
                SubjectType = VerificationSubjectType.Event,   // already in the vocabulary; no new subject type
                SubjectId = ev.Id,
                Decision = d,
                ReviewerId = userId,
                ReasonCode = reasonCode,
                Notes = notes,
            });
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Event {EventId} transitioned to {Status} via {Action}", eventId, target, action);

        // Chat follows the event's lifecycle from this one gate — there is deliberately no second
        // publish flow. Every branch is idempotent, so a republish or a retried transition is safe.
        if (target == EventStatus.Published)
        {
            // The room must exist before the first ticket, otherwise hosts have nowhere to post and
            // the room would be created lazily by whoever buys first.
            await chat.EnsureRoomExistsAsync(ev.Id, ct);
            await chat.AddEventHostsAsync(ev.Id, ct);
            // A user may accept a staff assignment while the event is still a draft, before any room
            // exists — publish is where those acceptances actually take effect.
            await chat.AddAcceptedStaffAsMembersAsync(ev.Id, ct);
        }
        else if (target == EventStatus.Cancelled)
        {
            await chat.LockRoomForEventAsync(ev.Id, "This event was cancelled. Chat is now read-only.", ct);
        }
        else if (target == EventStatus.Archived)
        {
            await chat.LockRoomForEventAsync(ev.Id, "This event is archived. Chat is now read-only.", ct);
        }
        // A reviewer who isn't an org member still needs to read back the (possibly-draft) event.
        return await GetAsync(ev.Id, userId, isAdmin || isReviewer, ct: ct);
    }

    public async Task<ServiceResult<PaymentReadiness>> GetPaymentReadinessAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<PaymentReadiness>.Fail("not_found");
        if (!await CanManageEventAsync(userId, ev, isAdmin, ct))
            return ServiceResult<PaymentReadiness>.Fail("not_found");

        var isPaid = await IsPaidEventAsync(ev.Id, ct);
        var caps = await trust.GetUserCapabilitiesAsync(ev.CreatedBy, ct);
        var orgCaps = await trust.GetOrgCapabilitiesAsync(ev.CreatedBy, ev.RepresentingOrgId, ct);

        var reasons = new List<string>();
        if (!isPaid) reasons.Add("no_paid_ticket_types");
        if (ev.Status != EventStatus.Published) reasons.Add("not_published");
        if (!caps.CanOrganizePaid) reasons.Add("organizer_not_verified_for_paid");
        if (!orgCaps.IsOrgVerified) reasons.Add("org_not_verified");

        var enabled = isPaid && ev.Status == EventStatus.Published && caps.CanOrganizePaid && orgCaps.IsOrgVerified;
        return ServiceResult<PaymentReadiness>.Success(new PaymentReadiness(isPaid, enabled, reasons));
    }

    public async Task<ServiceResult<EventWorkspaceView>> GetWorkspaceAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<EventWorkspaceView>.Fail("not_found");
        if (!await CanManageEventAsync(userId, ev, isAdmin, ct))
            return ServiceResult<EventWorkspaceView>.Fail("not_found");

        // Tabs (§20, rebuilt in D-266 M2): composed from every platform layer, not from the capability
        // catalog alone. Registration/Ticketing/Invitations/Scheduling/Finance/Infrastructure each contribute
        // their own surfaces, so a subsystem no longer has to masquerade as an event capability to show a
        // page — which is exactly what broke when `registration` stopped being a capability.
        var surfaces = await workspace.ComposeAsync(eventId, ct);
        var caps = (await capabilities.GetForEventAsync(eventId, ct))
            .Where(c => c.State != nameof(CapabilityState.Off) && c.State != nameof(CapabilityState.Locked)
                     && !string.IsNullOrEmpty(c.WorkspaceTab))
            .ToLookup(c => c.WorkspaceTab!.ToLowerInvariant().Replace(' ', '-'), StringComparer.Ordinal);

        var tabs = surfaces
            .Select(s => new WorkspaceTabView(s.DisplayName,
                caps[s.TabId].Select(c => new WorkspaceCapabilityView(c.Slug, c.Name, c.State)).ToList()))
            .ToList();

        // Publish checklist (§14.2/§20): project — never duplicate — the Phase-14 validation gates. For each forward
        // lifecycle action valid from the current status, report its gate result. TransitionGateAsync is the one
        // source of truth; `readOnly: true` evaluates it WITHOUT writing (no approval-request materialisation), so a
        // workspace GET stays a pure read.
        var checklist = new List<ChecklistItemView>();
        foreach (var action in EventStatusWorkflow.ForwardActions)
        {
            if (!EventStatusWorkflow.TryGetTarget(action, ev.Status, out var target)) continue;
            var blocker = await TransitionGateAsync(ev, target, ct, readOnly: true);
            checklist.Add(new ChecklistItemView(action, target.ToString(), blocker is null, blocker));
        }

        return ServiceResult<EventWorkspaceView>.Success(new EventWorkspaceView(ev.Id, ev.Status.ToString(), tabs, checklist));
    }

    public async Task<ServiceResult<EventAnalytics>> GetAnalyticsAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<EventAnalytics>.Fail("not_found");
        if (!await CanManageEventAsync(userId, ev, isAdmin, ct))
            return ServiceResult<EventAnalytics>.Fail("not_found");

        // Aggregate counts only — no per-attendee rows are read or returned. GrossPaise/CheckedIn are the
        // same IAnalyticsFactSource calls EventStatsAsync uses (V3 §16, Phase 17) — this route and the
        // admin/org event list can no longer show two different numbers for the same event.
        var ticketTypes = await db.TicketTypes.CountAsync(t => t.EventId == eventId && t.DeletedAt == null, ct);
        var ticketsIssued = await db.Tickets.CountAsync(t => t.EventId == eventId && t.State != TicketState.Void, ct);
        var checkedIn = (await analytics.CheckedInByEventAsync([eventId], ct))[eventId];
        var ordersPaid = await db.Orders.CountAsync(o => o.EventId == eventId && o.Status == OrderStatus.Paid, ct);
        var grossPaise = (await analytics.RevenueByEventAsync([eventId], ct))[eventId];

        return ServiceResult<EventAnalytics>.Success(new EventAnalytics(
            ev.Id, ev.ViewCount, ticketTypes, ticketsIssued, checkedIn, ordersPaid, grossPaise, ev.SettlementCurrency));
    }

    private async Task<bool> IsPaidEventAsync(Guid eventId, CancellationToken ct)
        => await db.TicketTypes.AnyAsync(t => t.EventId == eventId && t.DeletedAt == null && t.PricePaise > 0, ct);

    /// <summary>Paid events require the organizer (event creator) to be paid-verified (identity + bank +
    /// fraud-clear, M7) and the owning org to be verified (M5). Read live so a suspension blocks immediately.</summary>
    private async Task<string?> PaidOrganizerGateAsync(Event ev, CancellationToken ct)
    {
        var caps = await trust.GetUserCapabilitiesAsync(ev.CreatedBy, ct);
        if (!caps.CanOrganizePaid) return "organizer_not_verified_for_paid";
        var orgCaps = await trust.GetOrgCapabilitiesAsync(ev.CreatedBy, ev.RepresentingOrgId, ct);
        if (!orgCaps.IsOrgVerified) return "org_not_verified";
        return null;
    }

    /// <summary>D-367 — does an archetype support team registration?
    ///
    /// <para>Asked of <see cref="ICapabilityService.GetForArchetypeAsync"/> — the established boundary for
    /// "what does this archetype support", and the same persisted matrix the admin console owns (D-188).
    /// The INCOMING archetype is the subject here, so this is the archetype-scoped read rather than the
    /// event-scoped one: the event still carries its old slug at this point, by design.</para>
    ///
    /// <para><c>Locked</c> is how the resolver reports <c>Unsupported</c>. An unknown archetype answers
    /// with an empty set, which is also "no teams" — fail closed, never fail open.</para></summary>
    private async Task<bool> ArchetypeSupportsTeamsAsync(string archetypeSlug, EventMode mode, CancellationToken ct) =>
        CapabilitySet.Supports(await capabilities.GetForArchetypeAsync(archetypeSlug, mode.ToString(), ct), "teams");

    /// <summary>D-363 — has this event ever carried commerce or attendance?
    ///
    /// <para>Deleting an event is a HARD delete, and 46 tables cascade from <c>events</c> — orders,
    /// tickets, registrations, certificates, passes, credentials, admissions. So a delete that reaches a
    /// sold event does not orphan its records, it destroys them, silently and unrecoverably.</para>
    ///
    /// <para>One predicate, because both doors have to ask the same question and a second spelling of it
    /// is how one of them stops asking.</para></summary>
    private async Task<bool> HasCommerceOrAttendanceAsync(Guid eventId, CancellationToken ct) =>
        await db.Orders.AnyAsync(o => o.EventId == eventId, ct)
        || await db.Tickets.AnyAsync(t => t.EventId == eventId, ct)
        || await db.Registrations.AnyAsync(r => r.EventId == eventId, ct);

    public async Task<ServiceResult<bool>> DeleteDraftAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!await CanManageEventAsync(userId, ev, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        if (ev.Status != EventStatus.Draft) return ServiceResult<bool>.Fail("not_draft");

        /*
         * D-363 — the guard that makes "Draft" mean "nothing has happened to this yet".
         *
         * `Status == Draft` was the ONLY check here, and Draft is reachable from Published via
         * `unpublish`. So `Published (with orders) → unpublish → Draft → delete` hard-deleted an event
         * and cascade-deleted every order, ticket and registration on it. Found live: the dev database
         * held a Draft event with an order already sitting on it, so this was not hypothetical.
         *
         * `unpublish` refuses first (below, in TransitionAsync), which is the entrance. This is the exit,
         * and it is checked too — a guard on the only path you thought of is a guard until someone adds
         * a second path. An event with history ends at Cancelled or Archived, never at deletion.
         */
        if (await HasCommerceOrAttendanceAsync(eventId, ct))
            return ServiceResult<bool>.Fail("event_has_history");

        var deletedOrgId = ev.RepresentingOrgId;
        var deletedTitle = ev.Title;

        /*
         * D-364 — soft, as D-025 always said it was.
         *
         * This was `db.Events.Remove(ev)`: a hard delete cascading through 46 tables, on an entity whose
         * `DeletedAt` column existed the whole time and was never once written. The admin console even
         * described this button as "Soft delete — removed from every list but not purged", which was the
         * decision's words attached to the opposite behaviour.
         *
         * The row now survives and the global query filter takes it out of every read. Nothing cascades,
         * so the guard above and this are two independent reasons the record cannot be destroyed.
         */
        ev.DeletedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId,
            Action = "event.delete", Entity = "events", EntityId = eventId,
            DetailsJson = $"{{\"org_id\":\"{deletedOrgId}\",\"title\":\"{deletedTitle}\"}}",
        });
        await db.SaveChangesAsync(ct);
        log.LogInformation("Draft event {EventId} deleted", eventId);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<EventDetail>> GetAsync(Guid eventId, Guid? viewerUserId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null || !await CanViewAsync(ev, viewerUserId, isAdmin, ct)) return ServiceResult<EventDetail>.Fail("not_found");
        return ServiceResult<EventDetail>.Success(await ToDetailAsync(ev, ct));
    }

    public async Task<ServiceResult<EventDetail>> GetBySlugAsync(string slug, Guid? viewerUserId, bool isAdmin, string? viewVisitorKey = null, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug, ct);
        if (ev is null || !await CanViewAsync(ev, viewerUserId, isAdmin, ct)) return ServiceResult<EventDetail>.Fail("not_found");

        // D-130: append to the view stream instead of UPDATE-ing events.ViewCount. An insert into an
        // append-only table takes no lock on the (hot) event row, so concurrent readers of a popular
        // event no longer serialise behind each other. The nightly rollup derives both the daily
        // metrics and the denormalised events.ViewCount from these rows.
        if (viewVisitorKey is not null && ev.Status == EventStatus.Published)
        {
            db.EventViews.Add(new EventView
            {
                EventId = ev.Id,
                VisitorKey = viewVisitorKey,
                UserId = viewerUserId,
            });
            await db.SaveChangesAsync(ct);
        }
        return ServiceResult<EventDetail>.Success(await ToDetailAsync(ev, ct));
    }

    // V3 §15 (Phase 16): the public discovery surface is served by the single canonical ISearchService over the
    // outbox-fed index (FTS + trigram + deterministic ranking + collapse). Routes and DTOs are unchanged; these are
    // thin delegations so there is exactly one discovery implementation.
    public Task<(IReadOnlyList<EventSummary> Items, int Total)> SearchAsync(EventListFilter filter, CancellationToken ct = default)
        => search.SearchAsync(filter, ct);

    public async Task<ServiceResult<(IReadOnlyList<OrgEventRow> Items, int Total)>> ListForOrgAsync(Guid userId, Guid orgId, bool isAdmin, int page, int pageSize, CancellationToken ct = default)
    {
        // The admin console's per-org event list — any seat in the organization, preserved exactly.
        if (!(await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct)).IsMember && !isAdmin)
            return ServiceResult<(IReadOnlyList<OrgEventRow>, int)>.Fail("forbidden");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var q = db.Events.AsNoTracking().Where(e => e.RepresentingOrgId == orgId);
        var total = await q.CountAsync(ct);
        var events = await q.OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        // Per-event stats for the current page only (bounded ≤ 50 rows) — shared with ListForAdminAsync via
        // EventStatsAsync, one grouped-query implementation. Revenue is gross captured-order value;
        // check-ins come from issued tickets.
        var ids = events.Select(e => e.Id).ToList();
        var stats = await EventStatsAsync(ids, ct);
        var catIds = events.Select(e => e.CategoryId).Distinct().ToList();
        var catNames = await db.EventCategories.AsNoTracking()
            .Where(c => catIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var rows = events.Select(e =>
        {
            var s = stats[e.Id];
            return new OrgEventRow(
                e.Id, e.Title, e.Slug, e.Status.ToString(), e.Visibility.ToString(),
                catNames.GetValueOrDefault(e.CategoryId), e.VenueName, e.City, e.Capacity,
                e.IsPaid || s.IsPaid,
                s.Sold, s.CheckedIn, s.RevenuePaise,
                e.StartsAt, e.UpdatedAt, e.SettlementCurrency);
        }).ToList();

        return ServiceResult<(IReadOnlyList<OrgEventRow>, int)>.Success((rows, total));
    }

    /// <summary>The user-first event list backing Workspace. Authorization is the membership join itself —
    /// the query is scoped to the orgs the caller actually holds a seat in, so there is no separate role check
    /// to forget and no way to widen it with a crafted id (the caller supplies no org at all).</summary>
    public async Task<(IReadOnlyList<MyEventRow> Items, int Total)> ListMineAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        // Events the caller OWNS, plus events they can manage through a representation they hold a seat
        // in (D-268). Ownership comes first and stands alone: a personally-represented event has no
        // membership behind it at all, which is exactly why this can no longer be a membership join.
        var q = db.Events.AsNoTracking().Where(e => e.DeletedAt == null
            && (e.CreatedBy == userId
                || db.Memberships.Any(m => m.UserId == userId && m.OrgId == e.RepresentingOrgId)));

        var total = await q.CountAsync(ct);
        var events = await q.OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var ids = events.Select(e => e.Id).ToList();
        var stats = await EventStatsAsync(ids, ct);
        var catIds = events.Select(e => e.CategoryId).Distinct().ToList();
        var catNames = await db.EventCategories.AsNoTracking()
            .Where(c => catIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var orgIds = events.Select(e => e.RepresentingOrgId).Distinct().ToList();
        var orgs = await db.Organizations.AsNoTracking()
            .Where(o => orgIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Name, o.IsPersonal, o.VerificationStatus })
            .ToDictionaryAsync(o => o.Id, o => o, ct);

        var rows = events.Select(e =>
        {
            var s = stats[e.Id];
            var org = orgs.GetValueOrDefault(e.RepresentingOrgId);
            return new MyEventRow(
                e.Id, e.Title, e.Slug, e.Status.ToString(), e.Visibility.ToString(),
                catNames.GetValueOrDefault(e.CategoryId), e.VenueName, e.City, e.Capacity,
                e.IsPaid || s.IsPaid,
                s.Sold, s.CheckedIn, s.RevenuePaise,
                e.StartsAt, e.UpdatedAt, e.SettlementCurrency,
                // A self-representation row reads as Personal and surrenders its identity entirely — the
                // client is never told an organization exists, because as far as the domain goes, none does.
                org is null || org.IsPersonal
                    ? RepresentationView.ForPersonal()
                    : RepresentationView.ForOrganization(org.Id, org.Name,
                        org.VerificationStatus == OrgVerificationStatus.Verified));
        }).ToList();

        return (rows, total);
    }

    public Task<IReadOnlyList<EventSummary>> UpcomingAsync(int limit, CancellationToken ct = default) => search.UpcomingAsync(limit, ct);
    public Task<IReadOnlyList<EventSummary>> TrendingAsync(int limit, CancellationToken ct = default) => search.TrendingAsync(limit, ct);
    public Task<IReadOnlyList<EventSummary>> FeaturedAsync(int limit, CancellationToken ct = default) => search.FeaturedAsync(limit, ct);
    public Task<IReadOnlyList<EventSummary>> LatestAsync(int limit, CancellationToken ct = default) => search.LatestAsync(limit, ct);
    public Task<IReadOnlyList<EventSummary>> RelatedAsync(Guid eventId, int limit, CancellationToken ct = default) => search.RelatedAsync(eventId, limit, ct);

    /// <summary>Composition depth of an event (root = 1) by walking the <see cref="Event.ParentEventId"/> chain — §3.4
    /// rule 1 caps this at 3. Bounded to guard against a cycle from corrupt data. Lineage (Series) is not composition
    /// and is not counted (§3.4 rule 5).</summary>
    private async Task<int> CompositionDepthAsync(Guid eventId, CancellationToken ct)
    {
        var depth = 1;
        var parent = await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.ParentEventId).FirstOrDefaultAsync(ct);
        while (parent is { } pid && depth < 10)
        {
            depth++;
            parent = await db.Events.AsNoTracking().Where(e => e.Id == pid).Select(e => e.ParentEventId).FirstOrDefaultAsync(ct);
        }
        return depth;
    }

    private async Task<bool> CanViewAsync(Event ev, Guid? viewerUserId, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin) return true;
        // D-186: a suspended/hidden event is excluded from the Published fast-path (so public/anonymous
        // viewers get the same 404-not-403 a draft gets) but stays visible to its own org members below —
        // they need to see the moderation state and reason, not just lose the event.
        if (ev.Status == EventStatus.Published && !ev.IsSuspended && !ev.IsHidden) return true;
        if (viewerUserId is null) return false;
        // The owner always sees their own event, whatever its status and whoever it represents (D-268).
        if (ev.CreatedBy == viewerUserId.Value) return true;
        if ((await authority.ResolveOrgAsync(viewerUserId.Value, ev.RepresentingOrgId, isAdmin: false, ct)).IsMember) return true;
        // D-191: a VerificationReviewer (staff, but not necessarily the kurx_admin/SuperAdmin claim `isAdmin`
        // checks) must be able to see any event's detail — the same access AdminEventEndpoints.cs's
        // VerificationReviewer-gated routes already grant for the list/moderation surface.
        return await roles.HasRoleAsync(viewerUserId.Value, PlatformRole.VerificationReviewer, ct);
    }

    /// <summary>D-378 — the Content the listing is built from, required at submission.
    ///
    /// <para>Separate from <see cref="ValidatePublishReadiness"/> rather than folded into it: that gate
    /// runs on <c>publish</c>, which an already-approved event reaches, and adding fields to it would
    /// retroactively make previously-approved events unpublishable. This one runs on the single
    /// transition where the organiser is declaring the event ready, so it can only ever refuse an event
    /// that has not yet been reviewed.</para>
    ///
    /// <para>Whitespace is not content: <c>IsNullOrWhiteSpace</c>, matching the <c>.trim()</c> both
    /// clients apply. The length ceilings are not repeated here — <c>ApplyFieldGroups</c> already refuses
    /// them on the write that would store the over-long value, so no row can reach this holding one.</para></summary>
    private static string? ValidateSubmissionReadiness(Event ev)
    {
        // Content — what the listing card and the share preview are built from.
        if (string.IsNullOrWhiteSpace(ev.Tagline)) return "missing_tagline";
        if (string.IsNullOrWhiteSpace(ev.ShortDescription)) return "missing_short_description";
        if (string.IsNullOrWhiteSpace(ev.Rules)) return "missing_rules";

        /*
         * Location — conditional on Mode, and the condition is the rule rather than a softening of it.
         * A join link on an in-person event and a floor number on an online one are not missing, they do
         * not exist; demanding both groups would make every event unsubmittable. Both wizards render
         * these two groups on exactly this condition, so the server refuses precisely what the clients
         * refuse to leave blank.
         */
        if (ev.EventMode != EventMode.Online)
        {
            if (string.IsNullOrWhiteSpace(ev.Building)) return "missing_building";
            if (string.IsNullOrWhiteSpace(ev.Floor)) return "missing_floor";
            if (string.IsNullOrWhiteSpace(ev.Room)) return "missing_room";
            if (string.IsNullOrWhiteSpace(ev.GoogleMapsUrl)) return "missing_maps_url";
        }
        if (ev.EventMode != EventMode.Offline)
        {
            // `online_url_required` already guards create/update; this is the same fact at the gate that
            // decides submittability, so the message names the step rather than the write.
            if (string.IsNullOrWhiteSpace(ev.OnlineUrl)) return "missing_online_url";
            if (string.IsNullOrWhiteSpace(ev.MeetingPlatform)) return "missing_meeting_platform";
            if (string.IsNullOrWhiteSpace(ev.MeetingPassword)) return "missing_meeting_password";
        }

        // Windows — every window, and the pair ordering `ApplyFieldGroups` already refuses stays there.
        if (ev.RegistrationOpensAt is null) return "missing_registration_opens";
        if (ev.RegistrationClosesAt is null) return "missing_registration_closes";
        if (ev.CheckinOpensAt is null) return "missing_checkin_opens";
        if (ev.CheckinClosesAt is null) return "missing_checkin_closes";

        /*
         * A Private event is never SHOWN the results date, the certificate date, the team cap or EITHER
         * AGE BOUND — the private-gathering archetype has `scoring`, `certificates` and `teams`
         * Unsupported, and the wizards hide the whole eligibility age block with them. Only Gender
         * survives the filter. Requiring any of the five here would make every private event permanently
         * unsubmittable through a door no client can open: the organiser would be refused for a field
         * that was never on their screen.
         *
         * Keyed on `Product`, which is derived and snapshotted from the chosen Type at create (D-266 M1),
         * so it cannot disagree with the archetype.
         *
         * The age bounds sat OUTSIDE this block in the first version, while both clients skipped them —
         * so a wedding could be completed in the wizard and then refused by the server with
         * `missing_min_age`. Found by the live E2E's private walk, which is the only check that submits a
         * Private event end to end.
         */
        if (ev.Product != EventProduct.Private)
        {
            if (ev.MinAge is null) return "missing_min_age";
            if (ev.MaxAge is null) return "missing_max_age";
            if (ev.ResultDate is null) return "missing_result_date";
            if (ev.CertificateReleaseAt is null) return "missing_certificate_release";
            if (ev.MaxTeams is null) return "missing_max_teams";
        }

        // Legal.
        if (string.IsNullOrWhiteSpace(ev.TermsUrl)) return "missing_terms_url";
        if (string.IsNullOrWhiteSpace(ev.CodeOfConduct)) return "missing_code_of_conduct";
        if (string.IsNullOrWhiteSpace(ev.RefundPolicy)) return "missing_refund_policy";
        if (string.IsNullOrWhiteSpace(ev.CancellationPolicy)) return "missing_cancellation_policy";

        return null;
    }

    private static string? ValidatePublishReadiness(Event ev)
    {
        if (string.IsNullOrWhiteSpace(ev.Description)) return "missing_description";
        // An Online event has no venue by definition, so requiring one made it unpublishable through this
        // door — the only door the wizards and the whole review lifecycle use. The V3 §14.2 Scheduled gate
        // below already had the right rule; this copy never learned it. Same predicate, deliberately not
        // extracted: two call sites do not need a helper, they need to agree (D-299).
        //
        // The Online arm is defensive: ValidateMode already refuses an Online event with no OnlineUrl on
        // create AND update, so the API cannot produce that row. It stays because the sibling gate carries
        // it too, and because a publish gate that leans on an invariant enforced somewhere else is exactly
        // how the venue half of this got wrong.
        if (ev.EventMode == EventMode.Online
                ? string.IsNullOrWhiteSpace(ev.OnlineUrl)
                : ev.VenueId is null && string.IsNullOrWhiteSpace(ev.VenueName))
            return ev.EventMode == EventMode.Online ? "missing_online_url" : "missing_venue";
        return null;
    }

    /// <summary>V3 §14.2 five validation gates + §14.3 approval gate, applied per target. Additive: `publish` (direct
    /// Draft/review-state→Published) keeps its existing readiness/org/paid checks in <c>TransitionAsync</c> and here only
    /// gains the approval gate (a no-op without a chain); the granular Scheduled/Live/Completed transitions carry the
    /// respective gate. Returns an error code, or null when the gate passes.</summary>
    private async Task ApplyMaterialChangeAsync(Event ev, Guid actorId, bool isAdmin, DateTime beforeStart, DateTime beforeEnd,
        Guid? beforeVenue, EventMode beforeMode, string beforeVenueName, CancellationToken ct)
    {
        // V3 §14.5: a change to date/venue/mode after any Registration exists opens a refund window (7 days or event
        // start, whichever sooner), notifies every registrant (existing notification infra), and records before/after in
        // the audit spine. Refunds within the window stay registrant-initiated via the existing RefundService (no
        // auto-refund, no money-path change). Offline→Online locks (never deletes) the venue config, kept as history.
        var changed = ev.StartsAt != beforeStart || ev.EndsAt != beforeEnd || ev.VenueId != beforeVenue
            || !string.Equals(ev.VenueName, beforeVenueName, StringComparison.Ordinal) || ev.EventMode != beforeMode;
        if (!changed) return;
        await OpenRefundWindowAndNotifyAsync(ev, actorId, isAdmin,
            new { startsAt = beforeStart, endsAt = beforeEnd, venueId = beforeVenue, mode = beforeMode.ToString() },
            new { startsAt = ev.StartsAt, endsAt = ev.EndsAt, venueId = ev.VenueId, mode = ev.EventMode.ToString() }, ct);
    }

    /// <summary>V3 §14.5 core, shared by a date/venue/mode edit and a sub-event cancellation: opens a refund window
    /// (7 days or event start, whichever sooner — but <b>never shortening an already-open window</b>, N2), notifies
    /// every registrant, and records before/after in the audit spine. A no-op if no registration exists. Refunds within
    /// the window are registrant-initiated via the existing RefundService (no auto-refund, no money-path change).</summary>
    private async Task OpenRefundWindowAndNotifyAsync(Event ev, Guid actorId, bool isAdmin, object before, object after, CancellationToken ct)
    {
        var registrantIds = await db.Registrations.AsNoTracking()
            .Where(r => r.EventId == ev.Id && r.SubjectId != null && r.State != RegistrationState.Cancelled)
            .Select(r => r.SubjectId!.Value).Distinct().ToListAsync(ct);
        if (registrantIds.Count == 0) return;   // §14.5 applies only AFTER a registration exists

        var deadline = ev.StartsAt < DateTime.UtcNow.AddDays(7) ? ev.StartsAt : DateTime.UtcNow.AddDays(7);
        var newWindow = deadline > DateTime.UtcNow ? deadline : DateTime.UtcNow;
        ev.RefundWindowEndsAt = ev.RefundWindowEndsAt is { } open && open > newWindow ? open : newWindow;   // N2: never shorten

        audit.Write(new AuditEvent("event.material_change", "events", ev.Id, isAdmin ? "admin" : "user", actorId,
            before, new { change = after, refundWindowEndsAt = ev.RefundWindowEndsAt }));

        foreach (var uid in registrantIds)
            db.Notifications.Add(new Notification
            {
                UserId = uid, Kind = "event_material_change", Title = $"Change to {ev.Title}",
                Body = "An important detail changed. You may request a refund within the refund window.",
                DataJson = $"{{\"event_id\":\"{ev.Id}\",\"refund_window_ends_at\":\"{ev.RefundWindowEndsAt:o}\"}}",
            });
    }

    // <paramref name="readOnly"/>: when true the approval sub-check is evaluated WITHOUT materialising the request
    // (the workspace publish-checklist projection). A real transition passes false — it must materialise the request.
    /// <summary>D-266 M4 — the publish gate CONSUMES <c>PolicyResolver.PublishBlockers</c> (via
    /// <see cref="IEventPolicyService"/>) rather than re-deriving it. Before this the platform had two
    /// publish validations: this gate, and the blockers /policy-requirements reported. Two validations for
    /// one question drift, and the list a reviewer reads would stop matching the one that actually refuses
    /// the publish — which is the exact failure the policy engine was built to prevent.</summary>
    private async Task<string?> PolicyBlockerAsync(Event ev, CancellationToken ct)
    {
        var req = await policy.GetForEventAsync(ev.Id, ct);
        if (!req.Ok) return null;                      // unresolvable policy is not itself a blocker
        return req.Value!.PublishBlockers.FirstOrDefault();
    }

    private async Task<string?> TransitionGateAsync(Event ev, EventStatus target, CancellationToken ct, bool readOnly = false)
    {
        // Same gate rule, one source of truth: the real transition materialises; a read-only preview only evaluates.
        async Task<bool> ApprovalOk() => readOnly
            ? await approvals.IsCompleteAsync(ev.Id, ct)
            : await approvals.EnsureAndCheckCompleteAsync(ev.Id, ct);

        switch (target)
        {
            case EventStatus.Scheduled:   // Publish → SCHEDULED gate (§14.2)
                if (await PolicyBlockerAsync(ev, ct) is { } schedBlocker) return schedBlocker;
                if (string.IsNullOrWhiteSpace(ev.Description)) return "missing_description";
                if (ev.EventMode == EventMode.Online ? string.IsNullOrWhiteSpace(ev.OnlineUrl) : (ev.VenueId is null && string.IsNullOrWhiteSpace(ev.VenueName))) return "missing_venue_or_url";
                if (ev.OrgUnitId is null) return "missing_owner_unit";
                return await ApprovalOk() ? null : "approval_pending";
            // D-266 M4: Approved joins the states publish may leave from — it is the reviewed door,
            // and the only one a Public product may use once the review lifecycle is enforced.
            // Legacy publish also arrives from the review queue (submit_review -> publish), so those
            // states must run the same readiness gate rather than falling through it.
            case EventStatus.Published when ev.Status is EventStatus.Draft or EventStatus.Approved
                                                      or EventStatus.PendingReview or EventStatus.UnderReview:
                // D-266 M5 — the policy blockers were previously consumed on the Scheduled leg ALONE, so
                // every rule the policy engine owned was reported by /policy-requirements and then ignored
                // by the publish that actually mattered. A gate that refuses one path to Published and waves
                // through another is not a gate. This leg's policy check runs in TransitionAsync instead of
                // here, so it lands after org verification and readiness — see the comment there.
                return await ApprovalOk() ? null : "approval_pending";
            case EventStatus.Published:   // open_registration (Scheduled → Published) — Open-registration gate (§14.2)
                if (await PolicyBlockerAsync(ev, ct) is { } openBlocker) return openBlocker;
                if (!await db.TicketTypes.AnyAsync(t => t.EventId == ev.Id, ct)) return "no_pass";
                if (!await db.InventoryPools.AnyAsync(p => p.EventId == ev.Id, ct)) return "no_inventory_pool";
                if (string.IsNullOrWhiteSpace(ev.SettlementCurrency)) return "no_currency";
                return await ApprovalOk() ? null : "approval_pending";
            case EventStatus.Live:        // Go-live gate (§14.2): staff assigned
                return await db.EventAssignments.AnyAsync(a => a.EventId == ev.Id && a.Status == AssignmentStatus.Accepted, ct) ? null : "no_staff_assigned";
            case EventStatus.Completed:   // Complete gate (§14.2): results published if any Stage exists
                if (await db.Stages.AnyAsync(s => s.EventId == ev.Id, ct)
                    && !await db.StageResults.AnyAsync(r => db.Stages.Where(s => s.EventId == ev.Id).Select(s => s.Id).Contains(r.StageId)
                        && (r.State == ResultState.Published || r.State == ResultState.Corrected), ct))
                    return "results_not_published";
                return null;
            default: return null;
        }
    }

    /// <summary>D-266 M1 — read the behaviour axis off the chosen Type node. Returns a null slug when the
    /// type is unmapped (an admin-created type that predates its archetype assignment); like KindSlug this
    /// is additive and never blocks creation, and <see cref="ArchetypeSeeder"/> backfills it on next boot.
    /// An unmapped type falls back to Public — Private is only ever a deliberate classification.</summary>
    private async Task<(string? Slug, EventProduct Product)> ResolveArchetypeAsync(Guid? typeId, CancellationToken ct)
    {
        if (typeId is null) return (null, EventProduct.Public);
        var node = await db.EventCategories.AsNoTracking()
            .Where(c => c.Id == typeId)
            .Select(c => new { c.ArchetypeSlug, c.ProductClass })
            .FirstOrDefaultAsync(ct);
        return (node?.ArchetypeSlug, node?.ProductClass ?? EventProduct.Public);
    }

    private async Task<string?> ApplyVenueAsync(Event ev, Guid orgId, Guid? venueId, string? venueName,
        string? venueAddress, string? city, double? lat, double? lng, CancellationToken ct)
    {
        if (venueId is not null)
        {
            var venue = await db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == venueId && v.OrgId == orgId, ct);
            if (venue is null) return "invalid_venue";
            ev.VenueId = venue.Id;
            ev.VenueName = venue.Name;
            ev.VenueAddress = venue.Address;
            ev.City = venue.City;
            ev.Lat = venue.Lat;
            ev.Lng = venue.Lng;
            return null;
        }

        ev.VenueId = null;
        ev.VenueName = venueName?.Trim() ?? "";
        ev.VenueAddress = venueAddress?.Trim() ?? "";
        ev.City = city?.Trim() ?? "";
        ev.Lat = lat;
        ev.Lng = lng;
        return null;
    }

    private async Task SyncTagsAsync(Guid eventId, IReadOnlyList<string> tagNames, CancellationToken ct)
    {
        var existing = await db.EventTags.Where(t => t.EventId == eventId).ToListAsync(ct);
        db.EventTags.RemoveRange(existing);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wanted = new List<string>();
        foreach (var raw in tagNames)
        {
            var name = raw.Trim();
            if (name.Length == 0 || name.Length > 40 || !seen.Add(name)) continue;
            wanted.Add(name);
        }

        // DB-7: one lookup for every tag, not one per tag. This ran `ILike(t.Name, name)` inside the loop,
        // so a ten-tag event cost ten round trips — and `ILIKE` with no wildcards is case-insensitive
        // EQUALITY, which no index on this table could serve anyway (IX_tags_Name is a case-sensitive
        // unique btree). Batching removes N-1 round trips, which is the actual cost here; an index on
        // lower("Name") is deliberately NOT added, because `tags` is small enough that a sequential scan
        // is the cheaper plan and PostgreSQL is right to choose it.
        //
        // `t.Name.ToLower()` translates to `lower("Name") = ANY(...)`, preserving the case-insensitive
        // match exactly. Ordering by Id makes the winner deterministic: `IX_tags_Name` is case-SENSITIVE
        // unique, so "Music" and "music" can both exist, and the old FirstOrDefaultAsync picked between
        // them arbitrarily.
        var lowered = wanted.Select(n => n.ToLowerInvariant()).ToList();
        var byLower = new Dictionary<string, Guid>();
        foreach (var row in await db.Tags.AsNoTracking()
                     .Where(t => lowered.Contains(t.Name.ToLower()))
                     .OrderBy(t => t.Id)
                     .Select(t => new { t.Id, t.Name })
                     .ToListAsync(ct))
            byLower.TryAdd(row.Name.ToLowerInvariant(), row.Id);

        foreach (var name in wanted)
        {
            if (!byLower.TryGetValue(name.ToLowerInvariant(), out var tagId))
            {
                // Still per-tag: creating a tag is the rare path, and the slug probe needs the rows the
                // previous iteration may have just added.
                var tag = new Tag { Name = name, Slug = await UniqueTagSlugAsync(name, ct) };
                db.Tags.Add(tag);
                tagId = tag.Id;
                byLower[name.ToLowerInvariant()] = tagId;
            }
            db.EventTags.Add(new EventTag { EventId = eventId, TagId = tagId });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<EventDetail> ToDetailAsync(Event ev, CancellationToken ct)
    {
        var tags = await db.EventTags.Where(t => t.EventId == ev.Id)
            .Join(db.Tags, et => et.TagId, t => t.Id, (et, t) => t.Name)
            .OrderBy(n => n)
            .ToListAsync(ct);

        Venue? venue = ev.VenueId is not null
            ? await db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == ev.VenueId, ct)
            : null;
        var venueView = venue is not null
            ? new EventVenueView(venue.Id, venue.Name, venue.Address, venue.City, venue.Lat, venue.Lng, venue.GoogleMapsUrl)
            : new EventVenueView(null, ev.VenueName, ev.VenueAddress, ev.City, ev.Lat, ev.Lng, null);

        var mediaRows = await db.EventMedia.AsNoTracking().Where(m => m.EventId == ev.Id)
            .OrderBy(m => m.Kind).ThenBy(m => m.Sort)
            .Select(m => new EventMediaView(m.Id, m.Kind.ToString(), m.Key, m.Caption, m.Sort))
            .ToListAsync(ct);
        // A storage key is not fetchable; only a presigned URL is (D-302). The gallery, banner, logo,
        // thumbnail and promo video were all stored, all returned as keys, and all therefore unrenderable
        // — which is why no event image appeared anywhere on any client.
        var media = new List<EventMediaView>(mediaRows.Count);
        foreach (var m in mediaRows)
            media.Add(m with { Url = await SignAsync(m.Key, ct) });

        // Who the host is representing, named (D-302). Self-representation is persistence, never domain
        // (D-268) — an `IsPersonal` row is filtered out of every user-facing surface, so it resolves to null
        // here and the clients render the creator alone rather than an organization that does not exist.
        var representing = await db.Organizations.AsNoTracking()
            .Where(OrganizationScope.Real).Where(o => o.Id == ev.RepresentingOrgId)
            .Select(o => new EventRepresentationView(o.Id, o.Name, o.Slug, o.LogoKey,
                o.VerificationStatus == OrgVerificationStatus.Verified))
            .FirstOrDefaultAsync(ct);

        return new EventDetail(ev.Id, ev.RepresentingOrgId, ev.ParentEventId, ev.Title, ev.Slug, ev.ShortCode, ev.Subtitle, ev.Description,
            ev.CategoryId, ev.TypeId, ev.AudienceLevelId, ev.TemplateId, tags, venueView,
            ev.StartsAt, ev.EndsAt, ev.Timezone, ev.Capacity, ev.Visibility.ToString(), ev.Status.ToString(), ev.Language,
            ev.ContactEmail, ev.ContactPhone, ev.Website, ev.SocialLinksJson,
            ev.BannerKey, ev.IsFeatured, ev.ViewCount, media, ev.CreatedAt, ev.PublishedAt, ev.UpdatedAt,
            ev.EventMode.ToString(), ev.OnlineUrl, ev.SettlementCurrency,
            new EventContentView(ev.Tagline, ev.ShortDescription, ev.LogoKey, ev.ThumbnailKey,
                ev.PromoVideoKey, ev.Rules, ev.FaqJson,
                await SignAsync(ev.LogoKey, ct), await SignAsync(ev.ThumbnailKey, ct),
                await SignAsync(ev.PromoVideoKey, ct)),
            new EventLegalView(ev.TermsUrl, ev.TermsText, ev.CodeOfConduct, ev.RefundPolicy,
                ev.CancellationPolicy, ev.RequiresConsent, ev.ConsentText),
            new EventScheduleView(ev.RegistrationOpensAt, ev.RegistrationClosesAt, ev.CheckinOpensAt,
                ev.CheckinClosesAt, ev.ResultDate, ev.CertificateReleaseAt, ev.AutoClose),
            // MeetingPassword is intentionally absent — this projection is also the public one.
            new EventLocationDetailView(ev.Building, ev.Floor, ev.Room, ev.GoogleMapsUrl, ev.MeetingPlatform),
            new EventEligibilityView(ev.MinAge, ev.MaxAge, ev.GenderRestriction.ToString(),
                await TeamCapacityAsync(ev, ct)),
            new EventCommerceView(ev.PlatformFeePercent, ev.PlatformFeeFlatPaise, ev.TaxPercent,
                ev.TaxInclusive, ev.PrizePoolJson),
            representing,
            await SignAsync(ev.BannerKey, ct));
    }

    /*
     * D-375 — how many teams may enter, answered by the thing that actually decides.
     *
     * `events.MaxTeams` (D-265) was stored, echoed on the eligibility view, and enforced NOWHERE: an
     * organiser could type 50 while their team ticket sold 20 slots, and both numbers were presented as
     * fact on different screens. The authority is the registration unit's inventory — a `PerGroup` ticket
     * type's `Quantity`, which is what the pool draws against (V3 §17.1) — so that is what a client is
     * given whenever the event has one.
     *
     * The stored column survives as the fallback for an event with NO team ticket, where it contradicts
     * nothing and is the organiser's own note. It is not enforced and is not made a second authority:
     * wiring it into inventory is precisely the competing-capacity mistake §17.1 exists to prevent.
     *
     * Summed across team ticket types because an event may sell more than one team entry (a hackathon
     * with a junior and an open track); the total slots are the total teams.
     */
    private async Task<int?> TeamCapacityAsync(Event ev, CancellationToken ct)
    {
        // The quantities are listed rather than SUMmed in SQL because `SUM` over an empty set answers 0,
        // not null — which would report "0 teams may enter" for every event that has no team ticket at
        // all, exactly inverting the fallback. An event has a handful of ticket types, so the list is free.
        var teamSlots = await db.TicketTypes.AsNoTracking()
            .Where(t => t.EventId == ev.Id && t.DeletedAt == null && t.PricingUnit == PricingUnit.PerGroup)
            .Select(t => t.Quantity)
            .ToListAsync(ct);
        return teamSlots.Count > 0 ? teamSlots.Sum() : ev.MaxTeams;
    }

    /// <summary>Presign a storage key, or null when there is nothing stored. Null-in/null-out keeps
    /// every call site free of a guard it would otherwise repeat six times.</summary>
    private async Task<string?> SignAsync(string? key, CancellationToken ct)
        => string.IsNullOrWhiteSpace(key) ? null : await storage.PresignGetAsync(key, null, ct);

    // Online/Hybrid events require an OnlineUrl (D-064 A7). Returns an error code or null.
    private static string? ValidateMode(EventMode mode, string? onlineUrl)
        => mode is EventMode.Online or EventMode.Hybrid && string.IsNullOrWhiteSpace(onlineUrl)
            ? "online_url_required" : null;

    private static EventMode ParseMode(string? s)
        => Enum.TryParse<EventMode>(s, ignoreCase: true, out var m) && Enum.IsDefined(m) ? m : EventMode.Offline;

    // A verified Representative (D-075) reaches Manager authority on events representing its organization
    // (D-269); the trust-bearing gates (publish/sell) are enforced separately via live capability +
    // org-verification checks.

    /// <summary>Whether <paramref name="userId"/> may manage this event. **The event's creator owns it**
    /// (D-268) — that is the first and sufficient grant, and it is what makes "a User owns an Event" true
    /// in code rather than only in prose.
    ///
    /// <para>Before this, every event-scoped authorization here resolved to
    /// <c>RoleAsync(userId, ev.RepresentingOrgId)</c> alone: the right to manage an event derived *entirely* from
    /// membership of the organization it represents, and <see cref="Event.CreatedBy"/> was consulted only
    /// for notifications. That is the Organization-owns-Event model. It is also the sole reason a person
    /// hosting under their own name needed a synthetic organization to be a member of.</para>
    ///
    /// <para>The organization role is retained as an <b>additional</b> grant — that is what representation
    /// is for: staff of the represented institution collaborate on its events. It never replaces the
    /// owner's authority, and it is not required for a personally-represented event.</para></summary>
    /// <para>D-269: the rule itself now lives in <see cref="IEventAuthority"/>, which is the single source
    /// of truth for every event surface. This stays as a named shorthand for the ~7 call sites here.</para>
    private async Task<bool> CanManageEventAsync(Guid userId, Event ev, bool isAdmin, CancellationToken ct)
        => (await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageLifecycle);

    /*
     * D-379 — `ResolveSelfRepresentationAsync` was deleted here, along with the row it minted.
     *
     * It existed for one reason: `events.OrgId` is a non-null FK (D-055/D-075 both weighed making it
     * nullable and rejected it, because 300+ read sites across ~30 services dereference it), so an event
     * representing nobody still needed something to point at. It created an `IsPersonal` organization
     * named after the person, plus an Owner membership and a zero-balance wallet.
     *
     * Every event now represents a real organization, so nothing needs minting. The method is removed
     * rather than left uncalled: an unreachable factory for the one row type the platform no longer
     * permits is precisely the hidden path this decision exists to close, and "nobody calls it today" is
     * not a property that survives the next refactor.
     *
     * EXISTING rows are untouched. They are still read, still resolve, and the events pointing at them
     * still load — `OrganizationScope.Real` (D-368) keeps them out of every count and list exactly as
     * before. What is gone is the ability to create another one.
     */

    private async Task<string> UniqueEventSlugAsync(string title, CancellationToken ct)
    {
        var slug = Slugify(title);
        while (await db.Events.AnyAsync(e => e.Slug == slug, ct))
            slug = $"{Slugify(title)}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant()}";
        return slug;
    }

    // Voice/SMS-friendly reference, distinct from the slug (D-036). Random (not title-derived) so
    // it stays short regardless of title length; immutable once created, same convention as Slug.
    private async Task<string> UniqueEventShortCodeAsync(CancellationToken ct)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity
        while (true)
        {
            var bytes = RandomNumberGenerator.GetBytes(6);
            var code = new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
            if (!await db.Events.AnyAsync(e => e.ShortCode == code, ct)) return code;
        }
    }

    private async Task<string> UniqueTagSlugAsync(string name, CancellationToken ct)
    {
        var slug = Slugify(name);
        while (await db.Tags.AnyAsync(t => t.Slug == slug, ct))
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
        return slug.Length == 0 ? "item" : slug[..Math.Min(slug.Length, 60)];
    }
}
