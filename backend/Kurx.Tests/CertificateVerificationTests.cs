using Kurx.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Certificate signing and public verification (D-355, Phase 5).
///
/// <para>Four properties carry the weight, and each corresponds to a way this surface can lie:</para>
/// <list type="bullet">
/// <item><b>A tampered row must not verify.</b> That is the entire point of signing when verification is
/// online — the record is the evidence, so the signature guards the record.</item>
/// <item><b>An infrastructure failure must never read as "not valid".</b> This page tells someone whether
/// a credential is real; answering "no" because of a timeout is the worst possible failure.</item>
/// <item><b>A compromised key must not condemn a genuine certificate.</b> It really was issued; the page
/// says so, with a warning beside it.</item>
/// <item><b>The public payload must not leak.</b> Anyone with an id can reach this, so it carries what the
/// certificate already prints and nothing else.</item>
/// </list>
/// </summary>
public class CertificateVerificationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateVerificationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private sealed record Fixture(Guid EventId, Guid OwnerId, Guid TemplateId);

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

    private static byte[] Artwork()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(400, 283);
        using var output = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, output);
        return output.ToArray();
    }

    private async Task<Fixture> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var org = new Organization { Name = "Org " + suffix, Slug = "org" + suffix };
        var owner = new User { Phone = Phone(), Name = "Priya Patel" };
        db.Organizations.Add(org);
        db.Users.Add(owner);

        var categoryId = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Category).Select(c => c.Id).FirstAsync();

        var ev = new Event
        {
            Title = "Sample Hackathon 2026",
            Slug = $"hack-{suffix}",
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d", VenueName = "Hyderabad Innovation Campus", City = "Hyderabad",
            RepresentingOrgId = org.Id, CreatedBy = owner.Id, CategoryId = categoryId,
            StartsAt = new DateTime(2026, 9, 12, 9, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var templates = scope.ServiceProvider.GetRequiredService<ICertificateTemplateService>();
        var created = await templates.CreateAsync(owner.Id, ev.Id, new CertificateTemplateInput("Design"), false);
        var presigned = await templates.PresignBackgroundAsync(owner.Id, created.Value!.Id, "image/png", 4096, false);
        await scope.ServiceProvider.GetRequiredService<IStorage>().PutAsync(presigned.Value!.Key, Artwork(), "image/png");
        await templates.SetBackgroundAsync(owner.Id, created.Value.Id,
            new CertificateBackgroundInput(presigned.Value.Key, "image/png", 400, 283), false);
        await templates.ReplaceFieldsAsync(owner.Id, created.Value.Id, [
            new CertificateFieldInput("dynamicfield", "participant_name", "Name", null, 10, 40, 80, 10,
                FontSizePt: 30, HorizontalAlignment: "center"),
            new CertificateFieldInput("qrcode", null, "Verify", null, 82, 70, 13, 18),
        ], false);

        return new Fixture(ev.Id, owner.Id, created.Value.Id);
    }

    private async Task<IssuedCertificateView> IssueAsync(IServiceScope scope, Fixture f, string name = "Rahul Sharma")
    {
        var issued = await scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>()
            .IssueAsync(f.OwnerId, f.TemplateId,
                new IssueCertificateInput(name, "rahul@example.com",
                    new Dictionary<string, string> { ["achievement"] = "First Place" }), false);
        Assert.True(issued.Ok, issued.Error);
        return issued.Value!;
    }

    private ICertificateVerificationService Verifier(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>();

    // ── The canonical payload ───────────────────────────────────────────────────────────────────

    /// <summary>Determinism is the whole requirement: the payload is rebuilt from the stored row at every
    /// verification, so any variation makes every existing certificate fail — which looks exactly like
    /// tampering.</summary>
    [Fact]
    public void The_canonical_payload_is_deterministic_regardless_of_dictionary_order()
    {
        var id = Guid.NewGuid();
        var at = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

        var a = CertificateCanonicalPayload.Build("C-1", id, id, 1, at,
            new Dictionary<string, string> { ["b"] = "2", ["a"] = "1", ["c"] = "3" });
        var b = CertificateCanonicalPayload.Build("C-1", id, id, 1, at,
            new Dictionary<string, string> { ["c"] = "3", ["a"] = "1", ["b"] = "2" });

        Assert.Equal(a, b);
    }

    /// <summary>The payload must survive being stored, which is the only form verification ever sees it in.
    ///
    /// <para>The defect this exists to prevent reported genuine certificates as TAMPERED. Signing happens
    /// over a timestamp in memory; verification rebuilds from the written row, and PostgreSQL
    /// <c>timestamptz</c> keeps microseconds while .NET ticks are 100ns. The dropped digits changed the
    /// payload, the signature failed, and the holder of a real document was told it was forged.</para>
    ///
    /// <para>It was invisible on macOS, whose clock is already microsecond-granular — the suite passed
    /// here and failed in CI, where ~9 in 10 timestamps carry sub-microsecond ticks, as every deployed
    /// Linux container does. So this asserts the property directly rather than relying on the platform
    /// clock to produce a value that exposes it.</para></summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(9)]
    public void The_canonical_payload_ignores_precision_the_database_cannot_keep(int extraTicks)
    {
        var id = Guid.NewGuid();
        var values = new Dictionary<string, string> { ["participant_name"] = "Rahul Sharma" };

        var whole = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
        var finer = whole.AddTicks(extraTicks);   // what was signed, before the row truncated it

        Assert.Equal(
            CertificateCanonicalPayload.Build("C-1", id, id, 1, whole, values),
            CertificateCanonicalPayload.Build("C-1", id, id, 1, finer, values));
    }

    /// <summary>A microsecond is still a difference, though — truncating must not blunt the timestamp into
    /// something a real re-issue could collide with.</summary>
    [Fact]
    public void The_canonical_payload_still_distinguishes_a_microsecond()
    {
        var id = Guid.NewGuid();
        var values = new Dictionary<string, string> { ["participant_name"] = "Rahul Sharma" };
        var at = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

        Assert.NotEqual(
            CertificateCanonicalPayload.Build("C-1", id, id, 1, at, values),
            CertificateCanonicalPayload.Build("C-1", id, id, 1, at.AddTicks(TimeSpan.TicksPerMicrosecond), values));
    }

    [Fact]
    public void The_canonical_payload_changes_when_anything_it_covers_changes()
    {
        var id = Guid.NewGuid();
        var at = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
        var values = new Dictionary<string, string> { ["participant_name"] = "Rahul Sharma" };
        var baseline = CertificateCanonicalPayload.Build("C-1", id, id, 1, at, values);

        Assert.NotEqual(baseline, CertificateCanonicalPayload.Build("C-2", id, id, 1, at, values));
        Assert.NotEqual(baseline, CertificateCanonicalPayload.Build("C-1", Guid.NewGuid(), id, 1, at, values));
        Assert.NotEqual(baseline, CertificateCanonicalPayload.Build("C-1", id, id, 2, at, values));
        Assert.NotEqual(baseline, CertificateCanonicalPayload.Build("C-1", id, id, 1, at.AddSeconds(1), values));
        Assert.NotEqual(baseline, CertificateCanonicalPayload.Build("C-1", id, id, 1, at,
            new Dictionary<string, string> { ["participant_name"] = "Priya Patel" }));
    }

    /// <summary>Two different value sets must not serialise to the same string, or a certificate could be
    /// altered without changing what was signed.</summary>
    [Fact]
    public void Values_cannot_be_confused_by_concatenation()
    {
        var id = Guid.NewGuid();
        var at = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

        Assert.NotEqual(
            CertificateCanonicalPayload.Build("C", id, id, 1, at,
                new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }),
            CertificateCanonicalPayload.Build("C", id, id, 1, at,
                new Dictionary<string, string> { ["a"] = "1b2" }));
    }

    // ── Signing ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_signature_verifies_against_its_own_payload_and_not_another()
    {
        using var scope = _factory.Services.CreateScope();
        var signer = scope.ServiceProvider.GetRequiredService<ICertificateSigner>();

        var signature = await signer.SignAsync("payload-one");

        Assert.True((await signer.VerifyAsync(signature.KeyId, "payload-one", signature.Signature)).Matches);
        Assert.False((await signer.VerifyAsync(signature.KeyId, "payload-two", signature.Signature)).Matches);
    }

    /// <summary>An unknown key is not a mismatch. We cannot judge the certificate, and saying "does not
    /// match" would condemn a possibly-genuine document on the strength of our own key management.</summary>
    [Fact]
    public async Task An_unknown_key_is_reported_as_unknown_rather_than_as_a_mismatch()
    {
        using var scope = _factory.Services.CreateScope();

        var verdict = await scope.ServiceProvider.GetRequiredService<ICertificateSigner>()
            .VerifyAsync("cert-does-not-exist", "payload", Convert.ToBase64String(new byte[64]));

        Assert.False(verdict.KeyKnown);
        Assert.False(verdict.Matches);
    }

    [Fact]
    public async Task A_malformed_signature_does_not_throw()
    {
        using var scope = _factory.Services.CreateScope();
        var signer = scope.ServiceProvider.GetRequiredService<ICertificateSigner>();
        var signature = await signer.SignAsync("payload");

        var verdict = await signer.VerifyAsync(signature.KeyId, "payload", "not-base64!!");

        Assert.True(verdict.KeyKnown);
        Assert.False(verdict.Matches);
    }

    /// <summary>The private half is wrapped by the platform's existing protector, and the public half is
    /// never nulled — that retention is what keeps a certificate verifiable for years.</summary>
    [Fact]
    public async Task Signing_keys_are_protected_at_rest_and_retain_their_public_half()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICertificateSigner>().SignAsync("payload");

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var key = await db.CertificateSigningKeys.AsNoTracking().FirstAsync();

        Assert.NotNull(key.PublicKeySpki);
        Assert.NotNull(key.ProtectedPrivateKey);
        Assert.NotNull(key.ProtectionScheme);
        Assert.Equal("ES256", key.Algorithm);
    }

    // ── Verification outcomes ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_issued_certificate_verifies_as_valid_with_its_public_details()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var issued = await IssueAsync(scope, f);

        var result = await Verifier(scope).VerifyAsync(issued.CertificateId);

        Assert.Equal(CertificateVerificationOutcome.Valid, result.Outcome);
        Assert.Equal(issued.CertificateId, result.CertificateId);
        Assert.Equal("Rahul Sharma", result.Details!["participant_name"]);
        Assert.Equal("Sample Hackathon 2026", result.Details["event_name"]);
        Assert.Equal("First Place", result.Details["achievement"]);
        Assert.False(result.SigningKeyCompromised);
        // A verifier can compare what they hold against what was issued.
        Assert.NotNull(result.PreviewUrl);
    }

    /// <summary>The payload the caller supplies plays no part — verification rebuilds it from the stored
    /// row, which is what makes the signature a check on the record.</summary>
    [Fact]
    public async Task An_altered_record_does_not_verify()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var issued = await IssueAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Someone edits the database directly — the exact attack online verification is otherwise blind to.
        var row = await db.IssuedCertificates.FirstAsync(c => c.Id == issued.Id);
        row.FieldValuesJson = row.FieldValuesJson.Replace("Rahul Sharma", "Someone Else", StringComparison.Ordinal);
        await db.SaveChangesAsync();

        var result = await Verifier(scope).VerifyAsync(issued.CertificateId);

        Assert.Equal(CertificateVerificationOutcome.Tampered, result.Outcome);
        // Nothing about the altered contents is echoed back as though it were genuine.
        Assert.Null(result.Details);
    }

    [Fact]
    public async Task An_unknown_id_is_not_found_and_says_nothing_else()
    {
        using var scope = _factory.Services.CreateScope();

        foreach (var id in new[] { "NOPE-2026-00001", "", "   ", new string('x', 200) })
        {
            var result = await Verifier(scope).VerifyAsync(id);
            Assert.Equal(CertificateVerificationOutcome.NotFound, result.Outcome);
            Assert.Null(result.Details);
            Assert.Null(result.PreviewUrl);
        }
    }

    /// <summary>A compromised key does not make a genuine certificate fake. It really was issued by the
    /// platform; the page reports it valid and shows the warning beside it.</summary>
    [Fact]
    public async Task A_compromised_signing_key_still_verifies_valid_with_a_warning()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var issued = await IssueAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var row = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == issued.Id);
        var key = await db.CertificateSigningKeys.FirstAsync(k => k.KeyId == row.SignatureKeyId);
        key.State = CertificateSigningKeyState.Compromised;
        key.CompromisedAt = DateTime.UtcNow;
        key.CompromisedReason = "Key material exposed in an incident";
        await db.SaveChangesAsync();

        var result = await Verifier(scope).VerifyAsync(issued.CertificateId);

        Assert.Equal(CertificateVerificationOutcome.Valid, result.Outcome);
        Assert.True(result.SigningKeyCompromised);
        Assert.Equal("Rahul Sharma", result.Details!["participant_name"]);
    }

    /// <summary>A retired key must keep verifying — its public half is retained precisely so certificates
    /// signed years ago still check out.</summary>
    [Fact]
    public async Task A_retired_signing_key_still_verifies()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var issued = await IssueAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var row = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == issued.Id);
        var key = await db.CertificateSigningKeys.FirstAsync(k => k.KeyId == row.SignatureKeyId);
        key.State = CertificateSigningKeyState.Retired;
        key.RetiredAt = DateTime.UtcNow;
        key.ProtectedPrivateKey = null;   // discarded — it can no longer sign
        await db.SaveChangesAsync();

        var result = await Verifier(scope).VerifyAsync(issued.CertificateId);

        Assert.Equal(CertificateVerificationOutcome.Valid, result.Outcome);
        Assert.False(result.SigningKeyCompromised);
    }

    /// <summary>A revoked certificate still returns its details. Someone checking one needs to learn it
    /// existed and what happened to it — "not found" would imply it was never real.</summary>
    [Fact]
    public async Task A_revoked_certificate_verifies_as_revoked_with_its_reason()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var issued = await IssueAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var row = await db.IssuedCertificates.FirstAsync(c => c.Id == issued.Id);
        row.Status = IssuedCertificateStatus.Revoked;
        db.CertificateRevocations.Add(new CertificateRevocation
        {
            CertificateId = row.Id,
            Reason = "Issued to the wrong person",
            RevokedByUserId = f.OwnerId,
        });
        await db.SaveChangesAsync();

        var result = await Verifier(scope).VerifyAsync(issued.CertificateId);

        Assert.Equal(CertificateVerificationOutcome.Revoked, result.Outcome);
        Assert.Equal("Issued to the wrong person", result.RevocationReason);
        Assert.NotNull(result.RevokedAt);
        Assert.Equal("Rahul Sharma", result.Details!["participant_name"]);
    }

    /// <summary>The single most important guarantee on this surface. A certificate whose signing key row
    /// has vanished cannot be judged — and must not be reported as invalid, because that tells someone a
    /// real credential is forged on the strength of our own failure.</summary>
    [Fact]
    public async Task An_unverifiable_certificate_is_unavailable_never_invalid()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var issued = await IssueAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var row = await db.IssuedCertificates.FirstAsync(c => c.Id == issued.Id);
        row.SignatureKeyId = "cert-vanished-key";
        await db.SaveChangesAsync();

        var result = await Verifier(scope).VerifyAsync(issued.CertificateId);

        Assert.Equal(CertificateVerificationOutcome.Unavailable, result.Outcome);
        Assert.NotEqual(CertificateVerificationOutcome.Tampered, result.Outcome);
        Assert.NotEqual(CertificateVerificationOutcome.NotFound, result.Outcome);
    }

    // ── Disclosure ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Anyone with an id can reach this. It must carry what the certificate already prints and
    /// nothing else — a spreadsheet may have held an employee number or a personal address that the design
    /// never showed, and an anonymous endpoint must not become a way to read it back.</summary>
    [Fact]
    public async Task Verification_discloses_only_what_the_certificate_shows()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var issued = await scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>()
            .IssueAsync(f.OwnerId, f.TemplateId, new IssueCertificateInput(
                "Rahul Sharma", "private@example.com",
                new Dictionary<string, string>
                {
                    ["achievement"] = "First Place",
                    ["employee_id"] = "EMP-SECRET-9931",
                    ["home_address"] = "12 Private Lane",
                }), false);

        var result = await Verifier(scope).VerifyAsync(issued.Value!.CertificateId);

        Assert.Equal(CertificateVerificationOutcome.Valid, result.Outcome);
        Assert.DoesNotContain("employee_id", result.Details!.Keys);
        Assert.DoesNotContain("home_address", result.Details.Keys);
        // The recipient's email is never in the public payload under any key.
        Assert.DoesNotContain(result.Details.Values, v => v.Contains("private@example.com", StringComparison.Ordinal));
    }

    // ── Links ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>The QR is printed permanently onto a certificate and cannot be corrected afterwards, so
    /// the origin has to be configuration rather than a constant.</summary>
    [Fact]
    public void The_verification_url_uses_the_configured_public_origin()
    {
        var links = new Kurx.Infrastructure.Certificates.CertificateVerificationLinks(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["CERTIFICATE_VERIFICATION_BASE_URL"] = "https://certificates.example.edu/",
                })
                .Build());

        Assert.Equal("https://certificates.example.edu/verify/ABC-2026-00001",
            links.VerificationUrl("ABC-2026-00001"));
    }

    [Fact]
    public async Task An_issued_certificate_records_which_key_signed_it()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var issued = await IssueAsync(scope, f);

        var row = await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
            .IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == issued.Id);

        // Recorded per certificate, because keys rotate and a certificate must stay verifiable against
        // the key that actually signed it rather than whichever is current.
        Assert.NotNull(row.SignatureKeyId);
        Assert.NotNull(row.Signature);
        Assert.StartsWith("cert-", row.SignatureKeyId, StringComparison.Ordinal);
    }
}
