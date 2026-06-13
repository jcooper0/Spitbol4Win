using System.Diagnostics;

namespace SblTestGen;

// Entry point. Subcommands mirroring the original convert.py / snapshot.py,
// plus a coverage report over the test corpus.
static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0) { Usage(); return 1; }
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "convert":
                    if (args.Length < 4) { Usage(); return 1; }
                    Converter.Run(args[1], args[2], args[3],
                                  args.Length > 4 ? args[4] : Path.GetTempPath());
                    return 0;

                case "snapshot":
                    if (args.Length < 3) { Usage(); return 1; }
                    Snapshotter.Run(args[1], args[2]);
                    return 0;

                case "coverage":
                    if (args.Length < 3) { Usage(); return 1; }
                    Coverage.Run(args[1], args[2],
                                 args.Length > 3 ? args[3] : null);
                    return 0;

                default:
                    Usage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 2;
        }
    }

    static void Usage() => Console.Error.WriteLine(
@"SblTestGen - convert Snobol4.Net C# tests to self-checking SPITBOL .sbl cases.

usage:
  SblTestGen convert <csTestDir> <outDir> <spitbolExe> [workDir]
      Walk <csTestDir> for *.cs, emit one .sbl per class into <outDir>,
      using <spitbolExe> to compile-probe each method (methods that fail to
      compile are excluded and listed in <outDir>\compile_excluded.txt).

  SblTestGen snapshot <outDir> <spitbolExe>
      Run every generated .sbl in <outDir>; wherever it reports *FAIL, rewrite
      the assertion to the observed native value and record the C#/native
      divergence in <outDir>\DIVERGENCES.md.

  SblTestGen coverage <sbl.min> <casesRoot> [reportMd]
      Cross-reference the error codes DEFINED by <sbl.min> against the codes
      ASSERTED by the test corpus under <casesRoot>, and report which are
      elicited, which are not, and (where known) why. Writes Markdown to
      [reportMd] if given, and always echoes it to stdout.");
}

// Helpers shared by both subcommands.
static class Util
{
    // Run <exe> <file> with the working directory set to <workDir> (so
    // -INCLUDE 'assert.inc' and the probe file resolve), capturing combined
    // stdout+stderr. Returns "" on timeout or launch failure. Reads both
    // streams asynchronously to avoid pipe-buffer deadlock.
    public static string RunSbl(string exe, string workDir, string file, int timeoutSec)
    {
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(file);

            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutSec * 1000))
            {
                try { p.Kill(true); } catch { /* ignore */ }
                return "";
            }
            return stdout.Result + stderr.Result;
        }
        catch
        {
            return "";
        }
    }

    // Quote a string as a SPITBOL literal. When deEscape is set, first undo C#
    // source escaping (\" -> ", \\ -> \). Prefer single quotes; fall back to
    // double quotes; if both appear, splice quoted pieces with a "'" literal.
    public static string Quote(string s, bool deEscape)
    {
        if (deEscape) s = s.Replace("\\\"", "\"").Replace("\\\\", "\\");
        if (!s.Contains('\'')) return "'" + s + "'";
        if (!s.Contains('"')) return "\"" + s + "\"";
        return string.Join(" \"'\" ", s.Split('\'').Select(p => "'" + p + "'"));
    }
}
