import type { ReactNode } from "react";
import type { ReviewEvent, ReviewTicketType } from "@/lib/api";

/*
 * What the reviewer reads before deciding.
 *
 * The review queue card carried three fields and deep-linked to the 9-tab event workspace for the rest —
 * except the workspace's Overview tab renders the same 29-field list row, so the description, the rules,
 * the terms, the consent text, the eligibility gates and the registration windows appeared on **neither**
 * screen. Every one of them is collected by the Create Event wizard and stored; none was ever read before
 * approval.
 *
 * Sections, not a field dump: a reviewer decides about an *event*, so the shape follows the wizard's own
 * steps — Overview, Representation, Schedule, Location, Capacity & pricing, Eligibility, Content, Legal.
 * An empty section is omitted rather than rendered as a row of dashes, so the page is short when the
 * event is simple and long when there is genuinely more to read.
 *
 * Read-only by design. Editing a submitted event is `emergency-edit` (audited, SuperAdmin-only) and stays
 * where it is; a review surface that quietly let a reviewer rewrite the thing under review would make the
 * approval mean something else.
 */

type Row = [label: string, value: ReactNode];

/**
 * A section, or nothing at all.
 *
 * The empty check runs in JS rather than as a `:has(dl:empty)` CSS rule: CSS would leave the heading in
 * the accessibility tree, so a screen reader would announce "Eligibility" and then find no content —
 * which is exactly the "row of dashes" this omits. Filtering the rows first means an absent section is
 * absent everywhere.
 *
 * `false` counts as empty because every boolean here is a "show only when true" flag (`auto_close`,
 * `requires_consent`); `0` does not, because a capacity or a team cap of zero is a real answer worth
 * seeing on a review surface.
 */
function Section({ title, rows }: { title: string; rows: Row[] }) {
  const present = rows.filter(([, v]) => v !== null && v !== undefined && v !== "" && v !== false);
  if (present.length === 0) return null;
  return (
    <section className="rounded-lg border border-border">
      <h4 className="border-b border-border px-3 py-2 text-xs font-semibold text-text">{title}</h4>
      <dl>
        {present.map(([label, value]) => (
          <div key={label} className="flex gap-3 border-b border-border/60 px-3 py-2 last:border-0">
            <dt className="w-40 shrink-0 text-xs text-muted">{label}</dt>
            <dd className="min-w-0 flex-1 whitespace-pre-wrap break-words text-xs text-text">{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

const dt = (iso?: string | null) =>
  iso ? new Date(iso).toLocaleString("en-IN", { dateStyle: "medium", timeStyle: "short" }) : null;

export function ReviewDossier({ event: e, tickets = [] }: { event: ReviewEvent; tickets?: ReviewTicketType[] }) {
  const venue = e.venue ?? {};
  const loc = e.location_detail ?? {};
  const el = e.eligibility ?? {};
  const sched = e.schedule ?? {};
  const legal = e.legal ?? {};
  const content = e.content ?? {};

  // "Any" is the server's default and means no restriction — showing it as a rule would read as one.
  const gender = el.gender_restriction && el.gender_restriction !== "Any" ? el.gender_restriction : null;
  const ages = el.min_age != null || el.max_age != null
    ? `${el.min_age ?? "any"} to ${el.max_age ?? "any"}`
    : null;

  // Same "Block A · Floor 3 · Room 301" shape the public surfaces use, so a reviewer and an attendee
  // read the same string.
  const placeInVenue = [
    loc.building,
    loc.floor ? `Floor ${loc.floor}` : null,
    loc.room ? `Room ${loc.room}` : null
  ].filter(Boolean).join(" · ") || null;

  return (
    <div className="mt-3 space-y-3">
      <Section title="Overview" rows={[
        ["Subtitle", e.subtitle],
        ["Description", e.description],
        ["Visibility", e.visibility],
        ["Language", e.language],
        ["Public link", <span key="l" className="font-mono">/e/{e.slug}</span>]
      ]} />

      {/*
        Null representation is the correct and common answer, not a missing field: a Private event — and
        a Public one under the dev verification bypass — is hosted by the person, and D-268 deleted the
        concept of a personal organization. Saying so in words beats an empty section that reads like a
        failed load, which is why this row always has a value and the section always renders.
      */}
      <Section title="Representation" rows={[
        ["Represents", e.representing?.name
          ? `${e.representing.name}${e.representing.is_verified ? " · verified" : " · NOT verified"}`
          : "Hosted by the organiser personally — no organization represented"]
      ]} />

      <Section title="Schedule" rows={[
        ["Starts", dt(e.starts_at)],
        ["Ends", dt(e.ends_at)],
        ["Timezone", e.timezone],
        ["Registration opens", dt(sched.registration_opens_at)],
        ["Registration closes", dt(sched.registration_closes_at)],
        ["Check-in opens", dt(sched.checkin_opens_at)],
        ["Check-in closes", dt(sched.checkin_closes_at)],
        ["Auto-close at capacity", sched.auto_close ? "Yes" : null]
      ]} />

      <Section title="Location" rows={[
        ["Mode", e.event_mode],
        ["Venue", venue.name],
        ["Address", venue.address],
        ["City", venue.city],
        ["Where inside", placeInVenue],
        ["Map", loc.google_maps_url],
        // An Online or Hybrid event reaching review with no join link is unpublishable
        // (`missing_online_url`), so its absence here is itself a review finding.
        ["Join link", e.online_url],
        ["Meeting platform", loc.meeting_platform]
      ]} />

      {/*
        D-357 — pricing WITH its unit. "Price: ₹2,000 · Quantity: 50" is what a reviewer used to get, and
        it cannot distinguish an entry fee for a whole team from one member's share, nor 50 teams from
        50 people. Each row states the unit, the team bounds and what the quantity counts.
      */}
      <Section title="Registration & pricing" rows={tickets.flatMap((t) => {
        const team = t.registration_mode === "Group";
        const rupees = (t.price_paise / 100).toLocaleString("en-IN");
        /*
         * D-366 — a banded ticket has no single price, so a reviewer must be shown the whole table.
         * `price_paise` is the cheapest band on such a ticket, and approving "₹250" on an event that
         * also charges ₹400 puts a reviewer's name against a fee they never saw.
         */
        const bands = t.price_tiers && t.price_tiers.length > 0
          ? [...t.price_tiers].sort((a, b) => a.min_size - b.min_size)
          : null;
        const price = bands
          ? bands
              .map((b) => `${b.min_size === b.max_size ? b.min_size : `${b.min_size}–${b.max_size}`} members → ₹${(b.price_paise / 100).toLocaleString("en-IN")}`)
              .join(" · ")
          : t.price_paise === 0
            ? (team ? "Free · per team" : "Free")
            : `₹${rupees} ${t.pricing_unit === "PerGroup" ? "/ team" : "/ participant"}`;
        const size = team && (t.group_min != null || t.group_max != null)
          ? `${t.group_min ?? "?"}–${t.group_max ?? "?"} members`
          : null;
        return [
          [`${t.name} · price${bands ? " by team size" : ""}`, price],
          [`${t.name} · participation`, team ? "Team" : "Individual"],
          [`${t.name} · team size`, size],
          [`${t.name} · capacity`, t.quantity == null ? null
            : `${t.quantity} ${team ? "team slots" : "places"}${t.sold != null ? ` · ${t.sold} taken` : ""}`]
        ] as [string, React.ReactNode][];
      })} />

      <Section title="Capacity & contact" rows={[
        ["Capacity", e.capacity],
        ["Currency", e.settlement_currency],
        ["Contact email", e.contact_email],
        ["Contact phone", e.contact_phone],
        ["Website", e.website]
      ]} />

      <Section title="Eligibility" rows={[
        ["Age range", ages],
        ["Gender restriction", gender],
        ["Maximum teams", el.max_teams]
      ]} />

      <Section title="Content" rows={[
        ["Tagline", content.tagline],
        ["Short description", content.short_description],
        ["Rules", content.rules]
      ]} />

      <Section title="Legal" rows={[
        ["Terms link", legal.terms_url],
        ["Terms text", legal.terms_text],
        ["Code of conduct", legal.code_of_conduct],
        ["Refund policy", legal.refund_policy],
        ["Cancellation policy", legal.cancellation_policy],
        // The exact wording every registrant's acceptance is recorded against — the one legal field a
        // reviewer is genuinely approving on the platform's behalf. An event that demands acceptance of
        // nothing is a finding, not a blank.
        ["Consent statement", legal.requires_consent ? (legal.consent_text || "REQUIRED BUT EMPTY") : null]
      ]} />
    </div>
  );
}
