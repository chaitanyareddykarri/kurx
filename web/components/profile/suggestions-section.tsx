import { UserCard } from "@kurx/ui";
import { getMySuggestionsAction } from "@/lib/social-actions";
import { AllyConnectButton } from "@/components/profile/ally-connect-button";

/** Ranked from real shared history only — shared event co-participation and shared organization
 * membership (v1 signals). No random suggestions, no fabricated relevance. */
export async function SuggestionsSection() {
  const suggestions = await getMySuggestionsAction().catch(() => []);
  if (suggestions.length === 0) return null;

  return (
    <section>
      <h2 className="mb-3 font-semibold text-text">Suggested Allies</h2>
      <div className="grid gap-3 sm:grid-cols-2">
        {suggestions.map((s) => (
          <UserCard
            key={s.user_id}
            name={s.name}
            username={s.username}
            avatarSrc={s.avatar_key}
            subtitle={s.reason}
            href={s.username ? `/u/${s.username}` : undefined}
            action={<AllyConnectButton targetUserId={s.user_id} initialRelation="none" initialConnectionId={null} />}
          />
        ))}
      </div>
    </section>
  );
}
