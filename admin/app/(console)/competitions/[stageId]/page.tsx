import Link from "next/link";
import { redirect } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { Badge, Card } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import {
  getStage, listStageParticipants, listFixtures, getStageResults,
  apiErrorMessage, apiErrorStatus
} from "@/lib/api";
import { StageDetail } from "@/components/admin/stage-detail";

// One stage: roster, fixtures, leaderboard, and the lifecycle actions.
// Roster and results carry a server-resolved `subjectName` (added 2026-07-25) — before that these
// tables could only have shown raw GUIDs, which is why this screen waited on the contract change.
export default async function StageDetailPage({
  params,
  searchParams
}: {
  params: { stageId: string };
  searchParams: { eventId?: string; q?: string };
}) {
  const session = await requireStaffSession();
  if (!session.roles.includes("SuperAdmin")) redirect("/");

  const backHref = searchParams.eventId
    ? `/competitions?eventId=${searchParams.eventId}&q=${encodeURIComponent(searchParams.q ?? "")}`
    : "/competitions";

  try {
    const stage = await getStage(session.accessToken, params.stageId);
    // Independent reads; a stage with no fixtures or no computed results is normal, not an error.
    const [participants, fixtures, results] = await Promise.all([
      listStageParticipants(session.accessToken, params.stageId),
      listFixtures(session.accessToken, params.stageId),
      getStageResults(session.accessToken, params.stageId)
    ]);

    return (
      <div className="space-y-6">
        <nav aria-label="Breadcrumb">
          <Link href={backHref} className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-text">
            <ArrowLeft size={15} /> Competitions
          </Link>
        </nav>

        <header>
          <p className="text-sm font-semibold text-accent-text">Events</p>
          <div className="mt-1 flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-semibold text-text">{stage.name}</h1>
            <Badge tone={stage.state === "Live" ? "success" : stage.state === "Draft" ? "muted" : "neutral"}>
              {stage.state}
            </Badge>
          </div>
          <p className="mt-1 text-sm text-muted">
            Stage {stage.sequence} · {stage.format} · source {stage.participantSource} · advancement{" "}
            {stage.advancementRule}
            {stage.advancementThreshold !== null ? ` (${stage.advancementThreshold})` : ""} · results{" "}
            {stage.resultsVisibility}
          </p>
        </header>

        <StageDetail stage={stage} participants={participants} fixtures={fixtures} results={results} />
      </div>
    );
  } catch (err) {
    const status = apiErrorStatus(err);
    return (
      <div className="space-y-4">
        <nav aria-label="Breadcrumb">
          <Link href={backHref} className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-text">
            <ArrowLeft size={15} /> Competitions
          </Link>
        </nav>
        <h1 className="text-2xl font-semibold text-text">Stage</h1>
        <Card>
          {status === 403 ? (
            <p className="text-sm text-muted">You don&apos;t have access to this competition.</p>
          ) : status === 404 ? (
            <p className="text-sm text-muted">This stage no longer exists.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load this stage: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
