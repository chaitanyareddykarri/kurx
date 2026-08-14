"use client";

import { useEffect } from "react";
import { AlertTriangle, RotateCw } from "lucide-react";
import Link from "next/link";

/**
 * Root error boundary for the whole site (D-235).
 *
 * Until Phase 44 the only `error.tsx` in web was on `/u/[username]`. Everything else — the public
 * site, the signed-in app, checkout, tickets — fell through to Next's built-in error screen, which
 * in production is an unstyled "Application error: a client-side exception has occurred" with no
 * branding, no retry and no way back. Admin has had this boundary since it was built.
 *
 * Says nothing about *why*. The cause is in the correlation id the API already logs, and surfacing
 * internals here would leak them to anyone able to trigger a 500 (see the error-handling rules).
 * The offer of a way home matters as much as the retry: `reset()` re-renders the same segment, so
 * it does nothing for a page that is broken rather than briefly failing.
 */
export default function RootError({
  error,
  reset
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("[kurx] unhandled error:", error.digest ?? error.message);
  }, [error]);

  return (
    <main className="container-shell py-20">
      <div className="mx-auto flex max-w-md flex-col items-center gap-4 text-center">
        <AlertTriangle size={32} className="text-muted" aria-hidden />
        <h1 className="text-xl font-semibold text-text">This page couldn&apos;t be loaded</h1>
        <p className="text-sm text-muted">
          Something went wrong on our side, not yours. Nothing you were doing has been lost.
        </p>
        <div className="flex flex-wrap items-center justify-center gap-2">
          <button
            type="button"
            onClick={reset}
            className="inline-flex min-h-11 items-center gap-1.5 rounded-md border border-border-strong px-4 text-sm text-text transition hover:bg-elevated"
          >
            <RotateCw size={14} aria-hidden /> Try again
          </button>
          <Link
            href="/"
            className="inline-flex min-h-11 items-center rounded-md px-4 text-sm text-accent-text underline underline-offset-4"
          >
            Go to the homepage
          </Link>
        </div>
      </div>
    </main>
  );
}
