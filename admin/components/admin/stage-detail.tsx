"use client";

import { useState, useTransition } from "react";
import { CalendarClock, ListOrdered, Trophy, UserMinus, Users } from "lucide-react";
import {
  Badge, Button, Card, ConfirmDialog, DataTable, EmptyState, SectionHeader, StatCard, type Column
} from "@kurx/ui";
import {
  removeParticipantAction, seedParticipantsAction, stageResultsAction
} from "@/lib/admin-actions";
import type { Fixture, Stage, StageParticipant, StageResult } from "@/lib/api";

const resultTone = { Published: "success", Provisional: "muted", Disputed: "danger", Corrected: "accent" } as const;
const fixtureTone = { Complete: "success", Live: "accent", Scheduled: "muted", Disputed: "danger", Walkover: "neutral", Abandoned: "neutral" } as const;

/** Pinned locale + UTC: `toLocaleString("en-IN")` with neither is resolved by the runtime, and the SSR
 *  runtime is not the browser (container UTC vs the operator's machine), which is a hydration
 *  mismatch. UTC is stated in the label rather than silently applied. */
function utc(iso: string | null): string {
  return iso ? new Date(iso).toLocaleString("en-GB", { timeZone: "UTC" }) : "—";
}

export function StageDetail({
  stage,
  participants,
  fixtures,
  results
}: {
  stage: Stage;
  participants: StageParticipant[];
  fixtures: Fixture[];
  results: StageResult[];
}) {
  const [notice, setNotice] = useState<{ tone: "ok" | "error"; text: string } | null>(null);
  const [confirm, setConfirm] = useState<null | { op: "compute" | "publish" | "advance" | "seed"; title: string; body: string }>(null);
  const [removing, setRemoving] = useState<StageParticipant | null>(null);
  const [pending, start] = useTransition();

  function run(fn: () => Promise<{ ok?: boolean; error?: string; added?: number; count?: number }>, ok: (n: number) => string) {
    setNotice(null);
    start(async () => {
      const res = await fn();
      if (res && "error" in res) setNotice({ tone: "error", text: String(res.error) });
      else setNotice({ tone: "ok", text: ok(res.added ?? res.count ?? 0) });
    });
  }

  const published = results.filter((r) => r.state === "Published" || r.state === "Corrected").length;

  const rosterColumns: Column<StageParticipant>[] = [
    { key: "seed", header: "Seed", align: "right", width: "4rem", render: (p) => p.seed ?? "—" },
    {
      key: "subjectName",
      header: "Participant",
      render: (p) => (
        <div>
          <span className="font-medium text-text">{p.subjectName}</span>
          {/* The id is kept alongside the resolved name for cross-referencing and traceability. */}
          <p className="font-mono text-[11px] text-muted">{p.subjectId}</p>
        </div>
      )
    },
    { key: "subjectType", header: "Type", render: (p) => <Badge tone="neutral">{p.subjectType}</Badge> },
    {
      key: "advanced",
      header: "Advanced",
      render: (p) => (p.advanced ? <Badge tone="success">advanced in</Badge> : <span className="text-muted">—</span>)
    },
    {
      key: "actions",
      header: "",
      srHeader: "Row actions",
      align: "right",
      width: "6rem",
      render: (p) => (
        <Button
          variant="ghost"
          aria-label={`Remove ${p.subjectName}`}
          className="h-8 px-2 text-danger hover:bg-danger/10"
          onClick={() => {
            setNotice(null);
            setRemoving(p);
          }}
        >
          <UserMinus size={14} />
        </Button>
      )
    }
  ];

  const resultColumns: Column<StageResult>[] = [
    { key: "rank", header: "#", align: "right", width: "3.5rem" },
    {
      key: "subjectName",
      header: "Subject",
      render: (r) => (
        <div>
          <span className="font-medium text-text">{r.subjectName}</span>
          <p className="font-mono text-[11px] text-muted">{r.subjectId}</p>
        </div>
      )
    },
    { key: "subjectType", header: "Type", render: (r) => <Badge tone="neutral">{r.subjectType}</Badge> },
    { key: "finalScore", header: "Score", align: "right", render: (r) => r.finalScore ?? "—" },
    {
      key: "state",
      header: "State",
      render: (r) => <Badge tone={resultTone[r.state as keyof typeof resultTone] ?? "neutral"}>{r.state}</Badge>
    },
    {
      key: "correctionCount",
      header: "Corrections",
      align: "right",
      render: (r) => (r.correctionCount > 0 ? r.correctionCount : "—")
    },
    { key: "publishedAt", header: "Published (UTC)", render: (r) => <span className="text-xs text-muted">{utc(r.publishedAt)}</span> }
  ];

  const fixtureColumns: Column<Fixture>[] = [
    { key: "roundNo", header: "Round", align: "right", width: "4.5rem" },
    { key: "label", header: "Label", render: (f) => f.label ?? "—" },
    {
      key: "state",
      header: "State",
      render: (f) => <Badge tone={fixtureTone[f.state as keyof typeof fixtureTone] ?? "neutral"}>{f.state}</Badge>
    },
    // Count, not names: FixtureView reuses the *input* record FixtureSubjectInput, which carries no
    // subjectName — rendering raw GUIDs would be worse. Tracked in docs/roadmap/README.md.
    { key: "participants", header: "Entrants", align: "right", render: (f) => f.participants.length },
    { key: "officials", header: "Officials", align: "right", render: (f) => f.officials.length },
    { key: "slotStart", header: "Slot (UTC)", render: (f) => <span className="text-xs text-muted">{utc(f.slotStart)}</span> }
  ];

  return (
    <div className="space-y-6">
      <div className="grid gap-3 sm:grid-cols-3">
        <StatCard label="Roster" value={participants.length} icon={<Users size={16} />} />
        <StatCard label="Fixtures" value={fixtures.length} icon={<CalendarClock size={16} />} />
        <StatCard label="Results published" value={`${published} / ${results.length}`} icon={<Trophy size={16} />} />
      </div>

      {notice ? (
        <Card>
          <p role={notice.tone === "ok" ? "status" : "alert"} className={`text-sm ${notice.tone === "ok" ? "text-success" : "text-danger"}`}>
            {notice.text}
          </p>
        </Card>
      ) : null}

      <div className="flex flex-wrap gap-2">
        <Button
          variant="secondary"
          disabled={pending}
          onClick={() => setConfirm({
            op: "seed",
            title: "Seed the roster from registered teams?",
            body: "Adds every eligible competition team on this event that is not already on the stage. Idempotent — running it twice adds nothing."
          })}
        >
          Seed from registered
        </Button>
        <Button
          variant="secondary"
          disabled={pending}
          onClick={() => setConfirm({
            op: "compute",
            title: "Compute results?",
            body: "Deterministic aggregation over submitted scores and votes. Refused once results are published, and it replaces any provisional set."
          })}
        >
          Compute results
        </Button>
        <Button
          disabled={pending}
          onClick={() => setConfirm({
            op: "publish",
            title: "Publish results?",
            body: "Publishing makes the leaderboard visible per this stage's results-visibility rule, and certificates and prerequisites read published results."
          })}
        >
          Publish results
        </Button>
        <Button
          variant="secondary"
          disabled={pending}
          onClick={() => setConfirm({
            op: "advance",
            title: "Advance to the next stage?",
            body: "Seeds the next stage from this one's published set. Refused for Manual advancement, when no next stage exists, or before results are published."
          })}
        >
          Advance
        </Button>
      </div>

      <section className="space-y-2">
        <SectionHeader title={`Roster (${participants.length})`} />
        <DataTable
          columns={rosterColumns}
          data={participants}
          keyField={(p) => p.id}
          loading={pending}
          caption={`Roster for ${stage.name}`}
          emptyState={
            <EmptyState icon={<Users size={20} />} title="No participants"
              message="Seed from registered teams, or add competitors as they advance from an earlier stage." />
          }
        />
      </section>

      <section className="space-y-2">
        <SectionHeader title={`Leaderboard (${results.length})`} />
        <DataTable
          columns={resultColumns}
          data={results}
          keyField={(r) => r.id}
          loading={pending}
          caption={`Results for ${stage.name}`}
          emptyState={
            <EmptyState icon={<ListOrdered size={20} />} title="No results yet"
              message="Compute results once scores or votes have been submitted." />
          }
        />
      </section>

      <section className="space-y-2">
        <SectionHeader title={`Fixtures (${fixtures.length})`} />
        <DataTable
          columns={fixtureColumns}
          data={fixtures}
          keyField={(f) => f.id}
          caption={`Fixtures for ${stage.name}`}
          emptyState={
            <EmptyState icon={<CalendarClock size={20} />} title="No fixtures"
              message="Fixtures are scheduled by the organiser. Creating them here is deferred until the fixture contract exposes participant names." />
          }
        />
      </section>

      <ConfirmDialog
        open={confirm !== null}
        onClose={() => setConfirm(null)}
        onConfirm={() => {
          if (!confirm) return;
          if (confirm.op === "seed") run(() => seedParticipantsAction(stage.id), (n) => `Seeded ${n} participant${n === 1 ? "" : "s"}.`);
          else if (confirm.op === "compute") run(() => stageResultsAction(stage.id, "compute"), (n) => `Computed ${n} result row${n === 1 ? "" : "s"}.`);
          else if (confirm.op === "publish") run(() => stageResultsAction(stage.id, "publish"), (n) => `Published ${n} result row${n === 1 ? "" : "s"}.`);
          else run(() => stageResultsAction(stage.id, "advance"), (n) => `Advanced ${n} participant${n === 1 ? "" : "s"}.`);
        }}
        title={confirm?.title ?? ""}
        description={confirm?.body ?? ""}
        confirmLabel="Confirm"
        tone={confirm?.op === "publish" || confirm?.op === "advance" ? "danger" : "default"}
      />

      <ConfirmDialog
        open={removing !== null}
        onClose={() => setRemoving(null)}
        onConfirm={() => {
          if (removing) run(() => removeParticipantAction(removing.id), () => "Participant removed.");
        }}
        title={`Remove ${removing?.subjectName ?? ""} from the roster?`}
        description="They can be re-added by seeding or by advancement."
        confirmLabel="Remove"
        tone="danger"
      />
    </div>
  );
}
