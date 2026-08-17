namespace Kurx.Application.Abstractions;

/// <summary>
/// Reads a participant list (D-355, Phase 6).
///
/// <para><b>Deliberately unhelpful.</b> No delimiter sniffing, no type coercion, no locale handling, no
/// formula evaluation. Every cell reaches the renderer as the string the organiser sees in their
/// spreadsheet. The classic import failure is a parser being clever — <c>007</c> becoming <c>7</c>, a date
/// becoming <c>44562</c>, a phone number becoming scientific notation — and here that lands on a printed
/// certificate that has already been emailed.</para>
///
/// <para>One boundary over both formats so the mapping, validation and generation paths never learn which
/// one was uploaded.</para>
/// </summary>
public interface ISpreadsheetReader
{
    /// <summary>Whether this reader handles the given file name's extension.</summary>
    bool CanRead(string fileName);

    /// <summary>Parses a whole file.</summary>
    /// <exception cref="SpreadsheetFormatException">The file cannot be read as a spreadsheet at all.</exception>
    SpreadsheetTable Read(byte[] content, string fileName);
}

/// <param name="Columns">Header names in file order. May contain duplicates and blanks — real
/// spreadsheets do, and refusing them would reject files people actually have.</param>
/// <param name="Rows">One entry per data row, each a list of cell strings. A row may be shorter than the
/// header: a ragged tail is normal in a hand-edited sheet.</param>
/// <param name="Truncated">True when the file held more rows than the reader would take. Surfaced rather
/// than silently dropped, because "we generated 1000 of your 1500" must never be discovered by counting.</param>
public sealed record SpreadsheetTable(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    bool Truncated = false);

/// <summary>The file is not a readable spreadsheet. Distinct from "the file is readable but its contents
/// are wrong", which is the caller's judgement to make.</summary>
public class SpreadsheetFormatException(string message) : Exception(message);

/// <summary>
/// Chooses a reader by file name and applies the limits that protect the server (D-355, Phase 6).
/// </summary>
public interface ISpreadsheetService
{
    /// <summary>Reads an uploaded participant list.</summary>
    ServiceResult<SpreadsheetTable> Read(byte[] content, string fileName);

    /// <summary>Guesses which column feeds which certificate field.
    ///
    /// <para>A guess that must always be SHOWN, never applied silently. A column called "Name" holding a
    /// team name is an ordinary spreadsheet, and a silent mapping prints the wrong words on every
    /// certificate in the run.</para></summary>
    IReadOnlyDictionary<string, string> SuggestMapping(IReadOnlyList<string> columns);
}
