"use client";

import { RefreshCw, WifiOff } from "lucide-react";
import { Button } from "./button";

/** Error view with a retry action — the counterpart to EmptyState for failed loads. */
export function ErrorState({
  message = "Something went wrong. Please try again.",
  onRetry
}: {
  message?: string;
  onRetry: () => void;
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 px-6 py-12 text-center">
      <WifiOff className="text-danger" size={40} />
      <p className="max-w-sm text-sm text-text">{message}</p>
      <Button variant="secondary" onClick={onRetry} className="mt-1">
        <RefreshCw size={15} /> Retry
      </Button>
    </div>
  );
}
