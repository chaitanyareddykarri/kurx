using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

public class ScheduleService(KurxDbContext db, IEventAuthority authority) : IScheduleService
{
    public async Task<ServiceResult<SessionView>> CreateAsync(Guid userId, Guid eventId, bool isAdmin, SessionInput input, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<SessionView>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<SessionView>.Fail("forbidden");

        var title = input.Title.Trim();
        if (title.Length is < 1 or > 200) return ServiceResult<SessionView>.Fail("invalid_title");
        if (input.EndsAt <= input.StartsAt) return ServiceResult<SessionView>.Fail("invalid_dates");

        var kind = ScheduleItemKind.Session;
        if (input.Kind is not null && !Enum.TryParse(input.Kind, true, out kind)) return ServiceResult<SessionView>.Fail("invalid_kind");
        if (input.InventoryPoolId is { } poolId && !await db.InventoryPools.AnyAsync(p => p.Id == poolId && p.EventId == eventId, ct))
            return ServiceResult<SessionView>.Fail("invalid_pool");   // §3.4 — the pool must belong to this event

        var session = new EventSession
        {
            EventId = eventId,
            Title = title,
            Description = input.Description ?? "",
            Kind = kind,
            // Same Kind=Unspecified-vs-timestamptz issue as EventService.CreateAsync — see that fix's comment.
            StartsAt = DateTime.SpecifyKind(input.StartsAt, DateTimeKind.Utc),
            EndsAt = DateTime.SpecifyKind(input.EndsAt, DateTimeKind.Utc),
            Sort = input.Sort ?? await db.EventSessions.Where(s => s.EventId == eventId).CountAsync(ct),
            InventoryPoolId = input.InventoryPoolId,
        };
        db.EventSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return ServiceResult<SessionView>.Success(ToView(session, []));
    }

    public async Task<ServiceResult<SessionView>> UpdateAsync(Guid userId, Guid sessionId, bool isAdmin, SessionInput input, CancellationToken ct = default)
    {
        var session = await db.EventSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return ServiceResult<SessionView>.Fail("not_found");
        var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == session.EventId, ct);
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<SessionView>.Fail("forbidden");

        var title = input.Title.Trim();
        if (title.Length is < 1 or > 200) return ServiceResult<SessionView>.Fail("invalid_title");
        session.Title = title;
        session.Description = input.Description ?? session.Description;
        if (input.Kind is not null)
        {
            if (!Enum.TryParse<ScheduleItemKind>(input.Kind, true, out var kind)) return ServiceResult<SessionView>.Fail("invalid_kind");
            session.Kind = kind;
        }
        session.StartsAt = DateTime.SpecifyKind(input.StartsAt, DateTimeKind.Utc);
        session.EndsAt = DateTime.SpecifyKind(input.EndsAt, DateTimeKind.Utc);
        if (session.EndsAt <= session.StartsAt) return ServiceResult<SessionView>.Fail("invalid_dates");
        if (input.Sort is not null) session.Sort = input.Sort.Value;
        if (input.InventoryPoolId is { } poolId)   // null keeps the existing link (matches this file's update convention)
        {
            if (!await db.InventoryPools.AnyAsync(p => p.Id == poolId && p.EventId == session.EventId, ct)) return ServiceResult<SessionView>.Fail("invalid_pool");
            session.InventoryPoolId = poolId;
        }

        await db.SaveChangesAsync(ct);
        var speakerIds = await SpeakerIdsAsync(sessionId, ct);
        return ServiceResult<SessionView>.Success(ToView(session, speakerIds));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid sessionId, bool isAdmin, CancellationToken ct = default)
    {
        var session = await db.EventSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return ServiceResult<bool>.Fail("not_found");
        var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == session.EventId, ct);
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");

        db.EventSessions.Remove(session); // EventSessionSpeaker rows cascade
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<SessionView>> ListForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var sessions = await db.EventSessions.AsNoTracking().Where(s => s.EventId == eventId)
            .OrderBy(s => s.Sort).ThenBy(s => s.StartsAt).ToListAsync(ct);
        var links = await db.EventSessionSpeakers.AsNoTracking()
            .Where(x => sessions.Select(s => s.Id).Contains(x.SessionId)).ToListAsync(ct);
        var bySession = links.GroupBy(x => x.SessionId).ToDictionary(g => g.Key, g => g.Select(x => x.SpeakerId).ToList());
        return sessions.Select(s => ToView(s, bySession.TryGetValue(s.Id, out var ids) ? ids : [])).ToList();
    }

    public async Task<ServiceResult<bool>> ReorderAsync(Guid userId, Guid eventId, bool isAdmin, IReadOnlyList<Guid> orderedSessionIds, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");

        var sessions = await db.EventSessions.Where(s => s.EventId == eventId).ToListAsync(ct);
        if (orderedSessionIds.Count != sessions.Count || orderedSessionIds.Distinct().Count() != sessions.Count
            || !orderedSessionIds.All(id => sessions.Any(s => s.Id == id)))
            return ServiceResult<bool>.Fail("invalid_order");

        for (var i = 0; i < orderedSessionIds.Count; i++)
            sessions.First(s => s.Id == orderedSessionIds[i]).Sort = i;

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<List<Guid>> SpeakerIdsAsync(Guid sessionId, CancellationToken ct)
        => await db.EventSessionSpeakers.AsNoTracking().Where(x => x.SessionId == sessionId).Select(x => x.SpeakerId).ToListAsync(ct);

    private static SessionView ToView(EventSession s, IReadOnlyList<Guid> speakerIds) =>
        new(s.Id, s.EventId, s.Title, s.Description, s.Kind.ToString(), s.StartsAt, s.EndsAt, s.Sort, speakerIds, s.InventoryPoolId);
}
