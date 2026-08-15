# Certificate Platform — Implementation Progress

Living record of the 13-phase certificate module build. Updated after every phase and every major
milestone. A phase is **COMPLETE only when its implementation exists and its tests pass** — never merely
because files were created.

**Statuses:** `NOT STARTED` · `IN PROGRESS` · `BLOCKED` · `IMPLEMENTED` · `TESTING` · `COMPLETE` · `FAILED`

| Phase | Name | Status | Started | Completed | Tests | Notes |
|---|---|---|---|---|---|---|
| 1 | Domain + Database Foundation | COMPLETE | 2026-08-15 | 2026-08-15 | 24 pass | 9 tables, migration `AddCertificateModule`, atomic ID allocation |
| 2 | Storage | COMPLETE | 2026-08-15 | 2026-08-15 | 28 pass | S3-compatible `IStorage`, split endpoint, production guard |
| 3 | Template | COMPLETE | 2026-08-15 | 2026-08-15 | 28 backend + 30 web | CRUD, upload, fields, versioning, editor |
| 4 | Rendering | COMPLETE | 2026-08-15 | 2026-08-15 | 19 pass | QuestPDF, PDF + 300dpi PNG, QR, single issuance |
| 5 | Signing + Verification | COMPLETE | 2026-08-15 | 2026-08-15 | 17 pass | ES256, separate keys, public verification |
| 6 | Spreadsheet | COMPLETE | 2026-08-15 | 2026-08-15 | 58 backend + 19 web | CSV + XLSX behind `ISpreadsheetReader`, mapping review UI |
| 7 | Batch Generation | COMPLETE | 2026-08-15 | 2026-08-15 | 23 pass | Hangfire; preview → approve → full |
| 8 | Distribution | COMPLETE | 2026-08-15 | 2026-08-15 | 18 pass | Existing `IEmailSender` + outbox |
| 9 | Revocation + Reissue | COMPLETE | 2026-08-15 | 2026-08-15 | 19 pass | Immutable lineage |
| 10 | Participant Access | COMPLETE | 2026-08-15 | 2026-08-15 | 21 pass | Capability links, verified-email linking |
| 11 | Dashboard + Telemetry | COMPLETE | 2026-08-15 | 2026-08-15 | 23 pass | — |
| 12 | OCR | COMPLETE | 2026-08-15 | 2026-08-15 | 24 backend + 8 web | Boundary + stub only; no engine integrated |
| 13 | Template Reuse | COMPLETE | 2026-08-15 | 2026-08-15 | 19 pass | Copy-to-library and copy-to-event |

## Confirmed decisions governing this build

Separate modular certificate module inside Kurx · start clean, no restore from the removed designer ·
online verification only, no PAdES · separate certificate-signing keys reusing the existing signing/KMS
infrastructure, retained long-term · compromised key ⇒ certificate shows **valid with a
signing-key-compromised warning** · participants may be registered users **or** external people with no
account · verified-email linking only · `IStorage` with an S3-compatible production target · OCR deferred
to Phase 12 behind `ITextDetector` · XLSX via a maintained NuGet library behind `ISpreadsheetReader` ·
configurable public verification base URL · per-IP rate limiting plus suspicious-activity protection ·
long-lived revocable capability links · certificate IDs unique and immutable · revocation reissues under a
**new** certificate ID with full lineage · the three dormant tables (`certificates`, `design_templates`,
`id_cards`) are **not** deleted, repurposed or extended.

## Phase log

### Phase 1 — Domain + Database Foundation
**Started** 2026-08-15. Status: IN PROGRESS.

Conventions confirmed from the repository before writing anything:

- Entities live flat in `Kurx.Domain/Entities/*.cs`; enums are centralised in `Kurx.Domain/Enums/Enums.cs`.
- `Guid.NewGuid()` is the dominant id convention (136 uses vs 21 of `CreateVersion7`).
- **All enums persist as text** — a loop at the top of `OnModelCreating` sets the provider CLR type to
  `string` for every enum property, so no per-property conversion is needed.
- State vocabularies get a `CHECK` constraint via the existing `StateVocabulary<TEnum>(column)` helper.
- Relationships are configured **without navigation properties**: `HasOne<T>().WithMany().HasForeignKey(…)`.
  This is what lets the certificate module reference `Event`/`User` by id without coupling to them.
- Shared counters are mutated **in SQL**, never read-modify-write.
- Raw scalar SQL uses `db.Database.SqlQueryRaw<T>` with the column aliased `AS "Value"`.

**Completed** 2026-08-15. Status: COMPLETE.

**Tables (9):** `certificate_templates`, `certificate_template_fields`, `certificate_id_rules`,
`certificate_batches`, `certificate_recipients`, `issued_certificates`, `certificate_revocations`,
`certificate_deliveries`, `certificate_events`. Migration `AddCertificateModule` — additive only, zero
`DropTable`/`DropColumn`/`AlterColumn` in `Up`. The three dormant tables are not referenced by it at all.

**Certificate ID allocation.** One statement per call:
`UPDATE certificate_id_rules SET "NextSequence" = "NextSequence" + 1 WHERE "EventId" = @id RETURNING "NextSequence" - 1`.
Postgres holds the row lock and computes the new value; a concurrent caller blocks, then increments from
the committed value. The rule is created on first use with `INSERT … ON CONFLICT ("EventId") DO NOTHING`,
so two simultaneous first-generations cannot both insert. Read-modify-write is never performed anywhere.

**Guarantees in place:** unique index on `issued_certificates.CertificateId` (platform-wide — verification
has no event context); `CertificateId` marked `PropertySaveBehavior.Throw`, so EF refuses to update it
after insert; CHECK constraints on all four state vocabularies, generated from the enums.

**Tests:** 24 added, 24 pass. Includes 40-way concurrent allocation asserting distinct, contiguous
sequences; DB-level duplicate rejection; ID immutability; recipient with and without a `UserId`; arbitrary
and Unicode field keys; FK enforcement; event deletion preserving the creator's template; state-vocabulary
rejection via raw SQL.

**Full suite:** 1803 passed, 1 skipped, 4 failed — the 4 are `ClamAvUploadPathTests`, which require a
running clamd and were failing identically before this phase.

**Deviations:** none. One judgment call worth recording — `CertificateTemplate.Version` is a plain `int`
domain version, not EF's `IsRowVersion` optimistic-concurrency token. They answer different questions:
row-version detects "did someone else write while I was editing", whereas this must record "which layout
produced this certificate" and must not change on edits that do not affect rendering.

### Phase 2 — Storage
**Started** 2026-08-15. **Completed** 2026-08-15. Status: COMPLETE.

**Split endpoint, as decided.** `S3_ENDPOINT` is what the backend calls; `S3_PUBLIC_ENDPOINT` is what the
browser reaches. Two `IAmazonS3` clients: one for API calls, one used **only** to compute presigned URLs
and configured with the public endpoint, so the host that gets signed is the host in the URL. Nothing is
rewritten after signing — SigV4 covers the host, so a rewritten URL would 403. When
`S3_PUBLIC_ENDPOINT` is unset the two collapse to one client, which is the plain-AWS case.

**Addressing:** path-style when a custom `S3_ENDPOINT` is set (virtual-host would fold the bucket into the
hostname and need wildcard DNS most S3-compatible servers lack); the SDK's own virtual-host default for
real AWS. No new configuration variable for this.

**Production guard:** `STORAGE_PROVIDER=localdisk` now **throws at startup** under Production, matching
`FILE_SCANNER=none` (D-338). Previously it only logged a warning — so a deployment that forgot the
variable came up healthy, issued certificates, and lost every document on the next release while Postgres
kept the rows.

**Two real defects the tests caught, both fixed:**
1. `Uri.TryCreate(…, Absolute)` accepts `minio:9000` as scheme `minio` — a missing scheme sailed through
   validation and would have failed at the first request. Now an explicit http/https check.
2. **AWS SDK v4 emits `https://` for presigned S3 URLs even when `ServiceURL` is `http://`**, and there is
   no knob: `ClientConfig.UseHttp` documents itself as not applying once `ServiceURL` is set, and
   `AmazonS3Config` exposes no scheme property. Measured, not assumed. Designed around rather than hidden:
   `S3_PUBLIC_ENDPOINT` is now **required to be https** (it is browser-facing and carries private
   documents), and a test pins the behaviour so an SDK upgrade that changes it is caught. Server-side API
   calls are unaffected, so a plaintext internal `S3_ENDPOINT` remains supported.

**Security:** no ACL is ever set, so objects inherit the bucket's private default — certificates are never
world-readable. Credentials fall back to the SDK's default chain when no keys are configured. The secret
key never appears in a URL. `ExistsAsync` treats only HTTP 404 as absent and lets every other failure
propagate, so an outage cannot be mistaken for a missing file.

**Event-prefix ownership:** `CertificateStorageKeys` builds and validates in one place. Keys are ordinal-
and case-sensitive, event-scoped, and a template accepts only the prefix it actually lives under (event
vs. creator library). Background paths are versioned, so replacing artwork never overwrites the bytes an
issued certificate was rendered from.

**Tests:** 28 added, 28 pass — configuration validation, presigned host/scheme/signature/TTL, path-style
vs virtual-host, secret-leak check, production guard (refuses under Production, allows in development,
rejects unknown providers), and prefix isolation.

**One existing test fixture completed, not weakened:** `FileScannerProductionGuardTests` builds a
Production configuration, and the new storage guard runs *before* the scanner guard — so it began
asserting the storage refusal instead of the scanner refusal. Adding `STORAGE_PROVIDER=s3` + bucket +
region to its fixture restores it to testing the scanner.

**Full suite:** 1831 passed, 1 skipped, 4 failed — the same 4 `ClamAvUploadPathTests` needing a clamd.

**Not implemented, by design:** live round-trip tests against a real S3-compatible server. Presigning is a
local computation and is fully asserted; `PutAsync`/`GetAsync` need a live bucket, and asserting them
against a fake would only prove the fake works.

### Phase 3 — Template
**Started** 2026-08-15. **Completed** 2026-08-15. Status: COMPLETE.

**Backend.** `ICertificateTemplateService` + implementation + `CertificateTemplateEndpoints`. Template
CRUD, presign → PUT → record artwork upload, whole-set field replace, archive (never hard delete —
certificates record template id and version).

**Two authority shapes, not one.** An event template resolves through `IEventAuthority`
(`ManageContent`); a library template has no event, so ownership is the entire rule. Platform admins are
deliberately excluded from library templates: a creator's private design library is not event content and
nothing in the requirements asks staff to browse it. A template that is not yours is `not_found`, never
`forbidden` (D-018).

**Versioning works as designed.** `Version` bumps on a render-affecting edit **only once certificates have
been issued** — before that a template is still being drafted and bumping on every keystroke would make
the number meaningless. Replacement artwork is presigned under the *next* version's path, so the bytes an
issued certificate was rendered from are never overwritten. Verified end to end: after issuing, an edit
produces v2 while the issued certificate still reads v1.

**Artwork is guarded three ways:** content type restricted to what the renderer can actually decode; the
storage key must sit under the template's own prefix (event *or* creator library, whichever it lives in);
and the object must actually exist — a key that was presigned but never written is refused, because a
template pointing at nothing renders as a blank page and nobody finds out until generation.

**Web.** `certificate-api.ts`, `certificate-actions.ts`, a DOM-free `certificate-editor.ts`, a shared
`CertificateCanvas`, `BackgroundUpload`, the editor and the list, plus two routes and a nav entry.
**Simple by default, Advanced on demand** — exact coordinates, rotation, depth and masking sit behind a
toggle, because the common case is "put the name here".

**Cover-and-replace is stated plainly in the UI**, not hidden: the artwork cannot be un-printed, so a
masking field paints over the region and writes on top, and moving it reveals the original. The editor
says exactly that.

**No OCR anywhere** — every field is placed by the creator, which is the confirmed requirement that the
editor must be complete without detection.

**One real bug caught by the new tests:** `normaliseOrder` re-sorted by the *old* z_order after a reorder
had already rearranged the array, silently undoing the move. Split into `renumber` (by array position)
and `normaliseOrder` (sort, then renumber).

**Tests:** 28 backend (authority, cross-event isolation, library ownership, artwork prefix/existence/
content-type, arbitrary and Unicode field keys, masking persistence, bounds, colour validation, version
bump behaviour, archive) and 30 web (geometry, clamping, depth, duplication, masking padding, history,
save shape, immutability). All pass.

**Full suites:** backend 1859 passed / 1 skipped / 4 failed (the documented ClamAV four); web 602 passed
/ 1 skipped.

### Phase 4 — Rendering
**Started** 2026-08-15. **Completed** 2026-08-15. Status: COMPLETE.

**One renderer, one assembly path.** `ICertificateDocumentRenderer` (named distinctly from the pre-existing
`ICertificateRenderer`, which belongs to the older hardcoded-layout code and is not part of this module).
`CertificateIssuingService.BuildDocumentAsync` is the *only* place a template becomes a renderable
document — preview and issue both go through it, which is what makes "the preview matches the output"
structural rather than a promise.

**PNG is rasterised from the document, not from the PDF.** Routing it through `IDocumentRasterizer` would
produce a blank 1×1 image for every certificate — its only implementation is a stub that says so in its own
log line. A test asserts the preview PNG is a real page (>500px), which would catch that regression.

**Output:** print-ready PDF at A4 in either orientation, plus a 300dpi PNG; previews at 150dpi. Uploaded
artwork is painted `contain` and never stretched — the upload IS the design, and reshaping it damages the
only artefact the creator made.

**Issuance** allocates the id, renders both artefacts, stores them, hashes the PDF (`DocumentSha256` pins
the exact artefact), snapshots every substituted value onto the row, and creates the recipient — with no
account required. Required fields refuse rather than printing a blank.

**A real design flaw found by the tests.** Certificate IDs are unique **platform-wide** (verification has
no event context) but sequences are **per-event** — so with a shared default prefix, *every* event's first
certificate collided with every other event's first. Not an edge case: the common path. Fixed by defaulting
the prefix to `Event.ShortCode`, which is already unique per event, making ids distinct by construction. An
organiser may still configure any prefix; if two events are given the same one, the unique index refuses
the insert loudly at issue time rather than handing two people the same identifier.

**Tests:** 19 added. Rendering is asserted by *observable difference* rather than by parsing PDFs — a
substituted value must change the document, artwork must change it, a QR with a payload must differ from
one without, a masking fill must paint. If a field silently failed to draw (QuestPDF drops content that
does not fit a hard-height box) the renders would be byte-identical and these fail. Plus print-vs-preview
resolution, both orientations, oversized text being shrunk rather than dropped, malformed colours not
throwing, and authority.

**Full suite:** 1878 passed, 1 skipped, 4 failed — the documented ClamAV four.

### Phase 5 — Signing + Verification
**Started** 2026-08-15. **Completed** 2026-08-15. Status: COMPLETE.

**Separate certificate signing keys, in the module's own table.** `certificate_signing_keys` reuses the
platform's existing `ISigningKeyProtector` — so the private half is KMS-wrapped in production exactly as
JWT keys are — but does **not** share the JWT key table, because the two have opposite lifetimes. A JWT key
is retired and discarded within minutes; a certificate must verify for years. The public half is therefore
**never nulled**, and rows are never deleted. Migration `AddCertificateSigningKeys`, additive.

**What the signature actually buys, stated honestly.** Verification is online against our own database, so
the signature does not prove the certificate exists — the row does. It proves the row has not been
*altered*: a directly-edited `issued_certificates` record fails verification instead of verifying as
genuine. That is real and worthwhile, and narrower than "offline-verifiable", which this deliberately is
not. One signature covers all three required properties: platform issuance (only we hold the key), payload
integrity (it covers every displayed field), and correspondence to *this* id (the id is inside the payload,
so a signature cannot be transplanted).

**The canonical payload is deterministic or nothing works.** It is rebuilt from the stored row at every
verification, so any variation — dictionary ordering, culture-formatted dates, an ambiguous separator —
would make every existing certificate fail, which looks exactly like tampering. Values are ordinal-sorted
and joined with ASCII unit/record separators that cannot occur in a key or value; dates are round-trip UTC.
A format version is embedded so a future format can verify alongside this one rather than invalidating
history.

**Six verification outcomes, none collapsible:** `Valid`, `Revoked`, `Superseded`, `NotFound`, `Tampered`,
`Unavailable`. The load-bearing one is the last: **every unexpected failure becomes `Unavailable` (HTTP
503), never "not valid"**. This page tells someone whether a credential is real, and answering "no" because
a connection dropped is the worst thing it can do. `Tampered` is the only outcome that means do-not-trust.

**A compromised key reports the certificate as valid, with a warning** — per the confirmed decision. It
really was issued by the platform; calling it fake because of an operational incident on our side would be
the wrong lie. A retired key still verifies too.

**Disclosure is an allowlist, not a filter.** Only fields the certificate already prints are returned. A
spreadsheet may carry an employee number or a personal address the design never showed, and an anonymous
endpoint must not become a way to read it back. A test asserts exactly that.

**Public surface:** `GET /v1/verify/{certificateId}`, anonymous, under a new per-IP `verify` rate-limit
policy (`RATE_LIMIT_VERIFY_PER_MIN`, default 30). Certificate ids are sequential and enumerable by design,
so the mitigation is a low ceiling plus a minimal body rather than pretending they are secret. Unknown and
malformed ids return an identical `NotFound`.

**`CERTIFICATE_VERIFICATION_BASE_URL`** is the configurable public origin behind `ICertificateVerificationLinks`
— its own boundary so the QR, the emailed link and the shared link cannot drift apart. Deliberately not
`KURX_API_BASE`: a QR pointing at the API host would take a verifier to JSON. It is printed permanently onto
every certificate and cannot be corrected afterwards, which is documented in `.env.example`.

**Tests:** 17 added — payload determinism and collision-resistance, sign/verify round trip, unknown key
reported as unknown rather than mismatch, malformed signature not throwing, protection at rest, all six
outcomes, tampering detection via a direct database edit, compromised- and retired-key behaviour, the
disclosure allowlist, and configurable link building.

**Full suite:** 1895 passed, 1 skipped, 4 failed — the documented ClamAV four.





---

### Phase 6 — Spreadsheet

**Status:** COMPLETE — 58 backend + 19 web tests, all passing.

**The rule the whole phase is built around: nothing is reinterpreted.** Every cell reaches the renderer as
the string the organiser sees in their spreadsheet. The classic import failure is a parser being helpful —
`007` becoming `7`, a date becoming `44562`, a phone number becoming scientific notation — and here that
lands on a printed certificate that has already been emailed. `ISpreadsheetReader` is therefore
deliberately unhelpful: no delimiter sniffing, no type coercion, no locale handling, no formula
evaluation.

**`ClosedXML 0.105.1` added** — the one new dependency, authorised explicitly. XLSX is a ZIP of
schema-heavy XML with a shared-string table and number-format resolution; hand-parsing it to read a
participant list would be a far larger liability than the package.

**Formulas are never evaluated.** A cell carrying `=A1` is read from its *cached* value — what Excel last
stored, which is also what the organiser saw when they saved. Evaluating a stranger's uploaded workbook is
a much larger attack surface than reading one; formulas can reference external workbooks.

**Cells are read as displayed, not as typed** (`GetFormattedString()`), which is what keeps a
text-formatted `007` as `007` and a date as `12/09/2026` rather than its serial number.

**CSV is hand-rolled, and only just.** RFC 4180 is small and the reader implements all of it — quoted
fields, embedded commas, embedded newlines, doubled quotes, CRLF, bare LF, lone CR, and Excel's UTF-8 BOM.
A BOM left in place becomes an invisible prefix on the first header, so `Name` silently stops matching the
column called `Name` with nothing on screen to explain it.

**Limits, and truncation that is never silent.** 10MB per file, 5000 rows, 200 columns. `SpreadsheetTable`
carries a `Truncated` flag surfaced all the way to the UI, because "we generated 1000 of your 1500" must
never be discovered by counting.

**Format is chosen by extension, not by sniffing.** Content sniffing on an untrusted upload is a larger
attack surface than a filename check, and a `.numbers` file renamed to `.csv` should produce a clear
refusal rather than a parser that guesses and yields plausible nonsense. Malformed files fail as
`invalid_spreadsheet` — a refusal the organiser can act on, never a 500 that looks like the platform is
broken when the upload is what is wrong.

**Duplicate and blank headers are tolerated, not refused.** Real exports contain both. The mapping screen
is where the ambiguity is resolved.

**The mapping is suggested, never applied silently.** A column called "Name" holding a *team* name is an
ordinary spreadsheet; a silent mapping prints the wrong words on every certificate in the run and is
discovered after they are sent. `SuggestMapping` pre-fills a guess over an open vocabulary (an unrecognised
column is mapped by hand rather than refused), and first column wins per field so a sheet with both "Name"
and "Full Name" does not have the second silently overwrite the first.

**Public surface:** `POST /v1/events/{eventId}/certificate-participants/preview` — multipart, authorized
via `IEventAuthority`/`ManageContent` *before* a byte is parsed, 404-not-403 on a hidden event (D-018),
under the `heavy` rate-limit policy. It reads the file and throws it away; nothing is persisted until the
organiser has confirmed the mapping. The declared `Content-Length` is checked *and* the bytes counted as
they stream, because a claim is not a fact.

**Organiser UI:** `ParticipantMapping` renders each column with the guess pre-selected and real sample
values beneath it, lists placeholders still unfilled, names the columns being ignored, warns about sample
rows whose required value is blank, and blocks the continue button while any required field is unmapped.
The decision logic lives in `web/lib/certificate-mapping.ts`, pure and DOM-free.

**Deferred to Phase 7 by design:** writing the confirmed mapping to `CertificateBatch.ColumnMappingJson`.
The column exists (Phase 1) and the client-side model is done, but batches are created in Phase 7 and
building a second batch-creation path here would duplicate it.

**Tests:** 47 reader tests (RFC 4180 grammar, BOM, all three line endings, embedded delimiters/quotes/
newlines, blank and ragged rows, no coercion in both formats, leading zeros, dates, Unicode, formulas never
evaluated, duplicate/blank headers, row ceiling with `Truncated` reported, extension selection, safe
failure, CSV and XLSX producing identical tables, mapping suggestions) + 11 endpoint tests (anonymous,
outsider, unknown event, happy path, no reinterpretation end to end, sample-not-whole-list, four refusal
codes, oversized upload) + 19 web tests (assign/unassign, one column per field, leftovers, row resolution,
blank required values).

**Suites:** backend 1953 passed / 1 skipped / 4 failed (the documented ClamAV four — clamd is not running;
they fail identically on a clean checkout). +58 from the 1895 baseline, no regressions. Web 621 passed /
1 skipped, up from 602.

---

### Phase 7 — Batch Generation

**Status:** COMPLETE — 23 tests. Full suite 1976 passed / 1 skipped / 4 failed (the ClamAV four).

**The approval gate is the design.** Everything before it is cheap and reversible — a parsed file, some
recipient rows, three sample renders. Everything after it is hundreds of signed PDFs in object storage,
some of which may already have been emailed. So `ApproveAsync` refuses a batch that was never previewed,
and `RunAsync` independently refuses one with no `ApprovedAt`. Both checks exist because a queued job
outlives the request that queued it: re-checking at run time is what makes a cancelled or unapproved batch
safe even when the job fires.

**Idempotency lives in the database, not the loop.** A unique *filtered* index on
`issued_certificates(BatchId, RecipientId)`. A skip-list read at the top of a loop stops being true the
moment two workers pick up the same job, so the guarantee is placed where concurrency cannot get around
it. The filter is what keeps hand-issued certificates unconstrained — several may legitimately go to one
recipient when no batch is involved. A second unique filtered index on
`certificate_recipients(BatchId, SourceRowNumber)` keeps the row → recipient lookup unambiguous.

**Assemble once, render many.** `ICertificateIssuingService.PrepareAsync` builds the document and reads
the artwork out of storage a single time per run; `IssuePreparedAsync` renders each row against it. Per-row
assembly would mean one multi-megabyte storage fetch per certificate — the difference between a run that
takes a minute and one that takes an hour. `CertificateRenderPlan` is immutable and passed back in rather
than cached inside the service: hidden state keyed by template id would be wrong the first time two batches
ran concurrently against a template edited in between.

**Rows are re-read from the stored source file** rather than duplicated into the database. The file *is*
the record of what the run was asked to produce, which is also what makes a finished run re-examinable
against its own input.

**Partial runs stay visible.** One row that cannot be issued is counted, logged and stepped over — one bad
row must not cost the other four hundred. But the batch only reaches `Completed` when every row produced a
certificate; otherwise it stays `Failed` with the failed row numbers in the organiser's own coordinates.
Reporting a partial run as complete is how nobody finds out four people never got theirs.

**Cancelling does not revoke.** Certificates already issued stay issued — they exist, they are signed, and
some may already have been sent. Revocation is a separate, deliberate act (Phase 9).

**Public surface:** `POST /v1/events/{eventId}/certificate-batches` (multipart), `GET` list, `GET` one,
`POST .../preview`, `POST .../approve`, `POST .../cancel`. There is deliberately no "generate now"
endpoint — the full run only ever starts from an approval, and only ever in the background
(`CertificateBatchJob`, `[DisableConcurrentExecution]`, 3 retries).

**Migration:** `AddCertificateBatchIdempotency` — two `CreateIndex`, zero destructive operations.

**Organiser UI:** `/host/events/{id}/certificates/generate` — design picker → upload + mapping → sample
review with an explicit warning → approve → live progress. A finished-but-incomplete run says so instead
of showing a full bar.

**Tests:** creation and row materialisation, required-field and blank-name refusal before anything renders,
cross-event template refusal, outsider 404, preview renders real rows without issuing, approval refused
without a preview, unapproved run generates nothing, version pinned at approval, one certificate per row,
batch certificates are real (id/PDF/PNG/signature/hash) and every one verifies, re-run issues nothing,
interrupted run finished by the retry (2 issued + 1 skipped), database refuses a duplicate, hand-issued
certificates unconstrained, partial failure, cancellation preserving issued certificates, listing scoped to
its event, and spreadsheet values reaching the certificate unchanged (`007` still `007`).

---

### Phase 8 — Distribution

**Status:** COMPLETE — 18 tests. Full suite 1994 passed / 1 skipped / 4 failed (the ClamAV four).

**Queue, then drain.** Nothing is sent inside the request that asks for it. A send loop over four hundred
recipients inside an HTTP call fails halfway with no record of how far it got, and the organiser's only
recourse is to press the button again and double-send to everyone already reached. Each intended send is a
`certificate_deliveries` row instead, drained by a minutely recurring `CertificateDeliveryJob` — so "who has
been sent what" is a query rather than a guess, and a crash or provider outage resumes without anyone
re-pressing anything.

**`Sent` never means delivered.** It records that the email provider *accepted* the message. There is no
bounce pipeline, so no state can honestly claim more, and the UI says "Sent" with an explicit note beneath
it. `ProviderMessageId` is kept so a future bounce can be correlated back.

**Pressing send twice does not send twice.** Queueing skips any certificate with a delivery that is not
`Failed`. A *deliberate* resend is a separate call that always creates a new attempt — that is exactly the
case where "already sent" must not block, because the first address was usually wrong.

**A revoked certificate is never delivered.** Checked at queue time and again at dispatch, because a
certificate can be revoked in between. Putting one in an inbox is worse than sending nothing: the platform
will publicly declare that document invalid.

**Transient versus permanent is distinguished.** A provider having a bad minute leaves the row `Pending`
for the next tick (3 attempts, then parked). A malformed address, a missing artefact, or a file too large
to email is refused immediately via `CertificateDeliveryRefusedException` — retrying cannot fix any of
them, and the error names the actual problem.

**Recipients with nowhere to send are counted, not dropped.** They produce no delivery row at all — a row
claiming to be pending forever would be a lie about work that will never happen — and appear as
`NoDestination` in the summary and on the face of the UI. Counts are per *certificate*, not per delivery
row, so a certificate that failed once and succeeded on a resend counts once as sent and the numbers add
up.

**A linked account is an availability, not a send.** The certificate simply appears for that user, recorded
as `Sent` immediately with nothing transmitted.

**The email carries the PDF as an attachment plus the verification link in the body** — the person who
needs to check a certificate is often not the person holding the file, and asking them to scan a QR out of
a forwarded document is worse than a link they can click.

**Public surface:** `POST /v1/certificate-batches/{id}/send` (202), `GET /v1/certificate-batches/{id}/deliveries`,
`POST /v1/certificates/{id}/send` (resend, optional corrected address).

**Test-infrastructure note:** `CapturingEmailSender` gained subject/attachment capture and a `FailNext`
switch for exercising retry and give-up behaviour. Delivery tests address each test's recipients to their
own domain, because the class shares one database and a dispatch drains every pending row in it.

**Tests:** queueing per addressable certificate, queueing sends nothing by itself, double-send prevention,
outsider 404, attachment content and type, verification link and certificate id in the body, provider
message id recorded, double-dispatch prevention, transient failure retried, permanent give-up after 3
attempts, revoked-before-dispatch skipped with a reason, resend to a corrected address, resend refused with
no address / malformed address / revoked certificate, per-certificate counting, nothing-to-send, and the
account channel producing no email.

---

### Bug — participant upload never sent the file (found 2026-08-15, Phase 6/7)

**Symptom:** "Reading your file…" hung on the mapping screen.

**Cause:** the shared axios instance in `web/lib/api.ts` sets a default `Content-Type: application/json`.
Handed a `FormData` with that header, axios does not warn or fail — it serialises the form to JSON. The
body actually put on the wire was `{"file":{}}`. The participant list was dropped in the browser-side
client and never reached the API at all, so the failure surfaced far from its cause and looked like a slow
or broken server.

Verified against a real HTTP server rather than reasoned about:

```
A) instance default Content-Type: application/json   → content-type: application/json
                                                       body: {"file":{}}
B) no instance Content-Type                          → content-type: multipart/form-data; boundary=…
                                                       body: --boundary…Content-Disposition…
```

**Fix:** `uploadHeaders()` in `web/lib/certificate-api.ts` sets `Content-Type: undefined` explicitly,
which lets axios generate the multipart boundary itself. Applied to both upload paths — the Phase 6
participant preview and the Phase 7 batch creation, which had the same defect.

**Regression test:** `web/test/certificate-upload-headers.test.ts`, 3 tests, pinned against a real
`node:http` server and forced to `@vitest-environment node` — the rest of the web suite runs in jsdom,
where axios picks the XHR adapter, and this bug is a property of the Node adapter the server action
actually uses. The third test asserts the broken behaviour explicitly, so the reason the header override
exists stays visible instead of becoming folklore.

**Also fixed:** the Docker images serving `localhost:3000` were built 2026-08-14 20:14 with a copy of the
repo baked in, predating both the ID-card studio removal and this entire module. Rebuilt; `/designs/…` now
404s and the certificate routes are live.

---

### Phase 9 — Revocation + Reissue

**Status:** COMPLETE — 19 tests. Full suite 2013 passed / 1 skipped / 4 failed (the ClamAV four).

**Nothing is ever edited in place.** A certificate is a signed claim someone already holds a copy of and
may already have shown to an employer. Editing its values would leave that copy disagreeing with the
platform, and would break a signature made over the old values — which is precisely the change the
signature exists to detect. A test snapshots the original's certificate id, field values, signature, key
id, document hash, both storage keys, issue time and template version, performs a correction, and asserts
all nine come out identical. Only `Status` moves.

**Revoke and reissue are different public claims, kept apart everywhere.** *Revoked* means do not honour
this. *Superseded* means a corrected one exists and here it is. Collapsing them would publicly mark
someone with a merely-misspelled name as having had their certificate withdrawn. They are separate service
methods, separate endpoints, and separate buttons with separate wording in the UI.

**A correction is a new certificate, not a copy with a field swapped** — its own id, its own signature over
its own values, its own rendered PDF and PNG, and it verifies on its own terms. Asserted.

**Fields not being corrected carry over unchanged.** Correcting one misspelled word must not quietly alter
fields nobody touched, so values are read from the original's own snapshot and only the named ones
overridden.

**Linked in both directions.** The replacement records `SupersedesCertificateId`; the original's
revocation row records `ReplacementCertificateId`. Either end can be followed to the other, which is what
the verification page needs whichever id someone happens to be holding.

**Gap found and closed in the Phase 5 read path:** a superseded certificate verified as `Superseded` but
did not say *what replaced it* — leaving the holder with "this is not current" and nowhere to go. It now
returns the replacement's public id and the stated correction reason.

**State transitions are refused, not silently absorbed.** Revoking twice → `already_revoked`. Reissuing a
revoked certificate → `certificate_revoked`. Reissuing an already-superseded one → `certificate_superseded`
(otherwise the chain forks and "which one is live" stops having one answer). All three are 409, because the
request was well formed — the certificate is simply not in a state where it can happen.

**A reason is mandatory** on every revocation and correction. The platform publishes it on the verification
page, and a public claim with no reason behind it is not one a holder or a verifier can act on.

**Bulk withdrawal** exists for "all two hundred have the wrong date", where revoking one at a time is not a
real option. Re-running it is safe: already-revoked and already-superseded certificates are counted as
skipped rather than failed.

**Lineage** walks back to the original and forward through every replacement, bounded at 50 links — a cycle
should be impossible, and an impossible thing that hangs a request is worse than one that stops. The chain
comes out identical from any point in it.

**Public surface:** `POST /v1/certificates/{id}/revoke`, `POST /v1/certificates/{id}/reissue` (201),
`POST /v1/certificate-batches/{id}/revoke`, `GET /v1/certificates/{id}/lineage`. There is deliberately no
edit endpoint.

**Organiser UI:** `CertificateCorrections` (separate Correct / Withdraw buttons, each stating its
consequence *before* the confirm, plus a history view) and `BatchWithdrawal`, kept at the bottom of a
finished run behind a link rather than beside the send button — permanent and publicly visible actions
should take a deliberate reach.

**Tests:** revocation with reason recorded, reason required, double-revoke refused, outsider blocked (and
verified not to have taken effect), revoked verifies as revoked with its reason, reissue supersedes rather
than revokes, the nine-field immutability snapshot, the replacement being a real certificate that verifies,
carry-over of untouched fields, correcting a non-name field, superseded verification naming its
replacement, both invalid-state refusals, a three-link chain walkable from any point, a chain of one, bulk
withdrawal, bulk re-run skipping, and bulk reason required.

---

### Phase 10 — Participant Access

**Status:** COMPLETE — 21 tests. Full suite 2034 passed / 1 skipped / 4 failed (the ClamAV four).

**Two routes, because participants are not all account holders.** Someone with a Kurx account finds their
certificates in their account. Someone who was on a spreadsheet and nothing more gets a **capability
link** — a long-lived, revocable URL that *is* the credential. Requiring an account to collect a
certificate would exclude most of the people certificates are issued to, which is the confirmed decision
this implements.

**The token is never stored.** Only its SHA-256 hash, so a dump of `certificate_access_links` cannot be
turned back into working links — the same reason a password is not stored. 256 bits from a cryptographic
RNG. The raw value appears in exactly one response and the platform then genuinely cannot reproduce it,
which is why the UI offers "regenerate" and not "reveal". Both properties are asserted.

**The link belongs to the recipient, not to a certificate.** That is what makes it durable: after a
correction the same link keeps working and shows the corrected certificate. A link per certificate would go
stale on the first reissue — exactly when the holder most needs it.

**No expiry, deliberately.** A certificate is a permanent claim about something that happened, and a link
that quietly stops working in a year fails the person who kept it exactly as they were told to. Revocation
is the control instead: deliberate and attributable.

**A revoked link and a token that never existed give the identical answer.** Distinguishing them would
confirm to whoever is guessing that they had found a real one. Asserted by comparing the two errors.

**Linking is by verified email only.** `User.EmailVerifiedAt` must be set. Matching on an unverified
address would let anyone type a stranger's email into their profile and collect that stranger's
certificates — there is a test for exactly that attack. Matching is on the normalised address, so
`Rahul@Example.com` on the spreadsheet and `rahul@example.com` on the account are one person. Linking is
idempotent and runs on every `ListMine`, so a certificate issued before someone signed up appears the first
time they look rather than after a backfill they cannot ask for. A recipient already claimed is never
re-pointed.

**A certificate that is no longer live is still shown, but offers no download.** Hiding it would leave the
holder unable to find out what happened to a document they may already have sent to an employer. Handing
back a fresh copy of one the platform publicly calls invalid would arm a misunderstanding. The verification
link is still given either way.

**Test correction worth recording:** the "shared family address" scenario for re-pointing is impossible —
the database has a unique index on lowercased `users.Email`, so two accounts cannot hold one address. The
test was rewritten around the reachable path: someone links, then changes their email, freeing the address
for another account to verify. The certificate stays with the original holder.

**Migration:** `AddCertificateAccessLinks` — one `CreateTable` plus indexes, zero destructive operations in
`Up()`.

**Public surface:** `GET /v1/certificate-access/{token}` (anonymous, `verify` rate limit),
`GET /v1/me/certificates`, `POST /v1/certificate-recipients/{id}/access-link` (201, token shown once),
`GET /v1/certificate-recipients/{id}/access-links` (never includes tokens),
`DELETE /v1/certificate-access-links/{id}`. `ICertificateVerificationLinks` gained `AccessUrl` so the
emailed link and the dashboard link cannot drift apart.

**UI:** `/certificates/{token}` (public, `force-dynamic`, `robots: noindex` — the URL is a credential and
an indexed copy would publish it) and `/my-certificates` (signed in, nav entry added). Both render the same
`ParticipantCertificateList`. Also removed a dead `IdCard` import left in `nav-config.ts` by the ID-card
studio removal.

**Tests:** link resolution, raw token absent from the database, a minted link never shown again, two links
both working, unknown/blank/garbage tokens, revoked indistinguishable from never-existed, revocation
leaving the certificate alone, double-revoke tolerated, last-accessed recorded, outsider blocked on all
three organiser operations, withdrawn certificate shown without a download, the same link showing a
replacement after correction, the unverified-email attack, verified-email linking, case and whitespace
normalisation, idempotent re-linking, no reassignment of a claimed recipient, pre-signup certificates
appearing on first look, and an empty list for someone with none.

---

### Phase 11 — Dashboard + Telemetry + Export

**Status:** COMPLETE — 23 tests. Full suite 2057 passed / 1 skipped / 4 failed (the ClamAV four).

**Telemetry counts events, not people.** A `certificate_events` row carries a certificate, a type, a time
and the request's correlation id — no user, no address, no device, no IP. An organiser needs to know their
certificates are being checked; they do not need to know who is checking a particular person's credential.
Storing that would turn a verification log into a record of where the holder has been applying for jobs.
A test asserts the entity has no field that could hold a person, so a later addition has to argue with a
test rather than slip in.

**Only what the platform can actually observe is recorded.** Files are fetched directly from object
storage by the browser (D-302), so a completed download is never seen here and no download counter is
offered. `Shared` has no mechanism behind it at all and is never written. An always-zero counter that
looks like a feature is worse than an absent one — `RecordAsync` silently drops anything that is not
`Verified` or `Viewed`, and there is a test for each.

**A verification miss records nothing**, or the counter would measure guessing rather than use.

**Recording never throws and never blocks.** Verification and participant access must not fail because a
counter could not be written — the observation is worth less than the thing being observed.

**Two surfaces, two permissions.** The dashboard is counts and needs `ViewAnalytics`. The export carries
participant names and email addresses — it *is* the personal data, not a summary of it — and needs
`ManageContent`.

**`Live` is the headline, not `Total`.** Counting withdrawn and replaced certificates alongside good ones
would tell an organiser they have three hundred valid credentials out there when forty are void. Delivery
is counted per certificate taking its latest attempt, so a failed-then-resent certificate counts once and
the totals add up.

**The daily series has no holes.** Every day in the 30-day window is present including the empty ones — a
sparse series renders as a chart with gaps that read as missing data rather than as quiet days.

**CSV injection is neutralised.** Participant names come from a file a stranger uploaded, and a cell
beginning `=`, `+`, `-`, `@`, tab or CR executes as a formula when the export is reopened in Excel — the
classic path to `=HYPERLINK` phishing. Each is prefixed with a single quote: the value still reads
correctly and the spreadsheet treats it as text. This is the mirror of the Phase 6 reader, which never
*evaluates* a formula; neither half of the module lets an uploaded string become executable. Ordinary
names containing commas and quotes still round-trip correctly per RFC 4180.

**Bug found by its own test:** the export emitted no BOM. `new UTF8Encoding(true)` only decides what
`GetPreamble()` returns and has no effect on `GetBytes()`, so constructing it that way and expecting a BOM
silently produced a file without one — and an Indian participant list would have opened as mojibake in
Excel, the one application most organisers will use. Fixed by prepending the preamble explicitly.

**Public surface:** `GET /v1/events/{id}/certificate-dashboard`,
`GET /v1/events/{id}/certificates/export?batchId=` (CSV file response, `heavy` rate limit). A batch id
belonging to another event is refused as not-found rather than silently ignored.

**UI:** `CertificateDashboardPanel` on the event's certificates page, shown only once something has been
issued — an empty grid of zeroes on a fresh event is noise in front of the thing the organiser came to do.
Export goes through a new Next route handler (`/api/events/[id]/certificates/export`) for the same reason
`/api/ticket-qr` exists: a plain `<a download>` cannot carry an `Authorization` header and the token is in
an httpOnly cookie. Authorization is not re-implemented there — the backend's answer, 403 included, is
passed through verbatim, and the response is `private, no-store`.

**Tests:** verification counted, the privacy assertion on the event row, misses counted as nothing,
Downloaded/Shared/garbage all dropped, recording against an unknown certificate not throwing, capability
link counted as a view, live/withdrawn/replaced counted separately, delivery and addressability reported,
the gapless daily series, outsider blocked on both surfaces, export contents and headers, four
formula-injection payloads, comma-and-quote escaping, BOM and Unicode, narrowing to one run, refusing
another event's run, an empty export, and a withdrawn certificate appearing with its reason.

---

### Phase 12 — OCR (boundary and stub only)

**Status:** COMPLETE — 24 backend + 8 web tests. Backend 2081 passed / 1 skipped / 4 failed (the ClamAV
four). Web 632 passed / 1 skipped. **No OCR engine was integrated**, by instruction.

**`ITextDetector` is an extension point, not a feature.** OCR is deferred by decision and the editor is
required to work without it — every field is placed by hand today and that path is untouched. The
interface exists so that adding an engine later is a registration change rather than a rewrite.

**The contract**, deliberately vendor-neutral — nothing in it mentions a page, an engine, or any
particular confidence scale:

- `bool IsAvailable` — capability is a question you can ask *without* submitting an image, because an
  editor has to decide whether to offer a control before it has anything to detect.
- `Task<TextDetectionResult> DetectAsync(byte[] image, string contentType, ct)`
- `TextDetectionResult(bool Available, IReadOnlyList<DetectedTextRegion> Regions, string? Reason)`, with
  `Unavailable(reason)` and `Detected(regions)` constructors.
- `DetectedTextRegion(Text, X, Y, Width, Height, Confidence)` — coordinates are **percentages of the
  page, 0–100**, identical to `CertificateFieldInput`, so a detected region drops straight onto the canvas
  without the editor learning anything about image dimensions. Confidence is normalised 0–1 because every
  engine scales it differently.

**The load-bearing distinction: *nothing looked* versus *nothing found*.** Both have an empty region list,
so the list alone can never be the signal. `Available: false` is a fact about the deployment;
`Available: true` with no regions is a fact about the design. A stub returning an empty success would
collapse them and quietly tell a creator their certificate has no text on it.

**`UnavailableTextDetector`** is the only implementation. It reports `IsAvailable = false`, returns
`Unavailable` with a human-readable reason, never reads the image, never throws — including on empty
bytes, junk bytes and a blank content type — and **never fabricates a region**. Marked as a stub in its
own doc comment, per the project rule that anything temporary says so.

**Registered through `AddProvider`**, the helper that exists to *refuse* every value but the development
one. `TEXT_DETECTOR=tesseract` therefore fails loudly at boot rather than silently resolving to a detector
that detects nothing. Documented in `.env.example`.

**Editor integration.** `ICertificateTemplateService.DetectBackgroundTextAsync` →
`POST /v1/certificate-templates/{id}/detect-text`. It answers **200 with `available: false`**, not an
error status: no engine configured is a fact about the deployment, not a failed request, and an editor
renders a capability from it rather than handling an exception. Capability is checked *before* the artwork
is fetched, so nothing is pulled out of storage to prove there is nothing to analyse. Storage failures and
any future engine throwing are both contained into `Unavailable` — an assist that can break saving a
design is worse than no assist.

**Nothing consults it.** Save, preview, approve, issue and verify never call the detector. Tests walk the
whole chain — manual field placement, preview, issuance, verification, and a full batch through the
approval gate — with no engine registered.

**Client side.** `lib/certificate-detection.ts` holds the schema as a pure module with no dependency on
`lib/api.ts`, mirroring `lib/certificate-editor.ts`, so the contract is testable without booting the API
client. The editor probes once on mount via an action that swallows every failure into
`DETECTION_UNAVAILABLE`; the assisted control renders only when `available` is true, which is never today,
so the editor is visually and behaviourally identical to before.

**No engine was added**, and two tests enforce it: one asserts no OCR assembly is loaded, the other scans
every `.csproj` in the solution for eight vendor package names. The second asserts it found at least four
projects first — a "nothing found" test whose real failure mode is finding nothing because it looked
nowhere.

**Tests (24 backend):** detector resolvable by interface; capability askable without an image; stub
reports unavailable with a reason and no regions; unavailable distinguished from empty success; no region
invented across three content types; no throw on empty/junk input; editor gets a successful unavailable
answer rather than a failure; a design with no artwork answers unavailable rather than failing; outsider
refused (404, D-018); manual field placement unaffected; placement after a failed detection identical;
preview + issuance + verification unaffected; a full batch previewed, approved and generated; no OCR
assembly loaded (8 vendors); no OCR package in any project file.

**Tests (8 web):** the unavailable shape parses; `reason: null` accepted; regions carried in the editor's
coordinate space; nothing-looked distinguished from nothing-found; malformed answers rejected rather than
passed to the canvas; no assistance offered without an engine; assistance offered once one exists; a
failed probe treated as simply unavailable.

---

### Phase 13 — Template Reuse

**Status:** COMPLETE — 19 tests. Full suite 2100 passed / 1 skipped / 4 failed (the ClamAV four).

Phase 3 already shipped library templates as a *concept*: `CertificateTemplate.EventId` is nullable,
ownership rather than event authority governs them, `/v1/me/certificate-templates` lists them, and
`LibraryTemplateBackground` keys exist. What was missing was the reuse mechanic itself — a way to get a
design into the library and back out onto an event. That is this phase, and nothing else.

**A copy, never a reference.** Two designs sharing artwork could not be independently archived, would put
an object under `events/{id}/…` inside a personal library — breaking the prefix-ownership convention — and,
worst, would let an edit made months later in someone's library silently change what an event's
already-issued certificates claim to look like. The artwork bytes are duplicated into the destination's own
prefix. That costs one image copy at the moment somebody deliberately asks for a copy of a design.

**Two verbs, not one with a target.** `CopyToLibraryAsync` and `CopyToEventAsync` are separate because
"save this for later" and "use this here" are different intentions, and only the second needs an
entitlement on a destination event. Those are two separate checks: being allowed to *read* a design says
nothing about being allowed to add content to the destination, and a test covers a creator trying to copy
their own design onto someone else's event (`forbidden`).

**The copy starts at version 1, draft.** Version records what a given certificate was rendered from;
inheriting the source's would attach that history to a design that has issued nothing. Tested against a
source deliberately pushed past version 1.

**Copied property by property, not by counting fields.** The subtle failure of a copy is *incompleteness*
— an attribute nobody carried over produces a design that looks right in a list and wrong on the page. The
test compares nineteen field properties per field. It earned its keep immediately: the first
implementation missed `IsMasking` and `BackgroundColor` (and referenced an `ImageStorageKey` that does not
exist), caught at compile time and by that assertion.

**Independence is asserted in both directions** — editing the copy leaves the original alone, editing the
original leaves the copy alone, and archiving the original leaves the copy usable and still copyable onto
a new event. End to end, a reused design issues certificates on the new event that verify.

**A design whose artwork has gone is refused** (`artwork_unavailable`) rather than copied into a template
that would render as a blank page, discovered at generation time. A design with *no artwork yet* copies
fine — that is a work in progress, not a broken one.

**Platform admins still cannot reach a creator's library**, consistent with Phase 3: a private design
library is not event content, and `isAdmin: true` gets `not_found`.

**Public surface:** `POST /v1/certificate-templates/{id}/copy-to-library` and
`POST /v1/certificate-templates/{id}/copy-to-event/{eventId}`, both 201.

**UI:** `SaveToLibrary` below the editor (keeping a design is something you do when finished, not while
placing fields) and `UseSavedDesign` beside "create a design" on the list — the same decision, so the same
place. It renders nothing when the library is empty; a picker with an empty list is a dead end that has to
be explained, while its absence explains itself. Both are described as *copying*, never linking or
sharing, because wording that implied one shared design would set up exactly the surprise the copy
prevents.

**Tests:** save to library, name defaulting, library listing, use on another event, version/status reset,
the nineteen-property layout comparison, artwork duplicated under the copy's own prefix with identical
bytes and both objects present, event-prefix placement, independence both ways, archiving independence,
end-to-end reuse issuing a verifiable certificate, outsider refused on both verbs, copying onto an event
the caller does not run, admin refused on a library design, blank-name refusal, missing-artwork refusal,
and copying a design with no artwork yet.

---

### Full stack brought up (2026-08-15)

`docker compose build` + `up -d` across all six services. All healthy and smoke-tested: backend `/health`
200, web 200, admin 307 (its login redirect), Postgres `pg_isready` accepting, Redis `PONG`, clamd
detecting EICAR.

**The four ClamAV failures were never environmental in a vague sense — the image has no build for this
CPU.** `clamav/clamav` publishes **amd64 only**, on every tag including `latest`, and this is an arm64
machine, so `up` aborted with *"no matching manifest for linux/arm64/v8"* and clamd simply could not run.
Pinning `platform: linux/amd64` on the service runs it under emulation; it becomes healthy and scans
correctly. A one-line change with a comment explaining it, and a no-op on an amd64 host.

**With clamd up, the backend suite is green end to end: 2104 passed, 1 skipped, 0 failed.** That closes
the long-standing four and, per CLAUDE.md, restores "green is the standard".

**A mistake worth recording:** the first run with clamd up reported *42* failures. That was self-inflicted
— exporting `FILE_SCANNER=clamav` for the whole suite routed every upload in every test through emulated
clamd, breaking 38 unrelated tests (representation-request document uploads and similar). The scanner
tests set `FILE_SCANNER` on their own factory already (`UploadScanCoverageTests.cs:34`); only
`CLAMAV_HOST`/`CLAMAV_PORT` need to point at the container. Re-run correctly: zero failures.

**Two things found and reported rather than changed:**

- `kurx-minio` is an **orphan container**. It carries this compose project's labels and names service
  `minio`, but `docker-compose.yml` defines no such service, so `up` never manages it. Nothing depends on
  it today — the backend runs `STORAGE_PROVIDER=localdisk`. It is left running and untouched.
- A capability link with an unknown token **renders the not-found content correctly but returns HTTP
  200**, because the page streams: the public layout's shell flushes before `notFound()` resolves. The
  reader sees the right thing; only the status line is imprecise. Fixing it means restructuring a shared
  public layout, which is out of scope here.

---

### MinIO added to compose; S3 storage active (2026-08-15)

The certificate module's confirmed storage target is S3-compatible object storage, and until now compose
ran `STORAGE_PROVIDER=localdisk` with an orphaned MinIO container nothing referenced. Both are fixed:
MinIO is a real compose service and the backend runs on S3 by default.

**The constraint that shaped the setup.** `S3StorageOptions` refuses a non-https `S3_PUBLIC_ENDPOINT`,
because the AWS SDK emits presigned URLs as `https://` unconditionally (verified in Phase 2 — `UseHttp` is
documented as not applying when an explicit `ServiceURL` is set, and setting it either side of `ServiceURL`
changed nothing). So an `http://` public endpoint would produce URLs that lie about their own scheme. That
guard is load-bearing and was **not** relaxed for local development.

**Three services instead of one**, because of that:

- `minio` — plain HTTP on the private network, port 9000 deliberately **not** published. Console on
  127.0.0.1:9001.
- `minio-init` — creates the bucket and exits. `backend` depends on it with
  `condition: service_completed_successfully`, which is the difference between "storage is up" and
  "storage is usable"; without it the first upload fails against a bucket that does not exist.
- `minio-tls` — Caddy terminating TLS on published port 9000 with its own local CA, forwarding to MinIO
  over HTTP. **The Host header is preserved deliberately**: SigV4 signs the host, so a URL signed for
  `localhost:9000` only verifies if MinIO sees that same host. Rewriting it would break every presigned
  URL.

**The split endpoint, exactly as Phase 2 designed it:**

    S3_ENDPOINT        = http://minio:9000      # server-side; private network, no cert to distrust
    S3_PUBLIC_ENDPOINT = https://localhost:9000 # browser-side; URLs are SIGNED for this host

Confirmed from the backend's own startup summary:

    IStorage -> s3 (kurx-certificates @ http://minio:9000,
                    browser URLs signed for https://localhost:9000)

**Verified end to end, not assumed.** A presigned GET signed for `localhost:9000` was fetched through
Caddy and returned 200 with the correct body — MinIO validated a signature computed over a host it only
sees because the proxy preserves it. Bucket creation over the server-side endpoint is proven by
`minio-init` succeeding. Both halves of the split endpoint therefore work.

**Known friction:** Caddy's local CA is not in any trust store, so the first certificate download in a
browser shows a warning. Accept it once for `https://localhost:9000`; the CA persists in the
`kurx_caddydata` volume, so it does not recur. Documented in `.env.example`. The alternative — allowing an
http public endpoint outside Production — was rejected: it would not work anyway, since the SDK would
still emit `https://` URLs.

**Volumes added:** `kurx_miniodata` (objects) and `kurx_caddydata` (the local CA, persisted so the
certificate the browser is asked to trust stays the same across restarts).

---

### Localdisk migrated into MinIO (2026-08-15)

All 29 objects in the `kurx_storage` volume copied into `s3://kurx-certificates` under **identical keys**.
`LocalDiskStorage.PathFor` maps a key straight onto `root + key`, so a file's path relative to
`LOCALDISK_ROOT` *is* its storage key — no translation was needed, and every database row that references
a key still resolves.

**Copied, not moved.** The localdisk volume is untouched, so the previous state is recoverable by flipping
`STORAGE_PROVIDER` back.

**Content types were chosen, not guessed, and that is the interesting part.** The localdisk receiver serves
*everything* as `application/octet-stream`, with a comment naming why: *"Content sniffing an
attacker-supplied file into text/html is stored XSS."* S3 honours a stored Content-Type, so sniffing types
during migration would have quietly undone that defence. The rule applied instead:

- Platform-generated or upload-allowlisted files get their true type — 4 `.pdf` (our renderer), 1 `.png`
  and 1 `.jpg` (template artwork, which passes the `image/png|jpeg|webp` allowlist on upload).
- The other **23 extension-less objects** — avatars, event media, an authorization document, design-studio
  leftovers — stay `application/octet-stream`, exactly what they were served as before.

**Verified rather than assumed.** Object count 29/29; every object's ETag compared against the local file's
MD5 — 0 missing, 0 differing, 0 unexpected. Then fetched through the real path: a presigned GET of the live
template background returned 200, `image/png`, 576,202 bytes, decoding as a valid 830x654 PNG; an avatar
returned 200 as `application/octet-stream`, confirming the opacity rule survived.

**What is actually in there**, worth knowing before anyone treats the bucket as clean:

| Count | Prefix | Note |
|---|---|---|
| 2 | `certificates/templates/.../background.{png,jpg}` | live Phase 3 artwork |
| 4 | `certificates/{guid}.pdf` | **old-shape** keys from the pre-removal certificate feature, not the Phase 4 `certificates/issued/{id}/certificate.pdf` layout |
| 13 | `designs/...` | orphans from the removed ID-card design studio |
| 10 | `media/...`, `avatar/...`, `authorization/...` | live ticketing/profile data |

The 17 old-shape and orphan objects were migrated rather than dropped: deciding what is garbage is not a
call to make silently during a data move. They can be removed later with a targeted prefix delete.

**Naming observation:** the bucket is `kurx-certificates`, but `IStorage` is one boundary for *all*
storage, so it now also holds avatars, event media and an authorization document. `kurx-storage` would be
the honest name. Left as-is rather than renamed unilaterally — it is a one-line default plus a re-run of
`minio-init` whenever it is wanted.

---

### Upload failure after the S3 switch — two causes (2026-08-15)

Reported as *"Upload failed. Your existing design is unchanged"* (the Phase 3 `BackgroundUpload` message).
Two separate problems, one of them mine.

**1. The compose file had been partially reverted.** `minio`, `minio-init`, `minio-tls`, the backend's
S3 environment and both new volume declarations were gone from `docker-compose.yml`; the clamav
`platform: linux/amd64` fix in the same file survived. The containers were absent — not stopped, removed —
while the running backend still pointed at `http://minio:9000`. Storage was simply not there, so every
upload failed. Re-applied; the `kurx_miniodata` volume was untouched and all **29 migrated objects were
still present**. Worth watching: something outside this session rewrote that file, and it can happen again.

**2. Browser uploads over local TLS cannot work without trusting Caddy's CA — and I understated this.**
The earlier note said "the first download shows a warning you can accept". That is true for a top-level
navigation and false for an upload: a presigned PUT is an **XHR, which has no click-through**. It fails
outright. Measured against the restored proxy:

    no CA trusted     -> 000 (connection refused at TLS)
    trusting Caddy CA -> 200
    leaf SANs         -> DNS:localhost

So the fix is real and one command, but it needs the user's own privileges:

    sudo security add-trusted-cert -d -r trustRoot \
      -k /Library/Keychains/System.keychain .caddy-local-ca.crt

Covers Chrome and Safari (system keychain); Firefox keeps its own store. Documented in `.env.example`
alongside the alternative — `STORAGE_PROVIDER=localdisk` in `.env`, which sidesteps storage TLS entirely
at the cost of not exercising the S3 path locally.

**A correction to an earlier claim in this file:** I reported "trusting Caddy's CA still fails" while
diagnosing. That test was invalid — `minio-tls` was already gone at that point, so *everything* failed.
Re-run against the restored proxy, trusting the CA works.

---

### Reverted to localdisk; MinIO kept as an opt-in profile (2026-08-15)

Local storage is `localdisk` again, on request. The reasoning is sound: browser-direct uploads to a local
MinIO require TLS (presigned URLs are always https), and an untrusted certificate fails an XHR outright
with no prompt — so an S3 dev stack costs a CA install before anyone can upload a file.

**Nothing was lost in either direction.** Before switching, both stores were diffed: localdisk held 30
files, MinIO 29, and the difference was one template background written while the backend was briefly on
localdisk. **localdisk is a strict superset** — no object existed only in MinIO. Its contents were checked
too: valid PNG header, 191 bytes, a genuinely small image rather than a truncated or error-body upload.

**MinIO is now behind the `s3` compose profile** rather than deleted. A default `up` starts six services
and no object storage; `docker compose --profile s3 up -d` brings back the three storage services. The
backend's `depends_on: minio-init` was removed, since a profiled service cannot be an unconditional
dependency. The migrated objects stay in the `kurx_miniodata` volume, so flipping back does not repeat the
migration.

`.env.example` documents the four steps to switch, including that trusting the CA is **not optional** for
uploads. The extracted CA was removed from the repo root — it is one `docker cp` away whenever it is
wanted.

Confirmed after the switch: `IStorage -> localdisk (LocalDiskStorage — not durable)`, and the storage
receiver still enforces its signature (an unsigned PUT answers 403).

---

### Bug — "Reading your file…" never finished (found 2026-08-15, Phase 6/7)

Reported from the Generate screen. The request **never reached the backend** — nothing in the API log, and
the only nearby entry in the web log was an unrelated 500 from a Delete-draft click. So the failure was
entirely in the browser to Next hop, and two of my own defects combined to make it undiagnosable.

**1. The spinner could outlive the attempt.** Every action handler in this module was written as

    setBusy(true);
    const result = await someAction(...);
    setBusy(false);

A *rejection* skips the clear, so the UI sits on "Reading your file…" forever with nothing to act on —
which is worse than the upload failing, because it gives the person no way to tell a broken file from a
broken app. Five handlers had the same shape: the participant preview, batch create, approve, cancel, send,
and the revoke/reissue submit. All now use `try/catch/finally`, with `finally` clearing the pending state
unconditionally and `catch` surfacing a sentence that says what did *not* happen ("Nothing has been
generated", "The certificate is unchanged").

**2. Three size limits that disagreed.** The client refuses over 10MB, the API endpoint refuses over 10MB —
but participant lists are uploaded through a **Next Server Action**, whose default `bodySizeLimit` is
**1MB**, and no `serverActions` config existed. A large-but-legal spreadsheet was therefore rejected at a
boundary nobody had configured, before any of our own validation ran, and the rejection surfaced as the
hang above. `next.config.mjs` now sets `bodySizeLimit: "12mb"` — 10MB plus multipart overhead — so the
three numbers agree.

The first defect is the one that mattered: with it fixed, this class of failure now reports itself instead
of hanging, whatever the cause.

---

### Root cause of the upload failures: a stale service worker (2026-08-15)

After the fail-safe landed, the same upload reported *"That file could not be read"* — my generic catch —
with **nothing logged on either server**. Not in the API log, not in the Next log. A failure that reaches
neither server is not a failure in either one.

**`next-pwa` is active in the container.** It is disabled only when `NODE_ENV === "development"`, and the
image runs `next start` in production, so a service worker is registered and **precaches the app shell's JS
chunks by build id**. Roughly four `docker compose build web` runs today each minted a new build id.

**Server Action ids are per-build.** A tab loaded before a rebuild holds the old bundle, calls an action id
the running server no longer has, and the call fails in the browser before any application code executes —
which is exactly why neither log had anything to say.

`skipWaiting: true` was set but `clientsClaim` was not, so a new worker activated without taking over
already-open tabs. Both `clientsClaim: true` and `cleanupOutdatedCaches: true` are now set, so a future
deploy claims open tabs immediately and superseded precaches are dropped rather than accumulating one per
deploy.

**This does not retroactively fix the currently-open tab** — the stale worker has to be replaced once, by
hand: DevTools → Application → Storage → *Clear site data*, then reload.

**Server side was verified sound during the diagnosis**, not assumed: an OTP session was obtained through
the real auth flow and the CSV posted to
`POST /v1/events/{id}/certificate-participants/preview` as multipart. It answered **403** — the test
account does not run that event — and a 403 rather than a 400 proves multipart binding succeeded and
authorization was reached. The read path itself is covered by the 47 reader and 11 endpoint tests.

---

### File uploads moved off Server Actions onto route handlers (2026-08-15)

The participant upload kept failing with **nothing in either server log** — not the API's, not Next's.
Next in production only prints errors, so silence there means the action never ran. A failure that reaches
no application code cannot report itself, which is why three rounds of diagnosis produced no evidence.

Rather than keep guessing at an invisible boundary, both file-carrying calls were moved to **route
handlers**, which is the shape this repo already uses for files (`/api/ticket-qr`,
`/api/events/[id]/certificates/export`):

    POST /api/events/{id}/certificate-participants/preview
    POST /api/events/{id}/certificate-batches

A Server Action added three failure modes that a plain multipart POST does not have: a `File` argument
must survive the action's own serialisation; action ids are per-build, so a tab open across a deploy calls
an id the server no longer has; and the action body limit is separate from the one the endpoint enforces.
None of those exist for a route handler.

The decisive advantage is diagnostic: **a route can be exercised with curl.** Verified immediately after
deploying — an unauthenticated multipart POST returns `401 {"error":"unauthenticated"}`, proving the
handler runs, parses multipart and reads the session. A Server Action could never be checked that way,
which is precisely why the earlier failures were opaque.

`lib/certificate-uploads.ts` holds the client half. Every function returns a discriminated result instead
of throwing, so a caller cannot leave a spinner running by forgetting to catch, and unrecognised failures
now surface their HTTP status and error code rather than a generic sentence — an error that names itself
is one somebody can act on.

---

### Reported "doubled text" on generated certificates — traced, and not a renderer bug (2026-08-15)

Reported as the resolved value rendering *in addition to* the template's placeholder:
`[Recipient's Full Name] John Doe`. The whole pipeline was traced before changing anything.

**The pipeline is 1:1 at every step.** `BuildDocumentAsync` maps template fields to render elements with a
plain `fields.Select(f => new CertificateRenderElement(...))` — one field, one element. `DrawElement`
switches once on kind and draws exactly one thing: a `dynamicfield` draws its resolved value, or `{key}`
when there is no value; a `text` element draws its `StaticText`. Nothing anywhere draws a label beside a
value or emits an element twice.

**The live template confirms it.** Template `6079ab24` carries four fields — one image and one each of
`participant_name`, `event_name`, `event_date`. No duplicate keys. (An older *archived* template on the
same event does contain three `participant_name` fields at nearly identical coordinates, which would
genuinely double text — but it is not the one being rendered.)

**The placeholder is printed into the uploaded artwork.** The design is a JPEG exported from a template
that has `[Recipient's Full Name]`, `Date of Issuance: 01/02/2025` and a company name baked into the
image. The renderer draws the participant's value once, on top of pixels it cannot edit. Reproduced by
rendering the real artwork with the real field coordinates: "John Doe" appears exactly once, overlapping
the printed placeholder.

**So there is no root cause available in the rendering layer** — you cannot remove ink from a raster the
customer supplied. The two honest resolutions are (a) delete the placeholder text from the design before
exporting it, leaving clear space where the field sits, or (b) use the masking already in the model —
`CertificateTemplateField.IsMasking` / `BackgroundColor`, painted by `DrawElement` before content — which
is a cover-and-replace, explicitly rejected in the request.

**What was done instead:**

*Regression tests* — `CertificateFieldSubstitutionTests`, 23 cases, pinning the half that is ours. The
load-bearing one renders a resolved dynamic field and an equivalent *static* text element and asserts the
PNGs are **byte-identical**: impossible if a placeholder were drawn as well as a value. Repeated across
every resolved key including a custom one, plus static text unaffected by participant data, static text
never substituted even when it looks like a key, determinism, value-driven difference, long names and long
event names, empty optional fields drawing a marker and nothing more, and the PDF path substituting like
the image path.

*Mapping vocabulary* — training-style exports now resolve without hand-mapping:
`course`/`coursename`/`program`/`programme`/`training` → `event_name`, `completiondate` → `event_date`,
and `issuedate`/`issuedon`/`dateofissue` → `issue_date` (deliberately distinct from the event's date: a
course can finish in March and be certified in April). The supplied demo file's five columns now all map
to distinct fields, asserted end to end.

---

### Editor warning when a field is placed over the design's own artwork (2026-08-15)

The follow-up to the "doubled text" diagnosis. The renderer cannot remove ink from a raster the customer
supplied, so the only place to catch `[Recipient's Full Name] John Doe` is while the layout is still being
decided — before anything is generated.

**A coarse ink map, computed server-side.** `GET /v1/certificate-templates/{id}/artwork-map` returns a
40x28 grid over the artwork, one character per cell, `1` where something is printed. Two reasons it is not
done in the browser: reading the artwork's pixels requires a canvas, and the canvas is tainted whenever
storage is a different origin — which it is by default; and a request per mouse move would be unusable.
The editor fetches the map once per design and does the overlap arithmetic locally as a field is dragged.

**The background is measured, not assumed.** It is taken as the modal colour of the image, quantised to 32
levels per channel. Assuming white would mark every cream, navy or dark certificate as printed edge to
edge. A pixel counts as ink at a distance of 60 from that colour, and a cell counts as occupied at 6% ink
— deliberately generous, because certificate stock carries watermarks and paper texture and flagging those
would fire the warning on every design.

**Tuned against the real artwork, not guessed.** Rendered as text, the map of the reported template reads
back as the certificate itself: the border, the title band, the `[Recipient's Full Name]` block, the
paragraph, the footer and the CPD badge each land where they are on the page. The `participant_name` field
at (13.8, 30.3) overlaps that block, so the warning fires exactly where the bug was reported.

**Threshold is 18% of the field's footprint**, well above zero on purpose. A field often clips a border or
a rule by a cell or two, and a warning that fires on every design is one people learn to dismiss — at
which point it is worse than no warning, because the one time it matters looks like all the times it did
not.

**An unreadable or absent map warns about nothing.** `Analysed: false` is carried explicitly rather than
returning an empty grid, so "we could not look" is never rendered as "your design is clear here".

**Tests:** 9 backend (content located where it is, a field over text detected, a field on clear space not,
plain pages of white/cream/navy entirely clear, faint watermark ignored, light print on a dark page found,
map shape) and 10 web (coverage high/zero/partial, unanalysed maps reporting nothing, zero-sized boxes,
grazing below threshold, null map). Web suite 642 passed / 1 skipped.

---

### Bug — every new field landed on the same spot (found 2026-08-15, Phase 3)

A generated certificate showed illegible overlapping text below the programme line. The cause was in the
editor, not the renderer: `newField` returned a **fixed** `x: 15, y: 45` for every text and dynamic field,
so three fields added in a row occupied one position exactly. The reported template proved it —
`event_date`, `certificate_id` and `organizer_name` all stored at `(15.0, 45.0)`.

On the canvas that reads as a single field that will not move, because the top one covers the others. On
the certificate it renders as three strings drawn over each other, two of them unresolved placeholders.

**Fixed with `nextFreeSlot`**, which walks down the page from the default and returns the first slot that
collides with nothing, then a second column nudged right so a design with many fields does not march off
the bottom. It also takes an optional `isBusy` predicate, which the editor backs with the artwork map — so
a new field avoids the design's own printed title as well as the other fields, rather than starting life
somewhere it has to be dragged out of.

Everywhere-taken falls back to a cascade off the default rather than the default itself: the point is that
a new field is always visibly its own box.

**Tests:** 6 added — default position on an empty design, moving clear of an occupied slot, three
consecutive adds getting three distinct slots (the reported case), never placing a field off the page,
avoiding caller-reported busy regions, and still returning something usable when everywhere is busy. Web
suite 648 passed / 1 skipped.

**Not done:** the three already-stacked fields on the reported template were left where they are. Moving a
creator's saved layout without being asked is not a migration to run silently — they are now visible and
draggable, and the editor will not create more.

---

### Cover-and-replace for placeholders printed into the artwork (2026-08-15)

Requested twice as "replace the existing text element instead of adding one on top". There is no text
element: the placeholder is pixels inside the uploaded JPEG, and nothing can edit pixels. What *is*
achievable — one value, in the placeholder's position, with the placeholder gone — is now a one-click
action on the field that already exists.

**No new element is created.** `coverArtwork(field, ground)` sets `is_masking` and `background_color` on
the field the creator already placed, keeping its id, key, coordinates, alignment, font family and size.
The renderer paints an element's background before its content, so the artwork is covered and the value
drawn on top, in one pass, by one element. Applying it twice only refreshes the colour — an earlier
version re-padded on each call, so a field adjusted a few times crept across the page. A test pins that.

**The colour is sampled, never assumed.** `GET /v1/certificate-templates/{id}/artwork-colour` returns the
modal colour of a band *outside* the region and along its margins — deliberately not the middle, which is
the ink being covered and would drag the patch toward grey. Measured on the reported design as `#F8F8F8`.
Assuming white would smear every cream, navy or grey certificate; tested against all three.

**Padding is small and capped.** The first attempt padded 18% of the field's height and clipped "This
certificate acknowledges that" on the line above — covering neighbouring static content is a worse defect
than leaving a stray pixel, because it destroys something the design meant to say. Now capped at 0.4% of
page height, enough for a descender. Verified by rendering the real template: placeholder gone, "John Doe"
alone and centred, and every surrounding line, logo, signature and border intact.

**Honest limitation:** the patch is a flat fill. On watermarked or textured stock it is faintly visible as
a smoother rectangle. Matching a texture would require inpainting the artwork, which is a different piece
of work entirely; removing the placeholder from the source design remains the clean fix, and the editor
says so.

**Tests:** 6 backend colour-sampling (ink does not tint the sample, page colour measured for
white/cream/navy/grey, edge regions, hex shape) and 8 web (styling and key preserved, sampled colour
applied, grows for descenders, cannot reach the line above, never off-page, idempotent, no new element,
reversible). Backend 2142 passed / 1 skipped / 0 failed; web 656 passed / 1 skipped.
