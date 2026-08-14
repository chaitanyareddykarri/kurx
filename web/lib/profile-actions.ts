"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import {
  updateProfile, updatePrivacy, presignProfileImage, apiErrorMessage,
  PROFILE_SECTIONS, SECTION_TIERS, type SectionTier
} from "@/lib/api";

// Step 1 of the profile-image upload (D-219). Lives in a server action because the access token is an
// httpOnly cookie the browser can't read; the browser does step 2 (PUT the bytes) with the returned URL
// and then submits the key with the profile form, so no image ever passes through this server.
export async function presignProfileImageAction(
  slot: "avatar" | "cover",
  contentType: string,
  maxBytes: number
) {
  const session = await requireSession();
  try {
    return { ok: true as const, ...(await presignProfileImage(session.accessToken, { slot, contentType, maxBytes })) };
  } catch (err) {
    return { ok: false as const, error: apiErrorMessage(err) };
  }
}

function field(formData: FormData, key: string): string {
  const v = formData.get(key);
  return typeof v === "string" ? v.trim() : "";
}

// Update the caller's public profile via PATCH /v1/me/profile. Headline/bio are cleared by sending an
// empty string; username is only sent when non-empty (the backend rejects a blank one and no-ops an
// unchanged one, so it can't be wiped by accident here).
//
// The form this serves prefills from GET /v1/me — never from the public profile (D-219). That matters:
// the old prefill went through getPublicProfile, which fails for an unclaimed username or a non-public
// profile, and because every field below is always sent, a failed prefill silently overwrote real
// headline/bio/skills with blanks. Prefilling from the caller's own record removes the failure mode
// rather than guarding against it here.
export async function updateProfileAction(_prev: unknown, formData: FormData) {
  const name = field(formData, "name");
  if (!name) return { error: "Name can't be empty." };

  const username = field(formData, "username").toLowerCase();
  // One parser for all three comma-separated lists — they have identical semantics, and three
  // copies of the same split/trim/filter is how one of them ends up trimming differently.
  const csv = (key: string) =>
    field(formData, key).split(",").map((s) => s.trim()).filter(Boolean);
  const skills = csv("skills");
  const languages = csv("languages");
  const interests = csv("interests");
  const links = buildLinksJson(formData);
  if (links === null) return { error: "Links must be valid URLs." };

  const education = buildEducationJson(formData);
  if (education === null) return { error: "Education years must be four-digit years, and the end year cannot precede the start." };

  const session = await requireSession();
  try {
    await updateProfile(session.accessToken, {
      name,
      username: username || undefined,
      headline: field(formData, "headline"),
      bio: field(formData, "bio"),
      skills,
      languages,
      interests,
      educationJson: education,
      linksJson: links,
      avatarKey: field(formData, "avatarKey") || undefined,
      coverKey: field(formData, "coverKey") || undefined
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/settings");
  revalidatePath(`/u/${username || session.me.username}`);
  return { ok: true };
}

// The four link slots the profile renders. Stored as one jsonb object, matching the existing
// LinksJson column rather than adding per-link columns. Returns null when a value isn't a URL.
const LINK_KEYS = ["github", "linkedin", "website", "instagram"] as const;

function buildLinksJson(formData: FormData): string | null {
  const links: Record<string, string> = {};
  for (const key of LINK_KEYS) {
    const raw = field(formData, `link_${key}`);
    if (!raw) continue;
    const url = raw.startsWith("http://") || raw.startsWith("https://") ? raw : `https://${raw}`;
    try {
      new URL(url);
    } catch {
      return null;
    }
    links[key] = url;
  }
  return JSON.stringify(links);
}

// Visibility settings (D-219, four-tier per section in D-221). Separate action from the profile form:
// these are a different resource with a different endpoint, and mixing them would make a failed
// profile save silently revert privacy too.
export async function updatePrivacyAction(_prev: unknown, formData: FormData) {
  const sections: Record<string, SectionTier> = {};
  for (const section of PROFILE_SECTIONS) {
    const raw = formData.get(`section_${section}`);
    if (typeof raw !== "string") continue;
    if (!(SECTION_TIERS as readonly string[]).includes(raw)) return { error: "Unknown visibility option." };
    sections[section] = raw as SectionTier;
  }

  const session = await requireSession();
  try {
    await updatePrivacy(session.accessToken, { sections });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/settings");
  if (session.me.username) revalidatePath(`/u/${session.me.username}`);
  return { ok: true };
}

/**
 * Serialises the education fieldset into the existing `education_json` array.
 *
 * **One entry, not a list.** The column has always been an array, but the public profile reads only
 * its first element (`PublicProfileView.College` is a single affiliation), so an editor that wrote
 * several would silently show one. One entry is what the read side can represent; a repeater belongs
 * with the read side that can display it.
 *
 * Empty everywhere means "not stated" and writes an empty array rather than a row of blanks — a
 * profile with a nameless institute is worse than one with no education section.
 *
 * Returns `null` when the years are invalid, so the caller can refuse rather than store nonsense.
 */
function buildEducationJson(formData: FormData): string | null {
  const institute = field(formData, "eduInstitute");
  const degree = field(formData, "eduDegree");
  const branch = field(formData, "eduBranch");
  const startYear = field(formData, "eduStartYear");
  const endYear = field(formData, "eduEndYear");

  if (![institute, degree, branch, startYear, endYear].some((v) => v.length > 0)) return "[]";

  // Years are optional, but a year that is present must be a real one. `1` and `20244` are both
  // rejected rather than stored and rendered.
  const year = (raw: string): number | null | undefined => {
    if (!raw) return undefined;
    if (!/^\d{4}$/.test(raw)) return null;
    const n = Number(raw);
    return n >= 1900 && n <= 2100 ? n : null;
  };
  const from = year(startYear);
  const to = year(endYear);
  if (from === null || to === null) return null;
  if (typeof from === "number" && typeof to === "number" && to < from) return null;

  return JSON.stringify([
    {
      institute: institute || null,
      degree: degree || null,
      branch: branch || null,
      start_year: from ?? null,
      end_year: to ?? null
    }
  ]);
}
