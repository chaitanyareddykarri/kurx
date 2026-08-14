"use client";

import { Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";
import { useEffect, useState } from "react";

/** Light/dark toggle — same token system and behaviour as web. Mounted guard avoids
 *  the hydration flash on first paint. */
export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);
  const dark = resolvedTheme !== "light";

  return (
    <button
      type="button"
      aria-label="Toggle theme"
      title="Toggle theme"
      onClick={() => setTheme(dark ? "light" : "dark")}
      className="grid h-10 w-10 place-items-center rounded-md border border-border bg-surface text-muted transition hover:bg-elevated hover:text-text"
    >
      {mounted ? dark ? <Sun size={17} /> : <Moon size={17} /> : <Sun size={17} className="opacity-0" />}
    </button>
  );
}
