using System.Reflection;
using System.Runtime.InteropServices;

namespace SpitbolTests;

/// <summary>
/// Locates the built spitbol executable and the test-corpus directory.
///
/// Resolution order:
///   * SPITBOL_EXE    env var (full path to the exe)            -> else probe up
///                    the tree for SpitbolExe\x64\{Debug,Release}\Spitbol.exe
///   * SPITBOL_TESTS  env var (full path to a corpus directory) -> else the
///                    "cases" folder copied next to the test assembly
///
/// Probing lets the suite "just work" when SpitbolTests lives in the same
/// solution as SpitbolExe; the env vars let CI or a relocated build override.
/// </summary>
public static class SpitbolPaths
{
    private static readonly string ExeName =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Spitbol.exe" : "spitbol";

    public static string? FindExe()
    {
        var fromEnv = Environment.GetEnvironmentVariable("SPITBOL_EXE");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
            return Path.GetFullPath(fromEnv);

        // Walk up from the test assembly looking for SpitbolExe\x64\<cfg>\<exe>.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var cfg in new[] { "Debug", "Release" })
            {
                var candidate = Path.Combine(dir.FullName, "SpitbolExe", "x64", cfg, ExeName);
                if (File.Exists(candidate))
                    return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }

    public static string CorpusRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable("SPITBOL_TESTS");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
            return Path.GetFullPath(fromEnv);

        // Prefer the project's source cases/ dir, so a program added there is
        // discovered on the next build without relying on the content-copy
        // glob. Fall back to the cases/ copied next to the assembly (e.g. a
        // relocated CI run where the source tree isn't present).
        var projectDir = ProjectDir();
        if (projectDir is not null)
        {
            var srcCases = Path.Combine(projectDir, "cases");
            if (Directory.Exists(srcCases))
                return srcCases;
        }
        return Path.Combine(AppContext.BaseDirectory, "cases");
    }

    private static string? ProjectDir() =>
        typeof(SpitbolPaths).Assembly
            .GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SpitbolTests.ProjectDir")?.Value;

    /// <summary>Per-case run timeout. Override with SPITBOL_TIMEOUT_MS.</summary>
    public static TimeSpan Timeout()
    {
        var raw = Environment.GetEnvironmentVariable("SPITBOL_TIMEOUT_MS");
        return int.TryParse(raw, out var ms) && ms > 0
            ? TimeSpan.FromMilliseconds(ms)
            : TimeSpan.FromSeconds(30);
    }

    /// <summary>When set, golden .expected files are (re)written from the run
    /// output. Use only against a TRUSTED build to capture a baseline.</summary>
    public static bool UpdateGolden() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SPITBOL_UPDATE_GOLDEN"));
}
