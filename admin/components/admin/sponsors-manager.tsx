"use client";

import { useCallback, useEffect, useMemo, useState, useTransition } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { Handshake, Plus, Trash2 } from "lucide-react";
import {
  Badge, Button, Card, ConfirmDialog, DataTable, Dialog, EmptyState, Field, FilterBar, FilterSelect,
  Input, Pagination, SearchBar, Select, type Column, type SortState
} from "@kurx/ui";
import {
  createSponsorAction, deleteSponsorAction, eventSponsorAction, updateSponsorAction
} from "@/lib/admin-actions";
import type { Sponsor } from "@/lib/api";

const PAGE_SIZE = 20;

// Mirrors SponsorTier. The API emits tier lowercase (ToLowerInvariant) and parses input
// case-insensitively, so lowercase values round-trip safely. Declared here, not imported from
// lib/api: a *value* import from that module drags axios and every zod schema into this bundle.
const TIERS = ["platinum", "gold", "silver", "bronze", "partner"];
const tierTone: Record<string, "accent" | "success" | "neutral" | "muted"> = {
  platinum: "accent", gold: "success", silver: "neutral", bronze: "neutral", partner: "muted"
};

export function SponsorsManager({
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
  roster: Sponsor[];
  lineup: Sponsor[];
}) {
  const [query, setQuery] = useState("");
  const [tier, setTier] = useState("");
  const [sort, setSort] = useState<SortState>({ key: "tier", dir: "asc" });
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Sponsor | null>(null);
  const [deleting, setDeleting] = useState<Sponsor | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, start] = useTransition();

  const closeCreate = useCallback(() => setCreating(false), []);
  const closeEdit = useCallback(() => setEditing(null), []);

  const onEvent = useMemo(() => new Set(lineup.map((s) => s.id)), [lineup]);

  // The endpoint returns the whole roster ordered by tier then priority with no paging/search params,
  // so these are computed over the full set rather than faked against a partial page.
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const rows = roster.filter(
      (s) =>
        (!tier || s.tier.toLowerCase() === tier) &&
        (!q || s.name.toLowerCase().includes(q) || (s.website ?? "").toLowerCase().includes(q))
    );
    const dir = sort.dir === "asc" ? 1 : -1;
    return [...rows].sort((a, b) => {
      if (sort.key === "priority") return (a.priority - b.priority) * dir;
      // Tier sorts by declared rank (platinum first), not alphabetically.
      if (sort.key === "tier") {
        const d = TIERS.indexOf(a.tier.toLowerCase()) - TIERS.indexOf(b.tier.toLowerCase());
        return (d !== 0 ? d : a.priority - b.priority) * dir;
      }
      return a.name.localeCompare(b.name) * dir;
    });
  }, [roster, query, tier, sort]);

  const pageRows = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const activeFilters = (query ? 1 : 0) + (tier ? 1 : 0);

  function run(fn: () => Promise<{ ok?: boolean; error?: string }>) {
    setError(null);
    start(async () => {
      const res = await fn();
      if (res && "error" in res) setError(String(res.error));
    });
  }

  const columns: Column<Sponsor>[] = [
    {
      key: "name",
      header: "Sponsor",
      sortable: true,
      render: (s) => (
        <div>
          <span className="font-medium text-text">{s.name}</span>
          {s.website ? <p className="line-clamp-1 text-xs text-muted">{s.website}</p> : null}
        </div>
      )
    },
    {
      key: "tier",
      header: "Tier",
      sortable: true,
      render: (s) => <Badge tone={tierTone[s.tier.toLowerCase()] ?? "neutral"}>{s.tier}</Badge>
    },
    { key: "priority", header: "Priority", sortable: true, align: "right" },
    {
      // logo_key is a storage key, not a URL: GET /v1/storage/{key} needs a signed `sig` and always
      // returns application/octet-stream, and nothing mints a GET signature for a sponsor logo — so
      // this reports whether one is set rather than pretending to render it.
      key: "logo_key",
      header: "Logo",
      render: (s) => (s.logo_key ? <Badge tone="neutral">set</Badge> : <span className="text-muted">—</span>)
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
            onClick={() => run(() => eventSponsorAction(orgId, eventId, s.id, onEvent.has(s.id) ? "remove" : "assign"))}
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
            {roster.length} sponsor{roster.length === 1 ? "" : "s"} · {lineup.length} on “{eventTitle}”
          </p>
        </div>
        <Button onClick={() => setCreating(true)}>
          <Plus size={15} /> New sponsor
        </Button>
      </div>

      <FilterBar
        activeCount={activeFilters}
        onClear={() => {
          setQuery("");
          setTier("");
          setPage(1);
        }}
      >
        <SearchBar
          value={query}
          onChange={(v) => {
            setQuery(v);
            setPage(1);
          }}
          placeholder="Search name or website"
          aria-label="Search sponsors"
        />
        <FilterSelect
          label="Tier"
          value={tier}
          onChange={(e) => {
            setTier(e.target.value);
            setPage(1);
          }}
          options={[{ value: "", label: "All tiers" }, ...TIERS.map((t) => ({ value: t, label: t }))]}
        />
      </FilterBar>

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
        caption={`Sponsor roster for ${orgName}`}
        emptyState={
          <EmptyState
            icon={<Handshake size={20} />}
            title={activeFilters ? "No matching sponsors" : "No sponsors yet"}
            message={
              activeFilters
                ? "Clear the filters to see the whole roster."
                : "Create the first sponsor for this organisation."
            }
          />
        }
      />

      <Pagination page={page} pageSize={PAGE_SIZE} total={filtered.length} onPageChange={setPage} />

      {creating ? <SponsorDialog orgId={orgId} title="New sponsor" onClose={closeCreate} /> : null}
      {editing ? (
        <SponsorDialog orgId={orgId} title={`Edit ${editing.name}`} sponsor={editing} onClose={closeEdit} />
      ) : null}

      <ConfirmDialog
        open={deleting !== null}
        onClose={() => setDeleting(null)}
        onConfirm={() => {
          if (deleting) run(() => deleteSponsorAction(orgId, deleting.id));
        }}
        title={`Delete “${deleting?.name ?? ""}”?`}
        description="This is a hard delete. The sponsor is removed from every event line-up it appears on."
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

function SponsorDialog({
  orgId,
  title,
  sponsor,
  onClose
}: {
  orgId: string;
  title: string;
  sponsor?: Sponsor;
  onClose: () => void;
}) {
  const boundAction = sponsor
    ? updateSponsorAction.bind(null, orgId, sponsor.id)
    : createSponsorAction.bind(null, orgId);
  const [state, action] = useFormState(boundAction, null);

  const succeeded = Boolean(state && "ok" in state);
  useEffect(() => {
    if (succeeded) onClose();
  }, [succeeded, onClose]);

  return (
    <Dialog open onClose={onClose} title={title}>
      <form action={action} className="space-y-3">
        <Field label="Name" htmlFor="sn-name">
          <Input id="sn-name" name="name" required minLength={2} maxLength={150} defaultValue={sponsor?.name ?? ""} />
        </Field>

        <Field label="Website" htmlFor="sn-site">
          <Input id="sn-site" name="website" type="url" defaultValue={sponsor?.website ?? ""} placeholder="https://example.com" />
        </Field>

        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Tier" htmlFor="sn-tier">
            <Select id="sn-tier" name="tier" defaultValue={sponsor?.tier.toLowerCase() ?? "partner"}>
              {TIERS.map((t) => <option key={t} value={t}>{t}</option>)}
            </Select>
          </Field>
          <Field label="Priority" htmlFor="sn-priority" helper="Lower shows first">
            <Input id="sn-priority" name="priority" type="number" defaultValue={sponsor?.priority ?? 0} />
          </Field>
        </div>

        <Field label="Logo key" htmlFor="sn-logo" helper="Storage key — the console cannot render the image">
          <Input id="sn-logo" name="logoKey" defaultValue={sponsor?.logo_key ?? ""} placeholder="e.g. orgs/…/sponsors/…png" className="font-mono text-xs" />
        </Field>

        {state && "error" in state ? (
          <p role="alert" className="text-sm text-danger">{String(state.error)}</p>
        ) : null}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <SubmitButton label={sponsor ? "Save" : "Create"} />
        </div>
      </form>
    </Dialog>
  );
}
