"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { AtSign, Search } from "lucide-react";
import { Button, controlClass } from "@kurx/ui";
import { searchUsersAction, inviteByUsernameAction } from "@/lib/invitation-actions";

const inputClass = controlClass;

type Found = { id: string; name: string; username: string; avatar_key: string | null };

/**
 * D-266 M6 (D9 Method A) — invite an existing Kurx user by username.
 *
 * **Needs no email address and no phone number** — that is the whole point of Method A. The invitee is
 * already here, so they get an in-app notification and accept or decline in place; nothing forwardable is
 * created.
 *
 * The search reads the same index as the public profile search, so a user who has not made their username
 * discoverable will not appear here. That is deliberate, not a gap: they are invited by email or phone
 * through the form beside this one.
 */
export function UsernameInvite({ eventId }: { eventId: string }) {
  const router = useRouter();
  const [q, setQ] = useState("");
  const [results, setResults] = useState<Found[]>([]);
  const [searching, startSearch] = useTransition();
  const [inviting, startInvite] = useTransition();
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);
  const seq = useRef(0);

  // Debounced: a keystroke-per-request search would hammer the endpoint and, worse, let a slow earlier
  // response overwrite a newer one. The sequence guard below is what actually prevents that.
  useEffect(() => {
    const term = q.trim();
    if (term.length < 2) { setResults([]); return; }
    const mine = ++seq.current;
    const t = setTimeout(() => {
      startSearch(async () => {
        const res = await searchUsersAction(term);
        // Drop a response that a newer keystroke has already superseded.
        if (mine !== seq.current) return;
        setResults("error" in res ? [] : res.users);
      });
    }, 250);
    return () => clearTimeout(t);
  }, [q]);

  function invite(u: Found) {
    startInvite(async () => {
      setMessage(null);
      const res = await inviteByUsernameAction(eventId, u.username);
      if ("error" in res) { setMessage({ ok: false, text: res.error ?? "Could not invite." }); return; }
      setMessage({ ok: true, text: `Invited @${u.username}.` });
      setQ("");
      setResults([]);
      router.refresh();
    });
  }

  return (
    <div className="space-y-3">
      <label className="block text-sm font-medium text-text" htmlFor="u-search">
        Invite someone already on Kurx
      </label>
      <div className="relative sm:w-96">
        <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
        <input id="u-search" value={q} onChange={(e) => setQ(e.target.value)}
          placeholder="Search by name or username" className={`${inputClass} pl-9`} />
      </div>

      {q.trim().length >= 2 ? (
        <ul className="max-w-md divide-y divide-border rounded-md border border-border">
          {searching && results.length === 0 ? (
            <li className="px-3 py-2 text-sm text-muted">Searching…</li>
          ) : results.length === 0 ? (
            <li className="px-3 py-2 text-sm text-muted">
              Nobody found. They may not have a public username — invite them by email or phone instead.
            </li>
          ) : (
            results.map((u) => (
              <li key={u.id} className="flex items-center justify-between gap-3 px-3 py-2">
                <span className="min-w-0">
                  <span className="block truncate text-sm text-text">{u.name || u.username}</span>
                  <span className="flex items-center gap-0.5 text-xs text-muted">
                    <AtSign size={11} />{u.username}
                  </span>
                </span>
                <Button variant="secondary" disabled={inviting} onClick={() => invite(u)}>Invite</Button>
              </li>
            ))
          )}
        </ul>
      ) : null}

      {message ? (
        <p role="status" className={`text-sm ${message.ok ? "text-success" : "text-danger"}`}>{message.text}</p>
      ) : null}

      <p className="text-xs text-muted">
        They&apos;ll get a notification in Kurx and can accept or decline. No email or phone number needed.
      </p>
    </div>
  );
}
