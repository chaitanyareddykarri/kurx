import { currentSession } from "@/lib/session";
import { getRegistrationStatus, type RegistrationStatus } from "@/lib/api";
import { RegistrationFlow } from "@/components/auth/registration-flow";
import { SignOutLink } from "@/components/auth/sign-out-link";

/**
 * Registration ceremony (Phase 2D). Anonymous visitors start with the phone-OTP signup inside the
 * wizard; a visitor who already has a session (e.g. arrived from the login OTP path needing
 * onboarding) resumes from wherever `/registration/status` says they are.
 */
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

  return (
    <main className="container-shell flex min-h-screen items-center justify-center py-10">
      <div className="w-full max-w-md space-y-6">
        {/*
          Two arrivals, two different sentences. Signing in with an account whose setup is unfinished
          lands here — `/login` redirects on `needs_onboarding` — and this page told that person to
          "Create your Kurx account" while ticking "Phone verified" above it. They read it as the
          login button creating a second account, which is exactly what it looks like.
        */}
        <div className="space-y-1.5 text-center">
          {initialStatus ? (
            <>
              <h1 className="text-2xl font-semibold">Finish setting up your account</h1>
              <p className="text-sm text-muted">
                You&apos;re signed in{initialStatus.phone ? ` as ${initialStatus.phone}` : ""} — just the
                steps below to go.
              </p>
            </>
          ) : (
            <>
              <h1 className="text-2xl font-semibold">Create your Kurx account</h1>
              <p className="text-sm text-muted">A few quick steps and you&apos;re in.</p>
            </>
          )}
        </div>
        <RegistrationFlow initialStatus={initialStatus} />

        {/*
          The way out. `/login` redirects a signed-in-but-incomplete account here, and this page had
          no sign-out and no account switcher — so pressing "Log in" could never produce a login form,
          for anyone in that state, ever. Only rendered on the resume path: a visitor who has not
          signed in has nothing to sign out of.
        */}
        {initialStatus ? (
          <p className="text-center text-sm text-muted">
            Not you? <SignOutLink />
          </p>
        ) : null}
      </div>
    </main>
  );
}
