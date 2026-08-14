"use client";

import { Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";
import { useEffect, useState } from "react";

export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  // `resolvedTheme` is undefined until next-themes reads storage/matchMedia after mount, so the
  // server picks Sun and a light client expects Moon — a <path> mismatch that, sitting in the root
  // layout outside any Suspense boundary, drops SSR for the whole document. Same guard as admin.
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);
  const dark = resolvedTheme !== "light";
  return (
    <button
      type="button"
      aria-label="Toggle theme"
      title="Toggle theme"
      onClick={() => setTheme(dark ? "light" : "dark")}
      // 40×40 was below the 44px touch floor `regression-criteria.md` §2.5 sets below 768px.
      className="grid h-11 w-11 shrink-0 place-items-center rounded-md border border-border bg-surface text-muted transition duration-fast hover:bg-elevated hover:text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
    >
      {mounted ? (
        dark ? <Sun size={17} aria-hidden /> : <Moon size={17} aria-hidden />
      ) : (
        <Sun size={17} aria-hidden className="opacity-0" />
      )}
    </button>
  );
}
