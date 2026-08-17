using System.Security.Claims;
using System.Text.Json.Serialization;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record CreateCategoryBody(string Level, string Name, Guid? ParentId, int? Sort, bool? IsVisible,
    string? Description, string? IconKey, string? Color, string? Badge, string? SearchKeywords);
public record UpdateCategoryBody(string? Name, int? Sort, bool? IsVisible,
    string? Description, string? IconKey, string? Color, string? Badge, string? SearchKeywords, Guid? ParentId);
public record ReorderItemBody([property: JsonPropertyName("id")] Guid Id, [property: JsonPropertyName("sort")] int Sort);
public record ReorderBody([property: JsonPropertyName("parent_id")] Guid? ParentId,
    [property: JsonPropertyName("order")] IReadOnlyList<ReorderItemBody> Order);
public record SetVisibilityBody(bool Visible);
public record CapabilityStateBody([property: JsonPropertyName("slug")] string Slug, [property: JsonPropertyName("state")] string State);
public record SetCapabilitiesBody([property: JsonPropertyName("capabilities")] IReadOnlyList<CapabilityStateBody> Capabilities);

/// <summary>D-188 (Platform Taxonomy Management) wire shape for import — mirrors <see cref="TaxonomyExportNode"/>
/// field-for-field so an export from this endpoint round-trips straight back into an import.</summary>
public record ImportNodeBody(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("parent_slug")] string? ParentSlug,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sort")] int Sort,
    [property: JsonPropertyName("is_visible")] bool IsVisible,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("icon_key")] string? IconKey,
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("badge")] string? Badge,
    [property: JsonPropertyName("search_keywords")] string? SearchKeywords);
public record ImportBody([property: JsonPropertyName("nodes")] IReadOnlyList<ImportNodeBody> Nodes);

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var categories = app.MapGroup("/v1/categories").WithTags("categories");

        categories.MapGet("/", async (string? level, string? q, ICategoryService svc, CancellationToken ct)
            => Results.Ok((await svc.ListAsync(level, q, includeHidden: false, ct)).Select(ToJson)))
            .Produces<IReadOnlyList<CategoryResponse>>();

        categories.MapPost("/", async (CreateCategoryBody body, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(body.Level, body.Name, body.ParentId, body.Sort ?? 0, body.IsVisible ?? true,
                body.Description, body.IconKey, body.Color, body.Badge, body.SearchKeywords, UserId(p), ct);
            return result.Ok ? Results.Ok(ToJson(result.Value!)) : Fail(result.Error);
        }).RequireAuthorization("KurxAdmin").WithValidation<CreateCategoryBody>().Produces<CategoryResponse>();

        categories.MapPatch("/{id:guid}", async (Guid id, UpdateCategoryBody body, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(id, body.Name, body.Sort, body.IsVisible,
                body.Description, body.IconKey, body.Color, body.Badge, body.SearchKeywords, body.ParentId, UserId(p), ct);
            return result.Ok ? Results.Ok(ToJson(result.Value!)) : Fail(result.Error);
        }).RequireAuthorization("KurxAdmin").Produces<CategoryResponse>();

        categories.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(id, UserId(p), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).RequireAuthorization("KurxAdmin").Produces<OperationAck>();

        // D-188 — lifecycle overrides, orthogonal to Delete. All KurxAdmin, same group/pattern as above.
        categories.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
            Result(await svc.DisableAsync(id, UserId(p), ct)))
            .RequireAuthorization("KurxAdmin").Produces<CategoryResponse>();
        categories.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
            Result(await svc.EnableAsync(id, UserId(p), ct)))
            .RequireAuthorization("KurxAdmin").Produces<CategoryResponse>();
        categories.MapPost("/{id:guid}/archive", async (Guid id, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
            Result(await svc.ArchiveAsync(id, UserId(p), ct)))
            .RequireAuthorization("KurxAdmin").Produces<CategoryResponse>();
        categories.MapPost("/{id:guid}/restore", async (Guid id, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
            Result(await svc.RestoreAsync(id, UserId(p), ct)))
            .RequireAuthorization("KurxAdmin").Produces<CategoryResponse>();
        categories.MapPost("/{id:guid}/visibility", async (Guid id, SetVisibilityBody body, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
            Result(await svc.SetVisibleAsync(id, body.Visible, UserId(p), ct)))
            .RequireAuthorization("KurxAdmin").Produces<CategoryResponse>();
        categories.MapPost("/{id:guid}/duplicate", async (Guid id, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
            Result(await svc.DuplicateAsync(id, UserId(p), ct)))
            .RequireAuthorization("KurxAdmin").Produces<CategoryResponse>();

        categories.MapPost("/reorder", async (ReorderBody body, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
        {
            var order = body.Order.Select(o => new ReorderItem(o.Id, o.Sort)).ToList();
            var result = await svc.ReorderAsync(body.ParentId, order, UserId(p), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).RequireAuthorization("KurxAdmin").Produces<OperationAck>();

        categories.MapGet("/{id:guid}/capabilities", async (Guid id, ICategoryService svc, CancellationToken ct) =>
        {
            var result = await svc.GetTypeCapabilitiesAsync(id, ct);
            return result.Ok ? Results.Ok(result.Value!.Select(ToJson)) : Fail(result.Error);
            // The mapper is a proven 1:1 snake_case projection of TypeCapabilityView, so declaring the View
            // describes the real body exactly. It is kept rather than deleted only because this file carries
            // six `ToJson` overloads resolved by argument type.
        }).RequireAuthorization("KurxAdmin").Produces<IReadOnlyList<TypeCapabilityView>>();

        categories.MapPut("/{id:guid}/capabilities", async (Guid id, SetCapabilitiesBody body, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
        {
            var states = body.Capabilities.Select(c => (c.Slug, c.State)).ToList();
            var result = await svc.SetTypeCapabilitiesAsync(id, states, UserId(p), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).RequireAuthorization("KurxAdmin").Produces<OperationAck>();

        // D-188 — the enriched admin listing (usage counts, all statuses, full metadata). Public GET / above
        // is unchanged, so no existing consumer (web, Flutter) is affected by any of this.
        var admin = app.MapGroup("/v1/admin/categories").WithTags("admin").RequireAuthorization("KurxAdmin");

        admin.MapGet("", async (string? level, string? q, ICategoryService svc, CancellationToken ct)
            => Results.Ok((await svc.ListForAdminAsync(level, q, ct)).Select(ToJson)))
            .Produces<IReadOnlyList<AdminCategoryResponse>>();

        admin.MapGet("/export", async (ICategoryService svc, CancellationToken ct) => Results.Ok(ToJson(await svc.ExportAsync(ct)))).Produces<TaxonomyExportResponse>();

        // `nodes` is not optional, but a record property deserialises to null when the caller omits it (or
        // sends a differently-named field), and ToExport then projected a null list — an ArgumentNullException
        // that escaped as a 500. A malformed body is a refusal the caller can act on, not a server fault, and
        // a 500 here also loses the machine-readable code every other admin failure carries.
        admin.MapPost("/import/preview", async (ImportBody body, ICategoryService svc, CancellationToken ct) =>
            body.Nodes is null or { Count: 0 }
                ? ProblemResults.Problem("nodes_required", StatusCodes.Status400BadRequest)
                : Results.Ok(ToJson(await svc.PreviewImportAsync(ToExport(body), ct)))).Produces<ImportPreviewResponse>();

        admin.MapPost("/import/apply", async (ImportBody body, ClaimsPrincipal p, ICategoryService svc, CancellationToken ct) =>
        {
            // Same guard as /import/preview: apply shares ToExport and threw the identical 500.
            if (body.Nodes is null or { Count: 0 })
                return ProblemResults.Problem("nodes_required", StatusCodes.Status400BadRequest);
            var result = await svc.ApplyImportAsync(ToExport(body), UserId(p), ct);
            return result is null
                ? ProblemResults.Problem("import_has_errors", StatusCodes.Status400BadRequest)
                // ImportResult is already an exact 1:1 of this shape, so it is returned rather than re-projected.
                : Results.Ok(result);
        }).Produces<ImportResult>();

        app.MapGet("/v1/tags", async (string? q, KurxDbContext db, CancellationToken ct) =>
        {
            var query = db.Tags.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(t => EF.Functions.ILike(t.Name, $"%{q}%"));
            var tags = await query.OrderBy(t => t.Name).Take(50)
                .Select(t => new TagResponse(t.Id, t.Name, t.Slug))
                .ToListAsync(ct);
            return Results.Ok(tags);
        }).WithTags("tags").Produces<IReadOnlyList<TagResponse>>();

        // Category/subcategory field metadata — powers the metadata-driven Create-Event form. FieldPreset
        // already models slug -> {registration_mode, group_min/max, pricing_unit, fields:[...]}; this
        // exposes it for the first time (the table was seeded/scaffolded but had no read endpoint).
        app.MapGet("/v1/field-presets", async (string? slug, KurxDbContext db, CancellationToken ct) =>
        {
            var query = db.FieldPresets.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(slug))
                query = query.Where(p => p.CategoryOrTypeSlug == slug);
            var presets = await query.OrderBy(p => p.Name)
                .Select(p => new FieldPresetResponse(p.Id, p.CategoryOrTypeSlug, p.Name, p.PayloadJson))
                .ToListAsync(ct);
            return Results.Ok(presets);
        }).WithTags("field-presets").Produces<IReadOnlyList<FieldPresetResponse>>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Result(ServiceResult<CategoryView> r) => r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);

    private static TaxonomyExport ToExport(ImportBody body) => new(body.Nodes.Select(n => new TaxonomyExportNode(
        n.Slug, n.ParentSlug, n.Level, n.Name, n.Sort, n.IsVisible, n.Description, n.IconKey, n.Color, n.Badge, n.SearchKeywords)).ToList(), DateTime.UtcNow);

    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "category_in_use" or "category_has_children" or "parent_mismatch" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    // D-326 — ProductClass is passed through with its original capitalisation, unlike Level: both clients
    // compare it against "Private" exactly, and lower-casing it here would empty the Private catalogue in
    // precisely the silent way this bug already did once.
    private static CategoryResponse ToJson(CategoryView c) => new(
        c.Id,
        c.ParentId,
        c.Level.ToLowerInvariant(),
        c.Name,
        c.Slug,
        c.Sort,
        c.IsVisible,
        c.ProductClass,
        // D-357 — the archetype the Type derives, so a client can ask the capability engine whether this
        // event can have teams instead of re-deriving the mapping.
        c.ArchetypeSlug);

    private static AdminCategoryResponse ToJson(AdminCategoryView c) => new(
        c.Id,
        c.ParentId,
        c.Level.ToLowerInvariant(),
        c.Name,
        c.Slug,
        c.Sort,
        c.IsVisible,
        c.Status.ToLowerInvariant(),
        c.Description,
        c.IconKey,
        c.Color,
        c.Badge,
        c.SearchKeywords,
        c.Version,
        c.CreatedAt,
        c.UpdatedAt,
        c.CreatedBy,
        c.UpdatedBy,
        c.UsageCount,
        c.RegistrationsCount,
        c.AttendeesCount,
        c.RevenuePaise,
        c.ViewsCount,
        c.FavoritesCount,
        c.IsTrending,
        c.ProductClass);

    private static object ToJson(TypeCapabilityView c) => new { slug = c.Slug, name = c.Name, group_slug = c.GroupSlug, state = c.State };

    private static TaxonomyExportNodeResponse ToJson(TaxonomyExportNode n) => new(
        n.Slug,
        n.ParentSlug,
        n.Level.ToLowerInvariant(),
        n.Name,
        n.Sort,
        n.IsVisible,
        n.Description,
        n.IconKey,
        n.Color,
        n.Badge,
        n.SearchKeywords);

    private static TaxonomyExportResponse ToJson(TaxonomyExport e) => new(e.Nodes.Select(ToJson), e.ExportedAt);

    private static ImportPreviewResponse ToJson(ImportPreview p) => new(
        p.ToCreate.Select(ToJson), p.ToUpdate.Select(ToJson), p.Conflicts, p.Errors);
}
