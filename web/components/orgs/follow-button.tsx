"use client";

import { useState, useTransition } from "react";
import { UserPlus, UserCheck } from "lucide-react";
import { Button, useToast } from "@kurx/ui";
import { apiErrorMessage } from "@/lib/api";
import { toggleFollowAction } from "@/lib/social-actions";

/**
 * Follow / unfollow an organization.
 *
 * The toggle previously assigned straight from the awaited action with nothing around it. A rejected
 * call left the transition to reject unhandled and the button unchanged — the same silent-revert
 * shape Phase 18 found on the account toggles, where the only report of failure was that nothing
 * happened. It also announced nothing on success: the label changes, but a label change inside a
 * button the user just activated is not re-read.
 */
export function FollowButton({
  orgId,
  orgName,
  initialFollowing,
}: {
  orgId: string;
  orgName?: string;
  initialFollowing: boolean;
}) {
  const [following, setFollowing] = useState(initialFollowing);
  const [pending, start] = useTransition();
  const toast = useToast();

  const subject = orgName ?? "this organization";

  function toggle() {
    start(async () => {
      try {
        const next = await toggleFollowAction(orgId, following);
        setFollowing(next);
        toast(next ? `Following ${subject}.` : `No longer following ${subject}.`, "success");
      } catch (err) {
        toast(apiErrorMessage(err), "error");
      }
    });
  }

  return (
    <Button
      variant={following ? "secondary" : "primary"}
      aria-label={following ? `Unfollow ${subject}` : `Follow ${subject}`}
      disabled={pending}
      onClick={toggle}
    >
      {following ? <UserCheck size={16} aria-hidden /> : <UserPlus size={16} aria-hidden />}
      {following ? "Following" : "Follow"}
    </Button>
  );
}
