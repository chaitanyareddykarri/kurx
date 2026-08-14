using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>Named so the generated spec can describe it (D-259). Serializes to <c>{"withdrawalId": "..."}</c>,
/// byte-identical to the anonymous object it replaces — the JSON key is unchanged.</summary>
public record WithdrawalAccepted(Guid WithdrawalId);

public static class WalletEndpoints
{
    public static void MapWalletEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/v1/orgs/{orgId:guid}/wallet")
            .WithTags("wallet")
            .RequireAuthorization();

        // These three return the Application records directly, so the serialized shape is camelCase —
        // there is no JSON naming policy on this API and these records carry no [JsonPropertyName].
        // Declaring the response types (D-259) is what lets the generated spec say so; both web and
        // admin had hand-written snake_case schemas here that threw on every call precisely because
        // nothing published the real shape.
        grp.MapGet("", async (Guid orgId, ClaimsPrincipal principal, IWalletService svc, CancellationToken ct) =>
        {
            var result = await svc.GetWalletAsync(UserId(principal), orgId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).WithSummary("Get organisation wallet balance")
          .Produces<WalletView>()
          .ProducesProblem(StatusCodes.Status403Forbidden)
          .ProducesProblem(StatusCodes.Status404NotFound);

        grp.MapGet("/ledger", async (Guid orgId, int page, int pageSize, ClaimsPrincipal principal, IWalletService svc, CancellationToken ct) =>
        {
            var result = await svc.GetLedgerAsync(UserId(principal), orgId, IsAdmin(principal), page, pageSize, ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).WithSummary("Paginated ledger history for an organisation")
          .Produces<IReadOnlyList<LedgerEntryView>>()
          .ProducesProblem(StatusCodes.Status403Forbidden);

        grp.MapPost("/withdraw", async (Guid orgId, WithdrawInput body, ClaimsPrincipal principal, IWalletService svc, CancellationToken ct) =>
        {
            var result = await svc.InitiateWithdrawalAsync(UserId(principal), orgId, body, ct);
            return result.Ok
                ? Results.Ok(new WithdrawalAccepted(result.Value))
                : Fail(result.Error);
        }).WithSummary("Initiate a payout withdrawal").WithValidation<WithdrawInput>()
          .Produces<WithdrawalAccepted>()
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "wallet_not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "payout_not_active" or "insufficient_balance" => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
