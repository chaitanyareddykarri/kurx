using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api;

/// <summary>Reusable step-up enforcement for high-risk actions (Phase 2F, AM6/ADR-A7). Possession of a valid
/// access token is not authority to do damage: a sensitive action additionally requires a recent
/// device-signed step-up (a fresh proof of device possession).
///
/// <para>A user who <b>cannot</b> step up (no trusted device) is exempt — demanding a factor they cannot
/// produce would lock them out of the very features that protect them. This is the same rule recovery-code
/// minting already used; extracting it here lets every high-risk endpoint enforce it identically.</para>
///
/// <para>Usage: <c>if (await StepUpGuard.RequireAsync(userId, stepUp, ct) is { } denied) return denied;</c>
/// — a non-null result is the 403 to return, null means the action may proceed. Auth-owned high-risk
/// actions (e.g. recovery-code minting) call this today; platform actions (event creation, payouts,
/// ownership transfer) adopt the same call as they are wired in their own modules.</para></summary>
public static class StepUpGuard
{
    public static async Task<IResult?> RequireAsync(Guid userId, IStepUpService stepUp, CancellationToken ct = default)
    {
        if (await stepUp.CanStepUpAsync(userId, ct) && !(await stepUp.StatusAsync(userId, ct)).Satisfied)
            return ProblemResults.Problem("step_up_required", StatusCodes.Status403Forbidden);
        return null;
    }
}
