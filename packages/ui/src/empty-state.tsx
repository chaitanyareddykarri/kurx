import { ReactNode } from "react";
import { Button } from "./button";

/**
 * Friendly empty state used across data screens — never a bare "No data".
 *
 * Takes either a rendered `action` node or the `actionLabel`/`onAction` pair.
 * The callback form alone made this unusable from a Server Component, which is
 * the default in `web/` — so a server-rendered empty state could offer the user
 * no way out at all, which is precisely what an empty state is for.
 */
export function EmptyState({
  icon,
  title,
  message,
  action,
  actionLabel,
  onAction
}: {
  icon: ReactNode;
  title: string;
  message?: string;
  /** A rendered control — use from Server Components, where a callback cannot cross the boundary. */
  action?: ReactNode;
  actionLabel?: string;
  onAction?: () => void;
}) {
  /*
   * A bare string icon is dropped.
   *
   * `ReactNode` accepts a string, so seven call sites across web passed a Material Symbols *name*
   * — `icon="receipt_long"`, `"how_to_reg"`, `"forum"`, `"article"`, `"chat"`, `"drafts"` — from a
   * font this project does not load. TypeScript was happy and each one rendered the identifier as
   * visible text above the heading: users read "receipt_long" where an icon belonged.
   *
   * Rendering nothing is strictly better than rendering an identifier, and it fails visibly enough
   * in review to get a real icon passed. Phases 19–21 replace the names as they reach those screens.
   */
  const glyph = typeof icon === "string" ? null : icon;

  return (
    <div className="flex flex-col items-center justify-center gap-md px-xl py-3xl text-center">
      {glyph ? (
        <div aria-hidden="true" className="text-muted">
          {glyph}
        </div>
      ) : null}
      <h3 className="text-h3 text-text">{title}</h3>
      {message ? <p className="max-w-md text-body text-muted">{message}</p> : null}
      {action ?? (actionLabel && onAction ? (
        <Button variant="secondary" onClick={onAction} className="mt-2">
          {actionLabel}
        </Button>
      ) : null)}
    </div>
  );
}
