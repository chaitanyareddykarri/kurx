using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.Auth;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

// Request bodies stay camelCase — the platform contract is camelCase in, snake_case out (D-259
// addendum), and these are the "in" half.
public record CreatePostPollBody(string Question, IReadOnlyList<string> Options, bool AllowMultiple, DateTime? ClosesAt);

public record CreatePostBody(string? Body, string? Kind, string? Visibility,
    IReadOnlyList<Guid>? MediaIds, Guid? EventId, Guid? SharedPostId, CreatePostPollBody? Poll);

public record UpdatePostBody(string? Body, string? Visibility);
public record PresignPostMediaBody(string FileName, string ContentType, long SizeBytes);
public record ConfirmPostMediaBody(Guid MediaId, string StorageKey);
public record VotePostPollBody(IReadOnlyList<Guid> OptionIds);
public record CreatePostCommentBody(string Body, Guid? ParentCommentId);
public record HidePostBody(string Reason);

/// <summary>
/// The Posts module (D-262) — a social feed alongside Events.
///
/// <para>Handlers are thin by rule (CLAUDE.md §2): shape in, service call, map result. Every read
/// path's visibility decision lives in <see cref="IPostService"/>, never here, because an
/// authorization check duplicated at the edge is one that can disagree with the one that matters.</para>
///
/// <para>Cursors are opaque strings (the rule chat set in D-104) — deliberately not typed as
/// <c>Guid</c> so clients treat them as tokens rather than parsing them.</para>
/// </summary>
public static class PostEndpoints
{
    private const int DefaultLimit = 20;

    public static void MapPostEndpoints(this WebApplication app)
    {
        // ── Feed & single post ────────────────────────────────────────────────

        app.MapGet("/v1/feed",
            async (ClaimsPrincipal p, IPostService svc, CancellationToken ct, string? cursor = null, int limit = DefaultLimit) =>
            {
                var r = await svc.GetFeedAsync(UserId(p), cursor, limit, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostPage>();

        app.MapGet("/v1/posts/{postId:guid}",
            async (Guid postId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.GetAsync(postId, UserId(p), ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostView>();

        app.MapPost("/v1/posts",
            async (CreatePostBody body, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var poll = body.Poll is null
                    ? null
                    : new PostPollInput(body.Poll.Question,
                        (body.Poll.Options ?? []).Select(o => new PostPollOptionInput(o)).ToList(),
                        body.Poll.AllowMultiple, body.Poll.ClosesAt);
                var r = await svc.CreateAsync(UserId(p), body.Body, body.Kind, body.Visibility,
                    body.MediaIds, body.EventId, body.SharedPostId, poll, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization()
            .RequireRateLimiting("posts").WithValidation<CreatePostBody>().Produces<PostView>();

        app.MapPatch("/v1/posts/{postId:guid}",
            async (Guid postId, UpdatePostBody body, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.UpdateAsync(postId, UserId(p), body.Body, body.Visibility, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().WithValidation<UpdatePostBody>().Produces<PostView>();

        app.MapDelete("/v1/posts/{postId:guid}",
            async (Guid postId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.DeleteAsync(postId, UserId(p), IsModerator(p), ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces(StatusCodes.Status204NoContent);

        // ── Media (two-step upload, mirroring chat attachments D-110) ─────────
        // Presign hands out a URL; confirm is where the server first sees the bytes and therefore
        // where every authoritative check runs.

        app.MapPost("/v1/posts/media/presign",
            async (PresignPostMediaBody body, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.PresignMediaAsync(UserId(p), body.FileName, body.ContentType, body.SizeBytes, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization()
            .RequireRateLimiting("posts").WithValidation<PresignPostMediaBody>().Produces<PostMediaPresignView>();

        app.MapPost("/v1/posts/media/confirm",
            async (ConfirmPostMediaBody body, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.ConfirmMediaAsync(UserId(p), body.MediaId, body.StorageKey, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
                // Limited too, and it is the expensive half: confirm reads the whole object back out of
                // storage, decodes it and scans it. Presign only writes a row.
            }).WithTags("posts").RequireAuthorization()
            .RequireRateLimiting("posts").Produces<PostMediaView>();

        // ── Engagement ────────────────────────────────────────────────────────

        app.MapPost("/v1/posts/{postId:guid}/like",
            async (Guid postId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.SetLikeAsync(postId, UserId(p), liked: true, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostLikeResult>();

        app.MapDelete("/v1/posts/{postId:guid}/like",
            async (Guid postId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.SetLikeAsync(postId, UserId(p), liked: false, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostLikeResult>();

        app.MapPost("/v1/posts/{postId:guid}/save",
            async (Guid postId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.SetSaveAsync(postId, UserId(p), saved: true, ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces(StatusCodes.Status204NoContent);

        app.MapDelete("/v1/posts/{postId:guid}/save",
            async (Guid postId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.SetSaveAsync(postId, UserId(p), saved: false, ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces(StatusCodes.Status204NoContent);

        app.MapPost("/v1/posts/{postId:guid}/poll/vote",
            async (Guid postId, VotePostPollBody body, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.VoteAsync(postId, UserId(p), body.OptionIds, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostPollView>();

        // ── Comments ──────────────────────────────────────────────────────────

        app.MapGet("/v1/posts/{postId:guid}/comments",
            async (Guid postId, ClaimsPrincipal p, IPostService svc, CancellationToken ct,
                string? cursor = null, int limit = DefaultLimit) =>
            {
                var r = await svc.GetCommentsAsync(postId, UserId(p), cursor, limit, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostCommentPage>();

        app.MapPost("/v1/posts/{postId:guid}/comments",
            async (Guid postId, CreatePostCommentBody body, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.AddCommentAsync(postId, UserId(p), body.Body, body.ParentCommentId, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization()
            .RequireRateLimiting("posts").Produces<PostCommentView>();

        app.MapDelete("/v1/comments/{commentId:guid}",
            async (Guid commentId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.DeleteCommentAsync(commentId, UserId(p), IsModerator(p), ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces(StatusCodes.Status204NoContent);

        app.MapPost("/v1/comments/{commentId:guid}/like",
            async (Guid commentId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.SetCommentLikeAsync(commentId, UserId(p), liked: true, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostLikeResult>();

        app.MapDelete("/v1/comments/{commentId:guid}/like",
            async (Guid commentId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.SetCommentLikeAsync(commentId, UserId(p), liked: false, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostLikeResult>();

        // ── My posts ──────────────────────────────────────────────────────────

        app.MapGet("/v1/me/posts",
            async (ClaimsPrincipal p, IPostService svc, CancellationToken ct, string? cursor = null, int limit = DefaultLimit) =>
            {
                var r = await svc.GetMyPostsAsync(UserId(p), cursor, limit, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostPage>();

        app.MapGet("/v1/me/posts/saved",
            async (ClaimsPrincipal p, IPostService svc, CancellationToken ct, string? cursor = null, int limit = DefaultLimit) =>
            {
                var r = await svc.GetSavedAsync(UserId(p), cursor, limit, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostPage>();

        // ── Discovery ─────────────────────────────────────────────────────────

        // Public: an anonymous reader sees Public posts only, which the service enforces from a null
        // viewer rather than from this route knowing anything about visibility.
        app.MapGet("/v1/public/users/{username}/posts",
            async (string username, ClaimsPrincipal p, IPostService svc, CancellationToken ct,
                string? cursor = null, int limit = DefaultLimit) =>
            {
                var r = await svc.GetByUsernameAsync(username, OptionalUserId(p), cursor, limit, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").Produces<PostPage>();

        app.MapGet("/v1/events/{eventId:guid}/posts",
            async (Guid eventId, ClaimsPrincipal p, IPostService svc, CancellationToken ct,
                string? cursor = null, int limit = DefaultLimit) =>
            {
                var r = await svc.GetByEventAsync(eventId, UserId(p), cursor, limit, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostPage>();

        // Free-text search over post bodies. Declared BEFORE /v1/posts/{postId:guid} would ever be reached
        // for it — the guid constraint already keeps "search" from binding there, but the ordering is the
        // thing a reader checks first.
        app.MapGet("/v1/posts/search",
            async (ClaimsPrincipal p, IPostService svc, CancellationToken ct,
                string? q = null, string? cursor = null, int limit = DefaultLimit) =>
            {
                var r = await svc.SearchAsync(q ?? "", UserId(p), cursor, limit, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostPage>();

        app.MapGet("/v1/hashtags/{tag}/posts",
            async (string tag, ClaimsPrincipal p, IPostService svc, CancellationToken ct,
                string? cursor = null, int limit = DefaultLimit) =>
            {
                var r = await svc.GetByHashtagAsync(tag, UserId(p), cursor, limit, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("posts").RequireAuthorization().Produces<PostPage>();

        app.MapGet("/v1/hashtags/trending",
            async (ClaimsPrincipal p, IPostService svc, CancellationToken ct, int limit = 10) =>
                Results.Ok(await svc.GetTrendingHashtagsAsync(UserId(p), limit, ct)))
            .WithTags("posts").RequireAuthorization().Produces<List<TrendingHashtagView>>();

        // ── Moderation ────────────────────────────────────────────────────────

        var admin = app.MapGroup("/v1/admin/posts").WithTags("admin").RequireAuthorization("Moderation");

        admin.MapGet("", async (IPostService svc, CancellationToken ct,
                string? q = null, string? visibility = null, bool? reported = null, int page = 1, int pageSize = 20) =>
            Results.Ok(await svc.AdminListAsync(q, visibility, reported, page, pageSize, ct)))
            .Produces<PostAdminPage>();

        admin.MapPost("/{postId:guid}/hide",
            async (Guid postId, HidePostBody body, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.AdminSetHiddenAsync(postId, UserId(p), hidden: true, body.Reason, ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithValidation<HidePostBody>().Produces(StatusCodes.Status204NoContent);

        admin.MapPost("/{postId:guid}/unhide",
            async (Guid postId, ClaimsPrincipal p, IPostService svc, CancellationToken ct) =>
            {
                var r = await svc.AdminSetHiddenAsync(postId, UserId(p), hidden: false, null, ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>A post the caller may not see does not exist to them: every visibility miss lands on
    /// <c>post_not_found</c> / 404, never 403 (D-018), so the status code cannot confirm that a private
    /// post exists. <c>post_hidden</c> is the one exception and only ever reaches the post's author.</summary>
    private static IResult Fail(string? error) => error switch
    {
        "post_not_found" or "comment_not_found" or "event_not_found" or "post_hidden"
            => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "not_post_author" or "not_event_participant"
            => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "already_voted" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        "rate_limited" => ProblemResults.Problem(error, StatusCodes.Status429TooManyRequests),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    /// <summary>The public profile route accepts a bearer token but does not require one — a signed-in
    /// visitor sees what their connection to the author allows, an anonymous one sees Public only.</summary>
    private static Guid? OptionalUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>Platform-moderator capability — exactly the set the "Moderation" policy admits (D-059),
    /// so "who can hide a post" and "who can delete anyone's post" cannot drift apart. Read from claims
    /// the transformation resolves LIVE from Postgres per request (D-040), never from the token itself.</summary>
    private static bool IsModerator(ClaimsPrincipal principal)
        => principal.HasClaim(PlatformRoleClaimsTransformation.AdminClaim, "true")
           || principal.FindAll(PlatformRoleClaimsTransformation.PlatformRoleClaim).Any(c =>
               c.Value is nameof(PlatformRole.SuperAdmin)
                       or nameof(PlatformRole.VerificationReviewer)
                       or nameof(PlatformRole.Support));
}
