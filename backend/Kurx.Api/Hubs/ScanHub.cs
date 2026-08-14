using System.Security.Claims;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Hubs;

/// <summary>Live scan feed for one event — scanner staff and the host dashboard join the "event:{id}" group.</summary>
[Authorize]
public class ScanHub(KurxDbContext db) : Hub
{
    public async Task JoinEvent(Guid eventId)
    {
        var userId = UserId();
        var orgId = await db.Events.Where(e => e.Id == eventId).Select(e => (Guid?)e.RepresentingOrgId).FirstOrDefaultAsync();
        if (orgId is null) throw new HubException("not_found");

        // D-186: same kurx_admin bypass every org-scoped REST endpoint already honours, so the admin
        // console's event workspace can watch any org's live scan feed, not just its own members'.
        if (!IsAdmin())
        {
            var isMember = await db.Memberships.AnyAsync(m => m.OrgId == orgId && m.UserId == userId);
            if (!isMember) throw new HubException("forbidden");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"event:{eventId}");
    }

    public Task LeaveEvent(Guid eventId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"event:{eventId}");

    private Guid UserId()
        => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new HubException("invalid_token");

    private bool IsAdmin() => Context.User?.HasClaim("kurx_admin", "true") == true;
}
