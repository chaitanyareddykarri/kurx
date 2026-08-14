using System.Security.Claims;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Hubs;

/// <summary>Live sales feed for one org's host dashboard — clients join the "org:{id}" group.</summary>
[Authorize]
public class SalesHub(KurxDbContext db) : Hub
{
    public async Task JoinOrg(Guid orgId)
    {
        var userId = UserId();
        // D-186: same kurx_admin bypass every org-scoped REST endpoint already honours.
        if (!IsAdmin())
        {
            var isMember = await db.Memberships.AnyAsync(m => m.OrgId == orgId && m.UserId == userId);
            if (!isMember) throw new HubException("forbidden");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"org:{orgId}");
    }

    public Task LeaveOrg(Guid orgId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"org:{orgId}");

    private Guid UserId()
        => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new HubException("invalid_token");

    private bool IsAdmin() => Context.User?.HasClaim("kurx_admin", "true") == true;
}
