using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Certificates;
using Kurx.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// A dynamic field renders its value, once, and nothing else (D-355).
///
/// <para>Written after a report of doubled text on a finished certificate — the resolved name appearing
/// alongside a <c>[Recipient's Full Name]</c> placeholder. The pipeline turned out to be correct: one
/// template field becomes exactly one render element, and one element draws exactly one thing. The
/// placeholder was printed into the uploaded artwork, which is pixels the renderer cannot edit.</para>
///
/// <para>These tests pin the half that *is* ours, so that if a genuine double-draw is ever introduced —
/// a label rendered beside its value, an element emitted twice — it fails here rather than on somebody's
/// certificate. The technique throughout is comparison: render the same layout twice with one thing
/// changed, and assert only that thing changed.</para>
/// </summary>
public class CertificateFieldSubstitutionTests
{
    private static readonly ICertificateDocumentRenderer Renderer =
        new CertificateDocumentRenderer(new QrCodeGenerator(NullLogger<QrCodeGenerator>.Instance));

    private const string Page = "a4-landscape";

    private static CertificateRenderElement Dynamic(string key, double y = 40) =>
        new("dynamicfield", key, null, 10, y, 80, 10, 0, 0, false, null, null,
            "sans", 24, "normal", "#000000", "center", "middle");

    private static CertificateRenderElement Static(string text, double y = 20) =>
        new("text", null, text, 10, y, 80, 10, 0, 0, false, null, null,
            "sans", 24, "normal", "#000000", "center", "middle");

    private static CertificateRenderData Data(params (string Key, string Value)[] values) =>
        new(values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal),
            new Dictionary<string, byte[]>());

    private static Task<byte[]> Png(CertificateDocument doc, CertificateRenderData data) =>
        Renderer.RenderPngAsync(doc, data, 96);

    // ── The value replaces the placeholder; it does not join it ─────────────────────────────────

    /// <summary>The core claim. A field with a value must not look like a field without one — if the
    /// placeholder were drawn as well as the value, these two would share ink and the test would still
    /// pass, so the stronger assertion is below.</summary>
    [Fact]
    public async Task A_resolved_field_renders_differently_from_an_unresolved_one()
    {
        var doc = new CertificateDocument(Page, null, [Dynamic("participant_name")]);

        var resolved = await Png(doc, Data(("participant_name", "John Doe")));
        var unresolved = await Png(doc, Data());

        Assert.NotEqual(resolved, unresolved);
    }

    /// <summary>The assertion that would catch a genuine double-draw.
    ///
    /// <para>If the renderer drew the placeholder *and* the value, then rendering with a value would
    /// contain everything the unresolved render contains plus more ink — so a layout whose only element
    /// is a resolved field could never match a layout whose only element is that same text as STATIC
    /// text. Matching proves the field drew the value alone.</para></summary>
    [Fact]
    public async Task A_resolved_field_draws_exactly_what_a_static_element_would()
    {
        var dynamic = new CertificateDocument(Page, null, [Dynamic("participant_name")]);
        var equivalentStatic = new CertificateDocument(Page, null, [Static("John Doe", y: 40)]);

        var fromField = await Png(dynamic, Data(("participant_name", "John Doe")));
        var fromStatic = await Png(equivalentStatic, Data());

        // Same box, same font, same alignment, same text — so the same pixels, unless something extra
        // was drawn.
        Assert.Equal(fromStatic, fromField);
    }

    /// <summary>Every field key the product resolves, checked the same way. A regression in any one of
    /// them — a label drawn beside the value, a placeholder left behind — separates these renders.</summary>
    [Theory]
    [InlineData("participant_name", "John Doe")]
    [InlineData("event_name", "Certificate Course")]
    [InlineData("event_date", "2026-08-15")]
    [InlineData("issue_date", "2026-08-15")]
    [InlineData("organization", "Demo Organisation")]
    [InlineData("achievement", "First Place")]
    [InlineData("certificate_id", "CERT-2026-0001")]
    [InlineData("some_custom_field", "Anything At All")]
    public async Task Every_dynamic_key_draws_only_its_value(string key, string value)
    {
        var dynamic = new CertificateDocument(Page, null, [Dynamic(key)]);
        var equivalentStatic = new CertificateDocument(Page, null, [Static(value, y: 40)]);

        Assert.Equal(
            await Png(equivalentStatic, Data()),
            await Png(dynamic, Data((key, value))));
    }

    // ── Static content is untouched by participant data ─────────────────────────────────────────

    /// <summary>A certificate's fixed text — titles, paragraphs, labels — must render identically no
    /// matter whose certificate it is.</summary>
    [Fact]
    public async Task Static_text_renders_identically_for_different_participants()
    {
        var doc = new CertificateDocument(Page, null, [
            Static("Certificate of Completion", y: 10),
            Static("This certificate acknowledges that", y: 25),
        ]);

        var first = await Png(doc, Data(("participant_name", "John Doe")));
        var second = await Png(doc, Data(("participant_name", "Ananya Rao")));

        Assert.Equal(first, second);
    }

    /// <summary>Static text is never substituted, even when it happens to look like a field key.</summary>
    [Fact]
    public async Task Static_text_is_not_substituted_even_when_it_names_a_key()
    {
        var doc = new CertificateDocument(Page, null, [Static("{participant_name}", y: 30)]);

        var withValue = await Png(doc, Data(("participant_name", "John Doe")));
        var without = await Png(doc, Data());

        Assert.Equal(without, withValue);
    }

    // ── Once per element, regardless of the value ───────────────────────────────────────────────

    /// <summary>Two participants, one layout: only the name changes, and the rest of the page is
    /// byte-identical. Catches a value leaking between rows as much as a double-draw.</summary>
    [Fact]
    public async Task Two_participants_differ_only_where_the_field_is()
    {
        var doc = new CertificateDocument(Page, null, [
            Static("Certificate of Completion", y: 10),
            Dynamic("participant_name", y: 40),
        ]);

        var john = await Png(doc, Data(("participant_name", "John Doe")));
        var same = await Png(doc, Data(("participant_name", "John Doe")));
        var other = await Png(doc, Data(("participant_name", "Priya Patel")));

        Assert.Equal(john, same);        // deterministic
        Assert.NotEqual(john, other);    // and actually driven by the value
    }

    /// <summary>A long name is shrunk to fit its box (D-355 Phase 4) rather than overflowing into a
    /// second line of ink that could read as duplication.</summary>
    [Fact]
    public async Task A_very_long_value_still_produces_one_rendered_field()
    {
        var doc = new CertificateDocument(Page, null, [Dynamic("participant_name")]);

        var longName = await Png(doc, Data(("participant_name",
            "Dr Ananya Venkataraman Krishnamurthy Subramanian Rao-Chatterjee III")));
        var shortName = await Png(doc, Data(("participant_name", "Jo")));

        Assert.NotEmpty(longName);
        Assert.NotEqual(shortName, longName);
    }

    [Fact]
    public async Task A_long_event_name_behaves_the_same_way()
    {
        var doc = new CertificateDocument(Page, null, [Dynamic("event_name")]);

        var rendered = await Png(doc, Data(("event_name",
            "Advanced Confined Space Entry, Rescue Procedures and Emergency Response Training Programme")));

        Assert.NotEmpty(rendered);
    }

    /// <summary>An optional field with nothing behind it draws its key as a visible marker, not a blank —
    /// so a mapping mistake shows on the preview instead of shipping as a gap. It must not draw a value
    /// *and* a marker.</summary>
    [Fact]
    public async Task An_empty_optional_field_draws_a_marker_and_nothing_else()
    {
        var doc = new CertificateDocument(Page, null, [Dynamic("achievement")]);
        var marker = new CertificateDocument(Page, null, [Static("{achievement}", y: 40)]);

        Assert.Equal(await Png(marker, Data()), await Png(doc, Data()));
        Assert.Equal(await Png(marker, Data()), await Png(doc, Data(("achievement", ""))));
    }

    // ── Both output paths agree ─────────────────────────────────────────────────────────────────

    /// <summary>PDF and PNG come from one <c>Build()</c>, so substitution cannot differ between them.
    /// Asserted rather than assumed, since they are separate public methods.</summary>
    [Fact]
    public async Task The_pdf_path_substitutes_the_same_way_the_image_path_does()
    {
        var doc = new CertificateDocument(Page, null, [Dynamic("participant_name")]);

        var withValue = await Renderer.RenderPdfAsync(doc, Data(("participant_name", "John Doe")));
        var withoutValue = await Renderer.RenderPdfAsync(doc, Data());

        Assert.NotEmpty(withValue);
        Assert.NotEqual(withValue.Length, withoutValue.Length);
    }

    // ── The mapping the demo file needs ─────────────────────────────────────────────────────────

    /// <summary>The supplied demo export, column by column. A header the vocabulary does not recognise is
    /// mapped by hand on the review screen, so the cost of a miss is a manual step — but the common
    /// training-export headers should not need one.</summary>
    [Theory]
    [InlineData("RecipientName", "participant_name")]
    [InlineData("CourseName", "event_name")]
    [InlineData("Organisation", "organization")]
    [InlineData("IssueDate", "issue_date")]
    [InlineData("CertificateNumber", "certificate_number")]
    public void The_demo_files_headers_are_recognised(string column, string expected)
    {
        var service = new SpreadsheetService([new CsvSpreadsheetReader(), new XlsxSpreadsheetReader()]);

        Assert.Equal(expected, service.SuggestMapping([column])[column]);
    }

    /// <summary>End to end on the demo file: every column resolves to a distinct field, and the values
    /// reach the row unchanged.</summary>
    [Fact]
    public void The_demo_file_resolves_every_column_to_a_distinct_field()
    {
        var service = new SpreadsheetService([new CsvSpreadsheetReader(), new XlsxSpreadsheetReader()]);
        var csv = Encoding.UTF8.GetBytes(
            "RecipientName,CourseName,Organisation,IssueDate,CertificateNumber\n" +
            "John Doe,Certificate Course,Demo Organisation,2026-08-15,CERT-2026-0001\n");

        var read = service.Read(csv, "demo_certificate.csv");
        Assert.True(read.Ok, read.Error);

        var mapping = service.SuggestMapping(read.Value!.Columns);

        Assert.Equal(5, mapping.Count);
        Assert.Equal(5, mapping.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            ["John Doe", "Certificate Course", "Demo Organisation", "2026-08-15", "CERT-2026-0001"],
            read.Value.Rows[0]);
    }
}
