using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Picks a reader and enforces the limits that protect the server (D-355, Phase 6).
///
/// <para>Format selection is by extension, not by sniffing content. An organiser who renames a
/// <c>.numbers</c> file to <c>.csv</c> gets a clear "could not read" rather than a parser that guesses
/// and produces plausible nonsense — and content sniffing on an untrusted upload is a larger attack
/// surface than a filename check.</para>
/// </summary>
public class SpreadsheetService(IEnumerable<ISpreadsheetReader> readers) : ISpreadsheetService
{
    /// <summary>10MB. A participant list is text; anything larger is a spreadsheet with images embedded
    /// in it, and reading it into memory to find that out is the problem.</summary>
    public const int MaxFileBytes = 10 * 1024 * 1024;

    public ServiceResult<SpreadsheetTable> Read(byte[] content, string fileName)
    {
        if (content is null || content.Length == 0) return ServiceResult<SpreadsheetTable>.Fail("empty_file");
        if (content.Length > MaxFileBytes) return ServiceResult<SpreadsheetTable>.Fail("file_too_large");

        var name = (fileName ?? "").Trim();
        var reader = readers.FirstOrDefault(r => r.CanRead(name));
        if (reader is null) return ServiceResult<SpreadsheetTable>.Fail("unsupported_file_type");

        try
        {
            var table = reader.Read(content, name);
            if (table.Rows.Count == 0) return ServiceResult<SpreadsheetTable>.Fail("no_rows");
            return ServiceResult<SpreadsheetTable>.Success(table);
        }
        catch (SpreadsheetFormatException)
        {
            return ServiceResult<SpreadsheetTable>.Fail("invalid_spreadsheet");
        }
        catch (Exception)
        {
            // A malformed file must fail safely, as a refusal the organiser can act on — never as a 500
            // that looks like the platform is broken when the upload is what is wrong.
            return ServiceResult<SpreadsheetTable>.Fail("invalid_spreadsheet");
        }
    }

    /// <summary>
    /// Guesses which column feeds which certificate field, from the header names alone.
    ///
    /// <para><b>Always shown, never applied silently.</b> A column called "Name" holding the TEAM name is
    /// an ordinary spreadsheet; a silent mapping prints the wrong words on every certificate in the run
    /// and nobody notices until they are sent. The mapping screen renders this pre-filled beside real
    /// sample values so the organiser is correcting a visible guess rather than trusting an invisible one.</para>
    ///
    /// <para>The suggestions are a convenience over a vocabulary that is deliberately open: a field key is
    /// an arbitrary string, so a column this does not recognise is mapped by hand rather than refused.</para>
    /// </summary>
    public IReadOnlyDictionary<string, string> SuggestMapping(IReadOnlyList<string> columns)
    {
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var column in columns)
        {
            if (string.IsNullOrWhiteSpace(column)) continue;

            // Compared on letters and digits only, so "Participant Name", "participant_name" and
            // "Participant-Name" all match without needing an entry each.
            var normalised = new string(column.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

            var token = normalised switch
            {
                "name" or "participantname" or "fullname" or "studentname" or "candidatename"
                    or "recipient" or "recipientname" or "attendeename" => "participant_name",
                "email" or "emailaddress" or "mail" => "email",
                "team" or "teamname" => "team_name",
                "role" => "role",
                "designation" or "title" or "post" => "designation",
                "organization" or "organisation" or "college" or "institution" or "company"
                    or "school" or "university" => "organization",
                "achievement" or "award" or "prize" or "position" or "rank" => "achievement",
                "certificateno" or "certificatenumber" or "certno" => "certificate_number",
                "registrationid" or "regid" or "registrationno" or "registrationnumber" => "registration_id",
                "participantid" or "rollno" or "rollnumber" or "employeeid" => "participant_id",
                // A "course" or "programme" column is the event under another name — common on training
                // and certification exports, which is most of what gets uploaded here.
                "event" or "eventname" or "course" or "coursename"
                    or "program" or "programme" or "programname" or "training" => "event_name",
                "date" or "eventdate" or "completiondate" => "event_date",
                // Distinct from the event's date: when the certificate was issued. Kept separate because
                // a course can finish in March and be certified in April, and printing one as the other
                // is the kind of error nobody spots until someone checks.
                "issuedate" or "issuedon" or "dateofissue" => "issue_date",
                _ => null,
            };

            // First column wins for a token: a sheet with both "Name" and "Full Name" should map one of
            // them, not have the second silently overwrite the first.
            if (token is not null && !mapping.ContainsValue(token)) mapping[column] = token;
        }

        return mapping;
    }
}
