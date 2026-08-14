using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Orgs;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Organization registry (M4, D-043) under event-first (D-074/D-075): an institution is never
/// self-minted — it is submitted as a representation request (POST /v1/orgs/representation-requests) that an
/// admin approves, and the registry (GET /v1/orgs/search) lists only *Verified* orgs. Domain dedup, fuzzy /
/// alias / domain search, and the "NSRIT ↔ full name" rule are exercised via real HTTP + pg_trgm.</summary>
public class OrgRegistryTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public OrgRegistryTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private static Task<HttpResponseMessage> SubmitRequest(HttpClient client, string name,
        string? type = null, string? primaryDomain = null) =>
        client.PostAsJsonAsync("/v1/orgs/representation-requests", new
        {
            name, type, primaryDomain,
            documents = new[] { new { docType = "registration_cert", storageKey = "private/verif/reg.pdf" } },
        });

    // ── Representation request (D-075): a not-yet-registered institution is staged, not minted ──
    [Fact]
    public async Task Request_with_type_and_domain_sets_registry_fields_and_stays_pending()
    {
        var client = await LoginAsync("9910000001");
        var res = await SubmitRequest(client, "NSRIT College", "College", "https://www.nsrit.edu.in/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var org = await Json(res);
        Assert.Equal("college", org.GetProperty("type").GetString());
        Assert.Equal("nsrit.edu.in", org.GetProperty("primary_domain").GetString());   // scheme/www/slash stripped
        Assert.Equal("pendingreview", org.GetProperty("verification_status").GetString());  // never verified on submit
    }

    [Fact]
    public async Task Request_defaults_type_to_other_when_absent()
    {
        var client = await LoginAsync("9910000002");
        var org = await Json(await SubmitRequest(client, "Untyped Society"));
        Assert.Equal("other", org.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Request_reusing_a_verified_orgs_domain_is_blocked()
    {
        var ownerA = await LoginAsync("9910000003");
        await _factory.CreateVerifiedOrgAsync(ownerA, "First Owner", "Company", "acme-dup.example.com");

        var ownerB = await LoginAsync("9910000013");
        var dup = await SubmitRequest(ownerB, "Impersonator", "Company", "acme-dup.example.com");
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("organization_domain_taken", (await Json(dup)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Invalid_domain_is_rejected()
    {
        var client = await LoginAsync("9910000004");
        var res = await SubmitRequest(client, "Bad Domain Org", primaryDomain: "not a domain!!");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_domain", (await Json(res)).GetProperty("error").GetString());
    }

    // ── Registry search: only Verified orgs are discoverable (D-074) ──
    [Fact]
    public async Task Search_finds_verified_org_by_exact_name()
    {
        var client = await LoginAsync("9910000005");
        _factory.SeedVerifiedOrgForClient(client, "Tech Fest Vizag");
        var results = await Json(await client.GetAsync("/v1/orgs/search?q=Tech Fest Vizag"));
        var hit = results.EnumerateArray().First();
        Assert.Equal("Tech Fest Vizag", hit.GetProperty("name").GetString());
        Assert.Equal("exact", hit.GetProperty("match").GetString());
    }

    [Fact]
    public async Task Search_finds_verified_org_by_fuzzy_name()
    {
        var client = await LoginAsync("9910000006");
        _factory.SeedVerifiedOrgForClient(client, "Hackathon Society Guntur");
        // Typo query (dropped letters) — trigram similarity still resolves it.
        var results = await Json(await client.GetAsync("/v1/orgs/search?q=hackaton societ guntur"));
        var names = results.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToList();
        Assert.Contains("Hackathon Society Guntur", names);
    }

    [Fact]
    public async Task Search_resolves_acronym_to_canonical_verified_org_via_alias()
    {
        var client = await LoginAsync("9910000007");
        var orgId = _factory.SeedVerifiedOrgForClient(client, "Nadimpalli Satyanarayana Raju Institute of Technology");
        // Seed the acronym alias (later modules add alias-management endpoints; M4 provides the model).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.OrganizationAliases.Add(new OrganizationAlias
            {
                OrgId = orgId, Alias = "NSRIT",
                NormalizedAlias = OrganizationRegistryService.Normalize("NSRIT"),
                Source = AliasSource.Official,
            });
            await db.SaveChangesAsync();
        }

        var results = await Json(await client.GetAsync("/v1/orgs/search?q=NSRIT"));
        var hit = results.EnumerateArray().First();
        Assert.Equal(orgId, hit.GetProperty("id").GetGuid());          // acronym resolves to the one org
        Assert.Equal("alias", hit.GetProperty("match").GetString());
    }

    [Fact]
    public async Task Search_finds_verified_org_by_domain()
    {
        var client = await LoginAsync("9910000008");
        var orgId = _factory.SeedVerifiedOrgForClient(client, "Domain Match College", "College", "dmc.ac.in");
        var results = await Json(await client.GetAsync("/v1/orgs/search?q=dmc.ac.in"));
        var hit = results.EnumerateArray().First();
        Assert.Equal(orgId, hit.GetProperty("id").GetGuid());
        Assert.Equal("domain", hit.GetProperty("match").GetString());
    }

    // A personal "Just me" org (still created directly via POST /v1/orgs) must never surface in the registry.
    [Fact]
    public async Task Personal_org_is_hidden_from_registry_search()
    {
        var client = await LoginAsync("9910000009");
        var res = await client.PostAsJsonAsync("/v1/orgs/", new { name = "Solaris Personal Society", personal = true });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var results = await Json(await client.GetAsync("/v1/orgs/search?q=Solaris Personal Society"));
        Assert.Empty(results.EnumerateArray());
    }

    // D-074 reverses D-055's "unverified real org stays searchable for dedup": a pending representation
    // request must NOT appear in the registry until an admin approves it — the registry is verified-only.
    [Fact]
    public async Task Pending_request_org_is_hidden_from_registry_search()
    {
        var client = await LoginAsync("9910000010");
        await SubmitRequest(client, "Lumen Public Institute", "College");   // stays PendingReview
        var results = await Json(await client.GetAsync("/v1/orgs/search?q=Lumen Public Institute"));
        Assert.Empty(results.EnumerateArray());
    }
}
