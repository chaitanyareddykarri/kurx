"use client";

import { useState } from "react";
import { Button } from "@kurx/ui";

/**
 * D-388 — "Request changes" opens the proposal form; it does not navigate anywhere.
 *
 * A separate page would lose the approved values the host is comparing against, and would need its own
 * route, its own data fetch and its own back-link for one form. Kept collapsed by default so the live
 * record — what the event actually says right now — is what the page opens on: a host arriving to check
 * a detail should not be looking at an edit form.
 *
 * `<details>` was the obvious answer and is not used here: the panel contains a form, and a native
 * disclosure keeps a closed form's fields in the DOM and submittable by an Enter key inside it. State is
 * one `useState`, which is less than the workaround would have been.
 */
export function RequestChangesDisclosure({ children }: { children: React.ReactNode }) {
  const [open, setOpen] = useState(false);

  if (!open) {
    return (
      <Button type="button" onClick={() => setOpen(true)}>
        Request changes
      </Button>
    );
  }

  return (
    <div className="space-y-4 rounded-lg border border-border-strong p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-text">Propose changes</h3>
        <Button type="button" variant="ghost" size="sm" onClick={() => setOpen(false)}>
          Cancel
        </Button>
      </div>
      {children}
    </div>
  );
}
