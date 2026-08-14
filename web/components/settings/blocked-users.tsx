"use client";

import { useState, useTransition } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { unblockUserAction } from "@/lib/account-actions";
import type { BlockedUser } from "@/lib/account-api";

/// Blocked users (D-263). Unblocking is the only action here — blocking happens where you encounter
/// the person (a post, a profile, a conversation), not from a list of people you have never met.
export function BlockedUsers({ initial }: { initial: BlockedUser[] }) {
  const [rows, setRows] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [, startTransition] = useTransition();

  function unblock(userId: string) {
    setError(null);
    const before = rows;
    setRows((cur) => cur.filter((r) => r.user_id !== userId));
    startTransition(async () => {
      const result = await unblockUserAction(userId);
      if ("error" in result) {
        setRows(before);
        setError(result.error);
      }
    });
  }

  return (
    <Card>
      <h2 className="text-lg font-semibold text-text">Blocked accounts</h2>
      <p className="mt-1 text-sm text-muted">
        Blocking hides you from each other completely — posts, comments and messages, in both
        directions. Neither of you can start a conversation with the other.
      </p>

      {error ? <p className="mt-3 text-sm text-danger">{error}</p> : null}

      {rows.length === 0 ? (
        <p className="mt-4 text-sm text-muted">You haven&apos;t blocked anyone.</p>
      ) : (
        <ul className="mt-4 divide-y divide-border">
          {rows.map((u) => (
            <li key={u.user_id} className="flex items-center justify-between gap-4 py-3">
              <div className="min-w-0">
                <p className="truncate text-sm text-text">{u.name}</p>
                {u.username ? <p className="truncate text-xs text-muted">@{u.username}</p> : null}
              </div>
              <Button variant="secondary" onClick={() => unblock(u.user_id)}>Unblock</Button>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
