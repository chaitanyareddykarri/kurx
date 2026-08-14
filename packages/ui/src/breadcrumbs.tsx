import NextLink from "next/link";
import { ChevronRight } from "lucide-react";

export type Crumb = { label: string; href?: string };

/**
 * A breadcrumb trail.
 *
 * `<nav aria-label="Breadcrumb">` wrapping an ordered list is the documented
 * pattern — the order is the hierarchy, so it has to be an `<ol>`. The current
 * page is the last item, is not a link, and carries `aria-current="page"`.
 *
 * Separators are decorative and hidden: a screen reader announcing "chevron"
 * between every level is noise, and the list structure already conveys depth.
 */
export function Breadcrumbs({ items, className = "" }: { items: Crumb[]; className?: string }) {
  if (items.length === 0) return null;
  return (
    <nav aria-label="Breadcrumb" className={className}>
      <ol className="flex flex-wrap items-center gap-1 text-caption text-muted">
        {items.map((item, i) => {
          const last = i === items.length - 1;
          return (
            <li key={`${item.label}-${i}`} className="flex items-center gap-1">
              {item.href && !last ? (
                <NextLink href={item.href} className="rounded-sm hover:text-text hover:underline">
                  {item.label}
                </NextLink>
              ) : (
                <span aria-current={last ? "page" : undefined} className={last ? "text-text" : undefined}>
                  {item.label}
                </span>
              )}
              {!last ? <ChevronRight size={14} aria-hidden="true" className="shrink-0 text-muted" /> : null}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
