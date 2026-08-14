"use client";

import { useState, useTransition } from "react";
import { Sheet, Spinner } from "@kurx/ui";
import type { MutualDetail } from "@/lib/api";
import { getMutualDetailAction } from "@/lib/social-actions";

/** "Shared history" trigger that fetches shared events/orgs on demand (not prefetched per row — a
 * whole list of these would otherwise fire one call per card before anyone opens any of them). */
export function MutualDetailToggle({ otherUserId, otherName }: { otherUserId: string; otherName: string }) {
  const [open, setOpen] = useState(false);
  const [detail, setDetail] = useState<MutualDetail | null>(null);
  const [pending, start] = useTransition();

  function openSheet() {
    setOpen(true);
    if (!detail) start(async () => setDetail(await getMutualDetailAction(otherUserId)));
  }

  return (
    <>
      <button type="button" onClick={openSheet} className="text-xs font-semibold text-accent-text underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
        Shared history
      </button>
      <Sheet open={open} onClose={() => setOpen(false)} title={`You and ${otherName}`}>
        {pending || !detail ? (
          // The sheet opens instantly and its content arrives later; a bare "Loading…" paragraph is
          // drawn, never announced, so a screen-reader user was told nothing between opening the
          // sheet and the content landing.
          <p className="flex items-center gap-2 text-muted">
            <Spinner size={16} label={`Loading your shared history with ${otherName}`} />
            <span aria-hidden>Loading…</span>
          </p>
        ) : (
          <div className="space-y-5">
            {/* Why they know each other (D-226). Leads the sheet because it is the answer people
                actually came for; the raw shared events/orgs below are the evidence for it.
                `label` is already written from the viewer's perspective — rendered verbatim. */}
            {detail.relationships.length > 0 && (
              <div>
                <h3 className="mb-2 font-semibold text-text">How you know each other</h3>
                <ul className="space-y-1.5">
                  {detail.relationships.map((r) => (
                    <li key={r.type} className="flex flex-wrap items-baseline gap-x-2">
                      <span className="text-text">{r.label}</span>
                      {r.count > 1 && <span className="text-xs text-muted">×{r.count}</span>}
                      {r.context && <span className="text-xs text-muted">· {r.context}</span>}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {detail.shared_events.length > 0 && (
              <div>
                <h3 className="mb-2 font-semibold text-text">Shared events</h3>
                <ul className="space-y-1.5">
                  {detail.shared_events.map((e) => (
                    <li key={e.id}>
                      <a href={`/e/${e.slug}`} className="text-accent-text underline-offset-2 hover:underline">{e.title}</a>
                      <span className="ml-1.5 text-muted">{new Date(e.starts_at).toLocaleDateString("en-IN", { year: "numeric", month: "short" })}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {detail.shared_orgs.length > 0 && (
              <div>
                <h3 className="mb-2 font-semibold text-text">Shared organizations</h3>
                <ul className="space-y-1.5">
                  {detail.shared_orgs.map((o) => (
                    <li key={o.id}><a href={`/o/${o.slug}`} className="text-accent-text underline-offset-2 hover:underline">{o.name}</a></li>
                  ))}
                </ul>
              </div>
            )}
            {detail.shared_events.length === 0 && detail.shared_orgs.length === 0
              && detail.relationships.length === 0 && (
              <p className="text-muted">No shared public events or organizations found.</p>
            )}
          </div>
        )}
      </Sheet>
    </>
  );
}
