"use client";

import { logoutAction } from "@/lib/actions";

/**
 * Sign out, rendered inline inside a sentence rather than as a button.
 *
 * `LogoutButton` (settings) is a `Button` in its own `<form>`, which is right for a settings row and
 * wrong for "Not you? Sign out" — a block-level button mid-paragraph breaks the line. Same action,
 * same server-side `logoutAction`; only the presentation differs.
 */
export function SignOutLink() {
  return (
    <form action={logoutAction} className="inline">
      <button
        type="submit"
        className="text-accent-text underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        Sign out
      </button>
    </form>
  );
}
