using ClosedXML.Excel;
using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// XLSX, via ClosedXML (D-344, Phase 6).
///
/// <para><b>Formulas are never evaluated.</b> A cell carrying <c>=A1</c> is read from its CACHED value —
/// the number Excel last stored — and never recomputed. Evaluating a stranger's uploaded workbook is a
/// remote-code-execution surface in miniature: formulas can reference external workbooks, and an
/// evaluation engine handed an adversarial file is a much larger attack surface than a value reader.
/// The cached value is also what the organiser actually saw when they saved the file, which is the value
/// they mean.</para>
///
/// <para><b>Cells are read as displayed, not as typed.</b> A cell formatted as text containing
/// <c>007</c> must stay <c>007</c>, and a date must stay <c>12/09/2026</c> rather than becoming
/// <c>44562</c>. That is the whole reason this reads formatted strings rather than raw values.</para>
/// </summary>
public class XlsxSpreadsheetReader : ISpreadsheetReader
{
    /// <summary>Matches the CSV reader's ceiling so the two formats behave identically to everything
    /// downstream.</summary>
    public const int MaxRows = CsvSpreadsheetReader.MaxRows;

    /// <summary>A guard against a sheet whose used range is enormous because of stray formatting far to
    /// the right. Reading ten thousand empty columns per row is a memory problem, not a feature.</summary>
    private const int MaxColumns = 200;

    public bool CanRead(string fileName) =>
        fileName?.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) == true;

    public SpreadsheetTable Read(byte[] content, string fileName)
    {
        using var stream = new MemoryStream(content);

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            // A corrupt file, an .xls masquerading as .xlsx, or something that is not a workbook at all.
            // Fails as a format error rather than propagating a library exception the API would render
            // as a 500.
            throw new SpreadsheetFormatException($"That file could not be read as an Excel workbook. {ex.Message}");
        }

        using (workbook)
        {
            // The first worksheet, deliberately: asking which sheet is a question the organiser should not
            // have to answer for the common case, and guessing among several would be worse than taking
            // the one Excel opens on.
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new SpreadsheetFormatException("The workbook has no worksheets.");

            var used = sheet.RangeUsed();
            if (used is null) throw new SpreadsheetFormatException("The first worksheet is empty.");

            var firstRow = used.RangeAddress.FirstAddress.RowNumber;
            var lastRow = used.RangeAddress.LastAddress.RowNumber;
            var firstColumn = used.RangeAddress.FirstAddress.ColumnNumber;
            var lastColumn = Math.Min(used.RangeAddress.LastAddress.ColumnNumber, firstColumn + MaxColumns - 1);

            var columns = new List<string>();
            for (var c = firstColumn; c <= lastColumn; c++)
                columns.Add(Read(sheet.Cell(firstRow, c)).Trim());

            if (columns.Count == 0 || columns.All(string.IsNullOrWhiteSpace))
                throw new SpreadsheetFormatException("The first row must be a header naming each column.");

            var rows = new List<IReadOnlyList<string>>();
            var truncated = false;
            for (var r = firstRow + 1; r <= lastRow; r++)
            {
                if (rows.Count >= MaxRows) { truncated = true; break; }

                var cells = new List<string>(columns.Count);
                for (var c = firstColumn; c <= lastColumn; c++) cells.Add(Read(sheet.Cell(r, c)));

                // A row that is entirely blank is spacing, not a participant — the same rule the CSV
                // reader applies, so the two formats behave identically downstream.
                if (cells.All(string.IsNullOrWhiteSpace)) continue;
                rows.Add(cells);
            }

            return new SpreadsheetTable(columns, rows, truncated);
        }
    }

    /// <summary>One cell, as the organiser sees it.</summary>
    private static string Read(IXLCell cell)
    {
        try
        {
            // A formula cell is read from what Excel last computed and stored. Never recomputed: see the
            // class remarks.
            if (cell.HasFormula)
                return cell.CachedValue.IsBlank ? "" : cell.CachedValue.ToString() ?? "";

            if (cell.Value.IsBlank) return "";

            // The FORMATTED string, so a text-formatted "007" stays "007" and a date stays the date the
            // sheet displays rather than its underlying serial number.
            return cell.GetFormattedString() ?? "";
        }
        catch (Exception)
        {
            // One unreadable cell must not fail an entire import. Blank is honest — the value could not
            // be read — and the organiser sees it as a gap on the mapping screen rather than as a crash.
            return "";
        }
    }
}
