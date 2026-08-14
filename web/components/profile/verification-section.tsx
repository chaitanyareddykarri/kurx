import Link from "next/link";
import {
  AlertTriangle, CheckCircle2, CircleDashed, Clock, Coins, History, IdCard, Landmark,
  Mail, Phone, ShieldCheck, UserCheck, Wallet, XCircle,
} from "lucide-react";
import { Badge, Card } from "@kurx/ui";

/**
 * Verification — the **owner-only** block on a profile.
 *
 * **Who sees this is the whole design.** The public half of verification is a short list of proved
 * signals (see `TrustPanel`); this is everything that half deliberately withholds — which components
 * are pending, which were rejected, and what each one unlocks. It renders only when the viewer is
 * the profile's owner, because a component's *status* is as private as its value: "PAN rejected"
 * shown to a stranger is a statement about someone's financial identity they never agreed to make.
 *
 * **The money model is user-level.** A person's own identity and bank verification unlock that
 * person's wallet and payouts — an organization verifies the *user for events* and holds nothing.
 * That is the product model, and this screen states it that way.
 *
 * Every flag comes from `TrustService` via `/v1/me` and is never re-derived here: the same values
 * gate the money path server-side, so a local guess that disagreed would either promise a sale the
 * server refuses or hide one it would allow.
 */
export type ComponentState = "verified" | "pending" | "rejected" | "not_started";

export type IdentitySnapshot = {
  status: string;
  govt_id_last4: string | null;
  pan_last4: string | null;
  bank_last4: string | null;
  govt_id_status: string;
  pan_status: string;
  bank_status: string;
  penny_drop_status: string;
  bank_name_match: string;
  bank_verified_at?: string | null;
};

export type HistoryEntry = {
  component: string;
  decision: string;
  reason_code: string | null;
  notes: string | null;
  created_at: string;
};

/**
 * Maps a server component status onto the four display states.
 *
 * **This used to infer state from whether a masked value was present**, because per-component status
 * did not exist on the wire — so "submitted and rejected" and "never attempted" were the same
 * picture. The server now reports each component, and this only translates vocabulary.
 */
function stateOf(status: string): ComponentState {
  switch (status.toLowerCase()) {
    case "approved":
      return "verified";
    case "rejected":
    case "changesrequested":
    case "expired":
    case "revoked":
      return "rejected";
    case "submitted":
    case "underreview":
      return "pending";
    default:
      return "not_started";
  }
}

/** Penny drop has its own vocabulary — it is a bank test, not a review decision. */
function pennyDropState(status: string): ComponentState {
  switch (status.toLowerCase()) {
    case "passed":
      return "verified";
    case "failed":
      return "rejected";
    case "pending":
      return "pending";
    default:
      return "not_started";
  }
}

const STATE_UI: Record<
  ComponentState,
  { label: string; tone: "teal" | "warning" | "danger" | "muted"; icon: React.ReactNode }
> = {
  verified: { label: "Verified", tone: "teal", icon: <CheckCircle2 size={13} aria-hidden /> },
  pending: { label: "Pending", tone: "warning", icon: <Clock size={13} aria-hidden /> },
  rejected: { label: "Needs action", tone: "danger", icon: <XCircle size={13} aria-hidden /> },
  not_started: { label: "Not started", tone: "muted", icon: <CircleDashed size={13} aria-hidden /> },
};

function Row({
  icon, label, description, state, actionHref, actionLabel,
}: {
  icon: React.ReactNode;
  label: string;
  description: string;
  state: ComponentState;
  actionHref?: string;
  actionLabel?: string;
}) {
  const ui = STATE_UI[state];
  return (
    <li className="flex flex-wrap items-start justify-between gap-md rounded-md border border-border bg-elevated p-md">
      <div className="flex min-w-0 items-start gap-md">
        <span className="mt-0.5 shrink-0 text-muted">{icon}</span>
        <span className="min-w-0">
          <span className="flex flex-wrap items-center gap-sm">
            <span className="text-body font-medium text-text">{label}</span>
            <Badge tone={ui.tone} icon={ui.icon}>{ui.label}</Badge>
          </span>
          <span className="mt-0.5 block text-sm text-muted">{description}</span>
        </span>
      </div>
      {/* A verified component offers no action: there is nothing left to do, and a live button there
          invites a resubmission that would only reset a passed check. */}
      {state !== "verified" && actionHref && actionLabel ? (
        <Link
          href={actionHref}
          className="inline-flex min-h-11 shrink-0 items-center rounded-md border border-border px-3 text-sm font-medium text-text transition duration-fast hover:bg-surface"
        >
          {actionLabel}
        </Link>
      ) : null}
    </li>
  );
}

function Capability({
  icon, label, unlocked, requirement,
}: {
  icon: React.ReactNode;
  label: string;
  unlocked: boolean;
  requirement: string;
}) {
  return (
    <li className="flex flex-wrap items-start justify-between gap-md border-b border-border py-md last:border-0 last:pb-0">
      <span className="flex min-w-0 items-start gap-md">
        <span className={`mt-0.5 shrink-0 ${unlocked ? "text-teal" : "text-muted"}`}>{icon}</span>
        <span className="min-w-0">
          <span className="block text-body font-medium text-text">{label}</span>
          {/* Shown whether unlocked or not: someone who has it should still see what keeps it, and
              someone who lost it needs the same sentence back. */}
          <span className="block text-sm text-muted">{requirement}</span>
        </span>
      </span>
      <Badge tone={unlocked ? "teal" : "muted"} icon={unlocked ? <CheckCircle2 size={13} aria-hidden /> : <CircleDashed size={13} aria-hidden />}>
        {unlocked ? "Unlocked" : "Locked"}
      </Badge>
    </li>
  );
}

export function VerificationSection({
  identity,
  history,
  emailVerified,
  canOrganizePaid,
  canReceivePayout,
}: {
  /** Null when the identity read failed — the section still renders, showing everything as unstarted
   *  would be a lie, so the components fall back to the aggregate "not started" and the copy holds. */
  identity: IdentitySnapshot | null;
  /** Newest first. Empty when nothing has ever been submitted — an empty trail, not an error. */
  history: readonly HistoryEntry[];
  emailVerified: boolean;
  canOrganizePaid: boolean;
  canReceivePayout: boolean;
}) {
  const govt = stateOf(identity?.govt_id_status ?? "NotStarted");
  const pan = stateOf(identity?.pan_status ?? "NotStarted");
  const bank = stateOf(identity?.bank_status ?? "NotStarted");
  const pennyDrop = pennyDropState(identity?.penny_drop_status ?? "NotStarted");
  const nameMatch = (identity?.bank_name_match ?? "NotChecked").toLowerCase();

  // Mirrors TrustService exactly: identity is PAN *or* government ID, never both.
  const identityVerified = govt === "verified" || pan === "verified";

  return (
    <div className="space-y-lg">
      <Card>
        <div className="flex flex-wrap items-start justify-between gap-md">
          <div>
            <h2 className="text-lg font-semibold text-text">Verification</h2>
            <p className="mt-1 max-w-2xl text-sm text-muted">
              Only you can see this. Only the last four digits of anything you submit are stored — the
              full value goes to the verification provider and is never kept by Kurx.
            </p>
          </div>
          {identityVerified ? (
            <Badge tone="teal" icon={<ShieldCheck size={13} aria-hidden />}>Identity verified</Badge>
          ) : null}
        </div>

        <ul className="mt-lg space-y-md">
          <Row
            icon={<Phone size={17} aria-hidden />}
            label="Phone"
            description="Verified when you signed in — every account is created by a one-time code."
            // True by construction: an account only exists after a successful OTP login, which is
            // why the backend states it rather than storing a redundant column.
            state="verified"
          />
          <Row
            icon={<Mail size={17} aria-hidden />}
            label="Email"
            description={emailVerified ? "Confirmed by one-time code." : "Used for recovery and receipts."}
            state={emailVerified ? "verified" : "not_started"}
            actionHref="/settings/account"
            actionLabel="Verify"
          />
          <Row
            icon={<IdCard size={17} aria-hidden />}
            label="Government ID"
            description={
              identity?.govt_id_last4
                ? `Confirmed · ending ${identity.govt_id_last4}`
                : "DigiLocker, Aadhaar offline, passport or driving licence."
            }
            state={govt}
            actionHref="/settings/identity"
            actionLabel="Verify"
          />
          <Row
            icon={<IdCard size={17} aria-hidden />}
            label="PAN"
            description={identity?.pan_last4 ? `Confirmed · ending ${identity.pan_last4}` : "Required to take payments."}
            state={pan}
            actionHref="/settings/identity"
            actionLabel="Verify"
          />
          <Row
            icon={<Landmark size={17} aria-hidden />}
            label="Bank account"
            description={identity?.bank_last4 ? `Account ending ${identity.bank_last4}` : "Where your payouts are settled."}
            state={bank}
            actionHref="/settings/identity"
            actionLabel="Add"
          />
          <Row
            icon={<Coins size={17} aria-hidden />}
            label="Penny drop"
            description={
              pennyDrop === "verified"
                ? "A ₹1 credit reached the account — it exists and accepts deposits."
                : pennyDrop === "rejected"
                  ? "The test credit did not land. Check the account number and IFSC."
                  : "A ₹1 test credit proves the account is real and live."
            }
            state={pennyDrop}
            actionHref="/settings/identity"
            actionLabel="Retry"
          />
          <Row
            icon={<UserCheck size={17} aria-hidden />}
            label="Account holder"
            description={
              nameMatch === "match"
                ? "The bank's registered holder name matched yours."
                : nameMatch === "partialmatch"
                  ? "The holder name partly matched — a review may be needed."
                  : nameMatch === "mismatch"
                    ? "The holder name did not match. Payouts must settle to an account in your name."
                    : "Checked against the bank's registered holder name when you add an account."
            }
            state={
              nameMatch === "match"
                ? "verified"
                : nameMatch === "mismatch"
                  ? "rejected"
                  : nameMatch === "partialmatch"
                    ? "pending"
                    : "not_started"
            }
            actionHref="/settings/identity"
            actionLabel="Fix"
          />
        </ul>
      </Card>

      <Card>
        <h2 className="text-lg font-semibold text-text">What this unlocks</h2>
        <p className="mt-1 text-sm text-muted">
          Checked live on every request — a change takes effect on your next action, not your next
          sign-in.
        </p>
        <ul className="mt-lg">
          <Capability
            icon={<CheckCircle2 size={17} aria-hidden />}
            label="Attend events, chat, post, run free events"
            unlocked
            requirement="Available to every account."
          />
          <Capability
            icon={<ShieldCheck size={17} aria-hidden />}
            label="Create and sell paid events"
            unlocked={canOrganizePaid}
            requirement="Needs your identity and your bank account verified."
          />
          <Capability
            icon={<Wallet size={17} aria-hidden />}
            label="Your wallet and payouts"
            unlocked={canReceivePayout}
            requirement="Needs your bank account verified. Earnings settle to you."
          />
        </ul>

        {!canOrganizePaid ? (
          <p className="mt-lg flex items-start gap-sm rounded-md border border-warning/50 bg-elevated p-md text-sm text-muted">
            <AlertTriangle size={16} aria-hidden className="mt-0.5 shrink-0 text-warning" />
            <span>
              Until this is complete, buyers cannot pay for tickets on your events — checkout refuses
              the payment rather than failing quietly.
            </span>
          </p>
        ) : null}
      </Card>

      {history.length > 0 ? (
        <Card>
          <h2 className="flex items-center gap-sm text-lg font-semibold text-text">
            <History size={18} aria-hidden className="text-muted" />
            Verification history
          </h2>
          <p className="mt-1 text-sm text-muted">
            Every decision made about your identity, newest first.
          </p>
          <ol className="mt-lg space-y-md">
            {history.map((h, i) => {
              const approved = h.decision === "approve" || h.decision === "approved";
              return (
                <li
                  key={`${h.created_at}-${h.component}-${i}`}
                  className="flex flex-wrap items-start justify-between gap-md rounded-md border border-border bg-elevated p-md"
                >
                  <span className="min-w-0">
                    <span className="flex flex-wrap items-center gap-sm">
                      <span className="text-body font-medium capitalize text-text">
                        {h.component.replace(/_/g, " ")}
                      </span>
                      <Badge
                        tone={approved ? "teal" : "danger"}
                        icon={approved ? <CheckCircle2 size={13} aria-hidden /> : <XCircle size={13} aria-hidden />}
                      >
                        {approved ? "Approved" : "Rejected"}
                      </Badge>
                    </span>
                    {h.notes ? <span className="mt-0.5 block text-sm text-muted">{h.notes}</span> : null}
                  </span>
                  <time dateTime={h.created_at} className="shrink-0 text-sm text-muted">
                    {new Date(h.created_at).toLocaleDateString(undefined, {
                      year: "numeric", month: "short", day: "numeric",
                    })}
                  </time>
                </li>
              );
            })}
          </ol>
        </Card>
      ) : null}
    </div>
  );
}
