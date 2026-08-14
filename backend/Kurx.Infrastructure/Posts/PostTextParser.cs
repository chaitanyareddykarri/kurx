using System.Text.RegularExpressions;

namespace Kurx.Infrastructure.Posts;

/// <summary>Pulls <c>#hashtags</c> and <c>@mentions</c> out of a post body (D-262).
///
/// <para>Server-side and only server-side. Accepting a tag list from the client would let a caller attach
/// their post to any trend, and accepting a mention list would let them notify anyone — neither has
/// anything to do with what they actually wrote.</para>
///
/// <para>The pattern is character-for-character the one <c>web/components/posts/post-body.tsx</c>
/// tokenizes with. That is not incidental: if the two drifted, the client would render a link over text
/// the server never indexed, or index text the client never links. One grammar, two consumers.</para></summary>
public static partial class PostTextParser
{
    /// <summary>A token must start the string or follow whitespace/'(' — so an email address and a C#
    /// generic argument are not mentions — and must end the string or be followed by whitespace or
    /// closing punctuation.</summary>
    [GeneratedRegex(@"(^|[\s(])([#@][A-Za-z0-9_]{1,30})(?=$|[\s).,!?:;])",
        RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenPattern();

    /// <summary>Distinct lowercase tags, without the leading '#', in the order they appear.</summary>
    public static IReadOnlyList<string> Hashtags(string? body) => Tokens(body, '#');

    /// <summary>Distinct lowercase handles, without the leading '@', in the order they appear. Usernames
    /// are stored lowercase (<c>User.Username</c>), so the caller matches on these directly.</summary>
    public static IReadOnlyList<string> MentionHandles(string? body) => Tokens(body, '@');

    private static IReadOnlyList<string> Tokens(string? body, char prefix)
    {
        if (string.IsNullOrWhiteSpace(body)) return [];

        var seen = new List<string>();
        foreach (Match m in TokenPattern().Matches(body))
        {
            var token = m.Groups[2].Value;
            if (token[0] != prefix) continue;
            var value = token[1..].ToLowerInvariant();
            if (!seen.Contains(value)) seen.Add(value);
        }
        // Bounded so a pathological body cannot turn one post into thousands of index rows and
        // notifications. Beyond this the author is not tagging, they are spamming.
        return seen.Count > 30 ? seen[..30] : seen;
    }
}
