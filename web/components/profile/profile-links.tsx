import { Github, Linkedin, Globe, Instagram } from "lucide-react";

// Self-declared links (D-219). Rendered from the existing jsonb `links` field, which the API has always
// returned and nothing displayed. Only the four known slots render, and only with an http(s) URL — a
// stored `javascript:` value would otherwise become a clickable script on a public page.
const SLOTS = {
  github: { icon: Github, label: "GitHub" },
  linkedin: { icon: Linkedin, label: "LinkedIn" },
  website: { icon: Globe, label: "Website" },
  instagram: { icon: Instagram, label: "Instagram" }
} as const;

function safeUrl(value: unknown): string | null {
  if (typeof value !== "string" || !value) return null;
  try {
    const url = new URL(value);
    return url.protocol === "https:" || url.protocol === "http:" ? url.toString() : null;
  } catch {
    return null;
  }
}

export function ProfileLinks({ linksJson }: { linksJson: string | null }) {
  if (!linksJson) return null;

  let parsed: Record<string, unknown>;
  try {
    const raw = JSON.parse(linksJson);
    if (typeof raw !== "object" || raw === null) return null;
    parsed = raw as Record<string, unknown>;
  } catch {
    return null;
  }

  const entries = (Object.keys(SLOTS) as (keyof typeof SLOTS)[])
    .map((slot) => ({ slot, href: safeUrl(parsed[slot]) }))
    .filter((entry): entry is { slot: keyof typeof SLOTS; href: string } => entry.href !== null);

  if (entries.length === 0) return null;

  return (
    <div className="mt-4 flex flex-wrap gap-2">
      {entries.map(({ slot, href }) => {
        const { icon: Icon, label } = SLOTS[slot];
        return (
          <a
            key={slot}
            href={href}
            target="_blank"
            rel="noopener noreferrer nofollow"
            className="inline-flex items-center gap-1.5 rounded-full border border-border px-3 py-1 text-sm text-muted transition hover:text-text"
          >
            <Icon size={14} aria-hidden />
            {label}
          </a>
        );
      })}
    </div>
  );
}
