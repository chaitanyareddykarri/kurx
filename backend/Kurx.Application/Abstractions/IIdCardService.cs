namespace Kurx.Application.Abstractions;

/// <summary>What the issuer asserts. Every field here is set by the organization, never by the holder
/// (D-331) — this record exists partly to make that boundary visible in the type system rather than
/// only in an authorization check.</summary>
public record IdCardIssueInput(
    Guid UserId,
    string? StudentId,
    string? Department,
    string? Course,
    string? Year,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    /// <summary>D-335: required. The event is what the card is proof of, and its creator is the issuer.</summary>
    Guid EventId,
    string? Template,
    /// <summary>D-334 §8. Ignored unless <see cref="EventId"/> is set — a college ID has no event meals.</summary>
    bool ShowMealInfo = false);

/// <summary>What the holder may change. Deliberately disjoint from <see cref="IdCardIssueInput"/>:
/// nothing the issuer asserted appears here, so "the holder edited their own student ID" is not a bug
/// that can be written. Blood group, address and emergency contact are optional health/third-party
/// data and are never required to issue or generate a card.</summary>
public record IdCardHolderInput(
    string? Template,
    string? PhotoKey,
    string? SignatureKey,
    string? LayoutJson,
    string? BloodGroup,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

public record IdCardView(
    Guid Id, Guid OrgId, string OrgName, Guid UserId, string HolderName,
    string CardNumber, string VerifyCode, string Status, string Template,
    string? StudentId, string? Department, string? Course, string? Year,
    DateOnly? ValidFrom, DateOnly? ValidUntil,
    string? PhotoUrl, string? PdfUrl, string? PngUrl,
    DateTime? GeneratedAt, bool IsRevoked, string? RevokedReason, DateTime CreatedAt,
    bool ShowMealInfo = false, string? MealLine = null);

/// <summary>The public verification projection. Carries only what answers "is this card genuine and
/// current" — no student ID, no department, no contact data (D-331). Kept as its own type so a future
/// edit to <see cref="IdCardView"/> cannot widen the anonymous page by accident.</summary>
public record IdCardVerification(
    string VerifyCode, string HolderName, string OrgName, string Status,
    DateOnly? ValidFrom, DateOnly? ValidUntil, bool IsRevoked, DateTime IssuedAt);

public interface IIdCardService
{
    /// <summary>Issues a card for an event. The issuer is the event's creator/organizer and must be
    /// verified; the holder must be a participant of that event and may not be the issuer (D-335).</summary>
    Task<ServiceResult<IdCardView>> IssueAsync(Guid actorId, Guid eventId, IdCardIssueInput input, bool isAdmin, CancellationToken ct = default);

    /// <summary>Holder-scoped presentation edit. Cannot reach any asserted identifier.</summary>
    Task<ServiceResult<IdCardView>> UpdateHolderFieldsAsync(Guid actorId, Guid cardId, IdCardHolderInput input, CancellationToken ct = default);

    /// <summary>Renders and stores the PDF and PNG, stamping <c>GeneratedAt</c>. Idempotent in effect:
    /// re-running replaces the artefacts and the timestamp, which is the "regenerate" capability.</summary>
    Task<ServiceResult<IdCardView>> GenerateAsync(Guid actorId, Guid cardId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Turn the meal panel on or off (D-334 §8). Gated by <c>CanManageAsync</c> — verified
    /// membership of the issuing org, which by D-331 includes the holder themselves. That is acceptable
    /// only because the quantities are READ from grants rather than typed: the worst a holder can do is
    /// print a true number. Changing the flag does not reprint; the card must be regenerated, which is
    /// also when the quantities are re-read.</summary>
    Task<ServiceResult<IdCardView>> SetMealDisplayAsync(Guid actorId, Guid cardId, bool show, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<IdCardView>> GetAsync(Guid actorId, Guid cardId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<IdCardView>>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    /// <summary>The roster for one event, for its organizer (D-335).</summary>
    Task<ServiceResult<IReadOnlyList<IdCardView>>> ListForEventAsync(Guid actorId, Guid eventId, string? status, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<bool>> RevokeAsync(Guid actorId, Guid cardId, string reason, bool isAdmin, CancellationToken ct = default);

    /// <summary>Anonymous lookup by printed code. A revoked card resolves (200) so the revocation can be
    /// confirmed — contrast D-018's hide-to-404, per D-036.</summary>
    Task<ServiceResult<IdCardVerification>> VerifyAsync(string verifyCode, CancellationToken ct = default);
}

public record IdCardRenderRequest(
    string TemplateKey,
    string HolderName,
    string OrgName,
    string? StudentId,
    string? Department,
    string? Course,
    string? Year,
    string? BloodGroup,
    string? Phone,
    string? Email,
    string? DateOfBirth,
    string? Address,
    string? EmergencyContact,
    string ValidityLine,
    /// <summary>Pre-formatted meal entitlement, e.g. "B1 · L1 · D1 · S2". Null when the card does not
    /// show meals or the holder has none. Computed at generation from live grants, never stored.</summary>
    string? MealLine,
    string CardNumber,
    byte[]? PhotoBytes,
    byte[]? LogoBytes,
    byte[]? SignatureBytes,
    byte[] QrPng);

public record IdCardRenderResult(byte[] PdfBytes, byte[] PngBytes);

public interface IIdCardRenderer
{
    /// <summary>Renders an ID card to PDF (CR80, 85.6×54 mm) and PNG. Uses QuestPDF directly, so no
    /// PDF→PNG rasterization step is involved — the stub rasterizer is only for uploaded-PDF templates
    /// (D-035) and is not on this path.</summary>
    Task<IdCardRenderResult> RenderAsync(IdCardRenderRequest request, CancellationToken ct = default);
}
