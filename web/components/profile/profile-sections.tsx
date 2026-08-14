"use client";

import { useState } from "react";
import {
  Award, Building2, CalendarDays, CheckCircle2, Handshake, Medal, Sparkles, Trophy,
} from "lucide-react";
import { Tabs, TabPanel, Card, EmptyState, Badge, Chip } from "@kurx/ui";
import type {
  TimelineEntry, PublicEventCard, ProfileOrg, PublicCertificateCard, AchievementCard, AllyProfileCard,
} from "@/lib/api";
import { formatDate } from "@/lib/formatters";

const KIND_ICON: Record<TimelineEntry["kind"], React.ReactNode> = {
  org_joined: <Building2 size={16} />,
  org_verified: <CheckCircle2 size={16} />,
  participation: <CalendarDays size={16} />,
  achievement: <Trophy size={16} />,
  certificate: <Medal size={16} />,
  organized: <Sparkles size={16} />,
  attended: <CalendarDays size={16} />,
};

/**
 * Counts for the tab labels. **Never derived from `array.length`** (D-231).
 *
 * A hidden section arrives as an empty array, which is indistinguishable from "this person genuinely
 * has none" — so measuring the array printed `Organizations (0)` for someone who had simply chosen
 * not to publish them. Counts come from the canonical metric source instead (D-229), where hidden is
 * `null` and can be told apart from zero.
 *
 * `null` ⇒ the count is withheld, so the label carries no number at all. A bare "Organizations" says
 * nothing false; "Organizations (0)" says something false.
 */
export type SectionCounts = {
  eventsTotal: number | null;
  organizations: number | null;
  certificates: number | null;
  achievements: number | null;
  allies: number | null;
};

function count(value: number | null | undefined): string {
  return value === null || value === undefined ? "" : ` (${value})`;
}

/** Only one profile-sections strip renders per page, so a literal is enough to key tab↔panel. */
const TABS_ID = "profile-sections";

export function ProfileSections({
  timeline, events, organizations, certificates, achievements, allies, counts,
}: {
  timeline: TimelineEntry[];
  events: PublicEventCard[];
  organizations: ProfileOrg[];
  certificates: PublicCertificateCard[];
  achievements: AchievementCard[];
  allies: AllyProfileCard[];
  counts?: SectionCounts;
}) {
  const [tab, setTab] = useState("timeline");

  return (
    <div className="mt-8">
      <Tabs
        id={TABS_ID}
        value={tab}
        onChange={setTab}
        tabs={[
          { id: "timeline", label: "Timeline" },
          { id: "events", label: `Events${count(counts?.eventsTotal)}` },
          { id: "organizations", label: `Organizations${count(counts?.organizations)}` },
          { id: "certificates", label: `Certificates${count(counts?.certificates)}` },
          { id: "achievements", label: `Achievements${count(counts?.achievements)}` },
          { id: "allies", label: `Allies${count(counts?.allies)}` },
        ]}
      />
      <div className="mt-4">
        <TabPanel tabsId={TABS_ID} id="timeline" active={tab === "timeline"}>
          <TimelineList entries={timeline} />
        </TabPanel>
        <TabPanel tabsId={TABS_ID} id="events" active={tab === "events"}>
          <EventsList events={events} />
        </TabPanel>
        <TabPanel tabsId={TABS_ID} id="organizations" active={tab === "organizations"}>
          <OrgsList organizations={organizations} />
        </TabPanel>
        <TabPanel tabsId={TABS_ID} id="certificates" active={tab === "certificates"}>
          <CertificatesList certificates={certificates} />
        </TabPanel>
        <TabPanel tabsId={TABS_ID} id="achievements" active={tab === "achievements"}>
          <AchievementsList achievements={achievements} />
        </TabPanel>
        <TabPanel tabsId={TABS_ID} id="allies" active={tab === "allies"}>
          <AlliesList allies={allies} />
        </TabPanel>
      </div>
    </div>
  );
}

function TimelineList({ entries }: { entries: TimelineEntry[] }) {
  if (entries.length === 0)
    return <EmptyState icon={<CalendarDays size={32} />} title="No milestones yet" message="Verified event activity will show up here." />;
  return (
    <div className="space-y-3">
      {entries.map((e, i) => (
        <Card key={i} className="flex items-start gap-3">
          <span className="mt-0.5 text-accent">{KIND_ICON[e.kind]}</span>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-1.5">
              <p className="font-semibold text-text">{e.title}</p>
              {e.is_first_event && <Badge tone="accent">First on Kurx</Badge>}
            </div>
            {e.roles.length > 0 && (
              <div className="mt-1 flex flex-wrap gap-1">
                {e.roles.map((r) => <Chip key={r}>{r}</Chip>)}
              </div>
            )}
            <p className="mt-1 text-xs text-muted">
              {[e.org_name, e.city, formatDate(e.occurred_at)].filter(Boolean).join(" · ")}
            </p>
          </div>
        </Card>
      ))}
    </div>
  );
}

function EventsList({ events }: { events: PublicEventCard[] }) {
  if (events.length === 0)
    return <EmptyState icon={<CalendarDays size={32} />} title="No events yet" message="Public event involvement will show up here." />;
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {events.map((e) => (
        <Card key={e.id}>
          <a href={`/e/${e.slug}`} className="font-semibold text-text hover:text-accent-text hover:underline">{e.title}</a>
          <p className="mt-1 text-xs text-muted">{[e.org_name, e.city, formatDate(e.starts_at)].join(" · ")}</p>
          <div className="mt-2 flex flex-wrap gap-1.5">
            {e.roles.map((r) => <Chip key={r}>{r}</Chip>)}
            {e.is_achievement && <Badge tone="accent">Achievement</Badge>}
            {e.certificate_verify_code && (
              <a
                href={`/verify/${e.certificate_verify_code}`}
                aria-label={`Verify the certificate for ${e.title}`}
                className="rounded-full focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              >
                <Badge tone="success">Certificate</Badge>
              </a>
            )}
          </div>
        </Card>
      ))}
    </div>
  );
}

function OrgsList({ organizations }: { organizations: ProfileOrg[] }) {
  if (organizations.length === 0)
    return <EmptyState icon={<Building2 size={32} />} title="No verified organizations" message="Real institutional involvement will show up here." />;
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {organizations.map((o) => (
        <Card key={o.org_id}>
          <div className="flex items-center gap-1.5">
            <a href={`/o/${o.org_slug}`} className="font-semibold text-text hover:text-accent-text hover:underline">{o.org_name}</a>
            {o.is_verified && <Badge tone="accent">Verified</Badge>}
          </div>
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            {o.roles.map((r) => <Chip key={r}>{r}</Chip>)}
          </div>
          <p className="mt-2 text-xs text-muted">
            {o.org_events_conducted} events · {o.org_certificates_count} certificates · {o.org_achievements_count} achievements
          </p>
          <p className="mt-1 text-xs text-muted">
            Since {formatDate(o.joined_at)}{o.valid_until ? ` · until ${formatDate(o.valid_until)}` : ""}
          </p>
        </Card>
      ))}
    </div>
  );
}

function CertificatesList({ certificates }: { certificates: PublicCertificateCard[] }) {
  if (certificates.length === 0)
    return <EmptyState icon={<Medal size={32} />} title="No certificates yet" />;
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {certificates.map((c) => (
        <a
          key={c.id}
          href={`/verify/${c.verify_code}`}
          className="rounded-lg focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <Card className="transition duration-fast hover:border-accent/50">
            <div className="flex items-center gap-1.5">
              <Medal size={16} className="text-accent" />
              <p className="font-semibold text-text">{c.event_title}</p>
            </div>
            <p className="mt-1 text-xs text-muted">Issued {formatDate(c.issued_at)}</p>
          </Card>
        </a>
      ))}
    </div>
  );
}

function AchievementsList({ achievements }: { achievements: AchievementCard[] }) {
  if (achievements.length === 0)
    return <EmptyState icon={<Trophy size={32} />} title="No achievements yet" message="Recognitions earned through the platform will show up here." />;
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {achievements.map((a, i) => (
        <Card key={i}>
          <div className="flex items-center gap-1.5">
            <Trophy size={16} className="text-accent" />
            <p className="font-semibold text-text">{a.name}</p>
            <Badge tone={a.source === "certificate" ? "accent" : "muted"}>
              {a.source === "certificate" ? "Achievement" : "Platform Recognition"}
            </Badge>
          </div>
          <p className="mt-1 text-xs text-muted">
            {[a.event_title, a.org_name, formatDate(a.earned_at)].filter(Boolean).join(" · ")}
          </p>
        </Card>
      ))}
    </div>
  );
}

function AlliesList({ allies }: { allies: AllyProfileCard[] }) {
  if (allies.length === 0)
    return <EmptyState icon={<Handshake size={32} />} title="No allies yet" message="Mutual professional connections will show up here." />;
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {allies.map((a) => (
        <Card key={a.user_id} className="flex items-center justify-between gap-3">
          <div className="min-w-0">
            {/* An ally without a username has no public profile to open. It previously rendered
                `href="#"` — a focusable, link-announced control that navigated nowhere (the REG-004
                pattern). Plain text is the honest rendering; the name is still shown. */}
            {a.username ? (
              <a href={`/u/${a.username}`} className="font-semibold text-text hover:text-accent-text hover:underline">{a.name}</a>
            ) : (
              <p className="font-semibold text-text">{a.name}</p>
            )}
            {a.username && <p className="text-xs text-muted">@{a.username}</p>}
          </div>
          {a.mutual_event_count > 0 && (
            // `aria-label` on a bare <svg> is ignored by several screen readers — the element needs
            // an explicit img role before the name is exposed at all.
            <Award
              size={14}
              role="img"
              className="shrink-0 text-muted"
              aria-label={`${a.mutual_event_count} shared event${a.mutual_event_count === 1 ? "" : "s"}`}
            />
          )}
        </Card>
      ))}
    </div>
  );
}
