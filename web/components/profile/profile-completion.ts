/**
 * Profile completion — computed on the client from fields the profile already carries.
 *
 * Deliberately NOT a backend field. Completion is a nudge to the owner about their own profile, so
 * it needs no server truth and must never be visible to anyone else: publishing "this profile is
 * 40% complete" would be a statement about a person derived from choices they are entitled to make
 * (D-221 keeps the public profile to positive, verified signals only).
 *
 * Each step names the settings surface that satisfies it, so the card can link straight at the fix
 * rather than dropping the owner on a hub to hunt for it.
 */
export type CompletionStep = { key: string; label: string; done: boolean; href: string };

export type CompletionInput = {
  avatarKey: string | null;
  coverKey: string | null;
  headline: string | null;
  bio: string | null;
  skills: readonly string[];
  languages: readonly string[];
  interests: readonly string[];
  linksJson: string | null;
  hasEducation: boolean;
  emailVerified: boolean;
  identityVerified: boolean;
};

function hasAnyLink(linksJson: string | null): boolean {
  if (!linksJson) return false;
  try {
    const parsed = JSON.parse(linksJson) as Record<string, unknown>;
    return Object.values(parsed).some((v) => typeof v === "string" && v.trim().length > 0);
  } catch {
    // A malformed value is not a link. Never throw out of a completion calculation — it is
    // decoration, and taking the header down with it would be absurd.
    return false;
  }
}

export function completionSteps(input: CompletionInput): CompletionStep[] {
  return [
    { key: "avatar", label: "Add a profile photo", done: !!input.avatarKey, href: "/settings/profile" },
    { key: "cover", label: "Add a cover image", done: !!input.coverKey, href: "/settings/profile" },
    { key: "headline", label: "Write a headline", done: !!input.headline?.trim(), href: "/settings/profile" },
    { key: "bio", label: "Write a short bio", done: !!input.bio?.trim(), href: "/settings/profile" },
    { key: "skills", label: "List your skills", done: input.skills.length > 0, href: "/settings/profile" },
    { key: "languages", label: "Add the languages you speak", done: input.languages.length > 0, href: "/settings/profile" },
    { key: "interests", label: "Add your interests", done: input.interests.length > 0, href: "/settings/profile" },
    { key: "links", label: "Link your work", done: hasAnyLink(input.linksJson), href: "/settings/profile" },
    { key: "education", label: "Add your education", done: input.hasEducation, href: "/settings/profile" },
    { key: "email", label: "Verify your email", done: input.emailVerified, href: "/settings/account" },
    { key: "identity", label: "Verify your identity", done: input.identityVerified, href: "/settings/identity" },
  ];
}

export function completionPercent(steps: readonly CompletionStep[]): number {
  if (steps.length === 0) return 100;
  return Math.round((steps.filter((s) => s.done).length / steps.length) * 100);
}
