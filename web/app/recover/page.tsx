import { RecoveryPanel } from "@/components/auth/recovery-panel";

export const metadata = { title: "Recover your account · Kurx" };

/**
 * Account recovery (AM7/D-083). Deliberately outside the authenticated layout — the whole premise is
 * that the user cannot sign in.
 */
export default function RecoverPage() {
  return (
    <main className="mx-auto flex min-h-screen w-full max-w-md flex-col justify-center px-4 py-12">
      <RecoveryPanel />
    </main>
  );
}
