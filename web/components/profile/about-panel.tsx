import { Briefcase, GraduationCap, Heart, Languages as LanguagesIcon, Sparkles } from "lucide-react";
import { Card } from "@kurx/ui";

import { ProfileLinks } from "@/components/profile/profile-links";
import { ProvenanceBadge } from "@/components/profile/provenance-badge";

// Every member is nullable on the wire — a college row may carry an institute with no degree, or a
// degree with no institute. Rendering is filter-driven below, so any combination is safe.
type College = { institute: string | null; degree: string | null; branch: string | null } | null;

/** One accepted event assignment — verified experience, not a self-declared job history. */
type Assignment = {
  event_title: string;
  event_slug: string | null;
  role: string;
  starts_at: string | null;
};

function Pill({ children }: { children: React.ReactNode }) {
  return (
    <span className="rounded-pill border border-border bg-elevated px-2.5 py-1 text-sm text-muted">
      {children}
    </span>
  );
}

function Group({
  title,
  icon,
  provenance,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  provenance: "derived" | "self_declared";
  children: React.ReactNode;
}) {
  return (
    <div className="border-t border-border pt-lg first:border-0 first:pt-0">
      <div className="flex flex-wrap items-center gap-sm">
        <span className="text-muted">{icon}</span>
        <h3 className="text-body font-medium text-text">{title}</h3>
        <ProvenanceBadge source={provenance} />
      </div>
      <div className="mt-md">{children}</div>
    </div>
  );
}

/**
 * About — Phase 2.
 *
 * **The panel's organising rule is provenance, not topic.** Bio, education, skills, languages,
 * interests and links are things the person *says*; experience is the one block here the platform
 * can *prove*, because an assignment only appears once an organizer created it and the person
 * accepted it. Each block states which it is, for the same reason the header keeps the derived
 * headline apart from the self-declared one (D-225) — a claim must never inherit a proof's
 * credibility by sitting next to it.
 *
 * Education is marked self-declared deliberately (D-220): `EducationJson` was never migrated into
 * evidence-backed membership claims, so it is a claim like any other.
 *
 * **Interests are declared; Event DNA is derived and is not here.** Event DNA belongs to the
 * Professional Journey, where the rest of the derived material lives. Rendering both in About would
 * put a claim and its evidential counterpart side by side under one heading, which is exactly the
 * conflation this panel is arranged to prevent.
 */
export function AboutPanel({
  bio,
  college,
  skills,
  languages,
  interests,
  linksJson,
  assignments,
}: {
  bio: string | null;
  college: College;
  skills: readonly string[];
  languages: readonly string[];
  interests: readonly string[];
  linksJson: string | null;
  assignments: readonly Assignment[];
}) {
  const collegeParts = [college?.degree, college?.branch, college?.institute].filter(
    (v): v is string => !!v && v.trim().length > 0
  );

  const hasAnything =
    !!bio?.trim() ||
    collegeParts.length > 0 ||
    skills.length > 0 ||
    languages.length > 0 ||
    interests.length > 0 ||
    assignments.length > 0 ||
    !!linksJson;

  // An empty About card would be a heading over nothing. Absence renders as absence.
  if (!hasAnything) return null;

  return (
    <Card>
      <h2 className="text-lg font-semibold text-text">About</h2>

      <div className="mt-lg space-y-lg">
        {bio?.trim() ? (
          <Group title="Bio" icon={<Sparkles size={16} aria-hidden />} provenance="self_declared">
            <p className="whitespace-pre-line text-body text-muted">{bio}</p>
          </Group>
        ) : null}

        {assignments.length > 0 ? (
          <Group title="Experience" icon={<Briefcase size={16} aria-hidden />} provenance="derived">
            <ul className="space-y-md">
              {assignments.map((a, i) => (
                <li
                  key={`${a.event_slug ?? a.event_title}-${a.role}-${i}`}
                  className="flex flex-wrap items-baseline justify-between gap-sm rounded-md border border-border bg-elevated px-md py-sm"
                >
                  <span className="min-w-0">
                    <span className="block text-body font-medium text-text">{a.role}</span>
                    <span className="block truncate text-sm text-muted">{a.event_title}</span>
                  </span>
                  {a.starts_at ? (
                    <time
                      dateTime={a.starts_at}
                      className="shrink-0 text-sm text-muted"
                    >
                      {new Date(a.starts_at).getFullYear()}
                    </time>
                  ) : null}
                </li>
              ))}
            </ul>
          </Group>
        ) : null}

        {collegeParts.length > 0 ? (
          <Group
            title="Education"
            icon={<GraduationCap size={16} aria-hidden />}
            provenance="self_declared"
          >
            <div className="flex flex-wrap gap-1.5">
              {collegeParts.map((item) => <Pill key={item}>{item}</Pill>)}
            </div>
          </Group>
        ) : null}

        {skills.length > 0 ? (
          <Group title="Skills" icon={<Sparkles size={16} aria-hidden />} provenance="self_declared">
            <div className="flex flex-wrap gap-1.5">
              {skills.map((s) => <Pill key={s}>{s}</Pill>)}
            </div>
          </Group>
        ) : null}

        {languages.length > 0 ? (
          <Group
            title="Languages"
            icon={<LanguagesIcon size={16} aria-hidden />}
            provenance="self_declared"
          >
            <div className="flex flex-wrap gap-1.5">
              {languages.map((l) => <Pill key={l}>{l}</Pill>)}
            </div>
          </Group>
        ) : null}

        {interests.length > 0 ? (
          <Group title="Interests" icon={<Heart size={16} aria-hidden />} provenance="self_declared">
            <div className="flex flex-wrap gap-1.5">
              {interests.map((i) => <Pill key={i}>{i}</Pill>)}
            </div>
          </Group>
        ) : null}

        {linksJson ? (
          <div className="border-t border-border pt-lg">
            <h3 className="text-body font-medium text-text">Links</h3>
            <ProfileLinks linksJson={linksJson} />
          </div>
        ) : null}
      </div>
    </Card>
  );
}
