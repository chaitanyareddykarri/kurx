using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

// D-104: ClientMessageId makes the send idempotent per room, which is what lets an offline
// outbox retry safely and optimistic UI reconcile its placeholder.
// `LinkPreview` (D-295) is supplied by the sender's client — the server never fetches the URL.
public record SendChatMessageBody(string Body, Guid? ReplyToMessageId, Guid? ClientMessageId,
    IReadOnlyList<Guid>? AttachmentIds, ChatLinkPreviewInput? LinkPreview = null);
public record PresignAttachmentBody(string FileName, string ContentType, long SizeBytes);
public record ConfirmAttachmentBody(string StorageKey);
public record MarkReadBody(Guid LastReadMessageId);
public record MuteMemberBody(int Minutes);
public record UpdateRoomBody(string? PostPolicy, string? Status);
public record ReportMessageBody(string Reason);
public record EditChatMessageBody(string Body);   // D-293
// ── D-295 ──
public record ReactBody(string Emoji);
public record MarkDeliveredBody(Guid MessageId);
public record ForwardBody(Guid TargetRoomId, Guid? ClientMessageId);
public record MuteRoomBody(DateTime? Until);
/// <summary>D-296. Hours rather than an absolute instant: the caller is choosing "24 hours" from a menu,
/// and an instant would make every pin depend on the client's clock being right.</summary>
public record PinMessageBody(double? DurationHours);

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        // ── Room ──────────────────────────────────────────────────────────────

        app.MapGet("/v1/events/{eventId:guid}/chat",
            async (Guid eventId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.GetRoomAsync(eventId, UserId(principal), ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<ChatRoomView>();

        // D-292 — the room by its OWN id, and the navigation primitive for both kinds.
        //
        // `GetRoomByIdAsync` has existed since D-264 with no HTTP door: the only way in was
        // `POST /v1/dm/{userId}`, so a client holding a roomId could not load the room. Web keyed its
        // route on eventId as a result, and since a direct room has no event, **every DM row in the
        // inbox was a dead link**. Membership is enforced inside the service, which answers "forbidden"
        // for a caller who is not in the room.
        app.MapGet("/v1/chat/rooms/{roomId:guid}",
            async (Guid roomId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.GetRoomByIdAsync(roomId, UserId(principal), ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<ChatRoomView>();

        app.MapPatch("/v1/chat/rooms/{roomId:guid}",
            async (Guid roomId, UpdateRoomBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.UpdateRoomAsync(roomId, UserId(principal), body.PostPolicy, body.Status, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        // ── Messages ──────────────────────────────────────────────────────────

        app.MapGet("/v1/chat/rooms/{roomId:guid}/messages",
            // Cursors are opaque strings (D-104) — deliberately not typed as Guid so clients treat
            // them as tokens. `after` is the delta-sync primitive used after a reconnect.
            async (Guid roomId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct,
                string? before = null, string? after = null, int limit = 50) =>
            {
                var result = await svc.GetMessagesAsync(roomId, UserId(principal), before, after, limit, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<ChatMessagePage>();

        app.MapPost("/v1/chat/rooms/{roomId:guid}/messages",
            async (Guid roomId, SendChatMessageBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.SendMessageAsync(roomId, UserId(principal), body.Body, body.ReplyToMessageId, body.ClientMessageId, body.AttachmentIds, body.LinkPreview, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<ChatMessageView>();

        app.MapPost("/v1/chat/rooms/{roomId:guid}/read",
            async (Guid roomId, MarkReadBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.MarkReadAsync(roomId, UserId(principal), body.LastReadMessageId, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        // D-293 — edit your own message. PATCH rather than PUT: the body is the only mutable field, and
        // everything else on the message is server-owned.
        app.MapPatch("/v1/chat/messages/{messageId:guid}",
            async (Guid messageId, EditChatMessageBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.EditMessageAsync(messageId, UserId(principal), body.Body, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<ChatMessageView>();

        // Delete for EVERYONE. Redacts the body and drops the attachments for every member.
        app.MapDelete("/v1/chat/messages/{messageId:guid}",
            async (Guid messageId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.DeleteMessageAsync(messageId, UserId(principal), ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        // D-293 — delete for ME. A distinct route rather than a flag on the one above, because the two
        // are different acts with different rights: this needs only membership and never touches what
        // anyone else sees, while the one above redacts the message for the whole room.
        app.MapDelete("/v1/chat/messages/{messageId:guid}/for-me",
            async (Guid messageId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.HideMessageAsync(messageId, UserId(principal), ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        // ── D-295 ─────────────────────────────────────────────────────────────

        // One toggle rather than add/remove: the client taps an emoji and the server decides whether
        // that means on or off, so two devices can never disagree about which call to make.
        app.MapPost("/v1/chat/messages/{messageId:guid}/reactions",
            async (Guid messageId, ReactBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.ToggleReactionAsync(messageId, UserId(principal), body.Emoji, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<IReadOnlyList<ChatReactionView>>();

        // Scoped to the caller's rooms inside the query — see SearchMessagesAsync for why filtering
        // afterwards would still leak.
        app.MapGet("/v1/chat/search",
            async (ClaimsPrincipal principal, IChatService svc, CancellationToken ct,
                string q = "", Guid? roomId = null, int limit = 25) =>
            {
                var result = await svc.SearchMessagesAsync(UserId(principal), q, roomId, limit, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<IReadOnlyList<ChatSearchHit>>();

        app.MapPost("/v1/chat/rooms/{roomId:guid}/delivered",
            async (Guid roomId, MarkDeliveredBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.MarkDeliveredAsync(roomId, UserId(principal), body.MessageId, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapPost("/v1/chat/messages/{messageId:guid}/forward",
            async (Guid messageId, ForwardBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.ForwardMessageAsync(
                    messageId, UserId(principal), body.TargetRoomId, body.ClientMessageId, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<ChatMessageView>();

        // Pin and mute are per-member filing, so they mirror the archive pair: POST sets, DELETE clears.
        app.MapPost("/v1/chat/rooms/{roomId:guid}/pin",
            async (Guid roomId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.SetPinnedAsync(roomId, UserId(principal), true, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapDelete("/v1/chat/rooms/{roomId:guid}/pin",
            async (Guid roomId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.SetPinnedAsync(roomId, UserId(principal), false, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapPost("/v1/chat/rooms/{roomId:guid}/mute",
            async (Guid roomId, MuteRoomBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.SetNotificationsMutedAsync(roomId, UserId(principal), body.Until, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapDelete("/v1/chat/rooms/{roomId:guid}/mute",
            async (Guid roomId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.SetNotificationsMutedAsync(roomId, UserId(principal), null, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapGet("/v1/chat/rooms/{roomId:guid}/media",
            async (Guid roomId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct, int limit = 60) =>
            {
                var result = await svc.RoomMediaAsync(roomId, UserId(principal), limit, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<IReadOnlyList<ChatAttachmentView>>();

        app.MapPost("/v1/chat/messages/{messageId:guid}/report",
            async (Guid messageId, ReportMessageBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.ReportMessageAsync(messageId, UserId(principal), body.Reason, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        // ── Attachments (D-110) ───────────────────────────────────────────────
        // Two steps by design: presign hands out a URL, confirm is where the server first sees the
        // bytes and therefore where every authoritative check runs.

        app.MapPost("/v1/chat/rooms/{roomId:guid}/attachments/presign",
            async (Guid roomId, PresignAttachmentBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.PresignAttachmentAsync(
                    roomId, UserId(principal), body.FileName, body.ContentType, body.SizeBytes, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<AttachmentUploadTicket>();

        app.MapPost("/v1/chat/rooms/{roomId:guid}/attachments/confirm",
            async (Guid roomId, ConfirmAttachmentBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.ConfirmAttachmentAsync(roomId, UserId(principal), body.StorageKey, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<ChatAttachmentView>();

        // Fresh signed URL — membership is re-checked, so a removed or banned member loses access
        // to files they could previously fetch.
        app.MapGet("/v1/chat/attachments/{attachmentId:guid}/url",
            async (Guid attachmentId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.GetAttachmentUrlAsync(attachmentId, UserId(principal), ct);
                return result.Ok ? Results.Ok(new AttachmentUrl(result.Value!)) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<AttachmentUrl>();

        // ── My chats ──────────────────────────────────────────────────────────

        // D-306 — `archived` selects which side of the caller's own filing to return, exactly as
        // `/v1/me/dm?archived=` has always done. Defaulted to false, so the route's existing behaviour and
        // every stored client are unchanged; without it, archiving an event room removed it from the only
        // list that could return it.
        app.MapGet("/v1/me/chats",
            async (ClaimsPrincipal principal, IChatService svc, CancellationToken ct, bool archived = false) =>
            {
                var result = await svc.GetMyChatsAsync(UserId(principal), archived, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<List<MyChatView>>();

        // D-292: archiving is a ChatMember concern and works on any room, so it gets a room-addressed
        // route. The DM-addressed pair under /v1/dm stays for the clients already calling it; both reach
        // the same service method, so the two can never diverge.
        app.MapPost("/v1/chat/rooms/{roomId:guid}/archive",
            async (Guid roomId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.SetArchivedAsync(roomId, UserId(principal), true, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapDelete("/v1/chat/rooms/{roomId:guid}/archive",
            async (Guid roomId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.SetArchivedAsync(roomId, UserId(principal), false, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        // ── Host moderation ───────────────────────────────────────────────────

        app.MapPost("/v1/chat/rooms/{roomId:guid}/members/{userId:guid}/mute",
            async (Guid roomId, Guid userId, MuteMemberBody body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.MuteMemberAsync(roomId, UserId(principal), userId, body.Minutes, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapPost("/v1/chat/rooms/{roomId:guid}/members/{userId:guid}/ban",
            async (Guid roomId, Guid userId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.BanMemberAsync(roomId, UserId(principal), userId, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapDelete("/v1/chat/rooms/{roomId:guid}/members/{userId:guid}/ban",
            async (Guid roomId, Guid userId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.UnbanMemberAsync(roomId, UserId(principal), userId, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        // D-296 — the body is optional so an older client that pins with no body still works; it lands on
        // the 7-day default rather than the permanent pin it used to get.
        // D-301 — explicit chat moderators. POST grants, DELETE revokes; the pair mirrors the ban routes
        // beside them. Hosts only, re-checked in the service.
        app.MapPost("/v1/chat/rooms/{roomId:guid}/members/{userId:guid}/moderator",
            async (Guid roomId, Guid userId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.SetModeratorAsync(roomId, UserId(principal), userId, true, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapDelete("/v1/chat/rooms/{roomId:guid}/members/{userId:guid}/moderator",
            async (Guid roomId, Guid userId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.SetModeratorAsync(roomId, UserId(principal), userId, false, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapPost("/v1/chat/messages/{messageId:guid}/pin",
            async (Guid messageId, PinMessageBody? body, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var duration = body?.DurationHours is { } h ? TimeSpan.FromHours(h) : (TimeSpan?)null;
                var result = await svc.PinMessageAsync(messageId, UserId(principal), true, duration, ct: ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();

        app.MapDelete("/v1/chat/messages/{messageId:guid}/pin",
            async (Guid messageId, ClaimsPrincipal principal, IChatService svc, CancellationToken ct) =>
            {
                var result = await svc.PinMessageAsync(messageId, UserId(principal), false, ct: ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("chat").RequireAuthorization().Produces<OperationAck>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        // D-301. `cannot_moderate_peer` and `cannot_change_host` are refusals of a valid request by an
        // authorised caller — 403, and distinct codes so a client can say WHY rather than "forbidden".
        "forbidden" or "banned" or "muted" or "hosts_only"
            or "cannot_moderate_peer" or "cannot_change_host"
            => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "room_locked" => ProblemResults.Problem(error, StatusCodes.Status423Locked),
        "upload_not_allowed" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "file_too_large" => ProblemResults.Problem(error, StatusCodes.Status413PayloadTooLarge),
        "unsupported_file_type" or "extension_mismatch" or "file_infected"
            => ProblemResults.Problem(error, StatusCodes.Status415UnsupportedMediaType),
        // The upload could not be verified. 503 rather than 4xx: nothing is wrong with the request,
        // the server simply cannot vouch for the file right now, and a retry may succeed.
        "scan_unavailable" => ProblemResults.Problem(error, StatusCodes.Status503ServiceUnavailable),
        "invalid_cursor" or "cursor_conflict" or "body_required" or "body_too_long" or "reason_required"
            or "invalid_attachment" or "too_many_attachments" or "invalid_storage_key" or "upload_not_found"
            => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
        // Surfaced from IReportService (D-106) — one open report per reporter+subject, matching
        // the status POST /v1/reports already returns for the same condition.
        "already_reported" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        "rate_limited" => ProblemResults.Problem(error, StatusCodes.Status429TooManyRequests),
        "delete_window_expired" or "edit_window_expired" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        // D-293. `no_change` is a 409: the request was well-formed and the caller may edit, the
        // message simply already says that — the same answer the Posts editor gives.
        "no_change" or "conflict" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),   // D-301 adds `conflict`
        // D-295. All four are malformed requests rather than refusals of a valid one.
        "invalid_emoji" or "query_too_short" or "nothing_to_forward"
            or "invalid_pin_duration"   // D-296
            => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
        "message_deleted" => ProblemResults.Problem(error, StatusCodes.Status410Gone),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
