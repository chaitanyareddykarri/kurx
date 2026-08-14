using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Auth;

/// <summary>Authentication risk scoring (AM8, ADR-AM22). Reads the append-only <c>security_events</c> feed
/// the auth platform already writes, combines it with the platform fraud score, and returns a decision.
///
/// The weights below are a deliberate first cut, not a tuned model: they exist so the decision *path* is
/// real and enforced, and so tuning later is a number change rather than an architecture change.</summary>
// ponytail: fixed additive weights over a 1-hour window; swap for a trained/velocity model (Redis, ADR-AM14)
// if false-positive rate becomes a real complaint.
public class RiskEngine(KurxDbContext db, IFraudService fraud) : IRiskEngine
{
    // Tuned so no single signal locks a legitimate user out: a replayed refresh chain (50) or repeated bad
    // signatures (25) alone still allow, while any two independent signals together deny.
    private const int DenyThreshold = 70;

    public async Task<RiskDecision> EvaluateLoginAsync(Guid userId, string? ip, string? userAgent,
        CancellationToken ct = default)
    {
        var reasons = new List<string>();
        var score = 0;
        var since = DateTime.UtcNow.AddHours(-1);

        // A blacklisted or high-risk subject is refused outright — this is the platform-wide gate (M7).
        if (!await fraud.IsUserClearAsync(userId, ct))
        {
            reasons.Add("fraud_not_clear");
            score += 100;
        }

        var recent = await db.SecurityEvents.AsNoTracking()
            .Where(e => e.UserId == userId && e.CreatedAt >= since)
            .Select(e => e.Type)
            .ToListAsync(ct);

        // Someone is throwing signatures at this account's challenges.
        var badSignatures = recent.Count(t => t == "challenge.signature_invalid");
        if (badSignatures >= 3)
        {
            reasons.Add("repeated_invalid_signatures");
            score += 25 * Math.Min(badSignatures / 3, 3);
        }

        // A refresh-token chain was replayed within the hour: treat further sign-ins as suspect.
        if (recent.Contains("refresh.reuse_detected"))
        {
            reasons.Add("recent_token_reuse");
            score += 50;
        }

        // Proof-of-possession failures mean someone holds a refresh string they cannot sign for.
        if (recent.Count(t => t == "refresh.pop_failed") >= 2)
        {
            reasons.Add("repeated_pop_failures");
            score += 30;
        }

        // A recovery in the last hour means the account just changed hands; be stricter, not laxer.
        if (recent.Contains("recovery.redeemed"))
        {
            reasons.Add("recent_account_recovery");
            score += 20;
        }

        var action = score >= DenyThreshold ? RiskAction.Deny : RiskAction.Allow;

        if (action == RiskAction.Deny)
        {
            db.SecurityEvents.Add(new Domain.Entities.SecurityEvent
            {
                UserId = userId, Type = "risk.denied", Severity = "critical",
                ContextJson = $"{{\"score\":{score},\"reasons\":\"{string.Join(",", reasons)}\"}}",
            });
            await db.SaveChangesAsync(ct);
        }

        return new RiskDecision(action, score, reasons);
    }
}
