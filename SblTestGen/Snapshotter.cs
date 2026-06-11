using System.Text;
using System.Text.RegularExpressions;

namespace SblTestGen;

// Port of snapshot.py. Run each generated .sbl; where it reports *FAIL, rewrite
// the matching assertion to the observed native value (so the case is green as a
// port regression test) and record the C#/native divergence in DIVERGENCES.md.
static class Snapshotter
{
    static readonly HashSet<string> Hand = new()
        { "array.sbl", "table.sbl", "item.sbl", "prototype.sbl", "sort.sbl", "rsort.sbl" };

    static readonly Regex ValRe = new(@"\*FAIL:\s+(.*?)\s+obs\[(.*?)\]\s+exp\[(.*)\]\s*$");
    static readonly Regex ErrRe = new(@"\*FAIL:\s+(.*?)\s+&errtype=\[(.*?)\]\s+expected\s+\[(.*)\]");
    static readonly Regex NfRe  = new(@"\*FAIL:\s+(.*?)\s+no error trapped");

    public static void Run(string outDir, string exe)
    {
        var report = new List<(string f, string tag, string kind, string cs, string nat)>();

        var files = Directory.EnumerateFiles(outDir, "*.sbl")
                             .OrderBy(p => p, StringComparer.Ordinal).ToList();
        foreach (var path in files)
        {
            var f = Path.GetFileName(path);
            if (Hand.Contains(f)) continue;

            var outp = Util.RunSbl(exe, outDir, f, 60);
            var fails = outp.Replace("\r", "").Split('\n').Where(l => l.Contains("*FAIL")).ToList();
            if (fails.Count == 0) continue;

            var lines = File.ReadAllText(path).Split('\n').ToList();
            int patched = 0;

            foreach (var fl in fails)
            {
                var m = ValRe.Match(fl);
                if (m.Success)
                {
                    string tag = m.Groups[1].Value, obs = m.Groups[2].Value, exp = m.Groups[3].Value;
                    string key = $"vchk('{tag}'";
                    var rx = new Regex(@"(vchk\('" + Regex.Escape(tag) + @"',[^,]*,\s*).*?(\)\s*)$");
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (!lines[i].Contains(key)) continue;
                        var nw = rx.Replace(lines[i], mm => mm.Groups[1].Value + Util.Quote(obs, false) + mm.Groups[2].Value);
                        if (nw != lines[i])
                        {
                            lines[i] = $"*  DIVERGENCE: C# expected [{exp}]; native value below.\n" + nw;
                            patched++;
                            report.Add((f, tag, "value", $"C#=[{exp}]", $"native=[{obs}]"));
                        }
                        break;
                    }
                    continue;
                }

                m = ErrRe.Match(fl);
                if (m.Success)
                {
                    string tag = m.Groups[1].Value, obs = m.Groups[2].Value, exp = m.Groups[3].Value;
                    string key = $"errchk('{tag}'";
                    var rx = new Regex(@"(errchk\('" + Regex.Escape(tag) + @"',\s*)\d+(\s*\))");
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (!lines[i].Contains(key)) continue;
                        var nw = rx.Replace(lines[i], mm => mm.Groups[1].Value + obs + mm.Groups[2].Value);
                        if (nw != lines[i])
                        {
                            lines[i] = $"*  DIVERGENCE: C# expected &errtype {exp}; native raises {obs}.\n" + nw;
                            patched++;
                            report.Add((f, tag, "errcode", $"C#={exp}", $"native={obs}"));
                        }
                        break;
                    }
                    continue;
                }

                m = NfRe.Match(fl);
                if (m.Success)
                {
                    string tag = m.Groups[1].Value;
                    string key = $"errchk('{tag}'";
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (!lines[i].Contains(key)) continue;
                        var cm = Regex.Match(lines[i], @"errchk\('" + Regex.Escape(tag) + @"',\s*(\d+)");
                        var cexp = cm.Success ? cm.Groups[1].Value : "?";
                        lines[i] = $"*  DIVERGENCE: C# expected &errtype {cexp}; native raises no error here.\n        okchk('{tag}')";
                        patched++;
                        report.Add((f, tag, "no-error", $"C#={cexp}", "native=none"));
                        break;
                    }
                    continue;
                }
            }

            File.WriteAllText(path, string.Join("\n", lines));
            Console.WriteLine($"patched {patched,2} in {f}");
        }

        var sb = new StringBuilder();
        sb.Append("# C# (Snobol4.Net) vs native SPITBOL divergences\n\n");
        sb.Append("Each row: the ported case asserts the **native** value (so the suite is\n");
        sb.Append("green against the Windows port); the C# port expected something different.\n\n");
        sb.Append("| file | test | kind | C# expected | native |\n|---|---|---|---|---|\n");
        foreach (var r in report)
            sb.Append($"| {r.f} | {r.tag} | {r.kind} | {r.cs} | {r.nat} |\n");
        File.WriteAllText(Path.Combine(outDir, "DIVERGENCES.md"), sb.ToString());

        Console.WriteLine($"\nTOTAL divergences snapshotted: {report.Count}");
    }
}
