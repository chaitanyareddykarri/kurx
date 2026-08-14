using Kurx.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Kurx.Api.Hubs;

/// <summary>Realtime status for a pending device-approval sign-in (AM9, ADR-AM17).
///
/// <para>This is the one hub that is deliberately <b>anonymous</b>: the whole point is a browser that has
/// no session yet, waiting to be told its login was approved. Authorization therefore comes from the
/// <b>poll token</b> issued by <c>/v1/auth/login/password</c> — the same secret that already gates
/// <c>/v1/auth/login/status</c> (D-080). A caller who knows only a challenge id cannot subscribe, exactly
/// as it cannot collect tokens.</para>
///
/// <para>The hub carries a <b>status signal only, never tokens</b>. The client still calls
/// <c>/status</c> to collect its session, so the "minted exactly once" guarantee stays on a single code
/// path instead of being duplicated across two transports.</para></summary>
public class LoginHub(ILoginApprovalService logins) : Hub
{
    public async Task Watch(Guid challengeId, string pollToken)
    {
        if (string.IsNullOrWhiteSpace(pollToken) || !await logins.CanWatchAsync(challengeId, pollToken))
            throw new HubException("not_authorized");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"login:{challengeId}");
    }
}
