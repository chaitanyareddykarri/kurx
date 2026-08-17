namespace Kurx.Application.Abstractions;

/// <summary>D-366 — one price band of a team ticket. Both ends inclusive; the price is for the WHOLE
/// team, never per member.</summary>
public record TicketPriceTierInput(int MinSize, int MaxSize, long PricePaise);

public record TicketTypeInput(
    string Name,
    long PricePaise,
    string PricingUnit,
    string RegistrationMode,
    int? GroupMin,
    int? GroupMax,
    int Quantity,
    DateTime SaleStarts,
    DateTime SaleEnds,
    int PerUserLimit,
    bool IsAllAccess,
    bool IsCompetition = false,
    /// <summary>D-366. Null and empty are the same thing and both mean "price from
    /// <paramref name="PricePaise"/>", which is every ticket type that predates this. A non-empty set
    /// must cover <paramref name="GroupMin"/>..<paramref name="GroupMax"/> exactly — no gap, no overlap.
    /// Sending it on a non-Group ticket is refused rather than ignored.</summary>
    IReadOnlyList<TicketPriceTierInput>? PriceTiers = null);

public record TicketTypeView(
    Guid Id,
    Guid EventId,
    string Name,
    long PricePaise,
    string PricingUnit,
    string RegistrationMode,
    int? GroupMin,
    int? GroupMax,
    int Quantity,
    int Sold,
    int Available,
    DateTime SaleStarts,
    DateTime SaleEnds,
    int PerUserLimit,
    bool IsAllAccess,
    bool IsCompetition,
    /// <summary>D-366 — empty for every ticket priced by a single amount, which is the default and the
    /// whole existing corpus. Ordered by size so a client can render the table without sorting it.</summary>
    IReadOnlyList<TicketPriceTierView>? PriceTiers = null);

public record TicketPriceTierView(int MinSize, int MaxSize, long PricePaise);

public record FormFieldInput(
    string Key,
    string Label,
    string Type,
    string Scope,
    bool Required,
    string? OptionsJson,
    int? Sort);

public record FormFieldView(
    Guid Id,
    Guid TicketTypeId,
    string Key,
    string Label,
    string Type,
    string Scope,
    bool Required,
    string? OptionsJson,
    int Sort);

/// <summary>
/// Ticket type CRUD scoped to an event (Owner/Manager manage; public reads for published events).
/// Form fields define the custom registration form attached to each ticket type.
/// </summary>
public interface ITicketTypeService
{
    Task<ServiceResult<TicketTypeView>> CreateAsync(Guid userId, Guid eventId, bool isAdmin, TicketTypeInput input, CancellationToken ct = default);

    Task<ServiceResult<TicketTypeView>> UpdateAsync(Guid userId, Guid ticketTypeId, bool isAdmin, TicketTypeInput input, CancellationToken ct = default);

    Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid ticketTypeId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Org-member / admin view — returns all ticket types regardless of sale window.</summary>
    Task<ServiceResult<IReadOnlyList<TicketTypeView>>> ListForOrgAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Public view — only for Published events; filters to currently on-sale types.</summary>
    Task<ServiceResult<IReadOnlyList<TicketTypeView>>> ListPublicAsync(Guid eventId, CancellationToken ct = default);

    // Form fields
    Task<ServiceResult<FormFieldView>> AddFieldAsync(Guid userId, Guid ticketTypeId, bool isAdmin, FormFieldInput input, CancellationToken ct = default);

    Task<ServiceResult<FormFieldView>> UpdateFieldAsync(Guid userId, Guid fieldId, bool isAdmin, FormFieldInput input, CancellationToken ct = default);

    Task<ServiceResult<bool>> DeleteFieldAsync(Guid userId, Guid fieldId, bool isAdmin, CancellationToken ct = default);

    Task<IReadOnlyList<FormFieldView>> ListFieldsAsync(Guid ticketTypeId, CancellationToken ct = default);
}
