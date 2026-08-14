export function KurxLogo({ compact = false }: { compact?: boolean }) {
  return (
    <div className="flex items-center gap-2" aria-label="Kurx">
      <span className="grid h-7 w-7 place-items-center rounded-md border border-border bg-elevated text-sm font-bold text-accent">
        K
      </span>
      {!compact ? <span className="text-base font-semibold tracking-normal text-text">Kurx</span> : null}
    </div>
  );
}
