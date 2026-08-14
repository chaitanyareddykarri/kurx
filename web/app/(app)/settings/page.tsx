import Link from "next/link";
import {
  Ban, ChevronRight, LifeBuoy, Lock, Scale, ShieldCheck, UserCircle, UserCog
} from "lucide-react";
import { Card } from "@/components/ui/card";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Settings · Kurx" };

/**
 * The Settings hub.
 *
 * **Settings had no index.** `/settings` rendered the profile editor under an `<h1>Profile</h1>`,
 * so the section had no front door and its own name appeared nowhere — while `/profile` redirected
 * *here*, making the public-identity editor and the account-configuration area the same address.
 *
 * The split is now by question: **Edit Profile authors what others see; everything below changes how
 * the account behaves.** Identity verification is deliberately absent — its status and its actions
 * live together on your profile, under Verification, because that is where the question is asked.
 * Notification preferences are equally absent: they sit with the notifications they govern.
 */
const SECTIONS = [
  {
    href: "/settings/profile",
    icon: UserCircle,
    label: "Edit profile",
    description: "Photo, headline, bio, skills, languages and links — what other people see."
  },
  {
    href: "/settings/account",
    icon: UserCog,
    label: "Account",
    description: "Username, email, phone, language, theme and account deletion."
  },
  {
    href: "/settings/privacy",
    icon: Lock,
    label: "Privacy",
    description: "Who can see each section of your profile."
  },
  {
    href: "/settings/security",
    icon: ShieldCheck,
    label: "Security",
    description: "Password, passkeys, trusted devices and browsers, sessions and recovery."
  },
  {
    href: "/settings/blocked",
    icon: Ban,
    label: "Blocked accounts",
    description: "People who cannot see or contact you."
  },
  { href: "/settings/help", icon: LifeBuoy, label: "Help & support", description: "Get in touch." },
  { href: "/settings/legal", icon: Scale, label: "Legal", description: "Privacy policy and terms." }
] as const;

export default async function SettingsHubPage() {
  const session = await requireSession();

  return (
    <div className="space-y-lg">
      <div>
        <h1 className="text-2xl font-semibold text-text">Settings</h1>
        <p className="mt-1 text-sm text-muted">
          Signed in as {session.me.username ? `@${session.me.username}` : session.me.name}.
        </p>
      </div>

      <Card>
        <ul className="divide-y divide-border">
          {SECTIONS.map((s) => {
            const Icon = s.icon;
            return (
              <li key={s.href}>
                <Link
                  href={s.href}
                  className="flex min-h-11 items-start gap-md py-md transition duration-fast hover:bg-elevated"
                >
                  <Icon size={18} aria-hidden className="mt-0.5 shrink-0 text-muted" />
                  <span className="min-w-0 flex-1">
                    <span className="block text-body font-medium text-text">{s.label}</span>
                    <span className="block text-sm text-muted">{s.description}</span>
                  </span>
                  <ChevronRight size={16} aria-hidden className="mt-1 shrink-0 text-muted" />
                </Link>
              </li>
            );
          })}
        </ul>
      </Card>
    </div>
  );
}
