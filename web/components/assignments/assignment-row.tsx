"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { Badge, Button } from "@kurx/ui";
import { respondToAssignmentAction } from "@/lib/assignment-actions";
import { formatDateTime } from "@/lib/formatters";
import type { EventAssignment } from "@/lib/api";

/**
 * One invitation to staff an event, with the only controls that can answer it (D-319).
 *
 * The invitee is the sole actor the API permits here — `RespondAsync` refuses the organiser who sent
 * the invite and everyone else — so this row is the entire path from `Invited` to `Accepted`, and the
 * §14.2 go-live gate downstream waits on it.
 */
export function AssignmentRow({ assignment }: { assignment: EventAssignment }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();

  const pending = assignment.status === "invited";
  const role = assignment.custom_role ?? assignment.role;

  function respond(accept: boolean) {
    startTransition(async () => {
      setError(null);
      const result = await respondToAssignmentAction(assignment.id, accept);
      if (result && "error" in result && result.error) { setError(result.error); return; }
      router.refresh();
    });
  }

  return (
    <li className="rounded-lg border border-border p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            {/* A deleted event leaves the invite listed but unlinkable — the slug is what a link needs,
                and losing the row entirely would hide an invite the person can still decline. */}
            {assignment.event_slug ? (
              <Link href={`/e/${assignment.event_slug}`} className="font-medium hover:underline">
                {assignment.event_title}
              </Link>
            ) : (
              <span className="font-medium">{assignment.event_title}</span>
            )}
            <Badge>{role}</Badge>
            {!pending ? <Badge tone="success">Accepted</Badge> : null}
          </div>
          <p className="mt-1 text-sm text-muted">
            {/* Null org name means a self-represented event, which names no organization at all
                (D-268) — so nothing is rendered, not a placeholder. */}
            {assignment.representing_org_name ? `${assignment.representing_org_name} · ` : ""}
            {formatDateTime(assignment.event_starts_at)}
          </p>
          {assignment.notes ? <p className="mt-2 text-sm text-muted">{assignment.notes}</p> : null}
        </div>

        {pending ? (
          <div className="flex shrink-0 items-center gap-2">
            <Button disabled={isPending} onClick={() => respond(true)}>Accept</Button>
            <Button variant="ghost" disabled={isPending} onClick={() => respond(false)}>Decline</Button>
          </div>
        ) : null}
      </div>

      {error ? (
        <p role="alert" className="mt-3 rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
          {error}
        </p>
      ) : null}
    </li>
  );
}
