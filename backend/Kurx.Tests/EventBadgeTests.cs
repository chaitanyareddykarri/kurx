using System.Net.Http.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Certificates;
using Kurx.Infrastructure.IdCards;
using Kurx.Infrastructure.Persistence;
using Kurx.Infrastructure.Providers;
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

    /// <summary>The deferred half of D-362, pinned so it cannot regress into something worse than
    /// "unsupported". A staff pass is structurally not a ticket code, so it can never be mistaken for one
    /// by a scanner that parses a bare Guid — the gate refuses it rather than resolving it to some other
    /// event's ticket. When <c>staff_gate_entries</c> lands, this test is what gets replaced by an
    /// admission assertion.</summary>
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
