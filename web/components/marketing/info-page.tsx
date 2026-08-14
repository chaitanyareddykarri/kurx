import { ReactNode } from "react";

import { jsonLd } from "@/lib/site";

/**
 * The shared frame for the simple public pages — About, Privacy, Terms, Blog,
 * Support, Contact.
 *
 * Five of them had each redefined the same three lines (a `<main>`, an `<h1>`, a
 * paragraph) with their own slightly different type sizes, so the marketing site
 * had no consistent page rhythm.
 *
 * It renders a `<div>`, **not** a `<main>`. The `(public)` layout owns the single
 * `<main id="main-content">` that the skip link targets; every one of these pages
 * previously nested a second `<main>` inside it, which is invalid and gives the
 * skip link two possible destinations.
 */
export function InfoPage({
  title,
  intro,
  schemaType,
  children
}: {
  title: string;
  /** The lead paragraph, also used as the structured-data description. */
  intro: string;
  /** schema.org type — Organization, PrivacyPolicy, and so on. */
  schemaType?: string;
  children?: ReactNode;
}) {
  return (
    <div className="container-shell py-3xl">
      {schemaType ? (
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={jsonLd(schemaType, { name: "Kurx", description: intro })}
        />
      ) : null}
      <h1 className="text-display">{title}</h1>
      <p className="mt-lg max-w-3xl text-body-lg text-muted">{intro}</p>
      {children ? <div className="mt-xl">{children}</div> : null}
    </div>
  );
}
