"use client";

import { useState, useTransition } from "react";
import { Badge, Card } from "@kurx/ui";
import { updateNotificationPreferenceAction } from "@/lib/account-actions";
import { CATEGORY_LABEL, type NotificationPreference } from "@/lib/account-api";

/// Notification preferences (D-263).
///
/// Optimistic per switch, reverted on failure. The alternative — waiting for the round trip before the
/// toggle moves — makes a grid of 48 switches feel broken on a slow connection.
///
/// A `locked` category renders disabled. That is presentation only: the server refuses to disable it
/// regardless of what is sent, because whoever is sending the PATCH is exactly who the rule protects
/// against.

const CHANNELS = [
  { key: "inApp" as const, field: "in_app" as const, label: "In-app" },
  { key: "push" as const, field: "push" as const, label: "Push" },
  { key: "email" as const, field: "email" as const, label: "Email" },
  { key: "whatsApp" as const, field: "whats_app" as const, label: "WhatsApp" }
];

export function NotificationPreferences({ initial }: { initial: NotificationPreference[] }) {
  const [rows, setRows] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [, startTransition] = useTransition();

  function toggle(category: string, channel: (typeof CHANNELS)[number], next: boolean) {
    setError(null);
    const before = rows;
    setRows((cur) =>
      cur.map((r) => (r.category === category ? { ...r, [channel.field]: next } : r))
    );

    startTransition(async () => {
      const result = await updateNotificationPreferenceAction(category, channel.key, next);
      if ("error" in result) {
        setRows(before);
        setError(result.error);
      }
    });
  }

  return (
    <Card>
      <h2 className="text-h3 text-text">Notifications</h2>
      <p className="mt-1 text-body text-muted">
        Choose how each kind of update reaches you. Security alerts can&apos;t be switched off — they
        are how you find out if someone else is trying to get into your account.
      </p>

      {/*
        The grid is optimistic and reverts on failure. Without a live region the
        switch simply slides back and nothing is announced — a screen-reader user
        is told neither that the save failed nor that their change was undone,
        which is the one case where optimistic UI most needs to speak up.
      */}
      <p role="status" aria-live="polite" className="sr-only">
        {error ? `Could not save. ${error}` : ""}
      </p>
      {error ? <p className="mt-3 text-body text-danger">{error}</p> : null}

      <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[34rem] text-body">
          {/* The table had no accessible name and no scoped headers, so the
              row/column relationship existed only visually. */}
          <caption className="sr-only">Notification channels by category</caption>
          <thead>
            <tr className="text-left text-micro uppercase tracking-wide text-muted">
              <th scope="col" className="pb-2 pr-4 font-medium">Category</th>
              {CHANNELS.map((c) => (
                <th key={c.key} scope="col" className="px-2 pb-2 text-center font-medium">{c.label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.category} className="border-t border-border">
                <th scope="row" className="py-2.5 pr-4 text-left font-normal">
                  <span className="text-text">{CATEGORY_LABEL[row.category] ?? row.category}</span>
                  {row.locked ? (
                    <Badge tone="muted">
                      <span className="ml-0">Always on</span>
                    </Badge>
                  ) : null}
                </th>
                {CHANNELS.map((c) => (
                  <td key={c.key} className="px-2 text-center">
                    {/*
                      The checkbox stays 16px visually — a 44px box in every cell
                      would make a 48-cell grid unreadable — but the label around
                      it is the hit area, so the target meets the floor without
                      the grid growing.
                    */}
                    <label className="flex min-h-11 cursor-pointer items-center justify-center">
                      <input
                        type="checkbox"
                        className="h-4 w-4 appearance-none rounded-sm border border-border-strong bg-surface checked:border-accent checked:bg-accent disabled:cursor-not-allowed disabled:opacity-40 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                        checked={Boolean(row[c.field])}
                        disabled={row.locked}
                        aria-label={`${CATEGORY_LABEL[row.category] ?? row.category} — ${c.label}`}
                        onChange={(e) => toggle(row.category, c, e.target.checked)}
                      />
                    </label>
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="mt-4 text-caption text-muted">
        Email and WhatsApp switches are saved, but only in-app and push are enforced today.
      </p>
    </Card>
  );
}
