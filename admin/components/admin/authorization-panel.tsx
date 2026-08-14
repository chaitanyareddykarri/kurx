"use client";

import { useEffect, useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { FileCheck2, FileWarning, ExternalLink } from "lucide-react";
import { useToast } from "@kurx/ui";
import { reviewEventAuthorizationAction } from "@/lib/admin-actions";
import type { EventAuthorization } from "@/lib/api";

/** The same closed vocabulary the event review uses (D-266 M4) — a rejection here is the same KIND of
 *  fact, and a second reason list would drift the first time either gained a value. */
const REASONS = [
  ["MisrepresentedAffiliation", "Misrepresented affiliation"],
  ["UnverifiedOrganiser", "Cannot verify the organiser speaks for this institution"],
  ["Incomplete", "Incomplete — the letter does not evidence the claim"],
  ["ProhibitedContent", "Prohibited content"],
  ["Other", "Other — explain in notes"],
] as const;

/**
 * Consumer mail domains. An institutional authorization sent from one is **not** invalid — plenty of real
 * colleges run on free mail — so this never refuses anything. It is a prompt to look harder: the letter is
 * the evidence, and an address anyone can register corroborates nothing about who signed it.
 */
const FREE_MAIL = new Set([
  "gmail.com", "googlemail.com", "yahoo.com", "yahoo.co.in", "outlook.com", "hotmail.com",
  "live.com", "aol.com", "icloud.com", "protonmail.com", "proton.me", "rediffmail.com",
  "mail.com", "yandex.com", "zoho.com",
]);

const isFreeMail = (email: string) => FREE_MAIL.has(email.split("@").pop()?.toLowerCase() ?? "");

const STATUS_TONE: Record<string, string> = {
  Approved: "border-success/40 text-success",
  Rejected: "border-danger/40 text-danger",
  ChangesRequested: "border-warning/40 text-warning",
  Submitted: "border-border text-muted",
};

function Submit({ label, tone }: { label: string; tone: "approve" | "reject" | "neutral" }) {
  const { pending } = useFormStatus();
  const cls =
    tone === "approve" ? "border-success/40 text-success hover:bg-success/10"
    : tone === "reject" ? "border-danger/40 text-danger hover:bg-danger/10"
    : "border-border text-text hover:bg-elevated";
  return (
    <button type="submit" disabled={pending}
      className={`h-8 rounded-md border px-3 text-xs font-semibold disabled:opacity-50 ${cls}`}>
      {pending ? "…" : label}
    </button>
  );
}

/**
 * D-266 M5/M7 — the institutional authorization panel.
 *
 * **This is not `IEventAuthority`.** It answers whether the organization the event *represents* consented
 * to being represented by it — reviewed evidence — not who may act on the event. A reviewer approving here
 * is not approving the event; that is the separate review decision below it.
 */
export function AuthorizationPanel({ eventId, authorization }: { eventId: string; authorization: EventAuthorization | null }) {
  const [state, formAction] = useFormState(reviewEventAuthorizationAction.bind(null, eventId), null);
  const [open, setOpen] = useState<string | null>(null);
  const toast = useToast();

  useEffect(() => {
    if (state && "error" in state) toast(String(state.error), "error");
    if (state && "ok" in state) { toast("Authorization decision recorded.", "success"); setOpen(null); }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  if (!authorization) {
    return (
      <p className="mt-3 flex items-center gap-1.5 text-xs text-muted">
        <FileWarning size={12} />
        No institutional authorization filed. A Public event representing an organization cannot publish without one.
      </p>
    );
  }

  const a = authorization;
  const needsForm = open === "reject" || open === "request_changes";

  return (
    <div className="mt-3 rounded-md border border-border bg-elevated p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h4 className="flex items-center gap-1.5 text-xs font-semibold text-text">
          <FileCheck2 size={13} /> Institutional authorization
        </h4>
        <span className={`rounded border px-1.5 py-0.5 text-[10px] font-semibold ${STATUS_TONE[a.status] ?? "border-border text-muted"}`}>
          {a.status}
        </span>
      </div>

      <dl className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
        <div><dt className="text-muted">Signatory</dt><dd className="text-text">{a.head_name}</dd></div>
        <div><dt className="text-muted">Designation</dt><dd className="text-text">{a.head_designation}</dd></div>
        <div>
          <dt className="text-muted">Role</dt>
          <dd className="text-text">
            {a.representative_role === "Other" && a.representative_role_other
              ? `${a.representative_role_other} (Other)`
              : a.representative_role || "—"}
          </dd>
        </div>
        <div>
          <dt className="text-muted">Official email</dt>
          <dd className="break-all text-text">{a.official_email}</dd>
        </div>
        <div><dt className="text-muted">Official phone</dt><dd className="text-text">{a.official_phone ?? "—"}</dd></div>
        {/* A LINK, never a grant: the named account holds no authority over this event (D-269). Shown so a
            reviewer can open the profile and see the signatory is a real, known person. */}
        <div>
          <dt className="text-muted">Kurx account</dt>
          <dd className="text-text">
            {a.representative_user_id ? (
              <a href={`/users/${a.representative_user_id}`} className="text-accent-text hover:underline">
                {a.representative_username ? `@${a.representative_username}` : "View profile"}
              </a>
            ) : "Not linked"}
          </dd>
        </div>
        {a.reviewed_at ? (
          <div className="col-span-2">
            <dt className="text-muted">Decision</dt>
            <dd className="text-text">
              {a.reviewer_name ?? "A reviewer"} · {new Date(a.reviewed_at).toLocaleString("en-IN")}
            </dd>
          </div>
        ) : null}
      </dl>

      {/* Advice, never a gate: a free-mail address does not make the authorization invalid, it makes the
          letter carry the whole claim. Refusing here would reject real colleges. */}
      {isFreeMail(a.official_email) ? (
        <p className="mt-2 flex items-start gap-1.5 rounded border border-warning/40 bg-warning/10 px-2 py-1.5 text-[11px] text-text">
          <FileWarning size={12} className="mt-0.5 shrink-0 text-warning" />
          <span>
            Not an institutional email domain. That is allowed, but the letterhead is then the only evidence
            of who signed — check it against the organization, and consider calling the number above.
          </span>
        </p>
      ) : null}

      {/* Presigned, short-lived URLs — the console never sees a storage key. */}
      <div className="mt-2 flex flex-wrap gap-2">
        {a.letterhead_url ? <DocLink href={a.letterhead_url} label="Letterhead" /> : null}
        {a.signature_url ? <DocLink href={a.signature_url} label="Signature" /> : null}
        {a.supporting_document_urls.map((u, i) => <DocLink key={u} href={u} label={`Supporting ${i + 1}`} />)}
      </div>

      {a.reason_code || a.notes ? (
        <p className="mt-2 text-xs text-muted">
          {a.reason_code ? <span className="font-semibold text-text">{a.reason_code}</span> : null}
          {a.notes ? <span> · {a.notes}</span> : null}
        </p>
      ) : null}

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <form action={formAction} className="inline">
          <input type="hidden" name="decision" value="approve" />
          <Submit label="Approve authorization" tone="approve" />
        </form>
        {(["reject", "request_changes"] as const).map((d) => (
          <button key={d} type="button" onClick={() => setOpen(open === d ? null : d)}
            className={`h-8 rounded-md border px-3 text-xs font-semibold ${
              d === "reject" ? "border-danger/40 text-danger hover:bg-danger/10" : "border-border text-text hover:bg-elevated"}`}>
            {d === "reject" ? "Reject" : "Request changes"}
          </button>
        ))}
      </div>

      {needsForm ? (
        <form action={formAction} className="mt-2 space-y-2 rounded-md border border-border bg-surface p-3">
          <input type="hidden" name="decision" value={open!} />
          {open === "reject" ? (
            <label className="block text-xs text-muted">
              Reason
              <select name="reasonCode" required defaultValue=""
                className="mt-1 h-8 w-full rounded-md border border-border-strong bg-elevated px-2 text-xs text-text">
                <option value="" disabled>Select a reason…</option>
                {REASONS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>
          ) : null}
          <label className="block text-xs text-muted">
            Notes {open === "request_changes" ? <span className="text-danger">*</span> : "(optional)"}
            <textarea name="notes" rows={3} required={open === "request_changes"}
              placeholder={open === "request_changes"
                ? "What the organiser needs to change about the authorization."
                : "Context for the audit trail."}
              className="mt-1 w-full rounded-md border border-border-strong bg-elevated px-2 py-1 text-xs text-text" />
          </label>
          <div className="flex gap-2">
            <Submit label={open === "reject" ? "Confirm reject" : "Send request"}
              tone={open === "reject" ? "reject" : "neutral"} />
            <button type="button" onClick={() => setOpen(null)}
              className="h-8 rounded-md border border-border px-3 text-xs text-muted hover:bg-elevated">
              Cancel
            </button>
          </div>
        </form>
      ) : null}
    </div>
  );
}

function DocLink({ href, label }: { href: string; label: string }) {
  return (
    <a href={href} target="_blank" rel="noreferrer"
      className="inline-flex items-center gap-1 rounded border border-border px-2 py-1 text-[11px] text-text hover:bg-surface">
      {label} <ExternalLink size={10} />
    </a>
  );
}
