// ============================================================
//  Nasm2Masm.cs -- post-pass that rewrites a SPITBOL-generated NASM
//  assembly file (sbl.asm) into MASM (ml64) syntax.
//
//  Faithful C# port of nasm2masm.py. Method names and control flow
//  mirror the Python so the two can be diffed and debugged side by side.
//
//  Handles only the syntax a prepended header (masm.h) cannot reach:
//      * extern  X            -> EXTERN X:<type>   (type derived from usage)
//      * global  X            -> PUBLIC X
//      * section/segment .text-> .CODE      .data -> .DATA
//      * section .note.GNU-stack ...        -> dropped (Linux-only)
//      * 0xHHHH               -> 0HHHHh      (MASM hex)
//      * %define-family / BITS / DEFAULT    -> dropped (replaced by masm.h)
//      * %ifdef/%endif/%macro...            -> IFDEF/ENDIF/MACRO...
//      * appends END at EOF
//
//  The abstract macro layer (m_word, registers, m_addr, flags, ...) is
//  NOT touched here -- masm.h defines those and ml64 expands them.
//
//  Build pipeline (mirrors how nasm.h is prepended for the NASM build):
//      Nasm2Masm sbl.asm sbl_masm_body.asm
//      copy /b masm.h + sbl_masm_body.asm  sbl_masm.asm      (Windows)
//
//  Usage:  Nasm2Masm <in.asm> <out.asm>
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SpitbolTools;

public static class Nasm2Masm
{
    // ------------------------------------------------------------
    //  classify_externs: {symbol -> masm_type} for every `extern`,
    //  chosen from how the symbol is actually used in this file --
    //  so it stays correct as sbl.min evolves, with no hand table.
    // ------------------------------------------------------------
    public static Dictionary<string, string> ClassifyExterns(string text)
    {
        var types = new Dictionary<string, string>();

        // distinct extern operands
        var externs = new HashSet<string>();
        foreach (Match m in Regex.Matches(text, @"^\s*extern\s+(\S+)", RegexOptions.Multiline))
            externs.Add(m.Groups[1].Value);

        foreach (var sym in externs)
        {
            string s = Regex.Escape(sym);

            if (Regex.IsMatch(text, @"\bldmxcsr\s*\[" + s + @"\]"))
                types[sym] = "DWORD";                          // ldmxcsr needs m32 (mxcsr_set)
            else if (Regex.IsMatch(text, @"\b(call|jmp)\s+" + s + @"\b"))
                types[sym] = "PROC";                           // called/jumped routine
            else if (Regex.IsMatch(text, @"\bm_char\s*\[" + s + @"\]")
                     && !Regex.IsMatch(text, @"\bm_word\s*\[" + s + @"\]"))
                types[sym] = "BYTE";                           // byte cell (reg_fl)
            else if (Regex.IsMatch(text, @"\[" + s + @"\]"))
                types[sym] = "QWORD";                          // word-sized data cell
            else
                types[sym] = "PROC";                           // routine families / unused
        }

        return types;
    }

    // ------------------------------------------------------------
    //  transform_line: per-line syntactic transform.
    //  Returns the rewritten line, or null to drop the line.
    // ------------------------------------------------------------
    public static string? TransformLine(string line, Dictionary<string, string> types)
    {
        // NASM preprocessor conditionals -> MASM
        line = Regex.Replace(line, @"^(\s*)%ifdef\b",  "$1IFDEF");
        line = Regex.Replace(line, @"^(\s*)%ifndef\b", "$1IFNDEF");
        line = Regex.Replace(line, @"^(\s*)%else\b",   "$1ELSE");
        line = Regex.Replace(line, @"^(\s*)%endif\b",  "$1ENDIF");

        // Drop NASM-only / Linux-only lines (masm.h replaces the %defines)
        if (Regex.IsMatch(line, @"^\s*%(define|xdefine|assign|undef)\b")) return null;
        if (Regex.IsMatch(line, @"^\s*(BITS|DEFAULT)\b"))                 return null;
        if (Regex.IsMatch(line, @"^\s*section\s+\.note\.GNU-stack"))      return null;

        // Segment / section switches
        line = Regex.Replace(line, @"^(\s*)(section|segment)\s+\.text\b.*", "$1.CODE");
        line = Regex.Replace(line, @"^(\s*)(section|segment)\s+\.data\b.*", "$1.DATA");

        // Symbol visibility
        line = Regex.Replace(line, @"^(\s*)global\s+(\S+)", "$1PUBLIC $2");

        var m = Regex.Match(line, @"^(\s*)extern\s+(\S+)\s*(;.*)?$");
        if (m.Success)
        {
            string sym = m.Groups[2].Value;
            string type = types.TryGetValue(sym, out var t) ? t : "PROC";
            line = $"{m.Groups[1].Value}EXTERN {sym}:{type}";
        }

        // Data-definition directives. These sit in the directive slot,
        // where MASM resolves the keyword BEFORE text-macro substitution --
        // so a TEXTEQU alias (d_word -> dq) is rejected as "syntax error".
        // Emit the literal directive here instead. Handles both the bare
        // form ("d_word x") and the labelled form ("lbl: d_word x").
        line = Regex.Replace(line, @"\bd_word\b", "dq");
        line = Regex.Replace(line, @"\bd_real\b", "dq");
        line = Regex.Replace(line, @"\bd_char\b", "db");

        // cdq -> cqo. The NASM header aliased cdq to cqo so the word-size
        // sign-extend becomes 64-bit (RDX:RAX). MASM forbids TEXTEQU on a
        // reserved mnemonic like cdq, so rewrite the instruction here.
        line = Regex.Replace(line, @"^(\s*)cdq\b", "$1cqo");

        // Hex literals 0xHHHH -> 0HHHHh
        line = Regex.Replace(line, @"0x([0-9A-Fa-f]+)", "0$1h");

        return line;
    }

    // ------------------------------------------------------------
    //  convert_macro_block: convert one NASM %macro..%endmacro block
    //  starting at lines[i]. Returns the index just past %endmacro.
    // ------------------------------------------------------------
    public static int ConvertMacroBlock(string[] lines, int i, List<string> outLines,
                                        Dictionary<string, string> types)
    {
        var head = Regex.Match(lines[i], @"^(\s*)%macro\s+(\w+)\s+(\d+)");
        string indent = head.Groups[1].Value;
        string name = head.Groups[2].Value;
        int argc = int.Parse(head.Groups[3].Value);

        var paramNames = Enumerable.Range(1, argc).Select(k => "p" + k);

        var body = new List<string>();
        var locals = new List<string>();

        i++;
        while (i < lines.Length && !Regex.IsMatch(lines[i], @"^\s*%endmacro"))
        {
            string ln = lines[i];

            // %%label -> a generated local; collect names, strip the %%
            foreach (Match loc in Regex.Matches(ln, @"%%(\w+)"))
            {
                string locName = loc.Groups[1].Value;
                if (!locals.Contains(locName)) locals.Add(locName);
            }
            ln = Regex.Replace(ln, @"%%(\w+)", "$1");

            // positional params %1 %2 ... -> p1 p2 ...
            ln = Regex.Replace(ln, @"%(\d+)", "p$1");

            // same body transforms as ordinary lines
            string? t = TransformLine(ln, types);
            if (t != null) body.Add(t);

            i++;
        }

        outLines.Add($"{indent}{name} MACRO {string.Join(", ", paramNames)}");
        if (locals.Count > 0)
            outLines.Add($"{indent}    LOCAL {string.Join(", ", locals)}");
        outLines.AddRange(body);
        outLines.Add($"{indent}    ENDM");

        return i + 1; // skip %endmacro
    }

    // ------------------------------------------------------------
    //  convert: main driver over the whole file.
    // ------------------------------------------------------------
    public static string Convert(string text)
    {
        var types = ClassifyExterns(text);
        var src = text.Split('\n');
        var outLines = new List<string>();

        int i = 0;
        while (i < src.Length)
        {
            if (Regex.IsMatch(src[i], @"^\s*%macro\s"))
            {
                i = ConvertMacroBlock(src, i, outLines, types);
                continue;
            }

            string? line = TransformLine(src[i], types);
            if (line != null) outLines.Add(line);
            i++;
        }

        // MASM requires a terminating END directive
        bool hasEnd = outLines.Skip(Math.Max(0, outLines.Count - 3))
                              .Any(l => Regex.IsMatch(l, @"^\s*END\s*$"));
        if (!hasEnd) outLines.Add("        END");

        return string.Join("\n", outLines);
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: Nasm2Masm <in.asm> <out.asm>");
            return 1;
        }

        string text = File.ReadAllText(args[0]);            // UTF-8, BOM-aware
        string result = Nasm2Masm.Convert(text);

        // UTF-8 without BOM, LF line endings (matches the Python output)
        File.WriteAllText(args[1], result, new UTF8Encoding(false));
        return 0;
    }
}
