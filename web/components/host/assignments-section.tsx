"use client";

import { useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { assignAction, removeAssignmentAction } from "@/lib/assignment-actions";
import { Button, PhoneField, UserCard, controlClass } from "@kurx/ui";
import { AllyConnectButton } from "@/components/profile/ally-connect-button";
import { toAllyRelation } from "@/lib/ally-status";
import type { EventAssignment, AllyRelationStatus } from "@/lib/api";
import { ConfirmSubmitButton } from "@/components/host/confirm-submit-button";

const ROLES = [
  "Volunteer", "Judge", "Moderator", "Registration Desk", "Stage Manager", "Security", "Photographer",
  "Videographer", "Host", "Media Team", "Speaker Coordinator", "Technical Team", "Support Team"
];
const inputClass = controlClass;

function SubmitButton({ disabled }: { disabled?: boolean }) {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" variant="secondary" disabled={pending || disabled} className="shrink-0">
      {pending ? "Assigning…" : "Assign"}
    </Button>
  );
}

export function AssignmentsSection({ orgId, eventId, assignments, myUserId, allyStatus }: {
  orgId: string; eventId: string; assignments: EventAssignment[];
  myUserId: string; allyStatus: Record<string, AllyRelationStatus>;
}) {
  const [state, formAction] = useFormState(assignAction.bind(null, orgId, eventId), null);
  const [phone, setPhone] = useState("");
  const [phoneValid, setPhoneValid] = useState(false);

  return (
    <div className="space-y-4">
      {assignments.length > 0 ? (
        <ul className="space-y-2">
          {assignments.map((a) => (
            <li key={a.id}>
              <UserCard
                name={a.assignee_name}
                username={a.assignee_username}
                avatarSrc={a.assignee_avatar_url}
                href={a.assignee_username ? `/u/${a.assignee_username}` : undefined}
                subtitle={`${a.role}${a.custom_role ? ` (${a.custom_role})` : ""} · ${a.status}${a.notes ? ` · ${a.notes}` : ""}`}
                action={
                  <div className="flex flex-col items-end gap-2 sm:flex-row sm:items-center">
                    {a.user_id !== myUserId && (
                      <AllyConnectButton targetUserId={a.user_id} initialRelation={toAllyRelation(allyStatus[a.user_id])} initialConnectionId={null} />
                    )}
                    <form action={removeAssignmentAction.bind(null, orgId, eventId, a.id)}>
                      <ConfirmSubmitButton
                        label="Remove"
                title="Remove this assignment?"
                description="The person keeps their place on the team but loses this task."
                confirmLabel="Remove"
                      />
                    </form>
                  </div>
                }
              />
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-muted">No staff assigned yet.</p>
      )}

      <form action={formAction} className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="flex-1">
          {/* Country picker, not a bare text box: a staff number typed as bare digits is interpreted in the
              legacy region, so a Singapore volunteer's "9123 4567" would have been filed as an Indian number
              and the assignment invite sent to a stranger. The hidden input carries the E.164 value the
              server action reads, since PhoneField is controlled and the form posts uncontrolled fields. */}
          <PhoneField id="a-phone" label="Phone" value={phone} onChange={(e164, valid) => { setPhone(e164); setPhoneValid(valid); }} />
          <input type="hidden" name="phone" value={phone} />
        </div>
        <div>
          <label className="text-sm font-medium text-text" htmlFor="a-role">Role</label>
          <select id="a-role" name="role" defaultValue="Volunteer" className={`mt-1 ${inputClass}`}>
            {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
          </select>
        </div>
        <SubmitButton disabled={!phoneValid} />
      </form>
      {state && "error" in state ? (
        <p className="text-sm text-danger">{String(state.error)}</p>
      ) : state && "ok" in state ? (
        <p className="text-sm text-success">Assigned — they accept it from their own assignments.</p>
      ) : null}
    </div>
  );
}
