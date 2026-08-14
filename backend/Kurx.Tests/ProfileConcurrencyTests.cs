using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-231 — concurrency and hidden-state contracts for profile privacy.
///
/// <para>The lost-update defect these cover was reachable in normal use, not under synthetic load:
/// the Flutter privacy screen saves per toggle, so a user flipping two switches in quick succession
/// fires two overlapping <c>PATCH /v1/me/privacy</c> calls. Each read the pre-existing settings map,
/// merged only its own section, and wrote the whole map back — so the slower write silently discarded
/// the other change and a section the user believed was hidden stayed public.</para></summary>
public class ProfileConcurrencyTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public ProfileConcurrencyTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9205{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync()
    {
        var phone = NextPhone();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    /// <summary>Two sections hidden simultaneously — both must survive. This is the direct proof of the
    /// atomic-jsonb fix: it fails against the read-modify-write version and passes against the single
    /// merging statement (D-231).</summary>
    [Fact]
    public async Task Concurrent_privacy_writes_to_different_sections_both_survive()
    {
        var (client, _) = await LoginAsync();

        // Two separate clients so the requests are genuinely in flight together rather than queued
        // behind one connection.
        var a = _factory.CreateClient();
        var b = _factory.CreateClient();
        var auth = client.DefaultRequestHeaders.Authorization!;
        a.DefaultRequestHeaders.Authorization = auth;
        b.DefaultRequestHeaders.Authorization = auth;

        await Task.WhenAll(
            a.PatchAsJsonAsync("/v1/me/privacy", new
            {
                sections = new Dictionary<string, string> { ["attended"] = "only_me" },
            }),
            b.PatchAsJsonAsync("/v1/me/privacy", new
            {
                sections = new Dictionary<string, string> { ["certificates"] = "only_me" },
            }));

        var sections = (await Json(await client.GetAsync("/v1/me")))
            .GetProperty("privacy").GetProperty("sections");

        Assert.Equal("only_me", sections.GetProperty("attended").GetString());
        Assert.Equal("only_me", sections.GetProperty("certificates").GetString());
    }

    /// <summary>The heavier version: many sections hidden at once from many callers. Every one must
    /// land — a single lost write here is a section the user believes is private and is not.
    /// Eight sections at once is the stress version of the same contract (D-231).</summary>
    [Fact]
    public async Task No_privacy_change_is_lost_under_parallel_writes()
    {
        var (client, _) = await LoginAsync();
        var auth = client.DefaultRequestHeaders.Authorization!;

        var targets = new[]
        {
            "events", "attended", "certificates", "achievements",
            "organizations", "timeline", "network", "metrics",
        };

        await Task.WhenAll(targets.Select(section =>
        {
            var c = _factory.CreateClient();
            c.DefaultRequestHeaders.Authorization = auth;
            return c.PatchAsJsonAsync("/v1/me/privacy", new
            {
                sections = new Dictionary<string, string> { [section] = "only_me" },
            });
        }));

        var sections = (await Json(await client.GetAsync("/v1/me")))
            .GetProperty("privacy").GetProperty("sections");

        foreach (var section in targets)
        {
            Assert.Equal("only_me", sections.GetProperty(section).GetString());
        }
    }

    /// <summary>The dual-written legacy booleans must stay consistent with the section map even when
    /// the writes raced — otherwise a rollback to the pre-D-221 read path would disagree with what
    /// the user configured.</summary>
    [Fact]
    public async Task Legacy_booleans_stay_consistent_after_concurrent_writes()
    {
        var (client, userId) = await LoginAsync();
        var auth = client.DefaultRequestHeaders.Authorization!;

        await Task.WhenAll(
            new[] { "attended", "certificates", "network" }.Select(section =>
            {
                var c = _factory.CreateClient();
                c.DefaultRequestHeaders.Authorization = auth;
                return c.PatchAsJsonAsync("/v1/me/privacy", new
                {
                    sections = new Dictionary<string, string> { [section] = "only_me" },
                });
            }));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);

        Assert.False(user.ShowAttended);
        Assert.False(user.ShowCertificates);
        Assert.False(user.ShowAllies);
    }

    /// <summary>Hidden and empty must be distinguishable in the payload itself, not merely in the UI:
    /// a hidden count is null, an empty one is 0. Measuring `organizations.length` on the client
    /// collapsed the two, which is what printed "Organizations: 0" for someone who had simply chosen
    /// not to publish them (D-231).</summary>
    [Fact]
    public async Task Hidden_and_empty_organizations_are_distinguishable_in_the_payload()
    {
        var (client, userId) = await LoginAsync();
        var username = "conc" + Guid.NewGuid().ToString("N")[..14];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == userId);
            user.Username = username;
            user.ProfilePublic = true;
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();

        // Visible and genuinely empty → 0, a true statement.
        var empty = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));
        Assert.Equal(0, empty.GetProperty("organizations").GetInt32());

        await client.PatchAsJsonAsync("/v1/me/privacy", new
        {
            sections = new Dictionary<string, string> { ["organizations"] = "only_me" },
        });

        // Hidden → null, so no client can render it as a number.
        var hidden = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("organizations").ValueKind);
    }
}
