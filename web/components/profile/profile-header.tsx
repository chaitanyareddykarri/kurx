import Link from "next/link";
import { Download, MapPin, Pencil, ShieldCheck } from "lucide-react";
import { Avatar, Badge, Chip, LinkButton } from "@kurx/ui";

import { ProvenanceBadge } from "@/components/profile/provenance-badge";
import { ShareProfile } from "@/components/profile/share-profile";
import { completionPercent, completionSteps, type CompletionInput } from "@/components/profile/profile-completion";

/**
 * The profile header.
 *
 * **The photo is the subject, so it owns the optical centre.** The cover is a backdrop at a fixed
 * aspect ratio with a scrim beneath it — without the scrim, a light cover photo puts white text on
 * white and the name disappears; the gradient is what makes an arbitrary user upload safe to place
 * type over.
 *
 * **Two headlines, never merged** (D-225). The derived line is what Kurx can prove from verified
 * activity and leads; the self-declared one follows, visibly marked. Collapsing them would let a
 * claim borrow the authority of a proof, which is the one thing this profile exists not to do.
 *
 * Completion renders for the owner only — see `profile-completion.ts` for why it is never public.
 */
export function ProfileHeader({
  username,
  name,
  avatarUrl,
  coverKey,
  derivedHeadline,
  headline,
  summary,
  bio,
  identityLabels,
  cities,
  identityVerified,
  verifiedMember,
  resumeHref,
  isOwnProfile,
  completion,
  actions,
}: {
  username: string;
  name: string;
  /** The PRESIGNED URL, not the storage key (D-302). Named for what it is: this prop was `avatarKey`
   *  and was handed a key, which is unfetchable, so the header rendered initials for everyone. */
  avatarUrl: string | null;
  coverKey: string | null;
  derivedHeadline: string | null;
  headline: string | null;
  summary: string;
  /// The person's own words. Shown here in full because it is the single most-read line on a
  /// profile; About repeats it under its provenance heading rather than being its only home.
  bio: string | null;
  identityLabels: readonly string[];
  /** Derived from where this person's events actually happened — never a self-declared field. */
  cities: readonly string[];
  identityVerified: boolean;
  verifiedMember: boolean;
  resumeHref: string;
  isOwnProfile: boolean;
  /** Owner-only. Null for every other viewer, which is what keeps completion private. */
  completion: CompletionInput | null;
  /** Connect / Message — rendered by the page because they need the viewer's ally relation. */
  actions?: React.ReactNode;
}) {
  const steps = completion ? completionSteps(completion) : [];
  const percent = completion ? completionPercent(steps) : 0;
  const nextStep = steps.find((s) => !s.done);

  return (
    <section className="overflow-hidden rounded-lg border border-border bg-surface">
      {/* Fixed aspect rather than a fixed height: a cover crops predictably at every breakpoint
          instead of letterboxing on wide screens. */}
      <div className="relative aspect-[4/1] min-h-32 bg-elevated sm:aspect-[5/1]">
        {coverKey ? (
          // Provider-swappable storage host (IStorage) — next/image would need remotePatterns
          // re-pinned on every provider change.
          // eslint-disable-next-line @next/next/no-img-element
          <img src={coverKey} alt="" aria-hidden className="h-full w-full object-cover" />
        ) : (
          <div className="h-full w-full bg-gradient-to-br from-elevated via-surface to-elevated" />
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-surface/90 via-surface/20 to-transparent" />
      </div>

      <div className="px-lg pb-lg sm:px-xl sm:pb-xl">
        <div className="flex flex-wrap items-end justify-between gap-lg">
          {/* The ring is what separates the avatar from an arbitrary cover photo behind it. */}
          <div className="-mt-14 rounded-full ring-4 ring-surface sm:-mt-16">
            <Avatar name={name} src={avatarUrl ?? undefined} size={128} />
          </div>

          <div className="ml-auto flex flex-wrap items-center gap-sm pt-md">
            {isOwnProfile ? (
              <LinkButton href="/settings/profile" variant="secondary">
                <Pencil size={15} aria-hidden /> Edit profile
              </LinkButton>
            ) : null}
            <ShareProfile username={username} name={name} />
            {/* Composed per viewer — a section this reader cannot see is absent from their copy,
                so the link is safe to show to anyone. */}
            <a
              href={resumeHref}
              className="inline-flex min-h-11 items-center gap-1.5 rounded-md border border-border px-3 text-sm text-text transition duration-fast hover:bg-elevated"
            >
              <Download size={15} aria-hidden /> Résumé
            </a>
            {actions}
          </div>
        </div>

        <div className="mt-lg">
          <div className="flex flex-wrap items-center gap-sm">
            <h1 className="text-3xl font-semibold tracking-tight text-text sm:text-4xl">{name}</h1>
            {/* Teal, not accent: verification is an attestation (D-286). */}
            {identityVerified ? (
              <Badge tone="teal" icon={<ShieldCheck size={12} aria-hidden />}>Identity verified</Badge>
            ) : null}
            {verifiedMember ? <Badge tone="teal">Verified member</Badge> : null}
          </div>
          <p className="mt-1 text-body text-muted">@{username}</p>

          {derivedHeadline ? (
            <p className="mt-md flex flex-wrap items-center gap-sm text-lg text-text">
              <span className="font-medium">{derivedHeadline}</span>
              <ProvenanceBadge source="derived" />
            </p>
          ) : null}
          {headline ? (
            <p className="mt-1 flex flex-wrap items-center gap-sm text-body text-muted">
              <span>{headline}</span>
              <ProvenanceBadge source="self_declared" />
            </p>
          ) : null}

          {cities.length > 0 ? (
            <p className="mt-md inline-flex items-center gap-1.5 text-sm text-muted">
              <MapPin size={14} aria-hidden />
              {/* "Active in", not "Lives in": this is where their events happened, which is a fact
                  the platform holds — a home address is not. */}
              Active in {cities.slice(0, 3).join(" · ")}
            </p>
          ) : null}

          {identityLabels.length > 0 ? (
            <div className="mt-md flex flex-wrap gap-1.5">
              {identityLabels.map((label) => <Chip key={label}>{label}</Chip>)}
            </div>
          ) : null}

          {summary ? <p className="mt-md max-w-2xl text-body text-muted">{summary}</p> : null}

          {bio?.trim() ? (
            <p className="mt-md max-w-2xl whitespace-pre-line text-body text-text">{bio}</p>
          ) : null}
        </div>

        {completion && percent < 100 ? (
          <div className="mt-xl rounded-md border border-border bg-elevated p-lg">
            <div className="flex flex-wrap items-center justify-between gap-sm">
              <h2 className="text-body font-semibold text-text">Profile strength</h2>
              <span className="text-sm font-semibold text-accent-text">{percent}%</span>
            </div>
            <div
              className="mt-md h-1.5 overflow-hidden rounded-pill bg-surface"
              role="progressbar"
              aria-valuenow={percent}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-label="Profile completion"
            >
              <div className="h-full rounded-pill bg-accent transition-all duration-slow" style={{ width: `${percent}%` }} />
            </div>
            {nextStep ? (
              <p className="mt-md text-sm text-muted">
                Next:{" "}
                <Link href={nextStep.href} className="font-medium text-accent-text hover:underline">
                  {nextStep.label}
                </Link>
              </p>
            ) : null}
          </div>
        ) : null}
      </div>
    </section>
  );
}
