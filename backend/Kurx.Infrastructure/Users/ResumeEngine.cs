using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kurx.Infrastructure.Users;

/// <summary>The Professional Resume (D-228) — a projection of the fact-set, not a template engine.
///
/// <para><b>It introduces no model of its own.</b> Every section is the same data the profile already
/// renders, composed by the same engines: the header's headline comes from <see cref="IdentityEngine"/>,
/// the spine from <see cref="JourneyEngine"/>, the counts from <see cref="ExperienceEngine"/>. If a
/// fact is not on the profile it is not on the resume, which is what keeps the two from drifting into
/// two different claims about one person.</para>
///
/// <para><b>Generated through the resolver, for the requesting viewer.</b> A section hidden from that
/// viewer is absent from their copy. Without this the resume would be a trivial bypass of the entire
/// privacy model — download the PDF and read what the page would not show you.</para>
///
/// <para><b>One template, no custom sections.</b> A template picker is personalisation with no trust
/// value; a free-text custom section is a self-declared claim inside a document whose whole worth is
/// that it is not self-declared. Neither is built, and neither is a gap.</para>
///
/// <para><b>Never stored.</b> A saved copy is stale the moment the next event completes, and creates
/// a "which version is real" problem on a document people will forward. Always regenerated, and the
/// footer carries the generation date so a stale printout is self-evident.</para></summary>
public class ResumeEngine
{
    static ResumeEngine()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string Accent = "#4F46E5";
    private const string Muted = "#6B7280";

    /// <summary>Composes the PDF. <paramref name="profileUrl"/> is printed as the verification anchor —
    /// every claim here is checkable against the live profile, which is what makes the document worth
    /// more than a typed CV.</summary>
    public byte[] Render(
        PublicProfileView profile,
        ProfileFactSet facts,
        ExperienceSummary experience,
        IReadOnlyList<JourneyNode> journey,
        SectionAccess access,
        string profileUrl,
        DateTime generatedAt)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Calibri).FontSize(10));

                page.Header().Element(e => ComposeHeader(e, profile, experience));
                page.Content().PaddingVertical(12).Element(e =>
                    ComposeBody(e, profile, facts, experience, journey, access));
                page.Footer().Element(e => ComposeFooter(e, profileUrl, generatedAt));
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, PublicProfileView profile, ExperienceSummary experience)
    {
        container.Column(col =>
        {
            col.Item().Text(profile.Name).FontSize(22).Bold();

            // The derived headline, not the self-declared one — the resume leads with what Kurx can
            // prove. The self-declared introduction appears later, explicitly labelled.
            if (!string.IsNullOrWhiteSpace(profile.DerivedHeadline))
                col.Item().PaddingTop(2).Text(profile.DerivedHeadline).FontSize(12).FontColor(Accent);

            var signals = TrustSignals(profile.Verification).ToList();
            if (signals.Count > 0)
                col.Item().PaddingTop(4).Text(string.Join("  ·  ", signals)).FontSize(9).FontColor(Muted);

            col.Item().PaddingTop(4).Text(
                $"{experience.Band}  ·  {experience.DistinctEvents} events  ·  {experience.YearsActive} years on Kurx")
                .FontSize(9).FontColor(Muted);

            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Accent);
        });
    }

    /// <summary>Positive signals only — the same rule the public profile follows (D-221). Nothing here
    /// can read as a negative claim, and an absent signal is indistinguishable from a privacy choice.</summary>
    private static IEnumerable<string> TrustSignals(VerificationBadges v)
    {
        if (v.IdentityVerified) yield return "Identity Verified";
        if (v.VerifiedMember) yield return "Verified Member";
        if (v.CommunityVerified) yield return "Community Verified";
        if (v.SpeakerVerified) yield return "Speaker Verified";
        if (v.EmailVerified) yield return "Email Verified";
        if (v.VerifiedCertificates > 0) yield return $"{v.VerifiedCertificates} verified certificates";
    }

    private static void ComposeBody(
        IContainer container, PublicProfileView profile, ProfileFactSet facts,
        ExperienceSummary experience, IReadOnlyList<JourneyNode> journey, SectionAccess access)
    {
        container.Column(col =>
        {
            col.Spacing(14);

            // The Journey is the resume's spine and its differentiator: a CV lists what someone claims
            // to have done; this shows when each capability was first proven.
            if (journey.Count > 0)
            {
                Section(col, "Professional Journey");
                foreach (var node in journey)
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(96).Text(node.FirstAttainedAt.ToString("MMM yyyy")).FontColor(Muted);
                        row.RelativeItem().Text(text =>
                        {
                            text.Span(TierLabel(node.Tier)).SemiBold();
                            if (node.Occurrences > 1) text.Span($"  ×{node.Occurrences}").FontColor(Muted);
                            var evidence = node.Detail ?? node.EventTitle ?? node.OrgName;
                            if (!string.IsNullOrWhiteSpace(evidence))
                                text.Span($"  —  {evidence}").FontColor(Muted);
                        });
                    });
                }
            }

            if (access.CanSee(ProfileSection.Organizations) && profile.Organizations.Count > 0)
            {
                Section(col, "Organizations");
                foreach (var org in profile.Organizations)
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(96).Text(org.JoinedAt.ToString("MMM yyyy")).FontColor(Muted);
                        row.RelativeItem().Text(text =>
                        {
                            text.Span(org.OrgName).SemiBold();
                            if (org.Roles.Count > 0) text.Span($"  —  {string.Join(", ", org.Roles)}");
                            if (org.IsVerified) text.Span("  ·  Verified").FontColor(Accent);
                        });
                    });
                }
            }

            if (access.CanSee(ProfileSection.Achievements) && facts.Results.Count > 0)
            {
                Section(col, "Competition Results");
                foreach (var r in facts.Results.OrderBy(r => r.Rank).ThenByDescending(r => r.OccurredAt))
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(96).Text(r.OccurredAt.ToString("MMM yyyy")).FontColor(Muted);
                        row.RelativeItem().Text(text =>
                        {
                            text.Span(RankLabel(r.Rank)).SemiBold().FontColor(Accent);
                            text.Span($"  —  {facts.Events[r.EventId].Title}");
                            text.Span($"  ({r.StageName})").FontColor(Muted);
                        });
                    });
                }
            }

            if (access.CanSee(ProfileSection.Events) && facts.Sessions.Count > 0)
            {
                Section(col, "Speaking");
                foreach (var s in facts.Sessions.OrderByDescending(s => s.StartsAt))
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(96).Text(s.StartsAt.ToString("MMM yyyy")).FontColor(Muted);
                        row.RelativeItem().Text(text =>
                        {
                            text.Span(s.SessionTitle).SemiBold();
                            text.Span($"  —  {facts.Events[s.EventId].Title}").FontColor(Muted);
                        });
                    });
                }
            }

            if (access.CanSee(ProfileSection.Events) && facts.Assignments.Count > 0)
            {
                Section(col, "Event Roles");
                foreach (var a in facts.Assignments.OrderByDescending(a => facts.Events[a.EventId].StartsAt))
                {
                    var role = a.Role == "Custom" && !string.IsNullOrWhiteSpace(a.CustomRole) ? a.CustomRole! : a.Role;
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(96)
                            .Text(facts.Events[a.EventId].StartsAt.ToString("MMM yyyy")).FontColor(Muted);
                        row.RelativeItem().Text(text =>
                        {
                            text.Span(role).SemiBold();
                            text.Span($"  —  {facts.Events[a.EventId].Title}");
                            if (a.CompletedAt.HasValue) text.Span("  ·  Completed").FontColor(Accent);
                        });
                    });
                }
            }

            // Certificates carry their verify code: every one is independently checkable by anyone
            // holding this document, which is the point of printing them at all.
            if (access.CanSee(ProfileSection.Certificates) && facts.Certificates.Count > 0)
            {
                Section(col, "Certificates");
                foreach (var c in facts.Certificates.OrderByDescending(c => c.IssuedAt))
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(96).Text(c.IssuedAt.ToString("MMM yyyy")).FontColor(Muted);
                        row.RelativeItem().Text(text =>
                        {
                            text.Span(facts.Events[c.EventId].Title).SemiBold();
                            text.Span($"  ·  verify {c.VerifyCode}").FontColor(Muted);
                        });
                    });
                }
            }

            // Last, and visibly separated: the only text on this page the person wrote themselves.
            if (!string.IsNullOrWhiteSpace(profile.Bio))
            {
                Section(col, "Introduction");
                col.Item().Text("Written by this person — not verified by Kurx.")
                    .FontSize(8).Italic().FontColor(Muted);
                col.Item().PaddingTop(2).Text(profile.Bio!);
            }
        });
    }

    private static void ComposeFooter(IContainer container, string profileUrl, DateTime generatedAt)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Muted);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Every entry is verifiable at ").FontSize(8).FontColor(Muted);
                    text.Span(profileUrl).FontSize(8).FontColor(Accent);
                });
                row.ConstantItem(150).AlignRight()
                    .Text($"Generated {generatedAt:d MMM yyyy}").FontSize(8).FontColor(Muted);
            });
        });
    }

    private static void Section(ColumnDescriptor col, string title) =>
        col.Item().PaddingTop(6).Text(title).FontSize(12).Bold().FontColor(Accent);

    private static string RankLabel(int rank) => rank switch
    {
        1 => "Winner",
        2 => "Runner-up",
        3 => "Third place",
        _ => $"#{rank}",
    };

    private static string TierLabel(string tier) => tier switch
    {
        "attendee" => "Attendee",
        "participant" => "Participant",
        "volunteer" => "Volunteer",
        "team_lead" => "Team Lead",
        "competition_winner" => "Competition Winner",
        "speaker" => "Speaker",
        "judge" => "Judge",
        "mentor" => "Mentor",
        "organizer" => "Organizer",
        "host" => "Host",
        "verified_member" => "Verified Member",
        _ => tier,
    };
}
