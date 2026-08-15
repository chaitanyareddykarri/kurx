using System.Text;
using ClosedXML.Excel;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Certificates;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Participant-list parsing, CSV and XLSX (D-344, Phase 6).
///
/// <para>Every case here is a real spreadsheet export, not a grammar exercise: Excel's UTF-8 BOM, CRLF, a
/// comma inside a quoted organisation name, a doubled quote inside a nickname, a blank line someone left
/// mid-sheet, a leading-zero roll number, a date, a formula. Each one, mishandled, produces certificates
/// with wrong values printed on them rather than a visible error — which is why the assertions are about
/// what is NOT done to a cell as much as what is.</para>
///
/// <para>The two formats are asserted against the same expectations wherever possible, because everything
/// downstream is supposed to be unable to tell which was uploaded.</para>
/// </summary>
public class SpreadsheetReaderTests
{
    private static readonly CsvSpreadsheetReader Csv = new();
    private static readonly XlsxSpreadsheetReader Xlsx = new();
    private static readonly SpreadsheetService Service = new([Csv, Xlsx]);

    private static byte[] Bytes(string content) => Encoding.UTF8.GetBytes(content);

    /// <summary>Builds a real .xlsx in memory. Generated rather than checked in so the fixture is
    /// readable and the test says exactly what the sheet contains.</summary>
    private static byte[] Workbook(Action<IXLWorksheet> build)
    {
        using var wb = new XLWorkbook();
        build(wb.AddWorksheet("Participants"));
        using var output = new MemoryStream();
        wb.SaveAs(output);
        return output.ToArray();
    }

    // ── CSV: the grammar ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Csv_reads_a_plain_file()
    {
        var table = Csv.Read(Bytes("Name,Team\nRahul Sharma,ByteBuilders\nPriya Patel,CodeCrafters\n"), "p.csv");

        Assert.Equal(["Name", "Team"], table.Columns);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["Rahul Sharma", "ByteBuilders"], table.Rows[0]);
    }

    /// <summary>Excel writes a BOM on "CSV UTF-8". Left in place it becomes an invisible prefix on the
    /// first header, so <c>Name</c> stops matching the column called <c>Name</c> and the organiser's first
    /// column is silently unmapped with nothing on screen to explain it.</summary>
    [Fact]
    public void Csv_strips_the_excel_byte_order_mark()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Bytes("Name,Team\nRahul,ByteBuilders\n")).ToArray();

        Assert.Equal("Name", Csv.Read(bytes, "p.csv").Columns[0]);
    }

    [Theory]
    [InlineData("Name\r\nRahul\r\nPriya\r\n")]
    [InlineData("Name\rRahul\rPriya\r")]
    [InlineData("Name\nRahul\nPriya")]
    public void Csv_handles_every_line_ending(string content)
    {
        Assert.Equal(2, Csv.Read(Bytes(content), "p.csv").Rows.Count);
    }

    /// <summary>The case a naive <c>Split(',')</c> gets wrong, and the one that silently truncates an
    /// organisation name to everything before its comma.</summary>
    [Fact]
    public void Csv_quoted_fields_may_contain_the_delimiter()
    {
        var table = Csv.Read(Bytes("Name,Organization\nRahul,\"Example Institute, Hyderabad\"\n"), "p.csv");

        Assert.Equal(["Rahul", "Example Institute, Hyderabad"], table.Rows[0]);
    }

    [Fact]
    public void Csv_quoted_fields_may_contain_doubled_quotes_and_newlines()
    {
        var table = Csv.Read(Bytes("Name,Note\n\"Arjun \"\"AJ\"\" Kumar\",\"line one\nline two\"\n"), "p.csv");

        Assert.Single(table.Rows);
        Assert.Equal("Arjun \"AJ\" Kumar", table.Rows[0][0]);
        Assert.Equal("line one\nline two", table.Rows[0][1]);
    }

    /// <summary>A trailing newline is not a participant, and neither is the blank line someone left
    /// between two groups. Both would otherwise render a certificate with nobody's name on it.</summary>
    [Fact]
    public void Csv_blank_lines_are_not_participants()
    {
        var table = Csv.Read(Bytes("Name\nRahul\n\nPriya\n\n"), "p.csv");

        Assert.Equal(2, table.Rows.Count);
    }

    /// <summary>A ragged tail is normal in a hand-edited sheet; rejecting the whole upload over one
    /// missing optional cell helps nobody.</summary>
    [Fact]
    public void Csv_tolerates_a_short_row()
    {
        var table = Csv.Read(Bytes("Name,Team,Role\nRahul,ByteBuilders\n"), "p.csv");

        Assert.Single(table.Rows);
        Assert.Equal(2, table.Rows[0].Count);
    }

    // ── The rule that matters most, in both formats ─────────────────────────────────────────────

    /// <summary>Values reach the renderer as the organiser typed them. A helpful parser turning
    /// <c>007</c> into <c>7</c> lands on a printed certificate.</summary>
    [Fact]
    public void Csv_never_reinterprets_a_value()
    {
        var table = Csv.Read(Bytes("Name,Roll,Date,Phone\nRahul,007,12/09/2026,+919876543210\n"), "p.csv");

        Assert.Equal("007", table.Rows[0][1]);
        Assert.Equal("12/09/2026", table.Rows[0][2]);
        Assert.Equal("+919876543210", table.Rows[0][3]);
    }

    /// <summary>The same guarantee in XLSX, where it is harder: a text-formatted cell must not lose its
    /// leading zeros, and a date must not arrive as its underlying serial number.</summary>
    [Fact]
    public void Xlsx_preserves_leading_zeros_and_shows_dates_as_dates()
    {
        var bytes = Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "Name";
            sheet.Cell(1, 2).Value = "Roll";
            sheet.Cell(1, 3).Value = "Date";
            sheet.Cell(2, 1).Value = "Rahul Sharma";
            // Text-formatted, which is how a roll number with leading zeros survives in a real sheet.
            sheet.Cell(2, 2).SetValue("007").Style.NumberFormat.Format = "@";
            sheet.Cell(2, 3).Value = new DateTime(2026, 9, 12);
        });

        var table = Xlsx.Read(bytes, "p.xlsx");

        Assert.Equal("007", table.Rows[0][1]);
        // Not "46277" — the serial number is exactly the failure this guards against.
        Assert.DoesNotContain("46", table.Rows[0][2], StringComparison.Ordinal);
        Assert.Contains("2026", table.Rows[0][2], StringComparison.Ordinal);
    }

    /// <summary>Formulas are read from their cached value and never recomputed. Evaluating a stranger's
    /// uploaded workbook is a far larger attack surface than reading one.</summary>
    [Fact]
    public void Xlsx_never_evaluates_a_formula()
    {
        var bytes = Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "Name";
            sheet.Cell(1, 2).Value = "Computed";
            sheet.Cell(2, 1).Value = "Rahul Sharma";
            sheet.Cell(2, 2).FormulaA1 = "=A2";
        });

        // Must not throw, must not recompute, must not fail the import.
        var table = Xlsx.Read(bytes, "p.xlsx");

        Assert.Single(table.Rows);
        Assert.Equal("Rahul Sharma", table.Rows[0][0]);
    }

    [Fact]
    public void Xlsx_preserves_unicode()
    {
        var bytes = Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "नाम";
            sheet.Cell(2, 1).Value = "अनन्या राव";
        });

        var table = Xlsx.Read(bytes, "p.xlsx");

        Assert.Equal("नाम", table.Columns[0]);
        Assert.Equal("अनन्या राव", table.Rows[0][0]);
    }

    [Fact]
    public void Xlsx_skips_blank_rows_like_csv_does()
    {
        var bytes = Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "Name";
            sheet.Cell(2, 1).Value = "Rahul";
            // Row 3 left entirely blank.
            sheet.Cell(4, 1).Value = "Priya";
        });

        Assert.Equal(2, Xlsx.Read(bytes, "p.xlsx").Rows.Count);
    }

    // ── Headers that real sheets actually have ──────────────────────────────────────────────────

    /// <summary>Duplicate and blank headers are refused by nothing: real exports contain both, and
    /// rejecting the file would reject spreadsheets people genuinely have. The mapping screen is where the
    /// organiser resolves the ambiguity.</summary>
    [Fact]
    public void Duplicate_and_blank_headers_are_tolerated()
    {
        var table = Csv.Read(Bytes("Name,,Name\nRahul,x,Sharma\n"), "p.csv");

        Assert.Equal(3, table.Columns.Count);
        Assert.Equal("", table.Columns[1]);
        Assert.Equal(3, table.Rows[0].Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    [InlineData(",,,\n")]
    public void A_file_with_no_usable_header_is_refused(string content)
    {
        Assert.Throws<SpreadsheetFormatException>(() => Csv.Read(Bytes(content), "p.csv"));
    }

    // ── Limits ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Truncation is REPORTED, never silent. "We generated 1000 of your 1500" must not be
    /// something the organiser discovers by counting.</summary>
    [Fact]
    public void Reading_stops_at_the_row_ceiling_and_says_so()
    {
        var content = "Name\n" + string.Concat(Enumerable.Repeat("Rahul\n", CsvSpreadsheetReader.MaxRows + 500));

        var table = Csv.Read(Bytes(content), "p.csv");

        Assert.Equal(CsvSpreadsheetReader.MaxRows, table.Rows.Count);
        Assert.True(table.Truncated);
    }

    [Fact]
    public void A_file_within_the_ceiling_is_not_marked_truncated()
    {
        Assert.False(Csv.Read(Bytes("Name\nRahul\n"), "p.csv").Truncated);
    }

    // ── Format selection and safe failure ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("participants.csv", true)]
    [InlineData("participants.CSV", true)]
    [InlineData("participants.xlsx", false)]
    [InlineData("participants.numbers", false)]
    public void Csv_claims_only_csv(string fileName, bool expected) =>
        Assert.Equal(expected, Csv.CanRead(fileName));

    [Theory]
    [InlineData("participants.xlsx", true)]
    [InlineData("participants.XLSX", true)]
    [InlineData("participants.xls", false)]
    [InlineData("participants.csv", false)]
    public void Xlsx_claims_only_xlsx(string fileName, bool expected) =>
        Assert.Equal(expected, Xlsx.CanRead(fileName));

    [Fact]
    public void An_unsupported_extension_is_refused_by_name()
    {
        var result = Service.Read(Bytes("Name\nRahul\n"), "participants.numbers");

        Assert.Equal("unsupported_file_type", result.Error);
    }

    /// <summary>A malformed file fails as a refusal the organiser can act on, never as a 500 that looks
    /// like the platform is broken when the upload is what is wrong.</summary>
    [Fact]
    public void A_file_that_is_not_a_workbook_fails_safely()
    {
        var result = Service.Read(Bytes("this is definitely not a spreadsheet"), "participants.xlsx");

        Assert.False(result.Ok);
        Assert.Equal("invalid_spreadsheet", result.Error);
    }

    /// <summary>An .xlsx renamed to .csv parses as CSV rather than being sniffed. Guessing at content on
    /// an untrusted upload is a larger attack surface than trusting the extension and failing clearly.</summary>
    [Fact]
    public void Format_is_chosen_by_extension_not_by_sniffing()
    {
        var xlsxBytes = Workbook(sheet => { sheet.Cell(1, 1).Value = "Name"; sheet.Cell(2, 1).Value = "Rahul"; });

        var result = Service.Read(xlsxBytes, "participants.csv");

        // Read as CSV, so it produces garbage columns or no rows — either way it does NOT silently
        // succeed as a workbook.
        Assert.True(!result.Ok || result.Value!.Columns[0] != "Name");
    }

    [Fact]
    public void Size_and_emptiness_are_refused_before_parsing()
    {
        Assert.Equal("empty_file", Service.Read([], "p.csv").Error);
        Assert.Equal("file_too_large",
            Service.Read(new byte[SpreadsheetService.MaxFileBytes + 1], "p.csv").Error);
    }

    [Fact]
    public void A_header_with_no_data_rows_is_refused()
    {
        Assert.Equal("no_rows", Service.Read(Bytes("Name,Team\n"), "p.csv").Error);
    }

    [Fact]
    public void Both_formats_produce_the_same_table()
    {
        var csv = Service.Read(Bytes("Name,Team\nRahul Sharma,ByteBuilders\n"), "p.csv");
        var xlsx = Service.Read(Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "Name";
            sheet.Cell(1, 2).Value = "Team";
            sheet.Cell(2, 1).Value = "Rahul Sharma";
            sheet.Cell(2, 2).Value = "ByteBuilders";
        }), "p.xlsx");

        Assert.True(csv.Ok, csv.Error);
        Assert.True(xlsx.Ok, xlsx.Error);
        Assert.Equal(csv.Value!.Columns, xlsx.Value!.Columns);
        Assert.Equal(csv.Value.Rows[0], xlsx.Value.Rows[0]);
    }

    // ── Mapping suggestions ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Name", "participant_name")]
    [InlineData("Full Name", "participant_name")]
    [InlineData("participant_name", "participant_name")]
    [InlineData("Participant-Name", "participant_name")]
    [InlineData("Team", "team_name")]
    [InlineData("Award", "achievement")]
    [InlineData("College", "organization")]
    [InlineData("Roll No", "participant_id")]
    [InlineData("Email Address", "email")]
    public void A_recognised_header_is_suggested(string column, string expected)
    {
        Assert.Equal(expected, Service.SuggestMapping([column])[column]);
    }

    /// <summary>The vocabulary is open, so a column nobody anticipated is simply not guessed — it is
    /// mapped by hand on the review screen rather than refused.</summary>
    [Fact]
    public void An_unrecognised_header_is_left_for_the_organiser()
    {
        var mapping = Service.SuggestMapping(["Employee Grade", "Cost Centre"]);

        Assert.Empty(mapping);
    }

    /// <summary>A sheet with both "Name" and "Full Name" should map one of them, not have the second
    /// silently overwrite the first.</summary>
    [Fact]
    public void Two_columns_competing_for_one_field_do_not_both_win()
    {
        var mapping = Service.SuggestMapping(["Name", "Full Name"]);

        Assert.Single(mapping);
        Assert.Equal("participant_name", mapping["Name"]);
    }

    [Fact]
    public void Blank_headers_are_never_suggested()
    {
        Assert.Empty(Service.SuggestMapping(["", "   "]));
    }

    /// <summary>The example from the requirements, end to end.</summary>
    [Fact]
    public void The_requirements_example_maps_as_specified()
    {
        var mapping = Service.SuggestMapping(["Name", "Event", "Date", "Achievement"]);

        Assert.Equal("participant_name", mapping["Name"]);
        Assert.Equal("event_name", mapping["Event"]);
        Assert.Equal("event_date", mapping["Date"]);
        Assert.Equal("achievement", mapping["Achievement"]);
    }
}
