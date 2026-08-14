namespace Kurx.Application.Abstractions;

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
    bool IsCompetition = false);

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
    bool IsCompetition);

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
