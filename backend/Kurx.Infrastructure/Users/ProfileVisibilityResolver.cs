using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Users;

/// <summary>The one place profile visibility is decided (D-221). Before this, the same question was
/// answered in nine scattered places across <see cref="PublicProfileService"/> and
/// <see cref="AllyService"/>, each re-deriving the rule from raw booleans; all nine now call here.
///
/// <para>Four tiers per section: Public, Connections (an accepted <c>AllyConnection</c>),
/// EventParticipants (at least one shared public event), OnlyMe. The owner always sees everything.</para>
///
/// <para>Cost: the two relationship checks run <b>at most once each per request</b> and only when some
/// section actually needs them — a profile whose sections are all Public issues zero extra queries,
/// which is what keeps this off the hot path.</para></summary>
public class ProfileVisibilityResolver(KurxDbContext db) : IProfileVisibilityResolver
{
    /// <summary>Every section's visibility when the user has expressed no preference. Sections that
    /// predate D-221 defer to their legacy boolean first (see <see cref="LegacyBooleanFor"/>); the rest
    /// default Public, which is exactly how they behaved before this column existed.</summary>
    private static readonly IReadOnlyDictionary<ProfileSection, SectionVisibility> Defaults =
        new Dictionary<ProfileSection, SectionVisibility>
        {
            [ProfileSection.Profile] = SectionVisibility.Public,
            [ProfileSection.Attended] = SectionVisibility.OnlyMe,      // ShowAttended defaulted false
            [ProfileSection.Certificates] = SectionVisibility.Public,
            [ProfileSection.Network] = SectionVisibility.Public,
            [ProfileSection.Events] = SectionVisibility.Public,
            [ProfileSection.Organizations] = SectionVisibility.Public,
            [ProfileSection.Achievements] = SectionVisibility.Public,
            [ProfileSection.Timeline] = SectionVisibility.Public,
            [ProfileSection.Metrics] = SectionVisibility.Public,
            [ProfileSection.Contributions] = SectionVisibility.Public,
        };

    /// <summary>The pre-D-221 boolean that governs a section, if any. Reading these keeps the two
    /// systems in agreement during the dual-write release and makes a rollback lossless.</summary>
    private static SectionVisibility? LegacyBooleanFor(User user, ProfileSection section) => section switch
    {
        ProfileSection.Profile => user.ProfilePublic ? SectionVisibility.Public : SectionVisibility.OnlyMe,
        ProfileSection.Attended => user.ShowAttended ? SectionVisibility.Public : SectionVisibility.OnlyMe,
        ProfileSection.Certificates => user.ShowCertificates ? SectionVisibility.Public : SectionVisibility.OnlyMe,
        ProfileSection.Network => user.ShowAllies ? SectionVisibility.Public : SectionVisibility.OnlyMe,
        // Sections added after D-221 have no legacy boolean; they fall through to Defaults.
        _ => null,
    };

    public async Task<SectionAccess> ResolveAsync(Guid ownerId, Guid? viewerId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ownerId, ct);

        // An unknown owner is resolved as "nothing visible" rather than throwing: callers turn an
        // invisible Profile section into the same 404 a missing user produces (D-018), so a deleted
        // user and a hidden one stay indistinguishable.
        if (user is null) return SectionAccess.Create(AllHidden(), isOwner: false);

        return await ResolveForUserAsync(user, viewerId, ct);
    }

    internal async Task<SectionAccess> ResolveForUserAsync(User user, Guid? viewerId, CancellationToken ct)
    {
        var isOwner = viewerId is Guid v && v == user.Id;
        if (isOwner)
            return SectionAccess.Create(
                Enum.GetValues<ProfileSection>().ToDictionary(s => s, _ => true), isOwner: true);

        var settings = EffectiveSettings(user);

        // Lazily evaluated, once each — a profile with no Connections/EventParticipants section never
        // pays for them.
        bool? isConnection = null;
        bool? isCoParticipant = null;

        var visible = new Dictionary<ProfileSection, bool>();
        foreach (var section in Enum.GetValues<ProfileSection>())
        {
            var tier = settings.TryGetValue(section, out var t) ? t : SectionVisibility.OnlyMe;
            visible[section] = tier switch
            {
                SectionVisibility.Public => true,
                SectionVisibility.OnlyMe => false,
                SectionVisibility.Connections =>
                    isConnection ??= viewerId is Guid c && await IsAcceptedConnectionAsync(user.Id, c, ct),
                SectionVisibility.EventParticipants =>
                    isCoParticipant ??= viewerId is Guid p && await SharesAPublicEventAsync(user.Id, p, ct),
                _ => false,   // unknown tier fails closed
            };
        }

        return SectionAccess.Create(visible, isOwner: false);
    }

    public async Task<IReadOnlySet<Guid>> VisibleProfileIdsAsync(
        IReadOnlyCollection<Guid> userIds, Guid? viewerId, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new HashSet<Guid>();
        var ids = userIds.Distinct().ToList();

        // One query for the batch. Moderated accounts are excluded here rather than at each call site:
        // "may this person be shown" has exactly one answer, and eight services previously each
        // remembered — or forgot — to ask it separately (D-230/BUG-B).
        var users = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.BannedAt == null && u.SuspendedAt == null)
            .ToListAsync(ct);
        if (users.Count == 0) return new HashSet<Guid>();

        var tiers = users.ToDictionary(u => u.Id, u => EffectiveSettings(u)[ProfileSection.Profile]);

        // The relationship queries run at most once each for the whole batch, and only when some user
        // in it actually uses that tier. Everyone Public ⇒ the loop below needs neither.
        HashSet<Guid>? connections = null;
        HashSet<Guid>? coParticipants = null;
        if (viewerId is Guid viewer)
        {
            if (tiers.Values.Any(t => t == SectionVisibility.Connections))
                connections = await AcceptedConnectionsAmongAsync(ids, viewer, ct);
            if (tiers.Values.Any(t => t == SectionVisibility.EventParticipants))
                coParticipants = await CoParticipantsAmongAsync(ids, viewer, ct);
        }

        var visible = new HashSet<Guid>();
        foreach (var (id, tier) in tiers)
        {
            var ok = tier switch
            {
                SectionVisibility.Public => true,
                SectionVisibility.OnlyMe => viewerId == id,
                SectionVisibility.Connections => viewerId == id || (connections?.Contains(id) ?? false),
                SectionVisibility.EventParticipants => viewerId == id || (coParticipants?.Contains(id) ?? false),
                _ => false,   // unknown tier fails closed, exactly as ResolveForUserAsync does
            };
            if (ok) visible.Add(id);
        }
        return visible;
    }

    /// <summary>Which of <paramref name="ownerIds"/> the viewer has an accepted connection with — one
    /// query for the whole set, versus one <see cref="IsAcceptedConnectionAsync"/> call per id.</summary>
    private async Task<HashSet<Guid>> AcceptedConnectionsAmongAsync(
        List<Guid> ownerIds, Guid viewerId, CancellationToken ct)
    {
        var rows = await db.AllyConnections.AsNoTracking()
            .Where(a => a.Status == AllyStatus.Accepted
                && ((a.UserLowId == viewerId && ownerIds.Contains(a.UserHighId))
                    || (a.UserHighId == viewerId && ownerIds.Contains(a.UserLowId))))
            .Select(a => a.UserLowId == viewerId ? a.UserHighId : a.UserLowId)
            .ToListAsync(ct);
        return rows.ToHashSet();
    }

    /// <summary>Which of <paramref name="ownerIds"/> share a public event with the viewer. The viewer's
    /// own event set is fetched once and then intersected server-side, so this is a fixed number of
    /// queries no matter how many ids are in the batch — the same definition
    /// <see cref="SharesAPublicEventAsync"/> uses, not a second one.</summary>
    private async Task<HashSet<Guid>> CoParticipantsAmongAsync(
        List<Guid> ownerIds, Guid viewerId, CancellationToken ct)
    {
        var viewerEvents = (await PublicEventIdsAsync(viewerId, ct)).ToList();
        if (viewerEvents.Count == 0) return [];

        // viewerEvents is already restricted to non-private events, so the owner side does not re-join
        // Events — the intersection carries that filter.
        var byParticipation = await db.EventParticipants.AsNoTracking()
            .Where(p => p.SubjectType == ParticipantSubjectType.Person && ownerIds.Contains(p.SubjectId)
                && viewerEvents.Contains(p.EventId)
                && p.Visibility == ParticipantVisibility.Public
                && (p.State == ParticipantState.Accepted || p.State == ParticipantState.Active
                    || p.State == ParticipantState.Completed))
            .Select(p => p.SubjectId)
            .Distinct().ToListAsync(ct);

        var byTicket = await db.Tickets.AsNoTracking()
            .Where(t => t.UserId != null && ownerIds.Contains(t.UserId.Value)
                && viewerEvents.Contains(t.EventId) && t.State == TicketState.CheckedIn)
            .Select(t => t.UserId!.Value)
            .Distinct().ToListAsync(ct);

        return byParticipation.Concat(byTicket).ToHashSet();
    }

    public async Task<IReadOnlyDictionary<ProfileSection, SectionVisibility>> GetSettingsAsync(
        Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? Defaults : EffectiveSettings(user);
    }

    /// <summary>Partial update of the caller's section settings.
    ///
    /// <para><b>Serialised with a row lock (D-231).</b> This is a read-modify-write over one jsonb
    /// column: read the effective map, merge the requested sections, write the whole map back. Two
    /// concurrent calls both read the pre-existing map, each merges only its own section, and the
    /// slower write wins — silently discarding the other change.</para>
    ///
    /// <para>That is not theoretical. The Flutter privacy screen saves <b>per toggle, immediately</b>
    /// (deliberately — a privacy control that stays unsaved is a bad failure), so flipping two
    /// switches in quick succession fires two overlapping requests. The user watches a toggle move and
    /// the setting silently reverts. On a privacy control that is the worst class of failure: they
    /// believe something is hidden when it is not.</para>
    ///
    /// <para><b>Fixed (D-231) by making the write a single atomic statement</b> — the merge happens
    /// inside Postgres, against the current row, so there is no read-modify-write window at all.</para>
    ///
    /// <para><b>A `SELECT … FOR UPDATE` row lock was tried first and reverted. Do not reach for it
    /// again.</b> It made things strictly worse: the suite went from ~7 minutes to <b>5h42m</b>. Postgres
    /// applies no default lock timeout, so one transaction that takes the lock and does not commit
    /// blocks <i>every</i> later writer indefinitely — turning a lost update into a hang, which on a
    /// privacy endpoint is the worse failure. The atomic statement below needs no explicit lock: the
    /// row-level write lock is taken and released within the statement, never across an await.</para></summary>
    public async Task<IReadOnlyDictionary<ProfileSection, SectionVisibility>> UpdateSettingsAsync(
        Guid userId, IReadOnlyDictionary<ProfileSection, SectionVisibility> changes, CancellationToken ct = default)
    {
        // The patch carries ONLY the sections this request is changing. Sending the whole merged map —
        // which the read-modify-write version did — is what made concurrent writes destructive: each
        // request rewrote all ten sections from a snapshot taken before the other request landed.
        var patch = JsonSerializer.Serialize(
            changes.ToDictionary(kv => SectionKey(kv.Key), kv => TierKey(kv.Value)));

        // One statement. Postgres takes the row-level write lock, merges, and releases it before
        // returning — there is no lock held across an await and no window in which a dying request
        // can strand it. `||` on jsonb is a shallow right-wins merge, which is exactly patch
        // semantics, and it reads the current value inside the same statement so a concurrent write
        // cannot be overwritten from a stale snapshot.
        //
        // The legacy booleans (D-221 dual-write) are recomputed in the same statement, and only for
        // sections the patch actually mentions — `ELSE <current>` leaves the rest untouched, because
        // an absent key means "unchanged", not "reset to default". A tier the boolean cannot express
        // (Connections / EventParticipants) is strictly more private than Public and maps to false, so
        // a rollback to the pre-D-221 read path over-hides rather than over-shares.
        //
        // `jsonb_build_object()` is the empty object. It is spelled that way rather than as an empty
        // JSON literal because this is an interpolated string: a brace in here opens an interpolation
        // hole, not a JSON object.
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH patch AS (SELECT {patch}::jsonb AS j)
            UPDATE users u
            SET "SectionVisibilityJson" = COALESCE(u."SectionVisibilityJson", jsonb_build_object()) || patch.j,
                "ProfilePublic" = CASE WHEN jsonb_exists(patch.j, 'profile')
                    THEN patch.j->>'profile' = 'public' ELSE u."ProfilePublic" END,
                "ShowAttended" = CASE WHEN jsonb_exists(patch.j, 'attended')
                    THEN patch.j->>'attended' = 'public' ELSE u."ShowAttended" END,
                "ShowCertificates" = CASE WHEN jsonb_exists(patch.j, 'certificates')
                    THEN patch.j->>'certificates' = 'public' ELSE u."ShowCertificates" END,
                "ShowAllies" = CASE WHEN jsonb_exists(patch.j, 'network')
                    THEN patch.j->>'network' = 'public' ELSE u."ShowAllies" END
            FROM patch
            WHERE u."Id" = {userId}
            """, ct);

        if (affected == 0) throw new InvalidOperationException("user_not_found");

        // Re-read to build the response. This deliberately reflects the *true current* state rather
        // than what this request believes it set — if a concurrent write landed between the update and
        // this read, the caller should see that, not a private fiction of its own change.
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        return EffectiveSettings(user);
    }

    /// <summary>Stored override → legacy boolean → documented default, in that order.</summary>
    private static IReadOnlyDictionary<ProfileSection, SectionVisibility> EffectiveSettings(User user)
    {
        var stored = ParseStored(user.SectionVisibilityJson);
        var result = new Dictionary<ProfileSection, SectionVisibility>();
        foreach (var section in Enum.GetValues<ProfileSection>())
        {
            result[section] = stored.TryGetValue(section, out var explicitTier) ? explicitTier
                : LegacyBooleanFor(user, section) ?? Defaults[section];
        }
        return result;
    }

    /// <summary>A malformed or hand-edited value yields no overrides rather than throwing the profile
    /// away — the fallback chain then produces exactly the pre-D-221 answer.</summary>
    private static Dictionary<ProfileSection, SectionVisibility> ParseStored(string? json)
    {
        var result = new Dictionary<ProfileSection, SectionVisibility>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (raw is null) return result;
            foreach (var (key, value) in raw)
            {
                if (Enum.TryParse<ProfileSection>(key.Replace("_", ""), ignoreCase: true, out var section)
                    && Enum.TryParse<SectionVisibility>(value.Replace("_", ""), ignoreCase: true, out var tier))
                {
                    result[section] = tier;
                }
            }
        }
        catch (JsonException)
        {
            // fall through to an empty override set
        }
        return result;
    }

    public static string SectionKey(ProfileSection section) => section switch
    {
        ProfileSection.Profile => "profile",
        ProfileSection.Attended => "attended",
        ProfileSection.Certificates => "certificates",
        ProfileSection.Network => "network",
        ProfileSection.Events => "events",
        ProfileSection.Organizations => "organizations",
        ProfileSection.Achievements => "achievements",
        ProfileSection.Timeline => "timeline",
        ProfileSection.Metrics => "metrics",
        ProfileSection.Contributions => "contributions",
        _ => section.ToString().ToLowerInvariant(),
    };

    public static string TierKey(SectionVisibility tier) => tier switch
    {
        SectionVisibility.Public => "public",
        SectionVisibility.Connections => "connections",
        SectionVisibility.EventParticipants => "event_participants",
        SectionVisibility.OnlyMe => "only_me",
        _ => tier.ToString().ToLowerInvariant(),
    };

    public static bool TryParseSection(string key, out ProfileSection section) =>
        Enum.TryParse(key.Replace("_", ""), ignoreCase: true, out section);

    public static bool TryParseTier(string key, out SectionVisibility tier) =>
        Enum.TryParse(key.Replace("_", ""), ignoreCase: true, out tier);

    private static Dictionary<ProfileSection, bool> AllHidden() =>
        Enum.GetValues<ProfileSection>().ToDictionary(s => s, _ => false);

    private Task<bool> IsAcceptedConnectionAsync(Guid ownerId, Guid viewerId, CancellationToken ct)
    {
        var (low, high) = ownerId.CompareTo(viewerId) < 0 ? (ownerId, viewerId) : (viewerId, ownerId);
        return db.AllyConnections.AsNoTracking()
            .AnyAsync(a => a.UserLowId == low && a.UserHighId == high && a.Status == AllyStatus.Accepted, ct);
    }

    /// <summary>Shared *public* event, by the same definition the ally suggestion/mutual code already
    /// uses: a public participation row, or a checked-in ticket, on a non-private event.</summary>
    private async Task<bool> SharesAPublicEventAsync(Guid ownerId, Guid viewerId, CancellationToken ct)
    {
        var ownerEvents = await PublicEventIdsAsync(ownerId, ct);
        if (ownerEvents.Count == 0) return false;
        var viewerEvents = await PublicEventIdsAsync(viewerId, ct);
        return ownerEvents.Overlaps(viewerEvents);
    }

    private async Task<HashSet<Guid>> PublicEventIdsAsync(Guid userId, CancellationToken ct)
    {
        var participated = await db.EventParticipants.AsNoTracking()
            .Where(p => p.SubjectType == ParticipantSubjectType.Person && p.SubjectId == userId
                && p.Visibility == ParticipantVisibility.Public
                && (p.State == ParticipantState.Accepted || p.State == ParticipantState.Active
                    || p.State == ParticipantState.Completed))
            .Join(db.Events.AsNoTracking().Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                p => p.EventId, e => e.Id, (p, e) => e.Id)
            .ToListAsync(ct);

        var attended = await db.Tickets.AsNoTracking()
            .Where(t => t.UserId == userId && t.State == TicketState.CheckedIn)
            .Join(db.Events.AsNoTracking().Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                t => t.EventId, e => e.Id, (t, e) => e.Id)
            .ToListAsync(ct);

        return participated.Concat(attended).ToHashSet();
    }
}
