namespace Kurx.Application.Abstractions;

/// <summary>Public view of a person's identity verification. Only masked last-4 values are exposed —
/// the full government-ID / PAN / account number is never stored or returned (M3, D-042).
///
/// <para><b>Per-component status is on the wire because it is the only honest answer.</b> The single
/// aggregate <c>Status</c> was written by every submission, so one failed bank check reported a person
/// with an approved government ID and PAN as "Rejected" overall, and a client could only guess a
/// component's state from whether its masked value happened to be present. Both problems are the same
/// bug: a component's state is a property of that component.</para></summary>
public record IdentityStatusView(
    string Level, string Status,
    string? GovtIdKind, string? GovtIdLast4, string? PanLast4, string? BankLast4,
    DateTime? ReviewedAt, DateTime? ExpiresAt, DateTime? UpdatedAt,
    // Per-component state. Defaulted so a caller constructing this record positionally still compiles.
    string GovtIdStatus = "NotStarted",
    string PanStatus = "NotStarted",
    string BankStatus = "NotStarted",
    /// <summary>Penny-drop outcome — the proof the account exists and accepts deposits, as opposed to
    /// an account number merely having been typed.</summary>
    string PennyDropStatus = "NotStarted",
    /// <summary>Whether the bank's registered holder name matched. The control that catches a PAN and
    /// a bank account belonging to two different people.</summary>
    string BankNameMatch = "NotChecked",
    DateTime? BankVerifiedAt = null);

/// <summary>One entry in a person's verification history — an append-only record of every decision
/// made about their identity, newest first.
///
/// <para>Sourced from <c>verification_reviews</c>, which has recorded every automated and human
/// decision since M3 and simply had no read path. Nothing new is written to produce this; the trail
/// already existed and was invisible to the person it was about.</para></summary>
public record IdentityHistoryEntry(
    /// <summary><c>government_id</c> | <c>pan</c> | <c>bank</c>, parsed from the reason code.</summary>
    string Component,
    /// <summary><c>approved</c> | <c>rejected</c>.</summary>
    string Decision,
    /// <summary>Null for an automated/provider decision, set when a human reviewed it.</summary>
    Guid? ReviewerId,
    string? ReasonCode,
    /// <summary>Reviewer note. Admin-authored and organiser-facing; never provider internals.</summary>
    string? Notes,
    DateTime CreatedAt);

/// <summary>Person identity verification / KYC (M3, D-042). Distinct from organization bank
/// verification (M9). Provider calls go through <c>IKycProvider</c> (mock in dev, like org KYC);
/// the production DigiLocker/PAN/penny-drop adapter is a gated integration task.</summary>
public interface IIdentityVerificationService
{
    Task<IdentityStatusView> GetStatusAsync(Guid userId, CancellationToken ct = default);
    Task<ServiceResult<IdentityStatusView>> SubmitGovernmentIdAsync(Guid userId, string kind, string idNumber, string name, CancellationToken ct = default);
    Task<ServiceResult<IdentityStatusView>> SubmitPanAsync(Guid userId, string pan, string name, CancellationToken ct = default);
    Task<ServiceResult<IdentityStatusView>> SubmitBankAsync(Guid userId, string accountNumber, string ifsc, string holderName, CancellationToken ct = default);

    /// <summary>The caller's own verification history, newest first. Caller-scoped and never exposed
    /// publicly: a rejection is a private fact about a person's financial identity.</summary>
    Task<IReadOnlyList<IdentityHistoryEntry>> GetHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default);
}
