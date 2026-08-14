"use client";

import { useEffect, useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { Button, ConfirmDialog, Field, Input, Select, Sheet, Textarea, useToast } from "@kurx/ui";
import { Archive, Copy, Eye, EyeOff, RotateCcw, ShieldCheck, ShieldOff, Trash2 } from "lucide-react";
import type { AdminCategory, TypeCapability } from "@/lib/api";
import {
  createCategoryAction,
  updateCategoryAction,
  deleteCategoryAction,
  disableCategoryAction,
  enableCategoryAction,
  archiveCategoryAction,
  restoreCategoryAction,
  setCategoryVisibilityAction,
  duplicateCategoryAction,
  getTypeCapabilitiesAction,
  setTypeCapabilitiesAction
} from "@/lib/admin-actions";

const LEVELS = ["audience", "category", "type"] as const;

/** Create and edit share one form — same pattern as the legacy CategoryDialog. On edit, Level is fixed
 *  (no level-change path); a Type row's parent (Category) can be changed, nothing else can reparent. */
export function TaxonomyDetailSheet({
  category,
  level,
  allCategories,
  onClose
}: {
  category?: AdminCategory;
  level?: string;
  allCategories: AdminCategory[];
  onClose: () => void;
}) {
  const isEdit = Boolean(category);
  const effectiveLevel = category?.level ?? level ?? "audience";
  const toast = useToast();

  const boundAction = category ? updateCategoryAction.bind(null, category.id) : createCategoryAction;
  const [state, action] = useFormState(boundAction, null);
  const [selectedLevel, setSelectedLevel] = useState(effectiveLevel);
  const [deleting, setDeleting] = useState(false);

  const succeeded = Boolean(state && "ok" in state);
  useEffect(() => {
    if (succeeded) {
      toast(isEdit ? "Saved." : "Created.", "success");
      onClose();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [succeeded]);

  const parentOptions =
    selectedLevel === "category"
      ? allCategories.filter((c) => c.level === "audience")
      : selectedLevel === "type"
        ? allCategories.filter((c) => c.level === "category")
        : [];

  const showParentField = (!isEdit && selectedLevel !== "audience") || (isEdit && effectiveLevel === "type");
  const hasChildren = isEdit && allCategories.some((c) => c.parent_id === category!.id);
  const canDelete = isEdit && category!.usage_count === 0 && !hasChildren;

  return (
    <Sheet open onClose={onClose} size="sm" title={isEdit ? category!.name : `New ${effectiveLevel}`}>
      <div className="space-y-5">
        {isEdit ? <LifecycleBar category={category!} onClose={onClose} /> : null}

        <form action={action} className="space-y-3">
          <Field label="Name" htmlFor="tx-name">
            <Input id="tx-name" name="name" required minLength={2} maxLength={100} defaultValue={category?.name ?? ""} />
          </Field>

          {!isEdit ? (
            <Field label="Level" htmlFor="tx-level">
              <Select id="tx-level" name="level" value={selectedLevel} onChange={(e) => setSelectedLevel(e.target.value)}>
                {LEVELS.map((l) => (
                  <option key={l} value={l}>
                    {l}
                  </option>
                ))}
              </Select>
            </Field>
          ) : null}

          {showParentField ? (
            <Field
              label={selectedLevel === "type" ? "Category" : "Audience"}
              htmlFor="tx-parent"
              helper={isEdit ? "Moving this changes where it appears in Create Event — historical events keep their original reference either way." : undefined}
            >
              <Select id="tx-parent" name="parentId" required={!isEdit} defaultValue={category?.parent_id ?? ""}>
                <option value="" disabled={!isEdit}>
                  Select…
                </option>
                {parentOptions.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </Select>
            </Field>
          ) : null}

          <Field label="Description" htmlFor="tx-desc">
            <Textarea id="tx-desc" name="description" rows={2} defaultValue={category?.description ?? ""} />
          </Field>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Color" htmlFor="tx-color" helper="Hex, e.g. #2D6A73">
              <Input id="tx-color" name="color" defaultValue={category?.color ?? ""} />
            </Field>
            <Field label="Badge" htmlFor="tx-badge" helper='e.g. "New"'>
              <Input id="tx-badge" name="badge" defaultValue={category?.badge ?? ""} />
            </Field>
          </div>
          <Field label="Icon key" htmlFor="tx-icon" helper="A storage key, not a URL — same pattern as speaker/sponsor photos. No upload widget yet.">
            <Input id="tx-icon" name="iconKey" defaultValue={category?.icon_key ?? ""} />
          </Field>
          <Field label="Search keywords" htmlFor="tx-kw" helper="Comma-separated aliases">
            <Input id="tx-kw" name="searchKeywords" defaultValue={category?.search_keywords ?? ""} />
          </Field>

          {!isEdit ? (
            <label className="flex items-center gap-2 text-sm text-text">
              <input type="checkbox" name="isVisible" value="true" defaultChecked className="h-4 w-4 accent-accent" />
              Visible in Create Event
            </label>
          ) : null}

          {state && "error" in state ? (
            <p role="alert" className="text-sm text-danger">
              {String(state.error)}
            </p>
          ) : null}

          <div className="flex justify-end gap-2 pt-1">
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <SubmitButton label={isEdit ? "Save" : "Create"} />
          </div>
        </form>

        {isEdit && effectiveLevel === "type" ? <CapabilitiesPanel typeId={category!.id} /> : null}

        {isEdit ? (
          <div className="space-y-2 rounded-lg border border-border p-3 text-xs text-muted">
            <p>
              Usage: {category!.usage_count} event{category!.usage_count === 1 ? "" : "s"}
              {hasChildren ? " · has child taxonomy nodes" : ""}
            </p>
            <p>
              Version {category!.version} · Updated {new Date(category!.updated_at).toLocaleString("en-IN")}
            </p>
            {effectiveLevel === "type" ? (
              <Button
                variant="ghost"
                
                onClick={async () => {
                  const r = await duplicateCategoryAction(category!.id);
                  if (r && "error" in r) toast(String(r.error), "error");
                  else {
                    toast("Duplicated.", "success");
                    onClose();
                  }
                }}
              >
                <Copy size={13} /> Duplicate
              </Button>
            ) : null}
          </div>
        ) : null}

        {isEdit ? (
          <div className="border-t border-border pt-3">
            <Button
              variant="ghost"
              className={canDelete ? "text-danger hover:bg-danger/10" : "cursor-not-allowed opacity-50"}
              disabled={!canDelete}
              onClick={() => canDelete && setDeleting(true)}
            >
              <Trash2 size={14} /> Delete
            </Button>
            {!canDelete ? (
              <p className="mt-1 text-xs text-muted">
                Delete is disabled — still used by {category!.usage_count} event{category!.usage_count === 1 ? "" : "s"}
                {hasChildren ? ", or still has child taxonomy nodes" : ""}. Disable or Archive instead.
              </p>
            ) : null}
          </div>
        ) : null}
      </div>

      <ConfirmDialog
        open={deleting}
        onClose={() => setDeleting(false)}
        onConfirm={async () => {
          const r = await deleteCategoryAction(category!.id);
          if (r && "error" in r) toast(String(r.error), "error");
          else {
            toast("Deleted.", "success");
            onClose();
          }
        }}
        title="Delete this node?"
        description="Permanent — only possible because nothing references it. Prefer Disable or Archive for anything that might be needed again."
        confirmLabel="Delete"
        tone="danger"
      />
    </Sheet>
  );
}

function SubmitButton({ label }: { label: string }) {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Saving…" : label}
    </Button>
  );
}

/** Active/Disabled/Archived — orthogonal to the Visible checkbox in the form above (D-188). Archived
 *  restores to Disabled, never straight back to Active — a deliberate two-step. */
function LifecycleBar({ category, onClose }: { category: AdminCategory; onClose: () => void }) {
  const toast = useToast();

  async function run(fn: (id: string) => Promise<{ ok?: boolean; error?: unknown }>, label: string) {
    const r = await fn(category.id);
    if (r && "error" in r) toast(String(r.error), "error");
    else {
      toast(label, "success");
      onClose();
    }
  }

  return (
    <div className="flex flex-wrap gap-1.5 rounded-lg border border-border bg-surface p-2">
      {category.status === "active" ? (
    <Button variant="ghost" onClick={() => run(disableCategoryAction, "Disabled.")}>
          <ShieldOff size={13} /> Disable
        </Button>
      ) : category.status === "disabled" ? (
        <>
     <Button variant="ghost" className="text-success" onClick={() => run(enableCategoryAction, "Enabled.")}>
            <ShieldCheck size={13} /> Enable
          </Button>
     <Button variant="ghost" onClick={() => run(archiveCategoryAction, "Archived.")}>
            <Archive size={13} /> Archive
          </Button>
        </>
      ) : (
    <Button variant="ghost" onClick={() => run(restoreCategoryAction, "Restored to Disabled.")}>
          <RotateCcw size={13} /> Restore
        </Button>
      )}
      <span className="mx-1 h-5 w-px self-center bg-border" />
      {category.is_visible ? (
    <Button variant="ghost" onClick={() => run((id) => setCategoryVisibilityAction(id, false), "Hidden from Create Event.")}>
          <EyeOff size={13} /> Hide
        </Button>
      ) : (
    <Button variant="ghost" onClick={() => run((id) => setCategoryVisibilityAction(id, true), "Visible in Create Event.")}>
          <Eye size={13} /> Unhide
        </Button>
      )}
    </div>
  );
}

/** Type-level only. Reuses the existing V3 Capability registry (~45 slugs) — never a new capability
 *  catalog. Not yet wired into live event-capability resolution (D-188) — sets a default for the future. */
function CapabilitiesPanel({ typeId }: { typeId: string }) {
  const [caps, setCaps] = useState<TypeCapability[] | null>(null);
  const [saving, setSaving] = useState(false);
  const toast = useToast();

  useEffect(() => {
    let cancelled = false;
    void getTypeCapabilitiesAction(typeId).then((r) => {
      if (cancelled) return;
      if (r && "capabilities" in r) setCaps(r.capabilities ?? null);
      else if (r && "error" in r) toast(String(r.error), "error");
    });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [typeId]);

  function toggle(slug: string, checked: boolean) {
    setCaps((prev) => prev?.map((c) => (c.slug === slug ? { ...c, state: checked ? "on" : "off" } : c)) ?? prev);
  }

  async function save() {
    if (!caps) return;
    setSaving(true);
    const payload = caps.filter((c) => c.state !== "off").map((c) => ({ slug: c.slug, state: c.state }));
    const r = await setTypeCapabilitiesAction(typeId, payload);
    setSaving(false);
    if (r && "error" in r) toast(String(r.error), "error");
    else toast("Capabilities saved.", "success");
  }

  if (!caps) return <p className="text-xs text-muted">Loading capabilities…</p>;

  const groups = Array.from(new Set(caps.map((c) => c.group_slug)));

  return (
    <div className="rounded-lg border border-border p-3">
      <div className="mb-2 flex items-center justify-between">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted">Capabilities this type supports</p>
        <Button variant="secondary"  onClick={save} disabled={saving}>
          {saving ? "Saving…" : "Save"}
        </Button>
      </div>
      <div className="space-y-3">
        {groups.map((g) => (
          <div key={g}>
            <p className="mb-1 text-[10px] uppercase text-muted">{g}</p>
            <div className="grid grid-cols-2 gap-1">
              {caps
                .filter((c) => c.group_slug === g)
                .map((c) => (
                  <label key={c.slug} className="flex items-center gap-1.5 text-xs text-text">
                    <input
                      type="checkbox"
                      checked={c.state !== "off"}
                      onChange={(e) => toggle(c.slug, e.target.checked)}
                      className="h-3.5 w-3.5 accent-accent"
                    />
                    {c.name}
                  </label>
                ))}
            </div>
          </div>
        ))}
      </div>
      <p className="mt-2 text-[11px] italic text-muted">
        Not yet wired into live event behavior — this sets a default for future use, not a current effect.
      </p>
    </div>
  );
}
