using System.Text.RegularExpressions;

namespace Kurx.Tests;

/// <summary>Every route that can answer <c>204</c> says so in its contract.
///
/// <para><b>The gap this closes.</b> D-313 declared response bodies across the API and correctly observed
/// that <i>no</i> operation had ever declared a 204, including the 32 that return
/// <c>Results.NoContent()</c> unconditionally. Those 32 were fixed. Three others were missed, and they are
/// the more dangerous shape — <c>value is null ? NoContent() : Ok(value)</c>, which answers 200-with-body
/// <b>or</b> 204 depending on state. Each declared only <c>Produces&lt;T&gt;</c>, so the spec described
/// half of a conditional endpoint and described the misleading half: a generated client reads
/// "200 + schema" as "a body always arrives" and has no branch for the empty case. Two of the three are on
/// the event-wizard autosave path, where the very first load of a new draft returns exactly the
/// undeclared outcome.</para>
///
/// <para><b>Why this reads source rather than the spec.</b> <c>openapi.json</c> is regenerated on demand,
/// so a spec-driven assertion passes or fails on when someone last ran the generator rather than on what
/// the code does. The registration chain in source is the truth, and it is checkable the moment the code
/// changes.</para></summary>
public class NoContentDeclarationTests
{
    /// <summary>Resolved through <see cref="RepoRoot"/>, which finds the repo from this source file's
    /// compile-time path. This used to walk up from <c>AppContext.BaseDirectory</c> and could not survive
    /// the container recipe's <c>-p:ArtifactsPath</c>, which puts the binary outside the repo mount — the
    /// long-standing "environmental" failure in the suite baseline was this lookup, not this assertion.</summary>
    private static string EndpointsDirectory()
        => RepoRoot.PathTo("backend", "Kurx.Api", "Endpoints") ?? "";

    /// <summary>Splits a file into route registrations: from a <c>Map*(</c> call to the <c>});</c> that
    /// closes its chain. Crude by design — it only has to be good enough to pair a handler body with the
    /// builder calls attached to it, and a miss here fails loudly rather than silently passing.</summary>
    private static IEnumerable<string> RouteBlocks(string source)
    {
        var starts = Regex.Matches(source, @"\bMap(Get|Post|Put|Patch|Delete)\s*\(");
        foreach (Match start in starts)
        {
            // The chain ends at the first `});` at or after the handler — everything between is the
            // handler body plus the builder calls that decorate it.
            var end = source.IndexOf("});", start.Index, StringComparison.Ordinal);
            if (end < 0) continue;
            var terminator = source.IndexOf(';', end);
            if (terminator < 0) continue;
            yield return source[start.Index..(terminator + 1)];
        }
    }

    [Fact]
    public void Every_route_that_can_return_204_declares_it()
    {
        var directory = EndpointsDirectory();
        Assert.True(Directory.Exists(directory), $"Could not locate the endpoints directory from {AppContext.BaseDirectory}.");

        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(directory, "*.cs"))
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("Results.NoContent()")) continue;

            foreach (var block in RouteBlocks(source))
            {
                if (!block.Contains("Results.NoContent()")) continue;
                if (block.Contains("Status204NoContent")) continue;

                // Name the route, not just the file — the point of the failure is to say which one.
                var route = Regex.Match(block, @"Map(Get|Post|Put|Patch|Delete)\s*\(\s*""([^""]*)""");
                offenders.Add($"{Path.GetFileName(file)}: {route.Groups[1].Value.ToUpperInvariant()} \"{route.Groups[2].Value}\"");
            }
        }

        Assert.True(offenders.Count == 0,
            "These routes can answer 204 but do not declare it, so the spec describes only their body case:\n  "
            + string.Join("\n  ", offenders.Distinct()));
    }
}
