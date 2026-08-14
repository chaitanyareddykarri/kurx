"use client";

import { useEffect, useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { Merge, ShieldAlert } from "lucide-react";
import { Button, Card, ConfirmDialog, Field, Input, useToast } from "@kurx/ui";
import { moderateOrgAction, mergeOrgsAction } from "@/lib/admin-actions";

function OpButton({ op, label, tone }: { op: string; label: string; tone: "warn" | "danger" }) {
  const { pending } = useFormStatus();
  const cls = tone === "danger"
    ? "border-danger/40 bg-danger/10 text-danger hover:bg-danger/20"
    : "border-border text-muted hover:bg-elevated hover:text-text";
  return (
    <button
      type="submit"
      name="op"
      value={op}
      disabled={pending}
      className={`h-9 rounded-md border px-3 text-sm font-semibold disabled:opacity-50 ${cls}`}
    >
      {pending ? "…" : label}
    </button>
  );
}

function MergeSubmit() {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" disabled={pending}>
      <Merge size={15} /> {pending ? "Merging…" : "Merge"}
    </Button>
  );
}

export function OrgAdminTools({ defaultOrgId, defaultOrgName }: { defaultOrgId?: string; defaultOrgName?: string } = {}) {
  const [modState, moderate] = useFormState(moderateOrgAction, null);
  const [mergeState, merge] = useFormState(mergeOrgsAction, null);
  const toast = useToast();

  // Both actions take immediate, hard-to-reverse effect on a live organization (blacklist is a
  // hard block; merge deletes the duplicate) — gated behind ConfirmDialog like every other
  // destructive action in this console, instead of firing on a single click.
  const [pendingModeration, setPendingModeration] = useState<{ formData: FormData; op: string } | null>(null);
  const [pendingMerge, setPendingMerge] = useState<FormData | null>(null);

  useEffect(() => {
    if (modState && "ok" in modState) toast(`Organization ${(modState as { op?: string }).op ?? "updated"}.`, "success");
    else if (modState && "error" in modState) toast(String(modState.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [modState]);

  useEffect(() => {
    if (mergeState && "ok" in mergeState) toast("Organizations merged.", "success");
    else if (mergeState && "error" in mergeState) toast(String(mergeState.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mergeState]);

  return (
    <div className="space-y-6">
      <Card>
        <div className="flex items-start gap-3">
          <ShieldAlert size={18} className="mt-0.5 shrink-0 text-accent-text" />
          <div>
            <h3 className="font-semibold text-text">Suspend or blacklist an organization</h3>
            <p className="mt-1 text-sm text-muted">
              Suspend is a reversible pause; blacklist is a hard block. Both take effect immediately.
            </p>
          </div>
        </div>
        <form
          className="mt-4 space-y-3"
          onSubmit={(e) => {
            e.preventDefault();
            const op = (e.nativeEvent as SubmitEvent).submitter?.getAttribute("value") ?? "suspend";
            setPendingModeration({ formData: new FormData(e.currentTarget), op });
          }}
        >
          {defaultOrgId ? (
            <div>
              <p className="text-xs text-muted">Organization</p>
              <p className="text-sm text-text">{defaultOrgName ?? defaultOrgId}</p>
              <input type="hidden" name="orgId" value={defaultOrgId} />
            </div>
          ) : (
            <Field label="Organization ID" htmlFor="org-mod-id" helper="UUID">
              <Input id="org-mod-id" name="orgId" required placeholder="00000000-0000-0000-0000-000000000000" className="font-mono" />
            </Field>
          )}
          <Field label="Reason" htmlFor="org-mod-reason" helper="Optional">
            <Input id="org-mod-reason" name="reason" placeholder="e.g. repeated chargebacks" />
          </Field>
          <div className="flex flex-wrap items-center gap-2">
            <OpButton op="suspend" label="Suspend" tone="warn" />
            <OpButton op="blacklist" label="Blacklist" tone="danger" />
          </div>
        </form>
      </Card>

      <Card>
        <div className="flex items-start gap-3">
          <Merge size={18} className="mt-0.5 shrink-0 text-accent-text" />
          <div>
            <h3 className="font-semibold text-text">Merge a duplicate organization</h3>
            <p className="mt-1 text-sm text-muted">
              Folds the duplicate into the canonical org, then removes it. Refused if the duplicate has events,
              a ledger, funds, or is already verified.
            </p>
          </div>
        </div>
        <form
          className="mt-4 space-y-3"
          onSubmit={(e) => {
            e.preventDefault();
            setPendingMerge(new FormData(e.currentTarget));
          }}
        >
          <Field label="Duplicate org ID" htmlFor="org-merge-dup" helper="Removed after merge">
            <Input id="org-merge-dup" name="duplicateOrgId" required placeholder="00000000-0000-0000-0000-000000000000" className="font-mono" />
          </Field>
          <Field label="Canonical org ID" htmlFor="org-merge-canonical" helper="Kept after merge">
            <Input id="org-merge-canonical" name="canonicalOrgId" required placeholder="00000000-0000-0000-0000-000000000000" className="font-mono" />
          </Field>
          <MergeSubmit />
        </form>
      </Card>

      <ConfirmDialog
        open={pendingModeration !== null}
        onClose={() => setPendingModeration(null)}
        onConfirm={() => {
          if (pendingModeration) moderate(pendingModeration.formData);
        }}
        title={pendingModeration?.op === "blacklist" ? "Blacklist this organization?" : "Suspend this organization?"}
        description={
          pendingModeration?.op === "blacklist"
            ? "This is a hard block, effective immediately. The organization and its events become unavailable."
            : "This immediately pauses the organization. It can be reversed later, but takes effect now."
        }
        confirmLabel={pendingModeration?.op === "blacklist" ? "Blacklist" : "Suspend"}
        tone="danger"
      />

      <ConfirmDialog
        open={pendingMerge !== null}
        onClose={() => setPendingMerge(null)}
        onConfirm={() => {
          if (pendingMerge) merge(pendingMerge);
        }}
        title="Merge these organizations?"
        description="The duplicate organization is permanently removed after its data is folded into the canonical one. This cannot be undone."
        confirmLabel="Merge"
        tone="danger"
      />
    </div>
  );
}
