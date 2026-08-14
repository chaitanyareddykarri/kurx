import { ResetPassword } from "@/components/auth/reset-password";

export const metadata = { title: "Reset password · Kurx admin" };

/** Staff password reset (Phase 2C, D-127). Outside the console gate — the premise is you can't sign in. */
export default function ResetPage() {
  return (
    <div className="grid min-h-screen place-items-center bg-background p-4">
      <ResetPassword />
    </div>
  );
}
