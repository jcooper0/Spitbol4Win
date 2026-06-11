using Xunit;

namespace SpitbolTests;

public class SpitbolTests
{
    // Resolve the exe once per test, failing with actionable guidance if it
    // isn't built. (xUnit 2.x has no dynamic skip; a clear failure is fine and
    // is what you want in CI anyway.)
    private static string RequireExe()
    {
        var exe = SpitbolPaths.FindExe();
        Assert.True(exe is not null,
            "Spitbol.exe not found. Build the Spitbol (SpitbolExe) project, or set the " +
            "SPITBOL_EXE environment variable to its full path. (See Configuration_IsResolvable.)");
        return exe!;
    }

    // ---- configuration guard ----------------------------------------------

    [Fact]
    public void Configuration_IsResolvable()
    {
        Assert.True(SpitbolPaths.FindExe() is not null,
            "Could not locate Spitbol.exe. Build SpitbolExe or set SPITBOL_EXE.");

        var corpus = SpitbolPaths.CorpusRoot();
        Assert.True(Directory.Exists(corpus),
            $"Corpus directory not found: {corpus}. Put test programs under cases\\ " +
            "or set SPITBOL_TESTS to a directory of .sbl programs.");
    }

    // ---- the corpus --------------------------------------------------------
    // One node per program. Golden cases diff against <name>.expected;
    // self-check cases require exit 0 and no "*FAIL" line.

    [Theory]
    [MemberData(nameof(TestCorpus.CaseNames), MemberType = typeof(TestCorpus))]
    public async Task Corpus(string caseName)
    {
        var exe = RequireExe();
        var c = TestCorpus.Resolve(caseName);
        var result = await SpitbolRunner.RunAsync(exe, c.SourceFile, c.StdinFile, SpitbolPaths.Timeout());

        Assert.False(result.TimedOut,
            $"'{caseName}' exceeded the {SpitbolPaths.Timeout().TotalSeconds:0}s timeout.\n" +
            Tail(result.StdErr));

        if (c.Mode == CaseMode.SelfCheck)
            AssertSelfCheck(caseName, result);
        else
            AssertGolden(c, result);
    }

    private static void AssertSelfCheck(string caseName, RunResult result)
    {
        Assert.True(result.ExitCode == 0,
            $"'{caseName}' exited with {result.ExitCode?.ToString() ?? "(killed)"}.\n" +
            Tail(result.StdErr));

        var combined = OutputNormalizer.Normalize(result.StdOut + "\n" + result.StdErr);
        var failures = combined.Split('\n').Where(l => l.Contains("*FAIL")).ToList();

        Assert.True(failures.Count == 0,
            $"'{caseName}' reported {failures.Count} chks() failure(s):\n" +
            string.Join("\n", failures.Take(25)));
    }

    // Golden comparison uses stdout AND stderr combined, so it captures
    // compile/runtime error listings wherever SPITBOL writes them. Normal
    // programs have empty stderr, so this is identical to stdout-only for them.
    private static string Captured(RunResult r) =>
        r.StdErr.Length == 0 ? r.StdOut
        : r.StdOut.Length == 0 ? r.StdErr
        : r.StdOut + "\n" + r.StdErr;

    private static void AssertGolden(TestCase c, RunResult result)
    {
        var filters = OutputNormalizer.LoadFilters(c.FilterFile);
        var actual = OutputNormalizer.Normalize(Captured(result), filters);

        if (SpitbolPaths.UpdateGolden())
        {
            File.WriteAllText(c.ExpectedFile!, actual + "\n");
            return; // baseline (re)captured from a trusted build
        }

        var expected = OutputNormalizer.Normalize(File.ReadAllText(c.ExpectedFile!), filters);

        Assert.True(expected == actual,
            $"'{c.Name}' output differs from {Path.GetFileName(c.ExpectedFile)} " +
            $"(exit {result.ExitCode?.ToString() ?? "killed"}):\n" +
            OutputNormalizer.Diff(expected, actual) +
            (result.StdErr.Length > 0 ? "\nstderr:\n" + Tail(result.StdErr) : ""));
    }

    private static string Tail(string s, int lines = 15)
    {
        var arr = s.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        return arr.Length <= lines ? s : string.Join("\n", arr[^lines..]);
    }
}
