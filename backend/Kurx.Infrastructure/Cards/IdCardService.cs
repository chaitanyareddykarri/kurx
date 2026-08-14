using System.Security.Cryptography;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Cards;

/// <summary>Issues, edits, renders and verifies ID cards (D-331).
///
/// <para>The load-bearing rule is that the issuing organization asserts the identifiers and the holder
/// cannot reach them. That is enforced in two places on purpose: <see cref="IdCardHolderInput"/> has no
/// field that could carry one, and <see cref="UpdateHolderFieldsAsync"/> writes an explicit list of
/// columns rather than mapping an object — so adding a field to the holder DTO cannot silently make an
/// asserted field writable.</para></summary>
public class IdCardService(
    KurxDbContext db,
    IStorage storage,
    IQrCodeGenerator qr,
    IIdCardRenderer renderer,
    IEventAuthority authority,
    ITrustService trust) : IIdCardService
{
    /// <summary>Where the QR points. Path shape matches the certificate verifier so one scanner
    /// convention covers both documents.</summary>
    private const string VerifyUrlBase = "https://kurx.in/verify/id/";

    public async Task<ServiceResult<IdCardView>> IssueAsync(
        Guid actorId, Guid eventId, IdCardIssueInput input, bool isAdmin, CancellationToken ct = default)
    {
        // D-335 — the issuer is the EVENT's creator/organizer, not an organization. A card is proof of
        // participation in an event, so the authority that can vouch for it is whoever runs that event.
        var ev = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId && e.DeletedAt == null, ct);
        if (ev is null) return ServiceResult<IdCardView>.Fail("not_found");

        // Control over the event, resolved creator-first by IEventAuthority (D-269) rather than by a
        // hand-rolled role list — the mistake this file made in its first version and the one
        // AnalyticsService was fixed for in the same branch.
        var issuerAccess = await authority.ResolveAsync(actorId, eventId, isAdmin, ct);
        if (!issuerAccess.Can(EventPermission.ManageLifecycle))
            return ServiceResult<IdCardView>.Fail("not_event_organizer");

        // A card asserts something about its holder, so the person asserting it has to be someone the
        // platform has actually checked. Gated on the CAPABILITY rather than the raw IdentityVerified
        // fact: D-323's bypass relaxes capabilities for the mock-backed proofs while leaving the facts
        // truthful, so gating on the fact would make the feature untestable outside production while
        // saying nothing extra in it.
        if (!isAdmin)
        {
            var issuerTrust = await trust.GetUserCapabilitiesAsync(actorId, ct);
            if (!issuerTrust.CanCreatePublicEvent)
                return ServiceResult<IdCardView>.Fail("issuer_not_verified");
        }

        // Self-issuance defeats the point: a document you minted for yourself proves only that you can
        // operate a form. Checked before eligibility so the clearer refusal wins.
        if (input.UserId == actorId) return ServiceResult<IdCardView>.Fail("cannot_issue_to_self");

        // The holder must actually be part of this event. Participate is the platform's existing
        // definition of that, so ticket-holders, participants and staff all resolve through one rule.
        var holderAccess = await authority.ResolveAsync(input.UserId, eventId, isAdmin: false, ct);
        if (!holderAccess.Can(EventPermission.Participate))
            return ServiceResult<IdCardView>.Fail("holder_not_a_participant");

        var holder = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == input.UserId, ct);
        if (holder is null) return ServiceResult<IdCardView>.Fail("not_found");

        if (input.ValidFrom is { } from && input.ValidUntil is { } until && until < from)
            return ServiceResult<IdCardView>.Fail("invalid_validity_window");

        // The event's organization is carried for display and for the per-issuer card-number sequence.
        // It is descriptive now, NOT the authority — no verification of it is required (D-335).
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == ev.RepresentingOrgId, ct);

        var card = new IdCard
        {
            OrgId = ev.RepresentingOrgId,
            UserId = input.UserId,
            EventId = eventId,
            VerifyCode = await NextVerifyCodeAsync(ct),
            ShowMealInfo = input.ShowMealInfo,
            CardNumber = await NextCardNumberAsync(ev.RepresentingOrgId, ct),
            StudentId = Trim(input.StudentId),
            Department = Trim(input.Department),
            Course = Trim(input.Course),
            Year = Trim(input.Year),
            ValidFrom = input.ValidFrom,
            ValidUntil = input.ValidUntil,
            Template = ParseTemplate(input.Template),
            PhotoKey = holder.AvatarKey,
            LogoKey = org.LogoKey,
            Status = IdCardStatus.Draft,
            IssuedBy = actorId,
        };

        db.IdCards.Add(card);
        Audit(actorId, isAdmin, "id_card.issued", card.Id, new
        {
            event_id = eventId,
            holder_id = input.UserId,
            card_number = card.CardNumber,
            asserted = new { input.StudentId, input.Department, input.Course, input.Year },
        });
        await db.SaveChangesAsync(ct);

        return ServiceResult<IdCardView>.Success(await ProjectAsync(card, org.Name, holder.Name, ct));
    }

    public async Task<ServiceResult<IdCardView>> UpdateHolderFieldsAsync(
        Guid actorId, Guid cardId, IdCardHolderInput input, CancellationToken ct = default)
    {
        var card = await db.IdCards.FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card is null) return ServiceResult<IdCardView>.Fail("not_found");
        if (card.UserId != actorId) return ServiceResult<IdCardView>.Fail("forbidden");
        if (card.IsRevoked) return ServiceResult<IdCardView>.Fail("card_revoked");

        // Written out column by column deliberately. A mapper would make adding a property to the DTO
        // enough to make an asserted identifier writable, which is the one thing this must never allow.
        // A storage key the holder names is presigned straight back to them by ProjectAsync and read
        // by GenerateAsync, and PresignGetAsync signs WHATEVER string it is handed while
        // /v1/storage/{*key} is anonymous — the signature is the authorization. Unvalidated, this
        // field was an arbitrary-object read: naming another user's chat attachment or verification
        // document returned a working, re-mintable URL for it. Bound to the caller's own prefix, the
        // shape MediaService issues (`users/{userId}/…`) and the same guard
        // ChatService.ConfirmAttachmentAsync makes for the identical reason.
        if (input.PhotoKey is not null && !OwnedByCaller(input.PhotoKey, actorId))
            return ServiceResult<IdCardView>.Fail("invalid_storage_key");
        if (input.SignatureKey is not null && !OwnedByCaller(input.SignatureKey, actorId))
            return ServiceResult<IdCardView>.Fail("invalid_storage_key");

        if (input.Template is not null) card.Template = ParseTemplate(input.Template);
        if (input.PhotoKey is not null) card.PhotoKey = input.PhotoKey;
        if (input.SignatureKey is not null) card.SignatureKey = input.SignatureKey;
        if (input.LayoutJson is not null) card.LayoutJson = input.LayoutJson;
        if (input.BloodGroup is not null) card.BloodGroup = Trim(input.BloodGroup);
        if (input.Address is not null) card.Address = Trim(input.Address);
        if (input.EmergencyContactName is not null) card.EmergencyContactName = Trim(input.EmergencyContactName);
        if (input.EmergencyContactPhone is not null) card.EmergencyContactPhone = Trim(input.EmergencyContactPhone);
        card.UpdatedAt = DateTime.UtcNow;

        // The values are NOT logged: blood group is health data and an emergency contact is a third
        // party's. The audit trail records that the holder changed their own supplied fields and which
        // ones, which is what an investigation needs without turning the log into a second copy.
        Audit(actorId, isAdmin: false, "id_card.holder_edited", card.Id, new
        {
            fields = FieldsPresent(input),
        });
        await db.SaveChangesAsync(ct);

        return await GetAsync(actorId, cardId, isAdmin: false, ct);
    }

    public async Task<ServiceResult<IdCardView>> GenerateAsync(
        Guid actorId, Guid cardId, bool isAdmin, CancellationToken ct = default)
    {
        var card = await db.IdCards.FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card is null) return ServiceResult<IdCardView>.Fail("not_found");
        if (card.IsRevoked) return ServiceResult<IdCardView>.Fail("card_revoked");
        if (!await CanManageAsync(actorId, card, isAdmin, ct)) return ServiceResult<IdCardView>.Fail("forbidden");

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == card.OrgId, ct);
        var holder = await db.Users.AsNoTracking().FirstAsync(u => u.Id == card.UserId, ct);

        var request = new IdCardRenderRequest(
            TemplateKey: card.Template.ToString(),
            HolderName: holder.Name,
            OrgName: org.Name,
            StudentId: card.StudentId,
            Department: card.Department,
            Course: card.Course,
            Year: card.Year,
            BloodGroup: card.BloodGroup,
            Phone: holder.Phone,
            Email: holder.Email,
            DateOfBirth: holder.DateOfBirth?.ToString("dd MMM yyyy"),
            Address: card.Address,
            EmergencyContact: EmergencyLine(card),
            ValidityLine: ValidityLine(card),
            MealLine: await MealLineAsync(card, ct),
            CardNumber: card.CardNumber,
            PhotoBytes: await TryFetchAsync(card.PhotoKey, ct),
            LogoBytes: await TryFetchAsync(card.LogoKey, ct),
            SignatureBytes: await TryFetchAsync(card.SignatureKey, ct),
            QrPng: qr.GeneratePng(VerifyUrlBase + card.VerifyCode));

        var rendered = await renderer.RenderAsync(request, ct);

        var pdfKey = $"id-cards/{card.Id}.pdf";
        var pngKey = $"id-cards/{card.Id}.png";
        await storage.PutAsync(pdfKey, rendered.PdfBytes, "application/pdf", ct);
        await storage.PutAsync(pngKey, rendered.PngBytes, "image/png", ct);

        card.PdfKey = pdfKey;
        card.PngKey = pngKey;
        card.GeneratedAt = DateTime.UtcNow;
        // Generating is what makes a card real; a Draft that has been rendered is Active unless an
        // admin has since ended it. Revoked is handled by the guard above, so it cannot be resurrected.
        if (card.Status is IdCardStatus.Draft or IdCardStatus.PendingApproval) card.Status = IdCardStatus.Active;
        card.UpdatedAt = DateTime.UtcNow;

        Audit(actorId, isAdmin, "id_card.generated", card.Id,
            new { card_number = card.CardNumber, regenerated = card.GeneratedAt is not null });
        await db.SaveChangesAsync(ct);

        return ServiceResult<IdCardView>.Success(await ProjectAsync(card, org.Name, holder.Name, ct));
    }

    public async Task<ServiceResult<IdCardView>> GetAsync(
        Guid actorId, Guid cardId, bool isAdmin, CancellationToken ct = default)
    {
        var card = await db.IdCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card is null) return ServiceResult<IdCardView>.Fail("not_found");
        // A card the caller may neither hold nor manage is not merely forbidden, it is invisible —
        // D-018's hidden-resource rule. The public verifier is the only anonymous read, and it is a
        // different projection entirely.
        if (card.UserId != actorId && !await CanManageAsync(actorId, card, isAdmin, ct))
            return ServiceResult<IdCardView>.Fail("not_found");

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == card.OrgId, ct);
        var holder = await db.Users.AsNoTracking().FirstAsync(u => u.Id == card.UserId, ct);
        return ServiceResult<IdCardView>.Success(await ProjectAsync(card, org.Name, holder.Name, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<IdCardView>>> ListForUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        var rows = await db.IdCards.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Join(db.Organizations.AsNoTracking(), c => c.OrgId, o => o.Id, (c, o) => new { Card = c, Org = o })
            .Join(db.Users.AsNoTracking(), x => x.Card.UserId, u => u.Id, (x, u) => new { x.Card, x.Org, User = u })
            .ToListAsync(ct);

        var views = new List<IdCardView>(rows.Count);
        foreach (var r in rows) views.Add(await ProjectAsync(r.Card, r.Org.Name, r.User.Name, ct));
        return ServiceResult<IReadOnlyList<IdCardView>>.Success(views);
    }

    public async Task<ServiceResult<IReadOnlyList<IdCardView>>> ListForEventAsync(
        Guid actorId, Guid eventId, string? status, bool isAdmin, CancellationToken ct = default)
    {
        // Same authority as issuing: the roster names every participant who holds a card, so it is the
        // organizer's to read, not any org member's (D-335).
        if (!(await authority.ResolveAsync(actorId, eventId, isAdmin, ct)).Can(EventPermission.ManageLifecycle))
            return ServiceResult<IReadOnlyList<IdCardView>>.Fail("not_event_organizer");

        var q = db.IdCards.AsNoTracking().Where(c => c.EventId == eventId);
        if (status is not null && Enum.TryParse<IdCardStatus>(status, ignoreCase: true, out var s))
            q = q.Where(c => c.Status == s);

        var rows = await q.OrderByDescending(c => c.CreatedAt)
            .Join(db.Organizations.AsNoTracking(), c => c.OrgId, o => o.Id, (c, o) => new { Card = c, Org = o })
            .Join(db.Users.AsNoTracking(), x => x.Card.UserId, u => u.Id, (x, u) => new { x.Card, x.Org, User = u })
            .ToListAsync(ct);

        var views = new List<IdCardView>(rows.Count);
        foreach (var r in rows) views.Add(await ProjectAsync(r.Card, r.Org.Name, r.User.Name, ct));
        return ServiceResult<IReadOnlyList<IdCardView>>.Success(views);
    }

    public async Task<ServiceResult<bool>> RevokeAsync(
        Guid actorId, Guid cardId, string reason, bool isAdmin, CancellationToken ct = default)
    {
        var card = await db.IdCards.FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card is null) return ServiceResult<bool>.Fail("not_found");
        if (!await CanManageAsync(actorId, card, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        if (string.IsNullOrWhiteSpace(reason)) return ServiceResult<bool>.Fail("reason_required");
        if (card.IsRevoked) return ServiceResult<bool>.Success(true);   // idempotent

        card.IsRevoked = true;
        card.RevokedReason = reason.Trim();
        card.RevokedAt = DateTime.UtcNow;
        card.RevokedBy = actorId;
        card.Status = IdCardStatus.Revoked;
        card.UpdatedAt = DateTime.UtcNow;

        Audit(actorId, isAdmin, "id_card.revoked", card.Id, new { reason = card.RevokedReason });
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IdCardVerification>> VerifyAsync(
        string verifyCode, CancellationToken ct = default)
    {
        var row = await db.IdCards.AsNoTracking()
            .Where(c => c.VerifyCode == verifyCode && c.IsPublic)
            .Join(db.Organizations.AsNoTracking(), c => c.OrgId, o => o.Id, (c, o) => new { Card = c, Org = o })
            .Join(db.Users.AsNoTracking(), x => x.Card.UserId, u => u.Id, (x, u) => new { x.Card, x.Org, User = u })
            .FirstOrDefaultAsync(ct);

        if (row is null) return ServiceResult<IdCardVerification>.Fail("not_found");

        // A revoked card resolves rather than 404s: confirming a revocation is the endpoint's purpose
        // (D-036). No student ID, no department, no contact data (D-331).
        //
        // RevokedReason is deliberately NOT here. It is unbounded free text written by staff through
        // RevokeAsync, who have no signal that it becomes world-readable — and this route is
        // anonymous, so "expelled for misconduct, pending police complaint" would be curl-able by
        // anyone who photographed the lanyard, forever. `IsRevoked` and `Status` answer the question
        // this endpoint exists for; the reason stays on the authenticated projection.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return ServiceResult<IdCardVerification>.Success(new IdCardVerification(
            VerifyCode: row.Card.VerifyCode,
            HolderName: row.User.Name,
            OrgName: row.Org.Name,
            Status: row.Card.EffectiveStatus(today).ToString().ToLowerInvariant(),
            ValidFrom: row.Card.ValidFrom,
            ValidUntil: row.Card.ValidUntil,
            IsRevoked: row.Card.IsRevoked,
            IssuedAt: row.Card.CreatedAt));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<IdCardView>> SetMealDisplayAsync(
        Guid actorId, Guid cardId, bool show, bool isAdmin, CancellationToken ct = default)
    {
        var card = await db.IdCards.FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card is null) return ServiceResult<IdCardView>.Fail("not_found");
        if (card.IsRevoked) return ServiceResult<IdCardView>.Fail("card_revoked");
        if (!await CanManageAsync(actorId, card, isAdmin, ct)) return ServiceResult<IdCardView>.Fail("forbidden");

        // A college ID has no event to draw meals from, so the flag would be a switch wired to nothing.
        if (show && card.EventId is null) return ServiceResult<IdCardView>.Fail("not_an_event_card");

        card.ShowMealInfo = show;
        card.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Deliberately does NOT regenerate. The printed artefact changes when someone asks for it to,
        // and that same call re-reads the quantities — coupling them here would reprint a card as a side
        // effect of a checkbox.
        return await GetAsync(actorId, cardId, isAdmin, ct);
    }

    /// <summary>The meal panel's text, read from the holder's LIVE entitlement grants (D-334 §8, §38).
    ///
    /// <para><b>Never stored, never supplied by a client.</b> A quantity printed on a card is a claim the
    /// caterer acts on, so it is computed here from the same rows redemption decrements. A card
    /// regenerated after an allocation change prints the new figure; one regenerated after the holder
    /// spent a lunch still prints the entitlement, because the card states what they are owed, not what
    /// is left — the counter at the counter answers that.</para>
    ///
    /// <para>Cancelled and refunded grants are excluded: those are entitlements that no longer exist.</para></summary>
    private async Task<string?> MealLineAsync(IdCard card, CancellationToken ct)
    {
        if (!card.ShowMealInfo || card.EventId is null) return null;

        var slots = await (
            from g in db.EntitlementGrants.AsNoTracking()
            join p in db.EntitlementProducts.AsNoTracking() on g.EntitlementProductId equals p.Id
            where p.EventId == card.EventId
                  && g.UserId == card.UserId
                  && p.Kind == EntitlementKind.Meal
                  && p.MealSlot != MealSlot.None
                  && g.Status != EntitlementGrantStatus.Cancelled
                  && g.Status != EntitlementGrantStatus.Refunded
            group g by p.MealSlot into bySlot
            select new { Slot = bySlot.Key, Quantity = bySlot.Sum(x => x.Quantity) }
        ).ToListAsync(ct);

        if (slots.Count == 0) return null;

        // Compact form: a card is 54mm wide and four sittings on their own lines would not fit beside a
        // photo. Ordered by sitting rather than alphabetically, so it reads as a day.
        var order = new[] { MealSlot.Breakfast, MealSlot.Lunch, MealSlot.Snack, MealSlot.Refreshment, MealSlot.Dinner };
        var parts = order
            .Select(slot => slots.FirstOrDefault(x => x.Slot == slot))
            .Where(x => x is not null && x.Quantity > 0)
            .Select(x => $"{x!.Slot.ToString()[0]}{x.Quantity}");

        return string.Join(" · ", parts);
    }

    /// <summary>Management authority over a card is authority over the event it belongs to (D-335).
    /// Previously this asked only whether the caller held ANY verified membership of the card's org —
    /// which, since MembershipVerificationService mints a verified Staff seat for every approved member,
    /// meant any student could revoke or regenerate a colleague's card.</summary>
    private async Task<bool> CanManageAsync(Guid actorId, IdCard card, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin) return true;
        if (card.EventId is not { } eventId) return false;
        return (await authority.ResolveAsync(actorId, eventId, isAdmin, ct)).Can(EventPermission.ManageLifecycle);
    }

    private async Task<IdCardView> ProjectAsync(IdCard c, string orgName, string holderName, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new IdCardView(
            c.Id, c.OrgId, orgName, c.UserId, holderName,
            c.CardNumber, c.VerifyCode,
            c.EffectiveStatus(today).ToString().ToLowerInvariant(),
            c.Template.ToString(),
            c.StudentId, c.Department, c.Course, c.Year,
            c.ValidFrom, c.ValidUntil,
            await PresignOrNullAsync(c.PhotoKey, ct),
            await PresignOrNullAsync(c.PdfKey, ct),
            await PresignOrNullAsync(c.PngKey, ct),
            c.GeneratedAt, c.IsRevoked, c.RevokedReason, c.CreatedAt,
            c.ShowMealInfo, await MealLineAsync(c, ct));
    }

    private async Task<string?> PresignOrNullAsync(string? key, CancellationToken ct)
        => key is null ? null : await storage.PresignGetAsync(key, ct: ct);

    /// <summary>Missing artefacts must not fail a render — a card whose avatar was pruned should print
    /// without a photo rather than 500. Storage errors are the same case from the caller's side.</summary>
    private async Task<byte[]?> TryFetchAsync(string? key, CancellationToken ct)
    {
        if (key is null) return null;
        try { return await storage.ExistsAsync(key, ct) ? await storage.GetAsync(key, ct) : null; }
        catch { return null; }
    }

    /// <summary>Whether a client-supplied storage key is one this caller could have uploaded.
    /// Ordinal, and the trailing slash matters: without it `users/{id}extra/...` would pass on a
    /// prefix that merely starts the same way.</summary>
    private static bool OwnedByCaller(string key, Guid actorId) =>
        key.StartsWith($"users/{actorId}/", StringComparison.Ordinal);

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string ValidityLine(IdCard c) =>
        (c.ValidFrom, c.ValidUntil) switch
        {
            ({ } f, { } u) => $"{f:dd MMM yyyy} – {u:dd MMM yyyy}",
            (null, { } u) => $"Valid until {u:dd MMM yyyy}",
            ({ } f, null) => $"Valid from {f:dd MMM yyyy}",
            _ => "No expiry",
        };

    private static string? EmergencyLine(IdCard c) =>
        c.EmergencyContactName is null && c.EmergencyContactPhone is null
            ? null
            : string.Join(" · ", new[] { c.EmergencyContactName, c.EmergencyContactPhone }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string[] FieldsPresent(IdCardHolderInput i)
    {
        var f = new List<string>();
        if (i.Template is not null) f.Add("template");
        if (i.PhotoKey is not null) f.Add("photo");
        if (i.SignatureKey is not null) f.Add("signature");
        if (i.LayoutJson is not null) f.Add("layout");
        if (i.BloodGroup is not null) f.Add("blood_group");
        if (i.Address is not null) f.Add("address");
        if (i.EmergencyContactName is not null) f.Add("emergency_contact_name");
        if (i.EmergencyContactPhone is not null) f.Add("emergency_contact_phone");
        return [.. f];
    }

    private static IdCardTemplate ParseTemplate(string? key) =>
        Enum.TryParse<IdCardTemplate>(key, ignoreCase: true, out var t) ? t : IdCardTemplate.StandardCollege;

    private void Audit(Guid actorId, bool isAdmin, string action, Guid cardId, object details) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = isAdmin ? "admin" : "user",
            ActorId = actorId,
            Action = action,
            Entity = "id_cards",
            EntityId = cardId,
            DetailsJson = JsonSerializer.Serialize(details),
        });

    private async Task<string> NextVerifyCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var code = NewVerifyCode();
            if (!await db.IdCards.AnyAsync(c => c.VerifyCode == code, ct)) return code;
        }
        // 100 consecutive collisions against this code space is not bad luck; it means the generator is
        // broken, and a duplicate verify code would let one card verify as another.
        throw new InvalidOperationException("Could not allocate a unique ID card verify code.");
    }

    /// <summary>10 chars of Crockford-ish base32, drawn from a CSPRNG. Same shape and alphabet as the
    /// certificate code so a verifier scanning either document reads the same kind of token.</summary>
    private static string NewVerifyCode()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTVWXYZ0123456789";
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);
        return string.Create(10, bytes.ToArray(), (span, b) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = alphabet[b[i] % alphabet.Length];
        });
    }

    /// <summary>Sequential per org and zero-padded, because a card number is read aloud and typed by
    /// humans. Uniqueness is enforced by the composite index; this only has to produce a sensible next
    /// value, and a race loses to the index rather than to a duplicate.</summary>
    private async Task<string> NextCardNumberAsync(Guid orgId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"{year}/";
        var issuedThisYear = await db.IdCards.CountAsync(c => c.OrgId == orgId && c.CardNumber.StartsWith(prefix), ct);
        return $"{prefix}{issuedThisYear + 1:D4}";
    }
}
