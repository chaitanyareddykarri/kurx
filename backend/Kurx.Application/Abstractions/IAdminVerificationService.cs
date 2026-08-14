namespace Kurx.Application.Abstractions;

public record ReviewLogEntry(Guid Id, string Decision, Guid? ReviewerId, string? ReasonCode, string? Notes, int? RiskScore, DateTime CreatedAt);
public record DocLogEntry(Guid Id, string DocType, string Status, DateTime CreatedAt);

/// <summary>Full audit trail for one verification subject (M12): every decision + evidence document,
/// across identity / organization / membership / event. The read side of the admin console.</summary>
public record VerificationHistory(string SubjectType, Guid SubjectId, IReadOnlyList<ReviewLogEntry> Reviews, IReadOnlyList<DocLogEntry> Documents);

/// <summary>Admin verification console read surface (M12, D-051). Gated by the VerificationReviewer
/// platform role at the endpoint. Writes (approve/reject/merge/blacklist) live in the subject services.</summary>
public interface IAdminVerificationService
{
    /// <summary>Returns null when <paramref name="subjectType"/> is not a known verification subject.</summary>
    Task<VerificationHistory?> GetHistoryAsync(string subjectType, Guid subjectId, CancellationToken ct = default);

    /// <summary>A short-lived presigned URL to view one evidence document (M12 / D-055 G2), presigned on
    /// open rather than in the list view. Null if the document doesn't exist.</summary>
    Task<string?> GetDocumentViewUrlAsync(Guid documentId, CancellationToken ct = default);
}
