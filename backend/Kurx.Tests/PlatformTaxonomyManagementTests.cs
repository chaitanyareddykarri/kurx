using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-188 (Platform Taxonomy Management) — the admin-manageable Audience/Category/Type module:
/// full lifecycle (create/update/disable/enable/archive/restore/duplicate/reorder/delete), usage counts,
/// audit trail, Type-level capability defaults, and merge-only import/export. Real HTTP against kurx_test,
/// same pattern as EventTaxonomyTests/AdminConsoleTests.</summary>
public class PlatformTaxonomyManagementTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public PlatformTaxonomyManagementTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    /// <summary>The KurxAdmin policy — and this feature's entire write surface — is granted live only to
    /// PlatformRole.SuperAdmin (PlatformRoleClaimsTransformation).</summary>
    private async Task<HttpClient> SuperAdminAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        var userId = tokens.GetProperty("user_id").GetGuid();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.SuperAdmin, grantedBy: null);
        return client;
    }

    private async Task<(HttpClient Client, Guid OrgId)> OwnerWithOrgAsync(string phone, string orgName)
    {
        var client = await LoginAsync(phone);
        var orgId = _factory.SeedVerifiedOrgForClient(client, orgName);
        return (client, orgId);
    }

    private Guid SeedCategory(CategoryLevel level, string name, Guid? parentId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var c = new EventCategory { Level = level, Name = name, Slug = Guid.NewGuid().ToString("N")[..12], ParentId = parentId };
        db.EventCategories.Add(c);
        db.SaveChanges();
        return c.Id;
    }

    [Fact]
    public async Task Create_stamps_metadata_and_audits_a_full_snapshot()
    {
        var admin = await SuperAdminAsync("9711000001");
        var res = await admin.PostAsJsonAsync("/v1/categories", new
        {
            level = "audience", name = "Corporate Events", sort = 0, isVisible = true,
            description = "For companies", color = "#2D6A73", badge = "New", searchKeywords = "corp,business"
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await Json(res);
        var id = body.GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.EventCategories.SingleAsync(c => c.Id == id);
        Assert.Equal(CategoryStatus.Active, row.Status);
        Assert.Equal("For companies", row.Description);
        Assert.Equal(1, row.Version);
        Assert.NotEqual(default, row.CreatedAt);

        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "category.create" && a.EntityId == id);
        Assert.Contains("For companies", audit.DetailsJson);
    }

    [Fact]
    public async Task Update_never_changes_slug_and_bumps_version()
    {
        var admin = await SuperAdminAsync("9711000002");
        var id = SeedCategory(CategoryLevel.Audience, "Slug Stable");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var before = await db.EventCategories.SingleAsync(c => c.Id == id);
            var originalSlug = before.Slug;

            var res = await admin.PatchAsJsonAsync($"/v1/categories/{id}", new { name = "Renamed Audience" });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            db.ChangeTracker.Clear();
            var after = await db.EventCategories.SingleAsync(c => c.Id == id);
            Assert.Equal(originalSlug, after.Slug);
            Assert.Equal("Renamed Audience", after.Name);
            Assert.Equal(2, after.Version);
        }
    }

    [Fact]
    public async Task Lifecycle_disable_enable_archive_restore_transitions()
    {
        var admin = await SuperAdminAsync("9711000003");
        var id = SeedCategory(CategoryLevel.Audience, "Lifecycle Test");

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/v1/categories/{id}/disable", new { })).StatusCode);
        Assert.Equal("disabled", await StatusOf(id));

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/v1/categories/{id}/archive", new { })).StatusCode);
        Assert.Equal("archived", await StatusOf(id));

        // Archived restores to Disabled, never straight to Active (D-188 refinement).
        var restore = await admin.PostAsJsonAsync($"/v1/categories/{id}/restore", new { });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Equal("disabled", await StatusOf(id));

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/v1/categories/{id}/enable", new { })).StatusCode);
        Assert.Equal("active", await StatusOf(id));
    }

    private async Task<string> StatusOf(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return (await db.EventCategories.SingleAsync(c => c.Id == id)).Status.ToString().ToLowerInvariant();
    }

    [Fact]
    public async Task Disabled_and_hidden_nodes_are_excluded_from_the_public_list_but_not_from_admin()
    {
        var admin = await SuperAdminAsync("9711000004");
        var visibleId = SeedCategory(CategoryLevel.Audience, "Publicly Listed");
        var disabledId = SeedCategory(CategoryLevel.Audience, "Disabled Node");
        await admin.PostAsJsonAsync($"/v1/categories/{disabledId}/disable", new { });

        var anon = _factory.CreateClient();
        var publicList = await Json(await anon.GetAsync("/v1/categories?level=Audience"));
        var publicIds = publicList.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(visibleId, publicIds);
        Assert.DoesNotContain(disabledId, publicIds);

        var adminList = await Json(await admin.GetAsync("/v1/admin/categories?level=audience"));
        var adminIds = adminList.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(disabledId, adminIds); // admin still sees it, just excluded from the public feed
    }

    [Fact]
    public async Task Duplicate_is_type_level_only()
    {
        var admin = await SuperAdminAsync("9711000005");
        var audienceId = SeedCategory(CategoryLevel.Audience, "Not Duplicable");
        var categoryId = SeedCategory(CategoryLevel.Category, "Parent Cat", audienceId);
        var typeId = SeedCategory(CategoryLevel.Type, "Duplicable Type", categoryId);

        var badRes = await admin.PostAsJsonAsync($"/v1/categories/{audienceId}/duplicate", new { });
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);

        var goodRes = await admin.PostAsJsonAsync($"/v1/categories/{typeId}/duplicate", new { });
        Assert.Equal(HttpStatusCode.OK, goodRes.StatusCode);
        var copy = await Json(goodRes);
        Assert.Contains("copy", copy.GetProperty("name").GetString());
        Assert.Equal(categoryId, copy.GetProperty("parent_id").GetGuid());
    }

    [Fact]
    public async Task Reorder_requires_matching_parent_and_supports_null_for_top_level_audiences()
    {
        var admin = await SuperAdminAsync("9711000006");
        var a1 = SeedCategory(CategoryLevel.Audience, "Ordered A");
        var a2 = SeedCategory(CategoryLevel.Audience, "Ordered B");
        var otherParentId = SeedCategory(CategoryLevel.Audience, "Different Parent");
        var childOfOther = SeedCategory(CategoryLevel.Category, "Child", otherParentId);

        // Reorder two Audience rows (parent_id: null) — must succeed.
        var res = await admin.PostAsJsonAsync("/v1/categories/reorder", new
        {
            parent_id = (Guid?)null,
            order = new[] { new { id = a1, sort = 5 }, new { id = a2, sort = 1 } }
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // Mixing a row with a different real parent must fail parent_mismatch (409).
        var mismatch = await admin.PostAsJsonAsync("/v1/categories/reorder", new
        {
            parent_id = (Guid?)null,
            order = new[] { new { id = a1, sort = 0 }, new { id = childOfOther, sort = 1 } }
        });
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
    }

    [Fact]
    public async Task Delete_is_blocked_when_in_use_or_has_children_never_when_neither()
    {
        var admin = await SuperAdminAsync("9711000007");
        var (owner, orgId) = await OwnerWithOrgAsync("9711000107", "Taxonomy Delete Org");
        var audienceId = SeedCategory(CategoryLevel.Audience, "Has Children");
        var categoryId = SeedCategory(CategoryLevel.Category, "In Use Category", audienceId);

        var ev = await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Taxonomy Delete Test Event", description = "x", categoryId,
            venueName = "V", venueAddress = "123 St", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(5), endsAt = DateTime.UtcNow.AddDays(5).AddHours(2)
        }));
        Assert.True(ev.TryGetProperty("id", out _), ev.ToString());

        // In use — blocked.
        var inUseRes = await admin.DeleteAsync($"/v1/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.Conflict, inUseRes.StatusCode);

        // Has children — blocked.
        var hasChildrenRes = await admin.DeleteAsync($"/v1/categories/{audienceId}");
        Assert.Equal(HttpStatusCode.Conflict, hasChildrenRes.StatusCode);

        // Neither — allowed.
        var unusedId = SeedCategory(CategoryLevel.Audience, "Truly Unused");
        var okRes = await admin.DeleteAsync($"/v1/categories/{unusedId}");
        Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);
    }

    [Fact]
    public async Task Usage_count_reflects_real_event_references()
    {
        var admin = await SuperAdminAsync("9711000008");
        var (owner, orgId) = await OwnerWithOrgAsync("9711000108", "Usage Count Org");
        var audienceId = SeedCategory(CategoryLevel.Audience, "Usage Audience");
        var categoryId = SeedCategory(CategoryLevel.Category, "Usage Category", audienceId);

        await owner.CreateEventAsync(orgId, new
        {
            title = "Usage Count Event", description = "x", categoryId,
            venueName = "V", venueAddress = "123 St", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(5), endsAt = DateTime.UtcNow.AddDays(5).AddHours(2)
        });

        var list = await Json(await admin.GetAsync("/v1/admin/categories?level=category"));
        var row = list.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == categoryId);
        Assert.Equal(1, row.GetProperty("usage_count").GetInt32());
    }

    [Fact]
    public async Task Type_capabilities_reuse_the_existing_registry_and_are_type_level_only()
    {
        var admin = await SuperAdminAsync("9711000009");
        var audienceId = SeedCategory(CategoryLevel.Audience, "Cap Audience");
        var categoryId = SeedCategory(CategoryLevel.Category, "Cap Category", audienceId);
        var typeId = SeedCategory(CategoryLevel.Type, "Cap Type", categoryId);

        var badRes = await admin.GetAsync($"/v1/categories/{categoryId}/capabilities");
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);

        var initial = await Json(await admin.GetAsync($"/v1/categories/{typeId}/capabilities"));
        // D-266 M2 reduced the capability registry from 57 slugs to D12's 30; the other 27 moved to the
        // subsystems that own them. D-334 then added `entitlements`, making 31. This asserts the exact
        // size rather than a floor that silently passed while the catalog shrank underneath it — which
        // is also why adding a capability is expected to fail here until the number is updated on purpose.
        Assert.Equal(31, initial.GetArrayLength());
        Assert.All(initial.EnumerateArray(), c => Assert.Equal("off", c.GetProperty("state").GetString()));

        var setRes = await admin.PutAsJsonAsync($"/v1/categories/{typeId}/capabilities", new
        {
            capabilities = new[] { new { slug = "certificates", state = "on" }, new { slug = "teams", state = "required" } }
        });
        Assert.Equal(HttpStatusCode.OK, setRes.StatusCode);

        var after = await Json(await admin.GetAsync($"/v1/categories/{typeId}/capabilities"));
        var certs = after.EnumerateArray().Single(c => c.GetProperty("slug").GetString() == "certificates");
        Assert.Equal("on", certs.GetProperty("state").GetString());
        var offCount = after.EnumerateArray().Count(c => c.GetProperty("state").GetString() == "off");
        Assert.Equal(initial.GetArrayLength() - 2, offCount);
    }

    [Fact]
    public async Task Export_then_reimport_the_same_payload_is_all_updates_no_creates_no_errors()
    {
        var admin = await SuperAdminAsync("9711000010");
        SeedCategory(CategoryLevel.Audience, "Roundtrip Audience");

        var export = await Json(await admin.GetAsync("/v1/admin/categories/export"));
        var preview = await Json(await admin.PostAsJsonAsync("/v1/admin/categories/import/preview", new { nodes = export.GetProperty("nodes") }));

        Assert.Equal(0, preview.GetProperty("to_create").GetArrayLength());
        Assert.True(preview.GetProperty("to_update").GetArrayLength() > 0);
        Assert.Equal(0, preview.GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public async Task Import_with_an_unresolvable_parent_is_an_error_and_apply_refuses_to_run()
    {
        var admin = await SuperAdminAsync("9711000011");
        var payload = new
        {
            nodes = new[]
            {
                new
                {
                    slug = "orphan-node-" + Guid.NewGuid().ToString("N")[..8], parent_slug = "does-not-exist-anywhere",
                    level = "Category", name = "Orphan", sort = 0, is_visible = true,
                    description = (string?)null, icon_key = (string?)null, color = (string?)null, badge = (string?)null, search_keywords = (string?)null
                }
            }
        };
        var preview = await Json(await admin.PostAsJsonAsync("/v1/admin/categories/import/preview", payload));
        Assert.True(preview.GetProperty("errors").GetArrayLength() > 0);

        var apply = await admin.PostAsJsonAsync("/v1/admin/categories/import/apply", payload);
        Assert.Equal(HttpStatusCode.BadRequest, apply.StatusCode);
    }

    [Fact]
    public async Task Import_creates_a_genuinely_new_node_via_merge()
    {
        var admin = await SuperAdminAsync("9711000012");
        var slug = "brand-new-" + Guid.NewGuid().ToString("N")[..8];
        var payload = new
        {
            nodes = new[]
            {
                new
                {
                    slug, parent_slug = (string?)null, level = "Audience", name = "Brand New Audience",
                    sort = 99, is_visible = true, description = (string?)null, icon_key = (string?)null,
                    color = (string?)null, badge = (string?)null, search_keywords = (string?)null
                }
            }
        };
        var apply = await Json(await admin.PostAsJsonAsync("/v1/admin/categories/import/apply", payload));
        Assert.Equal(1, apply.GetProperty("created").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.True(await db.EventCategories.AnyAsync(c => c.Slug == slug));
    }

    [Fact]
    public async Task Non_admin_is_rejected_from_every_write_route()
    {
        var (owner, _) = await OwnerWithOrgAsync("9711000013", "Non Admin Org");
        var id = SeedCategory(CategoryLevel.Audience, "Guarded Node");

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/v1/categories/{id}/disable", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/v1/categories", new { level = "audience", name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/v1/admin/categories")).StatusCode);
    }

    [Fact]
    public async Task Seeder_never_writes_a_second_time_once_the_table_has_any_row()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var countBefore = await db.EventCategories.CountAsync();
        Assert.True(countBefore > 0); // this fixture already has rows from earlier tests / prior seeding

        await Kurx.Infrastructure.Events.EventTaxonomySeeder.SeedAsync(db);
        var countAfter = await db.EventCategories.CountAsync();
        Assert.Equal(countBefore, countAfter); // no-op — the bootstrap guard already saw a non-empty table
    }

    // ── System-impact audit follow-up: EventService.CreateAsync/UpdateAsync were validating only Level,
    // never Status, so a client already holding a disabled category's id could still submit it. Fixed in
    // EventService.cs; these two tests are the regression coverage for that fix. ──

    [Fact]
    public async Task Creating_an_event_against_a_disabled_category_is_refused()
    {
        var admin = await SuperAdminAsync("9711000014");
        var (owner, orgId) = await OwnerWithOrgAsync("9711000114", "Disabled Category Org");
        var audienceId = SeedCategory(CategoryLevel.Audience, "Disabled-Cat Audience");
        var categoryId = SeedCategory(CategoryLevel.Category, "Soon Disabled Category", audienceId);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/v1/categories/{categoryId}/disable", new { })).StatusCode);

        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Should Be Refused", description = "x", categoryId,
            venueName = "V", venueAddress = "123 St", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(5), endsAt = DateTime.UtcNow.AddDays(5).AddHours(2)
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task An_event_already_using_a_since_disabled_category_can_still_be_edited_for_anything_else()
    {
        var admin = await SuperAdminAsync("9711000015");
        var (owner, orgId) = await OwnerWithOrgAsync("9711000115", "Grandfathered Category Org");
        var audienceId = SeedCategory(CategoryLevel.Audience, "Grandfather Audience");
        var categoryId = SeedCategory(CategoryLevel.Category, "Grandfather Category", audienceId);

        var created = await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Grandfathered Event", description = "x", categoryId,
            venueName = "V", venueAddress = "123 St", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(5), endsAt = DateTime.UtcNow.AddDays(5).AddHours(2)
        }));
        var eventId = created.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/v1/categories/{categoryId}/disable", new { })).StatusCode);

        // Editing an unrelated field (never touching categoryId) must succeed — the event's existing
        // reference to a now-disabled category is grandfathered, not retroactively broken.
        var patch = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { title = "Grandfathered Event (renamed)" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        // But explicitly trying to (re-)select that same disabled category is refused.
        var patchSameCategory = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { categoryId });
        Assert.Equal(HttpStatusCode.BadRequest, patchSameCategory.StatusCode);
    }
}
