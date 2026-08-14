"use client";

import { useEffect } from "react";
import { ErrorState } from "@kurx/ui";

/** Route-level error boundary for the console. Keeps the shell mounted and offers a retry. */
export default function ConsoleError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    // Surface for log aggregation once wired (Phase 2 monitoring).
    console.error("[admin] console error:", error);
  }, [error]);

  return <ErrorState message="This screen failed to load." onRetry={reset} />;
}
