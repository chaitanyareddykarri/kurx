using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Users;

/// <summary>Ally connections (D-201) — see <c>docs/architecture/PROFESSIONAL_IDENTITY_SPEC.md</c> §5
/// for the full state-machine table. One row per unordered user pair, ever; re-requesting after
/// Decline/Revoke reactivates the same row instead of accumulating history.</summary>
public class AllyService(
    KurxDbContext db, INotificationService notify, IProfileFactSetLoader facts,
    IProfileVisibilityResolver visibility, IStorage storage) : IAllyService
{
    private const int MaxPendingOutgoing = 200;

    /// <summary>How long a declined requester must wait before asking the same person again (BUG-C).
    /// Long enough that repeated declines cannot be used to spam, short enough that a genuine reconnect
    /// is only delayed, not foreclosed. A future block feature is orthogonal to this and permanent.</summary>
    private static readonly TimeSpan DeclineCooldown = TimeSpan.FromDays(30);

    public async Task<ServiceResult<AllyConnectionView>> RequestAsync(Guid requesterId, Guid targetUserId, CancellationToken ct = default)
    {
        if (requesterId == targetUserId)
            return ServiceResult<AllyConnectionView>.Fail("cannot_ally_self");
        if (!await db.Users.AnyAsync(u => u.Id == targetUserId, ct))
            return ServiceResult<AllyConnectionView>.Fail("not_found");

        var (low, high) = Canonical(requesterId, targetUserId);
        var row = await db.AllyConnections.FirstOrDefaultAsync(a => a.UserLowId == low && a.UserHighId == high, ct);
        var notifyKind = (string?)null;
        var notifyTarget = Guid.Empty;

        // The cap applies to every path that CREATES a pending outgoing request, not just a brand-new
        // row: reactivating a Declined/Revoked pair produces exactly the same obligation on the target,
        // so gating only the insert let a user who had been declined by N people re-ask all of them and
        // walk past the limit. Statuses that don't create a request (already Pending, already Accepted)
        // are exempt — they are idempotent no-ops, and rejecting them would break retry.
        // BUG-C: decline is reversible and re-request notifies the target again, and the pending cap
        // does not apply to a declined row — so a requester could generate unlimited request
        // notifications by re-asking after every decline. The cooldown applies ONLY to the same person
        // being declined again: if the DECLINER later changes their mind and asks, that is a legitimate
        // reconnect, not spam, and is never blocked. Reuses Status+RespondedAt, so no schema change.
        if (row is { Status: AllyStatus.Declined } declined
            && declined.RequesterId == requesterId
            && declined.RespondedAt is { } declinedAt
            && DateTimeOffset.UtcNow - declinedAt < DeclineCooldown)
        {
            return ServiceResult<AllyConnectionView>.Fail("declined_recently");
        }

        var createsPendingRequest = row is null || row.Status is AllyStatus.Declined or AllyStatus.Revoked;
        if (createsPendingRequest)
        {
            var pending = await db.AllyConnections.CountAsync(
                a => a.RequesterId == requesterId && a.Status == AllyStatus.Pending, ct);
            if (pending >= MaxPendingOutgoing)
                return ServiceResult<AllyConnectionView>.Fail("too_many_pending_requests");
        }

        if (row is null)
        {
            row = new AllyConnection { UserLowId = low, UserHighId = high, RequesterId = requesterId, AddresseeId = targetUserId };
            db.AllyConnections.Add(row);
            (notifyKind, notifyTarget) = (NotificationKinds.AllyRequested, targetUserId);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true)
            {
                // Lost the insert race: the other party asked for the same pair between our read above
                // and this INSERT, so `ix_ally_connections_pair` rejected ours. Both callers reaching
                // here saw no row, so without this the loser gets a 500 for what is really the crossed
                // request the sequential path already handles. Re-read and apply the same merge — the
                // result is identical to A-then-B ordering: a mutual_request auto-accept.
                db.ChangeTracker.Clear();
                row = await db.AllyConnections.FirstAsync(a => a.UserLowId == low && a.UserHighId == high, ct);
                (notifyKind, notifyTarget) = await MergeIntoExistingAsync(row, requesterId, targetUserId, ct);
                await db.SaveChangesAsync(ct);
            }
        }
        else
        {
            (notifyKind, notifyTarget) = await MergeIntoExistingAsync(row, requesterId, targetUserId, ct);
            await db.SaveChangesAsync(ct);
        }
        // In every branch that sets notifyKind, the actor (whoever should be named in the notification
        // copy) is the caller of this method — the other party in notifyTarget is always who receives it.
        if (notifyKind is not null) await NotifyAllyEventAsync(notifyKind, notifyTarget, requesterId, row.Id, ct);
        return ServiceResult<AllyConnectionView>.Success(await ToViewAsync(row, requesterId, ct));
    }

    /// <summary>Applies a new request onto the pair's existing row. Shared by the ordinary path (a row was
    /// already there when we read) and the insert-race path (the other party created it in between), so a
    /// simultaneous crossed request resolves exactly as a sequential one does.</summary>
    private async Task<(string?, Guid)> MergeIntoExistingAsync(
        AllyConnection row, Guid requesterId, Guid targetUserId, CancellationToken ct)
    {
        (string?, Guid) outcome = (null, Guid.Empty);
        switch (row.Status)
        {
            case AllyStatus.Declined or AllyStatus.Revoked:
                row.Status = AllyStatus.Pending;
                row.RequesterId = requesterId;
                row.AddresseeId = targetUserId;
                row.RequestedAt = DateTimeOffset.UtcNow;
                row.RespondedAt = null;
                row.ConnectedVia = null;
                outcome = (NotificationKinds.AllyRequested, targetUserId);
                break;
            case AllyStatus.Pending when row.RequesterId == requesterId:
                break;   // idempotent — already asked
            case AllyStatus.Pending:
                // crossed/mutual request: the other side already asked before this side responded
                row.Status = AllyStatus.Accepted;
                row.RespondedAt = DateTimeOffset.UtcNow;
                row.ConnectedVia = "mutual_request";
                await TryAttachFirstSharedEventAsync(row, ct);
                outcome = (NotificationKinds.AllyAccepted, row.RequesterId);   // the original requester, not the caller
                break;
            case AllyStatus.Accepted:
                break;   // no-op
        }
        row.UpdatedAt = DateTimeOffset.UtcNow;
        return outcome;
    }

    public async Task<ServiceResult<AllyConnectionView>> AcceptAsync(Guid userId, Guid connectionId, CancellationToken ct = default)
    {
        var row = await db.AllyConnections.FirstOrDefaultAsync(a => a.Id == connectionId, ct);
        if (row is null || !IsParty(row, userId)) return ServiceResult<AllyConnectionView>.Fail("not_found");
        if (row.Status != AllyStatus.Pending || row.AddresseeId != userId)
            return ServiceResult<AllyConnectionView>.Fail("invalid_state");

        row.Status = AllyStatus.Accepted;
        row.RespondedAt = DateTimeOffset.UtcNow;
        row.ConnectedVia = "request";
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await TryAttachFirstSharedEventAsync(row, ct);
        await db.SaveChangesAsync(ct);
        await NotifyAllyEventAsync(NotificationKinds.AllyAccepted, row.RequesterId, userId, row.Id, ct);
        return ServiceResult<AllyConnectionView>.Success(await ToViewAsync(row, userId, ct));
    }

    public async Task<ServiceResult<AllyConnectionView>> DeclineAsync(Guid userId, Guid connectionId, CancellationToken ct = default)
    {
        var row = await db.AllyConnections.FirstOrDefaultAsync(a => a.Id == connectionId, ct);
        if (row is null || !IsParty(row, userId)) return ServiceResult<AllyConnectionView>.Fail("not_found");
        if (row.Status != AllyStatus.Pending || row.AddresseeId != userId)
            return ServiceResult<AllyConnectionView>.Fail("invalid_state");

        row.Status = AllyStatus.Declined;
        row.RespondedAt = DateTimeOffset.UtcNow;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await NotifyAllyEventAsync(NotificationKinds.AllyDeclined, row.RequesterId, userId, row.Id, ct);
        return ServiceResult<AllyConnectionView>.Success(await ToViewAsync(row, userId, ct));
    }

    public async Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid connectionId, CancellationToken ct = default)
    {
        var row = await db.AllyConnections.FirstOrDefaultAsync(a => a.Id == connectionId, ct);
        if (row is null || !IsParty(row, userId)) return ServiceResult<bool>.Fail("not_found");
        if (row.Status is not (AllyStatus.Pending or AllyStatus.Accepted))
            return ServiceResult<bool>.Fail("invalid_state");

        var wasAccepted = row.Status == AllyStatus.Accepted;
        row.Status = AllyStatus.Revoked;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Only notify when an established connection is removed — a withdrawn pending request never
        // reached the other party's attention as an ally in the first place, so staying quiet avoids noise.
        if (wasAccepted)
        {
            var otherParty = row.UserLowId == userId ? row.UserHighId : row.UserLowId;
            await NotifyAllyEventAsync(NotificationKinds.AllyRemoved, otherParty, userId, row.Id, ct);
        }
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<AllyConnectionView>> SetVisibilityAsync(
        Guid userId, Guid connectionId, string visibility, CancellationToken ct = default)
    {
        if (!Enum.TryParse<AllyVisibility>(visibility, ignoreCase: true, out var parsed))
            return ServiceResult<AllyConnectionView>.Fail("invalid_visibility");

        var row = await db.AllyConnections.FirstOrDefaultAsync(a => a.Id == connectionId, ct);
        if (row is null || !IsParty(row, userId)) return ServiceResult<AllyConnectionView>.Fail("not_found");

        // Deliberately allowed in every status rather than gated to Accepted: visibility is a display
        // preference, not a state transition, so there is no invalid_state branch to test. A non-Accepted
        // row is already absent from the public list, so setting it early is simply a no-op that persists.
        //
        // BUG-A: the caller sets ONLY their own flag. Assigning the shared column directly made this
        // last-writer-wins, so B could silently republish a pair A had hidden. Now the pair is public
        // only when neither party hides it, and each party can freely reverse their OWN choice.
        var hide = parsed == AllyVisibility.Hidden;
        if (row.UserLowId == userId) row.HiddenByLow = hide; else row.HiddenByHigh = hide;
        row.Visibility = row.HiddenByLow || row.HiddenByHigh ? AllyVisibility.Hidden : AllyVisibility.Public;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<AllyConnectionView>.Success(await ToViewAsync(row, userId, ct));
    }

    public async Task<IReadOnlyList<AllyConnectionView>> ListIncomingAsync(Guid userId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var (skip, take) = Paging(page, pageSize);
        var rows = await db.AllyConnections.AsNoTracking()
            .Where(a => a.AddresseeId == userId && a.Status == AllyStatus.Pending)
            // DB-6: RequestedAt is not unique across a burst of requests.
            .OrderByDescending(a => a.RequestedAt).ThenByDescending(a => a.Id).Skip(skip).Take(take).ToListAsync(ct);
        return await ToViewsAsync(rows, userId, ct);
    }

    public async Task<IReadOnlyList<AllyConnectionView>> ListOutgoingAsync(Guid userId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var (skip, take) = Paging(page, pageSize);
        var rows = await db.AllyConnections.AsNoTracking()
            .Where(a => a.RequesterId == userId && a.Status == AllyStatus.Pending)
            .OrderByDescending(a => a.RequestedAt).ThenByDescending(a => a.Id).Skip(skip).Take(take).ToListAsync(ct);
        return await ToViewsAsync(rows, userId, ct);
    }

    public async Task<IReadOnlyList<AllyConnectionView>> ListMineAsync(Guid userId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var (skip, take) = Paging(page, pageSize);
        var rows = await db.AllyConnections.AsNoTracking()
            .Where(a => (a.UserLowId == userId || a.UserHighId == userId) && a.Status == AllyStatus.Accepted)
            .OrderByDescending(a => a.RespondedAt).ThenByDescending(a => a.Id).Skip(skip).Take(take).ToListAsync(ct);
        return await ToViewsAsync(rows, userId, ct);
    }

    /// <summary>Clamps caller-supplied paging so a hostile or careless page size can't reintroduce the
    /// unbounded response these endpoints used to return.</summary>
    private static (int Skip, int Take) Paging(int page, int pageSize)
    {
        var size = Math.Clamp(pageSize, 1, 100);
        return (Math.Max(0, (Math.Max(1, page) - 1) * size), size);
    }

    public async Task<ServiceResult<IReadOnlyList<AllyProfileCard>>> GetAlliesForProfileAsync(
        string username, int page, int pageSize, Guid? viewerId, CancellationToken ct = default)
    {
        username = username.ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null) return ServiceResult<IReadOnlyList<AllyProfileCard>>.Fail("not_found");
        // Moderation reaches display here too (BUG-B). This route does not pass through
        // PublicProfileService.LoadAsync, so it does not inherit that chokepoint's gate.
        if (user.BannedAt is not null || user.SuspendedAt is not null)
            return ServiceResult<IReadOnlyList<AllyProfileCard>>.Fail("not_found");

        // Gated by the resolver, not by ProfilePublic/ShowAllies (D-232). Reading the booleans meant a
        // Network or Profile tier of Connections/EventParticipants dual-wrote to false and this route
        // returned empty — or 404 — to *everyone*, the owner included. Two of the four tiers were dead
        // here, and the owner could not see their own list.
        var access = await visibility.ResolveAsync(user.Id, viewerId, ct);
        if (!access.CanSee(ProfileSection.Profile))
            return ServiceResult<IReadOnlyList<AllyProfileCard>>.Fail("not_found");
        if (!access.CanSee(ProfileSection.Network))
            return ServiceResult<IReadOnlyList<AllyProfileCard>>.Success(Array.Empty<AllyProfileCard>());

        var rows = await db.AllyConnections.AsNoTracking()
            .Where(a => (a.UserLowId == user.Id || a.UserHighId == user.Id)
                && a.Status == AllyStatus.Accepted && a.Visibility == AllyVisibility.Public)
            .OrderByDescending(a => a.RespondedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        // The relationship was mutually consented, but showing it on X's page still exposes Y's
        // identity — Y's own choice gates that independently of X's Network tier.
        //
        // Resolved for the whole page in one batch (D-233), against the *real* viewer. The previous
        // `u.ProfilePublic` filter collapsed Y's tier to a boolean, so a counterparty on the
        // Connections tier was omitted even from a viewer who was one of their connections. The batch
        // primitive costs the same one query and answers it correctly. Moderation is applied inside it.
        var otherIds = rows.Select(r => r.UserLowId == user.Id ? r.UserHighId : r.UserLowId).Distinct().ToList();
        var showable = await visibility.VisibleProfileIdsAsync(otherIds, viewerId, ct);
        var others = await db.Users.AsNoTracking()
            .Where(u => showable.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Username, u.AvatarKey })
            .ToDictionaryAsync(u => u.Id, ct);

        var cards = new List<AllyProfileCard>();
        foreach (var r in rows)
        {
            var otherId = r.UserLowId == user.Id ? r.UserHighId : r.UserLowId;
            if (!others.TryGetValue(otherId, out var other)) continue;
            // ponytail: still one shared-event count per surviving card. Folding it into a single
            // grouped query means reworking UserEventIdsAsync's multi-source union; the page is capped
            // so the cost is bounded, and correctness is unaffected.
            var mutual = await MutualEventCountAsync(user.Id, otherId, ct);
            cards.Add(new AllyProfileCard(otherId, other.Name, other.Username, other.AvatarKey, mutual,
                await storage.PresignOrNullAsync(other.AvatarKey, ct)));
        }
        return ServiceResult<IReadOnlyList<AllyProfileCard>>.Success(cards);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetStatusBatchAsync(
        Guid callerId, IReadOnlyList<Guid> targetUserIds, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var id in targetUserIds) result[id] = "none";
        if (targetUserIds.Count == 0) return result;

        var rows = await db.AllyConnections.AsNoTracking()
            .Where(a => (a.UserLowId == callerId && targetUserIds.Contains(a.UserHighId))
                     || (a.UserHighId == callerId && targetUserIds.Contains(a.UserLowId)))
            .ToListAsync(ct);
        foreach (var r in rows)
        {
            var other = r.UserLowId == callerId ? r.UserHighId : r.UserLowId;
            result[other] = r.Status switch
            {
                AllyStatus.Accepted => "accepted",
                AllyStatus.Pending when r.RequesterId == callerId => "pending_outgoing",
                AllyStatus.Pending => "pending_incoming",
                _ => "none",   // Declined/Revoked — free to re-request
            };
        }
        return result;
    }

    /// <summary>Shared history is only for people who've actually connected — mirrors the one place
    /// this is surfaced today (the "Shared history" toggle between existing allies). Without this
    /// check, any authenticated caller could pull another user's shared events/orgs by GUID alone,
    /// regardless of that user's own privacy settings (D-211 audit finding).</summary>
    public async Task<MutualDetail> GetMutualDetailAsync(Guid callerId, Guid otherUserId, CancellationToken ct = default)
    {
        var (low, high) = Canonical(callerId, otherUserId);
        var isAccepted = await db.AllyConnections.AsNoTracking()
            .AnyAsync(a => a.UserLowId == low && a.UserHighId == high && a.Status == AllyStatus.Accepted, ct);
        if (!isAccepted)
            return new MutualDetail(Array.Empty<SharedEventSummary>(), Array.Empty<SharedOrgSummary>(),
                Array.Empty<ProfileRelationship>());

        var myEvents = await UserEventIdsAsync(callerId, ct);
        var theirEvents = await UserEventIdsAsync(otherUserId, ct);
        var sharedEventIds = myEvents.Intersect(theirEvents).ToList();

        var sharedEvents = sharedEventIds.Count == 0
            ? new List<SharedEventSummary>()
            : await db.Events.AsNoTracking().Where(e => sharedEventIds.Contains(e.Id))
                .OrderByDescending(e => e.StartsAt)
                .Select(e => new SharedEventSummary(e.Id, e.Title, e.Slug, e.StartsAt))
                .Take(50).ToListAsync(ct);

        var myOrgIds = await MembershipOrgIdsAsync(callerId, ct);
        var theirOrgIds = await MembershipOrgIdsAsync(otherUserId, ct);
        var sharedOrgIds = myOrgIds.Intersect(theirOrgIds).ToList();

        var sharedOrgs = sharedOrgIds.Count == 0
            ? new List<SharedOrgSummary>()
            : await db.Organizations.AsNoTracking().Where(o => sharedOrgIds.Contains(o.Id))
                .Select(o => new SharedOrgSummary(o.Id, o.Name, o.Slug))
                .Take(50).ToListAsync(ct);

        // Why they know each other (D-226), derived from both fact-sets. Both loads are memoised for
        // this request, which is precisely why D-224 keyed the memo by user rather than by request.
        var relationships = ConnectionEngine.Derive(
            await facts.LoadAsync(callerId, ct), await facts.LoadAsync(otherUserId, ct));

        return new MutualDetail(sharedEvents, sharedOrgs, relationships);
    }

    public async Task<IReadOnlyList<AllySuggestion>> GetSuggestionsAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        var myEventIds = (await UserEventIdsAsync(userId, ct)).ToList();
        var myOrgIds = await MembershipOrgIdsAsync(userId, ct);

        // Set-based candidate gathering — a handful of queries total, never one per candidate.
        var sharedEventsByCandidate = new Dictionary<Guid, HashSet<Guid>>();
        void AddEventPairs(IEnumerable<(Guid UserId, Guid EventId)> pairs)
        {
            foreach (var (uid, eid) in pairs)
            {
                if (!sharedEventsByCandidate.TryGetValue(uid, out var set)) sharedEventsByCandidate[uid] = set = new();
                set.Add(eid);
            }
        }
        if (myEventIds.Count > 0)
        {
            AddEventPairs((await db.EventParticipants.AsNoTracking()
                .Where(p => p.SubjectType == ParticipantSubjectType.Person && p.SubjectId != userId
                    && myEventIds.Contains(p.EventId) && p.Visibility == ParticipantVisibility.Public
                    && (p.State == ParticipantState.Accepted || p.State == ParticipantState.Active || p.State == ParticipantState.Completed))
                .Select(p => new { p.SubjectId, p.EventId }).Distinct().ToListAsync(ct))
                .Select(x => (x.SubjectId, x.EventId)));

            AddEventPairs((await db.Tickets.AsNoTracking()
                .Where(t => t.UserId != null && t.UserId != userId && t.State == TicketState.CheckedIn && myEventIds.Contains(t.EventId))
                .Select(t => new { UserId = t.UserId!.Value, t.EventId }).Distinct().ToListAsync(ct))
                .Select(x => (x.UserId, x.EventId)));
        }

        var sharedOrgsByCandidate = new Dictionary<Guid, HashSet<Guid>>();
        if (myOrgIds.Count > 0)
        {
            var orgPairs = await db.Memberships.AsNoTracking()
                .Where(m => m.UserId != userId && m.ShowOnProfile && myOrgIds.Contains(m.OrgId))
                .Select(m => new { m.UserId, m.OrgId }).Distinct().ToListAsync(ct);
            foreach (var p in orgPairs)
            {
                if (!sharedOrgsByCandidate.TryGetValue(p.UserId, out var set)) sharedOrgsByCandidate[p.UserId] = set = new();
                set.Add(p.OrgId);
            }
        }

        var candidateIds = sharedEventsByCandidate.Keys.Union(sharedOrgsByCandidate.Keys).ToList();
        if (candidateIds.Count == 0) return Array.Empty<AllySuggestion>();

        // Exclude anyone already connected or pending in either direction.
        var existing = await db.AllyConnections.AsNoTracking()
            .Where(a => (a.UserLowId == userId && candidateIds.Contains(a.UserHighId))
                     || (a.UserHighId == userId && candidateIds.Contains(a.UserLowId)))
            .Where(a => a.Status == AllyStatus.Accepted || a.Status == AllyStatus.Pending)
            .Select(a => a.UserLowId == userId ? a.UserHighId : a.UserLowId)
            .ToListAsync(ct);
        var excluded = existing.ToHashSet();

        var ranked = candidateIds.Where(id => !excluded.Contains(id))
            .Select(id => new
            {
                Id = id,
                EventCount = sharedEventsByCandidate.TryGetValue(id, out var e) ? e.Count : 0,
                OrgCount = sharedOrgsByCandidate.TryGetValue(id, out var o) ? o.Count : 0,
            })
            .OrderByDescending(x => x.EventCount + x.OrgCount)
            .Take(limit)
            .ToList();
        if (ranked.Count == 0) return Array.Empty<AllySuggestion>();

        var pageIds = ranked.Select(x => x.Id).ToList();
        // BUG-D: a non-public profile is excluded outright rather than returned with its name intact and
        // username/avatar nulled. That half-measure both leaked the real name of someone who chose not to
        // be publicly visible AND rendered an unnavigable card (no username to link to).
        // BUG-B: moderated accounts are excluded on the same pass — now inside the batch primitive.
        // The caller is the viewer here, so a suggestion whose profile is Connections-tier and who is
        // already connected to the caller resolves correctly rather than being dropped (D-233).
        var showable = await visibility.VisibleProfileIdsAsync(pageIds, userId, ct);
        var users = await db.Users.AsNoTracking()
            .Where(u => showable.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Username, u.AvatarKey })
            .ToDictionaryAsync(u => u.Id, ct);

        var suggestions = ranked.Where(r => users.ContainsKey(r.Id)).Select(r =>
        {
            var u = users[r.Id];
            var reason = r.EventCount > 0 && r.OrgCount > 0
                ? $"{r.EventCount} shared event{(r.EventCount == 1 ? "" : "s")}, same organization"
                : r.EventCount > 0
                    ? $"{r.EventCount} shared event{(r.EventCount == 1 ? "" : "s")}"
                    : "Same organization";
            // Every survivor is public and unmoderated (filtered above), so the fields are unconditional.
            return new AllySuggestion(r.Id, u.Name, u.Username, u.AvatarKey, r.EventCount, r.OrgCount, reason);
        }).ToList();
        // D-302, presigned in a second pass: the projection above is a synchronous lambda, and awaiting
        // inside one is what would silently serialise the whole page onto the request thread.
        for (var i = 0; i < suggestions.Count; i++)
            suggestions[i] = suggestions[i] with { AvatarUrl = await storage.PresignOrNullAsync(suggestions[i].AvatarKey, ct) };
        return suggestions;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<List<Guid>> MembershipOrgIdsAsync(Guid userId, CancellationToken ct)
        => await db.Memberships.AsNoTracking().Where(m => m.UserId == userId && m.ShowOnProfile)
            .Select(m => m.OrgId).ToListAsync(ct);

    /// <summary>Sends the one notification each ally state transition produces, via the "do it right"
    /// pipeline (DB row + SignalR + FCM in one call) rather than a raw <c>db.Notifications.Add</c>.
    /// `route` follows the generic convention (<c>docs/architecture/PROFESSIONAL_IDENTITY_SPEC.md</c>
    /// §Notifications) — either client navigates there on tap, independent of `Kind`.</summary>
    private async Task NotifyAllyEventAsync(string kind, Guid recipientId, Guid actorId, Guid connectionId, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().Where(u => u.Id == actorId)
            .Select(u => new { u.Name, u.Username }).FirstAsync(ct);
        var (title, body) = kind switch
        {
            NotificationKinds.AllyRequested => ("New Ally Request", $"{actor.Name} wants to connect with you."),
            NotificationKinds.AllyAccepted => ("Request Accepted", $"{actor.Name} accepted your ally request."),
            NotificationKinds.AllyDeclined => ("Request Declined", $"{actor.Name} declined your ally request."),
            NotificationKinds.AllyRemoved => ("Connection Removed", $"{actor.Name} is no longer your ally."),
            _ => (kind, ""),
        };
        // The recipient is the viewer — this deep link exists only for them (D-233). Resolving against
        // them rather than against `ProfilePublic` means an "accepted" notification links to the profile
        // of someone whose tier is Connections, which is precisely the moment the recipient became
        // entitled to see it. The boolean sent them to /allies instead.
        var linkable = await visibility.VisibleProfileIdsAsync([actorId], recipientId, ct);
        var route = linkable.Contains(actorId) && actor.Username is not null ? $"/u/{actor.Username}" : "/allies";
        // connectionId rides alongside the generic `route` — kind-specific extra data a client that
        // recognizes "ally.*" kinds can use to render inline Accept/Decline with no extra lookup.
        await notify.NotifyAsync(recipientId, kind, title, body, new { route, connectionId }, ct);
    }

    private static bool IsParty(AllyConnection row, Guid userId) => row.RequesterId == userId || row.AddresseeId == userId;

    private static (Guid Low, Guid High) Canonical(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    /// <summary>Projects a whole page in <b>two</b> queries — one for the other parties, one for any
    /// first-shared events — instead of the 1–2 per row the per-row <see cref="ToViewAsync"/> costs. A
    /// user with a thousand allies previously made a thousand round trips to list them.</summary>
    private async Task<List<AllyConnectionView>> ToViewsAsync(List<AllyConnection> rows, Guid perspectiveUserId, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var otherIds = rows.Select(r => r.UserLowId == perspectiveUserId ? r.UserHighId : r.UserLowId)
            .Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => otherIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Username, u.AvatarKey })
            .ToDictionaryAsync(u => u.Id, ct);

        var eventIds = rows.Where(r => r.FirstSharedEventId != null)
            .Select(r => r.FirstSharedEventId!.Value).Distinct().ToList();
        var events = eventIds.Count == 0
            ? new Dictionary<Guid, (string Title, string Slug)>()
            : (await db.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Title, e.Slug }).ToListAsync(ct))
                .ToDictionary(e => e.Id, e => (e.Title, e.Slug));

        var views = new List<AllyConnectionView>(rows.Count);
        foreach (var r in rows)
        {
            var otherId = r.UserLowId == perspectiveUserId ? r.UserHighId : r.UserLowId;
            // A row whose counterparty vanished mid-page is skipped rather than crashed on; the FK is
            // Cascade, so this is only reachable in a delete-during-read interleaving.
            if (!users.TryGetValue(otherId, out var other)) continue;
            string? title = null, slug = null;
            if (r.FirstSharedEventId is Guid evId && events.TryGetValue(evId, out var ev))
                (title, slug) = (ev.Title, ev.Slug);

            views.Add(new AllyConnectionView(r.Id, otherId, other.Name, other.Username, other.AvatarKey,
                r.Status.ToString(), r.Visibility.ToString(), r.RequestedAt, r.RespondedAt,
                r.FirstSharedEventId, title, slug));
        }
        return views;
    }

    private async Task<AllyConnectionView> ToViewAsync(AllyConnection row, Guid perspectiveUserId, CancellationToken ct)
    {
        var otherId = row.UserLowId == perspectiveUserId ? row.UserHighId : row.UserLowId;
        var other = await db.Users.AsNoTracking().Where(u => u.Id == otherId)
            .Select(u => new { u.Name, u.Username, u.AvatarKey }).FirstAsync(ct);

        string? title = null, slug = null;
        if (row.FirstSharedEventId is Guid evId)
        {
            var ev = await db.Events.AsNoTracking().Where(e => e.Id == evId)
                .Select(e => new { e.Title, e.Slug }).FirstOrDefaultAsync(ct);
            title = ev?.Title; slug = ev?.Slug;
        }

        return new AllyConnectionView(row.Id, otherId, other.Name, other.Username, other.AvatarKey,
            row.Status.ToString(), row.Visibility.ToString(), row.RequestedAt, row.RespondedAt,
            row.FirstSharedEventId, title, slug);
    }

    /// <summary>Best-effort earliest public event both users have real involvement in (participated
    /// publicly, or checked in). Never fabricated — leaves <see cref="AllyConnection.FirstSharedEventId"/>
    /// null when no shared event is found.</summary>
    private async Task TryAttachFirstSharedEventAsync(AllyConnection row, CancellationToken ct)
    {
        var a = await UserEventIdsAsync(row.UserLowId, ct);
        if (a.Count == 0) return;
        var b = await UserEventIdsAsync(row.UserHighId, ct);
        var shared = a.Intersect(b).ToList();
        if (shared.Count == 0) return;

        var earliest = await db.Events.AsNoTracking()
            .Where(e => shared.Contains(e.Id))
            .OrderBy(e => e.StartsAt)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);
        if (earliest is Guid id) row.FirstSharedEventId = id;
    }

    private async Task<int> MutualEventCountAsync(Guid userA, Guid userB, CancellationToken ct)
    {
        var a = await UserEventIdsAsync(userA, ct);
        if (a.Count == 0) return 0;
        var b = await UserEventIdsAsync(userB, ct);
        return a.Intersect(b).Count();
    }

    private async Task<HashSet<Guid>> UserEventIdsAsync(Guid userId, CancellationToken ct)
    {
        var participated = await db.EventParticipants.AsNoTracking()
            .Where(p => p.SubjectType == ParticipantSubjectType.Person && p.SubjectId == userId
                && p.Visibility == ParticipantVisibility.Public
                && (p.State == ParticipantState.Accepted || p.State == ParticipantState.Active || p.State == ParticipantState.Completed))
            .Join(db.Events.AsNoTracking().Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                p => p.EventId, e => e.Id, (p, e) => e.Id)
            .ToListAsync(ct);
        var attended = await db.Tickets.AsNoTracking()
            .Where(t => t.UserId == userId && t.State == TicketState.CheckedIn)
            .Join(db.Events.AsNoTracking().Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed),
                t => t.EventId, e => e.Id, (t, e) => e.Id)
            .ToListAsync(ct);
        return new HashSet<Guid>(participated.Concat(attended));
    }
}
