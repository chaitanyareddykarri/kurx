import { Users } from "lucide-react";
import { Card } from "@kurx/ui";
import { UserCard } from "@kurx/ui";
import { myGroups, getAllyStatusBatch, apiErrorMessage, type AllyRelationStatus } from "@/lib/api";
import { requireSession } from "@/lib/session";
import { JoinGroupForm } from "@/components/groups/join-group-form";
import { AllyConnectButton } from "@/components/profile/ally-connect-button";
import { toAllyRelation } from "@/lib/ally-status";

export default async function GroupsPage() {
  const session = await requireSession();

  let groups;
  try {
    groups = await myGroups(session.accessToken);
  } catch (err) {
    return (
      <div className="space-y-4">
        <h1 className="text-3xl font-semibold">My Groups</h1>
        <Card>
          <p className="text-sm text-muted">Couldn&apos;t load your groups: <span className="text-text">{apiErrorMessage(err)}</span></p>
        </Card>
      </div>
    );
  }

  const otherMemberIds = groups
    .flatMap((g) => g.members.map((m) => m.user_id))
    .filter((id): id is string => id !== null && id !== session.me.id);
  const allyStatus = await getAllyStatusBatch(session.accessToken, otherMemberIds).catch(
    () => ({}) as Record<string, AllyRelationStatus>
  );

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-semibold">My Groups</h1>
        <p className="mt-2 text-sm text-muted">
          Join a group with a code from your leader, then track members and payment readiness. A new group is
          created when you book an event as a group leader.
        </p>
      </div>

      <Card>
        <h2 className="font-semibold">Join a group by code</h2>
        <div className="mt-3">
          <JoinGroupForm />
        </div>
      </Card>

      {groups.length === 0 ? (
        <p className="text-muted">
          You&apos;re not in any groups yet. Join one with a code above, or book an event as a group leader.
        </p>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {groups.map((g) => {
            const paid = g.members.filter((m) => m.ticket_id !== null).length;
            const total = g.members.length;
            const ready = total > 0 && paid === total;
            return (
              <Card key={g.id}>
                <div className="flex items-center gap-2">
                  <Users size={18} aria-hidden className="text-accent-text" />
                  <h2 className="font-semibold">{g.display_name ?? `Group #${g.group_number}`}</h2>
                </div>
                <p className="mt-1 text-sm text-muted">
                  Join code: <span className="font-mono text-text">{g.join_code}</span>
                </p>
                <p className="mt-2 text-sm text-muted">{total} / {g.capacity} members</p>
                <p className="mt-1 text-sm">
                  Payment readiness:{" "}
                  <span className={ready ? "text-accent-text" : "text-muted"}>
                    {paid} / {total} ticketed{ready ? " — ready" : ""}
                  </span>
                </p>
                {total > 0 ? (
                  <ul className="mt-3 space-y-2">
                    {g.members.map((m) => (
                      <li key={m.id}>
                        <UserCard
                          name={m.name}
                          username={m.username}
                          avatarSrc={m.avatar_key}
                          href={m.username ? `/u/${m.username}` : undefined}
                          subtitle={m.ticket_id ? "Ticketed" : "Pending"}
                          action={
                            m.user_id && m.user_id !== session.me.id ? (
                              <AllyConnectButton
                                targetUserId={m.user_id}
                                initialRelation={toAllyRelation(allyStatus[m.user_id])}
                                initialConnectionId={null}
                              />
                            ) : undefined
                          }
                        />
                      </li>
                    ))}
                  </ul>
                ) : null}
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
