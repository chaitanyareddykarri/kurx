import { AppShell } from "@/components/layout/app-shell";
import { requireSession } from "@/lib/session";

export default async function ProtectedLayout({ children }: { children: React.ReactNode }) {
  const session = await requireSession();
  return <AppShell>{children}</AppShell>;
}
