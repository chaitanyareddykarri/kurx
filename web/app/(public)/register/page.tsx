import Link from "next/link";

import { currentSession } from "@/lib/session";
import { getRegistrationStatus, type RegistrationStatus } from "@/lib/api";
import { AuthShell } from "@/components/auth/auth-shell";
import { RegistrationFlow } from "@/components/auth/registration-flow";
import { SignOutLink } from "@/components/auth/sign-out-link";
import { createMetadata } from "@/lib/site";

/**
 * Registration ceremony (Phase 2D). Anonymous visitors start with the phone-OTP signup inside the
 * wizard; a visitor who already has a session (e.g. arrived from the login OTP path needing
 * onboarding) resumes from wherever `/registration/status` says they are.
 *
 * **Moved inside `(public)` (D-384 §2).** It used to render its own bare `<main>` outside the group,
 * so signup had no navigation, no footer, no skip link and no logo — a stranger who landed here had
 * no orientation and no way out but the back button, while `/login` carried the full shell all
 * along. Route groups do not affect the URL, so `/register` is unchanged for every link, redirect
 * and test that points at it; the page simply stops reinventing a shell badly and drops its own
 * `<main>` so the layout owns the only one.
 */
export const metadata = {
  ...createMetadata({ title: "Create an account", path: "/register" }),
  // Signing up is not a search result — the same reasoning as `/login`, and `sitemap.ts` lists
  // `publicPages`, which this is deliberately not a member of.
  robots: { index: false, follow: true }
};

export default async function RegisterPage() {
  const session = await currentSession();
  let initialStatus: RegistrationStatus | null = null;
  if (session) {
    try {
      initialStatus = await getRegistrationStatus(session.accessToken);
    } catch {
      initialStatus = null;
    }
  }

  /*
   * Two arrivals, two different sentences. Signing in with an account whose setup is unfinished
   * lands here — `/login` redirects on `needs_onboarding` — and this page told that person to
   * "Create your Kurx account" while ticking "Phone verified" above it. They read it as the login
   * button creating a second account, which is exactly what it looks like.
   */
  const resuming = initialStatus !== null;

  return (
    <AuthShell
      title={resuming ? "Finish setting up your account" : "Create your Kurx account"}
      subtitle={
        resuming
          ? `You're signed in${initialStatus?.phone ? ` as ${initialStatus.phone}` : ""} — just the steps below to go.`
          : "Start with your phone number. The rest takes a couple of minutes."
      }
    >
      <RegistrationFlow initialStatus={initialStatus} />

      {/*
        The way out, and the way across.

        `/login` redirects a signed-in-but-incomplete account here, and this page had no sign-out and
        no account switcher — so pressing "Log in" could never produce a login form, for anyone in
        that state, ever. That escape is still only rendered on the resume path: a visitor who has
        not signed in has nothing to sign out of.

        The sign-in link is the other half. `OtpPanel` has always offered "New to Kurx? Create an
        account"; nothing here pointed back, so the pair of screens was a one-way door.
      */}
      <p className="mt-6 text-center text-body text-muted">
        {resuming ? (
          <>
            Not you? <SignOutLink />
          </>
        ) : (
          <>
            Already have an account?{" "}
            <Link href="/login" className="font-medium text-accent-text underline underline-offset-2">
              Sign in
            </Link>
          </>
        )}
      </p>
    </AuthShell>
  );
}
