using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// How the person named on a certificate reaches it (D-355, Phase 10).
///
/// <para>Two security properties carry this phase, and both are asserted rather than assumed.</para>
///
/// <para><b>The token is never stored.</b> Only its hash is, so a dump of the table cannot be turned back
/// into working links — and the platform cannot show a token again once minted, which is the honest
/// consequence of that and the reason "regenerate" exists instead of "reveal".</para>
///
/// <para><b>Linking is by verified email only.</b> Matching on an unverified address would let anyone type
/// a stranger's email into their profile and collect that stranger's certificates. There is a test for
/// exactly that attack.</para>
/// </summary>
public class CertificateParticipantAccessTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateParticipantAccessTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private sealed record Fixture(Guid EventId, Guid OwnerId, Guid OutsiderId, Guid TemplateId);

    private readonly string _token = Guid.NewGuid().ToString("N")[..8];

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);
    private string Address(string local) => $"{local}@{_token}.example.com";

    private static byte[] Artwork()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(600, 424);
        using var output = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, output);
        return output.ToArray();
    }

    private ICertificateParticipantService Participants(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateParticipantService>();

    private async Task<Fixture> SeedAsync()
    {
        Guid eventId, ownerId, outsiderId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var org = new Organization { Name = "Org " + suffix, Slug = "org" + suffix };
            var owner = new User { Phone = Phone(), Name = "Creator " + suffix };
            var outsider = new User { Phone = Phone(), Name = "Outsider " + suffix };
            db.Organizations.Add(org);
            db.Users.AddRange(owner, outsider);

            var categoryId = await db.EventCategories.AsNoTracking()
                .Where(c => c.Level == CategoryLevel.Category).Select(c => c.Id).FirstAsync();

            var ev = new Event
            {
                Title = "Hackathon " + suffix,
                Slug = "hackathon-" + suffix,
                ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
                Description = "d", VenueName = "v",
                RepresentingOrgId = org.Id, CreatedBy = owner.Id, CategoryId = categoryId,
                StartsAt = DateTime.UtcNow.AddDays(30), EndsAt = DateTime.UtcNow.AddDays(31),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            (eventId, ownerId, outsiderId) = (ev.Id, owner.Id, outsider.Id);
        }

        using var s2 = _factory.Services.CreateScope();
        var templates = s2.ServiceProvider.GetRequiredService<ICertificateTemplateService>();

        var created = await templates.CreateAsync(ownerId, eventId,
            new CertificateTemplateInput("Participation", "a4-landscape"), false);
        var templateId = created.Value!.Id;

        var artwork = Artwork();
        var presigned = await templates.PresignBackgroundAsync(
            ownerId, templateId, "image/png", artwork.Length, false);
        await s2.ServiceProvider.GetRequiredService<IStorage>()
            .PutAsync(presigned.Value!.Key, artwork, "image/png");
        await templates.SetBackgroundAsync(ownerId, templateId,
            new CertificateBackgroundInput(presigned.Value.Key, "image/png", 3508, 2480), false);

        await templates.ReplaceFieldsAsync(ownerId, templateId, [
            new("dynamicfield", "participant_name", "Participant name", null, 10, 40, 80, 10,
                HorizontalAlignment: "center", FontSizePt: 32, Color: "#0F172A", IsRequired: true)
        ], false);

        return new Fixture(eventId, ownerId, outsiderId, templateId);
    }

    /// <summary>One certificate for an external participant: a name, maybe an address, and no account.</summary>
    private async Task<(Guid CertificateRowId, Guid RecipientId)> IssueAsync(
        IServiceScope scope, Fixture f, string name = "Rahul Sharma", string? email = null)
    {
        var issued = await scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>()
            .IssueAsync(f.OwnerId, f.TemplateId,
                new IssueCertificateInput(name, email, new Dictionary<string, string>()), false);
        Assert.True(issued.Ok, issued.Error);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificate = await db.IssuedCertificates.AsNoTracking()
            .FirstAsync(c => c.Id == issued.Value!.Id);
        return (certificate.Id, certificate.RecipientId);
    }

    // ── The token ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_capability_link_reaches_the_participants_certificate()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, recipientId) = await IssueAsync(scope, f);

        var link = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);
        Assert.True(link.Ok, link.Error);
        Assert.NotNull(link.Value!.Url);

        var resolved = await Participants(scope).ResolveAccessAsync(TokenFrom(link.Value.Url!));

        Assert.True(resolved.Ok, resolved.Error);
        Assert.Equal("Rahul Sharma", resolved.Value!.RecipientName);
        var certificate = Assert.Single(resolved.Value.Certificates);
        Assert.Equal("issued", certificate.Status);
        Assert.NotNull(certificate.DownloadPdfUrl);
        Assert.NotNull(certificate.VerificationUrl);
    }

    /// <summary>A dump of this table must not be turnable back into working links, for the same reason a
    /// password is not stored either.</summary>
    [Fact]
    public async Task The_raw_token_is_never_written_to_the_database()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, recipientId) = await IssueAsync(scope, f);

        var link = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);
        var token = TokenFrom(link.Value!.Url!);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var stored = await db.CertificateAccessLinks.AsNoTracking().FirstAsync(l => l.Id == link.Value.Id);

        Assert.DoesNotContain(token, stored.TokenHash, StringComparison.Ordinal);
        Assert.Equal(64, stored.TokenHash.Length);   // SHA-256, hex
    }

    /// <summary>The honest consequence of not storing it: once minted, a link can be replaced but never
    /// shown again.</summary>
    [Fact]
    public async Task A_minted_link_cannot_be_shown_again()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, recipientId) = await IssueAsync(scope, f);
        await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);

        var listed = await Participants(scope).ListAccessLinksAsync(f.OwnerId, recipientId, false);

        var only = Assert.Single(listed.Value!);
        Assert.Null(only.Url);
        Assert.False(only.Revoked);
    }

    [Fact]
    public async Task Two_links_for_one_recipient_are_different_tokens()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, recipientId) = await IssueAsync(scope, f);

        var first = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);
        var second = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);

        Assert.NotEqual(first.Value!.Url, second.Value!.Url);
        // Both work: minting a new one does not silently break the one already in someone's inbox.
        Assert.True((await Participants(scope).ResolveAccessAsync(TokenFrom(first.Value.Url!))).Ok);
        Assert.True((await Participants(scope).ResolveAccessAsync(TokenFrom(second.Value.Url!))).Ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    public async Task A_token_that_was_never_minted_resolves_to_nothing(string token)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Participants(scope).ResolveAccessAsync(token);

        Assert.False(result.Ok);
        Assert.Equal("not_found", result.Error);
    }

    /// <summary>A revoked link and a token that never existed give the same answer. Distinguishing them
    /// would confirm to whoever is guessing that they had found a real one.</summary>
    [Fact]
    public async Task A_revoked_link_is_indistinguishable_from_one_that_never_existed()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, recipientId) = await IssueAsync(scope, f);
        var link = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);
        var token = TokenFrom(link.Value!.Url!);

        await Participants(scope).RevokeAccessLinkAsync(f.OwnerId, link.Value.Id, false);

        var revoked = await Participants(scope).ResolveAccessAsync(token);
        var invented = await Participants(scope).ResolveAccessAsync("never-existed");

        Assert.Equal(invented.Error, revoked.Error);
        Assert.Equal("not_found", revoked.Error);
    }

    /// <summary>Revoking a link closes a door; it does not withdraw anything.</summary>
    [Fact]
    public async Task Revoking_a_link_leaves_the_certificate_alone()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (certificateRowId, recipientId) = await IssueAsync(scope, f);
        var link = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);

        await Participants(scope).RevokeAccessLinkAsync(f.OwnerId, link.Value!.Id, false);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificate = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == certificateRowId);
        Assert.Equal(IssuedCertificateStatus.Issued, certificate.Status);
    }

    [Fact]
    public async Task Revoking_a_link_twice_is_not_an_error()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, recipientId) = await IssueAsync(scope, f);
        var link = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);

        await Participants(scope).RevokeAccessLinkAsync(f.OwnerId, link.Value!.Id, false);
        var again = await Participants(scope).RevokeAccessLinkAsync(f.OwnerId, link.Value.Id, false);

        Assert.True(again.Ok);
    }

    [Fact]
    public async Task Using_a_link_records_that_it_was_used_and_nothing_else()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, recipientId) = await IssueAsync(scope, f);
        var link = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);

        await Participants(scope).ResolveAccessAsync(TokenFrom(link.Value!.Url!));

        var listed = await Participants(scope).ListAccessLinksAsync(f.OwnerId, recipientId, false);
        Assert.NotNull(Assert.Single(listed.Value!).LastAccessedAt);
    }

    [Fact]
    public async Task An_outsider_cannot_mint_or_revoke_a_link()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, recipientId) = await IssueAsync(scope, f);
        var mine = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);

        var minted = await Participants(scope).CreateAccessLinkAsync(f.OutsiderId, recipientId, false);
        var revoked = await Participants(scope).RevokeAccessLinkAsync(f.OutsiderId, mine.Value!.Id, false);
        var listed = await Participants(scope).ListAccessLinksAsync(f.OutsiderId, recipientId, false);

        // D-018: not-found rather than forbidden.
        Assert.Equal("not_found", minted.Error);
        Assert.Equal("not_found", revoked.Error);
        Assert.Equal("not_found", listed.Error);
    }

    // ── What a link shows ───────────────────────────────────────────────────────────────────────

    /// <summary>A withdrawn certificate is still listed — hiding it would leave the holder unable to find
    /// out what happened — but no fresh download is minted for a document the platform publicly calls
    /// invalid.</summary>
    [Fact]
    public async Task A_withdrawn_certificate_is_shown_with_its_reason_but_offers_no_download()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (certificateRowId, recipientId) = await IssueAsync(scope, f);
        var link = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);

        await scope.ServiceProvider.GetRequiredService<ICertificateRevocationService>()
            .RevokeAsync(f.OwnerId, certificateRowId, "Issued to the wrong person", false);

        var resolved = await Participants(scope).ResolveAccessAsync(TokenFrom(link.Value!.Url!));

        var certificate = Assert.Single(resolved.Value!.Certificates);
        Assert.Equal("revoked", certificate.Status);
        Assert.Equal("Issued to the wrong person", certificate.RevocationReason);
        Assert.Null(certificate.DownloadPdfUrl);
        Assert.Null(certificate.DownloadPngUrl);
        // Still verifiable, so they can see for themselves what happened.
        Assert.NotNull(certificate.VerificationUrl);
    }

    /// <summary>The reason the link belongs to the recipient rather than to one certificate: after a
    /// correction the same link keeps working and shows the corrected one.</summary>
    [Fact]
    public async Task After_a_correction_the_same_link_shows_the_replacement()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (certificateRowId, recipientId) = await IssueAsync(scope, f, "Rahul Sharme");
        var link = await Participants(scope).CreateAccessLinkAsync(f.OwnerId, recipientId, false);

        var reissued = await scope.ServiceProvider.GetRequiredService<ICertificateRevocationService>()
            .ReissueAsync(f.OwnerId, certificateRowId,
                new CertificateCorrection("Rahul Sharma", null, "Name was misspelled"), false);

        var resolved = await Participants(scope).ResolveAccessAsync(TokenFrom(link.Value!.Url!));

        Assert.Equal(2, resolved.Value!.Certificates.Count);
        var live = Assert.Single(resolved.Value.Certificates.Where(c => c.Status == "issued"));
        Assert.Equal(reissued.Value!.CertificateId, live.CertificateId);
        Assert.NotNull(live.DownloadPdfUrl);

        var old = Assert.Single(resolved.Value.Certificates.Where(c => c.Status == "superseded"));
        Assert.Equal(reissued.Value.CertificateId, old.ReplacedBy);
        Assert.Null(old.DownloadPdfUrl);
    }

    // ── Verified-email linking ──────────────────────────────────────────────────────────────────

    /// <summary>The attack this rule exists for: type a stranger's address into your profile and collect
    /// their certificates. An unverified address is a claim, not a proof.</summary>
    [Fact]
    public async Task An_unverified_email_claims_nothing()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await IssueAsync(scope, f, "Rahul Sharma", Address("rahul"));

        var impostor = new User { Phone = Phone(), Name = "Impostor", Email = Address("rahul") };
        db.Users.Add(impostor);
        await db.SaveChangesAsync();

        var linked = await Participants(scope).LinkByVerifiedEmailAsync(impostor.Id);
        var mine = await Participants(scope).ListMineAsync(impostor.Id);

        Assert.Equal(0, linked);
        Assert.Empty(mine.Value!.Certificates);
    }

    [Fact]
    public async Task A_verified_email_claims_the_certificates_issued_to_that_address()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await IssueAsync(scope, f, "Rahul Sharma", Address("verified"));

        var user = new User
        {
            Phone = Phone(), Name = "Rahul", Email = Address("verified"),
            EmailVerifiedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var linked = await Participants(scope).LinkByVerifiedEmailAsync(user.Id);

        Assert.Equal(1, linked);
        var mine = await Participants(scope).ListMineAsync(user.Id);
        var certificate = Assert.Single(mine.Value!.Certificates);
        Assert.NotNull(certificate.DownloadPdfUrl);
    }

    /// <summary>Matching is on the normalised address, so the organiser typing "Rahul@Example.com" and the
    /// account holding "rahul@example.com" are the same person rather than two.</summary>
    [Fact]
    public async Task Linking_ignores_case_and_surrounding_whitespace()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await IssueAsync(scope, f, "Priya Patel", "  " + Address("PRIYA").ToUpperInvariant() + " ");

        var user = new User
        {
            Phone = Phone(), Name = "Priya", Email = Address("priya"),
            EmailVerifiedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        Assert.Equal(1, await Participants(scope).LinkByVerifiedEmailAsync(user.Id));
    }

    /// <summary>Safe to call on every sign-in.</summary>
    [Fact]
    public async Task Linking_twice_claims_nothing_new()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await IssueAsync(scope, f, "Rahul Sharma", Address("twice"));

        var user = new User
        {
            Phone = Phone(), Name = "Rahul", Email = Address("twice"),
            EmailVerifiedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        Assert.Equal(1, await Participants(scope).LinkByVerifiedEmailAsync(user.Id));
        Assert.Equal(0, await Participants(scope).LinkByVerifiedEmailAsync(user.Id));
    }

    /// <summary>A recipient already claimed is never re-pointed.
    ///
    /// <para>The reachable path, since the database allows only one account per address: someone links,
    /// then changes their email, freeing the old address for another account to verify. The certificate
    /// must not follow the address — it belongs to the person who already claimed it.</para></summary>
    [Fact]
    public async Task A_recipient_already_linked_is_never_reassigned()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await IssueAsync(scope, f, "Rahul Sharma", Address("shared"));

        var first = new User
        {
            Phone = Phone(), Name = "First", Email = Address("shared"), EmailVerifiedAt = DateTime.UtcNow,
        };
        db.Users.Add(first);
        await db.SaveChangesAsync();
        Assert.Equal(1, await Participants(scope).LinkByVerifiedEmailAsync(first.Id));

        // First moves to a new address, freeing the old one. The certificate stays with them.
        var moved = await db.Users.FirstAsync(u => u.Id == first.Id);
        moved.Email = Address("moved");
        await db.SaveChangesAsync();

        var second = new User
        {
            Phone = Phone(), Name = "Second", Email = Address("shared"), EmailVerifiedAt = DateTime.UtcNow,
        };
        db.Users.Add(second);
        await db.SaveChangesAsync();

        Assert.Equal(0, await Participants(scope).LinkByVerifiedEmailAsync(second.Id));
        Assert.Empty((await Participants(scope).ListMineAsync(second.Id)).Value!.Certificates);
        // And the original holder still has it.
        Assert.Single((await Participants(scope).ListMineAsync(first.Id)).Value!.Certificates);
    }

    /// <summary>A certificate issued before someone signed up appears the moment they look, rather than
    /// after some later backfill they have no way to ask for.</summary>
    [Fact]
    public async Task Certificates_issued_before_signup_appear_on_first_look()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await IssueAsync(scope, f, "Arjun Kumar", Address("later"));

        var user = new User
        {
            Phone = Phone(), Name = "Arjun", Email = Address("later"), EmailVerifiedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // No explicit link call — ListMine does it.
        var mine = await Participants(scope).ListMineAsync(user.Id);

        Assert.Single(mine.Value!.Certificates);
    }

    [Fact]
    public async Task A_user_with_no_certificates_gets_an_empty_list_not_an_error()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var mine = await Participants(scope).ListMineAsync(f.OutsiderId);

        Assert.True(mine.Ok, mine.Error);
        Assert.Empty(mine.Value!.Certificates);
    }

    private static string TokenFrom(string url) => url[(url.LastIndexOf('/') + 1)..];
}
