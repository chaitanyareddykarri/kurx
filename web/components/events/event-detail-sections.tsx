import type { ReactNode } from "react";
import type { EventDetail } from "@/lib/api";

/*
 * The parts of an event an attendee needs BEFORE registering, and that no client rendered.
 *
 * D-265 added six field groups to the create-event contract — content, legal, schedule windows,
 * location detail, eligibility and commerce. They are validated by the wizard, stored on `events`,
 * returned by `GET /v1/events/{slug}`, and were read by nothing: the public page showed title,
 * subtitle, description, banner, venue, start, mode, representation, gallery, tickets and reviews.
 *
 * So an organiser could set "18+", "no refunds inside 7 days", a registration window that closes on
 * Friday, and a code of conduct every registrant is bound by — and the person deciding whether to
 * register saw none of it. The rules a registration is subject to have to be legible before it, not
 * discoverable after.
 *
 * What is deliberately NOT here:
 *   · `capacity` — a raw ceiling is not a scarcity signal; "N left" already comes from the ticket
 *     types, which read the authoritative inventory pool rather than this number.
 *   · `meeting_password` — shown only to confirmed registrants, by design.
 *   · `commerce` (platform fee, tax) — organiser accounting, never attendee-facing.
 *   · `result_date` / `certificate_release_at` — meaningful only to a participant, and the archetype
 *     may not support them at all (D-327).
 */

/**
 * A price and what it buys (D-357).
 *
 * `₹2,000` alone is the ambiguity this exists to remove: a registrant cannot tell an entry fee for a
 * whole team from one person's share of it. `pricing_unit` has been on the public
 * `GET /v1/events/{id}/ticket-types` response all along and no surface rendered it.
 */
export function priceLabel(
  pricePaise: number,
  pricingUnit: string | null | undefined,
  format: (paise: number) => string,
  /// D-366 — when the ticket is priced by team size, one number cannot describe it. Passing the bands
  /// turns the headline into a "from" figure, which is what `price_paise` actually is then.
  tiers?: { min_size: number; max_size: number; price_paise: number }[] | null
): string {
  if (tiers && tiers.length > 0) {
    const prices = tiers.map((t) => t.price_paise);
    const low = Math.min(...prices);
    const high = Math.max(...prices);
    return low === high
      ? `${format(low)} per team`
      : `${format(low)}–${format(high)} per team, by size`;
  }
  if (pricePaise === 0) return pricingUnit === "PerGroup" ? "Free per team" : "Free";
  return pricingUnit === "PerGroup"
    ? `${format(pricePaise)} per team`
    : `${format(pricePaise)} per participant`;
}

/// D-366 — the full band table, as rows a surface can render.
///
/// The decision's §21: a registrant must never be shown a price they cannot interpret. "₹300" is
/// meaningless on an event that also charges ₹250 and ₹400; "3 members — ₹300" is not. Returns null
/// when the ticket has no bands, so the caller renders its single price as before.
export function teamPriceRows(
  tiers: { min_size: number; max_size: number; price_paise: number }[] | null | undefined,
  format: (paise: number) => string
): { size: string; price: string }[] | null {
  if (!tiers || tiers.length === 0) return null;
  return [...tiers]
    .sort((a, b) => a.min_size - b.min_size)
    .map((t) => ({
      size: t.min_size === t.max_size ? `${t.min_size} members` : `${t.min_size}–${t.max_size} members`,
      price: format(t.price_paise)
    }));
}

/// "teams of 3–5", or null when the ticket is not a team entry or the organiser set no bounds.
export function teamSizeLabel(
  registrationMode: string | null | undefined,
  groupMin: number | null | undefined,
  groupMax: number | null | undefined
): string | null {
  if (registrationMode !== "Group") return null;
  if (groupMin == null && groupMax == null) return null;
  if (groupMin != null && groupMax != null) {
    return groupMin === groupMax ? `teams of ${groupMin}` : `teams of ${groupMin}–${groupMax}`;
  }
  return groupMin != null ? `teams of ${groupMin}+` : `teams of up to ${groupMax}`;
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="rounded-md border border-border bg-surface px-3 py-2">
      <dt className="text-caption text-muted">{label}</dt>
      <dd className="mt-0.5 whitespace-pre-wrap text-body text-text">{children}</dd>
    </div>
  );
}

const when = (iso?: string | null) =>
  iso ? new Date(iso).toLocaleString("en-IN", { dateStyle: "medium", timeStyle: "short" }) : null;

/** Who may register, and by when. Rendered only when the organiser actually set a rule. */
export function ParticipationSection({ event }: { event: EventDetail }) {
  const el = event.eligibility ?? null;
  const sched = event.schedule ?? null;

  // "Any" is the server's default for no restriction; rendering it would read as a rule.
  const gender = el?.gender_restriction && el.gender_restriction !== "Any" ? el.gender_restriction : null;
  const ages = el?.min_age != null || el?.max_age != null
    ? el?.min_age != null && el?.max_age != null
      ? `${el.min_age}–${el.max_age}`
      : el?.min_age != null ? `${el.min_age} and over` : `${el!.max_age} and under`
    : null;

  const opens = when(sched?.registration_opens_at);
  const closes = when(sched?.registration_closes_at);
  const facts = [ages, gender, el?.max_teams, opens, closes].filter((v) => v !== null && v !== undefined);
  if (facts.length === 0) return null;

  return (
    <section>
      <h2 className="text-h2">Who can join</h2>
      <dl className="mt-3 grid gap-2 sm:grid-cols-2">
        {ages ? <Fact label="Age">{ages}</Fact> : null}
        {gender ? <Fact label="Open to">{gender}</Fact> : null}
        {el?.max_teams != null ? <Fact label="Maximum teams">{el.max_teams}</Fact> : null}
        {opens ? <Fact label="Registration opens">{opens}</Fact> : null}
        {closes ? <Fact label="Registration closes">{closes}</Fact> : null}
      </dl>
    </section>
  );
}

/** How to get in, once you have a ticket — the check-in window and the in-person specifics. */
export function GettingThereSection({ event }: { event: EventDetail }) {
  const loc = event.location_detail ?? null;
  const sched = event.schedule ?? null;
  const where = [loc?.building, loc?.floor ? `Floor ${loc.floor}` : null, loc?.room ? `Room ${loc.room}` : null]
    .filter(Boolean).join(" · ");
  const checkinOpens = when(sched?.checkin_opens_at);
  const checkinCloses = when(sched?.checkin_closes_at);
  if (!where && !loc?.google_maps_url && !loc?.meeting_platform && !checkinOpens && !checkinCloses) return null;

  return (
    <section>
      <h2 className="text-h2">Getting there</h2>
      <dl className="mt-3 grid gap-2 sm:grid-cols-2">
        {where ? <Fact label="Where inside">{where}</Fact> : null}
        {loc?.google_maps_url ? (
          <Fact label="Map">
            <a href={loc.google_maps_url} target="_blank" rel="noopener noreferrer"
              className="text-accent-text hover:underline">Open in Google Maps</a>
          </Fact>
        ) : null}
        {loc?.meeting_platform ? <Fact label="Platform">{loc.meeting_platform}</Fact> : null}
        {checkinOpens ? <Fact label="Check-in opens">{checkinOpens}</Fact> : null}
        {checkinCloses ? <Fact label="Check-in closes">{checkinCloses}</Fact> : null}
      </dl>
    </section>
  );
}

/** The rules of the event itself — what the organiser wrote, not the platform's terms. */
export function RulesSection({ event }: { event: EventDetail }) {
  const rules = event.content?.rules;
  const coc = event.legal?.code_of_conduct;
  if (!rules && !coc) return null;
  return (
    <section>
      <h2 className="text-h2">Rules</h2>
      <div className="mt-3 space-y-3">
        {rules ? <p className="whitespace-pre-wrap text-body text-muted">{rules}</p> : null}
        {coc ? (
          <div className="rounded-md border border-border bg-surface p-3">
            <p className="text-label text-text">Code of conduct</p>
            <p className="mt-1 whitespace-pre-wrap text-body text-muted">{coc}</p>
          </div>
        ) : null}
      </div>
    </section>
  );
}

/**
 * What you are agreeing to.
 *
 * The consent statement is the one that matters most: `RegistrationConsent` records an acceptance
 * against this exact wording, so showing it only at the moment of the checkbox — or not at all, which
 * is what happened — means the binding text was never legible while deciding.
 */
export function TermsSection({ event }: { event: EventDetail }) {
  const l = event.legal ?? null;
  if (!l) return null;
  const has = l.terms_url || l.terms_text || l.refund_policy || l.cancellation_policy
    || (l.requires_consent && l.consent_text);
  if (!has) return null;

  return (
    <section>
      <h2 className="text-h2">Terms</h2>
      <div className="mt-3 space-y-3">
        {l.requires_consent && l.consent_text ? (
          <div className="rounded-md border border-accent/30 bg-accent/5 p-3">
            <p className="text-label text-text">You will be asked to accept</p>
            <p className="mt-1 whitespace-pre-wrap text-body text-muted">{l.consent_text}</p>
          </div>
        ) : null}
        <dl className="grid gap-2 sm:grid-cols-2">
          {l.refund_policy ? <Fact label="Refunds">{l.refund_policy}</Fact> : null}
          {l.cancellation_policy ? <Fact label="Cancellation">{l.cancellation_policy}</Fact> : null}
        </dl>
        {l.terms_text ? <p className="whitespace-pre-wrap text-body text-muted">{l.terms_text}</p> : null}
        {l.terms_url ? (
          <p className="text-body">
            <a href={l.terms_url} target="_blank" rel="noopener noreferrer"
              className="text-accent-text hover:underline">Full terms for this event</a>
          </p>
        ) : null}
      </div>
    </section>
  );
}
