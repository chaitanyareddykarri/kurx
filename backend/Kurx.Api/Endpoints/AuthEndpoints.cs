using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Domain;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Kurx.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record OtpRequestBody(string Phone);
public record OtpVerifyBody(string Phone, string Code);
/// <summary>DeviceId/Signature are the sender-constrained refresh proof (AM5, D-081) — required only for a
/// device-bound session's token, ignored for a legacy bearer token.</summary>
public record RefreshBody(string RefreshToken, Guid? DeviceId = null, string? Signature = null);
public record UpdateProfileBody(
    string? Name, string? Username, string? Headline, string? Bio,
    string? EducationJson, string[]? Skills, string? LinksJson,
    string? AvatarKey, string? CoverKey,
    // Phase 2 (About). Same nullable-means-unchanged convention as every field above.
    string[]? Languages = null, string[]? Interests = null,
    // D-263: language is a profile field, so it extends this endpoint rather than getting its own.
    string? Language = null,
    /// <summary>Required to finish onboarding. Same nullable-means-unchanged convention as every field
    /// above, so an established client that never sends it keeps working.</summary>
    DateOnly? DateOfBirth = null);
/// <summary>Partial update of the caller's profile-visibility flags (D-219). Every field is nullable —
/// absent means "leave unchanged", matching <see cref="UpdateProfileBody"/>'s convention. The per-section
/// visibility map planned for Phase 1 lands as an additional optional property here, so a client written
/// against this body today keeps working unchanged.</summary>
public record UpdatePrivacyBody(
    bool? ProfilePublic, bool? ShowAttended, bool? ShowCertificates, bool? ShowAllies,
    /// <summary>Per-section visibility (D-221), e.g. <c>{"attended":"connections"}</c>. Optional and
    /// additive: a client written against the four booleans alone keeps working, and a section named
    /// here overrides the boolean that covers it.</summary>
    Dictionary<string, string>? Sections = null);
public record PhoneChangeBody(string Phone, string Code);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/v1/auth").WithTags("auth");

        auth.MapPost("/otp/request", async (OtpRequestBody body, HttpContext http, IAuthService svc, CancellationToken ct) =>
        {
            var result = await svc.RequestOtpAsync(body.Phone, http.Connection.RemoteIpAddress?.ToString(), ct);
            if (result.Ok) return Results.Ok(OperationAck.Success);
            // Both throttles carry a retry hint, so both are 429 — "resend_cooldown" arrives now that the
            // OTP platform (which enforces a per-destination resend spacing) backs this endpoint.
            return result.Error is "rate_limited" or "resend_cooldown"
                ? ProblemResults.Problem(result.Error, StatusCodes.Status429TooManyRequests,
                    new Dictionary<string, object?> { ["retryAfterSeconds"] = result.RetryAfterSeconds })
                : ProblemResults.Problem(result.Error, StatusCodes.Status400BadRequest);
        }).RequireRateLimiting("otp").WithValidation<OtpRequestBody>().Produces<OperationAck>();

        auth.MapPost("/otp/verify", async (OtpVerifyBody body, IAuthService svc, CancellationToken ct) =>
        {
            var result = await svc.VerifyOtpAsync(body.Phone, body.Code, ct);
            return result.Ok ? Results.Ok(ToTokenResponse(result)) : ProblemResults.Problem(result.Error, StatusCodes.Status401Unauthorized);
        }).RequireRateLimiting("otp").WithValidation<OtpVerifyBody>().Produces<OtpSessionTokens>();

        auth.MapPost("/refresh", async (RefreshBody body, IAuthService svc, CancellationToken ct) =>
        {
            var proof = body.DeviceId is Guid deviceId && body.Signature is { Length: > 0 } signature
                ? new DeviceProof(deviceId, signature)
                : null;
            var result = await svc.RefreshAsync(body.RefreshToken, proof, ct);
            return result.Ok ? Results.Ok(ToTokenResponse(result)) : ProblemResults.Problem(result.Error, StatusCodes.Status401Unauthorized);
            // Deliberately NOT on the "otp" policy (20/min/IP). Refresh is not a guessable credential — the
            // token is 32 bytes of CSPRNG and presenting a rotated one revokes the whole session family — so
            // a tighter cap buys no security. It would cost availability: India is heavily CGNAT'd, so one
            // carrier address fronts many users, and 20 refreshes a minute across all of them is reachable.
            // The global anonymous limiter (60/min/IP) already bounds this endpoint.
        }).WithValidation<RefreshBody>().Produces<OtpSessionTokens>();

        auth.MapPost("/logout", async (RefreshBody body, IAuthService svc, CancellationToken ct) =>
        {
            await svc.LogoutAsync(body.RefreshToken, ct);
            return Results.Ok(OperationAck.Success);
            // Same reasoning as /refresh above: revoking a token you already hold is not an attack worth
            // rate-limiting harder than the global anonymous cap already does.
        }).WithValidation<RefreshBody>().Produces<OperationAck>();

        app.MapGet("/v1/me", async (ClaimsPrincipal principal, KurxDbContext db,
            IIdentityVerificationService identitySvc, ITrustService trust, IPlatformRoleService roles,
            IProfileVisibilityResolver visibility, IPasswordService passwords, IStorage storage,
            CancellationToken ct) =>
        {
            var userId = ParseUserId(principal);
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
            // Onboarding completion spans two tables: the profile fields live on the user row, the
            // password in user_credentials. Reading it here keeps `needs_onboarding` the server's
            // answer rather than something the client infers from which fields came back empty.
            var hasPassword = await passwords.HasPasswordAsync(userId, ct);
            var identity = await identitySvc.GetStatusAsync(userId, ct);       // raw KYC state (M3)
            var caps = await trust.GetUserCapabilitiesAsync(userId, ct);       // derived capabilities (M7)
            // Live platform roles (M2, D-040 / D-056) — the admin console derives its whole RBAC from this,
            // not just the reviewer boolean. Authority still comes from the live per-request policy on each
            // admin endpoint; this is only for showing the right nav. SuperAdmin implies reviewer here too.
            var platformRoles = await roles.GetRolesAsync(userId, ct);
            var sections = await visibility.GetSettingsAsync(userId, ct);
            var isReviewer = platformRoles.Contains(PlatformRole.SuperAdmin)
                || platformRoles.Contains(PlatformRole.VerificationReviewer);
            return Results.Ok(new MeResponse(
                Id: user.Id,
                // Canonical E.164 where the D-089 backfill has reached this row, legacy digits otherwise —
                // the same projection SecurityCenterService and RegistrationService already use. This
                // endpoint alone returned the raw legacy column, so the API answered "what is this user's
                // phone number" two different ways depending on which route you asked.
                Phone: user.PhoneE164 ?? user.Phone,
                Name: user.Name,
                Username: user.Username,
                Email: user.Email,
                // Whether that address was actually proven, not merely entered. The caller's own
                // verification surface has to distinguish the two, and every other source of this fact is
                // a *public* read — `PublicProfileService` exposes it, which needs a claimed username and
                // a visible profile, so a user with neither could not see the state of their own email.
                EmailVerified: user.EmailVerifiedAt is not null,
                // The editable display fields (D-219). Before this, the only way to read your own
                // headline/bio/skills was the *public* profile endpoint — which fails for an unclaimed
                // username or a non-public profile, and a client that prefilled an edit form from it
                // silently wiped those fields on save.
                Headline: user.Headline,
                Bio: user.Bio,
                EducationJson: user.EducationJson,
                Skills: user.Skills,
                Languages: user.Languages,
                Interests: user.Interests,
                LinksJson: user.LinksJson,
                AvatarKey: user.AvatarKey,
                CoverKey: user.CoverKey,
                // D-302 applied to the profile: the key identifies the object, the URL fetches it. Both
                // go out, because the edit form round-trips the key while every renderer needs the URL.
                AvatarUrl: await storage.PresignOrNullAsync(user.AvatarKey, ct),
                CoverUrl: await storage.PresignOrNullAsync(user.CoverKey, ct),
                Privacy: PrivacyView(user, sections),
                DateOfBirth: user.DateOfBirth,
                CreatedAt: user.CreatedAt,
                NeedsOnboarding: Onboarding.IsIncomplete(user, hasPassword),
                IsPlatformReviewer: isReviewer,
                PlatformRoles: platformRoles.Select(r => r.ToString()).OrderBy(str => str).ToArray(),
                Identity: new MeIdentityView(identity.Level, identity.Status),
                // Reused rather than re-projected: TrustService already returns exactly these six
                // fields, and a second copy here would be free to drift from the values that gate the
                // money path.
                Trust: caps));
        }).RequireAuthorization().WithTags("me").Produces<MeResponse>();

        app.MapGet("/v1/usernames/availability", async (string username, KurxDbContext db, CancellationToken ct) =>
        {
            var normalized = username.ToLowerInvariant();
            var formatError = ReservedUsernames.Validate(normalized);
            var status = formatError switch
            {
                "invalid_username_format" => "invalid",
                "reserved_username" => "reserved",
                _ => null,
            };

            if (status is null)
            {
                var holdCutoff = DateTime.UtcNow.AddDays(-30);
                var taken = await db.Users.AnyAsync(u => u.Username == normalized, ct)
                    || await db.Organizations.AnyAsync(o => o.Slug == normalized, ct)
                    || await db.UsernameHistory.AnyAsync(h => h.Username == normalized && h.ReleasedAt > holdCutoff, ct);
                status = taken ? "unavailable" : "available";
            }

            return Results.Ok(new { username = normalized, status });
        }).WithTags("me");

        app.MapPatch("/v1/me/profile", async (UpdateProfileBody body, ClaimsPrincipal principal, KurxDbContext db,
            Kurx.Infrastructure.Providers.UploadScanGate scanGate, IStorage storage, CancellationToken ct) =>
        {
            var userId = ParseUserId(principal);
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

            // D-354 — the key has to be one this caller was actually issued.
            //
            // It was persisted as an arbitrary string: nothing checked it began with this caller's own
            // prefix, so anyone could PATCH `users/{someone-else}/avatar/{guid}` onto their own profile,
            // and every surface that renders an avatar would presign and serve another user's private
            // upload under their name. The prefix is exactly what
            // `MediaService.PresignProfileImageAsync` mints (`users/{userId}/{slot}/{guid}`), which is
            // what makes the presign the only way to obtain a usable key — the same pairing
            // `DesignService.Validate` relies on for design assets.
            //
            // First, before the scan: it costs no IO, and a key belonging to someone else must be
            // refused whether or not a scanner happens to be reachable.
            if (!OwnsKey(body.AvatarKey, userId, "avatar")) return InvalidKey();
            if (!OwnsKey(body.CoverKey, userId, "cover")) return InvalidKey();

            // D-338 — this is where a presigned avatar/cover key becomes real, so it is where the bytes are
            // scanned. Before any mutation below: a rejected image must not leave a half-applied profile
            // update behind, and these two keys are rendered directly in every client.
            if (await scanGate.RejectAsync(body.AvatarKey, userId, "users", userId, ct) is { } avatarScanError)
                return ProblemResults.Problem(avatarScanError, StatusCodes.Status400BadRequest);
            if (await scanGate.RejectAsync(body.CoverKey, userId, "users", userId, ct) is { } coverScanError)
                return ProblemResults.Problem(coverScanError, StatusCodes.Status400BadRequest);

            // D-354 — and it has to point at something.
            //
            // Last, because a real scanner already covers this: `ClamAvFileScanner` reads the bytes
            // through IStorage, so an absent key comes back ScanFailed and is refused above with the
            // more specific `scan_unavailable`. This closes the case the scanner cannot — FILE_SCANNER=none,
            // where NoOpFileScanner reports a key Clean WITHOUT READING IT. Without it a typo became a
            // permanently broken image that clients cannot tell from "no picture", which is exactly the
            // null-vs-broken distinction `StorageUrls.PresignOrNullAsync` exists to protect.
            if (!await ExistsIfClaimedAsync(body.AvatarKey, storage, ct)) return InvalidKey();
            if (!await ExistsIfClaimedAsync(body.CoverKey, storage, ct)) return InvalidKey();

            if (body.Username is not null)
            {
                var normalized = body.Username.ToLowerInvariant();
                if (normalized != user.Username)
                {
                    var formatError = ReservedUsernames.Validate(normalized);
                    if (formatError is not null) return ProblemResults.Problem(formatError, StatusCodes.Status400BadRequest);

                    if (await db.Organizations.AnyAsync(o => o.Slug == normalized, ct))
                        return ProblemResults.Problem("username_taken", StatusCodes.Status409Conflict);

                    if (await db.Users.AnyAsync(u => u.Username == normalized && u.Id != userId, ct))
                        return ProblemResults.Problem("username_taken", StatusCodes.Status409Conflict);

                    var holdCutoff = DateTime.UtcNow.AddDays(-30);
                    if (await db.UsernameHistory.AnyAsync(h => h.Username == normalized && h.ReleasedAt > holdCutoff, ct))
                        return ProblemResults.Problem("username_taken", StatusCodes.Status409Conflict);

                    if (user.UsernameChangedAt.HasValue &&
                        (DateTime.UtcNow - user.UsernameChangedAt.Value).TotalDays < 30)
                        return ProblemResults.Problem("username_change_throttled", StatusCodes.Status429TooManyRequests);

                    if (user.Username is not null)
                        db.UsernameHistory.Add(new Kurx.Domain.Entities.UsernameHistory
                        {
                            UserId = userId,
                            Username = user.Username,
                            ReleasedAt = DateTime.UtcNow,
                        });

                    user.Username = normalized;
                    user.UsernameChangedAt = DateTime.UtcNow;
                }
            }

            if (body.Name is not null) user.Name = body.Name;
            if (body.Headline is not null) user.Headline = body.Headline;
            if (body.Bio is not null) user.Bio = body.Bio;

            if (body.DateOfBirth is { } dateOfBirth)
            {
                // Server-authoritative: the client's date picker bounds are a convenience, not the rule.
                var ageError = Onboarding.ValidateDateOfBirth(
                    dateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow));
                if (ageError is not null)
                    return ProblemResults.Problem(ageError, StatusCodes.Status400BadRequest);
                user.DateOfBirth = dateOfBirth;
            }
            if (body.EducationJson is not null) user.EducationJson = body.EducationJson;
            if (body.Skills is not null) user.Skills = body.Skills;
            if (body.Languages is not null) user.Languages = body.Languages;
            if (body.Interests is not null) user.Interests = body.Interests;
            if (body.LinksJson is not null) user.LinksJson = body.LinksJson;
            if (body.AvatarKey is not null) user.AvatarKey = body.AvatarKey;
            if (body.CoverKey is not null) user.CoverKey = body.CoverKey;

            if (body.Language is not null)
            {
                // Allow-listed rather than free-form: the value selects a resource file, and an
                // unrecognised culture would silently fall back while looking like it was applied.
                var language = body.Language.Trim().ToLowerInvariant();
                if (language is not ("en" or "hi"))
                    return ProblemResults.Problem("invalid_language", StatusCodes.Status400BadRequest);
                user.Language = language;
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                id = user.Id,
                name = user.Name,
                username = user.Username,
                headline = user.Headline,
                bio = user.Bio,
                date_of_birth = user.DateOfBirth,
                education_json = user.EducationJson,
                skills = user.Skills,
                languages = user.Languages,
                interests = user.Interests,
                links_json = user.LinksJson,
                avatar_key = user.AvatarKey,
                cover_key = user.CoverKey,
                // Same pair as GET /v1/me. The write response is what a client re-renders from after a
                // save, so returning the key alone would blank the picture at the exact moment it worked.
                avatar_url = await storage.PresignOrNullAsync(user.AvatarKey, ct),
                cover_url = await storage.PresignOrNullAsync(user.CoverKey, ct),
                username_changed_at = user.UsernameChangedAt,
            });
        }).RequireAuthorization().WithTags("me").WithValidation<UpdateProfileBody>();

        // The write surface for the four profile-visibility flags (D-219). Every one of them was already
        // enforced on every public read path but settable by nothing — so ShowAttended, which defaults to
        // false, hid the attended-events lane for every user with no way to turn it on.
        app.MapPatch("/v1/me/privacy", async (UpdatePrivacyBody body, ClaimsPrincipal principal,
            KurxDbContext db, IProfileVisibilityResolver visibility, CancellationToken ct) =>
        {
            var userId = ParseUserId(principal);
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

            // The four booleans are expressed as section changes so both shapes go through one writer
            // (D-221) — the resolver dual-writes the booleans back, keeping the legacy read path exact.
            var changes = new Dictionary<ProfileSection, SectionVisibility>();
            void Set(ProfileSection section, bool visible) =>
                changes[section] = visible ? SectionVisibility.Public : SectionVisibility.OnlyMe;

            if (body.ProfilePublic is bool profilePublic) Set(ProfileSection.Profile, profilePublic);
            if (body.ShowAttended is bool showAttended) Set(ProfileSection.Attended, showAttended);
            if (body.ShowCertificates is bool showCertificates) Set(ProfileSection.Certificates, showCertificates);
            if (body.ShowAllies is bool showAllies) Set(ProfileSection.Network, showAllies);

            // Applied after the booleans so an explicit section wins when a request sends both.
            foreach (var (rawSection, rawTier) in body.Sections ?? [])
            {
                if (!ProfileVisibilityResolver.TryParseSection(rawSection, out var section))
                    return ProblemResults.Problem("invalid_section", StatusCodes.Status400BadRequest);
                if (!ProfileVisibilityResolver.TryParseTier(rawTier, out var tier))
                    return ProblemResults.Problem("invalid_visibility", StatusCodes.Status400BadRequest);
                changes[section] = tier;
            }

            var settings = await visibility.UpdateSettingsAsync(userId, changes, ct);
            await db.Entry(user).ReloadAsync(ct);
            return Results.Ok(PrivacyView(user, settings));
        }).RequireAuthorization().WithTags("me").Produces<MePrivacyView>();

        // Phone is the primary auth identifier (ADR-A6): changing it decides who can log in, which makes
        // this the most dangerous write on the account. It already revoked every session (D-038); D-263
        // adds the step-up gate and the notice to the number being replaced.
        app.MapPost("/v1/me/phone/verify", async (PhoneChangeBody body, ClaimsPrincipal principal,
            IAuthService svc, IStepUpService stepUp, INotificationService notifications, CancellationToken ct) =>
        {
            var userId = ParseUserId(principal);
            if (await StepUpGuard.RequireAsync(userId, stepUp, ct) is { } denied) return denied;

            var result = await svc.VerifyPhoneChangeAsync(userId, body.Phone, body.Code, ct);
            if (!result.Ok) return ProblemResults.Problem(result.Error, StatusCodes.Status400BadRequest);

            // Sent after the change, because the in-app record is what the owner finds when they get
            // back in. The security category is never suppressible (D-263), so a muted user still sees it.
            try
            {
                await notifications.NotifyAsync(userId, "phone_change_completed", "Phone number changed",
                    "The phone number on your Kurx account was changed and all other sessions were signed out.",
                    new { notificationType = "security", route = "/settings/security" }, ct);
            }
            catch { /* a failed notice must not undo a completed change */ }

            return Results.Ok(ToTokenResponse(result));
        }).RequireAuthorization().WithValidation<PhoneChangeBody>().WithTags("me").Produces<OtpSessionTokens>();
    }

    /// <summary>One shape for the caller's visibility settings, shared by <c>GET /v1/me</c> and
    /// <c>PATCH /v1/me/privacy</c> so the two can never disagree. The four booleans remain for
    /// backward compatibility; <c>sections</c> (D-221) is the richer four-tier view of the same state
    /// and is what a client should prefer.</summary>
    /// <summary>The one privacy projection, shared by <c>GET /v1/me</c> and
    /// <c>PATCH /v1/me/privacy</c>. Typed rather than anonymous so both endpoints appear in the
    /// contract; the emitted keys are unchanged, because the response converter derives them from the
    /// property names (<c>ProfilePublic</c> → <c>profile_public</c>).</summary>
    private static MePrivacyView PrivacyView(
        Kurx.Domain.Entities.User user,
        IReadOnlyDictionary<ProfileSection, SectionVisibility>? sections = null) => new(
        ProfilePublic: user.ProfilePublic,
        ShowAttended: user.ShowAttended,
        ShowCertificates: user.ShowCertificates,
        ShowAllies: user.ShowAllies,
        Sections: sections?.ToDictionary(
            kv => ProfileVisibilityResolver.SectionKey(kv.Key),
            kv => ProfileVisibilityResolver.TierKey(kv.Value)));

    /// <summary>Whether a claimed profile-image key sits under this caller's own presign prefix (D-354).
    ///
    /// <para>A null or blank key is "leave it alone" / "remove it" and passes — the partial-update
    /// convention every other field on this body follows.</para></summary>
    private static bool OwnsKey(string? key, Guid userId, string slot) =>
        string.IsNullOrWhiteSpace(key)
        || key.Trim().StartsWith($"users/{userId}/{slot}/", StringComparison.Ordinal);

    /// <summary>Whether a claimed key points at a stored object (D-354). Vacuously true when no key was
    /// claimed, for the same partial-update reason as <see cref="OwnsKey"/>.</summary>
    private static async Task<bool> ExistsIfClaimedAsync(string? key, IStorage storage, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(key) || await storage.ExistsAsync(key.Trim(), ct);

    /// <summary>One code for both refusals. Telling "that key is not yours" apart from "that key does
    /// not exist" would turn this endpoint into an oracle for which storage keys are real, which is the
    /// enumeration leak D-018 closes everywhere else.</summary>
    private static IResult InvalidKey() =>
        ProblemResults.Problem("invalid_storage_key", StatusCodes.Status400BadRequest);

    private static Guid ParseUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static OtpSessionTokens ToTokenResponse(AuthResult result) => new(
        result.Tokens!.AccessToken,
        result.Tokens.AccessExpiresAt,
        result.Tokens.RefreshToken,
        result.Tokens.RefreshExpiresAt,
        result.UserId,
        result.IsNewUser);
}
