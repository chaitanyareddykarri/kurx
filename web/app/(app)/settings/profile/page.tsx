import { Card } from "@/components/ui/card";
import { LinkButton } from "@/components/ui/button";
import { requireSession } from "@/lib/session";
import { ProfileForm } from "@/components/profile/profile-form";

export const metadata = { title: "Edit profile · Kurx" };

/// Reads the first entry of the `education_json` array — the one the public profile renders. A
/// malformed or empty value yields blank fields rather than throwing the page away.
function parseEducation(raw: string | null | undefined) {
  const empty = { institute: "", degree: "", branch: "", startYear: "", endYear: "" };
  if (!raw) return empty;
  try {
    const parsed = JSON.parse(raw);
    const first = Array.isArray(parsed) ? parsed[0] : parsed;
    if (!first || typeof first !== "object") return empty;
    return {
      institute: String(first.institute ?? ""),
      degree: String(first.degree ?? ""),
      branch: String(first.branch ?? ""),
      startYear: first.start_year ? String(first.start_year) : "",
      endYear: first.end_year ? String(first.end_year) : ""
    };
  } catch {
    return empty;
  }
}

// Parses the jsonb links object into the four slots the form edits. A malformed value (hand-edited,
// or written before this shape existed) yields empty fields rather than throwing the page away.
function parseLinks(raw: string | null | undefined): Record<string, string> {
  if (!raw) return {};
  try {
    const parsed = JSON.parse(raw);
    return typeof parsed === "object" && parsed !== null ? parsed : {};
  } catch {
    return {};
  }
}

/**
 * Edit Profile — authors the public projection.
 *
 * Split out of `/settings`, which rendered this form under an `<h1>Profile</h1>` and *was* the
 * settings index. Two consequences: Settings had no hub of its own, and the page that edits your
 * public identity was indistinguishable from the page that configures your account. They answer
 * different questions — "what do others see" versus "how does my account behave" — so they are now
 * different pages.
 *
 * Privacy moved to `/settings/privacy`, where it had a duplicate that this page silently competed
 * with: the same four visibility controls rendered in two places, either of which could be the last
 * writer.
 */
export default async function EditProfilePage() {
  const session = await requireSession();
  const { me } = session;

  // Prefilled from GET /v1/me — the caller's own record (D-219). Reading the *public* profile here
  // returns nothing for an unclaimed username or a private profile, and because the form always
  // submits every field, that empty prefill silently wiped headline/bio/skills on save.
  return (
    <div className="space-y-lg">
      <div className="flex flex-wrap items-center justify-between gap-md">
        <div>
          <h1 className="text-2xl font-semibold text-text">Edit profile</h1>
          <p className="mt-1 text-sm text-muted">This is what other people see.</p>
        </div>
        {me.username ? (
          <LinkButton href={`/u/${me.username}`} variant="secondary">View public profile</LinkButton>
        ) : null}
      </div>

      <Card>
        <ProfileForm
          initial={{
            name: me.name,
            username: me.username ?? "",
            headline: me.headline ?? "",
            bio: me.bio ?? "",
            skills: me.skills ?? [],
            languages: me.languages ?? [],
            interests: me.interests ?? [],
            education: parseEducation(me.education_json),
            links: parseLinks(me.links_json),
            avatarKey: me.avatar_key ?? "",
            coverKey: me.cover_key ?? ""
          }}
        />
      </Card>
    </div>
  );
}
