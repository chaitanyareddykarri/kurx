import { SettingsNav } from "@/components/settings/settings-nav";

export default function SettingsLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="space-y-6">
      <p className="text-xs font-semibold uppercase tracking-wide text-accent">Settings</p>
      <SettingsNav />
      <div>{children}</div>
    </div>
  );
}
