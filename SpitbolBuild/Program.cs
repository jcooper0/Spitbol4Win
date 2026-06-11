// ============================================================
//  SpitbolBuild -- MINIMAL/NASM -> MASM build tool.
//
//  Default run (no args, or given the working folder / sbl.min path):
//  requires all three sources to be present in the working directory and
//  converts them, writing the MASM output into a `runtime` subfolder:
//
//      working/sbl.min  -> working/sbl.lex, working/sbl.asm   (intermediates)
//                       -> runtime/sbl_masm.asm               (Nasm2Masm + masm.h)
//      working/err.asm  -> runtime/err_masm.asm               (Nasm2Masm + masm.h)
//      working/int.asm  -> runtime/int_masm.asm               (IntPrep + Nasm2Masm + int_masm.h)
//
//  If any of sbl.min / err.asm / int.asm is missing, it errors and stops.
//  (sbl.asm is GENERATED from sbl.min; it is not a required input.)
//
//  Single-file modes remain available:
//      SpitbolBuild --asm <file.asm> [out-dir] [--int]
//
//  Then on Windows:  ml64 /c /Fo <base>.obj runtime\<base>_masm.asm
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace SpitbolTools;

public static class Program
{
    public static int Main(string[] args)
    {
        string? input = null, outDir = null, headerPath = null, asmPath = null;
        bool isInt = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--header" or "/header":
                    if (++i >= args.Length) { Console.Error.WriteLine("error: --header needs a path"); return 1; }
                    headerPath = args[i]; break;
                case "--asm" or "/asm":
                    if (++i >= args.Length) { Console.Error.WriteLine("error: --asm needs a path"); return 1; }
                    asmPath = args[i]; break;
                case "--int" or "/int":
                    isInt = true; break;
                default:
                    if (input == null && asmPath == null) input = args[i];
                    else if (outDir == null) outDir = args[i];
                    break;
            }
        }

        try
        {
            if (asmPath != null) return ConvertNasmFile(asmPath, outDir, isInt);   // single file
            return BuildRuntime(input, headerPath);                               // default: all three
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    // Require sbl.min + err.asm + int.asm in the working directory; convert all
    // three; place the *_masm.asm outputs in a `runtime` subfolder.
    private static int BuildRuntime(string? input, string? headerPath)
    {
        string workDir =
            input == null                ? Directory.GetCurrentDirectory()
          : Directory.Exists(input)      ? input
          : File.Exists(input)           ? (Path.GetDirectoryName(Path.GetFullPath(input)) ?? ".")
          : null!;
        if (workDir == null) { Console.Error.WriteLine($"error: not found: {input}"); return 1; }

        string minPath = Path.Combine(workDir, "sbl.min");
        string errPath = Path.Combine(workDir, "err.asm");
        string intPath = Path.Combine(workDir, "int.asm");
        string dclPath = Path.Combine(workDir, "int.dcl");

        // All four required -- report every missing one, then stop.
        // (int.dcl is include()'d into sbl.asm by the MINIMAL generator; it
        // supplies calltab, the int math helpers, mxcsr and the data exports.)
        var missing = new List<string>();
        if (!File.Exists(minPath)) missing.Add("sbl.min");
        if (!File.Exists(errPath)) missing.Add("err.asm");
        if (!File.Exists(intPath)) missing.Add("int.asm");
        if (!File.Exists(dclPath)) missing.Add("int.dcl");
        if (missing.Count > 0)
        {
            Console.Error.WriteLine($"error: missing required source file(s) in {workDir}: {string.Join(", ", missing)}");
            Console.Error.WriteLine("       expected sbl.min, err.asm, int.asm and int.dcl together in the working directory.");
            return 1;
        }

        string runtimeDir = Path.Combine(workDir, "runtime");
        Directory.CreateDirectory(runtimeDir);

        // sbl.min -> sbl.lex, sbl.asm (in workDir) -> runtime/sbl_masm.asm
        BuildCore(minPath, workDir, runtimeDir, headerPath);
        Console.WriteLine();
        ConvertNasmFile(errPath, runtimeDir, isInt: false);   // -> runtime/err_masm.asm
        ConvertNasmFile(intPath, runtimeDir, isInt: true);    // -> runtime/int_masm.asm

        Console.WriteLine($"\nall three converted -> {runtimeDir}");
        return 0;
    }

    // ---- raw NASM file (err.asm / int.asm) -> MASM, into outDir ----
    private static int ConvertNasmFile(string asmPath, string? outDir, bool isInt)
    {
        if (!File.Exists(asmPath)) { Console.Error.WriteLine($"error: not found: {asmPath}"); return 1; }
        outDir ??= Path.GetDirectoryName(Path.GetFullPath(asmPath)) ?? ".";
        Directory.CreateDirectory(outDir);

        string baseName = Path.GetFileNameWithoutExtension(asmPath);
        string outPath = Path.Combine(outDir, baseName + "_masm.asm");

        string raw = File.ReadAllText(asmPath);
        if (isInt) raw = IntPrep.Preprocess(raw);                 // int.asm preprocessing
        string body = Nasm2Masm.Convert(raw);
        string header = LoadHeader(null, isInt ? "int_masm.h" : "masm.h", out string headerSrc);

        Console.WriteLine($"convert {Path.GetFileName(asmPath)}{(isInt ? " (int prep)" : "")} + {headerSrc} -> runtime\\{Path.GetFileName(outPath)}");
        WriteWithHeader(outPath, header, body);
        Console.WriteLine($"    ml64 /c /Fo {baseName}.obj \"{outPath}\"");
        return 0;
    }

    // ---- compiler core: sbl.min -> sbl_masm.asm ----
    //  intermediates (sbl.lex, sbl.asm) go to interDir; sbl_masm.asm to outDir.
    private static void BuildCore(string inPath, string interDir, string outDir, string? headerPath)
    {
        string lexPath  = Path.Combine(interDir, "sbl.lex");
        string nasmPath = Path.Combine(interDir, "sbl.asm");
        string masmPath = Path.Combine(outDir,  "sbl_masm.asm");
        var utf8 = new UTF8Encoding(false);

        Console.WriteLine($"[1/4] lex       sbl.min -> sbl.lex");
        using (var rdr = new StreamReader(inPath))
        using (var wtr = new StreamWriter(lexPath, false, utf8))
            new MinimalLexer(wtr).Run(rdr);

        Console.WriteLine($"[2/4] codegen   sbl.lex -> sbl.asm");
        using (var rdr = new StreamReader(lexPath))
        using (var wtr = new StreamWriter(nasmPath, false, utf8))
            new MinimalCodeGen(wtr).Run(rdr);

        // The MINIMAL generator (asm.sbl) does include('int.dcl'); splice it in
        // so calltab / the int math helpers / mxcsr / typet / data exports are
        // part of the sbl module (their `global` directives export sbl's data).
        string dclPath = Path.Combine(interDir, "int.dcl");
        string nasm = File.ReadAllText(nasmPath);
        if (File.Exists(dclPath))
        {
            nasm = IntPrep.SpliceIntDcl(nasm, File.ReadAllText(dclPath));
            File.WriteAllText(nasmPath, nasm, utf8);   // keep sbl.asm self-consistent
            Console.WriteLine($"        + int.dcl spliced (calltab, do_dvi/rmi, mxcsr, typet, exports)");
        }

        Console.WriteLine($"[3/4] nasm2masm sbl.asm -> (in memory)");
        string masmBody = Nasm2Masm.Convert(nasm);

        string header = LoadHeader(headerPath, "masm.h", out string headerSrc);
        Console.WriteLine($"[4/4] prepend   {headerSrc} + body -> runtime\\sbl_masm.asm");
        WriteWithHeader(masmPath, header, masmBody);
        Console.WriteLine($"    ml64 /c /Fo sbl.obj \"{masmPath}\"");
    }

    private static void WriteWithHeader(string path, string header, string body)
    {
        using var wtr = new StreamWriter(path, false, new UTF8Encoding(false));
        wtr.Write(header); if (!header.EndsWith("\n")) wtr.Write('\n');
        wtr.Write(body);   if (!body.EndsWith("\n"))   wtr.Write('\n');
    }

    private static string LoadHeader(string? headerPath, string embeddedName, out string source)
    {
        if (headerPath != null)
        {
            if (!File.Exists(headerPath)) throw new FileNotFoundException($"header not found: {headerPath}");
            source = Path.GetFileName(headerPath) + " (external)";
            return File.ReadAllText(headerPath);
        }
        var asm = Assembly.GetExecutingAssembly();
        string? res = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith(embeddedName));
        if (res == null) throw new FileNotFoundException($"{embeddedName} is not embedded in this build");
        using var s = asm.GetManifestResourceStream(res)!;
        using var r = new StreamReader(s);
        source = embeddedName + " (embedded)";
        return r.ReadToEnd();
    }
}
