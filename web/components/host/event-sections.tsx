"use client";

import { useTransition } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { useRouter } from "next/navigation";
import { Avatar, Badge, Button, controlClass } from "@kurx/ui";
import {
  addVenueAction, addSessionAction, addSpeakerAction, addSponsorAction, uploadEventMediaAction,
  updateSessionAction, deleteSessionAction, reorderSessionsAction,
  updateSpeakerAction, removeSpeakerAction, updateSponsorAction, removeSponsorAction, deleteMediaAction,
  uploadEventBannerAction, removeEventBannerAction
} from "@/lib/event-actions";
import type { EventSession, Speaker, Sponsor, EventMediaView } from "@/lib/api";
import { toLocalInput, withUtcTimes } from "@/lib/event-wizard";
import { ConfirmSubmitButton } from "@/components/host/confirm-submit-button";

const inputClass = controlClass;

function AddButton({ label }: { label: string }) {
  const { pending } = useFormStatus();
  return <Button type="submit" variant="secondary" disabled={pending}>{pending ? "Saving…" : label}</Button>;
}

export function VenueSection({ orgId, eventId, venueName }: { orgId: string; eventId: string; venueName: string }) {
  const [state, formAction] = useFormState(addVenueAction.bind(null, orgId, eventId), null);
  return (
    <div className="space-y-4">
      <p className="text-sm text-muted">
        Current venue: <span className="font-medium text-text">{venueName || "not set"}</span>. Saving a venue
        here adds it to the represented organization&apos;s reusable venue library and links it to this event.
      </p>
      <form action={formAction} className="grid gap-2 sm:grid-cols-2">
        <input name="name" placeholder="Venue name" required className={inputClass} />
        <input name="city" placeholder="City" className={inputClass} />
        <input name="address" placeholder="Address" aria-label="Address" className={`${inputClass} sm:col-span-2`} />
        <input name="capacity" type="number" min={1} placeholder="Capacity" className={inputClass} />
        <input name="googleMapsUrl" placeholder="Google Maps URL" inputMode="url" className={inputClass} />
        <div className="sm:col-span-2"><AddButton label="Save Venue" /></div>
      </form>
      {state && "error" in state ? <p className="text-sm text-danger">{String(state.error)}</p> : null}
    </div>
  );
}

export function ScheduleSection({ orgId, eventId, sessions }: { orgId: string; eventId: string; sessions: EventSession[] }) {
  const [state, formAction] = useFormState(addSessionAction.bind(null, orgId, eventId), null);
  const router = useRouter();
  const [pending, start] = useTransition();
  const ids = sessions.map((s) => s.id);

  function move(from: number, to: number) {
    if (to < 0 || to >= ids.length) return;
    const next = [...ids];
    const [m] = next.splice(from, 1);
    next.splice(to, 0, m);
    start(async () => {
      await reorderSessionsAction(orgId, eventId, next);
      router.refresh();
    });
  }

  return (
    <div className="space-y-4">
      {sessions.length === 0 ? (
        <p className="text-sm text-muted">No sessions yet.</p>
      ) : (
        <ul className="space-y-2">
          {sessions.map((s, i) => (
            <li key={s.id} className="rounded-md border border-border p-3 text-sm">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0 break-words">
                  <span className="font-medium">{s.title}</span>
                  <span className="ml-2 text-xs uppercase text-muted">{s.kind}</span>
                  <div className="text-muted">{new Date(s.starts_at).toLocaleString()} – {new Date(s.ends_at).toLocaleTimeString()}</div>
                </div>
                <div className="flex shrink-0 items-center gap-1">
                  <button type="button" disabled={pending || i === 0} onClick={() => move(i, i - 1)} aria-label="Move up" className="min-h-11 min-w-11 rounded border border-border px-1.5 py-1 text-xs text-muted disabled:opacity-40 lg:min-h-0 lg:min-w-0">↑</button>
                  <button type="button" disabled={pending || i === sessions.length - 1} onClick={() => move(i, i + 1)} aria-label="Move down" className="min-h-11 min-w-11 rounded border border-border px-1.5 py-1 text-xs text-muted disabled:opacity-40 lg:min-h-0 lg:min-w-0">↓</button>
                  <form action={deleteSessionAction.bind(null, orgId, eventId, s.id)}>
                    <ConfirmSubmitButton
                      label="Delete"
                      title={`Delete the session "${s.title}"?`}
                      description="It is removed from the published schedule. This cannot be undone."
                    />
                  </form>
                </div>
              </div>
              <details className="mt-2">
                <summary className="cursor-pointer text-xs text-accent-text">Edit</summary>
                {/* Times through `withUtcTimes` for the same reason the event's own start/end are
                    (D-289): `toLocalInput` renders them in the browser's zone, so the return leg has
                    to convert back or every save walks the session forward by the organiser's offset. */}
                <form action={(fd) => updateSessionAction(orgId, eventId, s.id, null, withUtcTimes(fd))} className="mt-2 grid gap-2 sm:grid-cols-2">
                  <input name="title" defaultValue={s.title} required className={inputClass} />
                  <select name="kind" defaultValue={s.kind} className={inputClass}>
                    <option value="Session">Session</option>
                    <option value="Break">Break</option>
                  </select>
                  <input name="startsAt" type="datetime-local" defaultValue={toLocalInput(s.starts_at)} required className={inputClass} />
                  <input name="endsAt" type="datetime-local" defaultValue={toLocalInput(s.ends_at)} required className={inputClass} />
                  <div className="sm:col-span-2"><Button type="submit" variant="secondary">Save session</Button></div>
                </form>
              </details>
            </li>
          ))}
        </ul>
      )}
      <form action={(fd) => formAction(withUtcTimes(fd))} className="grid gap-2 sm:grid-cols-2">
        <input name="title" placeholder="Session title" required className={inputClass} />
        <select name="kind" defaultValue="Session" className={inputClass}>
          <option value="Session">Session</option>
          <option value="Break">Break</option>
        </select>
        <input name="startsAt" type="datetime-local" required className={inputClass} />
        <input name="endsAt" type="datetime-local" required className={inputClass} />
        <div className="sm:col-span-2"><AddButton label="Add to Schedule" /></div>
      </form>
      {state && "error" in state ? <p className="text-sm text-danger">{String(state.error)}</p> : null}
    </div>
  );
}

export function SpeakersSection({ orgId, eventId, speakers }: { orgId: string; eventId: string; speakers: Speaker[] }) {
  const [state, formAction] = useFormState(addSpeakerAction.bind(null, orgId, eventId), null);
  return (
    <div className="space-y-4">
      {speakers.length === 0 ? (
        <p className="text-sm text-muted">No speakers yet.</p>
      ) : (
        <ul className="space-y-2">
          {speakers.map((s) => (
            <li key={s.id} className="rounded-md border border-border p-3 text-sm">
              <div className="flex items-start justify-between gap-2">
                <div className="flex min-w-0 items-center gap-2">
                  {s.user_id && <Avatar name={s.name} src={s.avatar_url ?? undefined} size={28} />}
                  <div className="min-w-0 break-words">
                    <div className="flex items-center gap-1.5">
                      {s.user_id && s.username ? (
                        <a href={`/u/${s.username}`} className="font-medium hover:text-accent-text">{s.name}</a>
                      ) : (
                        <span className="font-medium">{s.name}</span>
                      )}
                      {s.user_id && <Badge tone="accent">Linked account</Badge>}
                    </div>
                    {s.company ? <span className="text-muted">{s.role ? `${s.role}, ` : ""}{s.company}</span> : null}
                  </div>
                </div>
                <form action={removeSpeakerAction.bind(null, orgId, eventId, s.id)}>
                  <ConfirmSubmitButton
                    label="Remove"
                    title={`Remove ${s.name} from the speakers?`}
                    description="They no longer appear on the public event page."
                    confirmLabel="Remove"
                  />
                </form>
              </div>
              <details className="mt-2">
                <summary className="cursor-pointer text-xs text-accent-text">Edit</summary>
                <form action={updateSpeakerAction.bind(null, orgId, eventId, s.id, null)} className="mt-2 grid gap-2 sm:grid-cols-2">
                  <input name="name" defaultValue={s.name} required className={inputClass} />
                  <input name="company" defaultValue={s.company} placeholder="Company" className={inputClass} />
                  <input name="role" defaultValue={s.role} placeholder="Role" className={inputClass} />
                  <input name="bio" defaultValue={s.bio} placeholder="Bio" className={inputClass} />
                  <input name="linkUsername" defaultValue={s.username ?? ""} placeholder="@username (optional — link to a Kurx account)" className={inputClass} />
                  <div className="sm:col-span-2"><Button type="submit" variant="secondary">Save speaker</Button></div>
                </form>
              </details>
            </li>
          ))}
        </ul>
      )}
      <form action={formAction} className="grid gap-2 sm:grid-cols-2">
        <input name="name" placeholder="Speaker name" required className={inputClass} />
        <input name="company" placeholder="Company" className={inputClass} />
        <input name="role" placeholder="Role" className={inputClass} />
        <div className="sm:col-span-2"><AddButton label="Add Speaker" /></div>
      </form>
      {state && "error" in state ? <p className="text-sm text-danger">{String(state.error)}</p> : null}
    </div>
  );
}

export function SponsorsSection({ orgId, eventId, sponsors }: { orgId: string; eventId: string; sponsors: Sponsor[] }) {
  const [state, formAction] = useFormState(addSponsorAction.bind(null, orgId, eventId), null);
  return (
    <div className="space-y-4">
      {sponsors.length === 0 ? (
        <p className="text-sm text-muted">No sponsors yet.</p>
      ) : (
        <ul className="space-y-2">
          {sponsors.map((s) => (
            <li key={s.id} className="rounded-md border border-border p-3 text-sm">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0 break-words">
                  <span className="font-medium">{s.name}</span>
                  <span className="ml-2 text-xs uppercase text-muted">{s.tier}</span>
                </div>
                <form action={removeSponsorAction.bind(null, orgId, eventId, s.id)}>
                  <ConfirmSubmitButton
                    label="Remove"
                    title={`Remove ${s.name} from the sponsors?`}
                    description="They no longer appear on the public event page."
                    confirmLabel="Remove"
                  />
                </form>
              </div>
              <details className="mt-2">
                <summary className="cursor-pointer text-xs text-accent-text">Edit</summary>
                <form action={updateSponsorAction.bind(null, orgId, eventId, s.id, null)} className="mt-2 grid gap-2 sm:grid-cols-2">
                  <input name="name" defaultValue={s.name} required className={inputClass} />
                  <select name="tier" defaultValue={s.tier} className={inputClass}>
                    <option value="Platinum">Platinum</option>
                    <option value="Gold">Gold</option>
                    <option value="Silver">Silver</option>
                    <option value="Bronze">Bronze</option>
                    <option value="Partner">Partner</option>
                  </select>
                  <input name="website" defaultValue={s.website} placeholder="Website" inputMode="url" className="min-w-0 max-w-full sm:col-span-2" />
                  <div className="sm:col-span-2"><Button type="submit" variant="secondary">Save sponsor</Button></div>
                </form>
              </details>
            </li>
          ))}
        </ul>
      )}
      <form action={formAction} className="grid gap-2 sm:grid-cols-2">
        <input name="name" placeholder="Sponsor name" required className={inputClass} />
        <select name="tier" defaultValue="Partner" className={inputClass}>
          <option value="Platinum">Platinum</option>
          <option value="Gold">Gold</option>
          <option value="Silver">Silver</option>
          <option value="Bronze">Bronze</option>
          <option value="Partner">Partner</option>
        </select>
        <input name="website" placeholder="Website" inputMode="url" className="min-w-0 max-w-full sm:col-span-2" />
        <div className="sm:col-span-2"><AddButton label="Add Sponsor" /></div>
      </form>
      {state && "error" in state ? <p className="text-sm text-danger">{String(state.error)}</p> : null}
    </div>
  );
}

export function MediaSection({ orgId, eventId, media }: { orgId: string; eventId: string; media: EventMediaView[] }) {
  const [state, formAction] = useFormState(uploadEventMediaAction.bind(null, orgId, eventId), null);
  return (
    <div className="space-y-4">
      {media.length === 0 ? (
        <p className="text-sm text-muted">No gallery images or documents uploaded yet.</p>
      ) : (
        <ul className="space-y-2">
          {media.map((m) => (
            <li key={m.id} className="flex items-center justify-between gap-2 rounded-md border border-border p-3 text-sm">
              <span className="min-w-0 break-words">
                <span className="text-xs uppercase text-muted">{m.kind}</span>
                <span className="ml-2">{m.caption || m.key}</span>
              </span>
              <form action={deleteMediaAction.bind(null, orgId, eventId, m.id)}>
                <ConfirmSubmitButton
                  label="Delete"
                  title="Delete this media item?"
                  description="It is removed from the public event page. This cannot be undone."
                />
              </form>
            </li>
          ))}
        </ul>
      )}
      <form action={formAction} className="grid gap-2 sm:grid-cols-2">
        <select name="kind" defaultValue="Gallery" className={inputClass}>
          <option value="Gallery">Gallery</option>
          <option value="Document">Document</option>
          <option value="Poster">Poster</option>
          <option value="Brochure">Brochure</option>
          <option value="RulesPdf">Rules PDF</option>
        </select>
        <input name="caption" placeholder="Caption (optional)" className={inputClass} />
        <input name="file" type="file" required className="sm:col-span-2 min-w-0 max-w-full text-sm" />
        <div className="sm:col-span-2"><AddButton label="Upload" /></div>
      </form>
      {state && "error" in state ? <p className="text-sm text-danger">{String(state.error)}</p> : null}
    </div>
  );
}

/**
 * The event banner (D-302) — the image every card and detail hero renders.
 *
 * Separate from `MediaSection` above because they write to different places: the gallery attaches
 * `event_media` rows, while the banner is a field on the event itself. Offering "Banner" in that
 * section's kind dropdown would have created a row nothing reads, which looks like success and changes
 * nothing anyone can see.
 */
export function BannerSection({
  orgId,
  eventId,
  bannerUrl
}: {
  orgId: string;
  eventId: string;
  bannerUrl: string | null;
}) {
  const [state, formAction] = useFormState(uploadEventBannerAction.bind(null, orgId, eventId), null);
  const [removing, startRemove] = useTransition();
  const router = useRouter();

  return (
    <div className="space-y-4">
      <div className="aspect-[3/2] w-full max-w-md overflow-hidden rounded-lg border border-border bg-elevated">
        {bannerUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={bannerUrl} alt="Current event banner" className="h-full w-full object-cover" />
        ) : (
          <div className="flex h-full items-center justify-center p-4 text-center text-sm text-muted">
            No banner yet. Attendees see generated artwork until you add one.
          </div>
        )}
      </div>

      <form action={formAction} className="space-y-2">
        <label className="block text-sm font-medium" htmlFor="banner">
          {bannerUrl ? "Replace banner" : "Upload banner"}
        </label>
        <input
          id="banner"
          name="banner"
          type="file"
          required
          accept="image/jpeg,image/png,image/webp,image/avif"
          className="block max-w-full text-sm"
        />
        <p className="text-xs text-muted">JPEG, PNG, WebP or AVIF · up to 10 MB · shown at 3:2, so a wide image crops best.</p>
        <AddButton label={bannerUrl ? "Replace" : "Upload"} />
      </form>

      {bannerUrl ? (
        <Button
          type="button"
          variant="ghost"
          disabled={removing}
          onClick={() =>
            startRemove(async () => {
              await removeEventBannerAction(orgId, eventId);
              router.refresh();
            })
          }
        >
          {removing ? "Removing…" : "Remove banner"}
        </Button>
      ) : null}

      {state && !state.ok ? <p className="text-sm text-danger">{state.error}</p> : null}
    </div>
  );
}
