"use client";

import { useCallback, useEffect, useMemo, useState, useTransition } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { Mic, Plus, Trash2 } from "lucide-react";
import {
  Badge, Button, Card, ConfirmDialog, DataTable, Dialog, EmptyState, Field, Input, Pagination, SearchBar, Textarea,
  type Column, type SortState
} from "@kurx/ui";
import {
  createSpeakerAction, deleteSpeakerAction, eventSpeakerAction, updateSpeakerAction
} from "@/lib/admin-actions";
import type { Speaker } from "@/lib/api";

const PAGE_SIZE = 20;

export function SpeakersManager({
  orgId,
  orgName,
  eventId,
  eventTitle,
  roster,
  lineup
}: {
  orgId: string;
  orgName: string;
  eventId: string;
  eventTitle: string;
  roster: Speaker[];
  lineup: Speaker[];
}) {
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<SortState>({ key: "name", dir: "asc" });
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Speaker | null>(null);
  const [deleting, setDeleting] = useState<Speaker | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, start] = useTransition();

  const closeCreate = useCallback(() => setCreating(false), []);
  const closeEdit = useCallback(() => setEditing(null), []);

  // The endpoint takes no paging/search params and returns the org's whole roster ordered by name,
  // so search, sort and paging are computed here over the full set rather than faked on a page.
  const onEvent = useMemo(() => new Set(lineup.map((s) => s.id)), [lineup]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const rows = roster.filter(
      (s) =>
        !q ||
        s.name.toLowerCase().includes(q) ||
        (s.company ?? "").toLowerCase().includes(q) ||
        (s.role ?? "").toLowerCase().includes(q)
    );
    const dir = sort.dir === "asc" ? 1 : -1;
    return [...rows].sort((a, b) => {
      const key = sort.key as "name" | "company" | "role";
      return (a[key] ?? "").localeCompare(b[key] ?? "") * dir;
    });
  }, [roster, query, sort]);

  const pageRows = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  function run(fn: () => Promise<{ ok?: boolean; error?: string }>) {
    setError(null);
    start(async () => {
      const res = await fn();
      if (res && "error" in res) setError(String(res.error));
    });
  }

  const columns: Column<Speaker>[] = [
    {
      key: "name",
      header: "Speaker",
      sortable: true,
      render: (s) => (
        <div>
          <span className="font-medium text-text">{s.name}</span>
          {s.bio ? <p className="line-clamp-1 text-xs text-muted">{s.bio}</p> : null}
        </div>
      )
    },
    { key: "company", header: "Company", sortable: true, render: (s) => <span className="text-muted">{s.company || "—"}</span> },
    { key: "role", header: "Role", sortable: true, render: (s) => <span className="text-muted">{s.role || "—"}</span> },
    {
      // photo_key is a storage key, not a URL: GET /v1/storage/{key} needs a signed `sig` and always
      // returns application/octet-stream, and nothing mints a GET signature for a speaker photo — so
      // this reports whether one is set rather than pretending to render it.
      key: "photo_key",
      header: "Photo",
      render: (s) => (s.photo_key ? <Badge tone="neutral">set</Badge> : <span className="text-muted">—</span>)
    },
    {
      key: "lineup",
      header: "On this event",
      render: (s) =>
        onEvent.has(s.id) ? <Badge tone="success">on line-up</Badge> : <span className="text-muted">—</span>
    },
    {
      key: "actions",
      header: "",
      srHeader: "Row actions",
      align: "right",
      width: "17rem",
      render: (s) => (
        <div className="flex flex-wrap justify-end gap-1">
          <Button
            variant="secondary"
            
            disabled={pending}
            onClick={() => run(() => eventSpeakerAction(orgId, eventId, s.id, onEvent.has(s.id) ? "remove" : "assign"))}
          >
            {onEvent.has(s.id) ? "Remove from event" : "Add to event"}
          </Button>
          <Button variant="secondary"  onClick={() => setEditing(s)}>
            Edit
          </Button>
          <Button
            variant="ghost"
            aria-label={`Delete ${s.name}`}
            className="h-8 px-2 text-danger hover:bg-danger/10"
            onClick={() => {
              setError(null);
              setDeleting(s);
            }}
          >
            <Trash2 size={14} />
          </Button>
        </div>
      )
    }
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-text">{orgName}</p>
          <p className="text-xs text-muted">
            {roster.length} speaker{roster.length === 1 ? "" : "s"} · {lineup.length} on “{eventTitle}”
          </p>
        </div>
        <Button onClick={() => setCreating(true)}>
          <Plus size={15} /> New speaker
        </Button>
      </div>

      <SearchBar
        value={query}
        onChange={(v) => {
          setQuery(v);
          setPage(1);
        }}
        placeholder="Search name, company or role"
        aria-label="Search speakers"
      />

      {/* ConfirmDialog closes as soon as onConfirm returns and the transition resolves after it, so a
          refusal has to report here — inside the dialog it would render into a closed modal. */}
      {error ? (
        <Card>
          <p role="alert" className="text-sm text-danger">{error}</p>
        </Card>
      ) : null}

      <DataTable
        columns={columns}
        data={pageRows}
        keyField={(s) => s.id}
        loading={pending}
        sort={sort}
        onSortChange={setSort}
        caption={`Speaker roster for ${orgName}`}
        emptyState={
          <EmptyState
            icon={<Mic size={20} />}
            title={query ? "No matching speakers" : "No speakers yet"}
            message={
              query
                ? "Clear the search to see the whole roster."
                : "Create the first speaker for this organisation."
            }
          />
        }
      />

      <Pagination page={page} pageSize={PAGE_SIZE} total={filtered.length} onPageChange={setPage} />

      {creating ? <SpeakerDialog orgId={orgId} title="New speaker" onClose={closeCreate} /> : null}
      {editing ? (
        <SpeakerDialog orgId={orgId} title={`Edit ${editing.name}`} speaker={editing} onClose={closeEdit} />
      ) : null}

      <ConfirmDialog
        open={deleting !== null}
        onClose={() => setDeleting(null)}
        onConfirm={() => {
          if (deleting) run(() => deleteSpeakerAction(orgId, deleting.id));
        }}
        title={`Delete “${deleting?.name ?? ""}”?`}
        description="This is a hard delete. The speaker is removed from every event and session line-up they appear on."
        confirmLabel="Delete"
        tone="danger"
      />
    </div>
  );
}

function SubmitButton({ label }: { label: string }) {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Saving…" : label}</Button>;
}

/** Create and edit share one form. Text fields submit "" rather than being omitted, which is how a
 *  value gets cleared — the service only skips a field when it is null. */
function SpeakerDialog({
  orgId,
  title,
  speaker,
  onClose
}: {
  orgId: string;
  title: string;
  speaker?: Speaker;
  onClose: () => void;
}) {
  const boundAction = speaker
    ? updateSpeakerAction.bind(null, orgId, speaker.id)
    : createSpeakerAction.bind(null, orgId);
  const [state, action] = useFormState(boundAction, null);

  const succeeded = Boolean(state && "ok" in state);
  useEffect(() => {
    if (succeeded) onClose();
  }, [succeeded, onClose]);

  return (
    <Dialog open onClose={onClose} title={title}>
      <form action={action} className="space-y-3">
        <Field label="Name" htmlFor="sp-name">
          <Input id="sp-name" name="name" required minLength={2} maxLength={150} defaultValue={speaker?.name ?? ""} />
        </Field>

        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Company" htmlFor="sp-company">
            <Input id="sp-company" name="company" defaultValue={speaker?.company ?? ""} />
          </Field>
          <Field label="Role" htmlFor="sp-role">
            <Input id="sp-role" name="role" defaultValue={speaker?.role ?? ""} />
          </Field>
        </div>

        <Field label="Bio" htmlFor="sp-bio">
          <Textarea id="sp-bio" name="bio" rows={4} defaultValue={speaker?.bio ?? ""} />
        </Field>

        <Field label="Photo key" htmlFor="sp-photo" helper="Storage key — the console cannot render the image">
          <Input id="sp-photo" name="photoKey" defaultValue={speaker?.photo_key ?? ""} placeholder="e.g. orgs/…/speakers/…jpg" className="font-mono text-xs" />
        </Field>

        {state && "error" in state ? (
          <p role="alert" className="text-sm text-danger">{String(state.error)}</p>
        ) : null}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <SubmitButton label={speaker ? "Save" : "Create"} />
        </div>
      </form>
    </Dialog>
  );
}
