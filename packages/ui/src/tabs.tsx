"use client";

import { ReactNode, useRef } from "react";

/**
 * An underline tab strip implementing the WAI-ARIA tabs pattern.
 *
 * **Phase 18A correction.** This declared `role="tablist"` and `role="tab"` and
 * then delivered none of what those roles promise. Declaring the role is a
 * contract with assistive tech: it announces "tab, 1 of 6" and the user reaches
 * for the arrow keys. Nothing happened — every tab sat in the tab order at
 * `tabIndex` 0 (the opposite of the required roving tabindex), no tab pointed at
 * a panel, and no panel pointed back. A role that lies is worse than no role,
 * because it is the role that sets the expectation.
 *
 * Now: roving tabindex, Left/Right/Home/End navigation with focus following
 * selection, a visible focus ring (there was none), and a 44px-tall target.
 *
 * Panel wiring is opt-in via `id`. Supply it together with `TabPanel` and the
 * two are cross-referenced; omit it and the strip still gets keyboard support
 * but emits no dangling `aria-controls`. Admin's two call sites are on the
 * keyboard-only path until Phases 28/29 give them panels to point at.
 */
export function Tabs({
  tabs,
  value,
  onChange,
  id,
  trailing
}: {
  tabs: { id: string; label: string }[];
  value: string;
  onChange: (id: string) => void;
  /** Id base shared with `TabPanel`. Enables `aria-controls` / `aria-labelledby`. */
  id?: string;
  /**
   * A control parked at the end of the strip, on the same baseline — an overflow menu for the tabs
   * that did not earn a permanent slot (D-381), which is the only reason it exists.
   *
   * Rendered as a SIBLING of the tablist, never inside it: `role="tablist"` promises its children are
   * tabs, and a menu button among them makes assistive tech miscount ("tab, 6 of 10") and puts a
   * non-tab in the arrow-key path. Omit it and the markup is exactly what it was.
   */
  trailing?: ReactNode;
}) {
  const strip = useRef<HTMLDivElement>(null);

  /**
   * Automatic activation (the APG default for panels that are cheap to render):
   * arrowing selects. Focus has to be moved explicitly — the newly selected tab
   * is the only one with `tabIndex` 0, and without this the browser leaves focus
   * on a button that has just become unreachable.
   */
  function move(nextIndex: number) {
    const next = tabs[nextIndex];
    if (!next) return;
    onChange(next.id);
    strip.current?.querySelectorAll<HTMLButtonElement>('[role="tab"]')[nextIndex]?.focus();
  }

  function onKeyDown(event: React.KeyboardEvent) {
    const current = tabs.findIndex((t) => t.id === value);
    if (current === -1) return;
    switch (event.key) {
      case "ArrowRight":
        move((current + 1) % tabs.length);
        break;
      case "ArrowLeft":
        move((current - 1 + tabs.length) % tabs.length);
        break;
      case "Home":
        move(0);
        break;
      case "End":
        move(tabs.length - 1);
        break;
      default:
        return;
    }
    // Only after a handled key: Home/End otherwise still scroll the page, and an
    // unhandled arrow must stay available to whatever is listening above.
    event.preventDefault();
  }

  const tablist = (
    <div
      ref={strip}
      role="tablist"
      onKeyDown={onKeyDown}
      className={`flex gap-1 overflow-x-auto ${trailing ? "min-w-0 flex-1" : "border-b border-border"}`}
    >
      {tabs.map((tab) => {
        const active = tab.id === value;
        return (
          <button
            key={tab.id}
            type="button"
            role="tab"
            id={id ? `${id}-tab-${tab.id}` : undefined}
            aria-selected={active}
            aria-controls={id ? `${id}-panel-${tab.id}` : undefined}
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(tab.id)}
            className={`-mb-px inline-flex min-h-11 shrink-0 items-center border-b-2 px-4 text-sm font-semibold transition duration-fast focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent ${
              active ? "border-accent text-text" : "border-transparent text-muted hover:text-text"
            }`}
          >
            {tab.label}
          </button>
        );
      })}
    </div>
  );

  // The underline is the strip's own when it stands alone, and the wrapper's when something sits
  // beside it — otherwise the rule stops short of the trailing control and reads as a broken border.
  if (!trailing) return tablist;
  return (
    <div className="flex items-stretch border-b border-border">
      {tablist}
      <div className="flex shrink-0 items-center pl-2">{trailing}</div>
    </div>
  );
}

/**
 * The panel a `Tabs` tab controls. `tabIndex={0}` is deliberate: when a panel
 * holds no focusable element, it must be reachable itself or Tab from the strip
 * skips the content entirely.
 */
export function TabPanel({
  tabsId,
  id,
  active,
  children,
  className = ""
}: {
  /** Must match the `id` given to `Tabs`. */
  tabsId: string;
  /** Must match the tab's `id`. */
  id: string;
  active: boolean;
  children: ReactNode;
  className?: string;
}) {
  if (!active) return null;
  return (
    <div
      role="tabpanel"
      id={`${tabsId}-panel-${id}`}
      aria-labelledby={`${tabsId}-tab-${id}`}
      tabIndex={0}
      className={`focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${className}`}
    >
      {children}
    </div>
  );
}
