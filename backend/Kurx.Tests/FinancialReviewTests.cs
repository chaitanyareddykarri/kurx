using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M7 — financial review (D12 §6, A11 Fundraising).
///
/// <para>The rule is keyed on the <b>archetype</b>, not on "is it paid": a fundraiser solicits money for a
/// cause, which is the risk FinanceOps exists to check, while a ticketed concert (A8) takes more money and
/// raises none of it. These cases assert exactly that boundary, because getting it wrong in either
/// direction is invisible until it gates the wrong events.</para></summary>
public class FinancialReviewTests(KurxApiFactory factory) : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory = factory;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (c, t.GetProperty("user_id").GetGuid());
    }

    /// <summary>A platform FinanceOps client. Separate from the reviewer client on purpose — the whole
    /// point of this route is that clearing a money path is not the reviewer's competence.</summary>
    private async Task<HttpClient> FinanceOpsAsync(string phone)
    {
        var (c, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.FinanceOps, grantedBy: null);
        return c;
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId, string typeSlug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == typeSlug)
            .Select(c => new { c.Id, c.ParentId }).FirstAsync();

        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Fin " + Guid.NewGuid().ToString("N")[..6], description = "a real description",
            categoryId = t.ParentId!.Value, typeId = t.Id, venueName = "Hall", city = "C",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(3),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<List<string>> BlockersAsync(HttpClient c, Guid orgId, Guid eventId)
    {
        var req = await Json(await c.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/policy-requirements"));
        return req.GetProperty("publish_blockers").EnumerateArray().Select(b => b.GetString()!).ToList();
    }

    // ── The rule ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_fundraiser_cannot_publish_until_finance_clears_it()
    {
        var (owner, _) = await LoginAsync("9706000001");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Fundraiser College");
        var id = await CreateEventAsync(owner, orgId, "fundraiser");
        _factory.SeedApprovedEventAuthorization(id);   // isolate the financial rule from the M5 one

        using (var scope = _factory.Services.CreateScope())
        {
            var slug = await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
                .Events.AsNoTracking().Where(e => e.Id == id).Select(e => e.ArchetypeSlug).FirstAsync();
            // Guard the premise: if Fundraiser stopped resolving to A11 this test would pass vacuously.
            Assert.Equal("fundraising", slug);
        }

        Assert.Contains("financial_review_required", await BlockersAsync(owner, orgId, id));

        var finance = await FinanceOpsAsync("9706000002");
        Assert.Equal(HttpStatusCode.OK,
            (await finance.PostAsJsonAsync($"/v1/admin/events/{id}/financial-review",
                new { passed = true, notes = (string?)null })).StatusCode);

        Assert.DoesNotContain("financial_review_required", await BlockersAsync(owner, orgId, id));
    }

    /// <summary>The boundary in the other direction. A hackathon takes money too; it is not a fundraiser,
    /// and gating it would put FinanceOps in the path of most of the platform.</summary>
    [Fact]
    public async Task A_non_fundraising_event_needs_no_financial_review()
    {
        var (owner, _) = await LoginAsync("9706000003");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Hackathon College");
        var id = await CreateEventAsync(owner, orgId, "hackathon");
        _factory.SeedApprovedEventAuthorization(id);

        Assert.DoesNotContain("financial_review_required", await BlockersAsync(owner, orgId, id));
    }

    /// <summary>A failed review is a decision the organiser has to be able to act on.</summary>
    [Fact]
    public async Task Failing_a_review_without_notes_is_refused_and_keeps_the_blocker()
    {
        var (owner, _) = await LoginAsync("9706000004");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "FailNotes College");
        var id = await CreateEventAsync(owner, orgId, "fundraiser");
        _factory.SeedApprovedEventAuthorization(id);
        var finance = await FinanceOpsAsync("9706000005");

        var noNotes = await finance.PostAsJsonAsync($"/v1/admin/events/{id}/financial-review",
            new { passed = false, notes = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, noNotes.StatusCode);
        Assert.Contains("notes_required", await noNotes.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK,
            (await finance.PostAsJsonAsync($"/v1/admin/events/{id}/financial-review",
                new { passed = false, notes = "Settlement account does not match the named charity." })).StatusCode);

        // Failed is not cleared: the blocker must survive a refusal, or a failed review would be
        // indistinguishable from a passed one.
        Assert.Contains("financial_review_required", await BlockersAsync(owner, orgId, id));
    }

    /// <summary>Reviewing money is FinanceOps' competence, not the content reviewer's and not the
    /// organiser's. Asserted for both, because a route gated on the wrong policy looks correct until
    /// someone with the other role tries it.</summary>
    [Fact]
    public async Task Only_finance_ops_may_record_a_financial_review()
    {
        var (owner, _) = await LoginAsync("9706000006");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Authz Finance College");
        var id = await CreateEventAsync(owner, orgId, "fundraiser");

        var body = new { passed = true, notes = (string?)null };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.PostAsJsonAsync($"/v1/admin/events/{id}/financial-review", body)).StatusCode);

        var reviewer = await _factory.ReviewerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reviewer.PostAsJsonAsync($"/v1/admin/events/{id}/financial-review", body)).StatusCode);
    }

    /// <summary>The verdict is recorded on the event and in the audit spine — the trail a money decision
    /// needs.</summary>
    [Fact]
    public async Task The_verdict_is_persisted_and_audited()
    {
        var (owner, _) = await LoginAsync("9706000007");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Audited Finance College");
        var id = await CreateEventAsync(owner, orgId, "fundraiser");
        var finance = await FinanceOpsAsync("9706000008");

        await finance.PostAsJsonAsync($"/v1/admin/events/{id}/financial-review",
            new { passed = true, notes = "Verified against the charity register." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ev = await db.Events.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new { e.FinancialReviewStatus, e.FinancialReviewedBy, e.FinancialReviewedAt })
            .FirstAsync();

        Assert.Equal(FinancialReviewStatus.Passed, ev.FinancialReviewStatus);
        Assert.NotNull(ev.FinancialReviewedBy);
        Assert.NotNull(ev.FinancialReviewedAt);

        Assert.True(await db.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.Action == "event.financial_review" && a.EntityId == id));
    }

    [Fact]
    public async Task An_unknown_event_is_not_found()
    {
        var finance = await FinanceOpsAsync("9706000009");
        var res = await finance.PostAsJsonAsync($"/v1/admin/events/{Guid.NewGuid()}/financial-review",
            new { passed = true, notes = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
