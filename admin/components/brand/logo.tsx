/** Kurx admin wordmark — same mark as web, tagged "Admin" so an operator always knows
 *  they're in the internal console, not the public app. */
export function KurxAdminLogo({ compact = false }: { compact?: boolean }) {
  return (
    <div className="flex items-center gap-2" aria-label="Kurx Admin">
      <span className="grid h-7 w-7 place-items-center rounded-md border border-border bg-elevated text-sm font-bold text-accent-text">
        K
      </span>
      {!compact ? (
        <span className="flex items-baseline gap-1.5">
          <span className="text-base font-semibold tracking-normal text-text">Kurx</span>
          <span className="rounded border border-accent/40 bg-accent/10 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-accent-text">
            Admin
          </span>
        </span>
      ) : null}
    </div>
  );
}
