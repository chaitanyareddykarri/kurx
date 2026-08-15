import type { CertificateDashboard } from "@/lib/certificate-api";

/**
 * Certificate activity for an event (D-344, Phase 11).
 *
 * Two things this panel is careful about. **Live is the headline, not total** — counting withdrawn and
 * replaced certificates alongside good ones would tell an organiser they have three hundred valid
 * credentials out there when forty of them are void. And **verifications are the number that means
 * something**: it is the only signal that says the credential is being used by the people it was issued
 * for, rather than sitting in an inbox.
 *
 * What is deliberately absent: any count of downloads. Files go straight from object storage to the
 * browser, so the platform never observes one, and a zero that looks like a feature is worse than no
 * number at all.
 */
export function CertificateDashboardPanel({ eventId, data }: {
  eventId: string;
  data: CertificateDashboard;
}) {
  const peak = Math.max(1, ...data.recent_verifications.map((d) => d.count));

  return (
    <div className="space-y-6">
      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label="Valid certificates" value={data.live} emphasis />
        <Stat label="Times verified" value={data.verifications} emphasis />
        <Stat label="Emailed" value={data.sent} />
        <Stat label="Opened by recipients" value={data.views} />
      </dl>

      {(data.revoked > 0 || data.superseded > 0 || data.failed_delivery > 0 || data.no_destination > 0) && (
        <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {data.revoked > 0 && <Stat label="Withdrawn" value={data.revoked} tone="bad" />}
          {data.superseded > 0 && <Stat label="Replaced" value={data.superseded} />}
          {data.failed_delivery > 0 && (
            <Stat label="Failed to send" value={data.failed_delivery} tone="bad" />
          )}
          {/* Said on the face of it: these people can never be emailed theirs. */}
          {data.no_destination > 0 && (
            <Stat label="No email address" value={data.no_destination} tone="warn" />
          )}
        </dl>
      )}

      <section>
        <h3 className="text-sm font-semibold text-slate-900">Verifications, last 30 days</h3>
        {data.verifications === 0 ? (
          <p className="mt-2 text-sm text-slate-600">
            Nobody has checked one of these certificates yet. That is normal until recipients start
            sharing them.
          </p>
        ) : (
          <div className="mt-3 flex h-24 items-end gap-1" role="img"
               aria-label={`${data.verifications} verifications over the last 30 days`}>
            {data.recent_verifications.map((day) => (
              <div
                key={day.day}
                title={`${day.day}: ${day.count}`}
                style={{ height: `${Math.max(2, (day.count / peak) * 100)}%` }}
                className={`flex-1 rounded-t ${day.count > 0 ? "bg-slate-900" : "bg-slate-200"}`}
              />
            ))}
          </div>
        )}
      </section>

      <div className="flex flex-wrap gap-3 text-sm">
        {/* A plain link, not a fetch: the browser downloads it with the filename the server chose. */}
        <a
          href={`/api/events/${eventId}/certificates/export`}
          className="rounded-md border border-slate-300 px-3 py-1.5"
        >
          Download the full record (CSV)
        </a>
      </div>
    </div>
  );
}

function Stat({ label, value, emphasis, tone }: {
  label: string;
  value: number;
  emphasis?: boolean;
  tone?: "bad" | "warn";
}) {
  const colour =
    tone === "bad" ? "text-red-700" : tone === "warn" ? "text-amber-800" : "text-slate-900";

  return (
    <div className={`rounded-md border p-3 ${emphasis ? "border-slate-300" : "border-slate-200"}`}>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className={`text-2xl font-semibold ${colour}`}>{value.toLocaleString()}</dd>
    </div>
  );
}
