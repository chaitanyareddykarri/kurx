using System.Globalization;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Kurx.Infrastructure.Users;

/// <summary>Professional Identity read-model (D-201, see <c>docs/architecture/PROFESSIONAL_IDENTITY_SPEC.md</c>).
/// Every value is DERIVED from verified rows (participations, certificates, memberships, check-ins,
/// badges, identity KYC, ally connections) — the self-declared profile (headline/bio/skills) is
/// authority-zero and never a trust input ("bio is never proof", D-041). This is a projection over
/// existing V3 data (+ the new <c>AllyConnection</c> table, owned by <see cref="AllyService"/>); it
/// owns no tables of its own.</summary>
public class PublicProfileService(
    KurxDbContext db, IProfileVisibilityResolver visibility,
    IProfileFactSetLoader facts) : IPublicProfileService
{
    /// <summary>The Professional Journey (D-223). Since D-224 the engine is a pure function over the
    /// shared fact-set: this method loads the facts once and projects them, which is the pattern every
    /// engine added in 3C follows — and the reason the Resume can project all of them from one load.</summary>
    public async Task<ServiceResult<IReadOnlyList<JourneyNode>>> GetJourneyAsync(
        string username, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<IReadOnlyList<JourneyNode>>.Fail("not_found");

        return ServiceResult<IReadOnlyList<JourneyNode>>.Success(
            JourneyEngine.Build(await facts.LoadAsync(user.Id, ct), access));
    }

    public async Task<ServiceResult<ProfileMetrics>> GetMetricsAsync(
        string username, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<ProfileMetrics>.Fail("not_found");
        if (!access.CanSee(ProfileSection.Metrics))
            return ServiceResult<ProfileMetrics>.Fail("forbidden");

        return ServiceResult<ProfileMetrics>.Success(
            MetricsEngine.Build(await facts.LoadAsync(user.Id, ct), access, DateTime.UtcNow));
    }

    public async Task<ServiceResult<ExperienceSummary>> GetExperienceAsync(
        string username, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<ExperienceSummary>.Fail("not_found");
        if (!access.CanSee(ProfileSection.Events))
            return ServiceResult<ExperienceSummary>.Fail("forbidden");

        return ServiceResult<ExperienceSummary>.Success(
            ExperienceEngine.Build(await facts.LoadAsync(user.Id, ct), DateTime.UtcNow));
    }

    public async Task<ServiceResult<ContributionsSummary>> GetContributionsAsync(
        string username, int months, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<ContributionsSummary>.Fail("not_found");
        if (!access.CanSee(ProfileSection.Contributions))
            return ServiceResult<ContributionsSummary>.Fail("forbidden");

        return ServiceResult<ContributionsSummary>.Success(
            ContributionsEngine.Build(await facts.LoadAsync(user.Id, ct), access, months, DateTime.UtcNow));
    }

    /// <summary>The resume is the one surface that projects every section at once — which is exactly
    /// what the shared fact-set (D-224) was built for: one load, one render, no per-section fan-out.</summary>
    public async Task<ServiceResult<byte[]>> GetResumePdfAsync(
        string username, string profileUrl, Guid? viewerId = null, CancellationToken ct = default)
    {
        var profile = await GetProfileAsync(username, viewerId, ct);
        if (!profile.Ok) return ServiceResult<byte[]>.Fail(profile.Error ?? "not_found");

        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<byte[]>.Fail("not_found");

        var factSet = await facts.LoadAsync(user.Id, ct);
        var pdf = new ResumeEngine().Render(
            profile.Value!, factSet,
            ExperienceEngine.Build(factSet, DateTime.UtcNow),
            JourneyEngine.Build(factSet, access),
            access, profileUrl, DateTime.UtcNow);

        return ServiceResult<byte[]>.Success(pdf);
    }

    /// <summary>Resolves the owner and the viewer's access in one step (D-221). Returns null when the
    /// profile should read as absent — either no such username, or the Profile section itself is not
    /// visible to this viewer. Both collapse to the same 404 upstream (D-018), so a hidden profile is
    /// indistinguishable from one that never existed.
    ///
    /// <para>This is the single entry point every read <b>in this file</b> goes through; the six
    /// hand-rolled boolean checks that used to live here are gone.</para>
    ///
    /// <para><b>Remaining gap, narrowed but not closed:</b> <see cref="AllyService"/> now gates the
    /// profile owner through the resolver like everything else, but its counterparty batch still filters
    /// on the legacy <c>ProfilePublic</c> column to avoid one resolver call per card — as do 14 other
    /// "may this other person be shown here" gates across eight services (attendees, assignments,
    /// speakers, reviews, leaderboards, org members, search). All 15 must move to a <b>batched</b> tier
    /// lookup before the boolean columns can be dropped (D-221's planned follow-up); a per-row resolve
    /// at any of them is N+1. Enumerated in D-232.</para></summary>
    private async Task<(Kurx.Domain.Entities.User User, SectionAccess Access)?> LoadAsync(
        string username, Guid? viewerId, CancellationToken ct)
    {
        username = username.ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null) return null;
        // BUG-B: a moderated account's public profile is gone, not merely unauthenticatable. This is the
        // single chokepoint every public profile section routes through, so gating here covers profile,
        // timeline, metrics, experience, contributions, journey, certificates, allies and the resume in
        // one place rather than per-endpoint — the shape that made the original miss possible.
        if (user.BannedAt is not null || user.SuspendedAt is not null) return null;

        var access = await visibility.ResolveAsync(user.Id, viewerId, ct);
        return access.CanSee(ProfileSection.Profile) ? (user, access) : null;
    }

    private const int ProfileAchievementCap = 24;

    // Product role vocabulary <- EventParticipant.RoleSlug (D-202). Slugs outside this map still
    // count and render — as a humanized fallback chip, never hidden.
    private static readonly Dictionary<string, string> RoleLabels = new()
    {
        ["attendee"] = "Participant",
        ["competitor"] = "Competitor",
        ["volunteer"] = "Volunteer",
        ["staff"] = "Staff",
        ["coordinator"] = "Coordinator",
        ["manager"] = "Organizer",
        ["owner"] = "Host",
        ["speaker"] = "Speaker",
        ["judge"] = "Judge",
        ["mentor"] = "Mentor",
        ["sponsor"] = "Sponsor",
    };

    public async Task<ServiceResult<PublicProfileView>> GetProfileAsync(
        string username, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<PublicProfileView>.Fail("not_found");

        // One pipeline (D-229). Everything below projects the same fact-set — the cached parallel
        // summary, the separate org query and the direct achievement queries are gone, which is what
        // makes it structurally impossible for two surfaces to disagree about one number.
        var now = DateTime.UtcNow;
        var factSet = await facts.LoadAsync(user.Id, ct);

        var metrics = MetricsEngine.Build(factSet, access, now);
        var achievements = AchievementEngine.Build(factSet, access, ProfileAchievementCap);
        var orgs = access.CanSee(ProfileSection.Organizations)
            ? OrganizationsEngine.Build(factSet, now)
            : Array.Empty<OrgProfileView>();
        var college = ParseCollege(user.EducationJson);

        var stats = new ProfileStats(
            metrics.EventsOrganized, metrics.EventsAttended, metrics.Certificates,
            metrics.EventsParticipated, metrics.AchievementCertificates, metrics.AllyCount);

        var badges = new VerificationBadges(
            factSet.Trust.IdentityVerified,
            factSet.Memberships.Any(m => m.IsVerified),
            // "Organizer" is a trust signal, not a count, so it survives a hidden Events section only
            // as a boolean the owner already publishes by having run a public event.
            ProfileCounts.OrganizedEventIds(factSet, now).Count > 0,
            metrics.Certificates,
            Math.Max(0, (int)((now - user.CreatedAt).TotalDays / 365)),
            factSet.Trust.EmailVerified,
            factSet.Trust.SpeakerLinked,
            factSet.Memberships.Any(m => m.IsVerified && m.OrgIsVerified));

        // Labels and the derived headline come from one engine (D-225) so they can never disagree —
        // the headline is the short form of the same labels, not a second derivation.
        var identityLabels = IdentityEngine.BuildLabels(factSet, achievements);
        var derivedHeadline = IdentityEngine.BuildHeadline(factSet, achievements);
        var summary = BuildSummary(user.Name, metrics, orgs.Count);

        return ServiceResult<PublicProfileView>.Success(new PublicProfileView(
            user.Id, user.Name, user.Username!, user.Headline, user.Bio,
            user.AvatarKey, user.CoverKey, college, user.LinksJson, user.Skills,
            stats, orgs, summary, badges, metrics.EventDna, achievements, identityLabels, derivedHeadline,
            user.Languages, user.Interests,
            // Month precision, formatted invariantly so the key is the same string in every locale.
            // The column is UTC; a month boundary is not worth localising and doing so would make two
            // viewers disagree about what a profile says.
            JoinedAt: user.CreatedAt.ToUniversalTime().ToString("yyyy-MM", CultureInfo.InvariantCulture)));
    }

    public async Task<IReadOnlyList<PublicUserSearchResult>> SearchUsersAsync(
        string query, int page, int pageSize, CancellationToken ct = default)
    {
        var term = query.Trim();
        if (term.Length < 2) return Array.Empty<PublicUserSearchResult>();

        var rows = await db.Users.AsNoTracking()
            // BUG-B: a banned or suspended account must not be discoverable. Moderation previously
            // reached authentication only, so a banned identity stayed searchable by name and username.
            //
            // THE ONE DOCUMENTED `ProfilePublic` READ LEFT IN THE PRODUCT (D-233). Every other gate moved
            // to the batch primitive; this one cannot, because the filter has to run *inside* the query:
            // it precedes `Skip`/`Take`, and post-filtering a page in memory would return short pages.
            // It is safe only because `UpdateSettingsAsync` dual-writes this column, so it is exactly
            // equivalent to "Profile tier is Public" — and it is deliberately NOT viewer-aware, because
            // global discoverability is a product rule (only fully-public profiles are searchable), not
            // a per-viewer entitlement. Dropping the boolean columns therefore needs one more step:
            // backfill `SectionVisibilityJson` for every row so the tier can be filtered in SQL without
            // a fallback. Named in D-233 as the remaining prerequisite.
            .Where(u => u.ProfilePublic && u.Username != null
                && u.BannedAt == null && u.SuspendedAt == null
                && (EF.Functions.ILike(u.Name, $"%{term}%") || EF.Functions.ILike(u.Username!, $"%{term}%")))
            // Names are the least unique key on the platform — two "Rahul Sharma" rows tie outright, and
            // a people search is exactly where duplicates across pages get noticed (DB-6).
            .OrderBy(u => u.Name).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new { u.Id, u.Name, u.Username, u.AvatarKey, u.Headline })
            .ToListAsync(ct);

        return rows.Select(u => new PublicUserSearchResult(u.Id, u.Name, u.Username!, u.AvatarKey, u.Headline)).ToList();
    }

    public async Task<ServiceResult<IReadOnlyList<PublicEventCard>>> GetEventsAsync(
        string username, string type, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<IReadOnlyList<PublicEventCard>>.Fail("not_found");

        var isAttended = string.Equals(type, "attended", StringComparison.OrdinalIgnoreCase);
        if (isAttended && !access.CanSee(ProfileSection.Attended))
            return ServiceResult<IReadOnlyList<PublicEventCard>>.Fail("forbidden");
        if (!isAttended && !access.CanSee(ProfileSection.Events))
            return ServiceResult<IReadOnlyList<PublicEventCard>>.Fail("forbidden");

        var now = DateTime.UtcNow;
        List<PublicEventCard> items;

        if (string.Equals(type, "attended", StringComparison.OrdinalIgnoreCase))
        {
            var raw = await db.Tickets.AsNoTracking()
                .Where(t => t.UserId == user.Id && t.State == TicketState.CheckedIn)
                .Join(db.Events.AsNoTracking()
                    .Where(e => e.EndsAt < now && e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                    t => t.EventId, e => e.Id, (t, e) => e)
                .Distinct()   // a person may hold >1 checked-in ticket for one event — dedupe to the event
                .Join(db.Organizations.AsNoTracking(), e => e.RepresentingOrgId, o => o.Id,
                    (e, o) => new { e.Id, e.Title, e.Slug, e.BannerKey, e.StartsAt, e.City, e.Visibility, OrgName = o.Name })
                // DB-6: StartsAt alone is not a total order — a festival's sub-events and any two events
                // sharing a start slot tie, and the database is free to break that tie differently per
                // query, which repeats or drops a row between pages.
                .OrderByDescending(x => x.StartsAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);
            var certByEvent = await CertLookupAsync(user.Id, raw.Select(r => r.Id).ToList(), ct);
            items = raw.Select(r =>
            {
                certByEvent.TryGetValue(r.Id, out var cert);
                return new PublicEventCard(r.Id, r.Title, r.Slug, r.BannerKey, r.StartsAt, r.City, r.OrgName,
                    new[] { "Attended" }, r.Visibility.ToString(), cert.VerifyCode, cert.IsAchievement);
            }).ToList();
        }
        else
        {
            var orgIds = await db.Memberships.AsNoTracking()
                .Where(m => m.UserId == user.Id).Select(m => m.OrgId).ToListAsync(ct);

            var raw = await db.Events.AsNoTracking()
                .Where(e => (orgIds.Contains(e.RepresentingOrgId) || e.CreatedBy == user.Id)
                    && (e.Status == EventStatus.Published || e.Status == EventStatus.Closed)
                    && e.EndsAt < now && e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed)
                .Join(db.Organizations.AsNoTracking(), e => e.RepresentingOrgId, o => o.Id,
                    (e, o) => new { e.Id, e.Title, e.Slug, e.BannerKey, e.StartsAt, e.City, e.Visibility, OrgName = o.Name })
                // DB-6: StartsAt alone is not a total order — a festival's sub-events and any two events
                // sharing a start slot tie, and the database is free to break that tie differently per
                // query, which repeats or drops a row between pages.
                .OrderByDescending(x => x.StartsAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);
            var certByEvent = await CertLookupAsync(user.Id, raw.Select(r => r.Id).ToList(), ct);
            items = raw.Select(r =>
            {
                certByEvent.TryGetValue(r.Id, out var cert);
                return new PublicEventCard(r.Id, r.Title, r.Slug, r.BannerKey, r.StartsAt, r.City, r.OrgName,
                    new[] { "Organizer" }, r.Visibility.ToString(), cert.VerifyCode, cert.IsAchievement);
            }).ToList();
        }

        return ServiceResult<IReadOnlyList<PublicEventCard>>.Success(items);
    }

    public async Task<ServiceResult<IReadOnlyList<PublicCertificateCard>>> GetCertificatesAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<IReadOnlyList<PublicCertificateCard>>.Fail("not_found");
        if (!access.CanSee(ProfileSection.Certificates))
            return ServiceResult<IReadOnlyList<PublicCertificateCard>>.Fail("forbidden");

        // Order by the Certificate's own column before projecting — EF can't translate an
        // OrderBy over a property of the constructed PublicCertificateCard (it threw at runtime).
        var certs = await db.Certificates.AsNoTracking()
            .Where(c => c.UserId == user.Id && c.IsPublic && !c.IsRevoked)
            .Join(db.Events.AsNoTracking(), c => c.EventId, e => e.Id, (c, e) => new { Cert = c, e.Title })
            // Certificates are generated in one bulk pass per event (D-035), so a whole cohort shares a
            // CreatedAt tick — the worst case for an untied sort.
            .OrderByDescending(x => x.Cert.CreatedAt).ThenByDescending(x => x.Cert.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Cert.Id, x.Title, x.Cert.CreatedAt, x.Cert.VerifyCode, x.Cert.Kind })
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<PublicCertificateCard>>.Success(
            certs.Select(c => new PublicCertificateCard(c.Id, c.Title, c.CreatedAt, c.VerifyCode, IsAchievementKind(c.Kind))).ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<TimelineEntry>>> GetTimelineAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<IReadOnlyList<TimelineEntry>>.Fail("not_found");
        if (!access.CanSee(ProfileSection.Timeline))
            return ServiceResult<IReadOnlyList<TimelineEntry>>.Fail("forbidden");

        var now = DateTime.UtcNow;
        var take = page * pageSize;   // merge-pagination: pull the top `take` from every lane, merge, then slice
        var entries = new List<TimelineEntry>(take * 4);

        var roleNames = await db.ParticipantRoles.AsNoTracking()
            .Where(r => r.OrgId == null)
            .Select(r => new { r.Slug, r.Name })
            .ToDictionaryAsync(r => r.Slug, r => r.Name, ct);

        // Lanes: org joined / org verified — one pair of milestones per org, not per membership row.
        var memberships = await db.Memberships.AsNoTracking()
            .Where(m => m.UserId == user.Id && m.ShowOnProfile)
            .Join(db.Organizations.AsNoTracking(), m => m.OrgId, o => o.Id, (m, o) => new { m, OrgName = o.Name })
            .ToListAsync(ct);
        foreach (var g in memberships.GroupBy(x => x.m.OrgId))
        {
            var orgName = g.First().OrgName;
            entries.Add(new TimelineEntry("org_joined", $"Joined {orgName}", null, null,
                Array.Empty<string>(), orgName, null, g.Min(x => x.m.CreatedAt), null, false));

            var verifiedAts = g.Where(x => x.m.VerifiedAt.HasValue).Select(x => x.m.VerifiedAt!.Value).ToList();
            if (verifiedAts.Count > 0)
                entries.Add(new TimelineEntry("org_verified", $"{orgName} Verified", null, null,
                    Array.Empty<string>(), orgName, null, verifiedAts.Min(), null, false));
        }

        // Lane: participation, deduped per event with every role aggregated onto one entry (D-201 fix).
        var parts = await db.EventParticipants.AsNoTracking()
            .Where(p => p.SubjectType == ParticipantSubjectType.Person && p.SubjectId == user.Id
                && p.Visibility == ParticipantVisibility.Public
                && (p.State == ParticipantState.Accepted || p.State == ParticipantState.Active || p.State == ParticipantState.Completed))
            .Join(db.Events.AsNoTracking().Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed
                    && (e.Status == EventStatus.Published || e.Status == EventStatus.Closed)),
                p => p.EventId, e => e.Id, (p, e) => new { p, e })
            .Join(db.Organizations.AsNoTracking(), x => x.e.RepresentingOrgId, o => o.Id, (x, o) => new { x.p, x.e, OrgName = o.Name })
            .OrderByDescending(x => x.e.StartsAt)
            .Take(take)
            .ToListAsync(ct);
        foreach (var g in parts.GroupBy(x => x.e.Id))
        {
            var first = g.First();
            var roles = g.Select(x => x.p.CustomLabel
                    ?? (roleNames.TryGetValue(x.p.RoleSlug, out var rn) ? rn : Humanize(x.p.RoleSlug)))
                .Distinct().ToList();
            var occurredAt = g.Select(x => x.p.CompletedAt ?? x.p.AcceptedAt)
                .Where(d => d.HasValue).Select(d => d!.Value).DefaultIfEmpty(first.e.StartsAt).Max();
            entries.Add(new TimelineEntry("participation", first.e.Title, first.e.Slug, first.e.BannerKey,
                roles, first.OrgName, first.e.City, occurredAt, null, false));
        }

        // Lane: achievement — literally the same merge the Achievements section renders (D-203),
        // now the same call rather than a parallel one (D-229), so the two can never disagree.
        var timelineFacts = await facts.LoadAsync(user.Id, ct);
        foreach (var a in AchievementEngine.Build(timelineFacts, access, take))
            entries.Add(new TimelineEntry("achievement",
                a.Source == "certificate" ? $"Won {a.EventTitle}" : $"Earned {a.Name}",
                a.EventSlug, null, new[] { a.Name }, a.OrgName, null, a.EarnedAt, null, false));

        // Lane: certificate — non-achievement certs only, so a Participation cert doesn't read as a milestone.
        if (access.CanSee(ProfileSection.Certificates))
        {
            var plainCerts = await db.Certificates.AsNoTracking()
                .Where(c => c.UserId == user.Id && c.IsPublic && !c.IsRevoked && !ProfileCounts.AchievementKinds.Contains(c.Kind))
                .Join(db.Events.AsNoTracking(), c => c.EventId, e => e.Id, (c, e) => new { c, e })
                .Join(db.Organizations.AsNoTracking(), x => x.e.RepresentingOrgId, o => o.Id, (x, o) => new { x.c, x.e, OrgName = o.Name })
                .OrderByDescending(x => x.c.CreatedAt)
                .Take(take)
                .Select(x => new { x.c.Kind, x.c.CreatedAt, x.c.VerifyCode, x.e.Title, x.e.Slug, x.e.BannerKey, x.e.City, x.OrgName })
                .ToListAsync(ct);
            foreach (var c in plainCerts)
                entries.Add(new TimelineEntry("certificate", c.Title, c.Slug, c.BannerKey,
                    new[] { AchievementEngine.CertLabel(c.Kind) }, c.OrgName, c.City, c.CreatedAt, c.VerifyCode, false));
        }

        // Lane: competition_result (D-222) — a judged placement, kept distinct from the certificate
        // lane because a result is decided by judges and a certificate is issued by an organizer.
        if (access.CanSee(ProfileSection.Achievements))
        {
            foreach (var r in await CompetitionResultsAsync(user.Id, 0, take, ct))
                entries.Add(new TimelineEntry("competition_result",
                    $"{AchievementEngine.RankLabel(r.Rank)} · {r.EventTitle}", r.EventSlug, r.BannerKey,
                    new[] { r.StageName }, r.OrgName, null, r.OccurredAt, null, false));
        }

        // Lane: speaker_session (D-222) — one entry per talk actually given.
        if (access.CanSee(ProfileSection.Events))
        {
            foreach (var s in await SpeakerSessionsAsync(user.Id, 0, take, ct))
                entries.Add(new TimelineEntry("speaker_session", s.SessionTitle, s.EventSlug, s.BannerKey,
                    new[] { "Speaker" }, s.OrgName, null, s.StartsAt, null, false));

            // Lane: assignment_completed — service delivered, which the participation lane cannot show
            // (it records the role, never whether the duty was carried out).
            var completed = await db.EventAssignments.AsNoTracking()
                .Where(a => a.UserId == user.Id && a.ShowOnProfile && a.CompletedAt != null)
                .Join(db.Events.AsNoTracking().Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                    a => a.EventId, e => e.Id, (a, e) => new { a, e })
                .Join(db.Organizations.AsNoTracking(), x => x.e.RepresentingOrgId, o => o.Id,
                    (x, o) => new { x.a, x.e, OrgName = o.Name })
                .OrderByDescending(x => x.a.CompletedAt)
                .Take(take)
                .ToListAsync(ct);
            foreach (var x in completed)
            {
                var role = x.a.Role == "Custom" && !string.IsNullOrWhiteSpace(x.a.CustomRole)
                    ? x.a.CustomRole! : x.a.Role;
                entries.Add(new TimelineEntry("assignment_completed", x.e.Title, x.e.Slug, x.e.BannerKey,
                    new[] { role }, x.OrgName, x.e.City, x.a.CompletedAt!.Value, null, false));
            }
        }

        // Lane: organized.
        var orgIds = memberships.Select(x => x.m.OrgId).Distinct().ToList();
        var organized = await db.Events.AsNoTracking()
            .Where(e => (orgIds.Contains(e.RepresentingOrgId) || e.CreatedBy == user.Id)
                && (e.Status == EventStatus.Published || e.Status == EventStatus.Closed)
                && e.EndsAt < now && e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed)
            .Join(db.Organizations.AsNoTracking(), e => e.RepresentingOrgId, o => o.Id, (e, o) => new { e, OrgName = o.Name })
            .OrderByDescending(x => x.e.StartsAt)
            .Take(take)
            .ToListAsync(ct);
        foreach (var e in organized)
            entries.Add(new TimelineEntry("organized", e.e.Title, e.e.Slug, e.e.BannerKey,
                new[] { "Organizer" }, e.OrgName, e.e.City, e.e.StartsAt, null, false));

        // Lane: attended (check-in verified), opt-in, deduped — a person may hold >1 ticket for one event.
        if (access.CanSee(ProfileSection.Attended))
        {
            var attended = await db.Tickets.AsNoTracking()
                .Where(t => t.UserId == user.Id && t.State == TicketState.CheckedIn)
                .Join(db.Events.AsNoTracking().Where(e => e.EndsAt < now && e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                    t => t.EventId, e => e.Id, (t, e) => new { t.CheckedInAt, e })
                .Join(db.Organizations.AsNoTracking(), x => x.e.RepresentingOrgId, o => o.Id, (x, o) => new { x.CheckedInAt, x.e, OrgName = o.Name })
                .OrderByDescending(x => x.e.StartsAt)
                .Take(take)
                .ToListAsync(ct);
            foreach (var g in attended.GroupBy(x => x.e.Id))
            {
                var first = g.First();
                var checkedInAt = g.Select(x => x.CheckedInAt).Where(d => d.HasValue).Select(d => d!.Value)
                    .DefaultIfEmpty(first.e.StartsAt).Min();
                entries.Add(new TimelineEntry("attended", first.e.Title, first.e.Slug, first.e.BannerKey,
                    new[] { "Attended" }, first.OrgName, first.e.City, checkedInAt, null, false));
            }
        }

        if (entries.Count > 0)
        {
            var earliestIndex = 0;
            for (var i = 1; i < entries.Count; i++)
                if (entries[i].OccurredAt < entries[earliestIndex].OccurredAt) earliestIndex = i;
            entries[earliestIndex] = entries[earliestIndex] with { IsFirstEvent = true };
        }

        // ponytail: in-memory merge of the lanes — fine at profile scale; move to a SQL UNION-ALL
        // window if a single user ever accumulates tens of thousands of timeline facts.
        // Sorted in memory over facts merged from several queries, so "stable sort preserves input order"
        // is only as deterministic as that input — and each contributing query orders on a non-unique key
        // of its own. Slug (unique per event) then Title makes the final order total on its own terms,
        // independent of the order the sources happened to append in (DB-6).
        var paged = entries
            .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Slug).ThenBy(x => x.Title)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToList();

        return ServiceResult<IReadOnlyList<TimelineEntry>>.Success(paged);
    }

    // ── Achievements (D-203) — source-agnostic merge, reused by the profile section AND the timeline ──

    // ── Verified sources wired in D-222 ──────────────────────────────────────
    // Each was fully built, tested and reachable over HTTP; none reached the profile. These are reads
    // over existing rows — no schema, no backfill, no new write path.

    public async Task<ServiceResult<IReadOnlyList<CompetitionResultCard>>> GetCompetitionResultsAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<IReadOnlyList<CompetitionResultCard>>.Fail("not_found");
        if (!access.CanSee(ProfileSection.Achievements))
            return ServiceResult<IReadOnlyList<CompetitionResultCard>>.Fail("forbidden");

        return ServiceResult<IReadOnlyList<CompetitionResultCard>>.Success(
            await CompetitionResultsAsync(user.Id, (page - 1) * pageSize, pageSize, ct));
    }

    /// <summary>Only <c>Published</c> results, and only on non-private events. A Provisional result is
    /// not yet a fact about the person, and a Disputed one is explicitly contested — surfacing either
    /// as an achievement would overclaim. Subject is the person; team results (SubjectType=Team) are
    /// not attributed to individuals here because the team roster, not the result row, decides who
    /// shares the credit.</summary>
    private async Task<List<CompetitionResultCard>> CompetitionResultsAsync(
        Guid userId, int skip, int take, CancellationToken ct)
    {
        var rows = await db.StageResults.AsNoTracking()
            .Where(r => r.SubjectType == CompetitionSubjectType.Person && r.SubjectId == userId
                && r.State == ResultState.Published)
            .Join(db.Stages.AsNoTracking(), r => r.StageId, s => s.Id, (r, s) => new { r, s })
            .Join(db.Events.AsNoTracking().Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                x => x.s.EventId, e => e.Id, (x, e) => new { x.r, x.s, e })
            // Carried, not filtered on: a self-represented event must still show the result, it just has
            // no organization to name (D-268). Joining `!IsPersonal` would be an inner join and would
            // delete the whole card.
            .Join(db.Organizations.AsNoTracking(), x => x.e.RepresentingOrgId, o => o.Id,
                (x, o) => new { x.r, x.s, x.e, OrgName = o.Name, o.IsPersonal })
            // Rank ties by design (joint placements) and PublishedAt ties because a stage's results are
            // published in one action, so both existing keys can tie together. The result id settles it.
            .OrderBy(x => x.r.Rank).ThenByDescending(x => x.r.PublishedAt).ThenByDescending(x => x.r.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return rows.Select(x => new CompetitionResultCard(
            x.e.Id, x.e.Title, x.e.Slug, x.e.BannerKey, x.s.Name, x.r.Rank, x.r.FinalScore,
            x.r.PublishedAt ?? x.r.CreatedAt, x.IsPersonal ? null : x.OrgName)).ToList();
    }

    public async Task<ServiceResult<IReadOnlyList<AssignmentCard>>> GetAssignmentsAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<IReadOnlyList<AssignmentCard>>.Fail("not_found");
        if (!access.CanSee(ProfileSection.Events))
            return ServiceResult<IReadOnlyList<AssignmentCard>>.Fail("forbidden");

        // ShowOnProfile is the assignment's own per-row opt-out and composes with the section tier as
        // AND — a section-level "public" never overrides a row the user chose to hide.
        var rows = await db.EventAssignments.AsNoTracking()
            .Where(a => a.UserId == user.Id && a.ShowOnProfile
                && (a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Completed))
            .Join(db.Events.AsNoTracking().Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                a => a.EventId, e => e.Id, (a, e) => new { a, e })
            .Join(db.Organizations.AsNoTracking(), x => x.e.RepresentingOrgId, o => o.Id,
                (x, o) => new { x.a, x.e, OrgName = o.Name, o.IsPersonal })
            // One person can hold several assignments at one event (Judge and Mentor), so the event's
            // StartsAt repeats within this list by design; the assignment row settles the order.
            .OrderByDescending(x => x.e.StartsAt).ThenByDescending(x => x.a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<AssignmentCard>>.Success(rows.Select(x => new AssignmentCard(
            x.e.Id, x.e.Title, x.e.Slug, x.e.BannerKey,
            // Custom is a real stored value, not a fallback — render the organizer's own label.
            x.a.Role == "Custom" && !string.IsNullOrWhiteSpace(x.a.CustomRole) ? x.a.CustomRole! : x.a.Role,
            x.a.Status.ToString(), x.a.CompletedAt, x.e.StartsAt,
            x.IsPersonal ? null : x.OrgName)).ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<SpeakerSessionCard>>> GetSpeakerSessionsAsync(
        string username, int page, int pageSize, Guid? viewerId = null, CancellationToken ct = default)
    {
        if (await LoadAsync(username, viewerId, ct) is not var (user, access) || user is null)
            return ServiceResult<IReadOnlyList<SpeakerSessionCard>>.Fail("not_found");
        if (!access.CanSee(ProfileSection.Events))
            return ServiceResult<IReadOnlyList<SpeakerSessionCard>>.Fail("forbidden");

        return ServiceResult<IReadOnlyList<SpeakerSessionCard>>.Success(
            await SpeakerSessionsAsync(user.Id, (page - 1) * pageSize, pageSize, ct));
    }

    /// <summary>Sessions whose speaker record an organizer linked to this account (D-208). Only
    /// <c>Session</c> items — a Break is scheduling furniture, not a talk anyone gave.</summary>
    private async Task<List<SpeakerSessionCard>> SpeakerSessionsAsync(
        Guid userId, int skip, int take, CancellationToken ct)
    {
        var rows = await db.Speakers.AsNoTracking()
            .Where(s => s.UserId == userId && s.DeletedAt == null)
            .Join(db.EventSessionSpeakers.AsNoTracking(), s => s.Id, ss => ss.SpeakerId, (s, ss) => ss)
            .Join(db.EventSessions.AsNoTracking().Where(x => x.Kind == ScheduleItemKind.Session),
                ss => ss.SessionId, sess => sess.Id, (ss, sess) => sess)
            .Join(db.Events.AsNoTracking().Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                sess => sess.EventId, e => e.Id, (sess, e) => new { sess, e })
            .Join(db.Organizations.AsNoTracking(), x => x.e.RepresentingOrgId, o => o.Id,
                (x, o) => new { x.sess, x.e, OrgName = o.Name, o.IsPersonal })
            // Parallel tracks start at the same minute constantly — a conference's 10:00 slot is several
            // sessions, and a speaker on two of them ties outright.
            .OrderByDescending(x => x.sess.StartsAt).ThenByDescending(x => x.sess.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return rows.Select(x => new SpeakerSessionCard(
            x.e.Id, x.e.Title, x.e.Slug, x.e.BannerKey,
            x.sess.Title, x.sess.StartsAt, x.sess.EndsAt,
            x.IsPersonal ? null : x.OrgName)).ToList();
    }

    // The achievement-kind set lives in ProfileCounts (D-229) — it was duplicated here, which is the
    // same second-source-of-truth pattern this refactor exists to remove.
    private static bool IsAchievementKind(CertificateKind kind) => ProfileCounts.AchievementKinds.Contains(kind);

    private async Task<Dictionary<Guid, (string? VerifyCode, bool IsAchievement)>> CertLookupAsync(
        Guid userId, List<Guid> eventIds, CancellationToken ct)
    {
        if (eventIds.Count == 0) return new();
        var rows = await db.Certificates.AsNoTracking()
            .Where(c => c.UserId == userId && c.IsPublic && !c.IsRevoked && eventIds.Contains(c.EventId))
            .Select(c => new { c.EventId, c.VerifyCode, c.Kind })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.EventId).ToDictionary(g => g.Key, g =>
        {
            var best = g.OrderByDescending(r => IsAchievementKind(r.Kind)).First();
            return ((string?)best.VerifyCode, IsAchievementKind(best.Kind));
        });
    }

    // Identity labels moved to IdentityEngine in D-225, where they are derived alongside the headline
    // from one pool — the two used to be one function and a plan for a second, which would have been
    // two derivations of the same thing that could disagree.

    // ── Organizations (§2) ───────────────────────────────────────────────────

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CertLabel(CertificateKind kind) => kind switch
    {
        CertificateKind.Participation => "Participant",
        CertificateKind.RunnerUp => "Runner Up",
        _ => kind.ToString(),
    };

    private static string Humanize(string slug) => string.Join(' ',
        slug.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    /// <summary>Deterministic journey summary from verified counts (no LLM). "AI-generated insights"
    /// can later replace this with a real model; the shape stays the same.</summary>
    private static string BuildSummary(string name, ProfileMetrics m, int orgCount)
    {
        // Prose form of the same canonical counts the stats row shows (D-229). A hidden count is
        // simply omitted from the sentence rather than stated as zero — the summary must never say
        // "attended 0 events" because the viewer is not entitled to the number.
        var parts = new List<string>();
        if (m.EventsOrganized > 0) parts.Add($"organized {m.EventsOrganized} event{Plural(m.EventsOrganized!.Value)}");
        if (m.EventsParticipated > 0) parts.Add($"took part in {m.EventsParticipated} event{Plural(m.EventsParticipated!.Value)}");
        if (m.EventsAttended > 0) parts.Add($"attended {m.EventsAttended} event{Plural(m.EventsAttended!.Value)}");
        if (m.Certificates > 0) parts.Add($"earned {m.Certificates} verified certificate{Plural(m.Certificates!.Value)}");
        if (m.VerifiedOrganizations > 0 && orgCount > 0) parts.Add($"is a verified member of {orgCount} organization{Plural(orgCount)}");
        if (m.AllyCount > 0) parts.Add($"has {m.AllyCount} Kurx {(m.AllyCount == 1 ? "ally" : "allies")}");

        if (parts.Count == 0)
            return $"{name} is building their verified event identity on Kurx.";

        var top = m.EventDna.Count > 0 ? $" Most active in {m.EventDna[0].Kind}." : "";
        return $"{name} has {JoinNaturally(parts)}.{top}";
    }

    private static string Plural(int n) => n == 1 ? "" : "s";

    private static string JoinNaturally(List<string> parts) => parts.Count switch
    {
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => $"{string.Join(", ", parts.Take(parts.Count - 1))}, and {parts[^1]}",
    };

    private static CollegeAffiliation? ParseCollege(string? educationJson)
    {
        if (string.IsNullOrWhiteSpace(educationJson)) return null;
        try
        {
            var entries = JsonSerializer.Deserialize<JsonElement[]>(educationJson);
            if (entries is null || entries.Length == 0) return null;

            JsonElement? current = null;
            int maxYear = -1;
            JsonElement? latest = null;
            foreach (var e in entries)
            {
                if (e.TryGetProperty("end_year", out var endProp) && endProp.ValueKind == JsonValueKind.Null)
                { current = e; break; }
                if (e.TryGetProperty("end_year", out var yr) && yr.ValueKind == JsonValueKind.Number)
                {
                    var y = yr.GetInt32();
                    if (y > maxYear) { maxYear = y; latest = e; }
                }
            }
            var chosen = current ?? latest;
            if (chosen is null) return null;
            var institute = chosen.Value.TryGetProperty("institute", out var inst) ? inst.GetString() : null;
            if (string.IsNullOrWhiteSpace(institute)) return null;
            var degree = chosen.Value.TryGetProperty("degree", out var deg) ? deg.GetString() : null;
            var branch = chosen.Value.TryGetProperty("branch", out var br) ? br.GetString() : null;
            return new CollegeAffiliation(institute, degree, branch);
        }
        catch { return null; }
    }
}
