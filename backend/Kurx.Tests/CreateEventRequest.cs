using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kurx.Tests;

/// <summary>Creating an event over HTTP the way a client does after D-267: <c>POST /v1/events</c>, with the
/// organization the caller represents carried as a body field rather than a path segment. There is no
/// org-scoped create route any more — an organization is never required to reach event creation.
///
/// <para>Tests keep passing the org id positionally because almost every fixture already has one (they seed a
/// verified org to exercise the publish gate); this helper folds it into the body so the 80-odd existing call
/// sites did not each have to grow a <c>representingOrgId</c> property. Passing <c>null</c> exercises the
/// Personal path, where the backend resolves or creates the caller's own "just me" org.</para></summary>
public static class CreateEventRequest
{
    /*
     * D-378 — the same argument, for the same reason, a second time.
     *
     * `submit_review` now refuses an event that has not answered every wizard step. Twenty-six existing
     * tests walk an event to review to reach the state they actually care about (a paid order, an
     * inventory hold, a visibility rule) and filled only what the OLD contract required, so every one of
     * them started failing on `missing_tagline` — a field none of them is about.
     *
     * Filling the submission fields here rather than in each test keeps those tests about their own
     * subject, and is exactly the role this helper already had for `representingOrgId`. `submittable:
     * false` opts out, for the tests whose subject IS the incompleteness (`EventSubmissionReadinessTests`).
     *
     * Nothing here overwrites a value the caller supplied: each key is added only when absent, so a test
     * that sets its own tagline, window or age bound still gets exactly what it wrote.
     */
    /*
     * Two INDEPENDENT flags, because they answer two different questions and conflating them broke both
     * kinds of test:
     *
     *   · `submittable`        fills the wizard field groups (D-378: content, location, windows,
     *                          eligibility, legal). Off for tests whose subject is an incomplete event.
     *   · `withAuthorization`  files this event's own authorization letter (D-379). Off for tests whose
     *                          subject is a MISSING letter.
     *
     * One flag meant `EventSubmissionReadinessTests` (fields missing, letter not its subject) was refused
     * on the letter before it ever reached a field check, and `EventRepresentationTests` (letter missing,
     * fields not its subject) had a letter filed underneath it.
     */
    public static Task<HttpResponseMessage> CreateEventAsync(
        this HttpClient client, Guid? representingOrgId, object body,
        bool submittable = true, bool withAuthorization = true)
    {
        var node = JsonSerializer.SerializeToNode(body)!.AsObject();
        if (representingOrgId is { } orgId) node["representingOrgId"] = orgId;
        if (submittable) AddSubmissionFields(node);
        return withAuthorization
            ? CreateAndAuthorizeAsync(client, node)
            : client.PostAsJsonAsync("/v1/events", node);
    }

    /*
     * D-379 — every event carries its own `EventAuthorization`, so a "submittable" event needs one filed
     * before `submit_review` will take it. Filed here for the same reason the submission fields are:
     * dozens of tests walk an event to review to reach the state they actually care about — a paid
     * order, an inventory hold, a refund ledger — and not one of them is about institutional consent.
     *
     * It goes through the real endpoint rather than a direct insert, so these tests exercise the same
     * path a client does. A failure to file is deliberately NOT thrown: the create is what this helper
     * promises, and a test whose subject IS the authorization opts out with `submittable: false`.
     */
    private static async Task<HttpResponseMessage> CreateAndAuthorizeAsync(HttpClient client, JsonObject node)
    {
        var created = await client.PostAsJsonAsync("/v1/events", node);
        if (!created.IsSuccessStatusCode) return created;

        /*
         * Read the body as a STRING and put it back.
         *
         * Reading it here consumed the response stream, so every caller's own
         * `ReadFromJsonAsync` then threw `ObjectDisposedException: Cannot access a closed Stream` —
         * 512 tests in one run, none of them about representation. A helper that reads a response it
         * also returns has to leave it readable.
         */
        var raw = await created.Content.ReadAsStringAsync();
        created.Content = new StringContent(raw, Encoding.UTF8, "application/json");

        using var parsed = JsonDocument.Parse(raw);
        if (!parsed.RootElement.TryGetProperty("id", out var idNode)
            || !idNode.TryGetGuid(out var eventId)) return created;

        await client.PostAsJsonAsync($"/v1/events/{eventId}/authorization", new
        {
            headName = "R Iyer",
            headDesignation = "Principal",
            officialEmail = "head@institute.ac.in",
            officialPhone = "+919876543210",
            representativeRole = "Principal",
            letterheadDocumentKey = "private/auth/letter.pdf",
        });

        /*
         * Filed, and deliberately NOT approved.
         *
         * There is ONE admin review and ONE approval decision (D-379): the letter is evidence weighed
         * inside that review, not a gate cleared before it. An earlier version approved it here, because
         * `PolicyResolver` was reading `Status == Approved` as a publish blocker — that was the second
         * approval gate the decision explicitly forbids, and it failed 214 tests. The resolver now reads
         * EXISTENCE, so a filed letter is enough and the verdict is recorded by the reviewer's single
         * approve/reject.
         */
        return created;
    }

    /// <summary>Adds only what is missing, group by group.</summary>
    private static void AddSubmissionFields(JsonObject node)
    {
        // Times are relative to the event's own start where the test gave one, so a fixture that pins a
        // date does not end up with windows years away from it. The gate only checks presence and pair
        // ordering, so any consistent set satisfies it.
        var start = node["startsAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow.AddDays(20);

        Group(node, "content", g =>
        {
            Fill(g, "tagline", "A tagline");
            Fill(g, "shortDescription", "A short description.");
            Fill(g, "rules", "Be kind.");
        });

        // Both location groups: the physical fields for an Offline/Hybrid event and the meeting fields
        // for an Online/Hybrid one. A test that sets `eventMode` gets whichever half its mode needs, and
        // the unused half is simply stored and never asked about.
        Group(node, "location", g =>
        {
            Fill(g, "building", "Block A");
            Fill(g, "floor", "2");
            Fill(g, "room", "204");
            Fill(g, "googleMapsUrl", "https://maps.example.com/x");
            Fill(g, "meetingPlatform", "Meet");
            Fill(g, "meetingPassword", "test-pass");
        });

        Group(node, "schedule", g =>
        {
            Fill(g, "registrationOpensAt", start.AddDays(-10));
            Fill(g, "registrationClosesAt", start.AddDays(-1));
            Fill(g, "checkinOpensAt", start.AddHours(-1));
            Fill(g, "checkinClosesAt", start.AddHours(1));
            Fill(g, "resultDate", start.AddDays(1));
            Fill(g, "certificateReleaseAt", start.AddDays(2));
        });

        Group(node, "eligibility", g =>
        {
            Fill(g, "minAge", 0);
            Fill(g, "maxAge", 120);
            Fill(g, "maxTeams", 100);
        });

        Group(node, "legal", g =>
        {
            Fill(g, "termsUrl", "https://example.com/terms");
            Fill(g, "codeOfConduct", "Be kind.");
            Fill(g, "refundPolicy", "No refunds.");
            Fill(g, "cancellationPolicy", "Cancel any time.");
        });
    }

    private static void Group(JsonObject node, string name, Action<JsonObject> fill)
    {
        if (node[name] is not JsonObject existing)
        {
            existing = new JsonObject();
            node[name] = existing;
        }
        fill(existing);
    }

    private static void Fill(JsonObject group, string key, object value)
    {
        if (group.ContainsKey(key)) return;
        group[key] = JsonSerializer.SerializeToNode(value);
    }
}
