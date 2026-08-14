using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Schema smoke test for the M0 verification substrate. Proves verification_documents and
/// verification_reviews were created against real kurx_test and round-trip correctly — including the
/// polymorphic subject (no FK), the jsonb payload, the enum-as-text mapping, and a null (system)
/// reviewer. Later modules (M3/M5/M6/M12) build their evidence + review flows on these two tables.</summary>
public class VerificationSubstrateTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public VerificationSubstrateTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    [Fact]
    public async Task Document_and_review_round_trip_with_polymorphic_subject()
    {
        var subjectId = Guid.NewGuid();   // stands in for an organization id — no FK, polymorphic
        Guid docId, reviewId, uploaderId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var uploader = new User { Phone = "919000000101", Name = "Uploader" };
            db.Users.Add(uploader);

            db.VerificationDocuments.Add(new VerificationDocument
            {
                SubjectType = VerificationSubjectType.Organization,
                SubjectId = subjectId,
                DocType = "registration_cert",
                StorageKey = "private/verif/doc1.pdf",
                Sha256 = "abc123",
                ExtractedJson = "{\"reg_no\":\"NGO/2020/123\"}",
                UploadedBy = uploader.Id,
            });
            var doc = db.ChangeTracker.Entries<VerificationDocument>().Single().Entity;

            db.VerificationReviews.Add(new VerificationReview
            {
                SubjectType = VerificationSubjectType.Organization,
                SubjectId = subjectId,
                Decision = VerificationDecision.RequestChanges,
                ReviewerId = null,                        // system / automated decision
                ReasonCode = "doc_illegible",
                RiskScore = 42,
            });
            var review = db.ChangeTracker.Entries<VerificationReview>().Single().Entity;

            await db.SaveChangesAsync();
            docId = doc.Id; reviewId = review.Id; uploaderId = uploader.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var doc = await db.VerificationDocuments.AsNoTracking().SingleAsync(d => d.Id == docId);
            Assert.Equal(VerificationSubjectType.Organization, doc.SubjectType);
            Assert.Equal(subjectId, doc.SubjectId);
            Assert.Equal("registration_cert", doc.DocType);
            Assert.Equal(VerificationDocumentStatus.Pending, doc.Status);   // default persisted
            Assert.Contains("reg_no", doc.ExtractedJson);
            Assert.Equal(uploaderId, doc.UploadedBy);

            var review = await db.VerificationReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
            Assert.Equal(VerificationDecision.RequestChanges, review.Decision);
            Assert.Null(review.ReviewerId);
            Assert.Equal("doc_illegible", review.ReasonCode);
            Assert.Equal(42, review.RiskScore);

            // Enums are stored as text (global convention) — assert against the raw column value.
            var statusText = await db.Database
                .SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM verification_documents WHERE \"Id\" = {docId}")
                .SingleAsync();
            Assert.Equal("Pending", statusText);
        }
    }
}
