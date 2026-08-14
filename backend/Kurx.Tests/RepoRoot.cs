using System.Runtime.CompilerServices;

namespace Kurx.Tests;

/// <summary>Locates the repository root for the handful of tests that read repository <b>files</b> rather
/// than running code — the 204-declaration sweep over <c>Kurx.Api/Endpoints/*.cs</c> and the committed
/// OpenAPI spec's coverage ratchet.
///
/// <para><b>Why not <c>AppContext.BaseDirectory</c>.</b> Both callers used to walk up from the test binary,
/// which works only while the binary sits inside the repo. The container test recipe passes
/// <c>-p:ArtifactsPath=/tmp/artifacts</c> (see <c>.claude/memory/testing-standards.md</c>), putting the
/// binary <b>outside the <c>/src</c> mount entirely</b> — so the walk climbs to <c>/</c> and finds nothing.
/// That cost each caller differently and neither outcome was acceptable: <c>NoContentDeclarationTests</c>
/// failed loudly and was written off as "an artifact of the runner", while <c>OpenApiCoverageTests</c>
/// returned early and <b>silently skipped its own assertion</b> — a coverage ratchet that quietly stopped
/// ratcheting is worse than one that fails.</para>
///
/// <para><b>Why <see cref="CallerFilePathAttribute"/> works.</b> The compiler bakes the calling source
/// file's path into the call site at build time, so the answer does not depend on where the assembly ends
/// up. Build and test run in the same container against the same mount, so the path resolves there exactly
/// as it does on a developer machine.</para>
///
/// <para>The <c>BaseDirectory</c> walk is kept as a fallback for the one case the attribute cannot cover:
/// a binary built elsewhere and run against a repo at a different path.</para></summary>
internal static class RepoRoot
{
    /// <summary>The repo root — the directory holding both <c>backend/</c> and <c>docs/</c> — or
    /// <c>null</c> when it genuinely cannot be found from either starting point.</summary>
    public static DirectoryInfo? Find([CallerFilePath] string callerFilePath = "")
    {
        string?[] starts = [Path.GetDirectoryName(callerFilePath), AppContext.BaseDirectory];

        foreach (var start in starts)
        {
            if (string.IsNullOrEmpty(start)) continue;

            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "backend"))
                    && Directory.Exists(Path.Combine(dir.FullName, "docs")))
                    return dir;
        }

        return null;
    }

    /// <summary>A repo-relative path, or <c>null</c> when the root could not be located. Callers assert on
    /// the result rather than skipping — see the type doc for why silently skipping is the failure mode
    /// this class exists to remove.</summary>
    public static string? PathTo(params string[] segments)
    {
        if (Find() is not { } root) return null;
        return Path.Combine([root.FullName, .. segments]);
    }
}
