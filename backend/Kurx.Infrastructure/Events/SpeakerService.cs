using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

public class SpeakerService(
    KurxDbContext db, IEventAuthority authority, IAuditWriter audit, IProfileVisibilityResolver visibility,
    IStorage storage) : ISpeakerService
{
    public async Task<ServiceResult<SpeakerView>> CreateAsync(Guid userId, Guid orgId, bool isAdmin, SpeakerInput input, CancellationToken ct = default)
    {
        if (!(await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct)).CanManage) return ServiceResult<SpeakerView>.Fail("forbidden");
        var name = input.Name.Trim();
        if (name.Length is < 2 or > 150) return ServiceResult<SpeakerView>.Fail("invalid_name");
        if (input.UserId is Guid linkId && !await db.Users.AnyAsync(u => u.Id == linkId, ct))
            return ServiceResult<SpeakerView>.Fail("invalid_user");

        var speaker = new Speaker
        {
            OrgId = orgId,
            Name = name,
            Bio = input.Bio ?? "",
            PhotoKey = input.PhotoKey,
            Company = input.Company ?? "",
            Role = input.Role ?? "",
            SocialLinksJson = input.SocialLinksJson,
            UserId = input.UserId,
        };
        db.Speakers.Add(speaker);
        WriteAudit("speaker.create", speaker.Id, userId, isAdmin, before: null, after: Snapshot(speaker));
        await db.SaveChangesAsync(ct);
        return ServiceResult<SpeakerView>.Success(await ToViewAsync(speaker, ct));
    }

    public async Task<ServiceResult<SpeakerView>> UpdateAsync(Guid userId, Guid speakerId, bool isAdmin, SpeakerInput input, CancellationToken ct = default)
    {
        var speaker = await db.Speakers.FirstOrDefaultAsync(s => s.Id == speakerId, ct);
        if (speaker is null) return ServiceResult<SpeakerView>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, speaker.OrgId, isAdmin, ct)).CanManage) return ServiceResult<SpeakerView>.Fail("forbidden");
        if (input.UserId is Guid linkId && !await db.Users.AnyAsync(u => u.Id == linkId, ct))
            return ServiceResult<SpeakerView>.Fail("invalid_user");

        var before = Snapshot(speaker);
        var name = input.Name.Trim();
        if (name.Length is < 2 or > 150) return ServiceResult<SpeakerView>.Fail("invalid_name");
        speaker.Name = name;
        if (input.Bio is not null) speaker.Bio = input.Bio;
        if (input.PhotoKey is not null) speaker.PhotoKey = input.PhotoKey;
        if (input.Company is not null) speaker.Company = input.Company;
        if (input.Role is not null) speaker.Role = input.Role;
        if (input.SocialLinksJson is not null) speaker.SocialLinksJson = input.SocialLinksJson;
        speaker.UserId = input.UserId;

        WriteAudit("speaker.update", speaker.Id, userId, isAdmin, before, Snapshot(speaker));
        await db.SaveChangesAsync(ct);
        return ServiceResult<SpeakerView>.Success(await ToViewAsync(speaker, ct));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid speakerId, bool isAdmin, CancellationToken ct = default)
    {
        var speaker = await db.Speakers.FirstOrDefaultAsync(s => s.Id == speakerId, ct);
        if (speaker is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, speaker.OrgId, isAdmin, ct)).CanManage) return ServiceResult<bool>.Fail("forbidden");

        // Hard delete (EventSpeaker/EventSessionSpeaker cascade), so the audit row is the only
        // surviving record of what was removed.
        WriteAudit("speaker.delete", speaker.Id, userId, isAdmin, before: Snapshot(speaker), after: null);
        db.Speakers.Remove(speaker);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<SpeakerView>>> ListForOrgAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct = default)
    {
        if (!(await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct)).IsMember && !isAdmin) return ServiceResult<IReadOnlyList<SpeakerView>>.Fail("forbidden");
        var speakers = await db.Speakers.AsNoTracking().Where(s => s.OrgId == orgId).OrderBy(s => s.Name).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<SpeakerView>>.Success(await ToViewsAsync(speakers, ct));
    }

    public async Task<ServiceResult<bool>> AssignToEventAsync(Guid userId, Guid eventId, bool isAdmin, Guid speakerId, Guid? sessionId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");
        if (!await db.Speakers.AnyAsync(s => s.Id == speakerId && s.OrgId == ev.RepresentingOrgId, ct)) return ServiceResult<bool>.Fail("invalid_speaker");

        if (!await db.EventSpeakers.AnyAsync(es => es.EventId == eventId && es.SpeakerId == speakerId, ct))
        {
            var sort = await db.EventSpeakers.Where(es => es.EventId == eventId).CountAsync(ct);
            db.EventSpeakers.Add(new EventSpeaker { EventId = eventId, SpeakerId = speakerId, Sort = sort });
            WriteAudit("speaker.event.assign", speakerId, userId, isAdmin, before: null,
                after: new { orgId = ev.RepresentingOrgId, eventId, speakerId, sort });
        }

        if (sessionId is not null)
        {
            if (!await db.EventSessions.AnyAsync(s => s.Id == sessionId && s.EventId == eventId, ct))
                return ServiceResult<bool>.Fail("invalid_session");
            if (!await db.EventSessionSpeakers.AnyAsync(x => x.SessionId == sessionId && x.SpeakerId == speakerId, ct))
                db.EventSessionSpeakers.Add(new EventSessionSpeaker { SessionId = sessionId.Value, SpeakerId = speakerId });
        }

        db.OutboxMessages.Add(Search.SearchReindex.Message(eventId));   // V3 §15 (Phase 16): speakers are indexed in the discovery document → reindex
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> RemoveFromEventAsync(Guid userId, Guid eventId, bool isAdmin, Guid speakerId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");

        var link = await db.EventSpeakers.FirstOrDefaultAsync(es => es.EventId == eventId && es.SpeakerId == speakerId, ct);
        if (link is null) return ServiceResult<bool>.Fail("not_found");
        WriteAudit("speaker.event.remove", speakerId, userId, isAdmin,
            before: new { orgId = ev.RepresentingOrgId, eventId, speakerId, sort = link.Sort }, after: null);
        db.EventSpeakers.Remove(link);

        var sessionLinks = await db.EventSessions.Where(s => s.EventId == eventId)
            .Join(db.EventSessionSpeakers.Where(x => x.SpeakerId == speakerId), s => s.Id, x => x.SessionId, (s, x) => x)
            .ToListAsync(ct);
        db.EventSessionSpeakers.RemoveRange(sessionLinks);

        db.OutboxMessages.Add(Search.SearchReindex.Message(eventId));   // V3 §15 (Phase 16): speaker removed from the indexed document → reindex
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<SpeakerView>> ListForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var speakers = await db.EventSpeakers.AsNoTracking().Where(es => es.EventId == eventId).OrderBy(es => es.Sort)
            .Join(db.Speakers.AsNoTracking(), es => es.SpeakerId, s => s.Id, (es, s) => s)
            .ToListAsync(ct);
        return await ToViewsAsync(speakers, ct);
    }

    /// <summary>Audit payload. A speaker's name/company/role are published event content, not private
    /// PII, so they are safe to record (D-102 redaction rule); bio is omitted as free text.</summary>
    private static object Snapshot(Speaker s) => new
    {
        orgId = s.OrgId, name = s.Name, company = s.Company, role = s.Role, photoKey = s.PhotoKey,
    };

    private void WriteAudit(string action, Guid speakerId, Guid actorId, bool isAdmin, object? before, object? after)
        => audit.Write(new AuditEvent(action, "speakers", speakerId,
            ActorType: isAdmin ? "admin" : "user", ActorId: actorId, Before: before, After: after));

    private async Task<SpeakerView> ToViewAsync(Speaker s, CancellationToken ct)
    {
        if (s.UserId is not Guid uid) return await ToViewAsync(s, null, EmptyIds, ct);
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
        return await ToViewAsync(s, u, await visibility.VisibleProfileIdsAsync([uid], null, ct), ct);
    }

    /// <summary>Batch-joins linked accounts in one query — avoids N+1 for a whole org/event's speaker list.</summary>
    private async Task<IReadOnlyList<SpeakerView>> ToViewsAsync(List<Speaker> speakers, CancellationToken ct)
    {
        var userIds = speakers.Where(s => s.UserId.HasValue).Select(s => s.UserId!.Value).Distinct().ToList();
        var usersById = userIds.Count == 0
            ? new Dictionary<Guid, User>()
            : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        // One batched visibility call for the whole speaker list, matching the batched user load above
        // (D-233). Anonymous viewer: a speaker card is a public event artefact with no caller identity
        // threaded to it, so the answer is the same for everyone — as `ProfilePublic` was.
        var linkable = await visibility.VisibleProfileIdsAsync(userIds, null, ct);
        return await Task.WhenAll(speakers.Select(s =>
            ToViewAsync(s, s.UserId is Guid uid && usersById.TryGetValue(uid, out var u) ? u : null, linkable, ct)));
    }

    private static readonly IReadOnlySet<Guid> EmptyIds = new HashSet<Guid>();

    /// Async and instance-scoped since D-302: a speaker carries two independent pictures — their own
    /// uploaded photo and, when linked, the Kurx account's avatar — and neither key is fetchable as-is.
    private async Task<SpeakerView> ToViewAsync(Speaker s, User? u, IReadOnlySet<Guid> linkable, CancellationToken ct)
    {
        var show = u is not null && linkable.Contains(u.Id);
        var avatarKey = show ? u!.AvatarKey : null;
        return new(s.Id, s.OrgId, s.Name, s.Bio, s.PhotoKey, s.Company, s.Role,
            s.SocialLinksJson, s.UserId, show ? u!.Username : null, avatarKey,
            await storage.PresignOrNullAsync(s.PhotoKey, ct),
            await storage.PresignOrNullAsync(avatarKey, ct));
    }
}
