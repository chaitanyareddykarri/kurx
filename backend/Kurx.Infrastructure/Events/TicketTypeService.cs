using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

public class TicketTypeService(KurxDbContext db, IEventAuthority authority, IInventoryService inventory, IEventRegistrationService registration, ITeamService team,
    ICapabilityService capabilities) : ITicketTypeService
{
    public async Task<ServiceResult<TicketTypeView>> CreateAsync(Guid userId, Guid eventId, bool isAdmin, TicketTypeInput input, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<TicketTypeView>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<TicketTypeView>.Fail("forbidden");
        if (ev.Status == EventStatus.Archived) return ServiceResult<TicketTypeView>.Fail("event_archived");
        if (EventStatusWorkflow.IsEditLocked(ev.Status)) return ServiceResult<TicketTypeView>.Fail("event_under_review");
        // D-388: what a live event costs is not the host's to change alone. Refused rather than routed into a
        // change request — that carries the event row only for now; see the decision's follow-up note.
        if (EventStatusWorkflow.IsLiveProtected(ev.Product, ev.Status))
            return ServiceResult<TicketTypeView>.Fail("change_request_required");

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
        if (input.PriceTiers is { Count: > 0 }
            && ValidateTiers(input.PriceTiers, regMode, input.GroupMin, input.GroupMax) is { } tierError)
            return ServiceResult<TicketTypeView>.Fail(tierError);
        // D-367 — a team registration is only legal where the archetype supports one.
        if (regMode == RegistrationMode.Group && !await SupportsTeamsAsync(eventId, ct))
            return ServiceResult<TicketTypeView>.Fail("teams_not_supported");

        var tt = new TicketType
        {
            EventId = eventId,
            Currency = ev.SettlementCurrency,   // V3 §9.1 — priced in the event's settlement currency
            Name = input.Name.Trim(),
            // D-366 — with bands, the ticket's own price is the LOWEST band, derived here rather than
            // trusted from the client. It stays the headline ("from ₹250") that sorting, the paid/free
            // discovery filter and `IsPaidEventAsync` all read, so none of them has to learn about
            // bands; the amount actually charged is the band the team's size resolves to.
            PricePaise = HeadlinePrice(input),
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
        await ReplaceTiersAsync(tt.Id, input.PriceTiers, ct);   // D-366 — after the type exists to hang off
        await ReopenReviewIfApprovedAsync(ev, userId, ct);
        return ServiceResult<TicketTypeView>.Success(ToView(tt, tiers: await TiersAsync(tt.Id, ct)));
    }

    public async Task<ServiceResult<TicketTypeView>> UpdateAsync(Guid userId, Guid ticketTypeId, bool isAdmin, TicketTypeInput input, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.FirstOrDefaultAsync(t => t.Id == ticketTypeId, ct);
        if (tt is null) return ServiceResult<TicketTypeView>.Fail("not_found");
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == tt.EventId, ct);
        if (ev is null) return ServiceResult<TicketTypeView>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<TicketTypeView>.Fail("forbidden");
        if (ev.Status == EventStatus.Archived) return ServiceResult<TicketTypeView>.Fail("event_archived");
        if (EventStatusWorkflow.IsEditLocked(ev.Status)) return ServiceResult<TicketTypeView>.Fail("event_under_review");
        // D-388: what a live event costs is not the host's to change alone. Refused rather than routed into a
        // change request — that carries the event row only for now; see the decision's follow-up note.
        if (EventStatusWorkflow.IsLiveProtected(ev.Product, ev.Status))
            return ServiceResult<TicketTypeView>.Fail("change_request_required");

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
        /*
         * D-367 — enforced only when the ticket is BECOMING a team ticket.
         *
         * A row already stored as `Group` stays editable even if the capability matrix would refuse it
         * today: an admin editing `archetype_capability_defaults` must not strand tickets that were legal
         * when written, leaving an organiser unable to fix or reprice one that may already have sold. The
         * invariant is "do not create NEW invalid state", not "make historical data unusable".
         */
        if (regMode == RegistrationMode.Group && tt.RegistrationMode != RegistrationMode.Group
            && !await SupportsTeamsAsync(tt.EventId, ct))
            return ServiceResult<TicketTypeView>.Fail("teams_not_supported");

        /*
         * D-366 — absent means LEAVE THE BANDS ALONE; present replaces them; `[]` clears them.
         *
         * "Absent replaces with none" is the obvious reading of a wholesale update and it is a trap: web's
         * host tickets page and mobile's ticket editor both PATCH the full row without knowing bands
         * exist, so renaming a banded ticket would have deleted every band and dropped every team size to
         * the cheapest price. A client that does not know about a field must not be able to destroy it.
         *
         * The stored set is still VALIDATED on every update, because this payload can invalidate it
         * without mentioning it: narrowing `GroupMax` to 3 while bands run to 5 leaves rules pricing a
         * team size the ticket no longer admits. Refused rather than silently trimmed.
         */
        var storedTiers = (await TiersAsync(ticketTypeId, ct))
            .Select(t => new TicketPriceTierInput(t.MinSize, t.MaxSize, t.PricePaise)).ToList();
        var tiersAfter = input.PriceTiers ?? storedTiers;
        if (tiersAfter is { Count: > 0 }
            && ValidateTiers(tiersAfter, regMode, input.GroupMin, input.GroupMax) is { } tierError)
            return ServiceResult<TicketTypeView>.Fail(tierError);

        tt.Name = input.Name.Trim();
        // D-366 — the cheapest band that will exist AFTER this update. Reading `input` alone would let a
        // client with no knowledge of bands set the headline to its own number while the bands survive,
        // so "from Rs.250" would stop being true of a ticket that still charges Rs.250.
        tt.PricePaise = tiersAfter is { Count: > 0 }
            ? tiersAfter.Min(t => t.PricePaise)
            : input.PricePaise;
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
        // Wholesale replacement (D-366), but only when the payload carried the field: a rule SET updated
        // row by row can pass through a state that has a gap in it, and the exclusion constraint would
        // reject the half-written middle. `null` means the client said nothing about bands and keeps
        // them; `[]` is an explicit "no bands".
        if (input.PriceTiers is not null) await ReplaceTiersAsync(tt.Id, input.PriceTiers, ct);
        await ReopenReviewIfApprovedAsync(ev, userId, ct);
        // Re-read the authoritative counts (Total changed) for the response view.
        return ServiceResult<TicketTypeView>.Success(ToView(tt,
            (await inventory.PoolCountsAsync([ticketTypeId], ct)).GetValueOrDefault(ticketTypeId),
            await TiersAsync(tt.Id, ct)));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid ticketTypeId, bool isAdmin, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.FirstOrDefaultAsync(t => t.Id == ticketTypeId, ct);
        if (tt is null) return ServiceResult<bool>.Fail("not_found");
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == tt.EventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");
        if (EventStatusWorkflow.IsEditLocked(ev.Status)) return ServiceResult<bool>.Fail("event_under_review");
        // D-388 — see CreateAsync. Deleting a live event's ticket type is a bigger change than editing one.
        if (EventStatusWorkflow.IsLiveProtected(ev.Product, ev.Status))
            return ServiceResult<bool>.Fail("change_request_required");
        // Block deletion if anything is actually taken — authoritative pool sold (Consumed+Held), not the mirror.
        var sold = (await inventory.PoolCountsAsync([ticketTypeId], ct)).TryGetValue(ticketTypeId, out var dc) ? dc.Sold : tt.Sold;
        if (sold > 0) return ServiceResult<bool>.Fail("tickets_already_sold");

        /*
         * D-363 — `sold` asks what is held RIGHT NOW; this asks what ever happened.
         *
         * `order_items` cascades from `ticket_types`, so a type whose every order was refunded reads
         * `sold == 0`, passes the check above, and takes its order lines with it — leaving `orders` rows
         * whose total no longer reconciles against anything. That does not correct the financial record,
         * it falsifies it.
         *
         * Order items ONLY. `passes` also cascades from `ticket_types` and was briefly checked here too,
         * which was wrong: a Pass is the 1:1 product mirror of a ticket type (`SyncPassAsync` creates one
         * for every type, on creation, not on purchase), so its existence is not evidence of a sale — it
         * is the type itself, and deleting it alongside its type is correct. Guarding on it blocked
         * deleting any ticket type at all.
         */
        if (await db.OrderItems.AnyAsync(i => i.TicketTypeId == ticketTypeId, ct))
            return ServiceResult<bool>.Fail("tickets_already_sold");

        db.TicketTypes.Remove(tt);
        db.OutboxMessages.Add(Search.SearchReindex.Message(tt.EventId));   // V3 §15 (Phase 16): removing the last priced type can flip paid→free → reindex
        await db.SaveChangesAsync(ct);
        await ReopenReviewIfApprovedAsync(ev, userId, ct);
        return ServiceResult<bool>.Success(true);
    }

    /*
     * D-363 §4 — what a reviewer assessed includes what the event COSTS.
     *
     * §4 names ticket price as a material field, but price does not live on `events` — it lives here, on
     * `ticket_types`, behind a different endpoint with its own service. So the re-review trigger in
     * `EventService.UpdateAsync` could not see it: an organiser could be approved on a free event, raise
     * the ticket to ₹5,000, and publish it themselves. The rule was real and the door beside it was open,
     * which is §3's lesson repeating one level down.
     *
     * The `IsEditLocked` refusals above are the same rule at the earlier moment: they stopped an event's
     * own fields changing under a reviewer, while its prices, quantities and sale windows stayed editable
     * — so a reviewer could approve a price that had already been replaced.
     *
     * `ev` is AsNoTracking on every path into here, so no tracked copy is left holding the old status.
     */
    private Task ReopenReviewIfApprovedAsync(Event ev, Guid userId, CancellationToken ct) =>
        EventReviewReopen.IfApprovedAsync(db, ev.Id, ev.Status, userId, "a ticket type", ct);

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
        // D-366 — one query for every type's bands, grouped in memory. A per-type query here is the N+1
        // that makes a price table cost as much as the listing itself.
        var ids = types.Select(t => t.Id).ToList();
        var tiers = (await db.TicketPriceTiers.AsNoTracking().Where(x => ids.Contains(x.TicketTypeId)).ToListAsync(ct))
            .GroupBy(x => x.TicketTypeId).ToDictionary(g => g.Key, g => (IReadOnlyList<TicketPriceTier>)g.ToList());
        return ServiceResult<IReadOnlyList<TicketTypeView>>.Success(
            types.Select(t => ToView(t, counts.GetValueOrDefault(t.Id), tiers.GetValueOrDefault(t.Id))).ToList());
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
        // D-366 — one query for every type's bands, grouped in memory. A per-type query here is the N+1
        // that makes a price table cost as much as the listing itself.
        var ids = types.Select(t => t.Id).ToList();
        var tiers = (await db.TicketPriceTiers.AsNoTracking().Where(x => ids.Contains(x.TicketTypeId)).ToListAsync(ct))
            .GroupBy(x => x.TicketTypeId).ToDictionary(g => g.Key, g => (IReadOnlyList<TicketPriceTier>)g.ToList());
        return ServiceResult<IReadOnlyList<TicketTypeView>>.Success(
            types.Select(t => ToView(t, counts.GetValueOrDefault(t.Id), tiers.GetValueOrDefault(t.Id))).ToList());
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
    private static TicketTypeView ToView(TicketType t, PoolCounts? counts = null,
        IReadOnlyList<TicketPriceTier>? tiers = null) => new(
        t.Id, t.EventId, t.Name, t.PricePaise,
        t.PricingUnit.ToString(), t.RegistrationMode.ToString(),
        t.GroupMin, t.GroupMax, t.Quantity,
        counts?.Sold ?? t.Sold,
        counts?.Available ?? Math.Max(0, t.Quantity - t.Sold),
        t.SaleStarts, t.SaleEnds, t.PerUserLimit, t.IsAllAccess, t.IsCompetition,
        // Null, not an empty list, when there are none: a client distinguishing "priced by bands" from
        // "priced by one amount" reads the presence of the collection, and `[]` would make every legacy
        // ticket look like a band-priced one with its bands missing (D-366).
        tiers is { Count: > 0 }
            ? tiers.OrderBy(x => x.MinSize).Select(x => new TicketPriceTierView(x.MinSize, x.MaxSize, x.PricePaise)).ToList()
            : null);

    /*
     * D-366 — the bands must describe every team size the ticket admits, exactly once.
     *
     * Validated here rather than trusted from the client, and checked as a SET rather than row by row:
     * an individually-valid band can still leave a gap ("2–2 and 4–5" refuses every team of 3 the same
     * ticket says is allowed) or an overlap ("2–4 and 3–5" makes a team of 3 ambiguous). Both are only
     * visible when the bands are read together.
     *
     * Overlap is ALSO prevented by an exclusion constraint in the database, which is what makes it a
     * guarantee rather than a check two concurrent writers can each pass. This is the message the
     * organiser gets; that is the thing that cannot be beaten.
     */
    /*
     * D-367 — does this event's archetype support TEAM registration?
     *
     * Read through `ICapabilityService.GetForEventAsync`, the established boundary: it resolves the
     * event's snapshotted `ArchetypeSlug` against the persisted `archetype_capability_defaults` matrix
     * the admin console owns (D-188). Calling `CapabilityResolver` from here would be a second
     * resolution path that could disagree with the one every read surface uses.
     *
     * `Locked` is how the resolver reports `Unsupported`; anything else — `Off` (supported, not yet
     * enabled), `On`, `Required` — is support. An event with no archetype resolves to `Locked`, and that
     * is deliberate rather than incidental: `CapabilityResolver.StateOf` returns `Unsupported` for a null
     * archetype because an event that has not said what kind of thing it is has not said it can have
     * teams. Both clients already fail closed the same way.
     */
    private async Task<bool> SupportsTeamsAsync(Guid eventId, CancellationToken ct) =>
        CapabilitySet.Supports(await capabilities.GetForEventAsync(eventId, ct), "teams");

    /// <summary>D-366 — a banded ticket's headline price is its cheapest band. Derived, never sent: a
    /// client that got it wrong would make "from ₹250" a lie and could flip a paid event to free.</summary>
    private static long HeadlinePrice(TicketTypeInput input)
        => input.PriceTiers is { Count: > 0 } tiers ? tiers.Min(t => t.PricePaise) : input.PricePaise;

    private static string? ValidateTiers(IReadOnlyList<TicketPriceTierInput> tiers, RegistrationMode mode,
        int? groupMin, int? groupMax)
    {
        // Per-participant pricing already scales with the roster; a size band on top of it would be two
        // answers to one question.
        if (mode != RegistrationMode.Group) return "price_tiers_require_group";
        if (groupMin is not { } min || groupMax is not { } max) return "price_tiers_require_group_size";

        foreach (var t in tiers)
        {
            if (t.MinSize < 1 || t.MaxSize < t.MinSize) return "invalid_price_tier";
            if (t.PricePaise <= 0) return "invalid_price_tier";
            // A band outside the admitted range prices a team that can never register — which reads to
            // the organiser as a price that silently does nothing.
            if (t.MinSize < min || t.MaxSize > max) return "price_tier_outside_group_size";
        }

        var ordered = tiers.OrderBy(t => t.MinSize).ToList();
        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i].MinSize <= ordered[i - 1].MaxSize) return "overlapping_price_tiers";

        // Coverage: contiguous from GroupMin to GroupMax with no hole between consecutive bands.
        if (ordered[0].MinSize != min || ordered[^1].MaxSize != max) return "price_tier_gap";
        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i].MinSize != ordered[i - 1].MaxSize + 1) return "price_tier_gap";

        return null;
    }

    /// <summary>Replaces a ticket type's bands wholesale. A partial update of a rule SET is how a gap
    /// appears between two individually-valid requests.</summary>
    private async Task ReplaceTiersAsync(Guid ticketTypeId, IReadOnlyList<TicketPriceTierInput>? tiers,
        CancellationToken ct)
    {
        await db.TicketPriceTiers.Where(x => x.TicketTypeId == ticketTypeId).ExecuteDeleteAsync(ct);
        if (tiers is not { Count: > 0 }) return;
        db.TicketPriceTiers.AddRange(tiers.Select(t => new TicketPriceTier
        {
            TicketTypeId = ticketTypeId, MinSize = t.MinSize, MaxSize = t.MaxSize, PricePaise = t.PricePaise,
        }));
        await db.SaveChangesAsync(ct);
    }

    private Task<List<TicketPriceTier>> TiersAsync(Guid ticketTypeId, CancellationToken ct) =>
        db.TicketPriceTiers.AsNoTracking().Where(x => x.TicketTypeId == ticketTypeId)
            .OrderBy(x => x.MinSize).ToListAsync(ct);

    private static FormFieldView ToFieldView(FormField f) => new(
        f.Id, f.TicketTypeId, f.Key, f.Label,
        f.Type.ToString(), f.Scope.ToString(), f.Required, f.OptionsJson, f.Sort);
}
