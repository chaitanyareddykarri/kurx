"use client";

import { useEffect } from "react";
import { ErrorState } from "@kurx/ui";

/** Top-level error boundary (segments above the console group). */
export default function RootError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error("[admin] error:", error);
  }, [error]);

  return (
    <div className="grid min-h-screen place-items-center bg-background p-6">
      <ErrorState message="Something went wrong." onRetry={reset} />
    </div>
  );
}
