using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-340: cloning an event must carry the organiser's configuration across. The clone used to be
/// built from a hand-maintained property list that named 47 of <c>Event</c>'s 110 columns, so every column
/// D-265 and D-266 added after it was written landed on its C# default in the copy — the legal terms, the
/// consent gate, the age and gender limits, the tax treatment, the registration policy. Measured against
/// the running API before the fix: 30 columns silently blanked, including <c>RequiresConsent</c> true→false
/// and <c>TaxInclusive</c> false→true.
///
/// <para>The load-bearing test is <see cref="Clone_inherits_every_field_except_the_ones_it_is_allowed_to_reset"/>,
/// which enumerates <c>Event</c>'s properties by reflection rather than listing them. A column added later
/// is in neither set below, so it must be inherited — and if someone adds a reset for it in
/// <c>EventService.CloneAsync</c> without saying so here, this fails. That is the whole point: the previous
/// bug was not a wrong line of code, it was a list nobody remembered to extend.</para></summary>
public class EventCloneTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static bool _reset;
    private static readonly object ResetLock = new();

    public EventCloneTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Clone Cat", Slug = "clone-cat" };
            db.EventCategories.Add(cat);
            db.SaveChanges();
            _categoryId = cat.Id;
            _reset = true;
        }
    }

    /// <summary>Re-derived on the clone: it is a new event, in Draft, owned by whoever asked for it.
    /// Asserted individually below rather than by the generic sweep.</summary>
    private static readonly HashSet<string> Rederived =
    [
        nameof(Event.Id), nameof(Event.CreatedBy), nameof(Event.Title), nameof(Event.Slug),
        nameof(Event.ShortCode), nameof(Event.Status), nameof(Event.OrgUnitId),
        nameof(Event.CreatedAt), nameof(Event.UpdatedAt),
    ];

    /// <summary>Reset to the type default on the clone: runtime state, admin moderation and review
    /// outcomes all belong to the event that actually ran, not to a fresh draft nobody has seen.</summary>
    private static readonly HashSet<string> Blanked =
    [
        nameof(Event.PublishedAt), nameof(Event.DeletedAt), nameof(Event.ViewCount),
        nameof(Event.IsFeatured), nameof(Event.RefundWindowEndsAt),
        nameof(Event.IsSuspended), nameof(Event.SuspendedReason),
        nameof(Event.IsHidden), nameof(Event.HiddenReason),
        nameof(Event.FinancialReviewStatus), nameof(Event.FinancialReviewedBy),
        nameof(Event.FinancialReviewedAt), nameof(Event.FinancialReviewNotes),
        nameof(Event.ReviewClaimedBy), nameof(Event.ReviewClaimedAt),
        nameof(Event.SeriesId), nameof(Event.EditionOrdinal), nameof(Event.EditionLabel),
    ];

    /// <summary>Left at whatever the created event carries instead of being probed. Every one is a value
    /// the API re-reads downstream — an archetype that resolves no capabilities, a currency Money refuses,
    /// a timezone <c>TimeZoneInfo</c> cannot find — so a nonsense probe would fail the clone for a reason
    /// that has nothing to do with field fidelity. They are still compared; only the mutation is skipped.
    /// Guid columns are skipped wholesale: every one is a foreign key.</summary>
    private static readonly HashSet<string> NotProbed =
    [
        nameof(Event.ArchetypeSlug), nameof(Event.KindSlug), nameof(Event.SettlementCurrency),
        nameof(Event.Timezone), nameof(Event.Slug), nameof(Event.ShortCode), nameof(Event.Title),
        // Stamping the soft-delete tombstone hides the row from CloneAsync's own lookup, which filters
        // DeletedAt == null — the clone then 404s and the sweep tests nothing.
        nameof(Event.DeletedAt),
    ];

    [Fact]
    public async Task Clone_inherits_every_field_except_the_ones_it_is_allowed_to_reset()
    {
        var owner = await LoginAsync("9940000001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Clone Fidelity College");
        var eventId = await CreateEventAsync(owner, orgId);

        // Give every inheritable column a value distinguishable from its default, so "the clone matches
        // the source" cannot pass by both sides being empty.
        var probed = await ProbeEveryColumnAsync(eventId);
        Assert.True(probed > 40, $"probe only populated {probed} columns — it is no longer covering Event");

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/clone", new { title = "Fidelity Copy" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var cloneId = (await Json(res)).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var src = await db.Events.AsNoTracking().SingleAsync(e => e.Id == eventId);
        var clone = await db.Events.AsNoTracking().SingleAsync(e => e.Id == cloneId);

        var lost = new List<string>();
        foreach (var p in Settable())
        {
            if (Rederived.Contains(p.Name)) continue;

            var srcValue = p.GetValue(src);
            var cloneValue = p.GetValue(clone);

            if (Blanked.Contains(p.Name))
            {
                var blank = p.PropertyType.IsValueType && Nullable.GetUnderlyingType(p.PropertyType) is null
                    ? Activator.CreateInstance(p.PropertyType)
                    : null;
                Assert.Equal(blank, cloneValue);
                continue;
            }

            // Everything else is configuration and must travel. Collected rather than asserted one at a
            // time so a regression names every lost column at once instead of stopping at the first.
            if (!Equals(srcValue, cloneValue)) lost.Add($"{p.Name}: source={srcValue ?? "null"} clone={cloneValue ?? "null"}");
        }

        Assert.True(lost.Count == 0, "the clone dropped organiser configuration:\n  " + string.Join("\n  ", lost));

        // The fields the clone is supposed to re-derive.
        Assert.NotEqual(eventId, clone.Id);
        Assert.Equal(EventStatus.Draft, clone.Status);
        Assert.Equal("Fidelity Copy", clone.Title);
        Assert.NotEqual(src.Slug, clone.Slug);
        Assert.NotEqual(src.ShortCode, clone.ShortCode);
        Assert.Equal(src.CreatedBy, clone.CreatedBy);           // same caller cloned it
        Assert.NotNull(clone.OrgUnitId);
    }

    /// <summary>The three that were dangerous rather than merely untidy, pinned by name so a future
    /// refactor cannot quietly reintroduce them: a consent gate that turns itself off, an age/gender
    /// restriction that stops restricting, and tax-exclusive pricing that becomes tax-inclusive at the
    /// same rupee value.</summary>
    [Fact]
    public async Task Clone_carries_the_consent_gate_the_eligibility_limits_and_the_tax_treatment()
    {
        var owner = await LoginAsync("9940000002");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Consent Gate College");
        var eventId = await CreateEventAsync(owner, orgId, new
        {
            legal = new { requiresConsent = true, consentText = "I accept the rules." },
            eligibility = new { minAge = 18, maxAge = 30, genderRestriction = "Female", maxTeams = 12 },
            commerce = new { taxPercent = 18.0m, taxInclusive = false, platformFeePercent = 3.5m },
        });

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/clone", new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var cloneId = (await Json(res)).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var clone = await db.Events.AsNoTracking().SingleAsync(e => e.Id == cloneId);

        Assert.True(clone.RequiresConsent);
        Assert.Equal("I accept the rules.", clone.ConsentText);
        Assert.Equal(18, clone.MinAge);
        Assert.Equal(30, clone.MaxAge);
        Assert.Equal(GenderRestriction.Female, clone.GenderRestriction);
        Assert.Equal(12, clone.MaxTeams);
        Assert.Equal(18.0m, clone.TaxPercent);
        Assert.False(clone.TaxInclusive);
        Assert.Equal(3.5m, clone.PlatformFeePercent);
    }

    [Fact]
    public async Task Clone_does_not_inherit_moderation_or_review_state_from_the_source()
    {
        var owner = await LoginAsync("9940000003");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Suspended Source College");
        var eventId = await CreateEventAsync(owner, orgId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = await db.Events.SingleAsync(e => e.Id == eventId);
            ev.IsSuspended = true;
            ev.SuspendedReason = "payment dispute";
            ev.IsHidden = true;
            ev.HiddenReason = "under investigation";
            ev.FinancialReviewStatus = FinancialReviewStatus.Passed;
            ev.ReviewClaimedBy = ev.CreatedBy;
            ev.ReviewClaimedAt = DateTime.UtcNow;
            ev.ViewCount = 4200;
            ev.IsFeatured = true;
            await db.SaveChangesAsync();
        }

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/clone", new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var cloneId = (await Json(res)).GetProperty("id").GetGuid();

        using var verify = _factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<KurxDbContext>();
        var clone = await vdb.Events.AsNoTracking().SingleAsync(e => e.Id == cloneId);

        Assert.False(clone.IsSuspended);
        Assert.Null(clone.SuspendedReason);
        Assert.False(clone.IsHidden);
        Assert.Null(clone.HiddenReason);
        // Inheriting a Passed review would clear a publish blocker nobody actually checked.
        Assert.Null(clone.FinancialReviewStatus);
        Assert.Null(clone.ReviewClaimedBy);
        Assert.Null(clone.ReviewClaimedAt);
        Assert.Equal(0, clone.ViewCount);
        Assert.False(clone.IsFeatured);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static IEnumerable<PropertyInfo> Settable() =>
        typeof(Event).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite);

    /// <summary>Writes a non-default value into every inheritable column, straight through the DbContext
    /// so columns without an API write path are covered too. Returns how many it touched — asserted by the
    /// caller, because a probe that silently stops covering Event turns the sweep into a no-op.</summary>
    private async Task<int> ProbeEveryColumnAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ev = await db.Events.SingleAsync(e => e.Id == eventId);
        var meta = db.Model.FindEntityType(typeof(Event))!;
        var probed = 0;

        foreach (var p in Settable())
        {
            if (NotProbed.Contains(p.Name) || Rederived.Contains(p.Name)) continue;

            var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (type == typeof(Guid)) continue;                       // every Guid column is a foreign key

            object? value;
            if (type.IsEnum)
            {
                var current = p.GetValue(ev);
                value = Enum.GetValues(type).Cast<object>().FirstOrDefault(v => !v.Equals(current));
                if (value is null) continue;
            }
            else if (type == typeof(string))
            {
                var column = meta.FindProperty(p.Name);
                value = column?.GetColumnType() == "jsonb"
                    ? "{\"probe\":1}"
                    : Truncate($"probe-{p.Name}", column?.GetMaxLength());
            }
            else if (type == typeof(bool)) value = !(bool)(p.GetValue(ev) ?? false);
            else if (type == typeof(int)) value = 7;
            else if (type == typeof(long)) value = 700L;
            else if (type == typeof(decimal)) value = 7.5m;           // fits the (5,2) precision on the fee columns
            else if (type == typeof(double)) value = 7.5d;
            else if (type == typeof(DateTime)) value = new DateTime(2027, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            else continue;

            p.SetValue(ev, value);
            probed++;
        }

        await db.SaveChangesAsync();
        return probed;
    }

    private static string Truncate(string value, int? max) =>
        max is > 0 && value.Length > max ? value[..max.Value] : value;

    private static async Task<JsonElement> Json(HttpResponseMessage res) =>
        await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private async Task<Guid> CreateEventAsync(HttpClient client, Guid orgId, object? extra = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = "Clone Fest " + Guid.NewGuid().ToString("N")[..6],
            ["description"] = "An event with plenty of detail.",
            ["categoryId"] = _categoryId,
            ["venueName"] = "Main Hall",
            ["city"] = "Vizag",
            ["startsAt"] = DateTime.UtcNow.AddDays(20),
            ["endsAt"] = DateTime.UtcNow.AddDays(20).AddHours(4),
        };
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties())
                body[JsonNamingPolicy.CamelCase.ConvertName(p.Name)] = p.GetValue(extra);

        var id = (await Json(await client.CreateEventAsync(orgId, body))).GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(id);
        return id;
    }
}
