using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>ID card issuance, holder editing, generation and public verification (D-331).
///
/// <para>Routes split on <b>who is asserting what</b>, not on convenience. Issuance and the roster hang
/// off <c>/v1/orgs/{orgId}</c> because the organization is the authority the card leans on; the
/// holder's own reads hang off <c>/v1/me</c>; verification is anonymous and is the only route here
/// outside the authorization group.</para></summary>
public static class IdCardEndpoints
{
    public record IssueBody(Guid UserId, string? StudentId, string? Department, string? Course,
        string? Year, DateOnly? ValidFrom, DateOnly? ValidUntil, string? Template,
        bool ShowMealInfo = false);
    public record MealDisplayBody(bool Show);

    public record HolderEditBody(string? Template, string? PhotoKey, string? SignatureKey,
        string? LayoutJson, string? BloodGroup, string? Address,
        string? EmergencyContactName, string? EmergencyContactPhone);

    public record RevokeBody(string Reason);

    public static void MapIdCardEndpoints(this WebApplication app)
    {
        // ── Public verification. Anonymous by design: a verifier holding the physical card has no
        // account. Returns the narrow projection only (D-331) — never the student ID or contact data.
        app.MapGet("/v1/id-cards/verify/{code}", async (
            string code, IIdCardService svc, CancellationToken ct) =>
        {
            var r = await svc.VerifyAsync(code, ct);
            return r.Ok ? Results.Ok(ToVerificationJson(r.Value!)) : Fail(r.Error);
        }).WithTags("id-cards");

        // D-335 — event-scoped, not org-scoped. The event is what the card is proof of, and its
        // creator/organizer is the issuing authority.
        var evt = app.MapGroup("/v1/events/{eventId:guid}/id-cards").WithTags("id-cards").RequireAuthorization();

        // Issue. The caller must control the event, be verified, not be the holder, and the holder must
        // be a participant.
        evt.MapPost("/", async (Guid eventId, IssueBody body, ClaimsPrincipal p,
            IIdCardService svc, CancellationToken ct) =>
        {
            var input = new IdCardIssueInput(body.UserId, body.StudentId, body.Department, body.Course,
                body.Year, body.ValidFrom, body.ValidUntil, eventId, body.Template, body.ShowMealInfo);
            var r = await svc.IssueAsync(UserId(p), eventId, input, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IdCardView>();

        // The event's card roster, for its organizer.
        evt.MapGet("/", async (Guid eventId, string? status, ClaimsPrincipal p,
            IIdCardService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForEventAsync(UserId(p), eventId, status, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<IdCardView>>();

        var cards = app.MapGroup("/v1/id-cards").WithTags("id-cards").RequireAuthorization();

        cards.MapGet("/{cardId:guid}", async (Guid cardId, ClaimsPrincipal p,
            IIdCardService svc, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(UserId(p), cardId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IdCardView>();

        // Holder-scoped edit. The body cannot carry an asserted identifier, so this route physically
        // cannot change a student ID however it is called.
        cards.MapPatch("/{cardId:guid}", async (Guid cardId, HolderEditBody body, ClaimsPrincipal p,
            IIdCardService svc, CancellationToken ct) =>
        {
            var input = new IdCardHolderInput(body.Template, body.PhotoKey, body.SignatureKey,
                body.LayoutJson, body.BloodGroup, body.Address,
                body.EmergencyContactName, body.EmergencyContactPhone);
            var r = await svc.UpdateHolderFieldsAsync(UserId(p), cardId, input, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IdCardView>();

        // Generate / regenerate. Same route for both: regenerating is generating again, and modelling
        // it as a separate verb would let the two drift.
        cards.MapPost("/{cardId:guid}/generate", async (Guid cardId, ClaimsPrincipal p,
            IIdCardService svc, CancellationToken ct) =>
        {
            var r = await svc.GenerateAsync(UserId(p), cardId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IdCardView>();

        // D-334 §8. Issuer-side, like revoke and unlike the holder's PATCH: what the card asserts is the
        // issuer's claim. Does not reprint — regenerate for that, which re-reads the quantities.
        cards.MapPost("/{cardId:guid}/meal-display", async (Guid cardId, MealDisplayBody body,
            ClaimsPrincipal p, IIdCardService svc, CancellationToken ct) =>
        {
            var r = await svc.SetMealDisplayAsync(UserId(p), cardId, body.Show, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IdCardView>();

        cards.MapPost("/{cardId:guid}/revoke", async (Guid cardId, RevokeBody body, ClaimsPrincipal p,
            IIdCardService svc, CancellationToken ct) =>
        {
            var r = await svc.RevokeAsync(UserId(p), cardId, body.Reason, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Fail(r.Error);
        }).Produces<OperationAck>();

        app.MapGet("/v1/me/id-cards", async (ClaimsPrincipal p, IIdCardService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForUserAsync(UserId(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).WithTags("id-cards").RequireAuthorization().Produces<IReadOnlyList<IdCardView>>();
    }


    /// <summary>The anonymous projection for ANONYMOUS verification. Written out by hand rather than
    /// derived from <see cref="IdCardView"/> with fields removed — the whole point is that widening the
    /// authenticated view must not widen this one (D-331).
    ///
    /// <para>This is the one id-card route with no <c>.Produces&lt;T&gt;()</c>: it renames
    /// <c>OrgName</c> to <c>organization</c>, so declaring <see cref="IdCardVerification"/> here would
    /// publish a schema the wire contradicts. Giving it an honest schema means either renaming that key
    /// — a public contract change — or adding a dedicated DTO, and neither is a cleanup decision.</para></summary>
    private static object ToVerificationJson(IdCardVerification v) => new
    {
        verify_code = v.VerifyCode,
        holder_name = v.HolderName,
        organization = v.OrgName,
        status = v.Status,
        valid_from = v.ValidFrom,
        valid_until = v.ValidUntil,
        is_revoked = v.IsRevoked,
        issued_at = v.IssuedAt,
    };

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        // A card the caller may not see is absent, not refused (D-018) — the service already collapses
        // that case to not_found, and this keeps the mapping honest for the rest.
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        // D-335 issuance refusals. All 403: the request is well-formed and the caller is authenticated;
        // what is missing is authority, verification, or the holder's standing in the event.
        "forbidden" or "not_event_organizer" or "issuer_not_verified"
            or "cannot_issue_to_self" or "holder_not_a_participant" or "invalid_storage_key"
            => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_an_event_card" or "card_revoked" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error ?? "bad_request", StatusCodes.Status400BadRequest),
    };
}
