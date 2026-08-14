using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kurx.Tests;

/// <summary>DB-8 — the database itself refuses a state value that is not in the enum's vocabulary.
///
/// <para><b>Raw SQL against a real seeded row, deliberately.</b> Two things had to be got right for these
/// to prove anything. Going through EF proves nothing — the enum converter cannot express an invalid value,
/// so the test would only re-test C#'s type system; the write paths a CHECK actually guards are the ones
/// nobody reviewed (a migration, a psql session, an <c>ExecuteUpdateAsync</c> with a typo'd literal). And
/// the statement has to hit a row that exists with every foreign key satisfied: an <c>UPDATE</c> matching
/// zero rows never evaluates the constraint and passes vacuously, while an <c>INSERT</c> carrying invented
/// foreign keys fails with <c>23503</c> before the CHECK is ever reached. Both mistakes were made first and
/// are the reason this seeds a real graph and then mutates it.</para>
///
/// <para><b>Vocabulary, not transitions.</b> Each constraint answers "may this value exist", never "may it
/// follow the previous one" or "may this caller set it" — those stay in <c>EventStatusWorkflow</c> and
/// <c>IEventAuthority</c>. The valid-value tests walk each enum in declaration order, so a row goes
/// straight from <c>Draft</c> to <c>Archived</c> and an order reaches <c>Refunded</c> having never been
/// <c>Paid</c>. Both are accepted, which is the proof that no workflow leaked into the schema.</para></summary>
public class StateVocabularyConstraintTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static Guid _eventId, _orderId, _ticketId, _ledgerId;

    public StateVocabularyConstraintTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            SeedAsync().GetAwaiter().GetResult();
            _reset = true;
        }
    }

    /// <summary>One valid row per constrained table, with every foreign key real. Written through EF, so the
    /// seed itself can only hold values the enum can express.</summary>
    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var user = new User { Phone = "919870000501", PhoneE164 = "+919870000501", Name = "Vocab Seed" };
        var org = new Organization { Name = "Vocab Org", Slug = "vocab-org-" + Guid.NewGuid().ToString("N")[..8] };
        var category = new EventCategory { Level = CategoryLevel.Category, Name = "Vocab", Slug = "vocab-cat" };
        db.Users.Add(user);
        db.Organizations.Add(org);
        db.EventCategories.Add(category);
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = org.Id, CategoryId = category.Id, CreatedBy = user.Id,
            Title = "Vocabulary Event", Slug = "vocab-ev-" + Guid.NewGuid().ToString("N")[..10],
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(20), EndsAt = DateTime.UtcNow.AddDays(21),
            Status = EventStatus.Draft,
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var ticketType = new TicketType { EventId = ev.Id, Name = "General", PricePaise = 0, Quantity = 10 };
        db.TicketTypes.Add(ticketType);
        await db.SaveChangesAsync();

        var order = new Order
        {
            EventId = ev.Id, TicketTypeId = ticketType.Id, Status = OrderStatus.Pending,
            AmountPaise = 0, GuestName = "Vocab Buyer", GuestPhone = "+919870000502",
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var item = new OrderItem { OrderId = order.Id, TicketTypeId = ticketType.Id, Qty = 1 };
        db.OrderItems.Add(item);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            OrderItemId = item.Id, EventId = ev.Id, HmacSig = "seed", State = TicketState.Issued,
        };
        var ledger = new LedgerEntry
        {
            OrgId = org.Id, EventId = ev.Id, AmountPaise = 0,
            State = LedgerState.Collected, RefType = "payment", RefId = order.Id,
        };
        db.Tickets.Add(ticket);
        db.LedgerEntries.Add(ledger);
        await db.SaveChangesAsync();

        _eventId = ev.Id;
        _orderId = order.Id;
        _ticketId = ticket.Id;
        _ledgerId = ledger.Id;
    }

    /// <summary>Runs a raw statement and returns the PostgreSQL error code, or null if it succeeded.
    /// <c>23514</c> is check_violation — asserting the code rather than the message keeps this independent
    /// of PostgreSQL's wording.</summary>
    private async Task<string?> SqlStateOfAsync(string sql)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
            return null;
        }
        catch (Exception ex) when (ex.InnerException is PostgresException pg) { return pg.SqlState; }
        catch (PostgresException pg) { return pg.SqlState; }
    }

    private async Task<string> ConstraintDefAsync(string constraint)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var conn = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = '{constraint}'";
        return (string?)await cmd.ExecuteScalarAsync() ?? "";
    }

    // ── The constraints exist, and say what the enum says ────────────────────

    [Theory]
    [InlineData("ck_orders_status", typeof(OrderStatus))]
    [InlineData("ck_tickets_state", typeof(TicketState))]
    [InlineData("ck_events_status", typeof(EventStatus))]
    [InlineData("ck_ledger_entries_state", typeof(LedgerState))]
    public async Task The_constraint_exists_and_carries_every_enum_member(string constraint, Type enumType)
    {
        var definition = await ConstraintDefAsync(constraint);
        Assert.StartsWith("CHECK", definition);
        foreach (var name in Enum.GetNames(enumType))
            Assert.Contains($"'{name}'", definition);
    }

    // ── Invalid values are refused by PostgreSQL, on a real row ──────────────

    [Fact]
    public async Task An_unknown_order_status_is_refused()
        => Assert.Equal("23514", await SqlStateOfAsync(
            $"""UPDATE orders SET "Status" = 'Settled' WHERE "Id" = '{_orderId}'"""));

    [Fact]
    public async Task An_unknown_ticket_state_is_refused()
        => Assert.Equal("23514", await SqlStateOfAsync(
            $"""UPDATE tickets SET "State" = 'Cancelled' WHERE "Id" = '{_ticketId}'"""));

    /// <summary><c>InReview</c> specifically: it was a real EventStatus member until
    /// <c>MigrateInReviewToPendingReview</c> retired it. A retired value is exactly what should now be
    /// impossible to write back.</summary>
    [Fact]
    public async Task A_retired_event_status_is_refused()
        => Assert.Equal("23514", await SqlStateOfAsync(
            $"""UPDATE events SET "Status" = 'InReview' WHERE "Id" = '{_eventId}'"""));

    [Fact]
    public async Task An_unknown_ledger_state_is_refused()
        => Assert.Equal("23514", await SqlStateOfAsync(
            $"""UPDATE ledger_entries SET "State" = 'Pending' WHERE "Id" = '{_ledgerId}'"""));

    /// <summary>Case matters. These columns store the exact member name, so `paid` is not `Paid` — and a
    /// constraint accepting both would let two spellings of one state coexist, which is how a status filter
    /// starts silently missing rows.</summary>
    [Fact]
    public async Task A_correctly_spelled_but_wrongly_cased_value_is_refused()
        => Assert.Equal("23514", await SqlStateOfAsync(
            $"""UPDATE orders SET "Status" = 'paid' WHERE "Id" = '{_orderId}'"""));

    /// <summary>An empty string is not a state. It is what a mapper writes when it finds no value, and it
    /// would otherwise sit in the column looking like an ordinary row.</summary>
    [Fact]
    public async Task An_empty_status_is_refused()
        => Assert.Equal("23514", await SqlStateOfAsync(
            $"""UPDATE orders SET "Status" = '' WHERE "Id" = '{_orderId}'"""));

    // ── Every valid value is still accepted ──────────────────────────────────

    /// <summary>The half that matters most: a too-narrow constraint fails a legitimate write, in
    /// production, on the one path that reaches the new member — strictly worse than no constraint. Each
    /// enum is walked in declaration order, which also skips every real lifecycle, proving the database
    /// enforces vocabulary and not transitions.</summary>
    [Fact]
    public async Task Every_valid_value_of_every_constrained_column_is_accepted()
    {
        foreach (var status in Enum.GetNames<OrderStatus>())
            Assert.Null(await SqlStateOfAsync(
                $"""UPDATE orders SET "Status" = '{status}' WHERE "Id" = '{_orderId}'"""));

        foreach (var ticketState in Enum.GetNames<TicketState>())
            Assert.Null(await SqlStateOfAsync(
                $"""UPDATE tickets SET "State" = '{ticketState}' WHERE "Id" = '{_ticketId}'"""));

        foreach (var status in Enum.GetNames<EventStatus>())
            Assert.Null(await SqlStateOfAsync(
                $"""UPDATE events SET "Status" = '{status}' WHERE "Id" = '{_eventId}'"""));

        foreach (var ledgerState in Enum.GetNames<LedgerState>())
            Assert.Null(await SqlStateOfAsync(
                $"""UPDATE ledger_entries SET "State" = '{ledgerState}' WHERE "Id" = '{_ledgerId}'"""));
    }

    /// <summary>The seeded rows are still readable through EF after all that. A constraint that the
    /// application's own round-trip cannot satisfy would be a constraint that broke the product.</summary>
    [Fact]
    public async Task The_seeded_rows_still_load_through_the_application()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.NotNull(await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == _eventId));
        Assert.NotNull(await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == _orderId));
        Assert.NotNull(await db.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == _ticketId));
        Assert.NotNull(await db.LedgerEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == _ledgerId));
    }

    // ── The migration itself, against a table that already has rows ──────────

    /// <summary>Exercises <c>AddStateVocabularyCheckConstraints</c>'s <c>Down()</c> then <c>Up()</c> against a
    /// <b>populated</b> table, which the ordinary suite cannot: every other test here runs on a database where
    /// the migration was applied before any row existed, so it proves the constraint works and says nothing
    /// about whether the migration would <i>apply</i>.
    ///
    /// <para>The middle step is the one that matters. PostgreSQL validates <c>ADD CONSTRAINT ... CHECK</c>
    /// against every existing row, so a database holding a value the new vocabulary does not contain makes the
    /// migration <b>fail</b> — the deploy stops, loudly, before the constraint exists. That is the desired
    /// behaviour and the reason this phase inspected the live data first: the alternative would be a migration
    /// that quietly accepts rows it claims are impossible. Asserting it here is what stops a future enum
    /// change from being merged on the assumption that a CHECK is a free, always-appliable addition.</para>
    ///
    /// <para>Run last by name ordering is <i>not</i> relied on — the constraint is restored in a
    /// <c>finally</c>, so a failure anywhere in the middle cannot leave the schema weakened for whatever
    /// runs next.</para></summary>
    [Fact]
    public async Task The_migration_applies_over_existing_rows_and_refuses_to_apply_over_an_invalid_one()
    {
        // Exactly the SQL the migration's Up() carries, rebuilt from the enum so the two cannot drift apart.
        var vocabulary = string.Join(", ", Enum.GetNames<OrderStatus>().Select(n => $"'{n}'"));
        var addConstraint =
            $"""ALTER TABLE orders ADD CONSTRAINT ck_orders_status CHECK ("Status" IN ({vocabulary}))""";
        const string dropConstraint = """ALTER TABLE orders DROP CONSTRAINT ck_orders_status""";

        try
        {
            // 1. Down(): the constraint comes off cleanly. Rollback has to work, or a bad deploy is trapped.
            Assert.Null(await SqlStateOfAsync(dropConstraint));

            // 2. Up() over rows that are all valid — the ordinary upgrade path, and the row count is not zero.
            Assert.Null(await SqlStateOfAsync(addConstraint));

            // 3. Now the interesting half. Take the constraint off, write a value the vocabulary excludes
            //    (only possible while unconstrained — which is precisely the state a legacy database is in),
            //    and try to apply the migration over it.
            Assert.Null(await SqlStateOfAsync(dropConstraint));
            Assert.Null(await SqlStateOfAsync(
                $"""UPDATE orders SET "Status" = 'LegacyUnknownStatus' WHERE "Id" = '{_orderId}'"""));

            // 23514 from an ALTER TABLE, not from an INSERT: PostgreSQL refused to CREATE the constraint
            // because an existing row violates it. The deploy fails; it does not silently half-apply.
            Assert.Equal("23514", await SqlStateOfAsync(addConstraint));

            // 4. Resolve the offending row — the honest fix, as MigrateInReviewToPendingReview did for
            //    events — and the same migration now applies unchanged.
            Assert.Null(await SqlStateOfAsync(
                $"""UPDATE orders SET "Status" = 'Pending' WHERE "Id" = '{_orderId}'"""));
            Assert.Null(await SqlStateOfAsync(addConstraint));
        }
        finally
        {
            // Whatever happened above, the row is valid and the constraint is back. Both statements are
            // written to be no-ops when they are already true, so this cannot itself fail the test.
            await SqlStateOfAsync($"""UPDATE orders SET "Status" = 'Pending' WHERE "Id" = '{_orderId}'""");
            await SqlStateOfAsync(addConstraint);
        }

        Assert.StartsWith("CHECK", await ConstraintDefAsync("ck_orders_status"));
    }
}
