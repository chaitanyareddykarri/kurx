import { requireSession } from "@/lib/session";
import { listBlocks } from "@/lib/account-api";
import { BlockedUsers } from "@/components/settings/blocked-users";

export const metadata = { title: "Blocked accounts · Kurx" };

/**
 * Blocked accounts.
 *
 * Previously rendered *as* `/settings/privacy` under an `<h1>Privacy</h1>`, while the actual privacy
 * controls — the four-tier per-section visibility — sat unlabelled on the settings index. Two
 * different concerns wearing one name: blocking is about specific people, visibility is about
 * sections. They are now two pages, each called what it is.
 */
export default async function BlockedUsersPage() {
  const session = await requireSession();
  const blocks = await listBlocks(session.accessToken).catch(() => []);

  return (
    <div className="space-y-lg">
      <div>
        <h1 className="text-2xl font-semibold text-text">Blocked accounts</h1>
        <p className="mt-1 text-sm text-muted">
          People who cannot see your profile, message you, or interact with your posts.
        </p>
      </div>
      <BlockedUsers initial={blocks} />
    </div>
  );
}
