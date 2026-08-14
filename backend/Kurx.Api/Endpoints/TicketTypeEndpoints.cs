using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record TicketTypeBody(
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

public record FormFieldBody(
    string Key,
    string Label,
    string Type,
    string Scope,
    bool Required,
    string? OptionsJson,
    int? Sort);

public static class TicketTypeEndpoints
{
    public static void MapTicketTypeEndpoints(this WebApplication app)
    {
        // Org-scoped routes (auth required — Owner/Manager/Admin)
        var org = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/ticket-types")
            .WithTags("ticket-types")
            .RequireAuthorization();

        org.MapPost("/", async (Guid orgId, Guid eventId, TicketTypeBody body, ClaimsPrincipal principal, ITicketTypeService svc, CancellationToken ct) =>
        {
            var input = ToInput(body);
            var result = await svc.CreateAsync(UserId(principal), eventId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithValidation<TicketTypeBody>().Produces<TicketTypeView>();

        org.MapGet("/", async (Guid orgId, Guid eventId, ClaimsPrincipal principal, ITicketTypeService svc, CancellationToken ct) =>
        {
            var result = await svc.ListForOrgAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<TicketTypeView>>();

        org.MapPatch("/{ticketTypeId:guid}", async (Guid orgId, Guid eventId, Guid ticketTypeId, TicketTypeBody body, ClaimsPrincipal principal, ITicketTypeService svc, CancellationToken ct) =>
        {
            var input = ToInput(body);
            var result = await svc.UpdateAsync(UserId(principal), ticketTypeId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithValidation<TicketTypeBody>().Produces<TicketTypeView>();

        org.MapDelete("/{ticketTypeId:guid}", async (Guid orgId, Guid eventId, Guid ticketTypeId, ClaimsPrincipal principal, ITicketTypeService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(UserId(principal), ticketTypeId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        // Form fields — nested under a ticket type
        var fields = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/ticket-types/{ticketTypeId:guid}/fields")
            .WithTags("ticket-types")
            .RequireAuthorization();

        fields.MapGet("/", async (Guid orgId, Guid eventId, Guid ticketTypeId, ITicketTypeService svc, CancellationToken ct)
            => Results.Ok((await svc.ListFieldsAsync(ticketTypeId, ct)))).Produces<IReadOnlyList<FormFieldView>>();

        fields.MapPost("/", async (Guid orgId, Guid eventId, Guid ticketTypeId, FormFieldBody body, ClaimsPrincipal principal, ITicketTypeService svc, CancellationToken ct) =>
        {
            var input = ToFieldInput(body);
            var result = await svc.AddFieldAsync(UserId(principal), ticketTypeId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithValidation<FormFieldBody>().Produces<FormFieldView>();

        fields.MapPatch("/{fieldId:guid}", async (Guid orgId, Guid eventId, Guid ticketTypeId, Guid fieldId, FormFieldBody body, ClaimsPrincipal principal, ITicketTypeService svc, CancellationToken ct) =>
        {
            var input = ToFieldInput(body);
            var result = await svc.UpdateFieldAsync(UserId(principal), fieldId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithValidation<FormFieldBody>().Produces<FormFieldView>();

        fields.MapDelete("/{fieldId:guid}", async (Guid orgId, Guid eventId, Guid ticketTypeId, Guid fieldId, ClaimsPrincipal principal, ITicketTypeService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteFieldAsync(UserId(principal), fieldId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        // Public route — no auth; only on-sale types for published events
        app.MapGet("/v1/events/{eventId:guid}/ticket-types",
            async (Guid eventId, ITicketTypeService svc, CancellationToken ct) =>
            {
                var result = await svc.ListPublicAsync(eventId, ct);
                return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
            }).WithTags("ticket-types").Produces<IReadOnlyList<TicketTypeView>>();
    }

    private static TicketTypeInput ToInput(TicketTypeBody b) => new(
        b.Name, b.PricePaise, b.PricingUnit, b.RegistrationMode,
        b.GroupMin, b.GroupMax, b.Quantity, b.SaleStarts, b.SaleEnds, b.PerUserLimit, b.IsAllAccess, b.IsCompetition);

    private static FormFieldInput ToFieldInput(FormFieldBody b) => new(
        b.Key, b.Label, b.Type, b.Scope, b.Required, b.OptionsJson, b.Sort);



    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
