"use client";

import { ThemeProvider } from "next-themes";
import { ReactNode } from "react";
import { QueryProvider } from "@/lib/query-client";

/** App-wide client providers: theme (shared token system, class strategy) + React Query. */
export function Providers({ children }: { children: ReactNode }) {
  return (
    <ThemeProvider attribute="class" defaultTheme="dark" enableSystem storageKey="kurx-admin-theme">
      <QueryProvider>{children}</QueryProvider>
    </ThemeProvider>
  );
}
