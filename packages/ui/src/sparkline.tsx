"use client";

import { useId } from "react";

type DayCount = { date: string; count: number };

const VIEW_W = 300;
const VIEW_H = 80;
const PAD = 4;

function points(data: DayCount[]) {
  const max = Math.max(1, ...data.map((d) => d.count));
  const step = data.length > 1 ? (VIEW_W - PAD * 2) / (data.length - 1) : 0;
  return data.map((d, i) => ({
    x: PAD + i * step,
    y: PAD + (1 - d.count / max) * (VIEW_H - PAD * 2),
    ...d
  }));
}

/** Dependency-free line/area chart for a day-count series (signups, events, …). No charting
 *  library exists in this monorepo and none is warranted for a single trend line — this is the
 *  full extent of what a KPI sparkline needs. */
export function Sparkline({ data, className = "" }: { data: DayCount[]; className?: string }) {
  const gradientId = useId();
  if (data.length === 0) return <div className={`h-20 ${className}`} />;

  const pts = points(data);
  const line = pts.map((p) => `${p.x},${p.y}`).join(" ");
  const area = `${PAD},${VIEW_H - PAD} ${line} ${VIEW_W - PAD},${VIEW_H - PAD}`;
  const total = data.reduce((sum, d) => sum + d.count, 0);

  return (
    <svg
      viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}
      preserveAspectRatio="none"
      className={`h-20 w-full overflow-visible ${className}`}
      role="img"
      aria-label={`Trend over ${data.length} days, ${total} total`}
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="rgb(var(--color-accent))" stopOpacity="0.35" />
          <stop offset="100%" stopColor="rgb(var(--color-accent))" stopOpacity="0" />
        </linearGradient>
      </defs>
      <polygon points={area} fill={`url(#${gradientId})`} />
      <polyline
        points={line}
        fill="none"
        stroke="rgb(var(--color-accent))"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
      />
      {pts.map((p) => (
        <circle key={p.date} cx={p.x} cy={p.y} r="7" fill="transparent">
          <title>{`${p.date}: ${p.count}`}</title>
        </circle>
      ))}
    </svg>
  );
}

/** Dependency-free bar chart for a day-count series. Promoted from the inline version that used
 *  to live in admin's analytics page, restyled with a gradient fill and rounded caps so it reads
 *  as one system with `Sparkline`. */
export function MiniBars({ data, className = "" }: { data: DayCount[]; className?: string }) {
  const max = Math.max(1, ...data.map((d) => d.count));
  return (
    <div className={`flex h-20 items-end gap-0.5 ${className}`}>
      {data.map((d) => (
        <div
          key={d.date}
          title={`${d.date}: ${d.count}`}
          className="flex-1 rounded-t-sm bg-gradient-to-t from-accent/90 to-accent/40 transition hover:from-accent hover:to-accent/60"
          style={{ height: `${(d.count / max) * 100}%`, minHeight: d.count > 0 ? "2px" : undefined }}
        />
      ))}
    </div>
  );
}
