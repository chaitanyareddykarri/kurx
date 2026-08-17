using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

// ── The certificate module (D-355) ───────────────────────────────────────────────────────────────
//
// A self-contained module: certificate issuance, verification and distribution. It references the
// platform's Event and User by **id value only** — there is not one navigation property to either, and
// none at all to Ticket, Order, Payment, Competition, Wallet, Seat, Chat or KYC. That is what makes the
// module theoretically extractable, and it is a constraint to preserve rather than an accident.
//
// These tables are NEW. The dormant `certificates`, `design_templates` and `id_cards` tables are left
// untouched: the old `Certificate` carries non-nullable TicketId and UserId, which directly contradicts
// issuing to someone who has no Kurx account.

/// <summary>
/// A certificate design: an uploaded image plus the fields placed on top of it.
///
/// <para><b>The upload is the design.</b> <see cref="BackgroundStorageKey"/> points at the organiser's
/// own artwork, stored once and never re-encoded. Everything the platform adds is an overlay described by
/// <see cref="CertificateTemplateField"/> rows. Nothing here rewrites the uploaded file.</para>
///
/// <para><b>Ownership is a person, not an organisation.</b> A User owns an Event on this platform, so a
/// reusable template belongs to the Event Creator who made it. <see cref="EventId"/> is nullable and that
/// nullability is the reuse mechanism: a template attached to an event is that event's, and a template
/// with no event is a library entry the same creator can later duplicate onto another event. It is
/// modelled this way rather than with a separate library table because the two are the same object at
/// different moments, and a second table would need every field of this one.</para>
/// </summary>
public class CertificateTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The event this template belongs to, or null for a creator's reusable library template.</summary>
    public Guid? EventId { get; set; }

    /// <summary>The Event Creator who owns it. Never null — a template with no owner cannot be
    /// authorised, listed or duplicated.</summary>
    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = null!;

    // ── The uploaded artwork ────────────────────────────────────────────────────────────────────
    /// <summary>Storage key of the uploaded design, resolved through <c>IStorage</c>. A key, never a URL:
    /// URLs expire, and this is the one thing that must still resolve in five years.</summary>
    public string? BackgroundStorageKey { get; set; }
    public string? BackgroundContentType { get; set; }

    /// <summary>Natural pixel size of the upload. Held because the editor needs the aspect ratio to place
    /// fields against, and re-reading the image from storage to learn it is a download per edit.</summary>
    public int? BackgroundWidthPx { get; set; }
    public int? BackgroundHeightPx { get; set; }

    /// <summary>Which page this design prints onto — the LABEL for the size below (D-361). Kept so the
    /// editor can say "A4 · Portrait" rather than "210 × 297", and so a preset survives a round-trip
    /// without being re-derived from the numbers.</summary>
    public CertificatePageSize PageSize { get; set; } = CertificatePageSize.A4Landscape;

    /// <summary>The page in millimetres — the authoritative size (D-361).
    ///
    /// <para>Physical, not pixels: a certificate has to print at the size it was designed for rather than
    /// whatever the printer infers. The renderer reads these directly, which is what lets
    /// <see cref="CertificatePageSize.Custom"/> exist and what removed the name → millimetre table that
    /// previously had to be kept in step across the server and the browser.</para>
    ///
    /// <para>Double rather than int because the imperial presets are not whole millimetres: US Letter is
    /// 215.9 × 279.4 mm exactly, and rounding it would print a certificate a fifth of a millimetre wrong
    /// on every edge.</para></summary>
    public double PageWidthMm { get; set; } = 297;
    public double PageHeightMm { get; set; } = 210;

    public CertificateTemplateStatus Status { get; set; } = CertificateTemplateStatus.Draft;

    /// <summary>Incremented on every change that affects rendering, once anything has been issued from
    /// this template. <see cref="IssuedCertificate.TemplateVersion"/> records which version produced a
    /// given certificate.
    ///
    /// <para>This is what keeps history honest: without it, editing a template silently rewrites what
    /// every past certificate claims to look like. It is a domain version, deliberately NOT EF's
    /// <c>IsRowVersion</c> optimistic-concurrency token — that answers "did someone else write while I
    /// was editing", which is a different question and would change on edits that do not affect
    /// rendering at all.</para></summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// One element placed on a template — a line of fixed text, a per-recipient value, an image, or a QR.
///
/// <para><b>Geometry is percentages of the page, never pixels.</b> A design positioned in a browser
/// viewport has to render identically onto A4 at 300dpi, and the only way that survives is for the
/// stored document to carry no resolution at all.</para>
/// </summary>
public class CertificateTemplateField
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }

    public CertificateFieldKind Kind { get; set; }

    /// <summary>The data key this field renders, for <see cref="CertificateFieldKind.DynamicField"/> —
    /// e.g. <c>participant_name</c>, or <c>employee_grade</c> if that is what the organiser's spreadsheet
    /// holds.
    ///
    /// <para><b>Deliberately an arbitrary string, not an enum.</b> The set of things a certificate can say
    /// is the organiser's, not ours: a fixed vocabulary would mean every new column needs a code change
    /// and a deploy. Spreadsheet columns map onto these keys at import.</para></summary>
    public string? FieldKey { get; set; }

    /// <summary>What the editor calls this field. Human-facing only.</summary>
    public string? Label { get; set; }

    /// <summary>Literal text for a <see cref="CertificateFieldKind.Text"/> field — identical on every
    /// certificate issued from the template.</summary>
    public string? StaticText { get; set; }

    // ── Geometry: percent of page width/height. X/Y are the top-left corner. ────────────────────
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>Degrees clockwise about the element's own centre.</summary>
    public double Rotation { get; set; }

    /// <summary>Paint order; lower is painted first. The uploaded background is always beneath all of
    /// them.</summary>
    public int ZOrder { get; set; }

    /// <summary>Whether generation refuses a recipient with no value for <see cref="FieldKey"/>. A blank
    /// space where a name belongs is worse than a refusal the organiser can act on.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Whether this field exists to COVER something already printed on the uploaded artwork.
    ///
    /// <para>Text baked into a JPG is pixels; nothing can edit it. "Replacing" printed text means painting
    /// <see cref="BackgroundColor"/> over it and drawing new text on top — which is why a masking field is
    /// a distinct, recorded intent rather than an ordinary field that happens to have a fill. The editor
    /// has to tell the organiser that moving one reveals the original underneath.</para></summary>
    public bool IsMasking { get; set; }

    /// <summary>Whether this element is only a HANDLE on text the artwork already prints, rather than
    /// something drawn.
    ///
    /// <para>Detection makes every line of an uploaded design clickable, so a creator can reach for words
    /// they can see. But most of those lines are never changed — and covering unchanged text to redraw it
    /// identically is destructive: the flat fill erases the design's watermark and texture, leaving a
    /// smooth rectangle with hard edges where there was paper. That is the "this was edited" tell.</para>
    ///
    /// <para>While true the renderer draws nothing and fills nothing; the artwork speaks for itself. The
    /// moment the creator actually edits the words it becomes false, and the element starts covering and
    /// drawing like any other. So a design is altered exactly where it was altered, and nowhere else.</para>
    ///
    /// <para><see cref="BackgroundColor"/> is still carried while this is true — it is the paper colour
    /// sampled at detection time, held ready for the edit that may never come.</para></summary>
    public bool MirrorsArtwork { get; set; }

    /// <summary>Fill painted behind this element, or null for transparent. On a masking field this is the
    /// colour sampled from just OUTSIDE the covered text — sampled from inside, it is a blend of ink and
    /// paper and shows as a smear exactly where it was meant to be invisible.</summary>
    public string? BackgroundColor { get; set; }

    // ── Typography. Null means "renderer default" rather than a hardcoded value here. ───────────
    public string? FontFamily { get; set; }
    public double? FontSizePt { get; set; }
    public string? FontWeight { get; set; }

    /// <summary><c>italic</c>, or null for upright. Its own column rather than folded into
    /// <see cref="FontWeight"/>: a design can be bold AND italic, and one string cannot say both.</summary>
    public string? FontStyle { get; set; }

    public bool Underline { get; set; }

    /// <summary>Multiplier on the line box, e.g. <c>1.4</c>. Null renders at the renderer's default —
    /// certificates use generous leading on names and tight leading on addresses.</summary>
    public double? LineHeight { get; set; }

    /// <summary>Extra tracking in ems. Small positive values are what make a title read as a title.</summary>
    public double? LetterSpacing { get; set; }

    public string? Color { get; set; }
    public CertificateHorizontalAlignment HorizontalAlignment { get; set; } = CertificateHorizontalAlignment.Left;
    public CertificateVerticalAlignment VerticalAlignment { get; set; } = CertificateVerticalAlignment.Middle;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The configurable certificate-ID format for one event, and the counter behind it.
///
/// <para><b><see cref="NextSequence"/> is never read into application memory and written back.</b> Two
/// concurrent generations would read the same value and issue the same id. It is allocated by a single
/// atomic SQL statement that increments and returns under the row lock — the same rule the platform
/// already applies to every shared counter.</para>
/// </summary>
public class CertificateIdRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>One rule per event. Enforced by a unique index, which is also what makes the
    /// create-if-absent upsert safe under concurrency.</summary>
    public Guid EventId { get; set; }

    /// <summary>Substituted for <c>{PREFIX}</c>. e.g. <c>CERT</c>.</summary>
    public string Prefix { get; set; } = "CERT";

    /// <summary>The id shape. Supported tokens: <c>{PREFIX}</c>, <c>{YYYY}</c>, <c>{YY}</c>,
    /// <c>{SEQ}</c>. Anything else in the string is literal.</summary>
    public string Pattern { get; set; } = DefaultPattern;

    /// <summary>The next sequence value to hand out. Starts at 1 so the first certificate reads
    /// <c>…-00001</c> rather than <c>…-00000</c>.</summary>
    public long NextSequence { get; set; } = 1;

    /// <summary>Zero-padding width for <c>{SEQ}</c>. A sequence longer than this is NOT truncated — it
    /// simply renders wider, because a truncated id would collide.</summary>
    public int Padding { get; set; } = 5;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public const string DefaultPattern = "{PREFIX}-{YYYY}-{SEQ}";

    /// <summary>Renders one certificate id from this rule.
    ///
    /// <para>Pure and side-effect free, so the format is testable without a database and the same
    /// function serves a preview of "what will my ids look like". <paramref name="issuedAt"/> is passed in
    /// rather than read from the clock so a certificate reissued later can be formatted against its own
    /// issue date if a caller needs that.</para></summary>
    public string Format(long sequence, DateTime issuedAt) =>
        (Pattern.Length == 0 ? DefaultPattern : Pattern)
            .Replace("{PREFIX}", Prefix ?? "", StringComparison.Ordinal)
            .Replace("{YYYY}", issuedAt.ToString("yyyy"), StringComparison.Ordinal)
            .Replace("{YY}", issuedAt.ToString("yy"), StringComparison.Ordinal)
            .Replace("{SEQ}", sequence.ToString().PadLeft(Math.Max(0, Padding), '0'), StringComparison.Ordinal);
}

/// <summary>
/// One generation run: a spreadsheet, a mapping, a preview, an approval, and the certificates that came
/// out of it.
///
/// <para>A batch is a persisted row rather than a transient request because it is edited after the fact —
/// a name is misspelled, an award is wrong, someone asks for their copy a week later. A run that returned
/// files and forgot them would make "fix this one" mean "regenerate all two hundred".</para>
/// </summary>
public class CertificateBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid TemplateId { get; set; }

    /// <summary>The template version this run rendered against, pinned at approval. Editing the template
    /// afterwards does not retroactively change what this batch produced.</summary>
    public int TemplateVersion { get; set; }

    public Guid CreatedByUserId { get; set; }
    public string Name { get; set; } = null!;

    public CertificateBatchStatus Status { get; set; } = CertificateBatchStatus.Draft;

    /// <summary>The uploaded participant list, kept so a run can be re-examined against its own source.</summary>
    public string? SourceFileStorageKey { get; set; }
    public string? SourceFileName { get; set; }

    /// <summary>Spreadsheet column name → <see cref="CertificateTemplateField.FieldKey"/>, as JSON. Held
    /// as data rather than code because the mapping is the organiser's, decided per run.</summary>
    public string? ColumnMappingJson { get; set; }

    public int RowCount { get; set; }

    /// <summary>How many certificates the preview stage rendered. The confirmed flow is preview → inspect
    /// → approve → full run, and this records the size of that first, small pass.</summary>
    public int PreviewCount { get; set; }

    /// <summary>When the organiser approved the preview. Null means the full run must not start.</summary>
    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// The person a certificate is for.
///
/// <para><b>A recipient is not a user and must never require one.</b> Certificates are issued to whoever
/// is on the organiser's list, most of whom have no Kurx account and never will. This holds only what
/// issuing and delivering needs — a name, optionally an email — and is emphatically not a shadow copy of
/// <c>User</c>: no password, no phone, no profile, no preferences.</para>
///
/// <para><b>Linking is opt-in and evidence-based.</b> <see cref="UserId"/> is set only when the platform
/// has independently verified that the account owns <see cref="NormalizedEmail"/>. Linking on an
/// unverified address would make claiming someone else's certificate a signup form away.</para>
/// </summary>
public class CertificateRecipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }

    /// <summary>The import that produced this recipient, or null for one added by hand.</summary>
    public Guid? BatchId { get; set; }

    /// <summary>The name as it will be printed. The organiser's spelling is authoritative — the platform
    /// does not correct, title-case or otherwise reinterpret it.</summary>
    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    /// <summary>Lowercased, trimmed <see cref="Email"/>. Exists solely so the linking rule can match
    /// without a functional index, and so two spellings of one address cannot become two people.</summary>
    public string? NormalizedEmail { get; set; }

    /// <summary>The Kurx account this recipient turned out to be, or null. Nullable is the whole point.</summary>
    public Guid? UserId { get; set; }

    /// <summary>When the link was made. Kept separate from <see cref="UserId"/> so "linked at signup" and
    /// "linked by a later backfill" remain distinguishable.</summary>
    public DateTime? LinkedAt { get; set; }

    /// <summary>1-based row in the uploaded file. The organiser's coordinate system, not ours — "row 47
    /// failed" has to point at something they can find in their own spreadsheet.</summary>
    public int? SourceRowNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An issued certificate — the durable, verifiable record.
///
/// <para><b>Append-only after issue.</b> Only <see cref="Status"/> and the lineage pointer ever change.
/// A correction is a new certificate with a new id, not an edit to this one; the revocation is recorded
/// beside it. The original stays historically accurate because nothing overwrites it.</para>
///
/// <para><b><see cref="FieldValuesJson"/> is a snapshot, not a join.</b> A certificate must render and
/// verify identically in five years even if the event was renamed, the template edited and the recipient's
/// details changed. Reconstructing it from live rows would make the document quietly drift.</para>
/// </summary>
public class IssuedCertificate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The public, human-facing identifier — what the QR resolves to and what someone types into
    /// the verification page. Unique platform-wide, because verification is by this value alone with no
    /// event context, and immutable after insert.</summary>
    public string CertificateId { get; set; } = null!;

    public Guid EventId { get; set; }
    public Guid TemplateId { get; set; }
    public int TemplateVersion { get; set; }

    /// <summary>The run that produced it, or null for one issued by hand.</summary>
    public Guid? BatchId { get; set; }

    public Guid RecipientId { get; set; }

    /// <summary>Field key → value, exactly as rendered. See the class remarks.</summary>
    public string FieldValuesJson { get; set; } = "{}";

    public IssuedCertificateStatus Status { get; set; } = IssuedCertificateStatus.Issued;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    // ── Rendered artefacts. Null until Phase 4 renders them. ────────────────────────────────────
    public string? PdfStorageKey { get; set; }
    public string? PngStorageKey { get; set; }

    /// <summary>SHA-256 of the rendered PDF, pinning the exact artefact this record describes.</summary>
    public string? DocumentSha256 { get; set; }

    // ── Signature. Null until Phase 5 signs. ────────────────────────────────────────────────────
    /// <summary>The signing key that produced <see cref="Signature"/>. Recorded per certificate because
    /// keys rotate and a certificate must stay verifiable against the key that actually signed it.</summary>
    public string? SignatureKeyId { get; set; }
    public string? Signature { get; set; }

    /// <summary>The certificate this one replaces, for a corrected reissue. Null for an original.</summary>
    public Guid? SupersedesCertificateId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The record of a certificate being withdrawn.
///
/// <para>A row rather than a set of nullable columns on <see cref="IssuedCertificate"/>: revocation is an
/// event with an actor, a reason and a time, and modelling it as flags loses all three the moment a
/// certificate is revoked twice or the reason is corrected.</para>
/// </summary>
public class CertificateRevocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The revoked certificate's row id.</summary>
    public Guid CertificateId { get; set; }

    public string Reason { get; set; } = null!;
    public Guid RevokedByUserId { get; set; }
    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The corrected certificate issued in its place, or null when it was revoked outright.</summary>
    public Guid? ReplacementCertificateId { get; set; }
}

/// <summary>
/// One attempt to put a certificate in front of its recipient.
///
/// <para>Per-attempt rather than a status column on the certificate, so a retry after a bounce is visible
/// as a second attempt instead of overwriting the first. <see cref="Destination"/> is the address used at
/// the time — a recipient's email can change, and the delivery record must say where it actually went.</para>
/// </summary>
public class CertificateDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CertificateId { get; set; }

    public CertificateDeliveryChannel Channel { get; set; }

    /// <summary>Where it was sent. Null for <see cref="CertificateDeliveryChannel.Account"/>, which is an
    /// availability rather than a send.</summary>
    public string? Destination { get; set; }

    public CertificateDeliveryStatus Status { get; set; } = CertificateDeliveryStatus.Pending;

    /// <summary>The provider's id for the message, kept so a later bounce can be correlated back.
    ///
    /// <para>Note what it does and does not mean: it confirms the provider ACCEPTED the message, not that
    /// anyone received it. <see cref="CertificateDeliveryStatus.Sent"/> must not be read as "delivered".</para></summary>
    public string? ProviderMessageId { get; set; }

    public string? Error { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Append-only telemetry: the counts the event dashboard reports.
///
/// <para><b>Deliberately carries no IP address and no user-agent.</b> The confirmed requirement is counts
/// — how many viewed, downloaded, verified — and storing per-view identifying data would create a
/// privacy obligation nobody asked for, on a surface that is public and unauthenticated.</para>
/// </summary>
public class CertificateEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CertificateId { get; set; }

    public CertificateEventType Type { get; set; }

    /// <summary>The request's correlation id, matching the platform's audit convention, so a telemetry row
    /// can be joined to logs and traces during an incident without storing anything about the person.</summary>
    public string? CorrelationId { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A key used to sign certificates (D-355, Phase 5).
///
/// <para><b>Deliberately separate from the platform's JWT <c>SigningKey</c>.</b> The two have opposite
/// lifetimes and that difference is not cosmetic. A JWT key is retired quickly and its private half is
/// discarded, because a token signed by it expires within minutes and the key becomes pure liability. A
/// certificate must remain verifiable for years, so its key's PUBLIC half is retained permanently — a
/// certificate whose key was purged would become unverifiable, which is indistinguishable from a forgery
/// to the person holding it. Sharing one table would mean one retirement policy for two requirements.</para>
///
/// <para><b>The private half is wrapped by the existing <c>ISigningKeyProtector</c></b>, so this reuses the
/// platform's KMS integration rather than inventing a second key-management story.
/// <see cref="ProtectionScheme"/> records which scheme wrapped it, so keys survive a change of protection
/// instead of being stranded.</para>
///
/// <para><b>Rows are never deleted.</b> Retiring stops a key signing NEW certificates; it does not stop
/// old ones verifying. A compromised key keeps verifying too — a certificate signed before the compromise
/// was genuinely issued by the platform, and the verification page says so alongside the warning rather
/// than calling a real certificate fake.</para>
/// </summary>
public class CertificateSigningKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable identifier recorded on every certificate this key signs, so verification selects
    /// the exact key rather than trying each in turn.</summary>
    public string KeyId { get; set; } = null!;

    public string Algorithm { get; set; } = "ES256";

    /// <summary>Base64 SubjectPublicKeyInfo. Safe to disclose, and **never nulled** — this is what makes
    /// a certificate verifiable for as long as it exists.</summary>
    public string PublicKeySpki { get; set; } = null!;

    /// <summary>Base64 PKCS#8, wrapped by <see cref="ProtectionScheme"/>. Nulled when the key is retired
    /// or compromised: it can no longer sign, and holding the material after that is only risk.</summary>
    public string? ProtectedPrivateKey { get; set; }

    /// <summary>Which protector wrapped <see cref="ProtectedPrivateKey"/>. Without it, changing the
    /// platform's protection scheme would strand every existing key.</summary>
    public string? ProtectionScheme { get; set; }

    public CertificateSigningKeyState State { get; set; } = CertificateSigningKeyState.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RetiredAt { get; set; }

    /// <summary>When the key was declared compromised, and why. Surfaced on the verification page as a
    /// warning beside a certificate that is still reported valid.</summary>
    public DateTime? CompromisedAt { get; set; }
    public string? CompromisedReason { get; set; }
}

/// <summary>A long-lived, revocable way for someone with no account to reach their certificates
/// (D-355, Phase 10).
///
/// <para>It belongs to the <b>recipient</b>, not to a single certificate, and that is what makes it
/// durable: when a certificate is corrected, the same link keeps working and shows the corrected one.
/// A link per certificate would go stale on the first reissue — exactly when the holder most needs it.</para>
///
/// <para><b>The token is a bearer credential and is never stored.</b> Only its SHA-256 hash is kept, so a
/// database copy cannot be turned back into working links. The raw value is returned once, at creation,
/// and after that the platform genuinely cannot produce it again.</para>
///
/// <para>No expiry, deliberately. A certificate is a permanent claim about something that happened, and a
/// link that quietly stops working in a year fails the person who kept it exactly as they were told to.
/// Revocation is the control instead — deliberate, attributable, and reversible only by issuing a new
/// link.</para></summary>
public class CertificateAccessLink
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecipientId { get; set; }

    /// <summary>SHA-256 of the token, hex-encoded. The lookup key: the raw token is never written down.</summary>
    public string TokenHash { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When it was last used. Kept so an organiser revoking a link can see whether it was ever
    /// actually reached, and nothing more — no address, no device, no location.</summary>
    public DateTime? LastAccessedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }

    public bool IsLive => RevokedAt is null;
}
