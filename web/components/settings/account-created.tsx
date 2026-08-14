"use client";

import { useEffect, useState } from "react";
import { formatDateTime } from "@/lib/formatters";

/**
 * Read-only display of when this account was created, in the viewer's own timezone.
 *
 * A client component on purpose. The settings page is a server component, so formatting there would
 * render the *server's* timezone — a user in Kolkata would be shown UTC and quietly conclude their
 * account was created five and a half hours earlier than it was. `Intl` picks up the browser's zone
 * only in the browser.
 *
 * Formatted after mount rather than during render, because the server and the client would otherwise
 * produce different text for the same node and React would report a hydration mismatch.
 */
export function AccountCreated({ createdAt }: { createdAt?: string }) {
  const [formatted, setFormatted] = useState<string | null>(null);

  useEffect(() => {
    if (createdAt) setFormatted(formatDateTime(createdAt));
  }, [createdAt]);

  if (!createdAt) return null;

  return (
    <p className="mt-1 text-sm text-muted">
      {/* The raw value stays in the DOM as a machine-readable `dateTime`, so it is still the exact
          instant the server sent even while the localised string is being computed. */}
      <time dateTime={createdAt} className="text-text">
        {formatted ?? "…"}
      </time>
    </p>
  );
}
