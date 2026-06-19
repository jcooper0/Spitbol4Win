using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SblTestGen;

/// <summary>
/// `coverage` subcommand: cross-references the error codes DEFINED by the
/// interpreter source (sbl.min) against the codes ASSERTED by the test corpus,
/// and classifies every code into one of five buckets:
///   tested        - a corpus case elicits it
///   untested-gap  - defined and reachable, but no test yet (actionable)
///   unreachable   - defined, but no code path can fire it on this build
///   not-testable  - reachable only under conditions a harness can't create
///                   (OS/device fault injection, SIGINT, OOM, external DLL, ...)
///   reserved      - number not defined in sbl.min (a numbering gap)
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
    public enum CodeClass { Tested, UntestedGap, Unreachable, NotTestable, Reserved }

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
                foreach (Match m in ACode.Matches(line)) Add(int.Parse(m.Groups[1].Value), rel);
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

    // ----------------------------------------------------------- classification

    private sealed record ClassNote(CodeClass Class, string Reason);

    /// <summary>Durable record of WHY specific DEFINED codes are not elicited,
    /// established by probing the reference interpreter. Codes elicited by the
    /// corpus are 'tested' regardless of any entry here; codes absent from
    /// sbl.min are 'reserved'; defined+reachable codes not listed here and not
    /// elicited surface as 'untested-gap'. Update as findings change.</summary>
    private static readonly Dictionary<int, ClassNote> Curated = new()
    {
        // --- unreachable: no code path fires these on this build (probed) ---
        [19] = new(CodeClass.Unreachable, "auto-promotes to real; never fires"),
        [190] = new(CodeClass.Unreachable, "numeric first arg accepted; never fires"),
        [197] = new(CodeClass.Unreachable, "never fires on this build"),
        [198] = new(CodeClass.Unreachable, "numeric first arg accepted; never fires"),
        [267] = new(CodeClass.Unreachable, "real exponent accepted; never fires"),
        [283] = new(CodeClass.Unreachable, "long strings accepted; never fires"),
        [310] = new(CodeClass.Unreachable, "tan returns finite; never fires"),
        [322] = new(CodeClass.Unreachable, "cos reduces via libc; never fires"),
        [323] = new(CodeClass.Unreachable, "sin reduces via libc; never fires"),
        // handlers exist in sbl.min but the functions are not registered callable
        [269] = new(CodeClass.Unreachable, "BUFFER not callable"),
        [270] = new(CodeClass.Unreachable, "BUFFER not callable"),
        [271] = new(CodeClass.Unreachable, "BUFFER not callable"),
        [272] = new(CodeClass.Unreachable, "BUFFER not callable"),
        [273] = new(CodeClass.Unreachable, "BUFFER not callable"),
        [275] = new(CodeClass.Unreachable, "APPEND not callable"),
        [276] = new(CodeClass.Unreachable, "APPEND not callable"),
        [277] = new(CodeClass.Unreachable, "INSERT not callable"),
        [278] = new(CodeClass.Unreachable, "INSERT not callable"),
        [279] = new(CodeClass.Unreachable, "INSERT not callable"),
        [280] = new(CodeClass.Unreachable, "INSERT not callable"),

        // --- not-testable: reachable only under conditions a harness can't make ---
        // OS / device fault injection
        [95] = new(CodeClass.NotTestable, "fault-injection"),
        [99] = new(CodeClass.NotTestable, "fault-injection"),
        [100] = new(CodeClass.NotTestable, "fault-injection"),
        [161] = new(CodeClass.NotTestable, "fault-injection (open failure maps to statement failure)"),
        [176] = new(CodeClass.NotTestable, "fault-injection"),
        [202] = new(CodeClass.NotTestable, "fault-injection"),
        [204] = new(CodeClass.NotTestable, "fault-injection (out of memory)"),
        [206] = new(CodeClass.NotTestable, "fault-injection"),
        [207] = new(CodeClass.NotTestable, "fault-injection (device variant; 207 also elicited by IoFixtures)"),
        [250] = new(CodeClass.NotTestable, "fault-injection (dump OOM)"),
        [252] = new(CodeClass.NotTestable, "fault-injection"),
        [297] = new(CodeClass.NotTestable, "fault-injection"),
        [320] = new(CodeClass.NotTestable, "user interrupt (SIGINT)"),
        // platform-dependent
        [254] = new(CodeClass.NotTestable, "HOST platform-dependent"),
        [255] = new(CodeClass.NotTestable, "HOST platform-dependent"),
        // require a real external library to load/call (elicited only with fixtures present)
        [143] = new(CodeClass.NotTestable, "needs external DLL (load input error during load)"),
        [265] = new(CodeClass.NotTestable, "needs external DLL"),
        [298] = new(CodeClass.NotTestable, "needs external DLL"),
        [326] = new(CodeClass.NotTestable, "needs external DLL"),
        [327] = new(CodeClass.NotTestable, "needs external DLL"),
        [328] = new(CodeClass.NotTestable, "needs external DLL"),
        // intentionally excluded
        [216] = new(CodeClass.NotTestable, "eNd valid under case-folding; missing-END abort is unnumbered"),
    };

    private static CodeClass Classify(
        int code, IReadOnlyDictionary<int, string> defined, IReadOnlySet<int> covered, out string reason)
    {
        reason = "";
        if (!defined.ContainsKey(code)) return CodeClass.Reserved;
        if (covered.Contains(code)) return CodeClass.Tested;
        if (Curated.TryGetValue(code, out var note)) { reason = note.Reason; return note.Class; }
        return CodeClass.UntestedGap;
    }

    private static string Label(CodeClass c) => c switch
    {
        CodeClass.Tested => "tested",
        CodeClass.UntestedGap => "untested-gap",
        CodeClass.Unreachable => "unreachable",
        CodeClass.NotTestable => "not-testable",
        CodeClass.Reserved => "reserved",
        _ => "?",
    };

    // -------------------------------------------------------------- report

    private static string BuildReport(
        SortedDictionary<int, string> defined,
        SortedDictionary<int, SortedSet<string>> asserted)
    {
        var covered = asserted.Keys.Where(defined.ContainsKey).ToHashSet();
        var orphans = asserted.Keys.Where(c => !defined.ContainsKey(c)).OrderBy(x => x).ToList();
        int max = defined.Keys.Max();

        var rows = new List<(int code, CodeClass cls, string reason, string msg)>();
        var counts = new Dictionary<CodeClass, int>();
        for (int c = 1; c <= max; c++)
        {
            var cls = Classify(c, defined, covered, out var reason);
            // numbers neither defined nor a real reserved gap don't occur here
            // (sbl.min is dense 1..max apart from the reserved gaps), but if a
            // larger gap ever appears, it is still reported as 'reserved'.
            var msg = defined.TryGetValue(c, out var m) ? m : "(undefined / reserved)";
            rows.Add((c, cls, reason, msg));
            counts[cls] = counts.GetValueOrDefault(cls) + 1;
        }

        var gaps = rows.Where(r => r.cls == CodeClass.UntestedGap).Select(r => r.code).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# SPITBOL error-code coverage & classification");
        sb.AppendLine();
        sb.AppendLine($"- Codes defined by sbl.min : {defined.Count}");
        sb.AppendLine($"- tested                   : {counts.GetValueOrDefault(CodeClass.Tested)}");
        sb.AppendLine($"- untested-gap             : {counts.GetValueOrDefault(CodeClass.UntestedGap)}");
        sb.AppendLine($"- unreachable              : {counts.GetValueOrDefault(CodeClass.Unreachable)}");
        sb.AppendLine($"- not-testable             : {counts.GetValueOrDefault(CodeClass.NotTestable)}");
        sb.AppendLine($"- reserved (numbering gaps): {counts.GetValueOrDefault(CodeClass.Reserved)}");
        if (orphans.Count > 0)
            sb.AppendLine($"- assertions on UNDEFINED codes (review) : {string.Join(", ", orphans)}");
        sb.AppendLine();

        sb.AppendLine("## Untested gaps (actionable: add a test or classify)");
        sb.AppendLine();
        if (gaps.Count == 0)
            sb.AppendLine("_None - every defined, reachable code is tested._");
        else
            foreach (var c in gaps)
                sb.AppendLine($"- {c}: {defined[c]}");
        sb.AppendLine();

        sb.AppendLine("## Full classification");
        sb.AppendLine();
        sb.AppendLine("| Code | Class | Note | Message |");
        sb.AppendLine("|-----:|-------|------|---------|");
        foreach (var (code, cls, reason, msg) in rows)
            sb.AppendLine($"| {code:D3} | {Label(cls)} | {reason} | {msg} |");
        sb.AppendLine();

        return sb.ToString();
    }
}