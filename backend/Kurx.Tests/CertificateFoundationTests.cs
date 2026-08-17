using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// The certificate module's persistent foundation (D-355, Phase 1).
///
/// <para>Three properties carry the weight. <b>A certificate id is unique under concurrency</b>, because
/// the obvious implementation is not and the failure only appears when two batches overlap.
/// <b>A recipient does not need an account</b>, because issuing to people who have never heard of Kurx is
/// the point. And <b>an issued id cannot change</b>, because an identifier that drifts is not one.</para>
///
/// <para>Against real Postgres through the repository's own factory, per the testing standard — the
/// concurrency and constraint behaviour under test is the database's, and a mocked context would assert
/// nothing about it.</para>
/// </summary>
public class CertificateFoundationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateFoundationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private sealed record Fixture(Guid EventId, Guid OtherEventId, Guid OwnerId, Guid TemplateId,
        string EventShortCode, string OtherEventShortCode);

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

    private async Task<Fixture> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var org = new Organization { Name = "Org " + suffix, Slug = "org" + suffix };
        var owner = new User { Phone = Phone(), Name = "Creator " + suffix };
        db.Organizations.Add(org);
        db.Users.Add(owner);

        var categoryId = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Category).Select(c => c.Id).FirstAsync();

        Event NewEvent(string title) => new()
        {
            Title = title,
            Slug = $"{title.ToLowerInvariant()}-{suffix}",
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d",
            VenueName = "v",
            RepresentingOrgId = org.Id,
            CreatedBy = owner.Id,
            CategoryId = categoryId,
            StartsAt = new DateTime(2026, 9, 12, 9, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc),
            Status = EventStatus.Published,
        };

        var mine = NewEvent("Mine");
        var theirs = NewEvent("Theirs");
        db.Events.AddRange(mine, theirs);

        var template = new CertificateTemplate
        {
            EventId = mine.Id,
            OwnerUserId = owner.Id,
            Name = "Uploaded design",
            PageSize = CertificatePageSize.A4Landscape,
            Status = CertificateTemplateStatus.Ready,
        };
        db.CertificateTemplates.Add(template);

        await db.SaveChangesAsync();
        return new Fixture(mine.Id, theirs.Id, owner.Id, template.Id, mine.ShortCode, theirs.ShortCode);
    }

    private async Task<Guid> RecipientAsync(Guid eventId, Guid? userId = null, string? email = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var recipient = new CertificateRecipient
        {
            EventId = eventId,
            FullName = "Ananya Rao",
            Email = email,
            NormalizedEmail = email?.Trim().ToLowerInvariant(),
            UserId = userId,
            LinkedAt = userId is null ? null : DateTime.UtcNow,
        };
        db.CertificateRecipients.Add(recipient);
        await db.SaveChangesAsync();
        return recipient.Id;
    }

    // ── Certificate id: formatting ──────────────────────────────────────────────────────────────

    /// <summary>Formatting is a pure domain function, so it is asserted without a database.</summary>
    [Theory]
    [InlineData("CERT", "{PREFIX}-{YYYY}-{SEQ}", 5, 1, "CERT-2026-00001")]
    [InlineData("CERT", "{PREFIX}-{YYYY}-{SEQ}", 5, 42, "CERT-2026-00042")]
    [InlineData("HACK", "{PREFIX}/{YY}/{SEQ}", 3, 7, "HACK/26/007")]
    [InlineData("X", "{SEQ}", 0, 9, "9")]
    public void An_id_follows_its_configured_prefix_pattern_and_padding(
        string prefix, string pattern, int padding, long sequence, string expected)
    {
        var rule = new CertificateIdRule { Prefix = prefix, Pattern = pattern, Padding = padding };

        Assert.Equal(expected, rule.Format(sequence, new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>Padding widens, it never truncates. A truncated id would collide with another, which is
    /// the one thing an identifier may not do.</summary>
    [Fact]
    public void A_sequence_wider_than_the_padding_is_not_truncated()
    {
        var rule = new CertificateIdRule { Prefix = "CERT", Padding = 3 };

        Assert.Equal("CERT-2026-123456", rule.Format(123456, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    // ── Certificate id: allocation ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Allocation_creates_the_events_rule_on_first_use_and_starts_at_one()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var first = await scope.ServiceProvider.GetRequiredService<ICertificateIdAllocator>()
            .AllocateAsync(f.EventId, new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, first.Sequence);
        // The default prefix is the event's short code, not a constant — see EnsureRuleAsync.
        Assert.Equal($"{f.EventShortCode}-2026-00001", first.CertificateId);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.True(await db.CertificateIdRules.AnyAsync(r => r.EventId == f.EventId));
    }

    [Fact]
    public async Task Sequential_allocations_advance()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var allocator = scope.ServiceProvider.GetRequiredService<ICertificateIdAllocator>();
        var at = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

        var ids = new List<string>();
        for (var i = 0; i < 5; i++) ids.Add((await allocator.AllocateAsync(f.EventId, at)).CertificateId);

        Assert.Equal(
            Enumerable.Range(1, 5).Select(i => $"{f.EventShortCode}-2026-{i:D5}"),
            ids);
    }

    /// <summary>Two events number independently — one organiser's run must not advance another's.</summary>
    [Fact]
    public async Task Each_event_has_its_own_sequence()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var allocator = scope.ServiceProvider.GetRequiredService<ICertificateIdAllocator>();
        var at = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

        var mineFirst = await allocator.AllocateAsync(f.EventId, at);
        await allocator.AllocateAsync(f.EventId, at);
        var other = await allocator.AllocateAsync(f.OtherEventId, at);

        Assert.Equal(1, other.Sequence);

        // Sequences are per-event but the id is unique PLATFORM-WIDE, so two events starting at 1 must
        // still produce different identifiers. A shared default prefix made every event's first
        // certificate collide with every other event's first.
        Assert.NotEqual(mineFirst.CertificateId, other.CertificateId);
    }

    /// <summary>The test this whole design exists for.
    ///
    /// <para>Read-modify-write passes every sequential test above and fails this one. Each task runs in
    /// its own scope — therefore its own DbContext and its own connection — so the allocations genuinely
    /// contend in Postgres rather than being serialised by a shared context.</para></summary>
    [Fact]
    public async Task Concurrent_allocation_never_issues_the_same_id_twice()
    {
        var f = await SeedAsync();
        const int concurrency = 40;
        var at = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

        var allocations = await Task.WhenAll(Enumerable.Range(0, concurrency).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ICertificateIdAllocator>()
                .AllocateAsync(f.EventId, at);
        }));

        Assert.Equal(concurrency, allocations.Select(a => a.CertificateId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(concurrency, allocations.Select(a => a.Sequence).Distinct().Count());
        // Contiguous from 1: no value skipped and none handed out twice.
        Assert.Equal(Enumerable.Range(1, concurrency).Select(i => (long)i), allocations.Select(a => a.Sequence).OrderBy(s => s));
    }

    /// <summary>The database is the final guarantee, independent of the allocator being correct.</summary>
    [Fact]
    public async Task The_database_refuses_a_duplicate_certificate_id()
    {
        var f = await SeedAsync();
        var recipientId = await RecipientAsync(f.EventId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        IssuedCertificate Make() => new()
        {
            CertificateId = "CERT-2026-09999",
            EventId = f.EventId,
            TemplateId = f.TemplateId,
            TemplateVersion = 1,
            RecipientId = recipientId,
        };

        db.IssuedCertificates.Add(Make());
        await db.SaveChangesAsync();

        using var second = _factory.Services.CreateScope();
        var db2 = second.ServiceProvider.GetRequiredService<KurxDbContext>();
        db2.IssuedCertificates.Add(Make());

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    // ── Certificate id: immutability ────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_issued_certificate_id_cannot_be_changed()
    {
        var f = await SeedAsync();
        var recipientId = await RecipientAsync(f.EventId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var certificate = new IssuedCertificate
        {
            CertificateId = "CERT-2026-00777",
            EventId = f.EventId,
            TemplateId = f.TemplateId,
            TemplateVersion = 1,
            RecipientId = recipientId,
        };
        db.IssuedCertificates.Add(certificate);
        await db.SaveChangesAsync();

        certificate.CertificateId = "CERT-2026-00778";

        // Throws rather than silently issuing an UPDATE. Everything a certificate asserts hangs off this
        // value, including the QR already printed on it.
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    // ── Recipients: with and without an account ─────────────────────────────────────────────────

    /// <summary>The confirmed requirement: issuance must never need a Kurx account.</summary>
    [Fact]
    public async Task A_recipient_can_exist_with_no_user_account()
    {
        var f = await SeedAsync();

        var id = await RecipientAsync(f.EventId, userId: null, email: "external@example.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var recipient = await db.CertificateRecipients.AsNoTracking().FirstAsync(r => r.Id == id);

        Assert.Null(recipient.UserId);
        Assert.Null(recipient.LinkedAt);
        Assert.Equal("external@example.com", recipient.NormalizedEmail);
    }

    [Fact]
    public async Task A_recipient_can_reference_a_registered_user()
    {
        var f = await SeedAsync();

        var id = await RecipientAsync(f.EventId, userId: f.OwnerId, email: "Creator@Example.COM");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var recipient = await db.CertificateRecipients.AsNoTracking().FirstAsync(r => r.Id == id);

        Assert.Equal(f.OwnerId, recipient.UserId);
        Assert.NotNull(recipient.LinkedAt);
        // The normalised form is what the linking rule matches on, so casing must not defeat it.
        Assert.Equal("creator@example.com", recipient.NormalizedEmail);
    }

    // ── Template fields ─────────────────────────────────────────────────────────────────────────

    /// <summary>Field keys are the organiser's vocabulary, not ours. A fixed enum would mean every new
    /// spreadsheet column needed a code change.</summary>
    [Theory]
    [InlineData("participant_name")]
    [InlineData("employee_grade")]
    [InlineData("नाम")]
    [InlineData("Column With Spaces")]
    public async Task An_arbitrary_field_key_is_accepted(string fieldKey)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var field = new CertificateTemplateField
        {
            TemplateId = f.TemplateId,
            Kind = CertificateFieldKind.DynamicField,
            FieldKey = fieldKey,
            Label = "Whatever the organiser calls it",
            X = 10, Y = 40, Width = 80, Height = 10,
            HorizontalAlignment = CertificateHorizontalAlignment.Center,
        };
        db.CertificateTemplateFields.Add(field);
        await db.SaveChangesAsync();

        Assert.Equal(fieldKey, (await db.CertificateTemplateFields.AsNoTracking()
            .FirstAsync(x => x.Id == field.Id)).FieldKey);
    }

    /// <summary>A masking field is how printed text is covered and replaced — the honest model for
    /// "editing" pixels. Its recorded intent and its sampled ground must both survive a round trip.</summary>
    [Fact]
    public async Task A_masking_field_persists_its_intent_and_its_sampled_ground()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var field = new CertificateTemplateField
        {
            TemplateId = f.TemplateId,
            Kind = CertificateFieldKind.Text,
            StaticText = "Certificate of Excellence",
            IsMasking = true,
            BackgroundColor = "#FFFFFF",
            X = 10, Y = 20, Width = 80, Height = 8,
            ZOrder = 3,
        };
        db.CertificateTemplateFields.Add(field);
        await db.SaveChangesAsync();

        var reloaded = await db.CertificateTemplateFields.AsNoTracking().FirstAsync(x => x.Id == field.Id);
        Assert.True(reloaded.IsMasking);
        Assert.Equal("#FFFFFF", reloaded.BackgroundColor);
        Assert.Equal(3, reloaded.ZOrder);
    }

    // ── Relationships and isolation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_certificate_requires_a_recipient_that_exists()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        db.IssuedCertificates.Add(new IssuedCertificate
        {
            CertificateId = "CERT-2026-00500",
            EventId = f.EventId,
            TemplateId = f.TemplateId,
            TemplateVersion = 1,
            RecipientId = Guid.NewGuid(),   // no such recipient
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_certificate_requires_a_template_that_exists()
    {
        var f = await SeedAsync();
        var recipientId = await RecipientAsync(f.EventId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        db.IssuedCertificates.Add(new IssuedCertificate
        {
            CertificateId = "CERT-2026-00501",
            EventId = f.EventId,
            TemplateId = Guid.NewGuid(),    // no such template
            TemplateVersion = 1,
            RecipientId = recipientId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>Deleting an event takes its certificate records with it, but must NOT take the template —
    /// a reusable design outlives the event it was first used on. That asymmetry is the reuse mechanism.</summary>
    [Fact]
    public async Task Deleting_an_event_keeps_the_creators_template()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        db.Events.Remove(await db.Events.FirstAsync(e => e.Id == f.EventId));
        await db.SaveChangesAsync();

        var template = await db.CertificateTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == f.TemplateId);
        Assert.NotNull(template);
        Assert.Null(template!.EventId);           // returned to the creator's library
        Assert.Equal(f.OwnerId, template.OwnerUserId);
    }

    /// <summary>An id rule belongs to exactly one event; a second is a schema-level impossibility rather
    /// than something the allocator has to defend against.</summary>
    [Fact]
    public async Task An_event_cannot_have_two_id_rules()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        db.CertificateIdRules.Add(new CertificateIdRule { EventId = f.EventId });
        await db.SaveChangesAsync();

        using var second = _factory.Services.CreateScope();
        var db2 = second.ServiceProvider.GetRequiredService<KurxDbContext>();
        db2.CertificateIdRules.Add(new CertificateIdRule { EventId = f.EventId });

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    /// <summary>The state vocabularies are enforced by CHECK constraints generated from the enums, so a
    /// value outside them cannot be written even by raw SQL.</summary>
    [Fact]
    public async Task A_status_outside_the_vocabulary_is_refused_by_the_database()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            """
            UPDATE certificate_templates SET "Status" = 'NotAStatus' WHERE "Id" = {0}
            """.Replace("{0}", $"'{f.TemplateId}'")));
    }

    /// <summary>Telemetry records what happened, not who. The dashboard needs counts; storing per-view
    /// identifying data on a public, unauthenticated surface would create an obligation nobody asked for.</summary>
    [Fact]
    public async Task Telemetry_carries_no_identifying_data()
    {
        var properties = typeof(CertificateEvent).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("IpAddress", properties);
        Assert.DoesNotContain("UserAgent", properties);
        Assert.DoesNotContain("UserId", properties);
        await Task.CompletedTask;
    }
}
