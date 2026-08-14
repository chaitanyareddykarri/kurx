"use client";

import { useEffect, useState, useTransition } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { useRouter } from "next/navigation";
import { Link2, Copy, Ban, KeyRound } from "lucide-react";
import { Button, ConfirmDialog, controlClass } from "@kurx/ui";
import { createInviteLinkAction, revokeInviteLinkAction } from "@/lib/event-actions";
import { withUtcTimes } from "@/lib/event-wizard";
import type { InviteLink } from "@/lib/api";

const inputClass = controlClass;

function Submit() {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Creating…" : "Create link"}</Button>;
}

/**
 * D-266 M6/M8 (D9 Method B) — shareable invite links.
 *
 * `Invite Only` is ONE registration policy; this is one of its two delivery methods. A link grants
 * **permission to register and nothing more** — an invited guest of a paid event still pays.
 *
 * Seat accounting is the server's: `used_count` is claimed in SQL under a conditional update, so what is
 * shown here is a report, never a number this component computes or predicts.
 */
export function InviteLinks({ eventId, links }: { eventId: string; links: InviteLink[] }) {
  const router = useRouter();
  const [state, formAction] = useFormState(createInviteLinkAction.bind(null, eventId), null);
  const [isPending, startTransition] = useTransition();
  const [copied, setCopied] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirming, setConfirming] = useState<string | null>(null);

  useEffect(() => {
    if (state && "error" in state) setError(String(state.error));
    if (state && "ok" in state) { setError(null); router.refresh(); }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  function copy(token: string) {
    const url = `${window.location.origin}/i/${token}`;
    navigator.clipboard.writeText(url).then(() => {
      setCopied(token);
      setTimeout(() => setCopied(null), 2000);
    });
  }

  function revoke(linkId: string) {
    startTransition(async () => {
      const res = await revokeInviteLinkAction(eventId, linkId);
      if ("error" in res) { setError(res.error ?? "Could not revoke that link."); return; }
      setConfirming(null);
      router.refresh();
    });
  }

  return (
    <div className="space-y-5">
      <form action={(fd) => formAction(withUtcTimes(fd, ["expiresAt"]))} className="grid gap-3 sm:grid-cols-2">
        <label className="text-sm text-muted">
          Seats (blank = unlimited)
          <input name="maxSeats" type="number" min={1} className={inputClass} placeholder="e.g. 50" />
        </label>
        <label className="text-sm text-muted">
          Expires (blank = never)
          <input name="expiresAt" type="datetime-local" className={inputClass} />
        </label>
        <label className="text-sm text-muted">
          Passcode (optional)
          <input name="passcode" className={inputClass} placeholder="A second factor on top of the link" />
        </label>
        <label className="flex items-end gap-2 text-sm text-text">
          <input name="singleUse" type="checkbox" className="h-4 w-4 accent-accent" />
          Single use — dies after one registration
        </label>
        <div className="sm:col-span-2"><Submit /></div>
      </form>

      {error ? (
        <p role="alert" className="rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
          {error}
        </p>
      ) : null}

      {links.length === 0 ? (
        <p className="text-sm text-muted">No invite links yet.</p>
      ) : (
        <ul className="space-y-2">
          {links.map((l) => {
            const revoked = l.status === "Revoked";
            const exhausted = l.max_seats !== null && l.used_count >= l.max_seats;
            const expired = l.expires_at !== null && new Date(l.expires_at) <= new Date();
            return (
              <li key={l.id}
                className={`flex flex-wrap items-center gap-3 rounded-lg border p-3 ${
                  revoked || exhausted || expired ? "border-border opacity-60" : "border-border"}`}>
                <Link2 size={14} className="shrink-0 text-muted" />
                <code className="min-w-0 flex-1 truncate text-xs text-text">/i/{l.token}</code>

                <span className="text-xs text-muted">
                  {l.used_count}
                  {l.max_seats !== null ? `/${l.max_seats}` : ""} used
                </span>
                {l.single_use ? <span className="text-[10px] font-semibold text-muted">SINGLE USE</span> : null}
                {l.requires_passcode ? (
                  <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-muted">
                    <KeyRound size={10} /> PASSCODE
                  </span>
                ) : null}
                {revoked ? <span className="text-[10px] font-semibold text-danger">REVOKED</span>
                  : expired ? <span className="text-[10px] font-semibold text-warning">EXPIRED</span>
                  : exhausted ? <span className="text-[10px] font-semibold text-warning">FULL</span>
                  : null}

                <span className="flex gap-1">
                  <button type="button" onClick={() => copy(l.token)} disabled={revoked}
                    className="inline-flex h-8 items-center gap-1 rounded-md border border-border px-2 text-xs text-text hover:bg-elevated disabled:opacity-50">
                    <Copy size={12} /> {copied === l.token ? "Copied" : "Copy"}
                  </button>
                  {revoked ? null : (
                    // Revoking kills a link that may already be circulating; it fired on one click.
                    <button type="button" onClick={() => setConfirming(l.id)} disabled={isPending}
                      className="inline-flex min-h-11 items-center gap-1 rounded-md border border-danger/40 px-2 text-xs text-danger transition duration-fast hover:bg-danger/10 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-50">
                      <Ban size={12} aria-hidden /> Revoke
                    </button>
                  )}
                </span>
              </li>
            );
          })}
        </ul>
      )}

      <ConfirmDialog
        open={confirming !== null}
        title="Revoke this invite link?"
        description="Anyone still holding the link will no longer be able to use it. Links already redeemed are unaffected."
        confirmLabel="Revoke"
        tone="danger"
        onConfirm={() => { if (confirming) revoke(confirming); }}
        onClose={() => setConfirming(null)}
      />

      <p className="text-xs text-muted">
        Revoking stops new arrivals. It never removes anyone who has already registered.
      </p>
    </div>
  );
}
