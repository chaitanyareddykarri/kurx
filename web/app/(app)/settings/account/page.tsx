import { Card } from "@/components/ui/card";
import { AppearanceCard } from "@/components/profile/appearance-card";
import { LanguageSwitcher } from "@/components/layout/language-switcher";
import { LogoutButton } from "@/components/settings/logout-button";
import { PhoneChange } from "@/components/settings/phone-change";
import { EmailChange } from "@/components/settings/email-change";
import { DeleteAccount } from "@/components/settings/delete-account";
import { AccountCreated } from "@/components/settings/account-created";
import { requireSession } from "@/lib/session";
import { getUsernameHistory, getAccountDeletion } from "@/lib/account-api";

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });
}

export default async function AccountSettingsPage() {
  const session = await requireSession();
  // Both are cheap reads and independent of each other, so they go together rather than serially.
  const [usernameHistory, deletion] = await Promise.all([
    getUsernameHistory(session.accessToken),
    getAccountDeletion(session.accessToken)
  ]);

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Account</h1>

      <Card>
        <h2 className="font-semibold">Phone</h2>
        <p className="mt-1 text-sm text-muted">Current: <span className="text-text">{session.me.phone}</span></p>
        <p className="mt-1 text-xs text-muted">
          Changing your phone signs you out everywhere else — it&apos;s how you sign in.
        </p>
        <div className="mt-3"><PhoneChange /></div>
      </Card>

      <Card>
        <h2 className="font-semibold">Email</h2>
        <div className="mt-3"><EmailChange current={session.me.email} /></div>
      </Card>

      {/* Read-only: the creation stamp is server-generated and immutable, so there is nothing to
          edit here. The public profile shows only the month (D-312). */}
      <Card>
        <h2 className="font-semibold">Account created</h2>
        <AccountCreated createdAt={session.me.created_at} />
      </Card>

      {usernameHistory.length > 0 ? (
        <Card>
          <h2 className="font-semibold">Previous usernames</h2>
          <p className="mt-1 text-sm text-muted">
            Released handles stay reserved for 30 days before anyone else can take them.
          </p>
          <ul className="mt-3 divide-y divide-border text-sm">
            {usernameHistory.map((h) => (
              <li key={`${h.username}-${h.released_at}`} className="flex justify-between gap-4 py-2">
                <span className="min-w-0 break-all text-text">@{h.username}</span>
                <span className="text-muted">{formatDate(h.released_at)}</span>
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      <Card>
        <div className="flex items-center justify-between gap-4">
          <div>
            <h2 className="font-semibold">Language</h2>
            <p className="text-sm text-muted">English or हिन्दी.</p>
          </div>
          <LanguageSwitcher />
        </div>
      </Card>

      <Card>
        <AppearanceCard
          initialAvatarKey={session.me.avatar_key ?? ""}
          initialAvatarUrl={session.me.avatar_url ?? null}
          name={session.me.name ?? session.me.username ?? "You"}
        />
      </Card>

      <Card>
        <h2 className="font-semibold">Sign out</h2>
        <p className="mt-1 text-sm text-muted">Sign out of Kurx on this device.</p>
        <div className="mt-3"><LogoutButton /></div>
      </Card>

      <Card>
        <h2 className="font-semibold text-danger">Delete account</h2>
        <div className="mt-3"><DeleteAccount initial={deletion} /></div>
      </Card>
    </div>
  );
}
