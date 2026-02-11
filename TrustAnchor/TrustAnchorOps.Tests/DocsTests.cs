using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace TrustAnchorOps.Tests;

public class DocsTests
{
    [Fact]
    public void Repo_DoesNotReference_Legacy_Tooling()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(path));

        foreach (var path in files)
        {
            var normalized = NormalizePath(path);
            Assert.DoesNotContain("/legacy-tooling/", normalized, StringComparison.Ordinal);

            if (!IsTextFile(path)) continue;
            var content = File.ReadAllText(path);
            Assert.False(ContainsLegacyToolingReference(content), $"Legacy tooling reference found in {path}");
        }
    }

    [Fact]
    public void Docs_Reference_TrustAnchor_Tooling()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("TrustAnchorDeployer", readme);
        Assert.Contains("ConfigureAgent", readme);
    }

    private static bool IsIgnoredPath(string path)
    {
        var normalized = NormalizePath(path);
        return normalized.Contains("/.git/", StringComparison.Ordinal)
            || normalized.Contains("/bin/", StringComparison.Ordinal)
            || normalized.Contains("/obj/", StringComparison.Ordinal)
            || normalized.Contains("/.worktrees/", StringComparison.Ordinal)
            || normalized.Contains("/worktrees/", StringComparison.Ordinal)
            || normalized.Contains("/docs/plans/", StringComparison.Ordinal)
            || normalized.EndsWith("/TrustAnchor/TrustAnchorOps.Tests/DocsTests.cs", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".md" or ".cs" or ".csproj" or ".sln" or ".json" or ".yml" or ".yaml" or ".sh" or ".txt" or ".props" or ".targets";
    }

    private static bool ContainsLegacyToolingReference(string content)
    {
        return Regex.IsMatch(content, @"\blegacy-tooling\b", RegexOptions.IgnoreCase);
    }
}
