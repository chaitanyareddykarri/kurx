"use client";

import { useCallback, useEffect, useState, useTransition } from "react";
import Link from "next/link";
import { useFormState, useFormStatus } from "react-dom";
import { Plus, Trophy } from "lucide-react";
import {
  Badge, Button, Card, ConfirmDialog, DataTable, Dialog, EmptyState, Field, Input, Select, type Column
} from "@kurx/ui";
import { createStageAction, deleteStageAction, transitionStageAction } from "@/lib/admin-actions";
import type { Stage } from "@/lib/api";

// Mirrors the backend enums (Kurx.Domain/Enums). Declared here rather than imported from lib/api:
// a *value* import from that module drags axios and every zod schema into this client bundle.
const FORMATS = ["SingleSubmission", "JuryReview", "Knockout", "DoubleElim", "RoundRobin", "Swiss", "League", "TimeTrial", "PublicVote"];
const SOURCES = ["AllRegistered", "AdvancedFrom", "Seeded", "Wildcard"];
const ADVANCEMENT = ["TopN", "TopPercent", "ScoreGte", "Manual"];
const VISIBILITY = ["Live", "OnStageClose", "OnEventClose"];

const stateTone = { Draft: "muted", Live: "success", Closed: "neutral" } as const;

/** The only transitions the backend accepts: Draft --open--> Live --close--> Closed. */
function nextAction(state: string): "open" | "close" | null {
  if (state === "Draft") return "open";
  if (state === "Live") return "close";
  return null;
}

export function StagesManager({
  eventId,
  eventTitle,
  stages,
  query
}: {
  eventId: string;
  eventTitle: string;
  stages: Stage[];
  query: string;
}) {
  const [creating, setCreating] = useState(false);
  const [deleting, setDeleting] = useState<Stage | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, start] = useTransition();

  const closeCreate = useCallback(() => setCreating(false), []);

  function run(fn: () => Promise<{ ok?: boolean; error?: string }>) {
    setError(null);
    start(async () => {
      const res = await fn();
      if (res && "error" in res) setError(String(res.error));
    });
  }

  const columns: Column<Stage>[] = [
    { key: "sequence", header: "#", align: "right", width: "3rem" },
    {
      key: "name",
      header: "Stage",
      render: (s) => (
        <div>
          <Link href={`/competitions/${s.id}?eventId=${eventId}&q=${encodeURIComponent(query)}`} className="font-medium text-text hover:text-accent-text">
            {s.name}
          </Link>
          <p className="text-xs text-muted">{s.format} · {s.participantSource}</p>
        </div>
      )
    },
    {
      key: "state",
      header: "State",
      render: (s) => <Badge tone={stateTone[s.state as keyof typeof stateTone] ?? "neutral"}>{s.state}</Badge>
    },
    { key: "participantCount", header: "Roster", align: "right" },
    {
      key: "advancementRule",
      header: "Advancement",
      render: (s) => (
        <span className="text-muted">
          {s.advancementRule}
          {s.advancementThreshold !== null ? ` (${s.advancementThreshold})` : ""}
        </span>
      )
    },
    { key: "resultsVisibility", header: "Results", render: (s) => <span className="text-muted">{s.resultsVisibility}</span> },
    {
      key: "actions",
      header: "",
      srHeader: "Row actions",
      align: "right",
      width: "16rem",
      render: (s) => {
        const act = nextAction(s.state);
        return (
          <div className="flex flex-wrap justify-end gap-1">
            {act ? (
              <Button
                variant="secondary"
                
                disabled={pending}
                onClick={() => run(() => transitionStageAction(s.id, act))}
              >
                {act === "open" ? "Open" : "Close"}
              </Button>
            ) : null}
            <Button
              variant="ghost"
              className="h-8 px-2 text-xs text-danger hover:bg-danger/10"
              onClick={() => {
                setError(null);
                setDeleting(s);
              }}
            >
              Delete
            </Button>
          </div>
        );
      }
    }
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-text">{eventTitle}</p>
          <p className="text-xs text-muted">{stages.length} stage{stages.length === 1 ? "" : "s"}</p>
        </div>
        <Button onClick={() => setCreating(true)}>
          <Plus size={15} /> New stage
        </Button>
      </div>

      {/* ConfirmDialog closes as soon as onConfirm returns and the transition resolves after that,
          so refusals (has_published_results / is_advancement_source / invalid_transition) report here. */}
      {error ? (
        <Card>
          <p role="alert" className="text-sm text-danger">{error}</p>
        </Card>
      ) : null}

      <DataTable
        columns={columns}
        data={stages}
        keyField={(s) => s.id}
        loading={pending}
        caption={`Competition stages for ${eventTitle}`}
        emptyState={
          <EmptyState
            icon={<Trophy size={20} />}
            title="No stages yet"
            message="Create the first stage to start running this competition."
          />
        }
      />

      {creating ? <StageDialog eventId={eventId} onClose={closeCreate} /> : null}

      <ConfirmDialog
        open={deleting !== null}
        onClose={() => setDeleting(null)}
        onConfirm={() => {
          if (deleting) run(() => deleteStageAction(deleting.id));
        }}
        title={`Delete stage “${deleting?.name ?? ""}”?`}
        description="Refused by the backend if the stage has published results or another stage advances from it."
        confirmLabel="Delete"
        tone="danger"
      />
    </div>
  );
}

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Creating…" : "Create"}</Button>;
}

function StageDialog({ eventId, onClose }: { eventId: string; onClose: () => void }) {
  const [state, action] = useFormState(createStageAction.bind(null, eventId), null);

  const succeeded = Boolean(state && "ok" in state);
  useEffect(() => {
    if (succeeded) onClose();
  }, [succeeded, onClose]);

  return (
    <Dialog open onClose={onClose} title="New stage">
      <form action={action} className="space-y-3">
        <Field label="Name" htmlFor="st-name">
          <Input id="st-name" name="name" required />
        </Field>

        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Sequence" htmlFor="st-seq">
            <Input id="st-seq" name="sequence" type="number" placeholder="auto" />
          </Field>
          <Field label="Format" htmlFor="st-format">
            <Select id="st-format" name="format">
              {FORMATS.map((f) => <option key={f} value={f}>{f}</option>)}
            </Select>
          </Field>
          <Field label="Participant source" htmlFor="st-source">
            <Select id="st-source" name="participantSource">
              {SOURCES.map((s) => <option key={s} value={s}>{s}</option>)}
            </Select>
          </Field>
          <Field label="Advancement rule" htmlFor="st-adv">
            <Select id="st-adv" name="advancementRule">
              {ADVANCEMENT.map((a) => <option key={a} value={a}>{a}</option>)}
            </Select>
          </Field>
          <Field label="Advancement threshold" htmlFor="st-thr">
            <Input id="st-thr" name="advancementThreshold" type="number" placeholder="e.g. 8 for TopN" />
          </Field>
          <Field label="Results visibility" htmlFor="st-vis">
            <Select id="st-vis" name="resultsVisibility">
              {VISIBILITY.map((v) => <option key={v} value={v}>{v}</option>)}
            </Select>
          </Field>
        </div>

        {state && "error" in state ? (
          <p role="alert" className="text-sm text-danger">{String(state.error)}</p>
        ) : null}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <SubmitButton />
        </div>
      </form>
    </Dialog>
  );
}
