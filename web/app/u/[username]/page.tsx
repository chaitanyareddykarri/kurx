import Link from "next/link";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Card, LinkButton, StatCard } from "@kurx/ui";

import {
  getPublicProfile, getPublicProfileCertificates, getPublicProfileEvents,
  getPublicProfileTimeline, getPublicProfileAllies, listMyAllies,
  listIncomingAllyRequests, listOutgoingAllyRequests,
  getPublicProfileJourney, getPublicProfileCompetitions, getPublicProfileSessions,
  getPublicProfileMetrics, getPublicProfileExperience, getPublicProfileContributions,
  getPublicProfileAssignments, publicProfileResumeUrl, section, sectionData,
  getMyIdentity, getMyIdentityHistory,
} from "@/lib/api";
import { currentSession } from "@/lib/session";
import { createMetadata, jsonLd } from "@/lib/site";

import { ProfileHeader } from "@/components/profile/profile-header";
import { ProfileNav, type ProfileNavItem } from "@/components/profile/profile-nav";
import { TrustPanel } from "@/components/profile/trust-panel";
import { AboutPanel } from "@/components/profile/about-panel";
import { VerificationSection } from "@/components/profile/verification-section";
import { JourneyRail } from "@/components/profile/journey-rail";
import { MetricsPanel } from "@/components/profile/metrics-panel";
import { ContributionsHeatmap } from "@/components/profile/contributions-heatmap";
import { VerifiedSections } from "@/components/profile/verified-sections";
import { ProfileSections } from "@/components/profile/profile-sections";
import { SectionUnavailable } from "@/components/profile/section-unavailable";
import { AllyConnectButton } from "@/components/profile/ally-connect-button";
import { MessageButton } from "@/components/profile/message-button";
import { formatJoinedMonth } from "@/lib/formatters";

/// Adds two counts that may each be hidden. If either is, the total would be a partial number
/// presented as a whole — so the tile shows an em dash instead of quietly under-reporting.
function sumOrDash(a: number | null, b: number | null): number | string {
  return a === null || b === null ? "—" : a + b;
}

export async function generateMetadata({ params }: { params: { username: string } }): Promise<Metadata> {
  const profile = await getPublicProfile(params.username).catch(() => null);
  const title = profile ? `${profile.name} (@${params.username})` : `${params.username} profile`;
  return createMetadata({ title, path: `/u/${params.username}` });
}

export default async function UserProfilePage({ params }: { params: { username: string } }) {
  // The viewer is resolved first because every read below is viewer-aware (D-229/C1): a signed-in
  // visitor may be entitled to sections an anonymous one is not, and fetching before we know who is
  // asking is exactly the bug that made the Connections tier dead on web.
  const session = await currentSession();
  const token = session?.accessToken;

  // 404 is the only status that means absent — and by D-018 it also covers "hidden from you", which
  // is deliberate and must stay indistinguishable. Anything else is rethrown to the error boundary,
  // which says something went wrong on our side instead of denying the person exists.
  const profileResult = await section(getPublicProfile(params.username, token));
  if (profileResult.state === "hidden") notFound();
  if (profileResult.state === "unavailable") {
    throw new Error(`Could not load the profile for @${params.username}.`);
  }
  const profile = profileResult.data;

  // Every section is fetched independently and classified, never merely swallowed (D-235). A 403 is a
  // normal outcome — the viewer isn't entitled to that section — and renders as absence. A 5xx, a
  // timeout or a dropped connection is NOT: it renders as "couldn't load", because an outage is not
  // evidence that this person has nothing.
  const [
    certificatesR, conductedR, attendedR, timelineR, alliesR, journeyR, competitionsR, sessionsR,
    metricsR, experienceR, contributionsR, assignmentsR,
  ] = await Promise.all([
    section(getPublicProfileCertificates(params.username, token)),
    section(getPublicProfileEvents(params.username, "conducted", token)),
    section(getPublicProfileEvents(params.username, "attended", token)),
    section(getPublicProfileTimeline(params.username, 1, 20, token)),
    section(getPublicProfileAllies(params.username, token)),
    section(getPublicProfileJourney(params.username, token)),
    section(getPublicProfileCompetitions(params.username, token)),
    section(getPublicProfileSessions(params.username, token)),
    section(getPublicProfileMetrics(params.username, token)),
    section(getPublicProfileExperience(params.username, token)),
    section(getPublicProfileContributions(params.username, 12, token)),
    // Phase 2: Experience. Accepted assignments are the one block in About the platform can prove —
    // an organizer created the role and this person accepted it.
    section(getPublicProfileAssignments(params.username, token)),
  ]);

  const certificates = sectionData(certificatesR, []);
  const conducted = sectionData(conductedR, []);
  const attended = sectionData(attendedR, []);
  const timeline = sectionData(timelineR, []);
  const allies = sectionData(alliesR, []);
  const journey = sectionData(journeyR, []);
  const competitions = sectionData(competitionsR, []);
  const sessions = sectionData(sessionsR, []);
  // Null, not empty — a hidden section must be distinguishable from an empty one, so the panel can
  // omit it rather than rendering zeros the owner did not choose to publish.
  const metrics = sectionData(metricsR, null);
  const experience = sectionData(experienceR, null);
  const contributions = sectionData(contributionsR, null);
  const assignments = sectionData(assignmentsR, []);

  // A count is only trustworthy if the fetch that produced it succeeded. When metrics is unavailable
  // the tabs must show no number at all rather than a number derived from a failed request.
  const metricsUnavailable = metricsR.state === "unavailable";
  const events = [...conducted, ...attended].sort((a, b) => b.starts_at.localeCompare(a.starts_at));

  const isOwnProfile = session?.me.id === profile.id;
  let allyRelation: "none" | "outgoing" | "incoming" | "accepted" = "none";
  let allyConnectionId: string | null = null;
  if (session && !isOwnProfile) {
    const [mine, outgoing, incoming] = await Promise.all([
      listMyAllies(session.accessToken).catch(() => []),
      listOutgoingAllyRequests(session.accessToken).catch(() => []),
      listIncomingAllyRequests(session.accessToken).catch(() => []),
    ]);
    const mineHit = mine.find((c) => c.other_user_id === profile.id);
    const outHit = outgoing.find((c) => c.other_user_id === profile.id);
    const inHit = incoming.find((c) => c.other_user_id === profile.id);
    if (mineHit) { allyRelation = "accepted"; allyConnectionId = mineHit.id; }
    else if (inHit) { allyRelation = "incoming"; allyConnectionId = inHit.id; }
    else if (outHit) { allyRelation = "outgoing"; allyConnectionId = outHit.id; }
  }

  // Owner-only, and fetched only when the viewer IS the owner: `/v1/me/identity` is caller-scoped,
  // so issuing it for anyone else would be a wasted request for data they can never be shown.
  const [identity, identityHistory] = isOwnProfile && session
    ? await Promise.all([
        getMyIdentity(session.accessToken).catch(() => null),
        getMyIdentityHistory(session.accessToken).catch(() => []),
      ])
    : [null, []];

  const hasJourney = journeyR.state === "unavailable" || journey.length > 0;
  const hasMetrics = metricsR.state === "unavailable" || metrics !== null || experience !== null;
  const hasActivity = events.length > 0 || certificates.length > 0 || profile.achievements.length > 0
    || profile.organizations.length > 0 || allies.length > 0 || timeline.length > 0;

  // Built from what actually rendered, so the nav can never point at a section this viewer cannot
  // see — an anchor to a withheld section is a 404 inside the page.
  const navItems: ProfileNavItem[] = [
    ...(hasJourney ? [{ id: "journey", label: "Journey" }] : []),
    ...(hasMetrics ? [{ id: "metrics", label: "Metrics" }] : []),
    ...(isOwnProfile ? [{ id: "verification", label: "Verification" }] : []),
    ...(hasActivity ? [{ id: "activity", label: "Activity" }] : []),
    { id: "about", label: "About" },
    { id: "content", label: "Content" },
  ];

  return (
    <main className="container-shell py-lg sm:py-xl">
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd("Person", { name: profile.name, alternateName: params.username })} />

      <ProfileHeader
        username={params.username}
        name={profile.name}
        avatarKey={profile.avatar_key}
        coverKey={profile.cover_key}
        derivedHeadline={profile.derived_headline || null}
        headline={profile.headline}
        summary={profile.summary}
        bio={profile.bio}
        identityLabels={profile.identity_labels}
        // Derived from where their events happened — the profile carries no location field, and this
        // is a stronger statement than a typed one would be.
        cities={metrics?.cities ?? []}
        identityVerified={profile.verification.identity_verified}
        verifiedMember={profile.verification.verified_member}
        resumeHref={publicProfileResumeUrl(params.username)}
        isOwnProfile={!!isOwnProfile}
        completion={
          isOwnProfile
            ? {
                avatarKey: profile.avatar_key,
                coverKey: profile.cover_key,
                headline: profile.headline,
                bio: profile.bio,
                skills: profile.skills ?? [],
                languages: profile.languages ?? [],
                interests: profile.interests ?? [],
                linksJson: profile.links_json,
                hasEducation: !!profile.college,
                emailVerified: profile.verification.email_verified ?? false,
                identityVerified: profile.verification.identity_verified,
              }
            : null
        }
        actions={
          !isOwnProfile && session ? (
            <>
              <AllyConnectButton targetUserId={profile.id} initialRelation={allyRelation} initialConnectionId={allyConnectionId} />
              {/* D-264: start a direct conversation. Idempotent server-side. */}
              <MessageButton targetUserId={profile.id} />
            </>
          ) : null
        }
      />

      <div className="mt-lg grid grid-cols-2 gap-md sm:grid-cols-5">
        {/* Null is "hidden from you", never zero (D-229/H2) — a hidden count renders an em dash so a
            privacy choice is never read as a statement about the person. */}
        <StatCard label="Events" value={sumOrDash(profile.stats.events_conducted, profile.stats.participations)} />
        {/* Organizations comes from the canonical metric, never from organizations.length (D-231):
            a hidden section arrives as [] and measuring it printed "0", which asserts the person
            belongs to no organizations — a claim they never made. */}
        <StatCard label="Organizations" value={metricsUnavailable ? "—" : metrics?.organizations ?? "—"} />
        <StatCard label="Certificates" value={profile.stats.certificates_count ?? "—"} />
        <StatCard label="Achievements" value={profile.stats.achievements ?? "—"} />
        <StatCard label="Allies" value={profile.stats.ally_count ?? "—"} />
      </div>

      <div className="mt-lg">
        <ProfileNav items={navItems} />
      </div>

      {/* Trust leads the body: "can I rely on this person" is the question a profile on an event
          platform exists to answer, and it is answered only by proved signals.

          The D-221 signals are optional on the wire so a client pinned to an older backend still
          parses. Absent means "this backend does not report it", which renders as no badge — the
          same as false, and never as a negative claim. */}
      <div className="mt-lg">
        <TrustPanel
          identityVerified={profile.verification.identity_verified}
          verifiedCertificates={profile.verification.verified_certificates}
          yearsOnPlatform={profile.verification.years_on_platform}
        />
      </div>

      {isOwnProfile ? (
        <section id="verification" className="mt-lg scroll-mt-20">
          <VerificationSection
            identity={identity}
            history={identityHistory}
            emailVerified={session!.me.email_verified}
            // Straight from TrustService — never re-derived here, so this can never disagree with
            // the gate that actually refuses the payment.
            canOrganizePaid={session!.me.can_organize_paid}
            canReceivePayout={session!.me.can_receive_payout}
          />
        </section>
      ) : null}

      {hasJourney ? (
        <section id="journey" className="mt-lg scroll-mt-20">
          {journeyR.state === "unavailable"
            ? <SectionUnavailable label="The professional journey" />
            : <JourneyRail nodes={journey} />}
        </section>
      ) : null}

      {hasMetrics ? (
        <section id="metrics" className="mt-lg scroll-mt-20 space-y-md">
          {metricsR.state === "unavailable" && experienceR.state === "unavailable"
            ? <SectionUnavailable label="Metrics" />
            : <MetricsPanel metrics={metrics} experience={experience} />}
          {contributionsR.state === "unavailable"
            ? <SectionUnavailable label="The contributions graph" />
            : <ContributionsHeatmap contributions={contributions} />}
        </section>
      ) : null}

      <section id="activity" className="mt-lg scroll-mt-20">
        <VerifiedSections competitions={competitions} sessions={sessions} />

        <ProfileSections
          timeline={timeline}
          events={events}
          organizations={profile.organizations}
          counts={{
            // Null anywhere here means "withheld from this viewer" — or, since D-235, "we failed to
            // load the number". Both render no number rather than a zero: a count we could not fetch is
            // not a count of zero, and asserting one would be a lie told by an outage.
            eventsTotal: metricsUnavailable || metrics === null ? null
              : metrics.events_organized === null || metrics.events_participated === null
                ? null
                : metrics.events_organized + metrics.events_participated,
            organizations: metricsUnavailable ? null : metrics?.organizations ?? null,
            certificates: metricsUnavailable ? null : metrics?.certificates ?? null,
            achievements: profile.stats.achievements ?? null,
            allies: profile.stats.ally_count ?? null,
          }}
          certificates={certificates}
          achievements={profile.achievements}
          allies={allies}
        />
      </section>

      <section id="about" className="mt-lg scroll-mt-20">
        <AboutPanel
          bio={profile.bio}
          college={profile.college}
          skills={profile.skills ?? []}
          languages={profile.languages ?? []}
          interests={profile.interests ?? []}
          linksJson={profile.links_json}
          assignments={assignments}
        />
      </section>

      <section id="content" className="mt-lg scroll-mt-20">
        <Card>
          <h2 className="text-lg font-semibold text-text">Content</h2>
          <p className="mt-1 text-sm text-muted">
            Posts open on their own page — the feed pages independently, so embedding it here would
            nest two separately-paging lists.
          </p>
          <div className="mt-lg flex flex-wrap gap-sm">
            <LinkButton href={`/posts/user/${params.username}`} variant="secondary">
              Posts by @{params.username}
            </LinkButton>
            <a
              href={publicProfileResumeUrl(params.username)}
              className="inline-flex min-h-11 items-center rounded-md border border-border px-3 text-sm text-text transition duration-fast hover:bg-elevated"
            >
              Download résumé
            </a>
          </div>
          <p className="mt-lg text-sm text-muted">
            Only verified, public information appears on this profile.{" "}
            {isOwnProfile ? (
              <Link href="/settings/privacy" className="text-accent-text hover:underline">
                Manage what is visible
              </Link>
            ) : null}
          </p>
        </Card>
      </section>

      {/* Account age, month precision. Metadata rather than a profile feature, so it closes the page
          quietly instead of competing with the content above it. The exact creation instant is
          deliberately not on the wire — see PublicProfileView.JoinedAt. */}
      {profile.joined_at ? (
        <p className="mt-lg text-center text-sm text-muted">
          Joined Kurx · {formatJoinedMonth(profile.joined_at)}
        </p>
      ) : null}
    </main>
  );
}
