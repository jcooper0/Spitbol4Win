using System.Text;
using System.Text.RegularExpressions;

namespace SblTestGen;

// Port of convert.py. For each C# test class, extract each method's embedded
// SNOBOL snippet, uniquify its labels for per-class combining, redirect :(end)
// to a per-block done label, compile-probe it, and emit okchk/errchk/vchk per
// the C# assertions. define()-using methods become standalone files.
static class Converter
{
    enum Kind { Ok, Err, Val }
    readonly record struct Assertion(Kind Kind, string A, string B, bool Numeric);

    // Classes not portable to native SPITBOL (skipped whole-file).
    static readonly HashSet<string> Skip = new()
    {
        "FunctionControl/Load.cs", "FunctionControl/Unload.cs", "FunctionControl/LoadTests.cs",
        "FunctionControl/LoadXnTests.cs", "FunctionControl/LoadSpecTests.cs",
        "FunctionControl/LoadAutoPrototypeTests.cs", "FunctionControl/LoadObjectLifecycleTests.cs",
        "FunctionControl/ExtCreateTests.cs", "FunctionControl/ExtXnblkTests.cs",
        "FunctionControl/ExtNoconvTests.cs", "FunctionControl/FSharpOptionTests.cs",
        "FunctionControl/VbLibraryTests.cs",
        "Memory/Dump.cs", "Memory/Collect.cs", "Miscellaneous/Time.cs", "Miscellaneous/Date .cs",
        "ArraysTables/Array.cs", "ArraysTables/Table.cs", "ArraysTables/Item.cs",
        "ArraysTables/Prototype.cs", "ArraysTables/Sort.cs", "ArraysTables/Rsort.cs",
        "ArraysTables/Compare.cs",
    };
    static readonly HashSet<string> SkipDirs = new() { "InputOutput" };
    static readonly HashSet<string> Special = new()
        { "end", "return", "freturn", "nreturn", "continue", "abort", "fail", "fret", "nret" };

    static readonly Regex MethodRe   = new(@"public\s+void\s+(\w+)\s*\(\s*\)\s*\{", RegexOptions.Singleline);
    static readonly Regex SnipRe     = new(@"var\s+s\s*=\s*@""(.*?)""\s*;", RegexOptions.Singleline);
    static readonly Regex OkRe       = new(@"Assert\.AreEqual\(\s*0\s*,\s*build\.ErrorCodeHistory\.Count\s*\)");
    static readonly Regex ErrRe      = new(@"Assert\.AreEqual\(\s*(\d+)\s*,\s*build\.ErrorCodeHistory\[0\]\s*\)");
    static readonly Regex ValRe      = new(@"Assert\.AreEqual\(\s*""((?:[^""\\]|\\.)*)""\s*,\s*\(\([A-Za-z]+Var\)\s*build\.Execute!?\.IdentifierTable\[\s*build\.FoldCase\(\s*""([^""]+)""\s*\)\s*\]\s*\)\.\w+\s*\)");
    static readonly Regex IncRe      = new(@"Assert\.Inconclusive");
    static readonly Regex ValTsRe    = new(@"Assert\.AreEqual\(\s*""((?:[^""\\]|\\.)*)""\s*,\s*build\.Execute!?\.IdentifierTable\[\s*build\.FoldCase\(\s*""([^""]+)""\s*\)\s*\]\.ToString\(\)\s*\)");
    static readonly Regex ValNumRe   = new(@"Assert\.AreEqual\(\s*(-?\d+(?:\.\d+)?)\s*,\s*\(\((?:Integer|Real)Var\)\s*build\.Execute!?\.IdentifierTable\[\s*build\.FoldCase\(\s*""([^""]+)""\s*\)\s*\]\s*\)\.\w+\s*\)");
    static readonly Regex GotoFieldRe  = new(@":(\s*(?:[SsFf]?\([A-Za-z$][A-Za-z0-9_.]*\))+\s*)$");
    static readonly Regex AssertIterRe = new(@"Assert\.\w+\([^;]*\)\s*;", RegexOptions.Singleline);
    static readonly Regex LabelStartRe = new(@"^([A-Za-z][A-Za-z0-9_.]*)");
    static readonly Regex LabelLineRe  = new(@"^([A-Za-z][A-Za-z0-9_.]*)(\s|$)");
    static readonly Regex GotoLabelRe  = new(@"\(([A-Za-z][A-Za-z0-9_.]*)\)");
    static readonly Regex NonAlnum     = new("[^a-z0-9]");

    static string _exe = "", _work = "";

    public static void Run(string srcDir, string outDir, string exe, string workDir)
    {
        _exe = exe; _work = workDir;
        Directory.CreateDirectory(outDir);

        var files = Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
                             .OrderBy(p => p, StringComparer.Ordinal).ToList();
        var summary = new List<(string fn, int meth, int skip, int unh, int sa)>();
        int standaloneCount = 0;
        var compileErr = new List<string>();

        foreach (var path in files)
        {
            var rel = Path.GetRelativePath(srcDir, path).Replace('\\', '/');
            if (Skip.Contains(rel) || SkipDirs.Contains(rel.Split('/')[0])) continue;

            var src = File.ReadAllText(path);
            var cls = Path.GetFileNameWithoutExtension(path).Trim();

            var blocks = new List<string>();
            var standalone = new List<(string nm, string sa)>();
            int nmeth = 0, nskip = 0, nunh = 0, idx = 0;

            foreach (var (name, body) in SplitMethods(src))
            {
                var sm = SnipRe.Match(body);
                if (!sm.Success) continue;
                var snip = sm.Groups[1].Value;

                var (asserts, unh) = ParseAsserts(body);
                if (asserts is null) { nskip++; continue; }

                nunh += unh;
                var lines = SnippetLines(snip);
                idx++;
                if (!CompileOk(lines)) { compileErr.Add($"{cls}.{name}"); continue; }

                if (snip.ToLowerInvariant().Contains("define("))
                    standalone.Add((name, RewriteBlock(name, lines, asserts, "", "qzdone")));
                else
                {
                    blocks.Add(RewriteBlock(name, lines, asserts, $"m{idx}_", $"m{idx}_z"));
                    nmeth++;
                }
            }

            if (blocks.Count > 0)
            {
                var fn = NonAlnum.Replace(cls.ToLowerInvariant(), "") + ".sbl";
                var sb = new StringBuilder();
                sb.Append("-INCLUDE 'assert.inc'\n");
                sb.Append($"\n*  Ported from {cls}.cs (Snobol4.Net C# test suite).\n");
                foreach (var b in blocks) sb.Append(b + "\n");
                sb.Append("\nEND\n");
                File.WriteAllText(Path.Combine(outDir, fn), sb.ToString());
                summary.Add((fn, nmeth, nskip, nunh, standalone.Count));
            }

            foreach (var (nm, sa) in standalone)
            {
                standaloneCount++;
                var saFn = NonAlnum.Replace(cls.ToLowerInvariant(), "") + "_" + nm.ToLowerInvariant() + ".sbl";
                File.WriteAllText(Path.Combine(outDir, saFn), "-INCLUDE 'assert.inc'\n\n" + sa + "\nEND\n");
            }
        }

        File.WriteAllText(Path.Combine(outDir, "compile_excluded.txt"), string.Join("\n", compileErr));
        Console.WriteLine($"compile-time-error methods excluded: {compileErr.Count}");
        Console.WriteLine($"{"file",-20} meth skip unh standalone");
        foreach (var s in summary)
            Console.WriteLine($"{s.fn,-20} {s.meth,4} {s.skip,4} {s.unh,3} {s.sa,4}");
        Console.WriteLine(
            $"TOTAL class-files: {summary.Count}  methods: {summary.Sum(s => s.meth)}" +
            $"  inconclusive-skipped: {summary.Sum(s => s.skip)}" +
            $"  unhandled-asserts: {summary.Sum(s => s.unh)}" +
            $"  standalone-files: {standaloneCount}");
    }

    static List<(string name, string body)> SplitMethods(string src)
    {
        var outl = new List<(string, string)>();
        foreach (Match m in MethodRe.Matches(src))
        {
            var name = m.Groups[1].Value;
            int start = m.Index + m.Length, depth = 1, i = start;
            while (i < src.Length && depth > 0)
            {
                char c = src[i];
                if (c == '{') depth++;
                else if (c == '}') depth--;
                i++;
            }
            outl.Add((name, src.Substring(start, (i - 1) - start)));
        }
        return outl;
    }

    // Returns (null, 0) to signal the whole method should be skipped
    // (an Assert.Inconclusive is present).
    static (List<Assertion>?, int) ParseAsserts(string body)
    {
        var res = new List<Assertion>();
        int unh = 0;
        if (IncRe.IsMatch(body)) return (null, 0);

        foreach (Match am in AssertIterRe.Matches(body))
        {
            var a = am.Value;
            if (OkRe.IsMatch(a)) { res.Add(new(Kind.Ok, "", "", false)); continue; }

            var m = ErrRe.Match(a);
            if (m.Success) { res.Add(new(Kind.Err, m.Groups[1].Value, "", false)); continue; }

            m = ValRe.Match(a);
            if (m.Success) { res.Add(new(Kind.Val, m.Groups[2].Value, m.Groups[1].Value, false)); continue; }

            m = ValTsRe.Match(a);
            if (m.Success) { res.Add(new(Kind.Val, m.Groups[2].Value, m.Groups[1].Value, false)); continue; }

            m = ValNumRe.Match(a);
            if (m.Success) { res.Add(new(Kind.Val, m.Groups[2].Value, m.Groups[1].Value, true)); continue; }

            if (a.Contains("AreNotEqual") && a.Contains("ErrorCodeHistory")) continue;
            unh++;
        }
        return (res, unh);
    }

    static List<string> SnippetLines(string snip)
    {
        var lines = snip.Replace("\r", "").Split('\n').ToList();
        while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    // A method compiles iff the sentinel reaches output; if a constant-argument
    // error (or any compile error) aborts compilation, the sentinel never runs.
    static bool CompileOk(List<string> lines)
    {
        var prog = "        output = '@@COMPILED'\n" + string.Join("\n", lines) + "\n";
        File.WriteAllText(Path.Combine(_work, "_probe.sbl"), prog);
        return Util.RunSbl(_exe, _work, "_probe.sbl", 20).Contains("@@COMPILED");
    }

    static HashSet<string> CollectLabels(List<string> lines)
    {
        var labels = new HashSet<string>();
        foreach (var ln in lines)
        {
            if (ln.Length == 0) continue;
            char c = ln[0];
            if (c is ' ' or '\t' or '*' or '-' or '+' or '.' or ';') continue;
            var m = LabelStartRe.Match(ln);
            if (m.Success && !Special.Contains(m.Groups[1].Value.ToLowerInvariant()))
                labels.Add(m.Groups[1].Value);
        }
        return labels;
    }

    static string RewriteBlock(string name, List<string> lines, List<Assertion> asserts, string prefix, string done)
    {
        var rmap = new Dictionary<string, string>();
        if (prefix.Length > 0)
            foreach (var l in CollectLabels(lines)) rmap[l] = prefix + l;

        var outl = new List<string> { "", $"* ===== {name} =====" };
        if (prefix.Length > 0)
        {
            // clear() resets natural variables but NOT keywords, so reset the
            // ones that survive and affect later blocks: &anchor back to its
            // fresh-program default (0), and &errlimit back to the trap budget
            // (assert.inc sets it only once at load). Each C# test is a fresh
            // program, so a block that needs anchored matching sets &anchor=1
            // itself, after this reset.
            outl.Add("        clear()");
            outl.Add("        &anchor = 0");
            outl.Add("        &errlimit = 1000000");
        }
        outl.Add("        caughtf =");

        foreach (var raw in lines)
        {
            var ln = raw;
            if (ln.Length > 0 && !char.IsWhiteSpace(ln[0]) && ln.Trim().ToLowerInvariant() == "end") continue;

            var m = LabelLineRe.Match(ln);
            if (m.Success && ln.Length > 0 && ln[0] != ' ' && ln[0] != '\t' && rmap.ContainsKey(m.Groups[1].Value))
                ln = rmap[m.Groups[1].Value] + ln.Substring(m.Groups[1].Value.Length);

            var gm = GotoFieldRe.Match(ln);
            if (gm.Success)
            {
                var field = GotoLabelRe.Replace(gm.Groups[1].Value, mm =>
                {
                    var lab = mm.Groups[1].Value;
                    if (lab.ToLowerInvariant() == "end") return "(" + done + ")";
                    if (rmap.TryGetValue(lab, out var v)) return "(" + v + ")";
                    return mm.Value;
                });
                ln = ln.Substring(0, gm.Index) + ":" + field;
            }
            outl.Add(ln);
        }

        var asl = new List<string>();
        foreach (var a in asserts)
        {
            switch (a.Kind)
            {
                case Kind.Ok: asl.Add($"okchk('{name}')"); break;
                case Kind.Err: asl.Add($"errchk('{name}', {a.A})"); break;
                case Kind.Val:
                    var want = a.Numeric ? a.B : Util.Quote(a.B, true);
                    asl.Add($"vchk('{name} {a.A}', $'{a.A}', {want})");
                    break;
            }
        }
        if (asl.Count == 0) asl.Add($"okchk('{name}')");

        outl.Add($"{done,-7} {asl[0]}");
        for (int i = 1; i < asl.Count; i++) outl.Add("        " + asl[i]);
        return string.Join("\n", outl);
    }
}
