"use client";

import { useState, useTransition } from "react";
import { Handshake } from "lucide-react";
import { Button, ConfirmDialog, EmptyState, UserCard, useToast } from "@kurx/ui";
import type { AllyConnection } from "@/lib/api";
import { apiErrorMessage } from "@/lib/api";
import { respondAllyRequestAction, revokeAllyAction } from "@/lib/social-actions";
import { MutualDetailToggle } from "@/components/profile/mutual-detail-sheet";

export function AlliesManager({
  initialMine, initialIncoming, initialOutgoing,
}: {
  initialMine: AllyConnection[];
  initialIncoming: AllyConnection[];
  initialOutgoing: AllyConnection[];
}) {
  const [mine, setMine] = useState(initialMine);
  const [incoming, setIncoming] = useState(initialIncoming);
  const [outgoing, setOutgoing] = useState(initialOutgoing);
  const [confirmRemove, setConfirmRemove] = useState<AllyConnection | null>(null);
  const [pending, start] = useTransition();
  const toast = useToast();

  /**
   * Every action here previously ran its server call bare inside the transition and then edited the
   * list unconditionally. Two consequences, both real:
   *
   * 1. **A failure looked like a success.** Decline a request while offline and the card still
   *    vanished; accept one that the server rejected and an ally appeared who was never connected.
   *    The list is now only edited after the call resolves, and a rejection is surfaced.
   * 2. **Nothing was announced.** The whole feedback channel was a card silently leaving the DOM,
   *    which a screen-reader user has no way to perceive. Outcomes now go through the toast live
   *    region — the one Phase 8 built for exactly this and which web had never mounted.
   */
  function run(action: () => Promise<void>, success: string) {
    start(async () => {
      try {
        await action();
        toast(success, "success");
      } catch (err) {
        toast(apiErrorMessage(err), "error");
      }
    });
  }

  function accept(c: AllyConnection) {
    run(async () => {
      await respondAllyRequestAction(c.id, true);
      setIncoming((l) => l.filter((x) => x.id !== c.id));
      setMine((l) => [{ ...c, status: "Accepted" as const }, ...l]);
    }, `${c.other_name} is now an ally.`);
  }
  function decline(c: AllyConnection) {
    run(async () => {
      await respondAllyRequestAction(c.id, false);
      setIncoming((l) => l.filter((x) => x.id !== c.id));
    }, `Declined the request from ${c.other_name}.`);
  }
  function cancelOutgoing(c: AllyConnection) {
    run(async () => {
      await revokeAllyAction(c.id);
      setOutgoing((l) => l.filter((x) => x.id !== c.id));
    }, `Cancelled the request to ${c.other_name}.`);
  }
  function remove(c: AllyConnection) {
    run(async () => {
      await revokeAllyAction(c.id);
      setMine((l) => l.filter((x) => x.id !== c.id));
    }, `Removed ${c.other_name} from your allies.`);
  }

  return (
    <div className="space-y-8">
      {incoming.length > 0 && (
        <section>
          <h2 className="mb-3 font-semibold text-text">Requests</h2>
          <div className="grid gap-3 sm:grid-cols-2">
            {incoming.map((c) => (
              <UserCard
                key={c.id} name={c.other_name} username={c.other_username} avatarSrc={c.other_avatar_key}
                href={c.other_username ? `/u/${c.other_username}` : undefined}
                action={
                  <div className="flex gap-2">
                    {/* Four "Accept" buttons on one page are indistinguishable in a screen reader's
                        control list; the name has to carry whose request it is. */}
                    <Button aria-label={`Accept ${c.other_name}`} disabled={pending} onClick={() => accept(c)}>Accept</Button>
                    <Button aria-label={`Decline ${c.other_name}`} variant="secondary" disabled={pending} onClick={() => decline(c)}>Decline</Button>
                  </div>
                }
              />
            ))}
          </div>
        </section>
      )}

      {outgoing.length > 0 && (
        <section>
          <h2 className="mb-3 font-semibold text-text">Sent</h2>
          <div className="grid gap-3 sm:grid-cols-2">
            {outgoing.map((c) => (
              <UserCard
                key={c.id} name={c.other_name} username={c.other_username} avatarSrc={c.other_avatar_key}
                href={c.other_username ? `/u/${c.other_username}` : undefined}
                action={
                  <Button aria-label={`Cancel the request to ${c.other_name}`} variant="secondary" disabled={pending} onClick={() => cancelOutgoing(c)}>
                    Cancel
                  </Button>
                }
              />
            ))}
          </div>
        </section>
      )}

      <section>
        <h2 className="mb-3 font-semibold text-text">Allies ({mine.length})</h2>
        {mine.length === 0 ? (
          <EmptyState icon={<Handshake size={32} />} title="No allies yet" message="Connect with people you've worked with on Kurx." />
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {mine.map((c) => (
              <UserCard
                key={c.id} name={c.other_name} username={c.other_username} avatarSrc={c.other_avatar_key}
                href={c.other_username ? `/u/${c.other_username}` : undefined}
                subtitle={c.first_shared_event_title ? `Met at ${c.first_shared_event_title}` : undefined}
                action={
                  <div className="flex flex-col items-end gap-1.5">
                    {/* Removing an ally is destructive and irreversible — re-connecting needs the
                        other person to accept again — and it sat one unguarded click away. */}
                    <Button aria-label={`Remove ${c.other_name}`} variant="secondary" disabled={pending} onClick={() => setConfirmRemove(c)}>
                      Remove
                    </Button>
                    <MutualDetailToggle otherUserId={c.other_user_id} otherName={c.other_name} />
                  </div>
                }
              />
            ))}
          </div>
        )}
      </section>

      <ConfirmDialog
        open={confirmRemove !== null}
        title={confirmRemove ? `Remove ${confirmRemove.other_name}?` : ""}
        description="You will no longer be allies. Connecting again needs a new request that they accept."
        confirmLabel="Remove"
        tone="danger"
        onConfirm={() => {
          if (confirmRemove) remove(confirmRemove);
        }}
        onClose={() => setConfirmRemove(null)}
      />
    </div>
  );
}
