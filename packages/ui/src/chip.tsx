"use client";

import Link from "next/link";
import { ReactNode } from "react";

/** Single source for the pill look, shared by the interactive (client) and link (server-safe) variants. */
export function chipClass(selected: boolean): string {
  return `inline-flex items-center gap-1.5 rounded-full border px-3.5 py-1.5 text-sm font-semibold transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${
    selected
      ? "border-accent bg-accent/15 text-text"
      : "border-border bg-surface text-muted hover:text-text hover:border-border"
  }`;
}

/** A selectable filter pill — selected chips take an accent tint. */
export function Chip({
  children,
  selected = false,
  onClick,
  count,
  icon
}: {
  children: ReactNode;
  selected?: boolean;
  onClick?: () => void;
  count?: number;
  icon?: ReactNode;
}) {
  return (
    <button type="button" onClick={onClick} className={chipClass(selected)}>
      {icon}
      {children}
      {typeof count === "number" ? (
        <span className={selected ? "text-accent" : "text-muted"}>{count}</span>
      ) : null}
    </button>
  );
}

/** A chip rendered as a plain `<Link>` — for server-rendered, URL-driven filter pages with no client JS. */
export function ChipLink({
  href,
  children,
  selected = false,
  icon,
  trailing
}: {
  href: string;
  children: ReactNode;
  selected?: boolean;
  icon?: ReactNode;
  trailing?: ReactNode;
}) {
  return (
    <Link href={href} className={chipClass(selected)}>
      {icon}
      {children}
      {trailing}
    </Link>
  );
}
