import { ResetPasswordPanel } from "@/components/auth/reset-password-panel";

export const metadata = { title: "Reset your password · Kurx" };

/**
 * Password reset (Phase 2C, D-127). Deliberately outside the authenticated layout — the premise is that
 * the user cannot sign in. INV-B: an OTP is never enough alone — completion needs a second factor, which
 * is a recovery code OR a satisfied step-up (`PasswordResetService` picks whichever is present). The
 * "plus a recovery code" this line used to claim was the panel's rule, never the server's.
 */
export default function ResetPage() {
  return (
    <main className="mx-auto flex min-h-screen w-full max-w-md flex-col justify-center px-4 py-12">
      <ResetPasswordPanel />
    </main>
  );
}
