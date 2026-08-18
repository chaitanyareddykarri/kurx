import { RecoveryPanel } from "@/components/auth/recovery-panel";
import { Atmosphere } from "@/components/marketing/atmosphere";

export const metadata = { title: "Recover your account · Kurx" };

/**
 * Moved inside `(public)` (D-384 §2, same reasoning as `/register`). The comment below has always
 * said "deliberately outside the AUTHENTICATED layout", and it still is — `(public)` is the
 * signed-out shell, so the premise that the user cannot sign in is untouched. What changes is that
 * the page stops rendering a bare `<main>` with no navigation, no footer and no logo. This one is
 * reached from the sign-in card's "Forgot password?", so leaving it a dead end meant the screen
 * beside it led straight into one. Route groups do not affect the URL.
 */

/**
 * Account recovery (AM7/D-083). Deliberately outside the authenticated layout — the whole premise is
 * that the user cannot sign in.
 */
export default function RecoverPage() {
  return (
    <section className="relative border-b border-border">
      <Atmosphere grid />
      <div className="container-shell relative flex justify-center py-16 sm:py-24">
        <div className="w-full max-w-md">
          <RecoveryPanel />
        </div>
      </div>
    </section>
  );
}
