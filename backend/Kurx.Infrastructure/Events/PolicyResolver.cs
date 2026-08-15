using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M3 — the one place a policy decision is made.
///
/// <para>The capability engine answers <i>"what can this event do?"</i>. This answers <i>"what is this
/// event allowed to do?"</i>. They are different questions and must never consult each other — and neither
/// answers <i>"who may act?"</i>, which is D-269's <c>IEventAuthority</c>.</para>
///
/// <para><b>Canonical resolution order.</b> Every decision runs this pipeline, and where two rules disagree
/// the earlier one wins:</para>
/// <code>
/// Product → Event Type → Allowed Policies (taxonomy) → Selected Policy
///         → Visibility → Invitation Rules (D9) → Registration Gates → Requirements
/// </code>
///
/// <para><b><see cref="RegistrationGate"/> is an output, never an input.</b> The organiser declares one
/// <see cref="EventRegistrationPolicy"/>; the per-ticket gates are derived from it here. Letting a caller
/// set both is how the two axes drift apart, and then nobody can say which one is true.</para></summary>
public static class PolicyResolver
{
    /// <summary>Step 1 — product rules, above everything below them. A Private product exists to be
    /// invited to: it can never be Open, never take approval queues, and never gate on an institution.</summary>
    private static readonly EventRegistrationPolicy[] PrivateOnly = [EventRegistrationPolicy.InviteOnly];

    /// <summary>Policy → gates. The single mapping; nothing else may construct a gate list.
    ///
    /// <para>The restricted policies all map to <see cref="RegistrationGate.Prerequisite"/> because from the
    /// gate's point of view they are the same mechanism — a check the registrant must pass — and differ only
    /// in <i>which</i> fact is checked. That fact is eligibility (D13 X5, D-265's stored eligibility fields),
    /// a separate axis; encoding it as distinct gates would put eligibility in two places.</para></summary>
    private static readonly Dictionary<EventRegistrationPolicy, RegistrationGate[]> GateMap = new()
    {
        [EventRegistrationPolicy.Open] = [RegistrationGate.Open],
        [EventRegistrationPolicy.InviteOnly] = [RegistrationGate.Invite],
        [EventRegistrationPolicy.ApprovalRequired] = [RegistrationGate.Approval],
        [EventRegistrationPolicy.CollegeRestricted] = [RegistrationGate.Prerequisite],
        [EventRegistrationPolicy.DepartmentRestricted] = [RegistrationGate.Prerequisite],
        [EventRegistrationPolicy.AlumniOnly] = [RegistrationGate.Prerequisite],
        [EventRegistrationPolicy.VerifiedOnly] = [RegistrationGate.Open],
    };

    /// <summary>VerifiedOnly is an identity bar, not a gate: the registrant is not queued or invited, they
    /// simply must already be verified. Expressing it as <see cref="IdentityRequirement"/> reuses the
    /// mechanism that already exists rather than inventing a parallel one.</summary>
    private static IdentityRequirement IdentityFor(EventRegistrationPolicy p) =>
        p == EventRegistrationPolicy.VerifiedOnly ? IdentityRequirement.Verified : IdentityRequirement.Account;

    public sealed record PolicyResolution(
        IReadOnlyList<EventRegistrationPolicy> Allowed,
        EventRegistrationPolicy Selected,
        IReadOnlyList<RegistrationGate> Gates,
        IdentityRequirement Identity,
        IReadOnlyList<string> Requirements,
        IReadOnlyList<string> Violations);

    /// <summary>D-266 M5 — the stored facts the representation rules (D12 §6) need, gathered by the caller
    /// so this resolver stays pure.
    ///
    /// <para>Two rules live here and they are routinely confused. <b>Whether an event must name an
    /// institution at all</b> is archetype-driven — a Career Fair run by nobody in particular is not a
    /// career fair. <b>Whether the institution it names has consented</b> is evidence-driven. An event can
    /// satisfy the first and fail the second.</para></summary>
    /// <param name="RepresentsInstitution">The event represents a real organization. False for a
    /// self-represented event: "Personal" is a persistence row, never a domain organization (D-268), so
    /// there is no third party whose consent could be required.</param>
    /// <param name="HasApprovedAuthorization">An approved <c>EventAuthorization</c> exists for it.</param>
    /// <param name="ArchetypeRequiresRepresentation">D12 §6 marks this archetype's Representation column
    /// Required (A7 Recruitment · A12 Festival · A13 Ceremonial).</param>
    /// <param name="ArchetypeRequiresFinancialReview">D-266 M7 — D12 §6 marks this archetype's Review cell
    /// "Required + financial" (A11 Fundraising alone).</param>
    /// <param name="FinancialReviewPassed">FinanceOps has cleared the money path.</param>
    /// <param name="AuthorizationBypassed">D-352 — outside Production the institutional-consent blockers
    /// are switched off by the same flag as the identity proofs (D-323), because a letterhead is reviewed
    /// by a human against documents a stubbed rasterizer produces: enforcing it in dev costs a manual
    /// approval per test event and establishes nothing about a real institution.
    ///
    /// <para>Carried as its OWN field rather than folded into <paramref name="HasApprovedAuthorization"/>,
    /// which stays a fact about what is on file. A bypass may open a gate; it must never forge the
    /// evidence — the same rule D-323 holds for <c>IdentityVerified</c>. The reviewer checklist and the
    /// organiser's readiness page keep reporting the truth while the blocker is lifted.</para></param>
    public sealed record RepresentationFacts(
        bool RepresentsInstitution,
        bool HasApprovedAuthorization,
        bool ArchetypeRequiresRepresentation,
        bool ArchetypeRequiresFinancialReview = false,
        bool FinancialReviewPassed = false,
        bool AuthorizationBypassed = false);

    /// <summary>Steps 1–3: which policies this event may use at all.
    /// <paramref name="typeAllowList"/> is the taxonomy's stored allow-list for the selected Type; null
    /// means the type has not been given one yet, in which case the product's own limits still apply —
    /// never a fallback to "anything goes".</summary>
    public static IReadOnlyList<EventRegistrationPolicy> AllowedFor(
        EventProduct product, IReadOnlyList<EventRegistrationPolicy>? typeAllowList)
    {
        if (product == EventProduct.Private) return PrivateOnly;

        var all = Enum.GetValues<EventRegistrationPolicy>();
        if (typeAllowList is null || typeAllowList.Count == 0) return all;

        // Intersection, not replacement: a type may narrow what the product permits, never widen it.
        return all.Where(typeAllowList.Contains).ToArray();
    }

    /// <summary>The full pipeline. <paramref name="selected"/> is the organiser's declared intent; a value
    /// outside <see cref="PolicyResolution.Allowed"/> is reported as a violation rather than silently
    /// corrected, because quietly substituting a policy changes who may attend an event.</summary>
    /// <param name="representation">D-266 M5. Null means "not evaluated" — used by the unit suite and by
    /// callers that only need the registration axis. It is NOT a pass: <c>EventPolicyService</c> always
    /// supplies it, so nothing that can actually publish an event skips these rules.</param>
    public static PolicyResolution Resolve(
        EventProduct product,
        IReadOnlyList<EventRegistrationPolicy>? typeAllowList,
        EventRegistrationPolicy selected,
        EventVisibility visibility,
        bool isPaid = false,
        RepresentationFacts? representation = null)
    {
        var allowed = AllowedFor(product, typeAllowList);
        var violations = new List<string>();
        var requirements = new List<string>();

        if (!allowed.Contains(selected))
        {
            violations.Add("registration_policy_not_allowed_for_type");
            // Resolve the rest against the nearest legal policy so the caller still gets a usable shape;
            // the violation is what blocks publish, not a thrown exception.
            selected = allowed[0];
        }

        // Step 5 — visibility. Product decides discoverability; a Private product is never listed, whatever
        // the organiser set. This is the rule that made "Private Wedding, fully discoverable" possible.
        if (product == EventProduct.Private && visibility == EventVisibility.Listed)
            violations.Add("private_product_cannot_be_listed");

        // Step 6 — D9. Invitation is the only way in for a Private product, so the invitation surface is
        // required rather than optional there.
        if (selected == EventRegistrationPolicy.InviteOnly)
            requirements.Add("invitation_list_required");

        // D9: being invited never bypasses payment. Stated as a requirement so both checklists carry it.
        if (selected == EventRegistrationPolicy.InviteOnly && isPaid)
            requirements.Add("invited_registrants_still_pay");

        if (product == EventProduct.Private && isPaid)
            violations.Add("private_product_cannot_take_payment");

        if (selected == EventRegistrationPolicy.ApprovalRequired)
            requirements.Add("approval_reviewer_required");

        if (selected is EventRegistrationPolicy.CollegeRestricted or EventRegistrationPolicy.DepartmentRestricted
                     or EventRegistrationPolicy.AlumniOnly)
            requirements.Add("eligibility_criteria_required");

        // Step 7 — D-266 M5, D12 §6 "Representation". A Private product represents nobody, so neither rule
        // touches it. Emitting these as violations (not requirements) is what makes them publish blockers:
        // the gate, the organiser checklist and the reviewer checklist all project this one list, so there
        // is no second place an authorization rule could be written and then drift.
        if (product == EventProduct.Public && representation is { } rep)
        {
            // D-352 — the bypass lifts the two consent blockers, and only those. It does not touch the
            // product rules above, the eligibility requirements, or the financial review below: those are
            // real logic over real inputs, and switching them off would test less rather than more.
            if (rep.RepresentsInstitution)
            {
                if (!rep.HasApprovedAuthorization && !rep.AuthorizationBypassed)
                    violations.Add("event_authorization_required");
            }
            else
            {
                // D-353 — a Public event must represent a real organization, archetype or not. This used to
                // fire only for archetypes that declared `RequiresRepresentation`, which left every other
                // public archetype publishable with no institution answerable for it. The creation gate
                // refuses the same shape up front; this is the twin that catches an event which reached
                // Draft before the rule existed, or whose representation was withdrawn afterwards.
                //
                // Bypassable with the rest of the consent blockers: outside Production nobody has an
                // admin-approved org to represent, so enforcing it would make public events untestable.
                if (!rep.AuthorizationBypassed) violations.Add("representation_required");
            }

            // Step 8 — D-266 M7. A fundraiser solicits money for a cause, so FinanceOps clears the money
            // path before it goes live. Keyed on the archetype, not on "is it paid": the risk is soliciting
            // on behalf of a cause, and a ticketed concert (A8) carries no such risk.
            if (rep.ArchetypeRequiresFinancialReview && !rep.FinancialReviewPassed)
                violations.Add("financial_review_required");
        }

        return new PolicyResolution(
            allowed, selected, GateMap[selected], IdentityFor(selected), requirements, violations);
    }

    /// <summary>The publish checklist and the reviewer checklist are two projections of this one array —
    /// never two lists maintained in parallel, which is how they end up disagreeing about whether an event
    /// is ready.</summary>
    public static IReadOnlyList<string> PublishBlockers(PolicyResolution r) => r.Violations;

    public static IReadOnlyList<string> ReviewerChecklist(PolicyResolution r)
        => r.Requirements.Concat(r.Violations).ToList();
}
