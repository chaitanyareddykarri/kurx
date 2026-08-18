"use client";

import { useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { updateProfileAction } from "@/lib/profile-actions";
import { Button } from "@/components/ui/button";
import { ImageUpload } from "@/components/profile/image-upload";

const inputClass = "mt-1 h-10 w-full rounded-md border border-border bg-background px-3 text-sm text-text";
const areaClass = "mt-1 w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-text";

type Initial = {
  name: string;
  username: string;
  headline: string;
  bio: string;
  skills: string[];
  languages: string[];
  interests: string[];
  education: { institute: string; degree: string; branch: string; startYear: string; endYear: string };
  links: Record<string, string>;
  avatarKey: string;
  coverKey: string;
  /** Presigned companions (D-302) — the keys above are submitted, these are rendered. */
  avatarUrl: string | null;
  coverUrl: string | null;
};

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Saving…" : "Save profile"}</Button>;
}

export function ProfileForm({ initial }: { initial: Initial }) {
  const [state, formAction] = useFormState(updateProfileAction, null);

  return (
    <form action={formAction} className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <ImageUpload slot="avatar" name="avatarKey" initialKey={initial.avatarKey} initialUrl={initial.avatarUrl} label="Profile photo" previewName={initial.name} />
        <ImageUpload slot="cover" name="coverKey" initialKey={initial.coverKey} initialUrl={initial.coverUrl} label="Cover image" previewName={initial.name} />
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="p-name">Name</label>
        <input id="p-name" name="name" required defaultValue={initial.name} className={inputClass} />
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="p-username">Username</label>
        <input id="p-username" name="username" defaultValue={initial.username} className={inputClass} />
        <p className="mt-1 text-xs text-muted">Changing your username is limited to once every 30 days.</p>
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="p-headline">Headline</label>
        <input id="p-headline" name="headline" defaultValue={initial.headline} placeholder="e.g. Event organizer & speaker" className={inputClass} />
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="p-bio">About</label>
        <textarea id="p-bio" name="bio" rows={4} defaultValue={initial.bio} placeholder="A short bio" className={areaClass} />
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="p-skills">Skills</label>
        <input id="p-skills" name="skills" defaultValue={initial.skills.join(", ")} placeholder="Comma-separated, e.g. React, Public speaking" className={inputClass} />
      </div>

      <fieldset className="space-y-1.5">
        {/* Education was rendered on every profile and editable nowhere, so the only values in the
            field were whatever predated the current form. Stored as the existing jsonb array
            (`education_json`) rather than new columns — the shape already exists and the public
            profile already reads its first entry. */}
        <legend className="text-sm font-medium text-text">Education</legend>
        <p className="text-xs text-muted">Shown on your profile as self-declared, not a verified affiliation.</p>
        <div className="grid gap-2 sm:grid-cols-2">
          <input
            name="eduInstitute"
            defaultValue={initial.education.institute}
            placeholder="Institute"
            aria-label="Institute"
            className={inputClass}
          />
          <input
            name="eduDegree"
            defaultValue={initial.education.degree}
            placeholder="Degree (e.g. B.Tech)"
            aria-label="Degree"
            className={inputClass}
          />
          <input
            name="eduBranch"
            defaultValue={initial.education.branch}
            placeholder="Branch (e.g. Computer Science)"
            aria-label="Branch"
            className={inputClass}
          />
          <div className="grid grid-cols-2 gap-2">
            <input
              name="eduStartYear"
              defaultValue={initial.education.startYear}
              placeholder="From"
              inputMode="numeric"
              aria-label="Start year"
              className={inputClass}
            />
            <input
              name="eduEndYear"
              defaultValue={initial.education.endYear}
              placeholder="To"
              inputMode="numeric"
              aria-label="End year"
              className={inputClass}
            />
          </div>
        </div>
      </fieldset>

      <div className="space-y-1.5">
        <label className="text-sm font-medium text-text" htmlFor="p-languages">Languages</label>
        <input id="p-languages" name="languages" defaultValue={initial.languages.join(", ")} placeholder="Comma-separated, e.g. English, Hindi, Telugu" className={inputClass} />
      </div>

      <div className="space-y-1.5">
        <label className="text-sm font-medium text-text" htmlFor="p-interests">Interests</label>
        <input id="p-interests" name="interests" defaultValue={initial.interests.join(", ")} placeholder="Comma-separated, e.g. Hackathons, Design, Robotics" className={inputClass} />
        {/* Named apart from Event DNA on purpose: this is what you say you care about, that is what
            you provably did. The profile shows both and never merges them. */}
        <p className="text-xs text-muted">Shown separately from your Event DNA, which is derived from events you actually took part in.</p>
      </div>
      <fieldset className="grid gap-3 sm:grid-cols-2">
        <legend className="text-sm font-medium text-text">Links</legend>
        {(["github", "linkedin", "website", "instagram"] as const).map((slot) => (
          <div key={slot}>
            <label className="text-xs capitalize text-muted" htmlFor={`p-link-${slot}`}>{slot}</label>
            <input
              id={`p-link-${slot}`}
              name={`link_${slot}`}
              defaultValue={initial.links[slot] ?? ""}
              placeholder={slot === "website" ? "yoursite.com" : `${slot}.com/you`}
              inputMode="url"
              className={inputClass}
            />
          </div>
        ))}
      </fieldset>
      <div className="flex flex-wrap items-center gap-3">
        <SubmitButton />
        {state && "error" in state ? <p className="text-sm text-danger">{String(state.error)}</p> : null}
        {state && "ok" in state ? <p className="text-sm text-accent">Profile saved.</p> : null}
      </div>
    </form>
  );
}
