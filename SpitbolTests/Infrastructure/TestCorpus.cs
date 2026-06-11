namespace SpitbolTests;

public enum CaseMode
{
    /// <summary>A sibling "&lt;name&gt;.expected" exists: diff output against it.</summary>
    Golden,
    /// <summary>No golden: program self-checks via chks(); pass iff exit 0 and no "*FAIL".</summary>
    SelfCheck,
}

public sealed record TestCase(
    string Name,            // corpus-relative, no extension, '/'-separated
    string SourceFile,      // absolute path to the .sbl/.sno
    string? ExpectedFile,   // absolute path to .expected, or null
    string? StdinFile,      // absolute path to .in, or null
    string? FilterFile,     // absolute path to .filter, or null
    CaseMode Mode);

public static class TestCorpus
{
    private static readonly string[] SourceGlobs = { "*.sbl", "*.sno", "*.spt" };

    /// <summary>xUnit MemberData source: one row per discovered case name.
    /// Names are stable, serializable strings so Test Explorer shows one node
    /// per program.</summary>
    public static IEnumerable<object[]> CaseNames()
    {
        foreach (var c in Discover())
            yield return new object[] { c.Name };
    }

    public static IEnumerable<TestCase> Discover()
    {
        var root = SpitbolPaths.CorpusRoot();
        if (!Directory.Exists(root))
            yield break;

        var skip = LoadSkip(root);

        var files = SourceGlobs
            .SelectMany(g => Directory.EnumerateFiles(root, g, SearchOption.AllDirectories))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var name = Path.GetRelativePath(root, file);
            name = Path.ChangeExtension(name, null)!.Replace('\\', '/');
            if (skip.Contains(name))
                continue;

            yield return Resolve(name);
        }
    }

    /// <summary>Resolve a single case by name (used inside the test body).</summary>
    public static TestCase Resolve(string name)
    {
        var root = SpitbolPaths.CorpusRoot();
        var basePath = Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar));

        var source = SourceGlobs
            .Select(g => Path.ChangeExtension(basePath, g.TrimStart('*', '.')))
            .FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"No source file for case '{name}' under {root}");

        var expected = First(basePath + ".expected");
        var stdin    = First(basePath + ".in");
        var filter   = First(basePath + ".filter");
        var mode     = expected is null ? CaseMode.SelfCheck : CaseMode.Golden;

        return new TestCase(name, source, expected, stdin, filter, mode);

        static string? First(string path) => File.Exists(path) ? path : null;
    }

    private static HashSet<string> LoadSkip(string root)
    {
        var skipFile = Path.Combine(root, "skip.txt");
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(skipFile))
        {
            foreach (var line in File.ReadAllLines(skipFile))
            {
                var t = line.Trim();
                if (t.Length > 0 && !t.StartsWith('#'))
                    set.Add(t.Replace('\\', '/'));
            }
        }
        return set;
    }
}
