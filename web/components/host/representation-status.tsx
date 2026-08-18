import { AlertTriangle, Clock, FileCheck2, ShieldCheck } from "lucide-react";
import { Card, LinkButton } from "@kurx/ui";
import type { EventAuthorizationView } from "@/lib/api";

/**
 * D-382 — Readiness REPORTS representation; it never collects it.
 *
 * This card used to be the whole `AuthorizationForm`: signatory, designation, official email, phone,
 * role, linked account and the letter upload, rendered a second time on a page whose job is to say
 * whether the event can go live. The same seven inputs existed on the Representing step of Create
 * Event, so an organiser was asked for the same authorization twice, in two places, with two chances
 * to file two different answers against one `event_authorizations` row.
 *
 * What is left is the state that form produces, and a way to reach it. Everything here is derived from
 * the authorization the server holds — nothing is asked, nothing is submitted.
 */
export function RepresentationStatus({ eventId, orgName, isPersonal = false, authorization }: {
  eventId: string;
  /// The organization the event REPRESENTS (D-267/D-271) — never the person who owns it.
  orgName: string | null;
  /// The event's "organization" is a pre-D-379 self-representation row (D-268). Named as legacy rather
  /// than left blank: an empty line reads as "loading", and printing the person's name here would tell
  /// an organiser they have institutional backing nobody ever gave. Same call the admin console makes.
  isPersonal?: boolean;
  authorization: EventAuthorizationView | null;
}) {
  const state = stateOf(authorization);

  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="flex items-center gap-2 text-lg font-semibold text-text">
            <FileCheck2 size={16} aria-hidden /> Representation
          </h2>
          <p className="mt-1 text-sm text-text">
            {isPersonal ? "Legacy personal representation" : orgName ?? "Not named"}
          </p>
          <p className={`mt-1 flex items-center gap-1.5 text-sm font-semibold ${state.tone}`}>
            <state.icon size={14} aria-hidden /> {state.label}
          </p>
          <p className="mt-1 text-sm text-muted">{state.hint}</p>
          {/* The reviewer's own words, so "action required" is never a verdict without a reason. */}
          {authorization?.reason_code || authorization?.notes ? (
            <p className="mt-2 rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-sm text-text">
              {authorization.reason_code ? <strong>{authorization.reason_code}</strong> : null}
              {authorization.notes ? <span> — {authorization.notes}</span> : null}
            </p>
          ) : null}
        </div>
        {/* One destination, whatever the state: Edit Event. The workspace's own Representing tab is
            gone — representation is answered inside Create Event, and the only time it is answered
            again is when a reviewer asks for changes, which is an EDIT of the event, not a second
            workflow parked in the workspace. */}
        <LinkButton href={`/host/events/${eventId}/details#representing`} variant="secondary" className="shrink-0">
          {state.action}
        </LinkButton>
      </div>
    </Card>
  );
}

/// The authorization's own status, mapped to what the organiser has to do about it. `Submitted` is the
/// one that is not self-evident: it means filed and queued, not being read — a reviewer opens it as part
/// of the event's review, so nothing is required of the organiser while it sits there.
function stateOf(a: EventAuthorizationView | null) {
  if (a === null) {
    return {
      label: "Action required",
      tone: "text-danger",
      icon: AlertTriangle,
      hint: "This event's organization authorization hasn't been filed yet. It can't be submitted for review without it.",
      action: "Complete representation",
    };
  }
  switch (a.status) {
    case "Approved":
      return {
        label: "Approved", tone: "text-success", icon: ShieldCheck,
        hint: "The event is authorized to represent this organization.",
        action: "View representation",
      };
    case "Rejected":
      return {
        label: "Action required", tone: "text-danger", icon: AlertTriangle,
        hint: "The authorization was rejected. File a corrected one before this event can go live.",
        action: "Manage representation",
      };
    case "ChangesRequested":
      return {
        label: "Action required", tone: "text-warning", icon: AlertTriangle,
        hint: "A reviewer asked for changes to the authorization.",
        action: "Manage representation",
      };
    default:
      return {
        label: "Pending review", tone: "text-warning", icon: Clock,
        hint: "Your organization representation is awaiting verification — a reviewer reads it as part of this event's review.",
        action: "Manage representation",
      };
  }
}
