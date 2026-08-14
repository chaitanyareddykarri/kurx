"use client";

import { ReactNode, useId } from "react";
import { ChevronDown, ChevronUp } from "lucide-react";
import { Skeleton } from "./skeleton";

export type SortDir = "asc" | "desc";
export type SortState = { key: string; dir: SortDir };

export type Column<T> = {
  /** Stable key; also the sort key when `sortable`. */
  key: string;
  header: ReactNode;
  /** Cell renderer; defaults to `String(row[key])` when the key is a field. */
  render?: (row: T) => ReactNode;
  align?: "left" | "right" | "center";
  sortable?: boolean;
  /** Optional fixed width (e.g. "12rem"). */
  width?: string;
  /** Screen-reader-only header label when `header` is an icon/empty. */
  srHeader?: string;
};

type DataTableProps<T> = {
  columns: Column<T>[];
  data: T[];
  keyField: (row: T) => string;
  loading?: boolean;
  /** Rendered in place of the table body when not loading and data is empty. */
  emptyState?: ReactNode;
  onRowClick?: (row: T) => void;
  /** Controlled sort — omit to disable sorting UI even if columns are `sortable`. */
  sort?: SortState;
  onSortChange?: (next: SortState) => void;
  /** Row selection (opt-in) — drives bulk actions in module screens. */
  selectable?: boolean;
  selectedIds?: Set<string>;
  onSelectionChange?: (ids: Set<string>) => void;
  /** Accessible table description (sr-only caption). */
  caption?: string;
  /**
   * What one row *is*, for the controls that act on it — "Suspend Asha Rao", not "Select row".
   *
   * Without it every checkbox in the table shares one accessible name, so a screen-reader user
   * moving through them hears "Select row" N times with no way to tell which is which. Required
   * whenever `selectable` or `onRowClick` is set, because both put a control on every row.
   */
  rowLabel?: (row: T) => string;
  className?: string;
};

const alignClass = { left: "text-left", right: "text-right", center: "text-center" } as const;

export function DataTable<T>({
  columns,
  data,
  keyField,
  loading = false,
  emptyState,
  onRowClick,
  sort,
  onSortChange,
  selectable = false,
  selectedIds,
  onSelectionChange,
  caption,
  rowLabel,
  className = ""
}: DataTableProps<T>) {
  const statusId = useId();
  const selected = selectedIds ?? new Set<string>();
  const allSelected = selectable && data.length > 0 && data.every((r) => selected.has(keyField(r)));
  const someSelected = selectable && !allSelected && data.some((r) => selected.has(keyField(r)));

  function toggleAll() {
    if (!onSelectionChange) return;
    onSelectionChange(allSelected ? new Set() : new Set(data.map(keyField)));
  }

  function toggleRow(id: string) {
    if (!onSelectionChange) return;
    const next = new Set(selected);
    next.has(id) ? next.delete(id) : next.add(id);
    onSelectionChange(next);
  }

  function headerSort(col: Column<T>) {
    if (!col.sortable || !onSortChange) return;
    const dir: SortDir = sort?.key === col.key && sort.dir === "asc" ? "desc" : "asc";
    onSortChange({ key: col.key, dir });
  }

  const colCount = columns.length + (selectable ? 1 : 0);

  return (
    /*
      The scroll container is focusable and named.
      
      A region that scrolls must be reachable by keyboard (WCAG 2.1.1) — a `<div>` with
      `overflow-x-auto` and no `tabIndex` can be scrolled by a mouse or a trackpad and by nothing
      else, which on an admin table wide enough to need it means whole columns a keyboard user cannot
      reach. `role="group"` plus the caption as its name keeps it out of the a11y tree as a landmark
      while still announcing what it is.
    */
    <div
      tabIndex={0}
      role="group"
      aria-label={caption}
      className={`overflow-x-auto rounded-lg border border-border focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent ${className}`}
    >
      <table aria-busy={loading || undefined} className="w-full border-collapse text-sm">
        {caption ? <caption className="sr-only">{caption}</caption> : null}
        <thead>
          <tr className="border-b border-border bg-elevated/50 text-left text-xs text-muted">
            {selectable ? (
              <th scope="col" className="w-10 px-3 py-2.5">
                {/* Partial selection showed as UNCHECKED, which states "none of these are
                    selected" while several are. `indeterminate` is a DOM property, not an
                    attribute, so it has to be set through a ref callback. */}
                <input
                  type="checkbox"
                  aria-label={allSelected ? "Clear selection" : "Select all rows"}
                  checked={allSelected}
                  ref={(el) => { if (el) el.indeterminate = someSelected; }}
                  onChange={toggleAll}
                  className="h-4 w-4 accent-accent"
                />
              </th>
            ) : null}
            {columns.map((col) => {
              const active = sort?.key === col.key;
              const ariaSort = !col.sortable ? undefined : active ? (sort!.dir === "asc" ? "ascending" : "descending") : "none";
              return (
                <th
                  key={col.key}
                  scope="col"
                  aria-sort={ariaSort}
                  style={col.width ? { width: col.width } : undefined}
                  className={`px-3 py-2.5 font-medium ${alignClass[col.align ?? "left"]}`}
                >
                  {col.sortable && onSortChange ? (
                    <button
                      type="button"
                      onClick={() => headerSort(col)}
                      className="inline-flex items-center gap-1 font-medium text-muted transition hover:text-text"
                    >
                      {col.srHeader ? <span className="sr-only">{col.srHeader}</span> : null}
                      {col.header}
                      {active ? (
                        sort!.dir === "asc" ? <ChevronUp size={13} /> : <ChevronDown size={13} />
                      ) : (
                        <ChevronDown size={13} className="opacity-30" />
                      )}
                    </button>
                  ) : (
                    <>
                      {col.srHeader ? <span className="sr-only">{col.srHeader}</span> : null}
                      {col.header}
                    </>
                  )}
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {loading ? (
            Array.from({ length: 6 }).map((_, i) => (
              <tr key={i} className="border-b border-border/60">
                {Array.from({ length: colCount }).map((__, j) => (
                  <td key={j} className="px-3 py-3">
                    <Skeleton className="h-4 w-full" />
                  </td>
                ))}
              </tr>
            ))
          ) : data.length === 0 ? (
            <tr>
              <td colSpan={colCount} className="px-3 py-0">
                {emptyState ?? <p className="py-10 text-center text-sm text-muted">No results.</p>}
              </td>
            </tr>
          ) : (
            data.map((row) => {
              const id = keyField(row);
              return (
                /*
                  A clickable row that only a mouse can click.
                  
                  `onRowClick` is how three admin screens open a record, and the `<tr>` carried no
                  `tabIndex`, no key handler and no role — so the primary action on those tables was
                  unreachable by keyboard entirely. It is a button now in everything but tag name,
                  named after the row so a screen reader says which record it opens.
                  
                  Selection also showed as `bg-accent/5`, a 5% tint that is very nearly nothing; it
                  now carries a left marker as well, so the state survives greyscale.
                */
                <tr
                  key={id}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  onKeyDown={
                    onRowClick
                      ? (e) => {
                          if (e.key !== "Enter" && e.key !== " ") return;
                          // Space scrolls the page by default, and the row is the target either way.
                          if (e.target !== e.currentTarget) return;
                          e.preventDefault();
                          onRowClick(row);
                        }
                      : undefined
                  }
                  tabIndex={onRowClick ? 0 : undefined}
                  role={onRowClick ? "button" : undefined}
                  aria-label={onRowClick && rowLabel ? rowLabel(row) : undefined}
                  className={`border-b border-border/60 transition duration-fast last:border-0 ${
                    onRowClick
                      ? "cursor-pointer hover:bg-surface focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent"
                      : ""
                  } ${selected.has(id) ? "bg-accent/10 shadow-[inset_2px_0_0_0_rgb(var(--color-accent))]" : ""}`}
                >
                  {selectable ? (
                    <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                      <input
                        type="checkbox"
                        aria-label={rowLabel ? `Select ${rowLabel(row)}` : "Select row"}
                        checked={selected.has(id)}
                        onChange={() => toggleRow(id)}
                        className="h-4 w-4 accent-accent"
                      />
                    </td>
                  ) : null}
                  {columns.map((col) => (
                    <td
                      key={col.key}
                      className={`px-3 py-3 text-text ${alignClass[col.align ?? "left"]}`}
                    >
                      {col.render ? col.render(row) : String((row as Record<string, unknown>)[col.key] ?? "")}
                    </td>
                  ))}
                </tr>
              );
            })
          )}
        </tbody>
      </table>

      {/*
        Selection drives the bulk actions above these tables — suspend, blacklist, archive — and
        changed with nothing said about it. Rendered unconditionally so the first message is
        observed; a region inserted together with its content is not announced.
      */}
      <p id={statusId} role="status" className="sr-only">
        {loading ? "Loading rows." : selectable && selected.size > 0 ? `${selected.size} selected.` : ""}
      </p>
    </div>
  );
}
