import { redirect } from "next/navigation";

import { OtpPanel } from "@/components/auth/otp-panel";
import { currentSession } from "@/lib/session";
import { createMetadata } from "@/lib/site";

/**
 * Sign-in, as its own route (D-290).
 *
 * The form itself is the unchanged `OtpPanel` — password-first with the trusted-device and
 * one-time-code paths intact (D-182). It previously rendered as a column inside the marketing
 * home page; nothing about how it authenticates changed, only where it lives.
 *
 * Lives under `(public)` so it keeps the nav, footer, skip link and `<main>` landmark rather
 * than reinventing them, and so a signed-out visitor can still reach Pricing or Support from
 * here instead of hitting a dead end.
 */
export const metadata = {
  ...createMetadata({ title: "Sign in", path: "/login" }),
  // A sign-in form is not a search result. `sitemap.ts` lists `publicPages`, which this is
  // deliberately not a member of; this is the belt to that suspenders.
  robots: { index: false, follow: true }
};

export default async function LoginPage({
  searchParams
}: {
  searchParams: { login?: string };
}) {
  // Already signed in: send them where the form would have. Reads the session, never mints
  // or clears one — `lib/session.ts` is untouched by this change.
  const session = await currentSession();
  if (session) redirect(session.me.needs_onboarding ? "/register" : "/discover");

  // `requireSession()` bounces expired sessions to `/?login=required`, and the home page
  // forwards that here. Saying why they are looking at a login form is the whole point of
  // carrying the parameter this far.
  const wasRequired = searchParams.login === "required";

  return (
    <section className="grid-bg flex min-h-[calc(100vh-64px)] items-center justify-center px-4 py-12">
      <div className="w-full max-w-md">
        {/*
          "Welcome back", not "Sign in": `OtpPanel` renders its own `<h2>Sign in</h2>`, and two
          headings saying the same word stacked 150px apart reads as a mistake. This keeps the
          page's h1 → h2 outline honest while letting the card name the action.
        */}
        <div className="mb-6 text-center">
          <h1 className="text-h1 text-text">Welcome back</h1>
          <p className="mt-2 text-body text-muted">Events. Simplified.</p>
        </div>

        {wasRequired ? (
          <p
            role="status"
            className="mb-4 rounded-md border border-border-strong bg-surface px-4 py-3 text-caption text-muted"
          >
            Your session ended. Sign in to pick up where you left off.
          </p>
        ) : null}

        {/*
          Rendered bare, with no wrapper. `OtpPanel`'s root is already the card — border, surface
          fill and padding — and boxing it again put a card on a card, which visual-identity.md §7
          forbids ("elevation is one step at a time"). It also already carries the "New to Kurx?
          Create an account" link, so the page adds no second one.
        */}
        <OtpPanel />
      </div>
    </section>
  );
}
