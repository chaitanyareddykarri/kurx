using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>
/// The host "attendee sheet" (D-054) — implements the pre-existing IAttendeeService scaffolding.
/// Owner/Manager/Staff of the event's org only. **Returns attendee PII** (names/phones + custom-field
/// answers); a Staff-masked variant and full Q/ticket-type/group filtering are documented refinements.
/// Uses keyed WHERE…IN lookups composed in memory (no brittle multi-joins) for translation safety.
/// Covered by AttendeeTests (roster incl. both the user + guest-order PII paths, non-member 404, state
/// filter, CSV export), CI-green — the dev machine can't run the test host (Windows Application Control).
/// </summary>
public class AttendeeService(KurxDbContext db, IEventAuthority authority, IProfileVisibilityResolver visibility) : IAttendeeService
{

    public async Task<ServiceResult<(IReadOnlyList<AttendeeRow> Items, int Total)>> ListAsync(
        Guid userId, Guid eventId, AttendeeListFilter filter, CancellationToken ct = default)
    {
        var gate = await AuthorizeAsync(userId, eventId, ct);
        if (gate is not null) return ServiceResult<(IReadOnlyList<AttendeeRow>, int)>.Fail(gate);

        var take = Math.Clamp(filter.PageSize <= 0 ? 50 : filter.PageSize, 1, 100);
        var skip = Math.Max(0, (Math.Max(1, filter.Page) - 1) * take);

        var q = db.Tickets.AsNoTracking().Where(t => t.EventId == eventId);
        if (!string.IsNullOrWhiteSpace(filter.State) && Enum.TryParse<TicketState>(filter.State, true, out var state))
            q = q.Where(t => t.State == state);

        var total = await q.CountAsync(ct);
        // ThenByDescending(Id) is not decoration (DB-6): CreatedAt is set from DateTime.UtcNow, whose
        // resolution is coarser than a bulk issuance, so a group booking writes many tickets on the same
        // tick. Ordering on CreatedAt alone leaves those rows in an order the database may choose
        // differently per query — so page 2 could repeat a row page 1 already showed, or skip one entirely.
        var tickets = await q.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip(skip).Take(take).ToListAsync(ct);
        var rows = await ComposeRowsAsync(tickets, ct);
        return ServiceResult<(IReadOnlyList<AttendeeRow>, int)>.Success((rows, total));
    }

    /// <summary>How many tickets one export batch loads and hydrates. Bounded so peak memory is a function
    /// of this constant, never of the event's size.</summary>
    private const int ExportBatchSize = 500;

    public async Task<ServiceResult<Func<Stream, CancellationToken, Task>>> ExportCsvAsync(
        Guid userId, Guid eventId, CancellationToken ct = default)
    {
        // Resolved BEFORE the writer is handed back, so a refusal is still a clean 404/403 — once the
        // response body starts, the status line is already on the wire and cannot be taken back.
        var gate = await AuthorizeAsync(userId, eventId, ct);
        if (gate is not null) return ServiceResult<Func<Stream, CancellationToken, Task>>.Fail(gate);

        return ServiceResult<Func<Stream, CancellationToken, Task>>.Success(
            (output, writeCt) => WriteCsvAsync(eventId, output, writeCt));
    }

    /// <summary>Walks the roster in keyset batches and writes each one out before loading the next.
    ///
    /// <para><b>Keyset, not OFFSET.</b> A batched export is the one place deep paging is guaranteed rather
    /// than hypothetical — the last batch of a 50k roster would carry OFFSET 49500, re-walking the whole
    /// index every batch. The pivot is the <c>(CreatedAt, Id)</c> pair for the same reason the list ordering
    /// needs a tie-breaker: same-tick rows would otherwise be re-emitted or dropped at every batch seam.</para>
    ///
    /// <para>No BOM and <c>WriteLine</c>'s platform newline, both matching the bytes the previous
    /// <c>StringBuilder</c>/<c>Encoding.UTF8.GetBytes</c> pair produced — this changes when rows are written,
    /// never what they contain.</para></summary>
    private async Task WriteCsvAsync(Guid eventId, Stream output, CancellationToken ct)
    {
        await using var writer = new StreamWriter(
            output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        await writer.WriteLineAsync("ticket_code,name,phone,ticket_type,state,checked_in_at,group");

        (DateTime CreatedAt, Guid Id)? pivot = null;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var q = db.Tickets.AsNoTracking().Where(t => t.EventId == eventId);
            if (pivot is not null)
                q = q.Where(t => EF.Functions.GreaterThan(
                    ValueTuple.Create(pivot.Value.CreatedAt, pivot.Value.Id),
                    ValueTuple.Create(t.CreatedAt, t.Id)));

            var batch = await q.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                .Take(ExportBatchSize).ToListAsync(ct);
            if (batch.Count == 0) break;

            foreach (var r in await ComposeRowsAsync(batch, ct))
                await writer.WriteLineAsync(string.Join(',', Csv(r.Code.ToString()), Csv(r.BuyerName),
                    Csv(r.BuyerPhone), Csv(r.TicketTypeName), Csv(r.State),
                    Csv(r.CheckedInAt?.ToString("o")), Csv(r.GroupDisplayName)));

            // Flush per batch so the client receives bytes as they are produced rather than at the end,
            // and so the writer's buffer never grows with the roster either.
            await writer.FlushAsync(ct);

            if (batch.Count < ExportBatchSize) break;
            var last = batch[^1];
            pivot = (last.CreatedAt, last.Id);
        }
    }

    /// <returns>null if authorized, else the error code.</returns>
    private async Task<string?> AuthorizeAsync(Guid userId, Guid eventId, CancellationToken ct)
    {
        // Staff-and-up: front-of-house needs the roster without authority over what is being sold
        // (EventPermission.ViewAttendees). **No admin bypass** — preserved exactly: these endpoints never
        // passed one, so platform staff read attendees through the admin console instead.
        var access = await authority.ResolveAsync(userId, eventId, isAdmin: false, ct);
        if (!access.EventExists) return "not_found";
        if (!access.HasStanding) return "not_found";          // hide existence from outsiders (404-not-403)
        return access.Can(EventPermission.ViewAttendees) ? null : "forbidden";
    }

    private async Task<List<AttendeeRow>> ComposeRowsAsync(List<Ticket> tickets, CancellationToken ct)
    {
        var orderItemIds = tickets.Select(t => t.OrderItemId).Distinct().ToList();
        var orderItems = await db.OrderItems.AsNoTracking().Where(oi => orderItemIds.Contains(oi.Id)).ToListAsync(ct);
        var oiById = orderItems.ToDictionary(oi => oi.Id);
        var orderIds = orderItems.Select(oi => oi.OrderId).Distinct().ToList();
        var orders = await db.Orders.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
        var ttIds = orderItems.Select(oi => oi.TicketTypeId).Distinct().ToList();
        var ttNames = await db.TicketTypes.AsNoTracking().Where(x => ttIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var userIds = tickets.Where(t => t.UserId != null).Select(t => t.UserId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var gmIds = tickets.Where(t => t.GroupMemberId != null).Select(t => t.GroupMemberId!.Value).Distinct().ToList();
        var gms = await db.GroupMembers.AsNoTracking().Where(g => gmIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, ct);
        var groupIds = gms.Values.Select(g => g.GroupId).Distinct().ToList();
        var groups = await db.Groups.AsNoTracking().Where(gr => groupIds.Contains(gr.Id)).ToDictionaryAsync(gr => gr.Id, ct);
        // One batched visibility call alongside the batched loads above (D-233). Anonymous viewer: this
        // list is organizer-facing and carries no viewer identity, so the public-identity answer is the
        // same for every organizer — identical to the `ProfilePublic` check it replaces. The attendee's
        // real name and phone are shown regardless; only the *public profile link* is gated.
        var linkable = await visibility.VisibleProfileIdsAsync(userIds, null, ct);

        var rows = new List<AttendeeRow>(tickets.Count);
        foreach (var t in tickets)
        {
            oiById.TryGetValue(t.OrderItemId, out var oi);
            var order = oi is not null && orders.TryGetValue(oi.OrderId, out var o) ? o : null;
            var ttId = oi?.TicketTypeId ?? Guid.Empty;
            var ttName = oi is not null && ttNames.TryGetValue(oi.TicketTypeId, out var n) ? n : "";
            var user = t.UserId is not null && users.TryGetValue(t.UserId.Value, out var u) ? u : null;
            var gm = t.GroupMemberId is not null && gms.TryGetValue(t.GroupMemberId.Value, out var g) ? g : null;
            var group = gm is not null && groups.TryGetValue(gm.GroupId, out var gr) ? gr : null;

            var name = !string.IsNullOrWhiteSpace(user?.Name) ? user!.Name : (gm?.Name ?? order?.GuestName ?? "");
            // Canonical column first (D-089): this feeds the attendee CSV, which organizers paste into
            // dialers and CRMs. A bare "6591234567" there is indistinguishable from an Indian number, so
            // the export has to carry the country code the account was registered with.
            var phone = user?.PhoneE164 ?? user?.Phone ?? gm?.Phone ?? order?.GuestPhone ?? "";
            var answers = ParseAnswers(t.AnswersJson);
            var publicUsername = user is not null && linkable.Contains(user.Id) ? user.Username : null;

            rows.Add(new AttendeeRow(t.Id, t.Code, t.State.ToString(), t.CheckedInAt, name, phone,
                ttId, ttName, group?.Id, group?.GroupNumber, group?.DisplayName, answers,
                user?.Id, publicUsername, publicUsername is not null ? user!.AvatarKey : null));
        }
        return rows;
    }

    private static IReadOnlyDictionary<string, string?> ParseAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string?>();
        try { return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>(); }
        catch { return new Dictionary<string, string?>(); }
    }

    private static string Csv(string? s) => s is null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";
}
