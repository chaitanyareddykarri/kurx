"use client";

import { AlertTriangle, RotateCw } from "lucide-react";

/**
 * Error boundary for the public profile route (D-235).
 *
 * This exists so a failure to load a profile cannot be reported as `notFound()`. The two say very
 * different things to a visitor: "this person does not exist on Kurx" is a claim about someone's
 * identity, and we must never make it because a query timed out. Everything the page cannot load
 * lands here instead, where the message is about us.
 *
 * Deliberately says nothing about *why*. The cause is in the correlation id the API already logs;
 * surfacing internals here would leak them to anyone who can trigger a 500 (see error-handling rules).
 */
export default function ProfileError({ reset }: { error: Error; reset: () => void }) {
  return (
    <main className="container-shell py-20">
      <div className="mx-auto flex max-w-md flex-col items-center gap-4 text-center">
        <AlertTriangle size={32} className="text-muted" aria-hidden />
        <h1 className="text-xl font-semibold text-text">This profile couldn&apos;t be loaded</h1>
        <p className="text-sm text-muted">
          Something went wrong on our side. The profile is still there — this is a temporary problem.
        </p>
        <button
          type="button"
          onClick={reset}
          className="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-sm text-text transition hover:bg-elevated"
        >
          <RotateCw size={14} aria-hidden /> Try again
        </button>
      </div>
    </main>
  );
}
