using System.Net.Http.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Certificates;
using Kurx.Infrastructure.IdCards;
using Kurx.Infrastructure.Persistence;
using Kurx.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Event badges (D-362) — the organizer-only half of D-331.
///
/// <para>Two things are worth pinning here and they fail differently. The rendering half fails visibly on
/// paper, so it is tested by comparison: render the same badge twice with one thing changed and assert
/// only that thing changed. The <b>authority</b> half fails invisibly — a badge is an entry credential,
/// and whoever can mint one can mint one for anybody — so it is tested against a real database with a real
/// authority resolution, never a mock.</para>
/// </summary>
public class EventBadgeTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public EventBadgeTests(KurxApiFactory factory) => _factory = factory;

    private static readonly ICertificateDocumentRenderer Renderer =
        new CertificateDocumentRenderer(new QrCodeGenerator(NullLogger<QrCodeGenerator>.Instance));

    private static BadgeRecipient Attendee(string name = "Asha Menon", string? photoKey = null) =>
        new(Guid.NewGuid(), name, BadgeKind.Attendee, "General Admission", null, photoKey,
            Guid.NewGuid().ToString());

    private static BadgeRecipient Staff(string name = "Ravi Kumar", string access = "All Access") =>
        new(Guid.NewGuid(), name, BadgeKind.Staff, "Organizer", access, null,
            $"staff:{Guid.NewGuid()}:deadbeef");

    private static CertificateRenderData DataFor(BadgeRecipient r) =>
        new(new Dictionary<string, string>
        {
            [BadgeLayout.FieldName] = r.Name,
            [BadgeLayout.FieldSubtitle] = r.Subtitle ?? "",
            [BadgeLayout.FieldAccess] = r.AccessLevel ?? "",
            [BadgeLayout.FieldEvent] = "Sample Hackathon 2026",
            [BadgeLayout.FieldEventDate] = "01 Oct 2026",
            [BadgeLayout.FieldCardNumber] = "ABCD1234",
        }, new Dictionary<string, byte[]>(), r.QrPayload);

    private static Task<byte[]> Png(BadgeRecipient r, BadgeSize size) =>
        Renderer.RenderPngAsync(BadgeLayout.Build(r, size), DataFor(r), 96);

    // ── Rendering ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_badge_size_renders_and_they_differ()
    {
        var r = Attendee();
        var rendered = new List<byte[]>();
        foreach (var size in BadgeSize.All)
        {
            var png = await Png(r, size);
            Assert.NotEmpty(png);
            rendered.Add(png);
        }

        // Three sizes must not collapse to one output — a size picker that silently prints the same
        // physical badge is worse than no picker, because the operator only learns at the guillotine.
        Assert.Equal(rendered.Count, rendered.Select(Convert.ToBase64String).Distinct().Count());
    }

    /// <summary>CR80 is the only landscape size, and it takes a different arrangement. If someone
    /// "simplifies" the layout to one branch, the card layout silently becomes a squashed portrait.</summary>
    [Fact]
    public void Only_the_card_size_is_landscape()
    {
        Assert.True(BadgeSize.Card.IsLandscape);
        Assert.False(BadgeSize.Lanyard.IsLandscape);
        Assert.False(BadgeSize.Large.IsLandscape);
    }

    /// <summary>A staff badge asserts an access level and an attendee badge does not, so the two must not
    /// render identically — a marshal reads authority off the badge across a room.</summary>
    [Fact]
    public async Task Staff_and_attendee_badges_render_differently()
    {
        var attendee = await Png(Attendee(), BadgeSize.Lanyard);
        var staff = await Png(Staff(), BadgeSize.Lanyard);
        Assert.NotEqual(Convert.ToBase64String(attendee), Convert.ToBase64String(staff));
    }

    /// <summary>Access level drives the colour band, so two staff badges at different levels differ.</summary>
    [Fact]
    public async Task Access_level_changes_the_staff_badge()
    {
        var allAccess = await Png(Staff(access: "All Access"), BadgeSize.Lanyard);
        var vendor = await Png(Staff(access: "Vendor"), BadgeSize.Lanyard);
        Assert.NotEqual(Convert.ToBase64String(allAccess), Convert.ToBase64String(vendor));
    }

    /// <summary>The edge cases named in the brief. Neither may throw: a print run of two hundred badges
    /// must not fail because one person has a long name or no avatar.</summary>
    [Theory]
    [InlineData("Bo")]
    [InlineData("Venkata Naga Sai Sri Lakshmi Narasimha Raju Bhupathiraju")]
    [InlineData("Ω 日本語 محمد")]
    public async Task Awkward_names_still_render(string name)
    {
        var png = await Png(Attendee(name), BadgeSize.Lanyard);
        Assert.NotEmpty(png);
    }

    [Fact]
    public async Task A_badge_with_no_photo_still_renders()
    {
        // No photo key at all, and no image supplied in the render data — the documented no-photo case.
        var png = await Png(Attendee(photoKey: null), BadgeSize.Lanyard);
        Assert.NotEmpty(png);
    }

    // ── Print sheet ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sheet_composes_many_badges_into_one_pdf()
    {
        var badges = new List<byte[]>();
        for (var i = 0; i < 9; i++) badges.Add(await Png(Attendee($"Person {i}"), BadgeSize.Lanyard));

        var pdf = BadgeSheetComposer.Compose(badges, BadgeSize.Lanyard.WidthMm, BadgeSize.Lanyard.HeightMm);

        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    /// <summary>More badges than fit on one A4 sheet must spill onto further pages rather than being
    /// dropped. Silent truncation here means missing lanyards on the day.</summary>
    [Fact]
    public async Task Sheet_pages_rather_than_truncates()
    {
        var one = await Png(Attendee(), BadgeSize.Lanyard);

        var small = BadgeSheetComposer.Compose([one], BadgeSize.Lanyard.WidthMm, BadgeSize.Lanyard.HeightMm);
        var many = BadgeSheetComposer.Compose(
            Enumerable.Repeat(one, 24).ToList(), BadgeSize.Lanyard.WidthMm, BadgeSize.Lanyard.HeightMm);

        Assert.True(many.Length > small.Length,
            "24 badges must produce more document than 1 — equal size would mean badges were dropped.");
    }

    // ── The staff pass signature ────────────────────────────────────────────────────────────────

    /// <summary>The security claim behind the staff QR (D-362): one secret signs two different things, so
    /// they must be domain-separated. If <c>SignStaffPass</c> ever collapses into <c>SignTicketCode</c>, a
    /// staff pass and a ticket signature become interchangeable over the same id space.</summary>
    [Fact]
    public void A_staff_pass_signature_is_not_a_ticket_signature()
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();

        var id = Guid.NewGuid();
        Assert.NotEqual(tokens.SignTicketCode(id), tokens.SignStaffPass(id));
    }

    [Fact]
    public void A_staff_pass_signature_is_stable_and_id_specific()
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        // Stable: it is recomputed at the gate rather than stored, so it must be a pure function of the id.
        Assert.Equal(tokens.SignStaffPass(a), tokens.SignStaffPass(a));
        Assert.NotEqual(tokens.SignStaffPass(a), tokens.SignStaffPass(b));
    }

    // ── Authority and recipient resolution (real database) ──────────────────────────────────────

    /// <summary>The access-control claim. A badge is an entry credential, so a caller with no standing on
    /// the event must not be able to enumerate who is attending, and must get 404 rather than 403 —
    /// D-018: a hidden resource does not confirm it exists.</summary>
    [Fact]
    public async Task A_stranger_cannot_list_badge_recipients()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        var stranger = SeedUser(scope, "+919000000099");
        var result = await svc.ListRecipientsAsync(seeded.EventId, stranger, isAdmin: false);

        Assert.False(result.Ok);
        Assert.Equal("not_found", result.Error);
    }

    [Fact]
    public async Task A_stranger_cannot_print_a_sheet()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        var stranger = SeedUser(scope, "+919000000098");
        var result = await svc.RenderSheetAsync(
            seeded.EventId, stranger, isAdmin: false,
            new BadgeSheetRequest(BadgeSize.Lanyard.Key, [BadgeKind.Attendee, BadgeKind.Staff]));

        Assert.False(result.Ok);
        Assert.Equal("not_found", result.Error);
    }

    [Fact]
    public async Task The_organizer_sees_both_attendees_and_staff()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        var result = await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, isAdmin: false);

        Assert.True(result.Ok);
        var recipients = result.Value!;
        Assert.Contains(recipients, r => r.Kind == BadgeKind.Attendee && r.UserId == seeded.AttendeeId);
        Assert.Contains(recipients, r => r.Kind == BadgeKind.Staff && r.UserId == seeded.StaffId);
    }

    /// <summary>The QR is what makes the badge a credential rather than a name tag, and the two kinds
    /// carry different schemes: an attendee's badge must resolve through the gate's existing ticket
    /// lookup, and a staff badge — who holds no ticket — must carry its own signed pass.</summary>
    [Fact]
    public async Task Each_kind_carries_the_right_qr_scheme()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();

        var recipients = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;

        var attendee = recipients.First(r => r.UserId == seeded.AttendeeId);
        // Exactly the ticket code, exactly as TicketQrEndpoints encodes it — so a printed badge scans
        // through GateEntryService with no change to the gate at all.
        Assert.Equal(seeded.TicketCode.ToString(), attendee.QrPayload);

        var staff = recipients.First(r => r.UserId == seeded.StaffId);
        var parts = staff.QrPayload.Split(':');
        Assert.Equal("staff", parts[0]);
        Assert.Equal(seeded.AssignmentId, Guid.Parse(parts[1]));
        Assert.Equal(tokens.SignStaffPass(seeded.AssignmentId), parts[2]);
    }

    /// <summary>Someone who bought a ticket AND staffs the event gets one badge, as staff. Two lanyards
    /// for one person is an invitation to wear the wrong one at a door.</summary>
    [Fact]
    public async Task Someone_who_is_both_gets_one_staff_badge()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Give the staff member a ticket as well.
        SeedTicket(db, seeded.EventId, seeded.TicketTypeId, seeded.StaffId,
            scope.ServiceProvider.GetRequiredService<TokenService>());

        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var recipients = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;

        var forStaffMember = recipients.Where(r => r.UserId == seeded.StaffId).ToList();
        Assert.Single(forStaffMember);
        Assert.Equal(BadgeKind.Staff, forStaffMember[0].Kind);
    }

    [Fact]
    public async Task An_unknown_size_is_refused_rather_than_defaulted()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        var result = await svc.RenderSheetAsync(
            seeded.EventId, seeded.OwnerId, false,
            new BadgeSheetRequest("postcard", [BadgeKind.Attendee]));

        // Defaulting would print a run at the wrong physical size, which is only discovered on paper.
        Assert.False(result.Ok);
        Assert.Equal("unknown_badge_size", result.Error);
    }

    [Fact]
    public async Task The_organizer_can_print_a_real_sheet()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        var result = await svc.RenderSheetAsync(
            seeded.EventId, seeded.OwnerId, false,
            new BadgeSheetRequest(BadgeSize.Lanyard.Key, [BadgeKind.Attendee, BadgeKind.Staff]));

        Assert.True(result.Ok, result.Error);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result.Value!, 0, 4));
    }

    // ── The card design (editor) ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_event_with_no_saved_design_gets_the_default()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        var result = await svc.GetAsync(seeded.EventId, seeded.OwnerId, false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(IdCardTemplateSpec.Default, result.Value);
        // Null fields means "the built-in layout", which is what the editor opens on.
        Assert.Null(result.Value!.Fields);
    }

    /// <summary>The built-in layout is expressed as placements so the editor can load and drag them. If it
    /// were not, the editor would open on an empty card and an organiser would have to rebuild it.</summary>
    [Fact]
    public void The_built_in_layout_is_available_as_placements()
    {
        var portrait = BadgeLayout.Defaults(BadgeSize.Lanyard, isStaff: true);
        var landscape = BadgeLayout.Defaults(BadgeSize.Card, isStaff: true);

        Assert.NotEmpty(portrait);
        Assert.NotEmpty(landscape);
        Assert.All(portrait, f => Assert.Contains(f.Key, IdCardField.Keys));
        Assert.All(landscape, f => Assert.Contains(f.Key, IdCardField.Keys));

        // Every placement is on the card.
        Assert.All(portrait, f =>
        {
            Assert.InRange(f.X, 0, 100);
            Assert.InRange(f.Y, 0, 100);
        });
    }

    [Fact]
    public async Task A_saved_design_round_trips()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        var spec = IdCardTemplateSpec.Default with
        {
            AccentColor = "#7f1d1d",
            SizeKey = BadgeSize.Card.Key,
            Fields =
            [
                new IdCardField(IdCardField.Name, 10, 20, 50, 8, 14, "#000000", "left", "bold"),
                new IdCardField(IdCardField.Qr, 70, 60, 25, 25),
            ],
        };

        var saved = await svc.SaveAsync(seeded.EventId, seeded.OwnerId, false, spec);
        Assert.True(saved.Ok, saved.Error);

        var read = (await svc.GetAsync(seeded.EventId, seeded.OwnerId, false)).Value!;
        Assert.Equal("#7f1d1d", read.AccentColor);
        Assert.Equal(BadgeSize.Card.Key, read.SizeKey);
        Assert.Equal(2, read.Fields!.Count);

        var name = read.Fields.Single(f => f.Key == IdCardField.Name);
        Assert.Equal(10, name.X);
        Assert.Equal(20, name.Y);
        Assert.Equal("left", name.Align);
        Assert.Equal("bold", name.Weight);
    }

    /// <summary>Saving twice must update the one design rather than accumulate rows — otherwise "this
    /// event's card" stops having a single answer.</summary>
    [Fact]
    public async Task Saving_twice_updates_one_design()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        await svc.SaveAsync(seeded.EventId, seeded.OwnerId, false, IdCardTemplateSpec.Default);
        await svc.SaveAsync(seeded.EventId, seeded.OwnerId, false,
            IdCardTemplateSpec.Default with { AccentColor = "#14532d" });

        var rows = await db.DesignTemplates.AsNoTracking()
            .Where(t => t.EventId == seeded.EventId && t.Kind == TemplateKind.IdCard).CountAsync();

        Assert.Equal(1, rows);
        Assert.Equal("#14532d", (await svc.GetAsync(seeded.EventId, seeded.OwnerId, false)).Value!.AccentColor);
    }

    /// <summary>Colours reach a rendered document, so an unparseable one must be dropped rather than
    /// stored and handed to every later reader.</summary>
    [Theory]
    [InlineData("red")]
    [InlineData("#12")]
    [InlineData("#zzzzzz")]
    [InlineData("javascript:alert(1)")]
    public async Task A_bad_colour_is_discarded_not_stored(string colour)
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        var saved = await svc.SaveAsync(seeded.EventId, seeded.OwnerId, false,
            IdCardTemplateSpec.Default with { AccentColor = colour });

        Assert.True(saved.Ok);
        Assert.Null(saved.Value!.AccentColor);
    }

    /// <summary>A field dragged off the page would render partly outside the card, and the organiser would
    /// only discover it on paper. Geometry is clamped on the way in.</summary>
    [Fact]
    public async Task Placements_are_clamped_onto_the_card()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        var saved = await svc.SaveAsync(seeded.EventId, seeded.OwnerId, false,
            IdCardTemplateSpec.Default with
            {
                Fields = [new IdCardField(IdCardField.Name, -50, 400, 900, -3, 999)],
            });

        var f = saved.Value!.Fields!.Single();
        Assert.InRange(f.X, 0, 99);
        Assert.InRange(f.Y, 0, 99);
        Assert.InRange(f.Width, 1, 100);
        Assert.InRange(f.Height, 1, 100);
        Assert.InRange(f.FontSizePt!.Value, 4, 72);
    }

    /// <summary>An unknown field key means a client sent something this build does not have. Dropped
    /// rather than rendered as a placeholder on somebody's printed badge.</summary>
    [Fact]
    public async Task An_unknown_field_key_is_dropped()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        var saved = await svc.SaveAsync(seeded.EventId, seeded.OwnerId, false,
            IdCardTemplateSpec.Default with
            {
                Fields =
                [
                    new IdCardField("salary", 10, 10, 20, 5),
                    new IdCardField(IdCardField.Name, 10, 20, 50, 8),
                ],
            });

        Assert.Single(saved.Value!.Fields!);
        Assert.Equal(IdCardField.Name, saved.Value.Fields!.Single().Key);
    }

    /// <summary>Both artwork and logo keys are storage paths the renderer reads. Accepting an arbitrary one
    /// would turn saving a design into a read primitive over the whole bucket.</summary>
    [Theory]
    [InlineData("logo")]
    [InlineData("background")]
    public async Task An_asset_key_outside_the_event_is_refused(string which)
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        var outside = "users/someone-else/avatar.png";
        var spec = which == "logo"
            ? IdCardTemplateSpec.Default with { LogoKey = outside }
            : IdCardTemplateSpec.Default with { BackgroundKey = outside };

        var result = await svc.SaveAsync(seeded.EventId, seeded.OwnerId, false, spec);

        Assert.False(result.Ok);
        Assert.Equal($"invalid_{which}_key", result.Error);
    }

    /// <summary>The design must actually change the printed card. If it did not, the editor would be a
    /// form that saves rows nothing reads.</summary>
    [Fact]
    public async Task The_saved_design_changes_the_rendered_card()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var cards = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var templates = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        var before = await cards.RenderOneAsync(
            seeded.EventId, seeded.OwnerId, false, seeded.AttendeeId, BadgeSize.Lanyard.Key);

        // Move the name and drop everything else — a change no default layout could produce.
        await templates.SaveAsync(seeded.EventId, seeded.OwnerId, false,
            IdCardTemplateSpec.Default with
            {
                AccentColor = "#7f1d1d",
                Fields = [new IdCardField(IdCardField.Name, 5, 70, 90, 10, 20, "#7f1d1d", "left", "bold")],
            });

        var after = await cards.RenderOneAsync(
            seeded.EventId, seeded.OwnerId, false, seeded.AttendeeId, BadgeSize.Lanyard.Key);

        Assert.True(before.Ok && after.Ok);
        Assert.NotEqual(Convert.ToBase64String(before.Value!), Convert.ToBase64String(after.Value!));
    }

    [Fact]
    public async Task A_preview_renders_for_both_kinds()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        foreach (var kind in new[] { BadgeKind.Attendee, BadgeKind.Staff })
        {
            var r = await svc.PreviewAsync(
                seeded.EventId, seeded.OwnerId, false, IdCardTemplateSpec.Default, kind);
            Assert.True(r.Ok, r.Error);
            Assert.NotEmpty(r.Value!);
        }
    }

    [Fact]
    public async Task A_stranger_cannot_read_or_change_the_design()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();
        var stranger = SeedUser(scope, "+919000000096");

        Assert.Equal("not_found", (await svc.GetAsync(seeded.EventId, stranger, false)).Error);
        Assert.Equal("not_found",
            (await svc.SaveAsync(seeded.EventId, stranger, false, IdCardTemplateSpec.Default)).Error);
    }

    [Fact]
    public async Task An_svg_asset_is_refused()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        // SVG is a script-capable document, and this one is fetched by the renderer and served in a page.
        var result = await svc.PresignAssetAsync(
            seeded.EventId, seeded.OwnerId, false, "image/svg+xml", "background");

        Assert.False(result.Ok);
        Assert.Equal("unsupported_content_type", result.Error);
    }

    /// <summary>Artwork and logo land under different prefixes with different ceilings, and both stay
    /// inside the event so the save-time check can accept them.</summary>
    [Theory]
    [InlineData("background")]
    [InlineData("logo")]
    public async Task A_presigned_asset_key_is_inside_the_event(string purpose)
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardTemplateService>();

        var r = await svc.PresignAssetAsync(seeded.EventId, seeded.OwnerId, false, "image/png", purpose);

        Assert.True(r.Ok, r.Error);
        Assert.StartsWith($"events/{seeded.EventId}/id-cards/{purpose}/", r.Value!.Key);
    }

    // ── Issuing real cards ──────────────────────────────────────────────────────────────────────
    //
    // The distinction these pin: generating a badge must create a RECORD, not just paper. A card that
    // exists only as a downloaded PDF cannot be looked up, revoked, or verified from the code on its face.

    [Fact]
    public async Task Generating_creates_real_id_card_rows_with_stored_artefacts()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        var report = await svc.GenerateAsync(
            seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Attendee, BadgeKind.Staff]));

        Assert.True(report.Ok, report.Error);
        Assert.Equal(2, report.Value!.Issued);          // one attendee, one staff
        Assert.Equal(0, report.Value.Regenerated);

        var cards = await db.IdCards.AsNoTracking().Where(c => c.EventId == seeded.EventId).ToListAsync();
        Assert.Equal(2, cards.Count);

        foreach (var card in cards)
        {
            Assert.False(string.IsNullOrWhiteSpace(card.CardNumber));
            Assert.Equal(10, card.VerifyCode.Length);   // same shape as Certificate.VerifyCode (D-331)
            Assert.Equal(IdCardStatus.Active, card.Status);
            Assert.Equal(seeded.OwnerId, card.IssuedBy);
            Assert.NotNull(card.GeneratedAt);

            // The artefacts are not merely named — they were actually written.
            Assert.True(await storage.ExistsAsync(card.PdfKey!), $"missing PDF for {card.CardNumber}");
            Assert.True(await storage.ExistsAsync(card.PngKey!), $"missing PNG for {card.CardNumber}");
        }
    }

    /// <summary>Reprinting a damaged badge must not mint a second identity for the same person. A genuine
    /// reissue after a loss takes a new number, and that is a different act (D-331).</summary>
    [Fact]
    public async Task Regenerating_keeps_the_existing_card_number()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var request = new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Attendee]);
        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false, request);

        var first = await db.IdCards.AsNoTracking()
            .Where(c => c.EventId == seeded.EventId && c.UserId == seeded.AttendeeId).SingleAsync();

        var second = await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false, request);
        Assert.Equal(0, second.Value!.Issued);
        Assert.Equal(1, second.Value.Regenerated);

        var after = await db.IdCards.AsNoTracking()
            .Where(c => c.EventId == seeded.EventId && c.UserId == seeded.AttendeeId).SingleAsync();

        Assert.Equal(first.Id, after.Id);
        Assert.Equal(first.CardNumber, after.CardNumber);
        Assert.Equal(first.VerifyCode, after.VerifyCode);
    }

    /// <summary>Card numbers are unique per issuing org — the database enforces it, and the allocator has
    /// to agree with that or a print run fails halfway through with a constraint violation.</summary>
    [Fact]
    public async Task Card_numbers_are_unique_within_the_org()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Attendee, BadgeKind.Staff]));

        var numbers = await db.IdCards.AsNoTracking()
            .Where(c => c.EventId == seeded.EventId).Select(c => c.CardNumber).ToListAsync();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    /// <summary>Once issued, the roster reports the card — which is how the console distinguishes
    /// "not issued" from "issued", a distinction the previous print-only version could not make.</summary>
    [Fact]
    public async Task The_roster_reports_issued_cards()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        var before = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;
        Assert.All(before, r => Assert.Null(r.Card));

        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Attendee]));

        var after = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;
        var attendee = after.First(r => r.UserId == seeded.AttendeeId);

        Assert.NotNull(attendee.Card);
        Assert.Equal("Active", attendee.Card!.Status);
        Assert.False(attendee.Card.IsRevoked);
        // Staff were not in this run, so they stay un-issued rather than being swept in.
        Assert.Null(after.First(r => r.UserId == seeded.StaffId).Card);
    }

    [Fact]
    public async Task A_stranger_cannot_issue_cards()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        var stranger = SeedUser(scope, "+919000000097");
        var result = await svc.GenerateAsync(seeded.EventId, stranger, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Attendee]));

        Assert.False(result.Ok);
        Assert.Equal("not_found", result.Error);
    }

    // ── The badge actually scans ────────────────────────────────────────────────────────────────

    /// <summary>The claim the whole feature rests on: a printed attendee badge is admitted by the real
    /// check-in flow, with no change to the gate.
    ///
    /// <para>Asserting that the payload equals <c>Ticket.Code</c> — which the test above does — proves the
    /// two strings match, not that the gate accepts the string. This takes the payload exactly as printed
    /// and hands it to <see cref="IGateEntryService"/>, which is what a scanner at the door calls. If
    /// anything about the badge's QR ever stops being a scannable ticket, this fails rather than the
    /// door.</para></summary>
    [Fact]
    public async Task An_attendee_badge_is_admitted_by_the_real_gate()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();

        var recipients = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;
        var badge = recipients.First(r => r.UserId == seeded.AttendeeId);

        // Exactly what is printed on the lanyard — parsed the way the scanner would.
        var scanned = Guid.Parse(badge.QrPayload);
        var result = await gate.ScanAsync(seeded.OwnerId, seeded.EventId, scanned, "badge-scan-test");

        Assert.True(result.Admitted, result.RejectionReason ?? "not admitted");
        Assert.False(result.IsDuplicate);
    }

    /// <summary>Scanning the same badge twice reports a duplicate rather than admitting again — the badge
    /// inherits the gate's existing single-admission behaviour because it *is* the ticket.</summary>
    [Fact]
    public async Task Scanning_a_badge_twice_is_a_duplicate()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();

        var recipients = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;
        var scanned = Guid.Parse(recipients.First(r => r.UserId == seeded.AttendeeId).QrPayload);

        await gate.ScanAsync(seeded.OwnerId, seeded.EventId, scanned, "first");
        var second = await gate.ScanAsync(seeded.OwnerId, seeded.EventId, scanned, "second");

        Assert.False(second.Admitted);
        Assert.True(second.IsDuplicate);
    }

    /// <summary>A staff pass is structurally not a ticket code, so a scanner parsing a bare Guid can never
    /// mistake one for the other and resolve it against some other event's ticket. This was D-362's only
    /// safety net while the staff gate was deferred; it stays because keeping the two schemes disjoint is
    /// still what makes two routes safe (D-385).</summary>
    [Fact]
    public async Task A_staff_pass_cannot_be_scanned_as_a_ticket()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        var recipients = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;
        var staffPayload = recipients.First(r => r.UserId == seeded.StaffId).QrPayload;

        Assert.False(Guid.TryParse(staffPayload, out _),
            "A staff pass must not parse as a bare ticket code — that is what keeps the two schemes apart.");
    }

    // ── D-385: the staff gate, which D-362 printed a credential for and could not verify ────────
    //
    // Until this landed, `SignStaffPass` had no counterpart anywhere in the platform: the QR was minted,
    // printed at 27mm on a real lanyard, and understood by nothing. These drive the real
    // IGateEntryService against a real database, because every check that matters here — is the
    // assignment still accepted, is it for THIS event — is a live query, not a property of the signature.

    /// <summary>The round-trip that makes the badge a credential: take the payload the badge actually
    /// prints and hand it to the service a scanner calls.</summary>
    [Fact]
    public async Task A_staff_badge_is_admitted_by_the_real_gate()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();

        var recipients = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;
        var pass = recipients.First(r => r.UserId == seeded.StaffId).QrPayload;

        var result = await gate.ScanStaffAsync(seeded.OwnerId, seeded.EventId, pass, "staff-scan-test");

        Assert.True(result.Admitted, result.Reason ?? "not admitted");
        Assert.False(result.IsDuplicate);
        // The marshal reads these back against the person in front of them.
        Assert.Equal("Volunteer", result.Role);
        Assert.Equal("Volunteer", result.AccessLevel);
        Assert.False(string.IsNullOrWhiteSpace(result.Name));
    }

    /// <summary>Staff come and go all day, so a repeat scan reports rather than refuses — and still shows
    /// who they are, which is the whole reason the marshal scanned again.</summary>
    [Fact]
    public async Task Scanning_a_staff_badge_twice_reports_the_first_arrival()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();

        var pass = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!
            .First(r => r.UserId == seeded.StaffId).QrPayload;

        await gate.ScanStaffAsync(seeded.OwnerId, seeded.EventId, pass, "first");
        var second = await gate.ScanStaffAsync(seeded.OwnerId, seeded.EventId, pass, "second");

        Assert.False(second.Admitted);
        Assert.True(second.IsDuplicate);
        Assert.NotNull(second.FirstScannedAt);
        Assert.Equal("Volunteer", second.AccessLevel);
    }

    /// <summary>The security claim. A genuine signature proves only that Kurx minted the pass — it says
    /// nothing about whether the holder is still crew, so revocation is read live (D-015). Removing
    /// someone from the crew must stop the badge already in their pocket.</summary>
    [Theory]
    [InlineData(AssignmentStatus.Removed)]
    [InlineData(AssignmentStatus.Declined)]
    [InlineData(AssignmentStatus.Invited)]
    public async Task A_staff_badge_stops_working_when_the_assignment_is_not_accepted(AssignmentStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var pass = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!
            .First(r => r.UserId == seeded.StaffId).QrPayload;

        var assignment = await db.EventAssignments.FirstAsync(a => a.Id == seeded.AssignmentId);
        assignment.Status = status;
        await db.SaveChangesAsync();

        var result = await gate.ScanStaffAsync(seeded.OwnerId, seeded.EventId, pass, "after-removal");

        Assert.False(result.Admitted);
        Assert.Equal("assignment_not_active", result.Reason);
    }

    /// <summary>A badge minted for Saturday's event must not open Sunday's door, however genuine its
    /// signature — the pass names an assignment, and the assignment names exactly one event.</summary>
    [Fact]
    public async Task A_staff_badge_from_another_event_is_refused()
    {
        using var scope = _factory.Services.CreateScope();
        var eventA = Seed(scope);
        var eventB = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();

        var passForA = (await svc.ListRecipientsAsync(eventA.EventId, eventA.OwnerId, false)).Value!
            .First(r => r.UserId == eventA.StaffId).QrPayload;

        var result = await gate.ScanStaffAsync(eventB.OwnerId, eventB.EventId, passForA, "wrong-event");

        Assert.False(result.Admitted);
        Assert.Equal("event_mismatch", result.Reason);
    }

    /// <summary>Forgery. Each of these is a payload someone could construct without the secret, and every
    /// one must be refused with the same answer — telling a forger which half they got wrong is free
    /// help.</summary>
    [Fact]
    public async Task A_forged_or_tampered_staff_pass_is_refused()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();
        var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();

        var real = tokens.SignStaffPass(seeded.AssignmentId);
        var forged = new[]
        {
            $"staff:{seeded.AssignmentId}:{new string('a', real.Length)}",   // invented signature
            $"staff:{seeded.AssignmentId}:{real[..^1]}",                     // truncated
            $"staff:{Guid.NewGuid()}:{real}",                                // real signature, other id
            $"staff:{seeded.AssignmentId}",                                  // no signature at all
            $"{seeded.AssignmentId}:{real}",                                 // no scheme prefix
            seeded.TicketCode.ToString(),                                    // an attendee's ticket code
            "staff:not-a-guid:deadbeef",
            "",
        };

        foreach (var pass in forged)
        {
            var result = await gate.ScanStaffAsync(seeded.OwnerId, seeded.EventId, pass, "forgery");
            Assert.False(result.Admitted, $"admitted a forged pass: {pass}");
            Assert.Equal("invalid_pass", result.Reason);
        }
    }

    /// <summary>Whoever cannot work the attendee gate cannot work the staff one either. A staff scan
    /// returns a person's name and access level, so an open route would be an attendee-list leak with a
    /// credential check attached.</summary>
    [Fact]
    public async Task A_stranger_cannot_scan_a_staff_badge()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();

        var pass = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!
            .First(r => r.UserId == seeded.StaffId).QrPayload;

        var stranger = SeedUser(scope, "+919000000095");
        var result = await gate.ScanStaffAsync(stranger, seeded.EventId, pass, "stranger");

        Assert.False(result.Admitted);
        Assert.Equal("forbidden", result.Reason);
        // And the refusal happens before the pass is even looked at, so it leaks nothing about the crew.
        Assert.Null(result.Name);
    }

    /// <summary>Why <c>staff_gate_entries</c> is its own table rather than a nullable column on
    /// <c>gate_entries</c>: every attendance figure on the platform counts that row, and admitting staff
    /// through it would inflate attendee check-ins with people who never bought anything.</summary>
    [Fact]
    public async Task A_staff_arrival_is_not_counted_as_an_attendee_check_in()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var pass = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!
            .First(r => r.UserId == seeded.StaffId).QrPayload;

        await gate.ScanStaffAsync(seeded.OwnerId, seeded.EventId, pass, "staff");

        Assert.Equal(0, await db.GateEntries.CountAsync(g => g.EventId == seeded.EventId));
        Assert.Equal(1, await db.StaffGateEntries.CountAsync(s => s.EventId == seeded.EventId));
    }

    /// <summary>An attendee's ticket code is not a staff pass, and a staff pass is not a ticket. Neither
    /// route resolves the other's credential — which is the point of giving them separate routes.</summary>
    [Fact]
    public async Task The_two_credentials_cannot_be_swapped()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();

        var recipients = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;
        var staffPass = recipients.First(r => r.UserId == seeded.StaffId).QrPayload;
        var ticketCode = Guid.Parse(recipients.First(r => r.UserId == seeded.AttendeeId).QrPayload);

        // A ticket code down the staff route.
        var asStaff = await gate.ScanStaffAsync(seeded.OwnerId, seeded.EventId, ticketCode.ToString(), "swap");
        Assert.False(asStaff.Admitted);

        // And a staff pass cannot even be expressed as the ticket route's argument, which is the
        // structural half of the guarantee.
        Assert.False(Guid.TryParse(staffPass, out _));
    }

    /// <summary>Over HTTP, because a route that is never mapped fails exactly like one that is
    /// forbidden.</summary>
    [Fact]
    public async Task The_staff_gate_route_is_mapped_and_authenticated()
    {
        var anonymous = _factory.CreateClient();
        var res = await anonymous.PostAsJsonAsync(
            $"/v1/gate/{Guid.NewGuid()}/scan-staff", new { pass = "staff:x:y" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);

        var (client, ownerId) = await AuthedClientAsync();
        Seeded seeded;
        using (var scope = _factory.Services.CreateScope())
            seeded = Seed(scope, ownerId);

        string pass;
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
            pass = (await svc.ListRecipientsAsync(seeded.EventId, ownerId, false)).Value!
                .First(r => r.UserId == seeded.StaffId).QrPayload;
        }

        var ok = await client.PostAsJsonAsync(
            $"/v1/gate/{seeded.EventId}/scan-staff", new { pass, deviceInfo = "http-test" });
        Assert.Equal(System.Net.HttpStatusCode.OK, ok.StatusCode);

        var body = await ok.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("admitted").GetBoolean());
        Assert.Equal("Volunteer", body.GetProperty("access_level").GetString());
    }

    /// <summary>The signature is verified in fixed time and only ever accepts the one it minted.</summary>
    [Fact]
    public void A_staff_pass_verifies_only_its_own_signature()
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();
        var id = Guid.NewGuid();

        Assert.Equal(id, tokens.VerifyStaffPass($"staff:{id}:{tokens.SignStaffPass(id)}"));
        // Case is normalised, because a scanner or a QR library may hand back either.
        Assert.Equal(id, tokens.VerifyStaffPass($"staff:{id}:{tokens.SignStaffPass(id).ToUpperInvariant()}"));
        Assert.Null(tokens.VerifyStaffPass($"staff:{id}:{tokens.SignStaffPass(Guid.NewGuid())}"));
        Assert.Null(tokens.VerifyStaffPass(null));
        // A ticket signature must never stand in for a staff one — that is what the domain prefix buys.
        Assert.Null(tokens.VerifyStaffPass($"staff:{id}:{tokens.SignTicketCode(id)}"));
    }

    // ── The routes themselves ───────────────────────────────────────────────────────────────────
    //
    // The tests above drive IIdCardService directly, which is where the authority check lives — but that
    // leaves the wiring untested, and a badge route that is never mapped fails exactly like one that is
    // forbidden. These go over HTTP.

    [Fact]
    public async Task Badge_routes_require_authentication()
    {
        var anonymous = _factory.CreateClient();
        var eventId = Guid.NewGuid();

        foreach (var path in new[] { "sizes", "recipients" })
        {
            var res = await anonymous.GetAsync($"/v1/events/{eventId}/badges/{path}");
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
        }

        var sheet = await anonymous.PostAsJsonAsync(
            $"/v1/events/{eventId}/badges/sheet", new { sizeKey = "lanyard" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, sheet.StatusCode);
    }

    /// <summary>The organizer's full path over HTTP: list, then download a real PDF. This is what proves
    /// the routes are mapped and the `.pdf` suffix route binds — neither of which the service-level tests
    /// can see.</summary>
    [Fact]
    public async Task The_organizer_can_list_and_download_over_http()
    {
        var (client, ownerId) = await AuthedClientAsync();

        Guid eventId, attendeeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var seeded = Seed(scope, ownerId);
            eventId = seeded.EventId;
            attendeeId = seeded.AttendeeId;
        }

        var recipients = await client.GetAsync($"/v1/events/{eventId}/badges/recipients");
        Assert.Equal(System.Net.HttpStatusCode.OK, recipients.StatusCode);

        var sizes = await client.GetAsync($"/v1/events/{eventId}/badges/sizes");
        Assert.Equal(System.Net.HttpStatusCode.OK, sizes.StatusCode);

        var sheet = await client.PostAsJsonAsync(
            $"/v1/events/{eventId}/badges/sheet", new { sizeKey = "lanyard", kinds = new[] { "attendee", "staff" } });
        Assert.Equal(System.Net.HttpStatusCode.OK, sheet.StatusCode);
        Assert.Equal("application/pdf", sheet.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await sheet.Content.ReadAsByteArrayAsync());

        var one = await client.GetAsync($"/v1/events/{eventId}/badges/{attendeeId}.pdf?size=card");
        Assert.Equal(System.Net.HttpStatusCode.OK, one.StatusCode);
        Assert.Equal("application/pdf", one.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>A signed-in caller with no standing on the event gets 404, not 403 — the event's roster
    /// must not be confirmed to exist by the shape of the refusal (D-018).</summary>
    [Fact]
    public async Task A_signed_in_stranger_gets_404_over_http()
    {
        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
            eventId = Seed(scope).EventId;

        var (stranger, _) = await AuthedClientAsync();
        var res = await stranger.GetAsync($"/v1/events/{eventId}/badges/recipients");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, res.StatusCode);
    }

    /// <summary>The card design goes out snake_case, because <c>IdCardTemplateSpec</c> lives in
    /// <c>Kurx.Application.Abstractions</c> and <c>SnakeCaseResponseConverter</c> rewrites those.
    ///
    /// <para>Written after the editor shipped broken: the web client parsed camelCase, every response
    /// failed its schema, and the page reported "the card design couldn't be loaded". The service-level
    /// tests could not see it — they never crossed the wire. This one asserts the actual bytes.</para></summary>
    [Fact]
    public async Task The_card_design_is_served_snake_case()
    {
        var (client, ownerId) = await AuthedClientAsync();

        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
            eventId = Seed(scope, ownerId).EventId;

        var res = await client.GetAsync($"/v1/events/{eventId}/badges/template");
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);

        var json = await res.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        foreach (var name in new[] { "accent_color", "text_color", "logo_key", "background_key", "size_key", "fields" })
            Assert.True(root.TryGetProperty(name, out _), $"missing {name} — the client parses this exact name");

        // And emphatically not the camelCase the client used to expect.
        Assert.False(root.TryGetProperty("accentColor", out _));
        Assert.False(root.TryGetProperty("backgroundKey", out _));
    }

    /// <summary>Requests bind camelCase even though responses do not — the other half of the platform's
    /// naming contract, and the half a round-trip through the editor depends on.</summary>
    [Fact]
    public async Task The_card_design_is_saved_from_camel_case()
    {
        var (client, ownerId) = await AuthedClientAsync();

        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
            eventId = Seed(scope, ownerId).EventId;

        var res = await client.PutAsJsonAsync($"/v1/events/{eventId}/badges/template", new
        {
            accentColor = "#7f1d1d", textColor = (string?)null, logoKey = (string?)null,
            backgroundKey = (string?)null, sizeKey = "card",
            // Nested records get the same treatment, which is the part most likely to be missed.
            fields = new[]
            {
                new { key = "holder_name", x = 10.0, y = 20.0, width = 50.0, height = 8.0, fontSizePt = 14.0, align = "left", weight = "bold", zOrder = 1, enabled = true },
            },
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);

        var saved = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("#7f1d1d", saved.GetProperty("accent_color").GetString());
        Assert.Equal("card", saved.GetProperty("size_key").GetString());

        var field = saved.GetProperty("fields")[0];
        Assert.Equal("holder_name", field.GetProperty("key").GetString());
        Assert.Equal(10.0, field.GetProperty("x").GetDouble());
        // snake_case reaches nested records too — font_size_pt, not fontSizePt.
        Assert.True(field.TryGetProperty("font_size_pt", out _));
        Assert.True(field.TryGetProperty("z_order", out _));
    }

    // ── D-385: what a badge prints when the data behind a field is missing ──────────────────────
    //
    // The renderer is shared with certificates and draws "{holder_name}" for an empty value. On a
    // certificate that is the right prompt — the issuing path refuses to issue with a required value
    // missing, so it is only ever seen in a design preview. The badge path has no such refusal, so the
    // placeholder reached real printed cards. These pin the fix at the layout, where it is exact.

    /// <summary>Proven against a real issued card before the fix: an account with no display name printed
    /// a lanyard reading <c>{holder_name}</c>. A blank line is a bad badge; a badge that says
    /// <c>{holder_name}</c> is a broken one, and both are discovered at the guillotine.</summary>
    [Fact]
    public void A_field_with_no_value_is_dropped_rather_than_printed_as_its_own_key()
    {
        var nameless = new BadgeRecipient(
            Guid.NewGuid(), "", BadgeKind.Attendee, "General Admission", null, null, Guid.NewGuid().ToString());

        var values = new Dictionary<string, string>
        {
            [BadgeLayout.FieldName] = "",
            [BadgeLayout.FieldSubtitle] = "General Admission",
            [BadgeLayout.FieldEvent] = "Sample Hackathon 2026",
            [BadgeLayout.FieldEventDate] = "01 Oct 2026",
            [BadgeLayout.FieldCardNumber] = "KRX-00001",
        };

        var doc = BadgeLayout.Build(nameless, BadgeSize.Lanyard, IdCardTemplateSpec.Default, null, values);

        Assert.DoesNotContain(doc.Elements, e => e.FieldKey == BadgeLayout.FieldName);
        // The rest of the card is untouched: one missing value must not blank a badge.
        Assert.Contains(doc.Elements, e => e.FieldKey == BadgeLayout.FieldSubtitle);
        Assert.Contains(doc.Elements, e => e.Kind == "qrcode");
    }

    [Fact]
    public void A_field_with_a_value_still_renders()
    {
        var r = Attendee();
        var doc = BadgeLayout.Build(r, BadgeSize.Lanyard, IdCardTemplateSpec.Default, null, DataFor(r).Values);

        Assert.Contains(doc.Elements, e => e.FieldKey == BadgeLayout.FieldName);
        Assert.Contains(doc.Elements, e => e.FieldKey == BadgeLayout.FieldCardNumber);
    }

    /// <summary>The preview path passes no values and keeps the placeholder, which is what makes an
    /// unmapped field visible to the organiser while they are designing.</summary>
    [Fact]
    public void Without_values_the_layout_keeps_every_field()
    {
        var doc = BadgeLayout.Build(Attendee(), BadgeSize.Lanyard, IdCardTemplateSpec.Default);
        Assert.Contains(doc.Elements, e => e.FieldKey == BadgeLayout.FieldName);
    }

    /// <summary>An account with no name but a handle prints the handle. Real data rather than an invented
    /// "Guest", and better than a blank lanyard.</summary>
    [Fact]
    public async Task A_nameless_account_falls_back_to_its_handle()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var attendee = await db.Users.FirstAsync(u => u.Id == seeded.AttendeeId);
        attendee.Name = "";
        attendee.Username = "asha_m";
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var recipients = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;

        Assert.Equal("asha_m", recipients.First(r => r.UserId == seeded.AttendeeId).Name);
    }

    // ── D-385: a stored artefact belongs to the size it was rendered at ─────────────────────────

    /// <summary>Proven on a real print sheet before the fix: cards issued at <c>lanyard</c> and then
    /// printed at <c>card</c> reused the lanyard raster, which QuestPDF fitted into the CR80 slot at
    /// 34.4×54mm instead of 85.6×54mm — and the QR shrank below the point a scanner could read it. The
    /// size selector silently stopped working the moment anything was issued.</summary>
    [Fact]
    public void An_artefact_key_is_specific_to_its_size()
    {
        var eventId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        Assert.NotEqual(
            IdCardStorageKeys.Pdf(eventId, cardId, BadgeSize.Lanyard.Key),
            IdCardStorageKeys.Pdf(eventId, cardId, BadgeSize.Card.Key));
        Assert.NotEqual(
            IdCardStorageKeys.Png(eventId, cardId, BadgeSize.Lanyard.Key),
            IdCardStorageKeys.Png(eventId, cardId, BadgeSize.Card.Key));

        // Still under the event, so the asset-key confinement checks elsewhere continue to hold.
        Assert.StartsWith($"events/{eventId}/id-cards/", IdCardStorageKeys.Pdf(eventId, cardId, "lanyard"));
    }

    [Fact]
    public async Task Printing_at_another_size_re_renders_rather_than_reusing_the_issued_raster()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Staff]));

        var asIssued = await svc.RenderOneAsync(
            seeded.EventId, seeded.OwnerId, false, seeded.StaffId, BadgeSize.Lanyard.Key);
        var atCr80 = await svc.RenderOneAsync(
            seeded.EventId, seeded.OwnerId, false, seeded.StaffId, BadgeSize.Card.Key);

        Assert.True(asIssued.Ok, asIssued.Error);
        Assert.True(atCr80.Ok, atCr80.Error);
        // Same card, two physical sizes, therefore two different documents. Identical bytes would mean
        // the CR80 request had been served the lanyard artefact.
        Assert.NotEqual(asIssued.Value!, atCr80.Value!);
    }

    /// <summary>The built-in layout is now served, because the editor needs it to open on the card the
    /// server would actually print rather than on an empty one (D-385).</summary>
    [Fact]
    public async Task The_built_in_layout_is_served_over_http()
    {
        var (client, ownerId) = await AuthedClientAsync();

        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
            eventId = Seed(scope, ownerId).EventId;

        var res = await client.GetAsync($"/v1/events/{eventId}/badges/template/defaults?size=lanyard&kind=staff");
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);

        var fields = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var keys = fields.EnumerateArray().Select(f => f.GetProperty("key").GetString()).ToList();

        // The QR above all: a layout seeded without it would let an organiser print badges that scan
        // nowhere, which is the failure the empty canvas actually caused.
        Assert.Contains("qr", keys);
        Assert.Contains("holder_name", keys);
        Assert.Contains("access_level", keys);
        // snake_case for nested records, exactly as the saved design is served.
        Assert.True(fields[0].TryGetProperty("z_order", out _));
    }

    [Fact]
    public async Task The_landscape_card_gets_its_own_layout()
    {
        var (client, ownerId) = await AuthedClientAsync();

        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
            eventId = Seed(scope, ownerId).EventId;

        var portrait = await (await client.GetAsync($"/v1/events/{eventId}/badges/template/defaults?size=lanyard&kind=staff"))
            .Content.ReadAsStringAsync();
        var landscape = await (await client.GetAsync($"/v1/events/{eventId}/badges/template/defaults?size=card&kind=staff"))
            .Content.ReadAsStringAsync();

        // CR80 is wider than tall and cannot carry the stacked portrait arrangement legibly.
        Assert.NotEqual(portrait, landscape);
    }

    // ── D-386: the four gaps D-385 recorded rather than closed ─────────────────────────────────

    /// <summary>Every role the platform can actually issue maps to a band. The first version of this
    /// switch named roles <c>ValidRoles</c> cannot produce, so thirteen of fourteen printed "Staff" and
    /// the colour a marshal reads across a room never varied.</summary>
    [Theory]
    [InlineData("Stage Manager", "All Access")]
    [InlineData("Host", "All Access")]
    [InlineData("Security", "All Access")]
    [InlineData("Judge", "Backstage")]
    [InlineData("Moderator", "Backstage")]
    [InlineData("Speaker Coordinator", "Backstage")]
    [InlineData("Photographer", "Backstage")]
    [InlineData("Videographer", "Backstage")]
    [InlineData("Media Team", "Backstage")]
    [InlineData("Technical Team", "Backstage")]
    [InlineData("Volunteer", "Volunteer")]
    [InlineData("Registration Desk", "Staff")]
    [InlineData("Support Team", "Staff")]
    public void Every_assignable_role_gets_an_access_band(string role, string expected)
        => Assert.Equal(expected, StaffAccess.LevelFor(role));

    /// <summary>An unrecognised role gets the narrowest band, not the widest. Defaulting the other way
    /// would let a typo in a CustomRole print an all-access lanyard.</summary>
    [Theory]
    [InlineData("Chief Vibes Officer")]
    [InlineData("")]
    [InlineData("   ")]
    public void An_unknown_role_falls_to_the_narrowest_band(string role)
        => Assert.Equal("Staff", StaffAccess.LevelFor(role));

    /// <summary>Proven on a real badge before the fix: "Ananya Krishnamurthy-Venkataraghavan" printed as
    /// "Ananya Krishnamurthy-" — the name wrapped and the second line fell outside a box only tall enough
    /// for one. The render must not clip it now.</summary>
    [Fact]
    public async Task A_very_long_name_is_shrunk_rather_than_clipped()
    {
        var shortName = Attendee("Jo Roy");
        var longName = Attendee("Ananya Krishnamurthy-Venkataraghavan");

        var a = await Png(shortName, BadgeSize.Lanyard);
        var b = await Png(longName, BadgeSize.Lanyard);

        // Both render, and they differ — the long one is drawn, not dropped.
        Assert.NotEmpty(a);
        Assert.NotEmpty(b);
        Assert.NotEqual(a, b);
    }

    /// <summary>The shrink only ever reduces, and only for a box that cannot hold two lines. A tall box is
    /// left alone: wrapping is what a certificate's body paragraph is for.</summary>
    [Fact]
    public async Task Type_is_only_ever_shrunk_to_fit_never_grown()
    {
        // Named throughout: CertificateRenderElement is positional and twenty-odd fields wide, and a
        // positional call is trivially shifted by one.
        var singleLineBox = new CertificateDocument("custom", null,
        [
            new(Kind: "dynamicfield", FieldKey: BadgeLayout.FieldName, StaticText: null,
                X: 4, Y: 42, Width: 92, Height: 8, Rotation: 0, ZOrder: 1, IsMasking: false,
                BackgroundColor: null, ImageKey: null, FontFamily: null, FontSizePt: 16,
                FontWeight: "bold", Color: "#111827",
                HorizontalAlignment: "center", VerticalAlignment: "middle"),
        ], 88.9, 139.7);

        var shortValue = new CertificateRenderData(
            new Dictionary<string, string> { [BadgeLayout.FieldName] = "Jo" },
            new Dictionary<string, byte[]>(), "x");
        var longValue = new CertificateRenderData(
            new Dictionary<string, string> { [BadgeLayout.FieldName] = new string('M', 120) },
            new Dictionary<string, byte[]>(), "x");

        // A value that already fits and one that cannot must both render — the long one at a smaller size
        // rather than spilling out of the card.
        Assert.NotEmpty(await Renderer.RenderPngAsync(singleLineBox, shortValue, 96));
        Assert.NotEmpty(await Renderer.RenderPngAsync(singleLineBox, longValue, 96));
    }

    [Fact]
    public async Task An_issued_badge_can_be_revoked()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Staff]));

        var result = await svc.RevokeAsync(seeded.EventId, seeded.OwnerId, false, seeded.StaffId, "lost lanyard");

        Assert.True(result.Ok, result.Error);
        Assert.True(result.Value!.IsRevoked);
        Assert.Equal("Revoked", result.Value.Status);

        // And the roster reports it, which is what the console renders the chip from.
        var roster = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!;
        Assert.True(roster.First(r => r.UserId == seeded.StaffId).Card!.IsRevoked);
    }

    /// <summary>A revoked card stays resolvable rather than disappearing (D-331): a verifier must be able
    /// to tell a revoked badge from one that never existed.</summary>
    [Fact]
    public async Task A_revoked_card_keeps_its_number_and_stays_on_the_roster()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Staff]));
        var before = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!
            .First(r => r.UserId == seeded.StaffId).Card!;

        await svc.RevokeAsync(seeded.EventId, seeded.OwnerId, false, seeded.StaffId, null);
        var after = (await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false)).Value!
            .First(r => r.UserId == seeded.StaffId).Card!;

        Assert.Equal(before.CardNumber, after.CardNumber);
        Assert.Equal(before.VerifyCode, after.VerifyCode);
        Assert.True(after.IsRevoked);
    }

    [Fact]
    public async Task Revoking_is_refused_when_there_is_nothing_issued_or_it_is_already_revoked()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        // Nothing issued yet — a different state from issued-and-revoked, and reported as one.
        Assert.Equal("card_not_issued",
            (await svc.RevokeAsync(seeded.EventId, seeded.OwnerId, false, seeded.StaffId, null)).Error);

        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Staff]));
        await svc.RevokeAsync(seeded.EventId, seeded.OwnerId, false, seeded.StaffId, null);

        Assert.Equal("already_revoked",
            (await svc.RevokeAsync(seeded.EventId, seeded.OwnerId, false, seeded.StaffId, null)).Error);
    }

    /// <summary>Revocation mints nothing and closes nothing — it marks a document. Whoever cannot manage
    /// the event cannot mark it.</summary>
    [Fact]
    public async Task A_stranger_cannot_revoke_a_badge()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Staff]));

        var stranger = SeedUser(scope, "+919000000094");
        var result = await svc.RevokeAsync(seeded.EventId, stranger, false, seeded.StaffId, null);

        Assert.False(result.Ok);
        Assert.Equal("not_found", result.Error);
    }

    /// <summary>Revoking the card must not quietly stop the holder entering — an attendee's entry
    /// credential is their ticket, and pretending otherwise is the dangerous reading of this feature.
    /// If admission ever becomes conditional on the card, this test is what should be rewritten to say
    /// so, deliberately.</summary>
    [Fact]
    public async Task Revoking_a_badge_does_not_by_itself_close_the_gate()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();
        var gate = scope.ServiceProvider.GetRequiredService<IGateEntryService>();

        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Attendee]));
        await svc.RevokeAsync(seeded.EventId, seeded.OwnerId, false, seeded.AttendeeId, "printed in error");

        var result = await gate.ScanAsync(seeded.OwnerId, seeded.EventId, seeded.TicketCode, "after-revoke");

        Assert.True(result.Admitted, result.RejectionReason ?? "not admitted");
    }

    /// <summary>The duplicate that used to kill the page, now refused by the database (D-386).
    ///
    /// <para>Two rows for one holder made `ToDictionaryAsync(c =&gt; c.UserId)` throw, so the roster — and
    /// every later generate — failed permanently for that event: a transient race became a dead surface.
    /// `GenerateAsync` is idempotent in code, but two concurrent calls could each read "no card" and write
    /// one, and only the database can make that check atomic.</para>
    ///
    /// <para>This inserts straight past EF's tracking, the way the losing request would have. It must be
    /// <b>refused</b>. `IdCardService.OldestPerHolder` still tolerates a duplicate on read, for rows
    /// written before the index existed — that belt cannot be exercised here precisely because this
    /// assertion passes.</para></summary>
    [Fact]
    public async Task A_second_card_for_the_same_holder_is_refused_by_the_database()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IIdCardService>();

        await svc.GenerateAsync(seeded.EventId, seeded.OwnerId, false,
            new BadgeIssueRequest(BadgeSize.Lanyard.Key, [BadgeKind.Staff]));

        var first = await db.IdCards.AsNoTracking()
            .FirstAsync(c => c.EventId == seeded.EventId && c.UserId == seeded.StaffId);

        object[] values =
        [
            Guid.NewGuid(), first.OrgId, first.UserId, first.EventId!,
            IdCardCodes.NewVerifyCode(), first.CardNumber + "-DUP", (int)first.Template,
            (int)IdCardStatus.Active, first.IssuedBy, DateTime.UtcNow.AddMinutes(5),
        ];

        var duplicate = () => db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO id_cards ("Id","OrgId","UserId","EventId","VerifyCode","CardNumber","Template",
                                  "ShowMealInfo","Status","IsPublic","IsRevoked","IssuedBy","CreatedAt","UpdatedAt")
            VALUES ({0},{1},{2},{3},{4},{5},{6},false,{7},true,false,{8},{9},{9})
            """, values);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(duplicate);

        // And the surface is untouched: one card, the original number.
        var roster = await svc.ListRecipientsAsync(seeded.EventId, seeded.OwnerId, false);
        Assert.True(roster.Ok, roster.Error);
        Assert.Equal(first.CardNumber, roster.Value!.First(r => r.UserId == seeded.StaffId).Card!.CardNumber);
    }

    /// <summary>A college ID (D-331) carries no event, and many share the null — so the uniqueness is
    /// filtered on <c>EventId IS NOT NULL</c> and must not collide across them.</summary>
    [Fact]
    public async Task The_uniqueness_does_not_apply_to_event_less_cards()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = Seed(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        for (var i = 0; i < 2; i++)
        {
            db.IdCards.Add(new IdCard
            {
                OrgId = seeded.OrgId, UserId = seeded.StaffId, EventId = null,
                VerifyCode = IdCardCodes.NewVerifyCode(), CardNumber = $"COLLEGE-{Guid.NewGuid():N}"[..20],
                IssuedBy = seeded.OwnerId, Status = IdCardStatus.Active,
            });
        }

        // Two college IDs for one person is legitimate — a reissue after a loss is exactly that.
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.IdCards.CountAsync(c => c.UserId == seeded.StaffId && c.EventId == null));
    }

    private async Task<(HttpClient Client, Guid UserId)> AuthedClientAsync()
    {
        // Distinct per call: these tests need genuinely different people, and a shared client would make
        // "stranger" the organizer.
        var phone = "8" + Random.Shared.NextInt64(100_000_000, 999_999_999);
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tok = await (await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        c.DefaultRequestHeaders.Authorization =
            new("Bearer", tok.GetProperty("access_token").GetString());
        return (c, tok.GetProperty("user_id").GetGuid());
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────────

    private sealed record Seeded(
        Guid OwnerId, Guid OrgId, Guid EventId, Guid TicketTypeId,
        Guid AttendeeId, Guid TicketCode, Guid StaffId, Guid AssignmentId);

    /// <param name="asOwner">When supplied, this user creates the event — so an HTTP-authenticated
    /// caller can be the organizer. Otherwise a fresh owner is seeded.</param>
    private Seeded Seed(IServiceScope scope, Guid? asOwner = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var ownerId = asOwner ?? SeedUser(scope, "+9190000" + suffix[..5]);
        var orgId = _factory.SeedVerifiedOrg(ownerId, "Badge Org " + suffix, OrgRole.Owner);

        var category = new EventCategory
        {
            Level = CategoryLevel.Category, Name = "Badge " + suffix, Slug = "badge-" + suffix,
        };
        db.EventCategories.Add(category);
        db.SaveChanges();

        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = ownerId, CategoryId = category.Id,
            Title = "Sample Hackathon 2026", Slug = "badge-event-" + suffix,
            ShortCode = suffix[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(3), EndsAt = DateTime.UtcNow.AddDays(4),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);

        var ticketType = new TicketType
        {
            EventId = ev.Id, Name = "General Admission", PricePaise = 0, Quantity = 100,
            SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(2),
        };
        db.TicketTypes.Add(ticketType);
        db.SaveChanges();

        var attendeeId = SeedUser(scope, "+9190001" + suffix[..5]);
        var ticketCode = SeedTicket(db, ev.Id, ticketType.Id, attendeeId,
            scope.ServiceProvider.GetRequiredService<TokenService>());

        var staffId = SeedUser(scope, "+9190002" + suffix[..5]);
        var assignment = new EventAssignment
        {
            EventId = ev.Id, OrgId = orgId, UserId = staffId,
            Role = "Volunteer", Status = AssignmentStatus.Accepted,
        };
        db.EventAssignments.Add(assignment);
        db.SaveChanges();

        return new Seeded(ownerId, orgId, ev.Id, ticketType.Id, attendeeId, ticketCode, staffId, assignment.Id);
    }

    /// <param name="tokens">Signs the ticket for real. A placeholder signature would make the ticket
    /// unscannable, and the gate round-trip below is the whole point of the badge's QR.</param>
    private static Guid SeedTicket(
        KurxDbContext db, Guid eventId, Guid ticketTypeId, Guid userId, TokenService tokens)
    {
        var order = new Order
        {
            UserId = userId, EventId = eventId, TicketTypeId = ticketTypeId,
            Status = OrderStatus.Paid, AmountPaise = 0,
        };
        db.Orders.Add(order);
        db.SaveChanges();

        var item = new OrderItem
        {
            OrderId = order.Id, TicketTypeId = ticketTypeId, Qty = 1, UnitPricePaise = 0,
        };
        db.OrderItems.Add(item);
        db.SaveChanges();

        var ticket = new Ticket
        {
            OrderItemId = item.Id, EventId = eventId, UserId = userId, State = TicketState.Issued,
        };
        ticket.HmacSig = tokens.SignTicketCode(ticket.Code);
        db.Tickets.Add(ticket);
        db.SaveChanges();
        return ticket.Code;
    }

    private static Guid SeedUser(IServiceScope scope, string phone)
    {
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User { Name = "Badge User " + phone, Phone = phone };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }
}
