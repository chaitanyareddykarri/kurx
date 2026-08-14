using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

public class TicketTypeService(KurxDbContext db, IEventAuthority authority, IInventoryService inventory, IEventRegistrationService registration, ITeamService team) : ITicketTypeService
{
    public async Task<ServiceResult<TicketTypeView>> CreateAsync(Guid userId, Guid eventId, bool isAdmin, TicketTypeInput input, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<TicketTypeView>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<TicketTypeView>.Fail("forbidden");
        if (ev.Status == EventStatus.Archived) return ServiceResult<TicketTypeView>.Fail("event_archived");

        if (!Enum.TryParse<PricingUnit>(input.PricingUnit, true, out var pricingUnit))
            return ServiceResult<TicketTypeView>.Fail("invalid_pricing_unit");
        if (!Enum.TryParse<RegistrationMode>(input.RegistrationMode, true, out var regMode))
            return ServiceResult<TicketTypeView>.Fail("invalid_registration_mode");

        if (input.PricePaise < 0) return ServiceResult<TicketTypeView>.Fail("invalid_price");
        if (input.Quantity <= 0) return ServiceResult<TicketTypeView>.Fail("invalid_quantity");
        if (input.SaleEnds <= input.SaleStarts) return ServiceResult<TicketTypeView>.Fail("invalid_sale_dates");
        if (regMode == RegistrationMode.Group && (input.GroupMin is null || input.GroupMax is null))
            return ServiceResult<TicketTypeView>.Fail("group_size_required");
        if (regMode == RegistrationMode.Group && input.GroupMin > input.GroupMax)
            return ServiceResult<TicketTypeView>.Fail("invalid_group_size");

        var tt = new TicketType
        {
            EventId = eventId,
            Currency = ev.SettlementCurrency,   // V3 §9.1 — priced in the event's settlement currency
            Name = input.Name.Trim(),
            PricePaise = input.PricePaise,
            PricingUnit = pricingUnit,
            RegistrationMode = regMode,
            GroupMin = regMode == RegistrationMode.Group ? input.GroupMin : null,
            GroupMax = regMode == RegistrationMode.Group ? input.GroupMax : null,
            Quantity = input.Quantity,
            // Same Kind=Unspecified-vs-timestamptz issue as EventService.CreateAsync — see that fix's comment.
            SaleStarts = DateTime.SpecifyKind(input.SaleStarts, DateTimeKind.Utc),
            SaleEnds = DateTime.SpecifyKind(input.SaleEnds, DateTimeKind.Utc),
            PerUserLimit = input.PerUserLimit > 0 ? input.PerUserLimit : 5,
            IsAllAccess = input.IsAllAccess,
            IsCompetition = input.IsCompetition,
        };
        db.TicketTypes.Add(tt);
        await inventory.SyncPoolAsync(tt.Id, tt.EventId, tt.Quantity, tt.Sold, ct);   // V3 §8.1 general pool (Phase 7)
        await registration.SyncPolicyAsync(tt.Id, ct);                                // V3 §7.1 registration policy (Phase 8)
        await registration.SyncPassAsync(tt.Id, ct);                                  // V3 §9.2 Pass + AdmissionRight (Phase 9)
        await team.SyncPolicyAsync(tt.Id, ct);                                        // V3 §6.3 TeamPolicy for competition types (Phase 10)
        db.OutboxMessages.Add(Search.SearchReindex.Message(tt.EventId));              // V3 §15 (Phase 16): a priced type flips the paid/free discovery filter → reindex
        await db.SaveChangesAsync(ct);
        return ServiceResult<TicketTypeView>.Success(ToView(tt));
    }

    public async Task<ServiceResult<TicketTypeView>> UpdateAsync(Guid userId, Guid ticketTypeId, bool isAdmin, TicketTypeInput input, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.FirstOrDefaultAsync(t => t.Id == ticketTypeId, ct);
        if (tt is null) return ServiceResult<TicketTypeView>.Fail("not_found");
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == tt.EventId, ct);
        if (ev is null) return ServiceResult<TicketTypeView>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<TicketTypeView>.Fail("forbidden");
        if (ev.Status == EventStatus.Archived) return ServiceResult<TicketTypeView>.Fail("event_archived");

        if (!Enum.TryParse<PricingUnit>(input.PricingUnit, true, out var pricingUnit))
            return ServiceResult<TicketTypeView>.Fail("invalid_pricing_unit");
        if (!Enum.TryParse<RegistrationMode>(input.RegistrationMode, true, out var regMode))
            return ServiceResult<TicketTypeView>.Fail("invalid_registration_mode");
        if (input.PricePaise < 0) return ServiceResult<TicketTypeView>.Fail("invalid_price");
        // Quantity can't drop below what's actually taken — read the AUTHORITATIVE pool sold (Consumed+Held),
        // not the legacy TicketType.Sold mirror (§17.1, Phase 9).
        var soldNow = (await inventory.PoolCountsAsync([ticketTypeId], ct)).TryGetValue(ticketTypeId, out var c) ? c.Sold : tt.Sold;
        if (input.Quantity < soldNow) return ServiceResult<TicketTypeView>.Fail("quantity_below_sold");
        if (input.SaleEnds <= input.SaleStarts) return ServiceResult<TicketTypeView>.Fail("invalid_sale_dates");

        tt.Name = input.Name.Trim();
        tt.PricePaise = input.PricePaise;
        tt.PricingUnit = pricingUnit;
        tt.RegistrationMode = regMode;
        tt.GroupMin = regMode == RegistrationMode.Group ? input.GroupMin : null;
        tt.GroupMax = regMode == RegistrationMode.Group ? input.GroupMax : null;
        tt.Quantity = input.Quantity;
        tt.SaleStarts = DateTime.SpecifyKind(input.SaleStarts, DateTimeKind.Utc);
        tt.SaleEnds = DateTime.SpecifyKind(input.SaleEnds, DateTimeKind.Utc);
        tt.PerUserLimit = input.PerUserLimit > 0 ? input.PerUserLimit : 5;
        tt.IsAllAccess = input.IsAllAccess;
        tt.IsCompetition = input.IsCompetition;

        await inventory.SyncPoolAsync(tt.Id, tt.EventId, tt.Quantity, tt.Sold, ct);   // mirror Total on quantity change
        await registration.SyncPolicyAsync(tt.Id, ct);                                // mirror policy sale windows
        await registration.SyncPassAsync(tt.Id, ct);                                  // mirror Pass commercial fields (Phase 9)
        await team.SyncPolicyAsync(tt.Id, ct);                                        // sync TeamPolicy sizes if competition (Phase 10)
        db.OutboxMessages.Add(Search.SearchReindex.Message(tt.EventId));              // V3 §15 (Phase 16): a price change can flip paid/free → reindex
        await db.SaveChangesAsync(ct);
        // Re-read the authoritative counts (Total changed) for the response view.
        return ServiceResult<TicketTypeView>.Success(ToView(tt, (await inventory.PoolCountsAsync([ticketTypeId], ct)).GetValueOrDefault(ticketTypeId)));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid ticketTypeId, bool isAdmin, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.FirstOrDefaultAsync(t => t.Id == ticketTypeId, ct);
        if (tt is null) return ServiceResult<bool>.Fail("not_found");
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == tt.EventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");
        // Block deletion if anything is actually taken — authoritative pool sold (Consumed+Held), not the mirror.
        var sold = (await inventory.PoolCountsAsync([ticketTypeId], ct)).TryGetValue(ticketTypeId, out var dc) ? dc.Sold : tt.Sold;
        if (sold > 0) return ServiceResult<bool>.Fail("tickets_already_sold");

        db.TicketTypes.Remove(tt);
        db.OutboxMessages.Add(Search.SearchReindex.Message(tt.EventId));   // V3 §15 (Phase 16): removing the last priced type can flip paid→free → reindex
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<TicketTypeView>>> ListForOrgAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<IReadOnlyList<TicketTypeView>>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, ev.RepresentingOrgId, isAdmin, ct)).IsMember && !isAdmin)
            return ServiceResult<IReadOnlyList<TicketTypeView>>.Fail("forbidden");

        var types = await db.TicketTypes.AsNoTracking()
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.PricePaise)
            .ToListAsync(ct);
        var counts = await inventory.PoolCountsAsync(types.Select(t => t.Id).ToList(), ct);
        return ServiceResult<IReadOnlyList<TicketTypeView>>.Success(types.Select(t => ToView(t, counts.GetValueOrDefault(t.Id))).ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<TicketTypeView>>> ListPublicAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null || ev.Status != EventStatus.Published)
            return ServiceResult<IReadOnlyList<TicketTypeView>>.Fail("not_found");

        var now = DateTime.UtcNow;
        var types = await db.TicketTypes.AsNoTracking()
            .Where(t => t.EventId == eventId && t.SaleStarts <= now && t.SaleEnds >= now)
            .OrderBy(t => t.PricePaise)
            .ToListAsync(ct);
        var counts = await inventory.PoolCountsAsync(types.Select(t => t.Id).ToList(), ct);
        return ServiceResult<IReadOnlyList<TicketTypeView>>.Success(types.Select(t => ToView(t, counts.GetValueOrDefault(t.Id))).ToList());
    }

    public async Task<ServiceResult<FormFieldView>> AddFieldAsync(Guid userId, Guid ticketTypeId, bool isAdmin, FormFieldInput input, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketTypeId, ct);
        if (tt is null) return ServiceResult<FormFieldView>.Fail("not_found");
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == tt.EventId, ct);
        if (ev is null) return ServiceResult<FormFieldView>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<FormFieldView>.Fail("forbidden");

        if (!Enum.TryParse<FormFieldType>(input.Type, true, out var fieldType))
            return ServiceResult<FormFieldView>.Fail("invalid_type");
        if (!Enum.TryParse<FormFieldScope>(input.Scope, true, out var scope))
            return ServiceResult<FormFieldView>.Fail("invalid_scope");
        if (await db.FormFields.AnyAsync(f => f.TicketTypeId == ticketTypeId && f.Key == input.Key, ct))
            return ServiceResult<FormFieldView>.Fail("duplicate_key");

        var sort = input.Sort ?? await db.FormFields.Where(f => f.TicketTypeId == ticketTypeId).CountAsync(ct);
        var field = new FormField
        {
            TicketTypeId = ticketTypeId,
            Key = input.Key.Trim(),
            Label = input.Label.Trim(),
            Type = fieldType,
            Scope = scope,
            Required = input.Required,
            OptionsJson = input.OptionsJson,
            Sort = sort,
        };
        db.FormFields.Add(field);
        await db.SaveChangesAsync(ct);
        return ServiceResult<FormFieldView>.Success(ToFieldView(field));
    }

    public async Task<ServiceResult<FormFieldView>> UpdateFieldAsync(Guid userId, Guid fieldId, bool isAdmin, FormFieldInput input, CancellationToken ct = default)
    {
        var field = await db.FormFields.FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field is null) return ServiceResult<FormFieldView>.Fail("not_found");
        var tt = await db.TicketTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == field.TicketTypeId, ct);
        if (tt is null) return ServiceResult<FormFieldView>.Fail("not_found");
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == tt.EventId, ct);
        if (ev is null) return ServiceResult<FormFieldView>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<FormFieldView>.Fail("forbidden");

        if (!Enum.TryParse<FormFieldType>(input.Type, true, out var fieldType))
            return ServiceResult<FormFieldView>.Fail("invalid_type");
        if (!Enum.TryParse<FormFieldScope>(input.Scope, true, out var scope))
            return ServiceResult<FormFieldView>.Fail("invalid_scope");

        // Key change is allowed only if it stays unique within the ticket type
        if (input.Key != field.Key && await db.FormFields.AnyAsync(f => f.TicketTypeId == field.TicketTypeId && f.Key == input.Key, ct))
            return ServiceResult<FormFieldView>.Fail("duplicate_key");

        field.Key = input.Key.Trim();
        field.Label = input.Label.Trim();
        field.Type = fieldType;
        field.Scope = scope;
        field.Required = input.Required;
        field.OptionsJson = input.OptionsJson;
        if (input.Sort is not null) field.Sort = input.Sort.Value;

        await db.SaveChangesAsync(ct);
        return ServiceResult<FormFieldView>.Success(ToFieldView(field));
    }

    public async Task<ServiceResult<bool>> DeleteFieldAsync(Guid userId, Guid fieldId, bool isAdmin, CancellationToken ct = default)
    {
        var field = await db.FormFields.FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field is null) return ServiceResult<bool>.Fail("not_found");
        var tt = await db.TicketTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == field.TicketTypeId, ct);
        if (tt is null) return ServiceResult<bool>.Fail("not_found");
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == tt.EventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");

        db.FormFields.Remove(field);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<FormFieldView>> ListFieldsAsync(Guid ticketTypeId, CancellationToken ct = default)
        => await db.FormFields.AsNoTracking()
            .Where(f => f.TicketTypeId == ticketTypeId)
            .OrderBy(f => f.Sort)
            .Select(f => new FormFieldView(f.Id, f.TicketTypeId, f.Key, f.Label, f.Type.ToString(), f.Scope.ToString(), f.Required, f.OptionsJson, f.Sort))
            .ToListAsync(ct);

    // Sold/Available are the AUTHORITATIVE pool counts (§17.1, Phase 9) when supplied; the TicketType.Sold scalar
    // is only a fallback for a type with no pool yet (pre-first-sale).
    private static TicketTypeView ToView(TicketType t, PoolCounts? counts = null) => new(
        t.Id, t.EventId, t.Name, t.PricePaise,
        t.PricingUnit.ToString(), t.RegistrationMode.ToString(),
        t.GroupMin, t.GroupMax, t.Quantity,
        counts?.Sold ?? t.Sold,
        counts?.Available ?? Math.Max(0, t.Quantity - t.Sold),
        t.SaleStarts, t.SaleEnds, t.PerUserLimit, t.IsAllAccess, t.IsCompetition);

    private static FormFieldView ToFieldView(FormField f) => new(
        f.Id, f.TicketTypeId, f.Key, f.Label,
        f.Type.ToString(), f.Scope.ToString(), f.Required, f.OptionsJson, f.Sort);
}
