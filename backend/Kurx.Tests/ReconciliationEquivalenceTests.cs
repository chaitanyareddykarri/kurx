using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>DB-3: <see cref="IEventRegistrationService.ReconcileAsync"/> was rewritten from five COUNT
/// queries per event into four grouped aggregates. Reconciliation is a CORRECTNESS mechanism — it is how
/// registration drift is discovered at all — so the bar is not "faster", it is "identical output".
///
/// <para>Every test here computes the drift the ORIGINAL per-event COUNT logic would have produced, from
/// the same seeded data, and asserts the rewritten implementation matches it exactly. That is stronger than
/// asserting hand-written expected values: it pins the new code to the old semantics rather than to my
/// reading of them.</para></summary>
public class ReconciliationEquivalenceTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public ReconciliationEquivalenceTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    /// <summary>The ORIGINAL algorithm, transcribed verbatim from the pre-rewrite implementation: the event
    /// set from distinct order EventIds, then five per-event COUNTs with exactly these filters. Kept as the
    /// oracle so the comparison is against the shipped behaviour, not against a paraphrase of it.</summary>
    private static async Task<List<RegistrationDrift>> LegacyReconcileAsync(
        KurxDbContext db, Guid? eventId = null)
    {
        var eventIds = eventId is not null
            ? new List<Guid> { eventId.Value }
            : await db.Orders.AsNoTracking().Select(o => o.EventId).Distinct().ToListAsync();

        var drift = new List<RegistrationDrift>();
        foreach (var eid in eventIds)
        {
            var orders = await db.Orders.CountAsync(o => o.EventId == eid);
            var regs = await db.Registrations.CountAsync(r => r.EventId == eid);
            var activeTickets = await db.Tickets.CountAsync(t => t.EventId == eid && t.State != TicketState.Void);
            var activeAdms = await db.Admissions.CountAsync(a => a.EventId == eid && a.State != AdmissionState.Void);
            var missingCred = await db.Admissions.CountAsync(a =>
                a.EventId == eid && a.State != AdmissionState.Void && a.PersonId != null && a.CredentialId == null);
            if (orders != regs || activeTickets != activeAdms || missingCred != 0)
                drift.Add(new RegistrationDrift(eid, orders, regs, activeTickets, activeAdms, missingCred));
        }
        return drift;
    }

    private async Task AssertEquivalentAsync(Guid? eventId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEventRegistrationService>();

        var expected = (await LegacyReconcileAsync(db, eventId)).OrderBy(d => d.EventId).ToList();
        var actual = (await service.ReconcileAsync(eventId)).OrderBy(d => d.EventId).ToList();

        Assert.Equal(expected.Count, actual.Count);
        foreach (var (e, a) in expected.Zip(actual))
        {
            Assert.Equal(e.EventId, a.EventId);
            Assert.Equal(e.Orders, a.Orders);
            Assert.Equal(e.Registrations, a.Registrations);
            Assert.Equal(e.ActiveTickets, a.ActiveTickets);
            Assert.Equal(e.ActiveAdmissions, a.ActiveAdmissions);
            Assert.Equal(e.AdmissionsMissingCredential, a.AdmissionsMissingCredential);
        }
    }

    // ── Seeding helpers ─────────────────────────────────────────────────────────────────────────

    private async Task<(Guid EventId, Guid UserId)> SeedEventAsync(KurxDbContext db, string slug)
    {
        var user = new User { Phone = $"9198{slug}", PhoneE164 = $"+9198{slug}", Name = slug, Username = $"rec{slug}" };
        var org = new Organization { Name = $"Org {slug}", Slug = $"org-{slug}", IsPersonal = true };
        var cat = new EventCategory { Level = CategoryLevel.Category, Name = $"Cat {slug}", Slug = $"cat-{slug}" };
        db.Users.Add(user); db.Organizations.Add(org); db.EventCategories.Add(cat);
        await db.SaveChangesAsync();

        var ev = new Event
        {
            Title = $"Event {slug}", Slug = $"ev-{slug}", RepresentingOrgId = org.Id, CreatedBy = user.Id,
            CategoryId = cat.Id, StartsAt = DateTime.UtcNow.AddDays(7), EndsAt = DateTime.UtcNow.AddDays(8),
            ShortCode = $"RC{slug}",   // NOT NULL on events
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (ev.Id, user.Id);
    }

    private static void AddOrder(KurxDbContext db, Guid eventId, Guid userId) =>
        db.Orders.Add(new Order { EventId = eventId, UserId = userId, AmountPaise = 0, Status = OrderStatus.Paid });

    // ── Cases ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_orders_at_all_yields_no_drift()
    {
        await AssertEquivalentAsync();
    }

    [Fact]
    public async Task Explicit_event_with_no_orders_is_still_examined()
    {
        // The single-event path must examine the event even when it has no orders — the event set is
        // supplied, not derived. A GROUP BY returns no row for it, and reading that as "skip" instead of
        // "zero" would silently drop the caller's requested event.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var (eventId, _) = await SeedEventAsync(db, "0001");
        await AssertEquivalentAsync(eventId);
    }

    /// <summary>THE zero-row trap. An event with orders and NO registrations is real drift — exactly what
    /// this job exists to surface. GROUP BY produces no registrations row for it, so an implementation that
    /// skipped absent keys would report this event as healthy. It must appear, with Registrations = 0.</summary>
    [Fact]
    public async Task Event_with_orders_but_no_registrations_is_reported_as_drift()
    {
        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (id, userId) = await SeedEventAsync(db, "0002");
            eventId = id;
            for (var i = 0; i < 5; i++) AddOrder(db, eventId, userId);
            await db.SaveChangesAsync();
        }

        await AssertEquivalentAsync();

        using var verify = _factory.Services.CreateScope();
        var service = verify.ServiceProvider.GetRequiredService<IEventRegistrationService>();
        var drift = await service.ReconcileAsync(eventId);
        var row = Assert.Single(drift);
        Assert.Equal(5, row.Orders);
        Assert.Equal(0, row.Registrations);   // absent group must read as zero, never as "skip"
    }

    [Fact]
    public async Task Multiple_events_are_each_reported_independently()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (a, ua) = await SeedEventAsync(db, "0003");
            var (b, ub) = await SeedEventAsync(db, "0004");
            AddOrder(db, a, ua);
            AddOrder(db, b, ub); AddOrder(db, b, ub); AddOrder(db, b, ub);
            await db.SaveChangesAsync();
        }
        await AssertEquivalentAsync();
    }

    [Fact]
    public async Task Void_tickets_are_excluded_exactly_as_before()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, userId) = await SeedEventAsync(db, "0005");
            AddOrder(db, eventId, userId);
            await db.SaveChangesAsync();

            var order = await db.Orders.FirstAsync(o => o.EventId == eventId);
            var tt = new TicketType
            {
                EventId = eventId, Name = "GA", PricePaise = 0, Quantity = 100, PerUserLimit = 5,
            };
            db.TicketTypes.Add(tt);
            await db.SaveChangesAsync();

            var oi = new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = 1, UnitPricePaise = 0 };
            db.OrderItems.Add(oi);
            await db.SaveChangesAsync();

            // One void and one live ticket: proves the State != Void filter survived the rewrite.
            // HmacSig is NOT NULL (gate-signature column); reconciliation never reads it, so a fixed
            // placeholder is enough here and avoids pulling the real signer into a counting test.
            db.Tickets.Add(new Ticket
            {
                EventId = eventId, UserId = userId, OrderItemId = oi.Id,
                State = TicketState.Void, HmacSig = "test-sig-void",
            });
            db.Tickets.Add(new Ticket
            {
                EventId = eventId, UserId = userId, OrderItemId = oi.Id,
                State = TicketState.Issued, HmacSig = "test-sig-live",
            });
            await db.SaveChangesAsync();
        }
        await AssertEquivalentAsync();
    }

    /// <summary>A wider seeded set, so the grouped aggregates are exercised across many events at once
    /// rather than on a single-event shortcut — the shape the daily job actually runs.</summary>
    [Fact]
    public async Task Many_events_with_mixed_shapes_match_the_legacy_result()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            for (var i = 0; i < 25; i++)
            {
                var (eventId, userId) = await SeedEventAsync(db, $"01{i:D2}");
                for (var o = 0; o < i % 4; o++) AddOrder(db, eventId, userId);
            }
            await db.SaveChangesAsync();
        }
        await AssertEquivalentAsync();
    }
}
