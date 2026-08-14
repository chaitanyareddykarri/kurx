"use client";

import { ChevronLeft, ChevronRight } from "lucide-react";

type PaginationProps = {
  page: number; // 1-based
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
  className?: string;
};

/** Range summary + prev/next. Page-based (server passes `total`); swap for a cursor
 *  variant per screen if a table outgrows offset pagination. */
export function Pagination({ page, pageSize, total, onPageChange, className = "" }: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);

  const btn =
    "grid h-8 w-8 place-items-center rounded-md border border-border text-muted transition hover:bg-elevated hover:text-text disabled:cursor-not-allowed disabled:opacity-40 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent";

  return (
    <nav
      aria-label="Pagination"
      className={`flex items-center justify-between gap-4 text-sm text-muted ${className}`}
    >
      <p aria-live="polite">
        {total === 0 ? "No results" : (
          <>
            <span className="text-text">{from.toLocaleString()}</span>–
            <span className="text-text">{to.toLocaleString()}</span> of{" "}
            <span className="text-text">{total.toLocaleString()}</span>
          </>
        )}
      </p>
      <div className="flex items-center gap-2">
        <button
          type="button"
          aria-label="Previous page"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          className={btn}
        >
          <ChevronLeft size={16} />
        </button>
        <span className="tabular-nums">
          Page <span className="text-text">{page}</span> / {totalPages}
        </span>
        <button
          type="button"
          aria-label="Next page"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          className={btn}
        >
          <ChevronRight size={16} />
        </button>
      </div>
    </nav>
  );
}
