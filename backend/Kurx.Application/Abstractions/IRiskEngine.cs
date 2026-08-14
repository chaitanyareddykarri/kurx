namespace Kurx.Application.Abstractions;

/// <summary>What the risk engine wants done about an authentication attempt. Deliberately two-valued: a
/// device-approval login already <i>is</i> strong authentication (a fresh signature from a trusted device),
/// so there is no weaker-then-stronger ladder to climb at this point — the attempt is either allowed or it
/// is not. A risk-driven step-up tier gets added when a caller exists that can act on it.</summary>
public enum RiskAction { Allow, Deny }

public record RiskDecision(RiskAction Action, int Score, IReadOnlyList<string> Reasons)
{
    public static RiskDecision Allowed => new(RiskAction.Allow, 0, []);
}

/// <summary>Authentication risk scoring (AM8, ADR-AM22). Deliberately layered <b>on top of</b>
/// <see cref="IFraudService"/> rather than beside it: blacklists and the aggregate subject risk score
/// already live there, so this adds only the auth-specific signals (recent failed signatures, reuse
/// detections, device churn) and turns the combination into a decision.</summary>
public interface IRiskEngine
{
    /// <summary>Scores a sign-in attempt before any challenge is issued.</summary>
    Task<RiskDecision> EvaluateLoginAsync(Guid userId, string? ip, string? userAgent, CancellationToken ct = default);
}
