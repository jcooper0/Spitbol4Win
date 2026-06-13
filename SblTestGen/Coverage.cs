using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SblTestGen;

/// <summary>
/// `coverage` subcommand: cross-references the error codes DEFINED by the
/// interpreter source (sbl.min) against the codes ASSERTED by the test
/// corpus, and reports which are elicited, which are not, and why.
///
/// Usage:
///     SblTestGen coverage &lt;sbl.min&gt; &lt;casesRoot&gt; [reportMd]
///
/// Definitions come from sbl.min `err`/`erb` lines (authoritative numbering;
/// the manual appendix disagrees in places and is NOT used). Assertions are
/// gathered from four mechanisms found in the corpus:
///   1. errchk('tag', NNN)         -- assert.inc self-check files
///   2. CHK('tag', 'N N ...', s)   -- the I/O suite set-membership checks
///   3. chks('expr', 'message')    -- the math suite, asserted by &amp;errtext
///   4. "error NNN" in *.expected   -- golden cases for terminating errors
///
/// chks() asserts by message text, so those are mapped back to codes by
/// matching the text against sbl.min messages.
/// </summary>
public static class Coverage
{
    public static void Run(string sblMin, string casesRoot, string? mdPath)
    {
        if (!File.Exists(sblMin))
            throw new FileNotFoundException($"sbl.min not found: {sblMin}");
        if (!Directory.Exists(casesRoot))
            throw new DirectoryNotFoundException($"cases root not found: {casesRoot}");

        var defined = ParseDefinedCodes(sblMin);          // code -> message
        var msgToCode = BuildMessageIndex(defined);       // normalized msg -> code
        var asserted = GatherAssertedCodes(casesRoot, msgToCode); // code -> set of files

        var report = BuildReport(defined, asserted);
        Console.Write(report);

        if (mdPath is not null)
        {
            File.WriteAllText(mdPath, report);
            Console.Error.WriteLine($"\n(report written to {mdPath})");
        }
    }

    // ---------------------------------------------------------------- parsing

    /// <summary>Defined codes from sbl.min: lines like
    /// "[label] err 117,message" or "erb 005,message" (leading zeros allowed).</summary>
    public static SortedDictionary<int, string> ParseDefinedCodes(string sblMin)
    {
        // optional label, then err|erb, code, comma, message to end of line.
        var rx = new Regex(@"^\s*(?:\S+\s+)?(?:err|erb)\s+0*(\d{1,3})\s*,\s*(.*?)\s*$",
                           RegexOptions.IgnoreCase);
        var map = new SortedDictionary<int, string>();
        foreach (var raw in File.ReadLines(sblMin))
        {
            var m = rx.Match(raw);
            if (!m.Success) continue;
            var code = int.Parse(m.Groups[1].Value);
            var msg = m.Groups[2].Value.Trim();
            // first definition wins (a code can appear at several err sites)
            if (!map.ContainsKey(code))
                map[code] = msg;
        }
        return map;
    }

    private static Dictionary<string, int> BuildMessageIndex(IDictionary<int, string> defined)
    {
        var idx = new Dictionary<string, int>();
        foreach (var (code, msg) in defined)
            idx.TryAdd(Normalize(msg), code);
        return idx;
    }

    private static string Normalize(string s)
    {
        s = s.ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", " ").Trim();
        return s;
    }

    // ----------------------------------------------------------- assertions

    private static readonly Regex ErrChk =
        new(@"errchk\(\s*(?:'[^']*'|""[^""]*"")\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase);

    // acode('tag','setup',NNN) -- variant used in a few ArraysTables files
    private static readonly Regex ACode =
        new(@"acode\(\s*(?:'[^']*'|""[^""]*"")\s*,\s*(?:'[^']*'|""[^""]*""|[^,]*)\s*,\s*(\d+)\s*\)",
            RegexOptions.IgnoreCase);

    private static readonly Regex Chk =
        new(@"\bCHK\(\s*(?:'[^']*'|""[^""]*"")\s*,\s*(?:'([0-9 ]+)'|""([0-9 ]+)"")\s*,\s*\d\s*\)");

    private static readonly Regex Chks =
        new(@"chks\(\s*(?:'(?:[^']|'')*'|""[^""]*"")\s*,\s*(?:'((?:[^']|'')*)'|""([^""]*)"")\s*\)",
            RegexOptions.IgnoreCase);

    private static readonly Regex ErrorLine = new(@"\berror\s+0*(\d{1,3})\b", RegexOptions.IgnoreCase);

    public static SortedDictionary<int, SortedSet<string>> GatherAssertedCodes(
        string casesRoot, IReadOnlyDictionary<string, int> msgToCode)
    {
        var hits = new SortedDictionary<int, SortedSet<string>>();
        void Add(int code, string file)
        {
            if (!hits.TryGetValue(code, out var set))
                hits[code] = set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(file);
        }

        foreach (var file in Directory.EnumerateFiles(casesRoot, "*.sbl", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(casesRoot, file);
            foreach (var line in LogicalLines(File.ReadAllLines(file)))
            {
                if (line.TrimStart().StartsWith('*')) continue; // comment line
                foreach (Match m in ErrChk.Matches(line)) Add(int.Parse(m.Groups[1].Value), rel);
                foreach (Match m in ACode.Matches(line))  Add(int.Parse(m.Groups[1].Value), rel);
                foreach (Match m in Chk.Matches(line))
                {
                    var grp = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    foreach (var tok in grp.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        if (int.TryParse(tok, out var c)) Add(c, rel);
                }
                foreach (Match m in Chks.Matches(line))
                {
                    var text = (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                               .Replace("''", "'");
                    if (msgToCode.TryGetValue(Normalize(text), out var c)) Add(c, rel);
                }
            }
        }

        // Golden cases: codes named in *.expected listings.
        foreach (var file in Directory.EnumerateFiles(casesRoot, "*.expected", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(casesRoot, file);
            foreach (Match m in ErrorLine.Matches(File.ReadAllText(file)))
                Add(int.Parse(m.Groups[1].Value), rel);
        }

        return hits;
    }

    /// <summary>Fold SPITBOL continuation lines (leading '+' or '.') into the
    /// preceding logical line, so an assertion split across lines is matched.</summary>
    private static IEnumerable<string> LogicalLines(string[] raw)
    {
        var buf = new StringBuilder();
        foreach (var l in raw)
        {
            if (l.Length > 0 && (l[0] == '+' || l[0] == '.') && buf.Length > 0)
                buf.Append(' ').Append(l.AsSpan(1));
            else
            {
                if (buf.Length > 0) { yield return buf.ToString(); buf.Clear(); }
                buf.Append(l);
            }
        }
        if (buf.Length > 0) yield return buf.ToString();
    }

    // -------------------------------------------------------------- report

    private static string BuildReport(
        SortedDictionary<int, string> defined,
        SortedDictionary<int, SortedSet<string>> asserted)
    {
        var sb = new StringBuilder();
        var coveredSet = asserted.Keys.Where(defined.ContainsKey).ToHashSet();
        var uncovered = defined.Keys.Where(c => !coveredSet.Contains(c)).ToList();

        // Assertions for codes the build does NOT define -> stale/misnumbered tests.
        var orphans = asserted.Keys.Where(c => !defined.ContainsKey(c)).ToList();

        sb.AppendLine("# SPITBOL error-code coverage");
        sb.AppendLine();
        sb.AppendLine($"- Codes defined by sbl.min : {defined.Count}");
        sb.AppendLine($"- Codes elicited by tests  : {coveredSet.Count}");
        sb.AppendLine($"- Codes NOT elicited       : {uncovered.Count}");
        if (orphans.Count > 0)
            sb.AppendLine($"- Assertions on UNDEFINED codes (review) : {string.Join(", ", orphans)}");
        sb.AppendLine();

        sb.AppendLine("## Unelicited codes");
        sb.AppendLine();
        foreach (var c in uncovered)
        {
            var note = ReasonHint.TryGetValue(c, out var r) ? $"  [{r}]" : "";
            sb.AppendLine($"- {c}: {defined[c]}{note}");
        }
        sb.AppendLine();

        sb.AppendLine("## Elicited codes");
        sb.AppendLine();
        foreach (var c in coveredSet.OrderBy(x => x))
            sb.AppendLine($"- {c}: {defined[c]}");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>Durable record of WHY specific codes cannot be elicited on this
    /// build, established by probing the reference interpreter. Annotates the
    /// unelicited list so the report distinguishes "untested" from
    /// "untestable". Update as findings change.</summary>
    private static readonly Dictionary<int, string> ReasonHint = new()
    {
        // genuinely undefined in build (no err/erb)
        [299] = "undefined in build", [300] = "undefined in build", [325] = "undefined in build",
        // hardware / OS fault injection required
        [95]  = "fault-injection", [99]  = "fault-injection", [100] = "fault-injection",
        [161] = "fault-injection (open failure maps to statement failure)",
        [176] = "fault-injection", [202] = "fault-injection", [204] = "fault-injection",
        [206] = "fault-injection", [207] = "fault-injection (device variant)",
        [250] = "fault-injection", [252] = "fault-injection", [297] = "fault-injection",
        [320] = "user interrupt (SIGINT)",
        // platform-dependent
        [254] = "HOST platform-dependent", [255] = "HOST platform-dependent",
        // need a real external library to load/call
        [143] = "needs external DLL", [265] = "needs external DLL", [298] = "needs external DLL",
        [326] = "needs external DLL", [327] = "needs external DLL", [328] = "needs external DLL",
        // handlers exist but functions not registered as callable on this build
        [269] = "BUFFER not callable", [270] = "BUFFER not callable", [271] = "BUFFER not callable",
        [272] = "BUFFER not callable", [273] = "BUFFER not callable",
        [275] = "APPEND not callable", [276] = "APPEND not callable",
        [277] = "INSERT not callable", [278] = "INSERT not callable", [279] = "INSERT not callable",
        [280] = "INSERT not callable",
        // do not fire on this build (probed)
        [19]  = "auto-promotes to real; never fires",
        [190] = "numeric first arg accepted; never fires",
        [198] = "numeric first arg accepted; never fires",
        [197] = "never fires on this build",
        [310] = "tan returns finite; never fires",
        [322] = "cos reduces via libc; never fires", [323] = "sin reduces via libc; never fires",
        [267] = "real exponent accepted; never fires",
        [283] = "long strings accepted; never fires",
        // compile-time syntax errors (need a compile-failure harness, not runtime traps)
        [7] = "compile-time", [212] = "compile-time", [213] = "compile-time", [214] = "compile-time",
        [215] = "compile-time", [216] = "compile-time", [217] = "compile-time", [218] = "compile-time",
        [219] = "compile-time", [220] = "compile-time", [221] = "compile-time", [222] = "compile-time",
        [223] = "compile-time", [224] = "compile-time", [225] = "compile-time", [226] = "compile-time",
        [227] = "compile-time", [228] = "compile-time", [229] = "compile-time", [230] = "compile-time",
        [231] = "compile-time", [232] = "compile-time", [233] = "compile-time", [234] = "compile-time",
        [247] = "compile-time", [284] = "compile-time (INCLUDE nesting)", [285] = "compile-time (INCLUDE open)",
    };
}
