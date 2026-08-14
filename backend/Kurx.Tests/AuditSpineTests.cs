using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Audit;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>M3a (D-102): the typed audit spine — a uniform <c>DetailsJson</c> envelope every subsystem
/// writes through, carrying the request correlation id and structured before/after state.
/// Deliberately exercised at the <b>service layer</b> (no HTTP login), so the audit contract stays
/// provable independently of the shared auth/OTP test helper.</summary>
public class AuditSpineTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public AuditSpineTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Audit Cat", Slug = "audit-cat" };
                db.EventCategories.Add(cat);
                db.SaveChanges();
                _categoryId = cat.Id;
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    // ── The on-disk envelope shape is a contract; pin it. ────────────────────
    [Fact]
    public void Envelope_shape_is_pinned()
    {
        var json = AuditWriter.Envelope(
            new AuditEvent("event.status_change", "events", Guid.Empty,
                Before: new { status = "Draft" },
                After: new { status = "Published", action = "publish" }),
            correlationId: "corr-123");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(AuditWriter.EnvelopeVersion, root.GetProperty("v").GetInt32());
        Assert.Equal("corr-123", root.GetProperty("correlation_id").GetString());
        Assert.Equal("Draft", root.GetProperty("before").GetProperty("status").GetString());
        Assert.Equal("Published", root.GetProperty("after").GetProperty("status").GetString());
        Assert.Equal("publish", root.GetProperty("after").GetProperty("action").GetString());
    }

    [Fact]
    public void Envelope_omits_absent_correlation_and_states()
    {
        var json = AuditWriter.Envelope(new AuditEvent("org.merge", "organizations", Guid.Empty), correlationId: null);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("correlation_id", out _));
        Assert.False(doc.RootElement.TryGetProperty("before", out _));
        Assert.False(doc.RootElement.TryGetProperty("after", out _));
        Assert.Equal(AuditWriter.EnvelopeVersion, doc.RootElement.GetProperty("v").GetInt32());
    }

    // ── A migrated call site really writes through the spine. ────────────────
    [Fact]
    public async Task Event_status_change_is_audited_through_the_typed_spine()
    {
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = new User { Phone = "9195" + Random.Shared.Next(1000000, 9999999), Name = "Audit Tester" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }
        var orgId = _factory.SeedVerifiedOrg(userId, "Audit Spine College");

        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<IEventService>();
            var created = await events.CreateAsync(userId, orgId, isAdmin: false, new CreateEventInput(
                Title: "Audit Fest", Subtitle: null, Description: "A detailed description for the audit test.",
                CategoryId: _categoryId, TypeId: null, AudienceLevelId: null, TemplateId: null, ParentEventId: null,
                Tags: null,
                VenueId: null, VenueName: "Main Hall", VenueAddress: null, City: "Vizag", Lat: null, Lng: null,
                StartsAt: DateTime.UtcNow.AddDays(20), EndsAt: DateTime.UtcNow.AddDays(20).AddHours(3), Timezone: null,
                Capacity: null, Visibility: null, Language: null,
                ContactEmail: null, ContactPhone: null, Website: null, SocialLinksJson: null));
            Assert.True(created.Ok, created.Error);
            eventId = created.Value!.Id;
        }

        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — this test is about the audit spine

        using (var scope = _factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<IEventService>();
            var published = await events.TransitionAsync(userId, eventId, isAdmin: false, isReviewer: false, "publish");
            Assert.True(published.Ok, published.Error);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var row = await db.AuditLogs.AsNoTracking()
                .Where(a => a.Entity == "events" && a.EntityId == eventId && a.Action == "event.status_change")
                .OrderByDescending(a => a.CreatedAt)
                .FirstAsync();

            Assert.Equal("user", row.ActorType);
            Assert.Equal(userId, row.ActorId);

            using var doc = JsonDocument.Parse(row.DetailsJson!);
            var root = doc.RootElement;
            Assert.Equal(AuditWriter.EnvelopeVersion, root.GetProperty("v").GetInt32());
            Assert.Equal("Draft", root.GetProperty("before").GetProperty("status").GetString());
            Assert.Equal("Published", root.GetProperty("after").GetProperty("status").GetString());
            Assert.Equal("publish", root.GetProperty("after").GetProperty("action").GetString());
        }
    }

    // The audit row must commit in the SAME transaction as the change it describes — a trail that can
    // commit separately from its subject is not a trail. A failed transition must leave no audit row.
    [Fact]
    public async Task A_refused_transition_writes_no_audit_row()
    {
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = new User { Phone = "9196" + Random.Shared.Next(1000000, 9999999), Name = "Audit Tester 2" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }
        var orgId = _factory.SeedVerifiedOrg(userId, "Refused Transition College");

        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<IEventService>();
            var created = await events.CreateAsync(userId, orgId, isAdmin: false, new CreateEventInput(
                Title: "Refused Fest", Subtitle: null, Description: "A detailed description for the audit test.",
                CategoryId: _categoryId, TypeId: null, AudienceLevelId: null, TemplateId: null, ParentEventId: null,
                Tags: null,
                VenueId: null, VenueName: "Main Hall", VenueAddress: null, City: "Vizag", Lat: null, Lng: null,
                StartsAt: DateTime.UtcNow.AddDays(20), EndsAt: DateTime.UtcNow.AddDays(20).AddHours(3), Timezone: null,
                Capacity: null, Visibility: null, Language: null,
                ContactEmail: null, ContactPhone: null, Website: null, SocialLinksJson: null));
            eventId = created.Value!.Id;

            // "close" is only valid from Published; from Draft it must be refused.
            var refused = await events.TransitionAsync(userId, eventId, isAdmin: false, isReviewer: false, "close");
            Assert.False(refused.Ok);
            Assert.Equal("invalid_transition", refused.Error);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.False(await db.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.Entity == "events" && a.EntityId == eventId && a.Action == "event.status_change"));
        }
    }
}
