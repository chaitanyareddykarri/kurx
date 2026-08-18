using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M5 — institutional authorization for an event, through the real API.
///
/// <para><b>Named "Document" to keep it apart from <see cref="EventAuthorizationTests"/></b>, which covers
/// D-269's <c>IEventAuthority</c>. The two use one English word for different things: that suite asks who
/// may act, this one asks whether the represented institution consented. Sharing a class name would leave
/// a failing run ambiguous about which of the two broke.</para></summary>
public class EventAuthorizationDocumentTests(KurxApiFactory factory) : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory = factory;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (c, t.GetProperty("user_id").GetGuid());
    }

    private async Task<(HttpClient Client, Guid OrgId)> LoginOrgAsync(string phone, string name)
    {
        var (c, _) = await LoginAsync(phone);
        return (c, _factory.SeedVerifiedOrgForClient(c, name));
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == slug)
            .Select(c => new { c.Id, c.ParentId }).FirstAsync();
        return (t.ParentId!.Value, t.Id);
    }

    /// <param name="orgId">The organization the event represents. When null, one is seeded for this
    /// caller — <b>D-353 refuses a self-represented PUBLIC event at creation</b> (409
    /// `representation_required`), and every type used here is Public, so passing null through would
    /// only ever produce a 409. The twelve call sites that pass null are about the authorization
    /// document, not about representation; they get a verified org so they test what they name.</param>
    /// <param name="typeSlug">Defaults by representation, because D-353 ties the two together: a PUBLIC
    /// event must name a real institution, so self-hosting is a Private-only affordance. A self-represented
    /// event therefore has to be filed under a Private type, and asking for a public one would be refused
    /// at creation before any of these tests reached what they are actually about.</param>
    /// <param name="selfRepresented">Opts out of that seeding, for the two cases that ARE about
    /// self-representation. Only legal with a Private type.</param>
    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid? orgId, string? typeSlug = null,
        bool selfRepresented = false)
    {
        // Keyed on self-representation, not on a null orgId: a null orgId here means "seed one for me",
        // so only the genuinely self-represented cases need the Private type D-353 confines them to.
        typeSlug ??= selfRepresented ? "birthday-party" : "hackathon";
        var (catId, typeId) = await TaxonAsync(typeSlug);
        Guid? representing = selfRepresented
            ? null
            : orgId ?? _factory.SeedVerifiedOrgForClient(owner, "Auth Org " + Guid.NewGuid().ToString("N")[..6]);
        // D-379 — this class's subject IS the letter, so the shared helper must not file one.
        var res = await owner.CreateEventAsync(representing, withAuthorization: false, body: new
        {
            title = "Auth " + Guid.NewGuid().ToString("N")[..6], description = "a real description",
            categoryId = catId, typeId, venueName = "Hall", city = "C",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(3),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private static object Body(string? letterheadDocumentKey = "events/seed/authorization/letter") => new
    {
        headName = "Dr. A. Rao",
        headDesignation = "Head of Department",
        officialEmail = "hod@college.edu",
        officialPhone = "+919700000000",
        representativeRole = "Principal",
        letterheadDocumentKey,
        signatureDocumentKey = (string?)null,
        supportingDocumentKeys = new[] { "events/seed/authorization/annexure" },
    };

    private static Task<HttpResponseMessage> Submit(HttpClient c, Guid eventId, object? body = null)
        => c.PostAsJsonAsync($"/v1/events/{eventId}/authorization", body ?? Body());

    private static Task<HttpResponseMessage> Review(HttpClient reviewer, Guid eventId, string decision,
        string? reasonCode = null, string? notes = null)
        => reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/authorization/review",
            new { decision, reasonCode, notes });

    private async Task<List<string>> BlockersAsync(HttpClient c, Guid orgId, Guid eventId)
    {
        var req = await Json(await c.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/policy-requirements"));
        return req.GetProperty("publish_blockers").EnumerateArray().Select(b => b.GetString()!).ToList();
    }

    // ── The milestone's exit criterion ───────────────────────────────────────────────────────

    /// <summary>M5's exit criterion: a Public event that names an institution cannot publish until that
    /// institution's authorization is approved. Asserted at BOTH surfaces — the checklist a reviewer reads
    /// and the transition that actually refuses — because M3's whole contract is that they are one array.</summary>
    [Fact]
    public async Task A_public_event_representing_an_institution_cannot_publish_unauthorized()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000001", "Authz College");
        var id = await CreateEventAsync(owner, orgId);

        Assert.Contains("event_authorization_required", await BlockersAsync(owner, orgId, id));

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("event_authorization_required", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_approved_authorization_clears_the_publish_blocker()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000002", "Cleared College");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        Assert.Equal(HttpStatusCode.OK, (await Submit(owner, id)).StatusCode);
        // Filed is not approved — the blocker survives submission, which is the point of a review.
        // D-379 — FILING clears the blocker, because the blocker asks whether a letter exists. The
        // reviewer's verdict is recorded on the letter (audit) and folded into the ONE event-review
        // decision; it is not a second gate the creator must wait on.
        Assert.DoesNotContain("event_authorization_required", await BlockersAsync(owner, orgId, id));

        Assert.Equal(HttpStatusCode.OK, (await Review(reviewer, id, "approve")).StatusCode);
        Assert.DoesNotContain("event_authorization_required", await BlockersAsync(owner, orgId, id));
    }

    /*
     * D-379 — this test's premise is retired, and the assertion is inverted rather than deleted.
     *
     * It used to prove that a self-represented event needs no authorization, because it names no
     * institution and there is nothing to consent to. There is no longer such an event: every event
     * represents a real organization, so the question it asked cannot arise.
     *
     * What replaces it is the fact that made it obsolete — the shape it was built on is now refused at
     * creation. Kept as a test rather than removed, so the retirement is asserted somewhere instead of
     * merely being described in a decision file.
     */
    [Fact]
    public async Task A_self_represented_event_can_no_longer_be_created_at_all()
    {
        var (owner, _) = await LoginAsync("9702000003");
        var (catId, typeId) = await TaxonAsync("wedding");

        var res = await owner.CreateEventAsync(null, new
        {
            title = "Self Hosted " + Guid.NewGuid().ToString("N")[..6],
            description = "An event with nobody behind it.",
            categoryId = catId, typeId,
            venueName = "Garden", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20),
            endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }, submittable: false, withAuthorization: false);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("representation_required", await res.Content.ReadAsStringAsync());
    }

    /// <summary>D12 §6's Representation column, the other half of M5. A Career Fair is an act of an
    /// institution; one filed as self-represented is misfiled.
    ///
    /// <para>D-353 moved where that is caught. It used to be a publish-time blocker on an event that had
    /// already been created; it is now refused at creation, because a public event must name a real
    /// institution before any row is written. The blocker still exists as the twin that catches an event
    /// which reached Draft before the rule, or whose representation was withdrawn afterwards — this
    /// asserts the front-line refusal, which is the one an organiser actually meets.</para></summary>
    [Fact]
    public async Task An_archetype_that_requires_representation_refuses_a_self_represented_event()
    {
        var (owner, _) = await LoginAsync("9702000013");
        // "Campus Recruitment" rather than "Career Fair": the latter exists under two audiences, so the
        // seeder prefixes its slug and a bare "career-fair" resolves to nothing.
        var (catId, typeId) = await TaxonAsync("campus-recruitment");

        using (var scope = _factory.Services.CreateScope())
        {
            // Guard the premise: if the type stopped resolving to `recruitment`, or stopped being a public
            // product, this test would pass for the wrong reason.
            var node = await scope.ServiceProvider.GetRequiredService<KurxDbContext>().EventCategories
                .AsNoTracking().Where(c => c.Id == typeId)
                .Select(c => new { c.ArchetypeSlug, c.ProductClass }).FirstAsync();
            Assert.Equal("recruitment", node.ArchetypeSlug);
            Assert.Equal(EventProduct.Public, node.ProductClass);
        }

        /*
         * D-353 moved this refusal from PUBLISH to CREATE, and widened it.
         *
         * It used to be an archetype-level publish blocker: a Career Fair filed self-represented was
         * misfiled, so `PolicyResolver` raised `representation_required` when it tried to publish. Every
         * OTHER public archetype was publishable with no institution behind it — the hole D-353's entry
         * names. Now any Public product is refused at creation, so the event never exists to be blocked.
         *
         * Asserting the create refusal rather than the publish blocker: that is where the rule now lives,
         * and a test that still walked through creation would be asserting against a row the API will
         * not make. 409 rather than 400 — the request is well-formed, the state forbids it.
         */
        var res = await owner.CreateEventAsync(null, new
        {
            title = "Auth " + Guid.NewGuid().ToString("N")[..6], description = "a real description",
            categoryId = catId, typeId, venueName = "Hall", city = "C",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(3),
        });


        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("representation_required", (await Json(res)).GetProperty("error").GetString());
    }

    // ── Filing ───────────────────────────────────────────────────────────────────────────────

    /// <summary>A name typed into a form proves nothing; the letter on institutional letterhead is the
    /// substance of the claim.</summary>
    [Fact]
    public async Task Filing_without_a_letterhead_is_refused()
    {
        var (owner, _) = await LoginOrgAsync("9702000004", "NoLetter College");
        var id = await CreateEventAsync(owner, null);

        var res = await Submit(owner, id, Body(letterheadDocumentKey: null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("letterhead_required", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Filing_requires_standing_and_hides_the_event_from_strangers()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000005", "Hidden College");
        var id = await CreateEventAsync(owner, orgId);
        var (stranger, _) = await LoginAsync("9702000006");

        // 404, never 403: a 403 would confirm to a stranger that this draft exists (D-018).
        Assert.Equal(HttpStatusCode.NotFound, (await Submit(stranger, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/v1/events/{id}/authorization")).StatusCode);
    }

    /// <summary>Evidence replaced after a verdict has not been reviewed. Leaving the old Approved stamp
    /// attached would let an organiser swap the letter and publish on a decision about a different one.</summary>
    [Fact]
    public async Task Resubmitting_clears_a_previous_approval()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000007", "Resubmit College");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Submit(owner, id);
        await Review(reviewer, id, "approve");
        Assert.DoesNotContain("event_authorization_required", await BlockersAsync(owner, orgId, id));

        Assert.Equal(HttpStatusCode.OK, (await Submit(owner, id)).StatusCode);

        var view = await Json(await owner.GetAsync($"/v1/events/{id}/authorization"));
        // The letter's own verdict is cleared — that is the audit record, and it is what a reviewer sees.
        Assert.Equal("Submitted", view.GetProperty("status").GetString());
        /*
         * D-379 — what actually protects the platform here is NOT a publish blocker keyed on this status.
         * It is D-363 §4: resubmitting the letter on an APPROVED event sends the EVENT back to the queue
         * (`EventAuthorizationService` calls `EventReviewReopen.IfApprovedAsync(..., "the authorization
         * letter", ...)`), so an organiser cannot swap the evidence and publish on the old decision.
         * The blocker asks only whether a letter exists, and one does.
         */
        Assert.DoesNotContain("event_authorization_required", await BlockersAsync(owner, orgId, id));
    }

    /// <summary>The same edit lock the event's own PATCH honours (D-266 M4). Evidence must not move under
    /// a reviewer who is reading it.</summary>
    [Fact]
    public async Task Authorization_is_locked_while_the_event_is_with_a_reviewer()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000008", "Locked College");
        var id = await CreateEventAsync(owner, orgId);
        await Submit(owner, id);

        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "submit_for_review" });

        var res = await Submit(owner, id);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("event_under_review", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task No_authorization_on_file_answers_204_not_404()
    {
        var (owner, _) = await LoginOrgAsync("9702000014", "Empty College");
        var id = await CreateEventAsync(owner, null);

        // The event exists and the caller may see it — there is simply nothing filed. A 404 would be
        // indistinguishable from "no such event".
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"/v1/events/{id}/authorization")).StatusCode);
    }

    // ── Review ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rejecting_without_a_reason_code_is_refused()
    {
        var (owner, _) = await LoginOrgAsync("9702000009", "Reason College");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, null);
        await Submit(owner, id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Review(reviewer, id, "reject")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Review(reviewer, id, "reject", reasonCode: "MisrepresentedAffiliation")).StatusCode);

        var view = await Json(await owner.GetAsync($"/v1/events/{id}/authorization"));
        Assert.Equal("Rejected", view.GetProperty("status").GetString());
        Assert.Equal("MisrepresentedAffiliation", view.GetProperty("reason_code").GetString());
    }

    /// <summary>ChangesRequested exists so the organiser can act on it; without notes it says only "no".</summary>
    [Fact]
    public async Task Requesting_changes_without_notes_is_refused()
    {
        var (owner, _) = await LoginOrgAsync("9702000015", "Notes College");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, null);
        await Submit(owner, id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Review(reviewer, id, "request_changes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Review(reviewer, id, "request_changes", notes: "Letterhead is illegible.")).StatusCode);

        var view = await Json(await owner.GetAsync($"/v1/events/{id}/authorization"));
        Assert.Equal("ChangesRequested", view.GetProperty("status").GetString());
    }

    [Fact]
    public async Task An_unknown_decision_is_refused()
    {
        var (owner, _) = await LoginOrgAsync("9702000016", "Bad Decision College");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, null);
        await Submit(owner, id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Review(reviewer, id, "maybe")).StatusCode);
    }

    /// <summary>Reviewing is a platform reviewer's act. An organiser holding full manage authority over
    /// their own event still cannot approve their own evidence — the same separation D-266 M4 enforces for
    /// the event itself.</summary>
    [Fact]
    public async Task An_organiser_cannot_review_their_own_authorization()
    {
        var (owner, _) = await LoginOrgAsync("9702000010", "SelfReview College");
        var id = await CreateEventAsync(owner, null);
        await Submit(owner, id);

        Assert.Equal(HttpStatusCode.Forbidden, (await Review(owner, id, "approve")).StatusCode);
    }

    /// <summary>A verdict the organiser is never told about is one they can only find by reopening the
    /// page. On a rejection that is worse than silent: the reason is the only thing that makes it fixable.</summary>
    [Fact]
    public async Task A_review_decision_notifies_whoever_filed_it()
    {
        var (owner, _) = await LoginOrgAsync("9702000018", "Notify College");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, null);
        await Submit(owner, id);

        await Review(reviewer, id, "reject", reasonCode: "MisrepresentedAffiliation", notes: "Wrong signatory.");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var n = await db.Notifications.AsNoTracking()
            .Where(x => x.Kind == "event_authorization_rejected")
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();

        Assert.NotNull(n);
        // The reason rides in the body: it is the actionable part, and leaving it in a panel the organiser
        // has to go looking for is how a rejection becomes a dead end.
        Assert.Contains("MisrepresentedAffiliation", n!.Body);
        Assert.Contains("Wrong signatory.", n.Body);
    }

    // ── Representative details (D-266 M5) ────────────────────────────────────────────────────

    /// <summary>A signatory who cannot be reached is not a verifiable one: contacting the person who
    /// supposedly signed the letter is the reviewer's only check independent of the letter itself.</summary>
    [Fact]
    public async Task Filing_without_a_phone_or_a_role_is_refused()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000020", "RepFields College");
        var id = await CreateEventAsync(owner, orgId);

        var noPhone = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "HOD", officialEmail = "hod@college.edu",
            officialPhone = (string?)null, representativeRole = "HOD",
            letterheadDocumentKey = "events/seed/authorization/letter",
        });
        Assert.Equal(HttpStatusCode.BadRequest, noPhone.StatusCode);

        var noRole = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "HOD", officialEmail = "hod@college.edu",
            officialPhone = "+919700000000", representativeRole = "",
            letterheadDocumentKey = "events/seed/authorization/letter",
        });
        Assert.Equal(HttpStatusCode.BadRequest, noRole.StatusCode);
    }

    /// <summary>E.164 or nothing — a stored number nobody can dial is a signatory nobody can verify.</summary>
    [Fact]
    public async Task A_phone_that_is_not_e164_is_refused()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000021", "E164 College");
        var id = await CreateEventAsync(owner, orgId);

        var res = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "HOD", officialEmail = "hod@college.edu",
            officialPhone = "9700000000", representativeRole = "HOD",
            letterheadDocumentKey = "events/seed/authorization/letter",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>A closed list that cannot express a real title pushes people into picking a wrong one, so
    /// <c>Other</c> exists — but it has to carry the words, or it says less than the list it escaped.</summary>
    [Fact]
    public async Task Role_Other_requires_the_typed_role()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000022", "OtherRole College");
        var id = await CreateEventAsync(owner, orgId);

        var missing = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "Chief Mentor", officialEmail = "hod@college.edu",
            officialPhone = "+919700000000", representativeRole = "Other",
            letterheadDocumentKey = "events/seed/authorization/letter",
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("representative_role_other_required", await missing.Content.ReadAsStringAsync());

        var ok = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "Chief Mentor", officialEmail = "hod@college.edu",
            officialPhone = "+919700000000", representativeRole = "Other",
            representativeRoleOther = "Chief Mentor",
            letterheadDocumentKey = "events/seed/authorization/letter",
        });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var view = await Json(await owner.GetAsync($"/v1/events/{id}/authorization"));
        Assert.Equal("Other", view.GetProperty("representative_role").GetString());
        Assert.Equal("Chief Mentor", view.GetProperty("representative_role_other").GetString());
    }

    /// <summary>Omitting a document on a re-file means "keep the one on file", never "delete it" — a client
    /// cannot resend a key it was never given, so correcting a typo must not destroy the evidence.</summary>
    [Fact]
    public async Task Refiling_without_documents_keeps_the_ones_on_file()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000028", "KeepDocs College");
        var id = await CreateEventAsync(owner, orgId);

        Assert.Equal(HttpStatusCode.OK, (await Submit(owner, id)).StatusCode);
        var before = await Json(await owner.GetAsync($"/v1/events/{id}/authorization"));
        Assert.NotNull(before.GetProperty("letterhead_url").GetString());
        Assert.Single(before.GetProperty("supporting_document_urls").EnumerateArray());

        // Change only the signatory's phone — no documents in the body at all.
        var res = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "Head of Department",
            officialEmail = "hod@college.edu", officialPhone = "+919700000009",
            representativeRole = "Principal",
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var after = await Json(await owner.GetAsync($"/v1/events/{id}/authorization"));
        Assert.Equal("+919700000009", after.GetProperty("official_phone").GetString());
        Assert.NotNull(after.GetProperty("letterhead_url").GetString());
        Assert.Single(after.GetProperty("supporting_document_urls").EnumerateArray());
    }

    /// <summary>A first filing still has to carry the letter — it is the substance of the claim.</summary>
    [Fact]
    public async Task A_first_filing_still_requires_a_letterhead()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000029", "NoDocs College");
        var id = await CreateEventAsync(owner, orgId);

        var res = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "HOD", officialEmail = "hod@college.edu",
            officialPhone = "+919700000000", representativeRole = "HOD",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("letterhead_required", await res.Content.ReadAsStringAsync());
    }

    /// <summary>The list is closed here or it is not closed at all — an unvalidated vocabulary is free text
    /// that merely looks analysable.</summary>
    [Fact]
    public async Task A_role_outside_the_vocabulary_is_refused()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000027", "Vocab College");
        var id = await CreateEventAsync(owner, orgId);

        var res = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "HOD", officialEmail = "hod@college.edu",
            officialPhone = "+919700000000", representativeRole = "Supreme Overlord",
            letterheadDocumentKey = "events/seed/authorization/letter",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("representative_role_invalid", await res.Content.ReadAsStringAsync());
    }

    /// <summary>The linked Kurx account is optional, and naming one is a <b>link</b>: it confers no
    /// authority over the event, which remains <c>IEventAuthority</c>'s alone (D-269).</summary>
    [Fact]
    public async Task A_linked_kurx_account_is_optional_and_grants_no_authority()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000023", "LinkRep College");
        var (rep, repId) = await LoginAsync("9702000024");
        var id = await CreateEventAsync(owner, orgId);

        // Filing without a link is accepted.
        Assert.Equal(HttpStatusCode.OK, (await Submit(owner, id)).StatusCode);

        var withLink = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "HOD", officialEmail = "hod@college.edu",
            officialPhone = "+919700000000", representativeRole = "HOD", representativeUserId = repId,
            letterheadDocumentKey = "events/seed/authorization/letter",
        });
        Assert.Equal(HttpStatusCode.OK, withLink.StatusCode);

        var view = await Json(await owner.GetAsync($"/v1/events/{id}/authorization"));
        Assert.Equal(repId, view.GetProperty("representative_user_id").GetGuid());

        // The named person gained nothing: the event stays hidden from them.
        Assert.Equal(HttpStatusCode.NotFound, (await rep.GetAsync($"/v1/events/{id}/authorization")).StatusCode);
    }

    /// <summary>An unknown account cannot be linked — an unresolvable id tells a reviewer nothing while
    /// looking like corroboration.</summary>
    [Fact]
    public async Task Linking_an_account_that_does_not_exist_is_refused()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000026", "GhostRep College");
        var id = await CreateEventAsync(owner, orgId);

        var res = await Submit(owner, id, new
        {
            headName = "Dr. A. Rao", headDesignation = "HOD", officialEmail = "hod@college.edu",
            officialPhone = "+919700000000", representativeRole = "HOD",
            representativeUserId = Guid.NewGuid(),
            letterheadDocumentKey = "events/seed/authorization/letter",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("representative_user_not_found", await res.Content.ReadAsStringAsync());
    }

    /// <summary>Changing the signatory after approval resets it: a verdict describes the details a reviewer
    /// actually read, not whatever replaced them afterwards.</summary>
    [Fact]
    public async Task Changing_representative_details_resets_the_approval()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000025", "RepReset College");
        var reviewer = await _factory.ReviewerClientAsync();
        var id = await CreateEventAsync(owner, orgId);

        await Submit(owner, id);
        await Review(reviewer, id, "approve");
        Assert.DoesNotContain("event_authorization_required", await BlockersAsync(owner, orgId, id));

        // Same documents, different signatory.
        await Submit(owner, id, new
        {
            headName = "Dr. B. Singh", headDesignation = "Dean", officialEmail = "dean@college.edu",
            officialPhone = "+919700000001", representativeRole = "Dean",
            letterheadDocumentKey = "events/seed/authorization/letter",
        });

        var view = await Json(await owner.GetAsync($"/v1/events/{id}/authorization"));
        // The letter's own verdict is cleared — that is the audit record, and it is what a reviewer sees.
        Assert.Equal("Submitted", view.GetProperty("status").GetString());
        /*
         * D-379 — what actually protects the platform here is NOT a publish blocker keyed on this status.
         * It is D-363 §4: resubmitting the letter on an APPROVED event sends the EVENT back to the queue
         * (`EventAuthorizationService` calls `EventReviewReopen.IfApprovedAsync(..., "the authorization
         * letter", ...)`), so an organiser cannot swap the evidence and publish on the old decision.
         * The blocker asks only whether a letter exists, and one does.
         */
        Assert.DoesNotContain("event_authorization_required", await BlockersAsync(owner, orgId, id));
    }

    /// <summary>The vocabulary is published so clients render the list the server validates against.
    /// A hardcoded copy per client is how a role gets offered on one surface and refused by the API.</summary>
    [Fact]
    public async Task The_role_vocabulary_is_served_and_matches_what_the_server_accepts()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000030", "Vocab Endpoint College");

        var res = await owner.GetAsync("/v1/events/authorization/roles");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var roles = (await Json(res)).EnumerateArray().Select(r => r.GetString()!).ToList();

        Assert.Equal(RepresentativeRoles.All, roles);
        Assert.Contains("Other", roles);

        // Every published role is actually accepted — the list and the validator cannot drift apart.
        var id = await CreateEventAsync(owner, orgId);
        foreach (var role in roles)
        {
            var body = new
            {
                headName = "Dr. A. Rao", headDesignation = role, officialEmail = "hod@college.edu",
                officialPhone = "+919700000000", representativeRole = role,
                representativeRoleOther = role == "Other" ? "Chief Mentor" : null,
                letterheadDocumentKey = "events/seed/authorization/letter",
            };
            Assert.Equal(HttpStatusCode.OK, (await Submit(owner, id, body)).StatusCode);
        }
    }

    /// <summary>Reading the vocabulary still needs a session — it is not an anonymous surface.</summary>
    [Fact]
    public async Task The_role_vocabulary_requires_a_session()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/v1/events/authorization/roles")).StatusCode);
    }

    // ── Confidentiality ──────────────────────────────────────────────────────────────────────

    /// <summary>A storage key is an internal address. Handing one to a client invites it to construct
    /// storage paths, and a client that constructs paths eventually constructs someone else's.</summary>
    [Fact]
    public async Task Documents_are_returned_as_urls_never_as_storage_keys()
    {
        var (owner, _) = await LoginOrgAsync("9702000011", "Keys College");
        var id = await CreateEventAsync(owner, null);
        await Submit(owner, id);

        var raw = await (await owner.GetAsync($"/v1/events/{id}/authorization")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("letterhead_document_key", raw);
        Assert.Contains("letterhead_url", raw);
    }

    // ── Presign ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Presign_scopes_the_key_to_the_event()
    {
        var (owner, _) = await LoginOrgAsync("9702000012", "Presign College");
        var id = await CreateEventAsync(owner, null);

        var res = await owner.PostAsJsonAsync($"/v1/events/{id}/authorization/presign",
            new { contentType = "application/pdf", maxBytes = 5_000_000 });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains($"events/{id}/authorization/", (await Json(res)).GetProperty("key").GetString());
    }

    // ── Concurrency ──────────────────────────────────────────────────────────────────────────

    /// <summary>Two managers filing at once must leave exactly one row. The unique index on EventId is what
    /// enforces it; without this test the constraint would be unproven and a second row would make "is this
    /// event authorised?" a query with two answers.</summary>
    [Fact]
    public async Task Concurrent_submissions_leave_exactly_one_authorization()
    {
        var (owner, orgId) = await LoginOrgAsync("9702000017", "Race College");
        var id = await CreateEventAsync(owner, orgId);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Submit(owner, id)));
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var count = await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
            .EventAuthorizations.AsNoTracking().CountAsync(a => a.EventId == id);
        Assert.Equal(1, count);
    }
}
