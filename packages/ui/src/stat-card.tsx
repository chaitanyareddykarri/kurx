import { ReactNode } from "react";
import Link from "next/link";
import { ArrowDownRight, ArrowUpRight } from "lucide-react";
import { Skeleton } from "./skeleton";

type StatCardProps = {
  label: string;
  value: ReactNode;
  icon?: ReactNode;
  /** Percentage or delta; `positive` picks the tone/arrow. */
  delta?: { value: string; positive?: boolean };
  /** Turns the whole card into a link to a drill-down screen. */
  href?: string;
  loading?: boolean;
  className?: string;
};

/** Dashboard KPI tile. Read-only; drill-down via `href`. Aggregates come from a
 *  cached summary endpoint, never a hot-path SUM. */
export function StatCard({ label, value, icon, delta, href, loading = false, className = "" }: StatCardProps) {
  const body = (
    <div
      className={`rounded-lg border border-border bg-surface p-4 shadow-github transition ${
        href ? "hover:border-accent/70" : ""
      } ${className}`}
    >
      <div className="flex items-center justify-between text-muted">
        <span className="text-sm">{label}</span>
        {icon ? <span className="text-accent">{icon}</span> : null}
      </div>
      {loading ? (
        <Skeleton className="mt-3 h-8 w-24" />
      ) : (
        <div className="mt-2 flex items-end justify-between gap-3">
          <strong className="text-2xl text-text">{value}</strong>
          {delta ? (
            <span
              className={`inline-flex items-center gap-0.5 text-xs font-semibold ${
                delta.positive ? "text-success" : "text-danger"
              }`}
            >
              {delta.positive ? <ArrowUpRight size={13} /> : <ArrowDownRight size={13} />}
              {delta.value}
            </span>
          ) : null}
        </div>
      )}
    </div>
  );

  return href ? (
    <Link href={href} className="block">
      {body}
    </Link>
  ) : (
    body
  );
}
