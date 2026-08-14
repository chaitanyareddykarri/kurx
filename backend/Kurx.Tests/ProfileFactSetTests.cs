using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-224 — the shared profile fact-set. These test the two properties the design depends on,
/// which nothing else would catch:
///
/// <list type="number">
/// <item><b>Memoisation per request.</b> If the loader stopped memoising, every section would silently
/// re-load and the whole point of the refactor would be lost with no visible symptom.</item>
/// <item><b>The private-event invariant, enforced once at load.</b> The rule used to be repeated in
/// every query that touched an event; it now lives in one place, so it needs one decisive test —
/// a private event's facts must not enter the fact-set at all.</item>
/// </list>
///
/// <para>Journey behaviour itself is covered by <see cref="ProfileVerifiedSourcesTests"/>, which is
/// deliberately left untouched by this refactor: those tests passing unchanged is the regression
/// proof that moving the engine onto the fact-set changed no output.</para></summary>
public class ProfileFactSetTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public ProfileFactSetTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9201{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<Guid> LoginAsync()
    {
        var phone = NextPhone();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        return tokens.GetProperty("user_id").GetGuid();
    }

    /// <summary>Seeds one event with the given visibility plus a public participation on it.</summary>
    private async Task<Guid> SeedEventWithParticipationAsync(
        KurxDbContext db, Guid userId, EventVisibility visibility)
    {
        var org = new Organization
        {
            Name = "Fact Org " + Guid.NewGuid().ToString("N")[..6],
            Slug = "factorg" + Guid.NewGuid().ToString("N")[..10],
        };
        db.Organizations.Add(org);
        var cat = await db.EventCategories.FirstOrDefaultAsync(c => c.Slug == "fact-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Fact Cat", Slug = "fact-cat" };
            db.EventCategories.Add(cat);
        }
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = org.Id, CategoryId = cat.Id, CreatedBy = userId,
            Title = "Fact Event " + Guid.NewGuid().ToString("N")[..5],
            Slug = "factev-" + Guid.NewGuid().ToString("N")[..10],
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            City = "Hyderabad", Status = EventStatus.Published, Visibility = visibility,
            StartsAt = DateTime.UtcNow.AddDays(-10), EndsAt = DateTime.UtcNow.AddDays(-9),
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        db.EventParticipants.Add(new EventParticipant
        {
            EventId = ev.Id, SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
            RoleSlug = "speaker", State = ParticipantState.Completed,
            Visibility = ParticipantVisibility.Public,
        });
        await db.SaveChangesAsync();
        return ev.Id;
    }

    /// <summary>Two loads inside one scope must return the same instance. Reference equality is the
    /// cheapest unambiguous proof that the second call issued no queries.</summary>
    [Fact]
    public async Task The_loader_memoises_within_one_request_scope()
    {
        var userId = await LoginAsync();

        using var scope = _factory.Services.CreateScope();
        var loader = scope.ServiceProvider.GetRequiredService<IProfileFactSetLoader>();

        var first = await loader.LoadAsync(userId);
        var second = await loader.LoadAsync(userId);

        Assert.Same(first, second);
    }

    /// <summary>Different people must not share a memo entry — <c>ConnectionEngine</c> will load two
    /// in one request, so a single-field cache would silently hand back the wrong person's facts.</summary>
    [Fact]
    public async Task The_memo_is_keyed_by_user()
    {
        var a = await LoginAsync();
        var b = await LoginAsync();

        using var scope = _factory.Services.CreateScope();
        var loader = scope.ServiceProvider.GetRequiredService<IProfileFactSetLoader>();

        var factsA = await loader.LoadAsync(a);
        var factsB = await loader.LoadAsync(b);

        Assert.NotSame(factsA, factsB);
        Assert.Equal(a, factsA.UserId);
        Assert.Equal(b, factsB.UserId);
    }

    /// <summary>A separate scope is a separate request, so it reloads — the fact-set is deliberately
    /// per-request, not a cross-request cache.</summary>
    [Fact]
    public async Task A_new_scope_reloads()
    {
        var userId = await LoginAsync();

        ProfileFactSet first, second;
        using (var scope = _factory.Services.CreateScope())
            first = await scope.ServiceProvider.GetRequiredService<IProfileFactSetLoader>().LoadAsync(userId);
        using (var scope = _factory.Services.CreateScope())
            second = await scope.ServiceProvider.GetRequiredService<IProfileFactSetLoader>().LoadAsync(userId);

        Assert.NotSame(first, second);
        Assert.Equal(first.UserId, second.UserId);
    }

    /// <summary>The invariant that used to be repeated in every event query. A private event, and
    /// every fact hanging off it, must be absent from the fact-set entirely — not filtered later by
    /// each consumer.</summary>
    [Fact]
    public async Task A_private_event_and_its_facts_never_enter_the_fact_set()
    {
        var userId = await LoginAsync();

        Guid publicEventId, privateEventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            publicEventId = await SeedEventWithParticipationAsync(db, userId, EventVisibility.Listed);
            privateEventId = await SeedEventWithParticipationAsync(db, userId, EventVisibility.InviteOnly);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var facts = await scope.ServiceProvider.GetRequiredService<IProfileFactSetLoader>().LoadAsync(userId);

            Assert.True(facts.Events.ContainsKey(publicEventId));
            Assert.False(facts.Events.ContainsKey(privateEventId));

            // The participation on the private event must be gone too, not merely unresolvable —
            // otherwise a consumer indexing Events[fact.EventId] would throw.
            Assert.Contains(facts.Participations, p => p.EventId == publicEventId);
            Assert.DoesNotContain(facts.Participations, p => p.EventId == privateEventId);
        }
    }

    /// <summary>Every fact must resolve against <c>Events</c>, so engines can index it directly. This
    /// is the contract that lets the Journey write <c>facts.Events[p.EventId]</c> without a guard.</summary>
    [Fact]
    public async Task Every_event_referencing_fact_resolves_against_the_events_map()
    {
        var userId = await LoginAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await SeedEventWithParticipationAsync(db, userId, EventVisibility.Listed);
            await SeedEventWithParticipationAsync(db, userId, EventVisibility.InviteOnly);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var facts = await scope.ServiceProvider.GetRequiredService<IProfileFactSetLoader>().LoadAsync(userId);

            foreach (var id in facts.Participations.Select(p => p.EventId)
                         .Concat(facts.Assignments.Select(a => a.EventId))
                         .Concat(facts.Attendance.Select(a => a.EventId))
                         .Concat(facts.Sessions.Select(s => s.EventId))
                         .Concat(facts.Results.Select(r => r.EventId)))
            {
                Assert.True(facts.Events.ContainsKey(id), $"fact references event {id} which is not in Events");
            }
        }
    }

    /// <summary>The fact-set is the person's facts, not one viewer's view of them — privacy is applied
    /// by the engines at emission. If a viewer parameter ever appears on the loader, this design has
    /// been broken and the resolver is no longer the single privacy boundary.</summary>
    [Fact]
    public void The_loader_contract_takes_no_viewer()
    {
        var method = typeof(IProfileFactSetLoader).GetMethod(nameof(IProfileFactSetLoader.LoadAsync))!;
        var parameterNames = method.GetParameters().Select(p => p.Name).ToList();

        Assert.Equal(new[] { "userId", "ct" }, parameterNames);
    }
}
