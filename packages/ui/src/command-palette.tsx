"use client";

import { AnimatePresence, motion } from "framer-motion";
import { Search } from "lucide-react";
import { ReactNode, useEffect, useId, useMemo, useRef, useState } from "react";

import { useOverlay } from "./use-overlay";

export type CommandItem = {
  id: string;
  label: string;
  /** Grouping header, e.g. "Events", "Go to", "Actions". */
  group?: string;
  hint?: string;
  icon?: ReactNode;
  onSelect: () => void;
};

/**
 * The command / search overlay.
 *
 * Modal — it takes over the screen — so it uses `useOverlay` for the trap,
 * restore and scroll lock, unlike the transient `Menu`.
 *
 * The combobox pattern rather than a list of buttons: the input keeps focus the
 * whole time while `aria-activedescendant` moves the *virtual* cursor through the
 * results, so typing and navigating never fight each other. Results are announced
 * politely via a live count, because a silent list that changes under a screen
 * reader is worse than no list.
 */
export function CommandPalette({
  open,
  onClose,
  items,
  placeholder = "Search events, people and actions",
  emptyMessage = "No matches. Try a different word."
}: {
  open: boolean;
  onClose: () => void;
  items: CommandItem[];
  placeholder?: string;
  emptyMessage?: string;
}) {
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(0);
  const panelRef = useRef<HTMLDivElement>(null);
  const listId = useId();
  const inputId = useId();

  useOverlay({ open, onClose, panelRef });

  useEffect(() => {
    if (open) {
      setQuery("");
      setActive(0);
    }
  }, [open]);

  const results = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return items;
    return items.filter((i) => i.label.toLowerCase().includes(q) || i.group?.toLowerCase().includes(q));
  }, [items, query]);

  useEffect(() => setActive(0), [query]);

  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActive((a) => (results.length ? (a + 1) % results.length : 0));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActive((a) => (results.length ? (a - 1 + results.length) % results.length : 0));
    } else if (e.key === "Enter" && results[active]) {
      e.preventDefault();
      results[active].onSelect();
      onClose();
    }
  }

  let lastGroup: string | undefined;

  return (
    <AnimatePresence>
      {open ? (
        <motion.div
          className="fixed inset-0 z-modal flex items-start justify-center bg-black/60 p-4 pt-[10vh]"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          onClick={onClose}
        >
          <motion.div
            ref={panelRef}
            role="dialog"
            aria-modal="true"
            aria-label="Search"
            tabIndex={-1}
            initial={{ opacity: 0, y: -8, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -8, scale: 0.98 }}
            transition={{ duration: 0.14 }}
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-xl overflow-hidden rounded-xl border border-border bg-surface shadow-lg focus:outline-none"
          >
            <div className="flex items-center gap-md border-b border-border px-lg">
              <Search size={18} aria-hidden="true" className="shrink-0 text-muted" />
              <input
                id={inputId}
                // Combobox: focus never leaves the input, so typing and arrowing
                // do not compete.
                role="combobox"
                aria-expanded="true"
                aria-controls={listId}
                aria-activedescendant={results[active] ? `${listId}-${results[active].id}` : undefined}
                aria-label={placeholder}
                autoComplete="off"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={onKeyDown}
                placeholder={placeholder}
                className="min-h-11 w-full bg-transparent py-3 text-body text-text placeholder:text-muted focus:outline-none"
              />
            </div>

            <p role="status" className="sr-only">
              {results.length} {results.length === 1 ? "result" : "results"}
            </p>

            <ul id={listId} role="listbox" aria-label="Results" className="max-h-80 overflow-y-auto p-1">
              {results.length === 0 ? (
                <li className="px-3 py-lg text-center text-body text-muted">{emptyMessage}</li>
              ) : (
                results.map((item, i) => {
                  const header = item.group && item.group !== lastGroup ? item.group : null;
                  lastGroup = item.group;
                  return (
                    <li key={item.id}>
                      {header ? (
                        <p className="px-3 pb-1 pt-md text-micro uppercase tracking-wide text-muted">{header}</p>
                      ) : null}
                      <div
                        id={`${listId}-${item.id}`}
                        role="option"
                        aria-selected={i === active}
                        onMouseEnter={() => setActive(i)}
                        onClick={() => {
                          item.onSelect();
                          onClose();
                        }}
                        className={`flex min-h-11 cursor-pointer items-center gap-sm rounded-md px-3 text-body ${
                          i === active ? "bg-elevated text-text" : "text-muted"
                        }`}
                      >
                        {item.icon}
                        <span className="flex-1 truncate">{item.label}</span>
                        {item.hint ? <span className="shrink-0 text-caption text-muted">{item.hint}</span> : null}
                      </div>
                    </li>
                  );
                })
              )}
            </ul>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  );
}
