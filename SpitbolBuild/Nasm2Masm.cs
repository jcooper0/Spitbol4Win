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
    public static string? TransformLine(string line, Dictionary<string, string> types,
                                        HashSet<string> equNames)
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

        // NASM reservation "[label:] times N dq V" -> MASM "label dq N dup(V)".
        line = Regex.Replace(line,
            @"^(\s*)([A-Za-z_$@?][\w$@?]*):?\s+times\s+(\S+)\s+(dq|db|dd|dw)\s+(\S+)",
            "$1$2 $4 $3 dup($5)");
        // label-less form: "times N dq V" -> "dq N dup(V)" (hand-written int.asm).
        line = Regex.Replace(line,
            @"^(\s*)times\s+(\S+)\s+(dq|db|dd|dw)\s+(\S+)",
            "$1$3 $2 dup($4)");

        // MASM data labels take NO colon: "name: dq 0" -> "name dq 0".
        // NASM accepts the colon; MASM rejects it ("syntax error : dq").
        // Only strip when a data directive follows -- code labels (followed by
        // an instruction) and bare label lines keep their colon.
        line = Regex.Replace(line,
            @"^(\s*[A-Za-z_$@?][\w$@?]*)\s*:(\s+(?:dq|db|dd|dw|dt|df|real4|real8|real10)\b)",
            "$1 $2");

        // cdq -> cqo. The NASM header aliased cdq to cqo so the word-size
        // sign-extend becomes 64-bit (RDX:RAX). MASM forbids TEXTEQU on a
        // reserved mnemonic like cdq, so rewrite the instruction here.
        line = Regex.Replace(line, @"^(\s*)cdq\b", "$1cqo");

        // Hex literals 0xHHHH -> 0HHHHh
        line = Regex.Replace(line, @"0x([0-9A-Fa-f]+)", "0$1h");

        // Raw NASM size specifiers "byte [x]" -> "BYTE PTR [x]". ml64
        // tolerates the bare form, but make it explicit and unambiguous.
        line = Regex.Replace(line, @"\bbyte\s+\[",  "BYTE PTR [");
        line = Regex.Replace(line, @"\bword\s+\[",  "WORD PTR [");
        line = Regex.Replace(line, @"\bdword\s+\[", "DWORD PTR [");
        line = Regex.Replace(line, @"\bqword\s+\[", "QWORD PTR [");
        line = Regex.Replace(line, @"\boword\s+\[", "XMMWORD PTR [");

        // Scalar-SSE memory operands need an explicit size. The generator
        // emits e.g. `movsd ra,[(cfp_b*rcval)+xr]` with no size word, which
        // ml64 rejects (A2070: invalid operands) because it cannot infer
        // 64-bit. Add m_real (=QWORD PTR) before the bare bracket operand.
        // Skip lines that already carry a size (the `ngr` negate uses m_real).
        if (Regex.IsMatch(line, @"^\s*(movsd|movss|addsd|subsd|mulsd|divsd|comisd|ucomisd|sqrtsd|sqrtss|cvtsi2sd|cvtsd2si|cvtsi2ss|cvtss2sd|cvtsd2ss)\b")
            && !Regex.IsMatch(line, @"\b(m_real|m_word|m_reall|QWORD|XMMWORD)\b"))
        {
            line = Regex.Replace(line, @"(?<=[,\s])\[", "m_real [");
        }

        // Address-of operands that MASM cannot take as an immediate.
        // `mov reg,m_addr S` is fine (mov r64,imm64). But mov-to-memory,
        // cmp, add, sub and push of a 64-bit address all need it in a
        // register first. r11 is unused by the generated code and is
        // Win64-volatile, so it is a safe scratch. (masm.h: m_addr->OFFSET.)
        {
            int sc = line.IndexOf(';');
            string code = sc >= 0 ? line.Substring(0, sc) : line;
            string cmt  = sc >= 0 ? "  " + line.Substring(sc) : "";
            var am = Regex.Match(code, @"^(\s*)(mov|cmp|add|sub|push)\s+(.*?)\s*$");
            if (am.Success && code.Contains("m_addr"))
            {
                string ind = am.Groups[1].Value, op = am.Groups[2].Value, ops = am.Groups[3].Value;

                // Identify the m_addr target. MINIMAL `=X` is "immediate value
                // of X": for a memory label that is its address (OFFSET); for
                // an EQU constant it is just the number, and OFFSET must NOT be
                // applied (ml64 A2098). For a constant, drop m_addr entirely --
                // the plain immediate form is correct for every op.
                var tgt = Regex.Match(ops, @"m_addr\s+(\S+)");
                if (tgt.Success && equNames.Contains(tgt.Groups[1].Value))
                    return Regex.Replace(code, @"m_addr\s+", "").TrimEnd() + cmt;

                if (op == "push")
                {
                    var pm = Regex.Match(ops, @"^m_addr\s+(\S+)$");
                    if (pm.Success)
                        return $"{ind}mov  r11,m_addr {pm.Groups[1].Value}\n{ind}push r11{cmt}";
                }
                else
                {
                    var dm = Regex.Match(ops, @"^(.+?),\s*m_addr\s+(\S+)$");
                    if (dm.Success)
                    {
                        string dest = dm.Groups[1].Value.Trim();
                        string sym  = dm.Groups[2].Value;
                        bool destIsMem = dest.Contains('[');
                        if (!(op == "mov" && !destIsMem))   // mov reg,addr stays as-is
                            return $"{ind}mov  r11,m_addr {sym}\n{ind}{op,-4} {dest},r11{cmt}";
                    }
                }
            }
        }

        return line;
    }

    // ------------------------------------------------------------
    //  convert_macro_block: convert one NASM %macro..%endmacro block
    //  starting at lines[i]. Returns the index just past %endmacro.
    // ------------------------------------------------------------
    public static int ConvertMacroBlock(string[] lines, int i, List<string> outLines,
                                        Dictionary<string, string> types, HashSet<string> equNames)
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
            string? t = TransformLine(ln, types, equNames);
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

    // x86-64 / SSE / SSE2 instruction mnemonics. A data label that collides
    // with one of these (e.g. `cmpss`) becomes ambiguous once MASM strips the
    // NASM colon -- `cmpss dq 0` parses `cmpss` as the instruction. Such
    // labels are renamed (whole-word, with a `_` suffix) across the file.
    private static readonly HashSet<string> ReservedMnemonics = new(StringComparer.OrdinalIgnoreCase)
    {
        "mov","movzx","movsx","movsxd","lea","push","pop","add","sub","adc","sbb",
        "cmp","test","and","or","xor","not","neg","inc","dec","mul","imul","div","idiv",
        "shl","shr","sal","sar","rol","ror","rcl","rcr","bt","bts","btr","btc","bsf","bsr",
        "jmp","call","ret","retn","retf","leave","enter","nop","int","into","iret",
        "loop","loope","loopne","jcxz","jecxz","jrcxz","xchg","xadd","cmpxchg","bswap",
        "movsb","movsw","movsd","movsq","stosb","stosw","stosd","stosq",
        "lodsb","lodsw","lodsd","lodsq","cmpsb","cmpsw","cmpsd","cmpsq","scasb","scasw","scasd","scasq",
        "rep","repe","repz","repne","repnz","cld","std","cmc","clc","stc","cli","sti",
        "cdq","cqo","cwd","cdqe","cbw","cwde",
        "movss","addss","subss","mulss","divss","comiss","ucomiss","cmpss","cmpps","cmppd",
        "addsd","subsd","mulsd","divsd","comisd","ucomisd","sqrtsd","sqrtss",
        "cvtsi2sd","cvtsd2si","cvtsi2ss","cvtss2sd","cvtsd2ss","cvtss2si","cvttsd2si",
        "movd","movq","movaps","movups","movapd","movupd","movdqa","movdqu",
        "xorps","xorpd","andps","andpd","orps","orpd","pxor","por","pand","pandn",
        "ldmxcsr","stmxcsr","fld","fst","fstp","wait","fwait",
        "in","out","ins","outs","hlt","lock","pause","rdtsc","cpuid","seta","setb","setz","setnz"
    };

    // ------------------------------------------------------------
    //  rename_colliding_labels: rename any DATA-definition label whose
    //  name is a reserved mnemonic, whole-word, throughout the file.
    // ------------------------------------------------------------
    public static string RenameCollidingLabels(string text)
    {
        var collisions = new HashSet<string>();
        foreach (Match m in Regex.Matches(text,
                     @"^\s*([A-Za-z_$@?][\w$@?]*)\s*:?\s+(d_word|d_char|d_real)\b",
                     RegexOptions.Multiline))
        {
            string lbl = m.Groups[1].Value;
            if (ReservedMnemonics.Contains(lbl)) collisions.Add(lbl);
        }
        foreach (var lbl in collisions)
        {
            string esc = Regex.Escape(lbl);
            text = Regex.Replace(text, $@"(?<![\w$@?]){esc}(?![\w$@?])", lbl + "_");
        }
        return text;
    }

    // ------------------------------------------------------------
    //  convert: main driver over the whole file.
    // ------------------------------------------------------------
    public static string Convert(string text)
    {
        text = RenameCollidingLabels(text);
        var types = ClassifyExterns(text);

        // EQU-defined names are numeric constants, not memory labels, so a
        // `=X` (m_addr) operand on them must not get OFFSET. Collect them.
        var equNames = new HashSet<string>();
        foreach (Match m in Regex.Matches(text,
                     @"^\s*([A-Za-z_$@?][\w$@?]*)\s+equ\b",
                     RegexOptions.Multiline | RegexOptions.IgnoreCase))
            equNames.Add(m.Groups[1].Value);

        var src = text.Split('\n');
        var outLines = new List<string>();

        int i = 0;
        bool inData = false;
        while (i < src.Length)
        {
            if (Regex.IsMatch(src[i], @"^\s*%macro\s"))
            {
                i = ConvertMacroBlock(src, i, outLines, types, equNames);
                continue;
            }

            // Merge a lone string-op prefix with the following string
            // instruction. NASM allows `rep` on its own line; MASM needs
            // `rep movsb` on a single line ("prefix must be followed by an
            // instruction" otherwise).
            var rep = Regex.Match(src[i], @"^(\s*)(rep|repe|repz|repne|repnz)\s*(;.*)?$");
            if (rep.Success && i + 1 < src.Length)
            {
                var nxt = Regex.Match(src[i + 1], @"^\s*(\S.*?)\s*(;.*)?$");
                if (nxt.Success && !nxt.Groups[1].Value.StartsWith(";"))
                {
                    string repCmt = rep.Groups[3].Value.Length > 0 ? "    " + rep.Groups[3].Value : "";
                    string merged = $"{rep.Groups[1].Value}{rep.Groups[2].Value} {nxt.Groups[1].Value}{repCmt}";
                    string? tlm = TransformLine(merged, types, equNames);
                    if (tlm != null) outLines.Add(tlm);
                    i += 2;
                    continue;
                }
            }

            string? line = TransformLine(src[i], types, equNames);
            if (line != null)
            {
                // Track simplified-segment state so we can fix labels below.
                if (Regex.IsMatch(line, @"^\s*\.DATA\b")) inData = true;
                else if (Regex.IsMatch(line, @"^\s*\.CODE\b")) inData = false;

                // A standalone label in a DATA segment ("sec02:", "_l0001:")
                // is a near *code* label, which some MASM-compatible
                // assemblers reject inside .DATA. Re-express it as a data
                // label of the same address via LABEL BYTE -- accepted by
                // ml64 and JWasm alike, and equivalent for address use.
                var sl = Regex.Match(line, @"^(\s*)([A-Za-z_$@?][\w$@?]*):\s*(;.*)?$");
                if (inData && sl.Success)
                {
                    string c = sl.Groups[3].Value.Length > 0 ? "  " + sl.Groups[3].Value : "";
                    line = $"{sl.Groups[1].Value}{sl.Groups[2].Value} LABEL BYTE{c}";
                }

                outLines.Add(line);
            }
            i++;
        }

        // MASM requires a terminating END directive
        bool hasEnd = outLines.Skip(Math.Max(0, outLines.Count - 3))
                              .Any(l => Regex.IsMatch(l, @"^\s*END\s*$"));
        if (!hasEnd) outLines.Add("        END");

        return string.Join("\n", outLines);
    }
}

