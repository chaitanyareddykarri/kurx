using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Posts;

public partial class PostService
{
    // ── Create ─────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<PostView>> CreateAsync(Guid authorId, string? body, string? kind, string? visibility,
        IReadOnlyList<Guid>? mediaIds, Guid? eventId, Guid? sharedPostId, PostPollInput? poll, CancellationToken ct = default)
    {
        body = (body ?? "").Trim();
        if (body.Length > MaxBodyChars) return ServiceResult<PostView>.Fail("body_too_long");

        var parsedVisibility = ParseVisibility(visibility);
        if (parsedVisibility is null) return ServiceResult<PostView>.Fail("invalid_visibility");

        // ── Reshare target ────────────────────────────────────────────────────
        // Sharing a share re-targets the ORIGINAL rather than nesting (D-262), so render depth is bounded
        // by the data instead of by client discipline. cannot_share_a_share is what is left when there is
        // nothing to re-target to — the original is deleted, hidden, or not visible to this sharer.
        Post? shareTarget = null;
        if (sharedPostId.HasValue)
        {
            var (target, error) = await LoadVisibleAsync(sharedPostId.Value, authorId, ct);
            if (target is null) return ServiceResult<PostView>.Fail(error);

            if (target.Kind == PostKind.Share)
            {
                if (target.SharedPostId is null) return ServiceResult<PostView>.Fail("cannot_share_a_share");
                var (original, _) = await LoadVisibleAsync(target.SharedPostId.Value, authorId, ct);
                if (original is null) return ServiceResult<PostView>.Fail("cannot_share_a_share");
                target = original;
            }
            shareTarget = target;
        }

        // ── Event attachment ──────────────────────────────────────────────────
        if (eventId.HasValue)
        {
            // One authority, resolved live per request (D-015/D-272). No admin bypass: posting is speech,
            // not moderation, so a platform admin gets no standing they did not earn — preserved exactly
            // from the membership check this replaced, which passed no admin either.
            var access = await authority.ResolveAsync(authorId, eventId.Value, isAdmin: false, ct);
            if (!access.EventExists) return ServiceResult<PostView>.Fail("event_not_found");
            if (!access.Can(EventPermission.Participate)) return ServiceResult<PostView>.Fail("not_event_participant");
        }

        // event_participants scopes to a specific event's audience, so it is meaningless without one.
        if (parsedVisibility == PostVisibility.EventParticipants && eventId is null)
            return ServiceResult<PostView>.Fail("invalid_visibility");

        // ── Media ─────────────────────────────────────────────────────────────
        var claimed = new List<PostMedia>();
        if (mediaIds is { Count: > 0 })
        {
            var ids = mediaIds.Distinct().ToList();
            if (ids.Count != mediaIds.Count) return ServiceResult<PostView>.Fail("invalid_media");

            // Only this author's own confirmed, unclaimed uploads. That quadruple check is what stops a
            // caller attaching someone else's file, an unverified one, or one that already belongs to a post.
            claimed = await db.PostMedia
                .Where(m => ids.Contains(m.Id) && m.UploadedBy == authorId
                         && m.PostId == null && m.IsConfirmed && m.DeletedAt == null)
                .ToListAsync(ct);
            if (claimed.Count != ids.Count) return ServiceResult<PostView>.Fail("invalid_media");

            var capError = CheckMediaCaps(claimed);
            if (capError is not null) return ServiceResult<PostView>.Fail(capError);

            // Render order comes from the order the author submitted the ids in, not from upload time.
            foreach (var m in claimed) m.Sort = ids.IndexOf(m.Id);
        }

        // ── Poll ──────────────────────────────────────────────────────────────
        List<PostPollOptionInput> pollOptions = [];
        if (poll is not null)
        {
            pollOptions = (poll.Options ?? [])
                .Select(o => new PostPollOptionInput((o.Text ?? "").Trim()))
                .Where(o => o.Text.Length > 0).ToList();
            if (pollOptions.Count < 2) return ServiceResult<PostView>.Fail("poll_needs_two_options");
            if (string.IsNullOrWhiteSpace(poll.Question)) return ServiceResult<PostView>.Fail("poll_needs_two_options");
            // A poll that closed before it opened can never be voted on.
            if (poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow)
                return ServiceResult<PostView>.Fail("poll_closed");
        }

        // A post with no body needs something else to be about. The client's `kind` is advisory: the kind
        // is DERIVED from what is actually attached, so the discriminator can never disagree with the
        // payload it describes.
        var derivedKind = DeriveKind(claimed, poll is not null, shareTarget is not null, eventId.HasValue);
        if (body.Length == 0 && derivedKind == PostKind.Text) return ServiceResult<PostView>.Fail("body_required");

        var post = new Post
        {
            AuthorId = authorId,
            Kind = derivedKind,
            Body = body,
            Visibility = parsedVisibility.Value,
            EventId = eventId,
            SharedPostId = shareTarget?.Id,
        };
        db.Posts.Add(post);

        foreach (var m in claimed) m.PostId = post.Id;

        if (poll is not null)
        {
            var pollRow = new PostPoll
            {
                PostId = post.Id,
                Question = poll.Question.Trim(),
                AllowMultiple = poll.AllowMultiple,
                ClosesAt = poll.ClosesAt,
            };
            db.PostPolls.Add(pollRow);
            for (var i = 0; i < pollOptions.Count; i++)
                db.PostPollOptions.Add(new PostPollOption { PollId = pollRow.Id, Text = pollOptions[i].Text, Sort = i });
        }

        var mentionedUserIds = await SyncTagsAndMentionsAsync(post.Id, body, existing: false, ct);

        await db.SaveChangesAsync(ct);

        // The original's share counter, computed by the database (D-240). A post being reshared by many
        // people at once is exactly the case a read-modify-write loses.
        if (shareTarget is not null)
            await db.Posts.Where(p => p.Id == shareTarget.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ShareCount, p => p.ShareCount + 1), ct);

        await NotifyMentionsAsync(post, mentionedUserIds, ct);

        var views = await BuildViewsAsync([post], authorId, includeShared: true, ct);
        return ServiceResult<PostView>.Success(views[0]);
    }

    /// <summary>What the post IS, read off what it carries. Order matters: a reshare with a comment is a
    /// share, and a poll attached to an event is a poll.</summary>
    private static PostKind DeriveKind(List<PostMedia> media, bool hasPoll, bool isShare, bool hasEvent)
    {
        if (isShare) return PostKind.Share;
        if (hasPoll) return PostKind.Poll;
        if (media.Any(m => m.Kind == PostMediaKind.Video)) return PostKind.Video;
        if (media.Any(m => m.Kind == PostMediaKind.Image)) return PostKind.Images;
        if (media.Any(m => m.Kind == PostMediaKind.Document)) return PostKind.Document;
        return hasEvent ? PostKind.Event : PostKind.Text;
    }

    private static string? CheckMediaCaps(List<PostMedia> media)
    {
        if (media.Count(m => m.Kind == PostMediaKind.Image) > PostMediaPolicy.MaxImages) return "too_many_media";
        if (media.Count(m => m.Kind == PostMediaKind.Video) > PostMediaPolicy.MaxVideos) return "too_many_media";
        if (media.Count(m => m.Kind == PostMediaKind.Document) > PostMediaPolicy.MaxDocuments) return "too_many_media";
        return null;
    }

    /// <summary>Replaces a post's hashtag and mention rows from its body and returns the users mentioned.
    /// Extraction is server-side and only server-side (D-262) — a client-supplied tag list would let a
    /// caller attach their post to any trend, and a client-supplied mention list would let them notify
    /// anyone.</summary>
    private async Task<List<Guid>> SyncTagsAndMentionsAsync(Guid postId, string body, bool existing, CancellationToken ct)
    {
        if (existing)
        {
            await db.PostHashtags.Where(h => h.PostId == postId).ExecuteDeleteAsync(ct);
            await db.PostMentions.Where(m => m.PostId == postId).ExecuteDeleteAsync(ct);
        }

        foreach (var tag in PostTextParser.Hashtags(body))
            db.PostHashtags.Add(new PostHashtag { PostId = postId, Tag = tag });

        var handles = PostTextParser.MentionHandles(body);
        if (handles.Count == 0) return [];

        // Unresolvable handles are dropped silently: a mention is a link plus a notification, and neither
        // means anything without an account behind it.
        var users = await db.Users.AsNoTracking()
            .Where(u => u.Username != null && handles.Contains(u.Username))
            .Select(u => u.Id).ToListAsync(ct);
        foreach (var userId in users)
            db.PostMentions.Add(new PostMention { PostId = postId, MentionedUserId = userId });
        return users;
    }

    // ── Update / delete ────────────────────────────────────────────────────────

    public async Task<ServiceResult<PostView>> UpdateAsync(Guid postId, Guid actorId, string? body, string? visibility, CancellationToken ct = default)
    {
        var (visible, error) = await LoadVisibleAsync(postId, actorId, ct);
        if (visible is null) return ServiceResult<PostView>.Fail(error);
        if (visible.AuthorId != actorId) return ServiceResult<PostView>.Fail("not_post_author");

        var post = await db.Posts.FirstAsync(p => p.Id == postId, ct);

        if (visibility is not null)
        {
            var parsed = ParseVisibility(visibility);
            if (parsed is null) return ServiceResult<PostView>.Fail("invalid_visibility");
            if (parsed == PostVisibility.EventParticipants && post.EventId is null)
                return ServiceResult<PostView>.Fail("invalid_visibility");
            post.Visibility = parsed.Value;
        }

        List<Guid> newMentions = [];
        if (body is not null)
        {
            var trimmed = body.Trim();
            if (trimmed.Length > MaxBodyChars) return ServiceResult<PostView>.Fail("body_too_long");
            // A text post cannot be emptied into nothing; one carrying media or a poll still has content.
            if (trimmed.Length == 0 && post.Kind == PostKind.Text) return ServiceResult<PostView>.Fail("body_required");

            var alreadyMentioned = await db.PostMentions.AsNoTracking()
                .Where(m => m.PostId == postId).Select(m => m.MentionedUserId).ToListAsync(ct);

            post.Body = trimmed;
            var mentioned = await SyncTagsAndMentionsAsync(postId, trimmed, existing: true, ct);
            // Only people the edit ADDED are notified — re-notifying everyone on every typo fix would
            // make the edit button a broadcast tool.
            newMentions = mentioned.Except(alreadyMentioned).ToList();
        }

        post.EditedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await NotifyMentionsAsync(post, newMentions, ct);

        var views = await BuildViewsAsync([post], actorId, includeShared: true, ct);
        return ServiceResult<PostView>.Success(views[0]);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid postId, Guid actorId, bool isModerator, CancellationToken ct = default)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);
        // A moderator can reach a hidden post; everyone else gets the same answer they would for a post
        // that never existed (D-018).
        if (post is null || (!isModerator && post.IsHidden && post.AuthorId != actorId))
            return ServiceResult<bool>.Fail("post_not_found");
        if (post.AuthorId != actorId && !isModerator) return ServiceResult<bool>.Fail("not_post_author");

        post.IsDeleted = true;
        post.DeletedBy = actorId;

        // Marked, not removed: the storage objects go in the sweep, so deletion stays fast and the sweep
        // stays idempotent — the same split chat attachments use.
        await db.PostMedia.Where(m => m.PostId == postId && m.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.DeletedAt, DateTime.UtcNow), ct);

        // Deleting a reshare gives the share back. Without this the original's counter only ever climbs,
        // so a post that was shared and un-shared a hundred times reads as a hundred live shares — the
        // increment on create had a matching decrement nowhere.
        if (post.Kind == PostKind.Share && post.SharedPostId is { } originalId)
            await db.Posts.Where(p => p.Id == originalId && p.ShareCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ShareCount, p => p.ShareCount - 1), ct);

        audit.Write(new AuditEvent("post.delete", "posts", postId,
            ActorType: isModerator && post.AuthorId != actorId ? "admin" : "user",
            ActorId: actorId));
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // ── Media (two-step, mirroring chat attachments D-110) ─────────────────────

    public async Task<ServiceResult<PostMediaPresignView>> PresignMediaAsync(Guid userId, string fileName, string contentType,
        long sizeBytes, CancellationToken ct = default)
    {
        // Cheap pre-checks only. Everything here is client-declared, so every one of them runs again on
        // confirm against the real bytes — this pass exists to fail fast, not to be trusted.
        var safeName = Chat.AttachmentPolicy.SanitizeFileName(fileName);
        if (!PostMediaPolicy.IsAllowedContentType(contentType ?? ""))
            return ServiceResult<PostMediaPresignView>.Fail("invalid_media");
        if (!PostMediaPolicy.ExtensionMatches(contentType!, safeName))
            return ServiceResult<PostMediaPresignView>.Fail("invalid_media");

        var kind = PostMediaPolicy.KindOf(contentType!)!.Value;
        if (sizeBytes <= 0 || sizeBytes > PostMediaPolicy.MaxBytesFor(kind))
            return ServiceResult<PostMediaPresignView>.Fail("invalid_media");

        var media = new PostMedia
        {
            UploadedBy = userId,
            Kind = kind,
            // Server-generated key with the sanitized filename as its final segment, so confirm — which
            // receives only a key — can still recover the display name. SanitizeFileName has already
            // stripped every path character, so the client cannot influence where the bytes land.
            StorageKey = $"posts/{userId}/{Guid.CreateVersion7():N}/{safeName}",
            FileName = safeName,
            ContentType = contentType!,
            SizeBytes = sizeBytes,
        };
        db.PostMedia.Add(media);
        await db.SaveChangesAsync(ct);

        var upload = await storage.PresignPutAsync(media.StorageKey, media.ContentType, PostMediaPolicy.MaxBytesFor(kind), ct);
        return ServiceResult<PostMediaPresignView>.Success(
            new PostMediaPresignView(media.Id, upload.Url, upload.Key));
    }

    public async Task<ServiceResult<PostMediaView>> ConfirmMediaAsync(Guid userId, Guid mediaId, string storageKey, CancellationToken ct = default)
    {
        var media = await db.PostMedia.FirstOrDefaultAsync(m => m.Id == mediaId, ct);
        // Ownership before anything else: a media id is a guessable-shaped Guid and confirming someone
        // else's upload would hand the caller a signed URL to bytes they never uploaded.
        if (media is null || media.UploadedBy != userId || media.DeletedAt is not null)
            return ServiceResult<PostMediaView>.Fail("invalid_media");
        if (!string.Equals(media.StorageKey, storageKey, StringComparison.Ordinal))
            return ServiceResult<PostMediaView>.Fail("invalid_media");

        // Idempotent: a retried confirm returns what the first one produced.
        if (media.IsConfirmed) return ServiceResult<PostMediaView>.Success(await ToMediaViewAsync(media, ct));

        if (!await storage.ExistsAsync(media.StorageKey, ct)) return ServiceResult<PostMediaView>.Fail("invalid_media");

        // The authoritative pass: the first moment the server can look at the actual bytes.
        var bytes = await storage.GetAsync(media.StorageKey, ct);
        var inspected = PostMediaPolicy.InspectAgainstAllowList(bytes, media.ContentType);
        if (inspected is null) return ServiceResult<PostMediaView>.Fail("invalid_media");

        // Always scanned, even when the configured scanner is a no-op — that is what keeps this call site
        // stable when a real scanner is wired.
        var verdict = await scanner.ScanAsync(media.StorageKey, ct);
        if (verdict != FileScanResult.Clean)
        {
            audit.Write(new AuditEvent("post.media_rejected", "post_media", media.Id,
                ActorId: userId,
                After: new { verdict = verdict.ToString() }));
            // Infected or unverifiable content must never become reachable, and removing it now means the
            // sweep has nothing left to find.
            media.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await TryDeleteObjectAsync(media.StorageKey, ct);
            return ServiceResult<PostMediaView>.Fail("invalid_media");
        }

        media.Kind = inspected.Kind;
        media.ContentType = inspected.ContentType;
        media.SizeBytes = inspected.SizeBytes;
        media.Width = inspected.Width;
        media.Height = inspected.Height;
        media.IsConfirmed = true;
        await db.SaveChangesAsync(ct);

        return ServiceResult<PostMediaView>.Success(await ToMediaViewAsync(media, ct));
    }

    private async Task TryDeleteObjectAsync(string storageKey, CancellationToken ct)
    {
        try
        {
            if (storage is Providers.LocalDiskStorage disk) await disk.DeleteAsync(storageKey, ct);
            // Other providers gain deletion when they are built; the row stays marked so a later sweep can
            // still remove the object rather than losing track of it.
        }
        catch
        {
            // Never fail a user-facing operation because cleanup could not reach storage.
        }
    }

    /// <summary>Notifies people a post mentions. Never the author about their own post, and never allowed
    /// to fail the write that triggered it — the post is already committed by the time this runs.
    ///
    /// <para><b>Gated on the recipient's own visibility.</b> A mention is the one notification whose
    /// recipient list the author picks freely, so without this check <c>@victim</c> inside an
    /// <c>only_me</c> post is a notification channel to anyone on the platform, carrying a deep link they
    /// will only ever get a 404 for. The rule: you are told you were mentioned only in a post you could
    /// have read anyway.</para></summary>
    private async Task NotifyMentionsAsync(Post post, IReadOnlyList<Guid> mentionedUserIds, CancellationToken ct)
    {
        if (mentionedUserIds.Count == 0) return;
        var authorName = await db.Users.AsNoTracking().Where(u => u.Id == post.AuthorId)
            .Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "Someone";

        foreach (var userId in mentionedUserIds.Where(id => id != post.AuthorId))
        {
            // Bounded by the 30-mention extraction cap, and each is one indexed lookup.
            if (!await VisibleTo(db.Posts.AsNoTracking(), userId).AnyAsync(p => p.Id == post.Id, ct)) continue;
            await NotifySafeAsync(userId, "post_mention", authorName,
                $"{authorName} mentioned you in a post", post.Id, null, post.AuthorId, ct);
        }
    }

    private async Task NotifySafeAsync(Guid userId, string kind, string title, string body,
        Guid postId, Guid? commentId, Guid actorId, CancellationToken ct)
    {
        try
        {
            await notifications.NotifyAsync(userId, kind, title, body, new
            {
                notificationType = "post",
                postId,
                commentId,
                actorId,
                // deep_link is what the Notification entity documents; `route` is the newer in-app path
                // convention (NotificationKinds). Both clients read one of the two, so both are sent.
                deep_link = $"/posts/{postId}",
                route = $"/posts/{postId}",
            }, ct);
        }
        catch
        {
            // Swallowed by design: a failed notice must not undo a completed write.
        }
    }
}
