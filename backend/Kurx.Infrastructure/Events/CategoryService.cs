using System.Security.Cryptography;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>D-188 (Platform Taxonomy Management). See <see cref="ICategoryService"/> for the full contract.</summary>
public class CategoryService(KurxDbContext db, IAuditWriter audit, ICapabilityService capabilities) : ICategoryService
{
    public async Task<ServiceResult<CategoryView>> CreateAsync(string level, string name, Guid? parentId, int sort,
        bool isVisible, string? description, string? iconKey, string? color, string? badge, string? searchKeywords,
        Guid? actorId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<CategoryLevel>(level, true, out var lvl)) return ServiceResult<CategoryView>.Fail("invalid_level");
        name = name.Trim();
        if (name.Length is < 2 or > 100) return ServiceResult<CategoryView>.Fail("invalid_name");
        if (parentId is not null && !await db.EventCategories.AnyAsync(c => c.Id == parentId, ct))
            return ServiceResult<CategoryView>.Fail("invalid_parent");

        var category = new EventCategory
        {
            Level = lvl,
            Name = name,
            Slug = await UniqueSlugAsync(name, ct),
            ParentId = parentId,
            Sort = sort,
            IsVisible = isVisible,
            Description = Norm(description),
            IconKey = Norm(iconKey),
            Color = Norm(color),
            Badge = Norm(badge),
            SearchKeywords = Norm(searchKeywords),
            CreatedBy = actorId,
            UpdatedBy = actorId,
        };
        db.EventCategories.Add(category);
        audit.Write(new AuditEvent("category.create", "event_categories", category.Id, ActorType: "admin", ActorId: actorId, After: Snapshot(category)));
        await db.SaveChangesAsync(ct);
        return ServiceResult<CategoryView>.Success(ToView(category));
    }

    public async Task<ServiceResult<CategoryView>> UpdateAsync(Guid id, string? name, int? sort, bool? isVisible,
        string? description, string? iconKey, string? color, string? badge, string? searchKeywords,
        Guid? parentId, Guid? actorId, CancellationToken ct = default)
    {
        var category = await db.EventCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return ServiceResult<CategoryView>.Fail("not_found");
        var before = Snapshot(category);

        if (name is not null)
        {
            var trimmed = name.Trim();
            if (trimmed.Length is < 2 or > 100) return ServiceResult<CategoryView>.Fail("invalid_name");
            category.Name = trimmed; // slug intentionally immutable, same convention as org/event slugs (D-010)
        }
        if (sort is not null) category.Sort = sort.Value;
        if (isVisible is not null) category.IsVisible = isVisible.Value;
        if (description is not null) category.Description = Norm(description);
        if (iconKey is not null) category.IconKey = Norm(iconKey);
        if (color is not null) category.Color = Norm(color);
        if (badge is not null) category.Badge = Norm(badge);
        if (searchKeywords is not null) category.SearchKeywords = Norm(searchKeywords);

        // Reparent — Type level only (D-188). A Category/Audience move was never asked for.
        if (parentId is not null && category.Level == CategoryLevel.Type)
        {
            var newParent = await db.EventCategories.FirstOrDefaultAsync(c => c.Id == parentId, ct);
            if (newParent is null || newParent.Level != CategoryLevel.Category)
                return ServiceResult<CategoryView>.Fail("invalid_parent");
            category.ParentId = newParent.Id;
        }

        category.Version++;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = actorId;
        audit.Write(new AuditEvent("category.update", "event_categories", category.Id, ActorType: "admin", ActorId: actorId, Before: before, After: Snapshot(category)));
        await db.SaveChangesAsync(ct);
        return ServiceResult<CategoryView>.Success(ToView(category));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid? actorId, CancellationToken ct = default)
    {
        var category = await db.EventCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return ServiceResult<bool>.Fail("not_found");
        if (await db.Events.AnyAsync(e => e.CategoryId == id || e.TypeId == id || e.AudienceLevelId == id, ct))
            return ServiceResult<bool>.Fail("category_in_use");
        if (await db.EventCategories.AnyAsync(c => c.ParentId == id, ct))
            return ServiceResult<bool>.Fail("category_has_children");

        audit.Write(new AuditEvent("category.delete", "event_categories", category.Id, ActorType: "admin", ActorId: actorId, Before: Snapshot(category)));
        db.EventCategories.Remove(category);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public Task<ServiceResult<CategoryView>> DisableAsync(Guid id, Guid? actorId, CancellationToken ct = default)
        => SetStatusAsync(id, CategoryStatus.Disabled, "category.disable", actorId, ct);
    public Task<ServiceResult<CategoryView>> EnableAsync(Guid id, Guid? actorId, CancellationToken ct = default)
        => SetStatusAsync(id, CategoryStatus.Active, "category.enable", actorId, ct);
    public Task<ServiceResult<CategoryView>> ArchiveAsync(Guid id, Guid? actorId, CancellationToken ct = default)
        => SetStatusAsync(id, CategoryStatus.Archived, "category.archive", actorId, ct);
    /// <summary>Archived restores to Disabled, never straight to Active — a deliberate two-step (D-188).</summary>
    public Task<ServiceResult<CategoryView>> RestoreAsync(Guid id, Guid? actorId, CancellationToken ct = default)
        => SetStatusAsync(id, CategoryStatus.Disabled, "category.restore", actorId, ct);

    private async Task<ServiceResult<CategoryView>> SetStatusAsync(Guid id, CategoryStatus status, string action, Guid? actorId, CancellationToken ct)
    {
        var category = await db.EventCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return ServiceResult<CategoryView>.Fail("not_found");
        var before = Snapshot(category);
        category.Status = status;
        category.Version++;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = actorId;
        audit.Write(new AuditEvent(action, "event_categories", category.Id, ActorType: "admin", ActorId: actorId, Before: before, After: Snapshot(category)));
        await db.SaveChangesAsync(ct);
        return ServiceResult<CategoryView>.Success(ToView(category));
    }

    public async Task<ServiceResult<CategoryView>> SetVisibleAsync(Guid id, bool visible, Guid? actorId, CancellationToken ct = default)
    {
        var category = await db.EventCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return ServiceResult<CategoryView>.Fail("not_found");
        var before = Snapshot(category);
        category.IsVisible = visible;
        category.Version++;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = actorId;
        audit.Write(new AuditEvent("category.visibility", "event_categories", category.Id, ActorType: "admin", ActorId: actorId, Before: before, After: Snapshot(category)));
        await db.SaveChangesAsync(ct);
        return ServiceResult<CategoryView>.Success(ToView(category));
    }

    public async Task<ServiceResult<CategoryView>> DuplicateAsync(Guid id, Guid? actorId, CancellationToken ct = default)
    {
        var source = await db.EventCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (source is null) return ServiceResult<CategoryView>.Fail("not_found");
        if (source.Level != CategoryLevel.Type) return ServiceResult<CategoryView>.Fail("invalid_level");

        var copyName = $"{source.Name} (copy)";
        var copy = new EventCategory
        {
            Level = source.Level, Name = copyName, Slug = await UniqueSlugAsync(copyName, ct),
            ParentId = source.ParentId, Sort = source.Sort + 1, IsVisible = source.IsVisible,
            Description = source.Description, IconKey = source.IconKey, Color = source.Color,
            Badge = source.Badge, SearchKeywords = source.SearchKeywords,
            CreatedBy = actorId, UpdatedBy = actorId,
        };
        db.EventCategories.Add(copy);
        audit.Write(new AuditEvent("category.duplicate", "event_categories", copy.Id, ActorType: "admin", ActorId: actorId, Before: Snapshot(source), After: Snapshot(copy)));
        await db.SaveChangesAsync(ct);
        return ServiceResult<CategoryView>.Success(ToView(copy));
    }

    public async Task<ServiceResult<bool>> ReorderAsync(Guid? parentId, IReadOnlyList<ReorderItem> order, Guid? actorId, CancellationToken ct = default)
    {
        if (order.Count == 0) return ServiceResult<bool>.Success(true);
        var ids = order.Select(o => o.Id).ToList();
        var rows = await db.EventCategories.Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        if (rows.Count != ids.Count) return ServiceResult<bool>.Fail("not_found");
        if (rows.Any(r => r.ParentId != parentId)) return ServiceResult<bool>.Fail("parent_mismatch");

        var sortById = order.ToDictionary(o => o.Id, o => o.Sort);
        foreach (var r in rows) { r.Sort = sortById[r.Id]; r.Version++; r.UpdatedAt = DateTime.UtcNow; r.UpdatedBy = actorId; }
        audit.Write(new AuditEvent("category.reorder", "event_categories", parentId ?? Guid.Empty, ActorType: "admin", ActorId: actorId,
            After: new { parent_id = parentId, order = order.Select(o => new { o.Id, o.Sort }) }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    /// <summary>D-188 refinement round 2 #4 — the one place "usable for a new event right now" is decided,
    /// so a future ActiveFrom/ActiveUntil pair only needs to change here.</summary>
    public static bool IsSelectableForNewEvents(EventCategory c) => c.Status == CategoryStatus.Active && c.IsVisible;

    public async Task<IReadOnlyList<CategoryView>> ListAsync(string? level, string? q, bool includeHidden, CancellationToken ct = default)
    {
        var query = db.EventCategories.AsNoTracking().AsQueryable();
        if (level is not null && Enum.TryParse<CategoryLevel>(level, true, out var lvl)) query = query.Where(c => c.Level == lvl);
        if (!includeHidden) query = query.Where(c => c.Status == CategoryStatus.Active && c.IsVisible);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => EF.Functions.ILike(c.Name, $"%{q}%"));

        return await query.OrderBy(c => c.Sort).ThenBy(c => c.Name)
            .Select(c => new CategoryView(c.Id, c.ParentId, c.Level.ToString(), c.Name, c.Slug, c.Sort, c.IsVisible,
                c.ProductClass.HasValue ? c.ProductClass.Value.ToString() : null,
                // D-357 — the Type's archetype, which is what decides whether the event can have teams.
                // Same column D-266 M1 snapshots onto the event; projecting it is all that was missing.
                c.ArchetypeSlug))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdminCategoryView>> ListForAdminAsync(string? level, string? q, CancellationToken ct = default)
    {
        var query = db.EventCategories.AsNoTracking().AsQueryable();
        if (level is not null && Enum.TryParse<CategoryLevel>(level, true, out var lvl)) query = query.Where(c => c.Level == lvl);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => EF.Functions.ILike(c.Name, $"%{q}%"));

        var rows = await query.OrderBy(c => c.Sort).ThenBy(c => c.Name).ToListAsync(ct);
        var usage = await UsageCountsAsync(rows.Select(r => r.Id).ToList(), ct);
        return rows.Select(c => new AdminCategoryView(
            c.Id, c.ParentId, c.Level.ToString(), c.Name, c.Slug, c.Sort, c.IsVisible,
            c.Status.ToString(), c.Description, c.IconKey, c.Color, c.Badge, c.SearchKeywords,
            c.Version, c.CreatedAt, c.UpdatedAt, c.CreatedBy, c.UpdatedBy,
            usage.GetValueOrDefault(c.Id),
            ProductClass: c.ProductClass?.ToString())).ToList();
    }

    /// <summary>One grouped query per FK column, never per-row — same batch-aggregate shape as
    /// <c>EventService.EventStatsAsync</c>. Scales to thousands of Types without an algorithmic change.</summary>
    public async Task<IReadOnlyDictionary<Guid, int>> UsageCountsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, int>();
        var byCategory = await db.Events.Where(e => ids.Contains(e.CategoryId))
            .GroupBy(e => e.CategoryId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var byType = await db.Events.Where(e => e.TypeId != null && ids.Contains(e.TypeId!.Value))
            .GroupBy(e => e.TypeId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var byAudience = await db.Events.Where(e => e.AudienceLevelId != null && ids.Contains(e.AudienceLevelId!.Value))
            .GroupBy(e => e.AudienceLevelId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        return ids.ToDictionary(id => id, id =>
            byCategory.GetValueOrDefault(id) + byType.GetValueOrDefault(id) + byAudience.GetValueOrDefault(id));
    }

    public async Task<TaxonomyExport> ExportAsync(CancellationToken ct = default)
    {
        var rows = await db.EventCategories.AsNoTracking().ToListAsync(ct);
        var slugById = rows.ToDictionary(r => r.Id, r => r.Slug);
        var nodes = rows.Select(r => new TaxonomyExportNode(
            r.Slug, r.ParentId is { } pid ? slugById.GetValueOrDefault(pid) : null, r.Level.ToString(), r.Name,
            r.Sort, r.IsVisible, r.Description, r.IconKey, r.Color, r.Badge, r.SearchKeywords)).ToList();
        return new TaxonomyExport(nodes, DateTime.UtcNow);
    }

    public async Task<ImportPreview> PreviewImportAsync(TaxonomyExport import, CancellationToken ct = default)
    {
        var existing = await db.EventCategories.AsNoTracking().ToListAsync(ct);
        var existingBySlug = existing.ToDictionary(r => r.Slug, StringComparer.Ordinal);
        var incomingSlugs = import.Nodes.Select(n => n.Slug).ToHashSet(StringComparer.Ordinal);

        var toCreate = new List<TaxonomyExportNode>();
        var toUpdate = new List<TaxonomyExportNode>();
        var conflicts = new List<string>();
        var errors = new List<string>();

        // Duplicate name-under-same-parent within the incoming file itself.
        var dupGroups = import.Nodes.GroupBy(n => (n.ParentSlug, Name: n.Name.Trim().ToLowerInvariant()))
            .Where(g => g.Count() > 1);
        var duplicateSlugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in dupGroups)
        {
            conflicts.Add($"Duplicate name \"{g.First().Name}\" under parent \"{g.Key.ParentSlug ?? "(root)"}\" appears {g.Count()} times in the import file.");
            foreach (var n in g) duplicateSlugs.Add(n.Slug);
        }

        foreach (var node in import.Nodes)
        {
            if (duplicateSlugs.Contains(node.Slug)) continue; // already reported above, skip individual handling

            if (!Enum.TryParse<CategoryLevel>(node.Level, true, out _))
            {
                errors.Add($"\"{node.Name}\" ({node.Slug}): invalid level \"{node.Level}\".");
                continue;
            }
            if (node.ParentSlug is not null && !existingBySlug.ContainsKey(node.ParentSlug) && !incomingSlugs.Contains(node.ParentSlug))
            {
                errors.Add($"\"{node.Name}\" ({node.Slug}): parent slug \"{node.ParentSlug}\" resolves to nothing in the database or this import.");
                continue;
            }

            if (existingBySlug.TryGetValue(node.Slug, out var existingRow))
            {
                var existingParentSlug = existingRow.ParentId is { } pid ? existing.FirstOrDefault(r => r.Id == pid)?.Slug : null;
                // Level comparison is case-insensitive: the wire format lowercases it (CategoryEndpoints.ToJson),
                // so an export fed straight back in as an import (the common round-trip case) must still match.
                if (!string.Equals(existingRow.Level.ToString(), node.Level, StringComparison.OrdinalIgnoreCase) || existingParentSlug != node.ParentSlug)
                {
                    conflicts.Add($"\"{node.Name}\" ({node.Slug}): exists in the database under a different level/parent than this import expects — skipped.");
                    continue;
                }
                toUpdate.Add(node);
            }
            else
            {
                toCreate.Add(node);
            }
        }

        return new ImportPreview(toCreate, toUpdate, conflicts, errors);
    }

    public async Task<ImportResult?> ApplyImportAsync(TaxonomyExport import, Guid? actorId, CancellationToken ct = default)
    {
        var preview = await PreviewImportAsync(import, ct);
        if (preview.Errors.Count > 0) return null; // hard stop — never a partial-with-errors apply

        var existing = await db.EventCategories.ToListAsync(ct);
        var bySlug = existing.ToDictionary(r => r.Slug, StringComparer.Ordinal);
        var toApply = preview.ToCreate.Concat(preview.ToUpdate).ToList();

        // Two passes: create every row first (so a within-batch ParentSlug always resolves), then set ParentId.
        foreach (var node in preview.ToCreate)
        {
            var lvl = Enum.Parse<CategoryLevel>(node.Level, true);
            var row = new EventCategory
            {
                Level = lvl, Name = node.Name, Slug = node.Slug, Sort = node.Sort, IsVisible = node.IsVisible,
                Description = node.Description, IconKey = node.IconKey, Color = node.Color, Badge = node.Badge,
                SearchKeywords = node.SearchKeywords, CreatedBy = actorId, UpdatedBy = actorId,
            };
            db.EventCategories.Add(row);
            bySlug[row.Slug] = row;
        }
        foreach (var node in preview.ToUpdate)
        {
            var row = bySlug[node.Slug];
            row.Name = node.Name; row.Sort = node.Sort; row.IsVisible = node.IsVisible;
            row.Description = node.Description; row.IconKey = node.IconKey; row.Color = node.Color;
            row.Badge = node.Badge; row.SearchKeywords = node.SearchKeywords;
            row.Version++; row.UpdatedAt = DateTime.UtcNow; row.UpdatedBy = actorId;
        }
        foreach (var node in toApply)
        {
            var row = bySlug[node.Slug];
            row.ParentId = node.ParentSlug is not null ? bySlug[node.ParentSlug].Id : null;
        }

        audit.Write(new AuditEvent("category.import", "event_categories", Guid.Empty, ActorType: "admin", ActorId: actorId,
            After: new { created = preview.ToCreate.Count, updated = preview.ToUpdate.Count, skipped = preview.Conflicts.Count }));
        await db.SaveChangesAsync(ct);
        return new ImportResult(preview.ToCreate.Count, preview.ToUpdate.Count, preview.Conflicts.Count);
    }

    public async Task<ServiceResult<IReadOnlyList<TypeCapabilityView>>> GetTypeCapabilitiesAsync(Guid typeId, CancellationToken ct = default)
    {
        var type = await db.EventCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == typeId, ct);
        if (type is null) return ServiceResult<IReadOnlyList<TypeCapabilityView>>.Fail("not_found");
        if (type.Level != CategoryLevel.Type) return ServiceResult<IReadOnlyList<TypeCapabilityView>>.Fail("invalid_level");

        var registry = await capabilities.ListRegistryAsync(ct);
        var current = await db.CategoryCapabilityDefaults.AsNoTracking()
            .Where(x => x.CategoryNodeId == typeId).ToDictionaryAsync(x => x.CapabilitySlug, x => x.State, ct);

        var views = registry.Select(c => new TypeCapabilityView(c.Slug, c.Name, c.GroupSlug,
            current.TryGetValue(c.Slug, out var state) ? state.ToString().ToLowerInvariant() : "off")).ToList();
        return ServiceResult<IReadOnlyList<TypeCapabilityView>>.Success(views);
    }

    public async Task<ServiceResult<bool>> SetTypeCapabilitiesAsync(Guid typeId, IReadOnlyList<(string Slug, string State)> states,
        Guid? actorId, CancellationToken ct = default)
    {
        var type = await db.EventCategories.FirstOrDefaultAsync(c => c.Id == typeId, ct);
        if (type is null) return ServiceResult<bool>.Fail("not_found");
        if (type.Level != CategoryLevel.Type) return ServiceResult<bool>.Fail("invalid_level");

        var registrySlugs = (await capabilities.ListRegistryAsync(ct)).Select(c => c.Slug).ToHashSet(StringComparer.Ordinal);
        var existing = await db.CategoryCapabilityDefaults.Where(x => x.CategoryNodeId == typeId).ToListAsync(ct);
        db.CategoryCapabilityDefaults.RemoveRange(existing);

        var toAdd = new List<CategoryCapabilityDefault>();
        foreach (var (slug, state) in states)
        {
            if (!registrySlugs.Contains(slug)) return ServiceResult<bool>.Fail("invalid_capability");
            if (!Enum.TryParse<CapabilityState>(state, true, out var parsed) || parsed == CapabilityState.Off) continue; // off = no row
            toAdd.Add(new CategoryCapabilityDefault { CategoryNodeId = typeId, CapabilitySlug = slug, State = parsed });
        }
        db.CategoryCapabilityDefaults.AddRange(toAdd);

        type.Version++; type.UpdatedAt = DateTime.UtcNow; type.UpdatedBy = actorId;
        audit.Write(new AuditEvent("category.capabilities", "event_categories", typeId, ActorType: "admin", ActorId: actorId,
            After: new { capabilities = toAdd.Select(c => new { c.CapabilitySlug, State = c.State.ToString() }) }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private static CategoryView ToView(EventCategory c) => new(c.Id, c.ParentId, c.Level.ToString(), c.Name, c.Slug, c.Sort, c.IsVisible);

    private static object Snapshot(EventCategory c) => new
    {
        c.Id, c.ParentId, Level = c.Level.ToString(), c.Name, c.Slug, c.Sort, c.IsVisible,
        Status = c.Status.ToString(), c.Description, c.IconKey, c.Color, c.Badge, c.SearchKeywords,
        c.Version, c.CreatedAt, c.UpdatedAt, c.CreatedBy, c.UpdatedBy,
    };

    private static string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var slug = Slugify(name);
        while (await db.EventCategories.AnyAsync(c => c.Slug == slug, ct))
            slug = $"{Slugify(name)}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant()}";
        return slug;
    }

    private static string Slugify(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "category" : slug[..Math.Min(slug.Length, 60)];
    }
}
