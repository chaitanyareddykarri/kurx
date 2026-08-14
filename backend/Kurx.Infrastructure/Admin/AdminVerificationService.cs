using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Admin;

/// <summary>Reads the verification audit trail (reviews + documents) for one subject across every
/// verification type (M12, D-051), from the M0 substrate tables. Storage keys are intentionally not
/// returned in the list view — the console fetches a presigned URL per document when opening it.</summary>
public class AdminVerificationService(KurxDbContext db, IStorage storage) : IAdminVerificationService
{
    public async Task<VerificationHistory?> GetHistoryAsync(string subjectType, Guid subjectId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<VerificationSubjectType>(subjectType, ignoreCase: true, out var subject))
            return null;

        var reviews = await db.VerificationReviews.AsNoTracking()
            .Where(r => r.SubjectType == subject && r.SubjectId == subjectId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewLogEntry(r.Id, r.Decision.ToString(), r.ReviewerId, r.ReasonCode, r.Notes, r.RiskScore, r.CreatedAt))
            .ToListAsync(ct);

        var docs = await db.VerificationDocuments.AsNoTracking()
            .Where(d => d.SubjectType == subject && d.SubjectId == subjectId)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new DocLogEntry(d.Id, d.DocType, d.Status.ToString(), d.CreatedAt))
            .ToListAsync(ct);

        return new VerificationHistory(subject.ToString(), subjectId, reviews, docs);
    }

    public async Task<string?> GetDocumentViewUrlAsync(Guid documentId, CancellationToken ct = default)
    {
        var key = await db.VerificationDocuments.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => d.StorageKey)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(key)) return null;
        return await storage.PresignGetAsync(key, TimeSpan.FromMinutes(10), ct);
    }
}
