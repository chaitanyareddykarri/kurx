using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>D-331. The card's value rests on two claims: that the issuer was entitled to assert the
/// identifiers, and that the holder could not have written them. Both are tested here, along with the
/// privacy boundary of the anonymous verification page — the one route that answers to nobody.</summary>
public class IdCardTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public IdCardTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private sealed record Fixture(Guid EventId, Guid CreatorId, Guid HolderId, Guid OutsiderId);

    /// <summary>An event with a verified creator and a participating holder — the state in which
    /// issuing is legitimate (D-335). Each test degrades exactly one condition.</summary>
    private async Task<Fixture> SeedAsync(bool creatorVerified = true, bool holderParticipates = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var org = new Organization { Name = "Org " + suffix, Slug = "org" + suffix };
        var creator = new User { Phone = Phone(), Name = "Creator " + suffix };
        var holder = new User { Phone = Phone(), Name = "Holder " + suffix };
        var outsider = new User { Phone = Phone(), Name = "Outsider " + suffix };
        db.Organizations.Add(org);
        db.Users.AddRange(creator, holder, outsider);

        var ev = new Event
        {
            Title = "Event " + suffix,
            Slug = "event-" + suffix,
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d",
            VenueName = "v",
            RepresentingOrgId = org.Id,
            CreatedBy = creator.Id,
            CategoryId = await db.EventCategories.AsNoTracking()
                .Where(c => c.Level == CategoryLevel.Category).Select(c => c.Id).FirstAsync(),
            StartsAt = DateTime.UtcNow.AddDays(30),
            EndsAt = DateTime.UtcNow.AddDays(31),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);
        db.Memberships.Add(new Membership { OrgId = org.Id, UserId = creator.Id, Role = OrgRole.Owner, IsVerified = true });

        // The holder participates by holding a seat on the event's org; the outsider holds nothing.
        if (holderParticipates)
            db.Memberships.Add(new Membership { OrgId = org.Id, UserId = holder.Id, Role = OrgRole.Staff, IsVerified = true });

        // The creator's verification is satisfied the way production satisfies it, not by a flag: the
        // suite runs with IDENTITY_VERIFICATION_BYPASS unset (D-323), so CanCreatePublicEvent is only
        // true when the identity row actually carries the approved proofs TrustService reads —
        // govt-ID/PAN for identityVerified, PAN for panVerified, and bank + a passed penny drop with a
        // non-mismatched name for bankVerified.
        if (creatorVerified) db.UserIdentities.Add(VerifiedIdentity(creator.Id));

        await db.SaveChangesAsync();
        return new Fixture(ev.Id, creator.Id, holder.Id, outsider.Id);
    }

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

    /// <summary>The exact combination TrustService reads for CanCreatePublicEvent. Written out rather
    /// than hidden behind a flag so a change to the trust rules fails here loudly instead of silently
    /// making every issuance test pass for the wrong reason.</summary>
    private static UserIdentity VerifiedIdentity(Guid userId) => new()
    {
        UserId = userId,
        Level = IdentityLevel.Bank,
        Status = IdentityStatus.Approved,
        GovtIdStatus = IdentityStatus.Approved,
        PanStatus = IdentityStatus.Approved,
        BankStatus = IdentityStatus.Approved,
        PennyDropStatus = PennyDropStatus.Passed,
        BankNameMatch = NameMatchStatus.Match,
    };

    private IIdCardService Service(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IIdCardService>();

    private static IdCardIssueInput Input(Guid holderId, Guid eventId) => new(
        UserId: holderId, StudentId: "CS2024001", Department: "Computer Science",
        Course: "B.Tech", Year: "3", ValidFrom: new DateOnly(2026, 1, 1),
        ValidUntil: new DateOnly(2028, 12, 31), EventId: eventId, Template: "StandardCollege");

    // ── issuance authority (D-335) ───────────────────────────────────────────────────────────────

    /// <summary>The happy path: the event's creator issues to a participant.</summary>
    [Fact]
    public async Task A_verified_event_creator_issues_to_a_participant()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var r = await Service(scope).IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false);

        Assert.True(r.Ok, r.Error);
        Assert.Equal("CS2024001", r.Value!.StudentId);
        Assert.Equal("draft", r.Value.Status);
    }

    /// <summary>Nobody can mint their own proof — a document you issued to yourself evidences only
    /// that you can operate a form.</summary>
    [Fact]
    public async Task Nobody_can_issue_a_card_to_themselves()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var r = await Service(scope).IssueAsync(f.CreatorId, f.EventId, Input(f.CreatorId, f.EventId), isAdmin: false);

        Assert.False(r.Ok);
        Assert.Equal("cannot_issue_to_self", r.Error);
    }

    /// <summary>Controlling the event is not enough — the creator has to be someone the platform has
    /// actually checked. The fixture withholds the approved proofs rather than flipping a flag, so this
    /// exercises the same capability path production reads.</summary>
    [Fact]
    public async Task An_unverified_event_creator_cannot_issue()
    {
        var f = await SeedAsync(creatorVerified: false);
        using var scope = _factory.Services.CreateScope();

        var r = await Service(scope).IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false);

        Assert.False(r.Ok);
        Assert.Equal("issuer_not_verified", r.Error);
    }

    /// <summary>A participant is not an issuer. Holding a seat on the event's organization — which is
    /// what D-331 wrongly accepted as authority — does not confer issuance.</summary>
    [Fact]
    public async Task A_participant_cannot_issue_cards_for_the_event()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var r = await Service(scope).IssueAsync(f.HolderId, f.EventId, Input(f.OutsiderId, f.EventId), isAdmin: false);

        Assert.False(r.Ok);
        Assert.Equal("not_event_organizer", r.Error);
    }

    /// <summary>Someone with no standing in the event at all.</summary>
    [Fact]
    public async Task A_stranger_cannot_issue_cards_for_the_event()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var r = await Service(scope).IssueAsync(f.OutsiderId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false);

        Assert.False(r.Ok);
        Assert.Equal("not_event_organizer", r.Error);
    }

    /// <summary>The recipient has to belong to the event. Without this the card would assert a
    /// participation that never happened, which is the one claim it exists to make.</summary>
    [Fact]
    public async Task A_non_participant_cannot_be_issued_a_card()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var r = await Service(scope).IssueAsync(f.CreatorId, f.EventId, Input(f.OutsiderId, f.EventId), isAdmin: false);

        Assert.False(r.Ok);
        Assert.Equal("holder_not_a_participant", r.Error);
    }

    /// <summary>Organization verification is no longer consulted at all (D-335). The seeded org is
    /// deliberately left Unverified and issuance still succeeds — the inverse of D-331's rule.</summary>
    [Fact]
    public async Task Organization_verification_is_not_required_to_issue()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var card = await Service(scope).IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false);
        Assert.True(card.Ok, card.Error);

        var org = await db.Organizations.AsNoTracking()
            .FirstAsync(o => o.Id == db.Events.AsNoTracking().First(e => e.Id == f.EventId).RepresentingOrgId);
        Assert.NotEqual(OrgVerificationStatus.Verified, org.VerificationStatus);
    }

    // ── the protected identifier ─────────────────────────────────────────────────────────────────

    /// <summary>The security requirement, stated as a test: a holder edit must not move an asserted
    /// identifier. The DTO makes it unexpressible; this proves the service agrees.</summary>
    [Fact]
    public async Task A_holder_edit_cannot_change_the_asserted_identifiers()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);

        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false)).Value!;

        var edited = await svc.UpdateHolderFieldsAsync(f.HolderId, issued.Id, new IdCardHolderInput(
            Template: "ModernCollege", PhotoKey: null, SignatureKey: null, LayoutJson: null,
            BloodGroup: "O+", Address: "1 Test Road", EmergencyContactName: "Next Of Kin",
            EmergencyContactPhone: "9000000000"));

        Assert.True(edited.Ok, edited.Error);
        // Presentation and holder-supplied fields moved...
        Assert.Equal("ModernCollege", edited.Value!.Template);
        // ...while every asserted field is exactly as the issuer left it.
        Assert.Equal("CS2024001", edited.Value.StudentId);
        Assert.Equal("Computer Science", edited.Value.Department);
        Assert.Equal("B.Tech", edited.Value.Course);
        Assert.Equal("3", edited.Value.Year);
        Assert.Equal(new DateOnly(2028, 12, 31), edited.Value.ValidUntil);
    }

    /// <summary>A stranger cannot edit someone else's card.</summary>
    [Fact]
    public async Task A_non_holder_cannot_edit_the_card()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false)).Value!;

        var r = await svc.UpdateHolderFieldsAsync(f.CreatorId, issued.Id, new IdCardHolderInput(
            "TechFest", null, null, null, null, null, null, null));

        Assert.False(r.Ok);
        Assert.Equal("forbidden", r.Error);
    }

    /// <summary>A holder cannot name a storage key they do not own.
    ///
    /// <para>The key is presigned straight back in <c>photo_url</c> and read during generation, and
    /// the presigner signs any string while the storage route is anonymous — so an unvalidated key
    /// was an arbitrary-object read, and a re-mintable one, which defeats revoking access to the
    /// object entirely. Each case below is a real key shape from this system.</para></summary>
    [Theory]
    [InlineData("chat/8f3a0e1c-0000-0000-0000-000000000000/019103/offer-letter.pdf")]  // another room's attachment
    [InlineData("id-cards/019103cd-0000-0000-0000-000000000000.pdf")]                  // another person's card
    [InlineData("certificates/019103cd-0000-0000-0000-000000000000.pdf")]              // another person's certificate
    [InlineData("users/019103cd-0000-0000-0000-000000000000/avatar/abc")]              // another USER's own prefix
    [InlineData("../secrets/dump")]                                                     // traversal-shaped
    public async Task A_holder_cannot_point_their_card_at_a_storage_key_they_do_not_own(string key)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false)).Value!;

        var r = await svc.UpdateHolderFieldsAsync(f.HolderId, issued.Id, new IdCardHolderInput(
            Template: null, PhotoKey: key, SignatureKey: null, LayoutJson: null,
            BloodGroup: null, Address: null, EmergencyContactName: null, EmergencyContactPhone: null));

        Assert.False(r.Ok);
        Assert.Equal("invalid_storage_key", r.Error);
    }

    /// <summary>The holder's own upload prefix is accepted — the guard binds the key to the caller
    /// rather than banning the field, which would break the feature it protects.</summary>
    [Fact]
    public async Task A_holder_may_point_their_card_at_their_own_upload()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false)).Value!;

        var r = await svc.UpdateHolderFieldsAsync(f.HolderId, issued.Id, new IdCardHolderInput(
            Template: null, PhotoKey: $"users/{f.HolderId}/avatar/abc", SignatureKey: null,
            LayoutJson: null, BloodGroup: null, Address: null,
            EmergencyContactName: null, EmergencyContactPhone: null));

        Assert.True(r.Ok, r.Error);
    }

    // ── the anonymous verification boundary ──────────────────────────────────────────────────────

    /// <summary>The public page answers "is this genuine and current" and nothing else. The assertions
    /// are written as absences because that is the property that matters: the projection type has no
    /// field that could carry a student ID, department or contact detail (D-331).</summary>
    [Fact]
    public async Task Verification_exposes_authenticity_and_withholds_the_identifiers()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false)).Value!;

        var v = await svc.VerifyAsync(issued.VerifyCode);

        Assert.True(v.Ok, v.Error);
        Assert.Equal(issued.HolderName, v.Value!.HolderName);
        Assert.Equal(issued.OrgName, v.Value.OrgName);
        Assert.Equal(new DateOnly(2028, 12, 31), v.Value.ValidUntil);
        Assert.False(v.Value.IsRevoked);

        // The type itself is the guarantee: if a future edit adds StudentId to IdCardVerification this
        // stops compiling, which is the point of it being a separate record from IdCardView.
        var exposed = typeof(IdCardVerification).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("StudentId", exposed);
        Assert.DoesNotContain("Department", exposed);
        Assert.DoesNotContain("Phone", exposed);
        Assert.DoesNotContain("Email", exposed);
        Assert.DoesNotContain("BloodGroup", exposed);
        Assert.DoesNotContain("Address", exposed);
        Assert.DoesNotContain("RevokedReason", exposed);
    }

    /// <summary>A revoked card still resolves. Verifying a revocation is the endpoint's purpose, so
    /// hiding it would defeat the check exactly when it matters most (D-036, contrast D-018).</summary>
    [Fact]
    public async Task A_revoked_card_still_verifies_and_says_so()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false)).Value!;

        var revoked = await svc.RevokeAsync(f.CreatorId, issued.Id, "lost card", isAdmin: false);
        Assert.True(revoked.Ok, revoked.Error);

        var v = await svc.VerifyAsync(issued.VerifyCode);

        Assert.True(v.Ok);                              // 200, not 404
        Assert.True(v.Value!.IsRevoked);
        Assert.Equal("revoked", v.Value.Status);

        // The WHY is withheld. A revoke reason is free text written by staff who have no signal it
        // becomes world-readable, and this route is anonymous — so the state crosses the boundary and
        // the sentence does not. Asserted on the TYPE as well as the value, so re-adding the field
        // fails to compile rather than quietly re-opening the leak.
        var exposed = typeof(IdCardVerification).GetProperties().Select(pr => pr.Name).ToArray();
        Assert.DoesNotContain("RevokedReason", exposed);
    }

    /// <summary>An unknown code is a miss, not an error — and reveals nothing about whether the code
    /// space is populated.</summary>
    [Fact]
    public async Task An_unknown_code_does_not_verify()
    {
        using var scope = _factory.Services.CreateScope();
        var v = await Service(scope).VerifyAsync("ZZZZZZZZZZ");
        Assert.False(v.Ok);
        Assert.Equal("not_found", v.Error);
    }

    /// <summary>Expiry is derived, not swept: a card past ValidUntil must never verify as current even
    /// though no job has run to restatus it.</summary>
    [Fact]
    public async Task A_card_past_its_validity_verifies_as_expired()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var input = Input(f.HolderId, f.EventId) with
        {
            ValidFrom = new DateOnly(2020, 1, 1),
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
        };
        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, input, isAdmin: false)).Value!;

        var v = await svc.VerifyAsync(issued.VerifyCode);

        Assert.True(v.Ok);
        Assert.Equal("expired", v.Value!.Status);
    }

    // ── generation ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Generation produces both artefacts and stamps the timestamp. This also exercises the
    /// QuestPDF path end to end, which is what proves the stub rasterizer is not on it (D-035).</summary>
    [Fact]
    public async Task Generating_produces_a_pdf_and_a_png_and_activates_the_card()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false)).Value!;

        var g = await svc.GenerateAsync(f.CreatorId, issued.Id, isAdmin: false);

        Assert.True(g.Ok, g.Error);
        Assert.NotNull(g.Value!.GeneratedAt);
        Assert.Equal("active", g.Value.Status);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.IdCards.AsNoTracking().FirstAsync(c => c.Id == issued.Id);
        Assert.NotNull(row.PdfKey);
        Assert.NotNull(row.PngKey);

        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var pdf = await storage.GetAsync(row.PdfKey!);
        var png = await storage.GetAsync(row.PngKey!);
        // A real PDF and a real PNG, not the 1×1 placeholder the stub rasterizer returns.
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(png.Length > 1000, $"PNG was {png.Length} bytes — suspiciously close to the stub placeholder");
    }

    /// <summary>A revoked card cannot be regenerated: reissuing artefacts for a card that has been
    /// withdrawn would hand back exactly the document the revocation removed.</summary>
    [Fact]
    public async Task A_revoked_card_cannot_be_regenerated()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false)).Value!;
        await svc.RevokeAsync(f.CreatorId, issued.Id, "lost", isAdmin: false);

        var g = await svc.GenerateAsync(f.CreatorId, issued.Id, isAdmin: false);

        Assert.False(g.Ok);
        Assert.Equal("card_revoked", g.Error);
    }

    // ── audit trail ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Issuance records who asserted the identifiers; the holder edit records that the holder
    /// changed their own fields and which — but never their values, because blood group is health data
    /// and an emergency contact is a third party's (D-331).</summary>
    [Fact]
    public async Task The_audit_trail_records_the_actors_without_copying_the_sensitive_values()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var issued = (await svc.IssueAsync(f.CreatorId, f.EventId, Input(f.HolderId, f.EventId), isAdmin: false)).Value!;
        await svc.UpdateHolderFieldsAsync(f.HolderId, issued.Id, new IdCardHolderInput(
            null, null, null, null, "O+", "1 Test Road", "Next Of Kin", "9000000000"));

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(l => l.Entity == "id_cards" && l.EntityId == issued.Id)
            .OrderBy(l => l.CreatedAt).ToListAsync();

        Assert.Contains(logs, l => l.Action == "id_card.issued" && l.ActorId == f.CreatorId);
        var edit = Assert.Single(logs, l => l.Action == "id_card.holder_edited");
        Assert.Equal(f.HolderId, edit.ActorId);
        Assert.Contains("blood_group", edit.DetailsJson);        // the field name is recorded...
        Assert.DoesNotContain("O+", edit.DetailsJson);            // ...the value is not
        Assert.DoesNotContain("1 Test Road", edit.DetailsJson);
        Assert.DoesNotContain("9000000000", edit.DetailsJson);
    }
}
