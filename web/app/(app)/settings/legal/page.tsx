import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { Card } from "@/components/ui/card";

export const metadata = { title: "Legal · Kurx" };

/// Only documents web actually serves. Mobile's list also carries a Refund Policy and Open Source
/// Licenses (`/legal/refund`, `/legal/licenses`); web has no route for either, and linking to a 404
/// from the legal page is worse than not listing it. They belong here the moment those pages exist.
const DOCUMENTS = [
  { href: "/privacy", label: "Privacy Policy" },
  { href: "/terms", label: "Terms & Conditions" }
] as const;

export default function LegalPage() {
  return (
    <div className="space-y-lg">
      <div>
        <h1 className="text-2xl font-semibold text-text">Legal</h1>
        <p className="mt-1 text-sm text-muted">The terms that apply to your use of Kurx.</p>
      </div>
      <Card>
        <ul className="divide-y divide-border">
          {DOCUMENTS.map((doc) => (
            <li key={doc.href}>
              <Link
                href={doc.href}
                className="flex min-h-11 items-center justify-between gap-md py-md text-body text-text transition duration-fast hover:text-accent-text"
              >
                {doc.label}
                <ChevronRight size={16} aria-hidden className="shrink-0 text-muted" />
              </Link>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}
