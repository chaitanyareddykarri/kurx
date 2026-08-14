"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { AtSign, ExternalLink, FileCheck2, Search, Upload, X } from "lucide-react";
import { Button, Field, Input, Select, Spinner, controlClass } from "@kurx/ui";
import { submitAuthorizationAction, uploadAuthorizationDocumentAction } from "@/lib/event-actions";
import { searchUsersAction } from "@/lib/invitation-actions";
import type { EventAuthorizationView } from "@/lib/api";

const inputClass = controlClass;


const STATUS_COPY: Record<string, { label: string; tone: string; hint: string }> = {
  // `Submitted` means "filed", not "being looked at". An authorization is reviewed as PART of the event's
  // review — it is the checklist item that gates Approve — so while the event is still a draft nobody is
  // reading it and nobody will until it is submitted. Saying otherwise leaves an organiser waiting on a
  // review that was never queued. The hint is resolved per event status below; this is the in-review case.
  Submitted: { label: "Under review", tone: "text-warning", hint: "A reviewer is checking your authorization." },
  Approved: { label: "Approved", tone: "text-success", hint: "Your event can be published." },
  Rejected: { label: "Rejected", tone: "text-danger", hint: "Fix the issue below and file it again." },
  ChangesRequested: { label: "Changes requested", tone: "text-warning", hint: "See the reviewer's note below, then file it again." },
};

/**
 * D-266 M5/M8 — the organiser's side of institutional authorization.
 *
 * A Public event that represents an organization cannot publish until that organization's written
 * authorization is approved. Without this form the gate would be unactionable: the workspace would say
 * "you need an authorization" and offer nowhere to provide one.
 *
 * The document never travels through the API — a presigned PUT takes the bytes, and only the storage key
 * it returns is submitted.
 */
export function AuthorizationForm({ eventId, existing, eventStatus, roles }: {
  eventId: string;
  existing: EventAuthorizationView | null;
  /** The closed vocabulary, fetched from the server that validates it. Never a copy held here: a list
   *  that drifts offers a role the API will refuse, and the organiser cannot tell which is wrong. */
  roles: string[];
  /** The EVENT's status. A filed authorization is only actually being read once the event itself is in
   *  review — see the note on STATUS_COPY.Submitted. */
  eventStatus?: string;
}) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [letterheadKey, setLetterheadKey] = useState<string | null>(null);
  const [fileName, setFileName] = useState<string | null>(null);
  const [form, setForm] = useState({
    headName: existing?.head_name ?? "",
    headDesignation: existing?.head_designation ?? "",
    officialEmail: existing?.official_email ?? "",
    officialPhone: existing?.official_phone ?? "",
    representativeRole: existing?.representative_role ?? "",
    representativeRoleOther: existing?.representative_role_other ?? "",
  });
  // The linked account is a LINK, never a grant — naming someone here gives them nothing (D-269).
  const [linked, setLinked] = useState<{ id: string; username: string } | null>(
    existing?.representative_user_id && existing.representative_username
      ? { id: existing.representative_user_id, username: existing.representative_username }
      : null,
  );

  const inReview = ["pendingreview", "underreview"].includes((eventStatus ?? "").toLowerCase());
  const status = existing ? STATUS_COPY[existing.status] : null;
  // Corrects the one status whose truth depends on the EVENT rather than the authorization: "Submitted"
  // means filed, and nobody reads it until the event itself is in review.
  const hint = existing?.status === "Submitted" && !inReview
    ? "Filed. It'll be reviewed once you submit this event for review."
    : status?.hint;
  const approved = existing?.status === "Approved";
  // An approved authorization is collapsed by default — there is nothing to do — but replacing it must
  // stay POSSIBLE. Hiding the form outright left an organiser with no way to correct a letter that named
  // the wrong signatory, with the backend perfectly able to accept one.
  const [replacing, setReplacing] = useState(false);
  const editable = !approved || replacing;

  /// Mirrors the server's clamp. Without this an oversized file is presigned for 10 MB, the PUT to
  /// storage then fails, and the only feedback is "could not be uploaded" — which reads as a bug rather
  /// than as "your file is too big".
  const MAX_BYTES = 10_000_000;

  function upload(file: File) {
    if (file.size > MAX_BYTES) {
      setError(`That file is ${(file.size / 1_000_000).toFixed(1)} MB. The limit is 10 MB.`);
      return;
    }
    startTransition(async () => {
      setError(null);
      /*
       * The previously attached file is dropped BEFORE the new one is attempted.
       *
       * Without this, a failed replace left the old key and the old filename in place while the error
       * said the upload had failed — so the form showed a letter attached, and submitting would have
       * filed the *previous* document under the new details. On an authorization letter, which is the
       * evidence a reviewer approves the event against, that is the wrong document going on record.
       */
      setLetterheadKey(null);
      setFileName(null);

      try {
        const res = await uploadAuthorizationDocumentAction(eventId, file.type || "application/pdf", file.size);
        if ("error" in res) { setError(res.error ?? "Upload could not be prepared."); return; }
        // The presigned PUT goes straight to storage — the bytes never pass through our API.
        const put = await fetch(res.url, { method: "PUT", headers: res.headers, body: file });
        if (!put.ok) { setError("The file could not be uploaded. Please try again."); return; }
        setLetterheadKey(res.key);
        setFileName(file.name);
      } catch {
        // A dropped connection rejects `fetch` rather than returning a non-ok response, and that
        // rejection went unhandled inside the transition — the button simply re-enabled.
        setError("The file could not be uploaded — the connection dropped. Please try again.");
      }
    });
  }

  function submit() {
    startTransition(async () => {
      setError(null);
      // Only a first filing must carry a letter. On a re-file an omitted key means "keep the one on
      // file" — the client is never given the stored key (reads return presigned URLs), so demanding one
      // here would force re-uploading an unchanged letter just to correct a phone number.
      if (!letterheadKey && !existing?.letterhead_url) {
        setError("Attach the authorization letter before submitting.");
        return;
      }
      const res = await submitAuthorizationAction(eventId, {
        ...form,
        // Only meaningful when the role is `Other`; sending it otherwise would store a contradiction.
        representativeRoleOther: form.representativeRole === "Other" ? form.representativeRoleOther : undefined,
        representativeUserId: linked?.id,
        letterheadDocumentKey: letterheadKey ?? undefined,
      });
      if ("error" in res) { setError(res.error ?? "That could not be submitted."); return; }
      router.refresh();
    });
  }

  return (
    <section className="space-y-4 rounded-lg border border-border p-4">
      <header className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-text">
          <FileCheck2 size={16} aria-hidden /> Organization authorization
        </h2>
        {status ? <span className={`text-xs font-semibold ${status.tone}`}>{status.label}</span> : null}
      </header>

      <p className="text-sm text-muted">
        {hint ??
          "This event is being run on behalf of an organization, so we need written authorization from someone who can speak for it before the event goes live."}
      </p>

      {existing?.reason_code || existing?.notes ? (
        <p className="rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-sm text-text">
          {existing.reason_code ? <strong>{existing.reason_code}</strong> : null}
          {existing.notes ? <span> — {existing.notes}</span> : null}
        </p>
      ) : null}

      {editable ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {/*
            These were `<label>`s wrapping both the control AND its explanatory `<span>`, which makes
            the explanation part of the field's ACCESSIBLE NAME rather than its description. "Official
            email" announced as *"Official email An address at the organization's own domain gets
            reviewed faster. A personal one is accepted, but then the letter has to carry the whole
            claim"* — a twenty-five word name on an email box. `Field` puts a helper on
            `aria-describedby`, where it is read after the name and can be skipped.
          */}
          <Field label="Name of the person authorizing">
            <Input value={form.headName}
              onChange={(e) => setForm({ ...form, headName: e.target.value })} />
          </Field>
          <Field label="Their designation">
            <Input value={form.headDesignation} placeholder="e.g. Head of Department"
              onChange={(e) => setForm({ ...form, headDesignation: e.target.value })} />
          </Field>
          <Field
            label="Official email"
            helper="An address at the organization's own domain gets reviewed faster. A personal one is accepted, but then the letter has to carry the whole claim."
          >
            <Input type="email" value={form.officialEmail}
              onChange={(e) => setForm({ ...form, officialEmail: e.target.value })} />
          </Field>
          <Field
            label="Official phone"
            helper="A reviewer may call to confirm the letter. Include the country code."
          >
            <Input value={form.officialPhone} placeholder="+919876543210" inputMode="tel"
              onChange={(e) => setForm({ ...form, officialPhone: e.target.value })} />
          </Field>

          <Field label="Their role at the organization">
            <Select value={form.representativeRole}
              onChange={(e) => setForm({ ...form, representativeRole: e.target.value })}>
              <option value="" disabled>Select a role</option>
              {/* The server-published vocabulary, never a client copy (D-266 M5 addendum 2). */}
              {roles.map((r) => <option key={r} value={r}>{r}</option>)}
            </Select>
          </Field>

          {form.representativeRole === "Other" ? (
            <Field label="Type their role">
              <Input value={form.representativeRoleOther} placeholder="e.g. Chief Mentor"
                onChange={(e) => setForm({ ...form, representativeRoleOther: e.target.value })} />
            </Field>
          ) : null}

          <div className="sm:col-span-2">
            <RepresentativePicker linked={linked} onChange={setLinked} />
          </div>

          <div className="sm:col-span-2">
            <p id="auth-letter-label" className="text-sm text-muted">
              Authorization letter (on the organization&apos;s letterhead)
            </p>
            {/* The 10 MB limit was only revealed after a too-large file had been chosen. */}
            <p id="auth-letter-help" className="mt-0.5 text-xs text-muted">
              PDF or image, up to 10 MB.
            </p>
            <div className="mt-2 flex items-center gap-2">
              <input type="file" accept="application/pdf,image/*" className="sr-only" id="auth-letter"
                aria-labelledby="auth-letter-label" aria-describedby="auth-letter-help"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) upload(f); }} />
              {/* `sr-only`, not `hidden`: a hidden input is removed from the accessibility tree and
                  cannot be reached at all, so the only route to it was this button forwarding a
                  click. It is now a real, focusable control that the button also drives. */}
              <Button type="button" variant="secondary" disabled={isPending}
                onClick={() => document.getElementById("auth-letter")?.click()}>
                {isPending ? <Spinner size={14} decorative /> : <Upload size={14} aria-hidden className="mr-1.5" />}
                {isPending ? "Uploading…" : fileName ? "Replace file" : "Attach letter"}
              </Button>
              <span role="status" className="truncate text-xs text-text">
                {fileName ? `${fileName} attached.` : ""}
              </span>
            </div>
          </div>
        </div>
      ) : null}

      {/* What is on file. Shown whatever the status, so an organiser can always check the letter a
          reviewer is looking at — or has already approved. Links are short-lived presigned URLs. */}
      {existing && (existing.letterhead_url || existing.supporting_document_urls.length > 0) ? (
        <div className="flex flex-wrap gap-2">
          {existing.letterhead_url ? <DocLink href={existing.letterhead_url} label="Letterhead" /> : null}
          {existing.signature_url ? <DocLink href={existing.signature_url} label="Signature" /> : null}
          {existing.supporting_document_urls.map((u, i) => (
            <DocLink key={u} href={u} label={`Supporting ${i + 1}`} />
          ))}
        </div>
      ) : null}

      {approved && !replacing ? (
        <div className="space-y-2">
          <Button variant="secondary" onClick={() => setReplacing(true)}>Change details or documents</Button>
          <p className="text-xs text-muted">
            Any change — including the signatory or their role — returns this authorization to review, so the
            event can&apos;t publish again until a reviewer approves it. An approval describes the details a
            reviewer actually read.
          </p>
        </div>
      ) : null}

      {error ? (
        <p role="alert" className="rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
          {error}
        </p>
      ) : null}

      {editable ? (
        <Button onClick={submit} disabled={isPending}>
          {isPending ? "Submitting…" : existing ? "Resubmit for review" : "Submit for review"}
        </Button>
      ) : null}

      {existing && editable ? (
        <p className="text-xs text-muted">
          Resubmitting replaces the details and letter on file, and returns it to review.
        </p>
      ) : null}
    </section>
  );
}

/**
 * Optionally point at the signatory's Kurx account.
 *
 * **A link, never a grant.** Naming someone here gives them no authority over the event and changes
 * nothing about their account (D-269 owns authority) — it only lets a reviewer see the signatory is a
 * known person rather than a name typed into a form. Reads the same user index the invite picker uses, so
 * a signatory who is not discoverable simply cannot be linked, and the letter stands on its own.
 */
function RepresentativePicker({ linked, onChange }: {
  linked: { id: string; username: string } | null;
  onChange: (v: { id: string; username: string } | null) => void;
}) {
  const [q, setQ] = useState("");
  const [results, setResults] = useState<{ id: string; name: string; username: string }[]>([]);
  const [searching, startSearch] = useTransition();
  const seq = useRef(0);

  // Debounced, with a sequence guard so a slow earlier response cannot overwrite a newer one.
  useEffect(() => {
    const term = q.trim();
    if (term.length < 2) { setResults([]); return; }
    const mine = ++seq.current;
    const t = setTimeout(() => {
      startSearch(async () => {
        const res = await searchUsersAction(term);
        if (mine !== seq.current) return;
        setResults("error" in res ? [] : res.users);
      });
    }, 250);
    return () => clearTimeout(t);
  }, [q]);

  if (linked) {
    return (
      <div className="space-y-1">
        <span className="text-sm text-muted">Their Kurx account</span>
        <span className="flex items-center gap-2 rounded-md border border-border px-3 py-2">
          <AtSign size={13} aria-hidden className="text-muted" />
          <span className="flex-1 truncate text-sm text-text">{linked.username}</span>
          <button type="button" aria-label="Unlink this account" className="text-muted hover:text-text"
            onClick={() => { onChange(null); setQ(""); }}>
            <X size={14} aria-hidden />
          </button>
        </span>
        <span className="block text-xs text-muted">
          Linking is just a pointer — it gives them no access to this event.
        </span>
      </div>
    );
  }

  return (
    <div className="space-y-1">
      <label className="text-sm text-muted" htmlFor="rep-search">
        Their Kurx account (optional)
      </label>
      <div className="relative">
        <Search size={15} aria-hidden className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
        <input id="rep-search" value={q} onChange={(e) => setQ(e.target.value)}
          placeholder="Search by name or username" className={`${inputClass} pl-9`} />
      </div>

      {/* The list simply changed underneath as you typed, with nothing said about it. */}
      <p role="status" className="sr-only">
        {q.trim().length < 2
          ? ""
          : searching
            ? "Searching."
            : `${results.length} ${results.length === 1 ? "account matches" : "accounts match"}.`}
      </p>

      {q.trim().length >= 2 ? (
        <ul className="divide-y divide-border rounded-md border border-border">
          {searching && results.length === 0 ? (
            <li className="px-3 py-2 text-sm text-muted">Searching…</li>
          ) : results.length === 0 ? (
            <li className="px-3 py-2 text-sm text-muted">
              Nobody found. This is optional — leave it blank if they aren&apos;t on Kurx.
            </li>
          ) : (
            results.map((u) => (
              <li key={u.id}>
                <button type="button" className="flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-elevated"
                  onClick={() => { onChange({ id: u.id, username: u.username }); setResults([]); }}>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm text-text">{u.name || u.username}</span>
                    <span className="flex items-center gap-0.5 text-xs text-muted">
                      <AtSign size={11} aria-hidden />{u.username}
                    </span>
                  </span>
                </button>
              </li>
            ))
          )}
        </ul>
      ) : (
        <span className="block text-xs text-muted">
          If the signatory is on Kurx, linking them helps a reviewer confirm who they are.
        </span>
      )}
    </div>
  );
}

/** Presigned, short-lived URLs — the client never sees a storage key. */
function DocLink({ href, label }: { href: string; label: string }) {
  return (
    <a href={href} target="_blank" rel="noreferrer"
      className="inline-flex items-center gap-1 rounded border border-border px-2 py-1 text-xs text-text hover:bg-elevated">
      {label} <ExternalLink size={10} aria-hidden />
      <span className="sr-only"> (opens in a new tab)</span>
    </a>
  );
}
