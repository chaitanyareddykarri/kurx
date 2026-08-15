using System.Text;
using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// CSV, per RFC 4180 (D-344, Phase 6).
///
/// <para>Hand-rolled, and only just: the grammar is small and this implements all of it — quoted fields,
/// embedded commas, embedded newlines, doubled quotes, CRLF, bare LF, a lone CR, and Excel's UTF-8 BOM.
/// What it deliberately does not do is guess. A NuGet parser would be a fine choice too; this is roughly
/// its size, has no supply-chain surface, and the one behaviour that matters — never reinterpreting a cell
/// — is the behaviour most parsers make configurable and get wrong by default.</para>
/// </summary>
public class CsvSpreadsheetReader : ISpreadsheetReader
{
    /// <summary>Hard ceiling on rows read. A file larger than this is a mistake, and parsing 200MB to
    /// discover that is a denial of service. Truncation is reported, never silent.</summary>
    public const int MaxRows = 5000;

    public bool CanRead(string fileName) =>
        fileName?.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) == true;

    public SpreadsheetTable Read(byte[] content, string fileName)
    {
        var records = Split(Decode(content));
        if (records.Count == 0) throw new SpreadsheetFormatException("The file is empty.");

        var columns = records[0].Select(c => c.Trim()).ToList();
        if (columns.Count == 0 || columns.All(string.IsNullOrWhiteSpace))
            throw new SpreadsheetFormatException("The first row must be a header naming each column.");

        // A trailing newline yields one empty record, and so does a blank line someone left between two
        // groups. Neither is a participant.
        var data = records.Skip(1)
            .Where(r => r.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();

        var truncated = data.Count > MaxRows;
        return new SpreadsheetTable(
            columns,
            data.Take(MaxRows).Select(IReadOnlyList<string> (r) => r).ToList(),
            truncated);
    }

    /// <summary>Decodes bytes to text, honouring a UTF-8 BOM.
    ///
    /// <para>Excel writes one on "CSV UTF-8". Left in place it becomes an invisible prefix on the first
    /// header name, so <c>Name</c> stops matching the column called <c>Name</c> — and the organiser sees
    /// their first column silently unmapped with nothing on screen to explain it.</para></summary>
    public static string Decode(byte[] bytes) =>
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetString(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
                ? bytes.AsSpan(3)
                : bytes);

    /// <summary>The state machine. One pass, character by character: a regex cannot express a field that
    /// may itself contain the delimiter and the record separator.</summary>
    private static List<List<string>> Split(string content)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (quoted)
            {
                if (c != '"') { field.Append(c); continue; }
                // A doubled quote inside a quoted field is one literal quote; a single one closes it.
                if (i + 1 < content.Length && content[i + 1] == '"') { field.Append('"'); i++; continue; }
                quoted = false;
                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    // Swallowed; the \n that follows ends the record. A lone \r (classic Mac) ends it here.
                    if (i + 1 < content.Length && content[i + 1] == '\n') break;
                    goto case '\n';
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = [];
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        // The last record, when the file does not end in a newline.
        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
        return records;
    }
}
