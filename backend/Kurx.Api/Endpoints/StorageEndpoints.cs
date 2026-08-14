using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Providers;

namespace Kurx.Api.Endpoints;

/// <summary>
/// The receiver a <see cref="LocalDiskStorage"/> presigned URL points at (D-110).
///
/// **This fixes a repo-wide bug, not just chat.** `LocalDiskStorage.PresignPutAsync` has always
/// returned a URL to a route that did not exist, so every presigned upload in the product — event
/// media, org verification documents, membership-claim evidence, representation requests — handed
/// the client a URL that 404s. Nothing had ever been uploaded through that path.
///
/// Authorization here is **the signature, not the session**, exactly as with S3: presigned URLs are
/// meant to be usable by a client that cannot attach a bearer token (an <c>&lt;a&gt;</c>, an image
/// tag, a background uploader). The signature carries the key, the expiry, the size ceiling and the
/// content type, so a URL cannot be widened after it was issued.
///
/// Only registered when the storage provider is localdisk. With a real object store the client PUTs
/// straight to the bucket and these routes are never mapped — which is why no caller anywhere knows
/// this exists.
/// </summary>
public static class StorageEndpoints
{
    public static void MapStorageEndpoints(this WebApplication app)
    {
        // Not mapped for real object storage: S3/R2/Azure presign to their own endpoints.
        if (app.Services.GetRequiredService<IStorage>() is not LocalDiskStorage disk) return;

        // Upload. Deliberately AllowAnonymous — the signature is the credential.
        app.MapPut("/v1/storage/{*key}", async (
            string key, HttpRequest request, IStorage storage, CancellationToken ct) =>
        {
            var exp = long.TryParse(request.Query["exp"], out var e) ? e : 0;
            var max = long.TryParse(request.Query["max"], out var m) ? m : 0;
            var contentType = request.Query["ct"].ToString();
            var signature = request.Query["sig"].ToString();

            if (!disk.VerifyPutSignature(key, exp, max, contentType, signature))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            // Refuse before reading a byte when the client declares an oversize body. The streaming
            // guard below is what actually enforces it — Content-Length is a claim, not a fact.
            if (request.ContentLength is > 0 && request.ContentLength > max)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > max)
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }

            await storage.PutAsync(key, buffer.ToArray(), contentType, ct);
            return Results.Ok(new { key });
        }).AllowAnonymous().WithTags("storage");

        // Download. Same signature scheme, and a GET signature can never be replayed as a PUT.
        app.MapGet("/v1/storage/{*key}", async (
            string key, HttpRequest request, IStorage storage, CancellationToken ct) =>
        {
            var exp = long.TryParse(request.Query["exp"], out var e) ? e : 0;
            if (!disk.VerifyGetSignature(key, exp, request.Query["sig"].ToString()))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (!await storage.ExistsAsync(key, ct)) return Results.NotFound();

            var bytes = await storage.GetAsync(key, ct);
            // Always octet-stream: never let a stored object choose how a browser interprets it.
            // Content sniffing an attacker-supplied file into text/html is stored XSS.
            return Results.File(bytes, "application/octet-stream");
        }).AllowAnonymous().WithTags("storage").Produces(StatusCodes.Status200OK, typeof(byte[]), "application/octet-stream");
    }
}
