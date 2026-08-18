import { ResetPasswordPanel } from "@/components/auth/reset-password-panel";
import { Atmosphere } from "@/components/marketing/atmosphere";

export const metadata = { title: "Reset your password · Kurx" };

/**
 * Moved inside `(public)` (D-384 §2, same reasoning as `/register`). The comment below has always
 * said "deliberately outside the AUTHENTICATED layout", and it still is — `(public)` is the
 * signed-out shell, so the premise that the user cannot sign in is untouched. What changes is that
 * the page stops rendering a bare `<main>` with no navigation, no footer and no logo. This one is
 * reached from the sign-in card's "Forgot password?", so leaving it a dead end meant the screen
 * beside it led straight into one. Route groups do not affect the URL.
 */

/**
 * Password reset (Phase 2C, D-127). Deliberately outside the authenticated layout — the premise is that
 * the user cannot sign in. INV-B: an OTP is never enough alone — completion needs a second factor, which
 * is a recovery code OR a satisfied step-up (`PasswordResetService` picks whichever is present). The
 * "plus a recovery code" this line used to claim was the panel's rule, never the server's.
 */
export default function ResetPage() {
  return (
    <section className="relative border-b border-border">
      <Atmosphere grid />
      <div className="container-shell relative flex justify-center py-16 sm:py-24">
        <div className="w-full max-w-md">
          <ResetPasswordPanel />
        </div>
      </div>
    </section>
  );
}
