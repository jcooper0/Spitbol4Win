// ============================================================
//  MinimalCodeGen.cs -- Component 2 of the C# MINIMAL translator.
//  Port of asm.sbl: reads the tokenized intermediate (sbl.lex) and
//  emits x86-64 NASM assembly (sbl.asm). Diffed against the bootstrap
//  sbl.asm the same way Component 1 was diffed against sbl.lex.
//
//  STATUS: foundation + first handler batch. The emit layer (getarg,
//  genop family, flush, outstmt, register) is a faithful port; handlers
//  are being ported in families. Unimplemented opcodes route to a stub
//  that records coverage, so a diff against the reference shows exactly
//  the first not-yet-ported opcode.
//
//  Method/label names mirror asm.sbl. NASM output is emitted verbatim
//  (no m_addr / OFFSET marker yet) so it can reach byte-zero against the
//  bootstrap; the m_addr patch is a separate, later step.
//
//  Usage:  MinimalCodeGen <in.lex> <out.asm>
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SpitbolTools;

// operand: minarg(type,text)
public readonly struct Operand
{
    public readonly int Type;
    public readonly string Text;
    public Operand(int type, string text) { Type = type; Text = text; }
}

// tstmt(label,opc,op1,op2,op3,comment) -- null string == SNOBOL null (omitted)
public sealed class Tstmt
{
    public string? Label, Opc, Op1, Op2, Op3, Comment;
    public Tstmt(string? label = null, string? opc = null, string? op1 = null,
                 string? op2 = null, string? op3 = null, string? comment = null)
    { Label = label; Opc = opc; Op1 = op1; Op2 = op2; Op3 = op3; Comment = comment; }
}

public sealed class MinimalCodeGen
{
    private const char Sep = '|';

    private readonly TextWriter outFile;

    // ---- per-statement parse fields (from p.csparse) ----
    private string inlabel = "", incode = "", iarg1 = "", iarg2 = "", iarg3 = "";
    private string incomment = "", slineno = "";
    private string thisline = "", thislabel = "";
    private string? lastlabel;
    private string? tcomment;
    private Operand i1, i2, i3;

    // ---- statement queues (before / code / after) ----
    private readonly List<Tstmt> bstmts = new();
    private readonly List<Tstmt> cstmts = new();
    private readonly List<Tstmt> astmts = new();

    // ---- state ----
    private int sectnow;
    private int genlabels;
    private int outputLines, inputLines, ntarget;
    private bool finished;

    // declaration / procedure bookkeeping
    private readonly Dictionary<string,string> ppmCases = new();
    private int prcCount1;
    private string prcArgs = "";
    private int maxExi;
    private string prcType = "";
    private int prcCount;
    private int jsrCalls;
    private int jsrCount;
    private string jsrLabelNorm = "";
    private string labNext = "";

    private static string Prcent(int n) => "prc_+cfp_b*" + (n - 1);

    // ---- tables ----
    private readonly HashSet<int> ismem = new() { 3,4,5,9,10,11,12,13,14,15 };
    private readonly Dictionary<string,int> pifatal = new();

    // branchtab: minimal conditional branch -> x86 (unsigned) jump
    private static readonly Dictionary<string,string> Branchtab = new()
    {
        {"beq","je"}, {"bne","jne"}, {"bgt","ja"}, {"bge","jae"},
        {"ble","jbe"}, {"blt","jb"}, {"blo","jb"}, {"bhi","ja"},
    };

    private static string Reglow(string s) => s switch
    {
        "w0" => "al", "wa" => "cl", "wb" => "bl", "wc" => "dl",
        "rax" => "al", "rbx" => "bl", "rcx" => "cl", "rdx" => "dl",
        _ => throw new InvalidOperationException("bad argument to reglow: " + s),
    };

    // ---- coverage of unimplemented opcodes ----
    public readonly SortedDictionary<string,int> NotImplemented = new();

    private static readonly Regex CsParse = new(
        @"^\|([^|]*)\|([^|]*)\|([^|]*)\|([^|]*)\|([^|]*)\|([^|]*)\|(.*)$",
        RegexOptions.Compiled);
    private static readonly Regex ArgRe = new(@"^(\d+),(.*)$", RegexOptions.Compiled);

    public MinimalCodeGen(TextWriter output)
    {
        outFile = output;
        foreach (var k in "aov beq bne bge bgt bhi ble blo blt bnz ceq cne mfi nzb zrb".Split(' '))
            pifatal[k] = 1;
    }

    // ============================================================
    //  Main loop (opnext)
    // ============================================================
    public void Run(TextReader input)
    {
        string? line;
        while ((line = ReadLine(input)) != null)
        {
            thisline = line;
            if (!Crack(thisline)) continue;     // not a token line (blank/comment) -> handled

            // emit label of executable instruction immediately (opnext label logic)
            if (inlabel.Length > 0)
            {
                thislabel = inlabel + (sectnow >= 3 ? ":" : "");
                if (sectnow >= 5 && incode != "ent")
                {
                    outFile.Write(thislabel); outFile.Write('\n');
                    outputLines++;
                    lastlabel = thislabel;
                    thislabel = "";
                }
            }
            thislabel = inlabel.Length > 0 ? inlabel + (sectnow >= 3 ? ":" : "") : "";

            i1 = Prsarg(iarg1);
            i2 = Prsarg(iarg2);
            i3 = Prsarg(iarg3);
            tcomment = Comregs(incomment) + "} " + incode + " "
                       + i1.Text + " " + i2.Text + " " + i3.Text;

            Dispatch(incode);
            if (finished) break;
        }
    }

    // ============================================================
    //  readline() -- pass blank/comment lines straight through
    // ============================================================
    private bool emittedEnd;
    private string? ReadLine(TextReader input)
    {
        while (true)
        {
            string? rd = input.ReadLine();
            if (rd == null)
            {
                if (emittedEnd) return null;
                emittedEnd = true;
                // synthetic end line in sbl.lex format
                return $"{Sep}{Sep}end{Sep}{Sep}{Sep}{Sep}{Sep}0";
            }
            inputLines++;
            if (rd.Length == 0) { outFile.Write('\n'); outputLines++; continue; }
            if (rd[0] == '*')   { outFile.Write(rd); outFile.Write('\n'); outputLines++; continue; }
            return rd;
        }
    }

    // ============================================================
    //  crack() -- parse one sbl.lex line via p.csparse
    // ============================================================
    private bool Crack(string line)
    {
        var m = CsParse.Match(line);
        if (!m.Success) return false;
        inlabel   = m.Groups[1].Value;
        incode    = m.Groups[2].Value;
        iarg1     = m.Groups[3].Value;
        iarg2     = m.Groups[4].Value;
        iarg3     = m.Groups[5].Value;
        incomment = m.Groups[6].Value;
        slineno   = m.Groups[7].Value;
        return true;
    }

    // ============================================================
    //  prsarg() -- "type,text" -> Operand ; "" -> (0,"")
    // ============================================================
    private static Operand Prsarg(string field)
    {
        if (field.Length == 0) return new Operand(0, "");
        var m = ArgRe.Match(field);
        if (m.Success)
            return new Operand(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                               m.Groups[2].Value);
        return new Operand(0, field);
    }

    // ============================================================
    //  register() -- identity map for this NASM x64 build
    // ============================================================
    // register map for this NASM x64 build. Identity except xt -> xl
    // (both are rsi; xl is the canonical output name).
    private static readonly Dictionary<string,string> RegMap = new()
    {
        {"xl","xl"}, {"xr","xr"}, {"xs","xs"}, {"xt","xl"},
        {"w0","w0"}, {"wa","wa"}, {"wb","wb"}, {"wc","wc"},
        {"wa_l","wa_l"}, {"wb_l","wb_l"}, {"wc_l","wc_l"},
        {"ia","ia"}, {"ra","ra"},
    };

    private string? Register(string s) => RegMap.TryGetValue(s, out var r) ? r : null;

    private bool IsReg(Operand a) => a.Type >= 7 && a.Type <= 8;
    private bool IsMem(Operand a) => ismem.Contains(a.Type);

    // ============================================================
    //  comregs() -- map 2-char minimal regs in a comment (identity here)
    // ============================================================
    // comregs() -- map 2-char minimal register names in a comment.
    // p.comregs = break(letters) . pre  span(letters) . word ; a 2-letter
    // word that is a register is replaced by register(word) (only xt->xl
    // changes anything here; the rest of the map is identity).
    private string Comregs(string line)
    {
        var sb = new StringBuilder();
        int i = 0, n = line.Length;
        while (i < n)
        {
            int start = i;
            while (i < n && !IsLetter(line[i])) i++;     // break(letters) -> pre
            sb.Append(line, start, i - start);
            if (i >= n) break;
            int ws = i;
            while (i < n && IsLetter(line[i])) i++;       // span(letters) -> word
            string word = line.Substring(ws, i - ws);
            if (word.Length == 2 && Register(word) is string r) word = r;
            sb.Append(word);
        }
        return sb.ToString();
    }

    private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    // ============================================================
    //  getarg() -- the 27-case operand formatter (verbatim port)
    // ============================================================
    private string Getarg(Operand iarg, bool mem = false)
    {
        string tmem = mem ? "" : "m_word ";   // (differ(imem) '', 'm_word ')
        string l1 = iarg.Text;
        int l2 = iarg.Type;
        switch (l2)
        {
            case 0:
            case 1:  return l1;                                   // int
            case 2:  return l1;                                   // dlbl
            case 3:
            case 4:  return tmem + "[" + l1 + "]";                // wlbl, clbl
            case 5:
            case 6:  return l1;                                   // elbl, plbl
            case 7:
            case 8:  return Register(l1) ?? l1;                   // w, x register
            case 9:                                               // (x) indirect
            {
                string r = Register(l1.Substring(1, 2)) ?? l1.Substring(1, 2);
                return tmem + "[" + r + "]";
            }
            case 10:                                              // (x)+ postincrement
            {
                string l1b = l1.Substring(1, 2);
                string t1 = Register(l1b) ?? l1b;
                string res = tmem + "[" + t1 + "]";
                if (l1b == "xs") Genaop(new Tstmt(null, "add", t1, "cfp_b"));
                else Genaop(new Tstmt(null, "lea", t1, "[" + t1 + "+cfp_b]"));
                return res;
            }
            case 11:                                              // -(x) predecrement
            {
                string t1 = Register(l1.Substring(2, 2)) ?? l1.Substring(2, 2);
                string res = tmem + "[" + t1 + "]";
                Genbop(new Tstmt(null, "lea", t1, "[" + t1 + "-cfp_b]"));
                return res;
            }
            case 12:
            case 13:                                              // int(x), dlbl(x)
            {
                int p = l1.IndexOf('(');
                string t1 = l1.Substring(0, p);
                string t2 = l1.Substring(p + 1, 2);
                return tmem + "[(cfp_b*" + t1 + ")+" + (Register(t2) ?? t2) + "]";
            }
            case 14:
            case 15:                                              // name(x) in working section
            {
                int p = l1.IndexOf('(');
                string t1 = l1.Substring(0, p);
                string t2 = l1.Substring(p + 1, 2);
                return tmem + "[" + t1 + "+" + (Register(t2) ?? t2) + "]";
            }
            case 16: return l1;                                   // signed integer
            case 17: return l1;                                   // signed real
            case 18: return "m_addr " + l1.Substring(1);         // =dlbl  (address-of)
            case 19: return "cfp_b*" + l1.Substring(1);           // *dlbl  (scaled value, not address)
            case 20:
            case 21: return "m_addr " + l1.Substring(1);          // =name data    (address-of)
            case 22: return "m_addr " + l1.Substring(1);          // =name program (address-of)
            case 23:
            case 24: return l1;                                   // pnam, eqop
            case 25:
            case 26:
            case 27: return l1;                                   // ptyp, text, dtext
            default: return l1;
        }
    }

    // ============================================================
    //  genop family
    // ============================================================
    private void Genop(string? opc = null, string? op1 = null, string? op2 = null, string? op3 = null)
        => Genopl(null, opc, op1, op2, op3);

    private void Genopl(string? lbl, string? opc = null, string? op1 = null, string? op2 = null, string? op3 = null)
        => cstmts.Add(new Tstmt(lbl, opc, op1, op2, op3));

    private void Genaop(Tstmt s) => astmts.Add(s);
    private void Genbop(Tstmt s) => bstmts.Add(s);
    private string Genlab() => "_l" + (++genlabels).ToString("D4", CultureInfo.InvariantCulture);

    // ============================================================
    //  flush() -- emit bstmts, cstmts, astmts; attach label+comment to first
    // ============================================================
    private void Flush()
    {
        if (astmts.Count == 0 && bstmts.Count == 0 && cstmts.Count == 0)
        {
            thislabel = "";
            Outstmt(new Tstmt());
        }
        else
        {
            foreach (var s in bstmts) Outstmt(s);
            foreach (var s in cstmts) Outstmt(s);
            foreach (var s in astmts) Outstmt(s);
        }
        astmts.Clear(); bstmts.Clear(); cstmts.Clear();
    }

    // ============================================================
    //  outstmt() -- format one target statement to the output file
    // ============================================================
    private void Outstmt(Tstmt ostmt)
    {
        string label = ostmt.Label ?? "";
        // clear label if definition already emitted
        if (label == (lastlabel ?? "\0")) label = "";

        string comment = ostmt.Comment ?? "";
        // statement's own comment wins; else attach tcomment once
        if (comment.Length == 0 && tcomment != null) { comment = tcomment; tcomment = null; }

        string opcode = ostmt.Opc ?? "";
        string op1 = ostmt.Op1 ?? "\0", op2 = ostmt.Op2 ?? "\0", op3 = ostmt.Op3 ?? "\0";

        // operands: op1 (,op2 (,op3)) -- a \0 sentinel means "null/omitted"
        string ops = "";
        if (op1 != "\0")
        {
            ops = op1;
            if (op2 != "\0")
            {
                ops += "," + op2;
                if (op3 != "\0") ops += "," + op3;
            }
        }

        // non-compress (padded) form
        string line = Rpad(Rpad(label, 7) + " " + Rpad(opcode, 4) + " " + ops, 27);
        line = line.TrimEnd();

        // asm.sbl ALWAYS appends the comment column (even when comment is empty)
        if (line.Length <= 48) line = Rpad(line, 48) + "; " + comment;
        else if (line.Length <= 56) line = Rpad(line, 56) + "; " + comment;
        else line = line + "; " + comment;

        outFile.Write(line); outFile.Write('\n');
        ntarget++; outputLines++;

        // record code labels (sectnow>=5 with a thislabel)
        if (sectnow >= 5 && !string.IsNullOrEmpty(thislabel))
        {
            int c = label.IndexOf(':');
            // labtab bookkeeping omitted (not needed for output)
        }
    }

    private static string Rpad(string s, int n) => s.Length >= n ? s : s + new string(' ', n - s.Length);

    // ============================================================
    //  dispatch  ( :($('g_' incode)) )
    // ============================================================
    private void Dispatch(string code)
    {
        switch (code)
        {
            // --- no-ops (listing directives, no asm output) ---
            case "ttl":
            case "ejc": return;                       // :(opnext)

            // --- section control ---
            case "sec": GSec(); return;

            // --- declarations / procedures ---
            case "exp": ppmCases[thislabel] = i1.Text; Genop("extern", thislabel); thislabel = ""; Opdone(); return;

            case "inp":                                  // bookkeeping only, NO output
                ppmCases[thislabel] = i2.Text;
                if (i1.Text == "n") prcCount1++;
                return;

            case "inr": Genop(""); Opdone(); return;

            case "prc":
            {
                prcArgs = Getarg(i2);
                ppmCases[thislabel] = i2.Text;
                thislabel = "";
                if (int.TryParse(prcArgs, out int pa) && pa > maxExi) maxExi = pa;
                prcType = i1.Text;
                if (prcType == "n")
                {
                    prcCount++;
                    Genop("pop", "m_word [" + Prcent(prcCount) + "]");
                }
                Opdone(); return;
            }

            case "exi": GExi(); return;

            case "jsr": GJsr(); return;

            case "zer":
                if (i1.Text == "(xr)+") { Genop("xor", "w0", "w0"); Genop("stos_w"); Opdone(); return; }
                if (IsReg(i1)) { string t = Getarg(i1); Genop("xor", t, t); Opdone(); return; }
                if (i1.Text == "-(xs)") { Genop("push", "0"); Opdone(); return; }
                Genop("xor", "w0", "w0"); Genop("mov", Getarg(i1), "w0"); Opdone(); return;

            case "ldi": Genop("mov", "ia", Getarg(i1)); Opdone(); return;
            case "sti": Genop("mov", Getarg(i1), "ia"); Opdone(); return;
            case "ica": Genop("add", Getarg(i1), "cfp_b"); Opdone(); return;
            case "icv": Genop("inc", Getarg(i1)); Opdone(); return;
            case "ngi": Genop("neg", "ia"); Opdone(); return;
            case "wtb": Genop("sal", Getarg(i1), "log_cfp_b"); Opdone(); return;

            case "lct":
                if (i1.Text != i2.Text) { Genop("mov", Getarg(i1), Getarg(i2)); Opdone(); return; }
                if (string.IsNullOrEmpty(thislabel)) return;   // ident(thislabel) -> opnext
                Genop(); Opdone(); return;

            case "lcp": Genop("mov", "r13", Getarg(i1)); Opdone(); return;     // cp = r13
            case "scp": Genop("mov", Getarg(i1), "r13"); Opdone(); return;

            case "mti":
                if (i1.Text == "(xs)+") Genop("pop", "ia");
                else Genop("mov", "ia", Getarg(i1));
                Opdone(); return;

            case "ldr": Genop("movsd", "ra", Getarg(i1, true)); Opdone(); return;
            case "str": Genop("movsd", Getarg(i1, true), "ra"); Opdone(); return;

            case "lei":
            {
                string t1 = Register(i1.Text) ?? i1.Text;
                Genop("movzx", t1, "byte [" + t1 + "-1]"); Opdone(); return;
            }

            // --- integer arithmetic (accumulator ia) ---
            case "adi": Genop("add", "ia", Getarg(i1)); Opdone(); return;
            case "sbi": Genop("sub", "ia", Getarg(i1)); Opdone(); return;
            case "mli": Genop("imul", "ia", Getarg(i1)); Opdone(); return;
            case "dvi": Genop("mov", "r10", Getarg(i1)); Genop("call", "do_dvi"); Opdone(); return;
            case "rmi": Genop("mov", "r10", Getarg(i1)); Genop("call", "do_rmi"); Opdone(); return;

            // --- bit ops ---
            case "anb": Genop("and", Getarg(i1), Getarg(i2)); Opdone(); return;
            case "orb": Genop("or", Getarg(i1), Getarg(i2)); Opdone(); return;
            case "xob": Genop("xor", Getarg(i1), Getarg(i2)); Opdone(); return;
            case "cmb": Genop("not", Getarg(i1)); Opdone(); return;
            case "btw": Genop("shr", Getarg(i1), "log_cfp_b"); Opdone(); return;

            // --- load const word ---
            case "lcw":
                Genop("mov", "r10", "m_word [r13]");
                Genop("mov", Getarg(i1, true), "r10");
                Genop("add", "r13", "cfp_b");
                Opdone(); return;

            // --- real arithmetic ---
            case "adr": Genop("ldmxcsr", "[mxcsr_set]"); Genop("addsd", "ra", Getarg(i1, true)); GRunder(); return;
            case "sbr": Genop("ldmxcsr", "[mxcsr_set]"); Genop("subsd", "ra", Getarg(i1, true)); GRunder(); return;
            case "mlr": Genop("ldmxcsr", "[mxcsr_set]"); Genop("mulsd", "ra", Getarg(i1, true)); GRunder(); return;
            case "dvr": Genop("ldmxcsr", "[mxcsr_set]"); Genop("divsd", "ra", Getarg(i1, true)); GRunder(); return;
            case "ngr":
                Genop("movsd", "xmm0", "m_real [zeron]");
                Genop("pxor", "ra", "xmm0"); Opdone(); return;

            case "rti":
                Genop("cvttsd2si", "ia", "ra");
                if (i1.Type == 0) { Opdone(); return; }
                Genop("mov", "r10", "0x80000000");
                Genop("cmp", "ia", "r10");
                Genop("je", Getarg(i1)); Opdone(); return;
            case "rno": Genop("call", "do_chk_real_inf"); Genop("jz", Getarg(i1)); Opdone(); return;
            case "rov": Genop("call", "do_chk_real_inf"); Genop("jnz", Getarg(i1)); Opdone(); return;

            case "ctb":
            {
                string t1 = Getarg(i1);
                Genop("add", t1, "(cfp_b-1)+cfp_b*" + i2.Text);
                Genop("and", t1, "-cfp_b"); Opdone(); return;
            }
            case "ctw":
            {
                string t1 = Getarg(i1);
                Genop("add", t1, "(cfp_c-1)+cfp_c*" + i2.Text);
                Genop("shr", t1, "log_cfp_c"); Opdone(); return;
            }
            case "mfi":
                if (i2.Type != 0) { Genop("test", "ia", "ia"); Genop("js", Getarg(i2)); }
                if (i1.Text == "-(xs)") { Genop("push", "ia"); Opdone(); return; }   // g_mfi.2
                Genop("mov", Getarg(i1), "ia"); Opdone(); return;
            case "itr":
                Genop("pxor", "ra", "ra"); Genop("cvtsi2sd", "ra", "ia"); Opdone(); return;

            case "err": GErr(false); return;
            case "ppm": GErr(true); return;
            case "erb":
            {
                string rc = (int.TryParse(i1.Text, out int v) ? v : 0).ToString();
                Genop("mov", "m_word [_rc_]", rc); Genop("jmp", "err_"); Opdone(); return;
            }

            // --- integer-flag branches ---
            case "ino": Genop("jno", Getarg(i1)); Opdone(); return;
            case "iov": Genop("jo", Getarg(i1)); Opdone(); return;
            case "aov": Genop("add", Getarg(i2), Getarg(i1)); Genop("jc", Getarg(i3)); Opdone(); return;
            case "ieq": case "ige": case "igt": case "ile": case "ilt": case "ine":
            {
                string jop = code switch
                { "ieq" => "je", "ige" => "jge", "igt" => "jg",
                  "ile" => "jle", "ilt" => "jl", _ => "jne" };
                Genop("cmp", "ia", "0"); Genop(jop, Getarg(i1)); Opdone(); return;
            }

            // --- real compares (vs 0.0) ---
            case "req": case "rne": case "rge": case "rgt": case "rle": case "rlt":
            {
                string jop = code switch
                { "req" => "je", "rne" => "jne", "rge" => "jae",
                  "rgt" => "ja", "rle" => "jbe", _ => "jb" };
                Genop("pxor", "xmm0", "xmm0"); Genop("ucomisd", "ra", "xmm0");
                Genop(jop, Getarg(i1)); Opdone(); return;
            }

            // --- compare-and-branch ---
            case "ceq": Memmem(); Genop("cmp", Getarg(i1), Getarg(i2)); Genop("je", Getarg(i3)); Opdone(); return;
            case "cne": Memmem(); Genop("cmp", Getarg(i1), Getarg(i2)); Genop("jnz", Getarg(i3)); Opdone(); return;

            // --- nonzero/zero byte branches ---
            case "nzb":
                if (IsReg(i1)) { Genop("test", Getarg(i1), Getarg(i1)); Genop("jnz", Getarg(i2)); }
                else { Genop("cmp", Getarg(i1), "0"); Genop("jnz", Getarg(i2)); }
                Opdone(); return;
            case "zrb":
                if (IsReg(i1)) { Genop("test", Getarg(i1), Getarg(i1)); Genop("jz", Getarg(i2)); }
                else { Genop("cmp", Getarg(i1), "0"); Genop("jz", Getarg(i2)); }
                Opdone(); return;
            case "zgb": Genop("nop"); Opdone(); return;

            case "ssl": case "sss": Genop(); Opdone(); return;

            // --- math intrinsics: call <op>_ ---
            case "atn": case "chp": case "cos": case "etx":
            case "lnf": case "sin": case "sqr": case "tan":
                Genop("call", code + "_"); Opdone(); return;

            // --- char-pointer prep (cfp_b==cfp_c path) ---
            case "psc": case "plc":
            {
                string t1 = Getarg(i1);
                int t2 = i2.Type;
                if (IsReg(i2) || (t2 >= 1 && t2 <= 2))
                {
                    Genop("lea", t1, "[cfp_f+" + t1 + "+" + Getarg(i2) + "]");
                    Opdone(); return;
                }
                Genop("add", t1, "cfp_f");
                if (i2.Type == 0) { Opdone(); return; }
                Genop("add", t1, Getarg(i2)); Opdone(); return;
            }
            case "csc":
                if (string.IsNullOrEmpty(thislabel)) return;   // ident(thislabel) -> opnext
                Genop(); Opdone(); return;

            // --- block moves (cfp_b==cfp_c paths) ---
            case "mvc":
                Genlab();                                       // t1=genlab() (counter only)
                Genop("rep"); Genop("movs_b"); Opdone(); return;
            case "mvw":
                Genop("shr", "wa", "log_cfp_b"); Genop("rep", "movs_w"); Opdone(); return;
            case "mwb":
                Genop("shr", "wa", "log_cfp_b"); Genop("std");
                Genop("lea", "xl", "[xl-cfp_b]"); Genop("lea", "xr", "[xr-cfp_b]");
                GenRep("movs_w"); Genop("cld"); Opdone(); return;
            case "mcb":
                Genop("std"); Genop("dec", "xl"); Genop("dec", "xr");
                GenRep("movs_b"); Genop("cld"); Opdone(); return;

            case "icp": Genop("add", "r13", "cfp_b"); Opdone(); return;
            case "lsh": Genop("shl", Getarg(i1), Getarg(i2)); Opdone(); return;
            case "rsh": Genop("shr", Getarg(i1), Getarg(i2)); Opdone(); return;

            case "chk":
            {
                string t1 = Genlab();
                Genop("cmp", "xs", "m_word [lowspminx]"); Genop("jae", t1);
                Genop("mov", "m_word [lowspminx]", "xs");
                Genop("cmp", "xs", "m_word [lowspmin]"); Genop("jb", "sec06");
                Genopl(t1 + ":"); Opdone(); return;
            }

            case "cvd":
                Genop("mov", "r10", "ia"); Genop("mov", "w0", "7378697629483820647");
                Genop("imul", "r10"); Genop("mov", "w0", "r10"); Genop("sar", "w0", "63");
                Genop("sar", "wc", "2"); Genop("sub", "wc", "w0"); Genop("lea", "w0", "[wc+wc*4]");
                Genop("mov", "ia", "wc"); Genop("add", "w0", "w0"); Genop("sub", "w0", "r10");
                Genop("add", "w0", "48"); Genop("mov", "wa", "w0"); Opdone(); return;

            case "cvm":
            {
                string t1 = Getarg(i1);
                Genop("mov", "w0", "ia"); Genop("imul", "w0", "10"); Genop("jo", t1);
                Genop("sub", "wb", "ch_d0"); Genop("sub", "w0", "wb");
                Genop("mov", "ia", "w0"); Genop("jo", t1); Opdone(); return;
            }

            case "cmc":
            {
                Genop("repe", "cmps_b"); Genop("mov", "xl", "0"); Genop("mov", "xr", "xl");
                string t1 = Getarg(i1), t2 = Getarg(i2);
                if (t1 == t2) { Genop("jnz", t1); Opdone(); return; }
                Genop("ja", t2); Genop("jb", t1); Opdone(); return;
            }

            case "lch": GLch(); return;
            case "sch": GSch(); return;
            case "trc": GTrc(); return;
            case "flc": GFlc(); return;

            case "ent":
            {
                string t1 = i1.Text;
                Genop("align", "2");
                if (t1.Length > 0) Genop("db", t1); else Genop("nop");
                Genopl(thislabel);
                thislabel = "";
                Opdone(); return;
            }

            case "enp":
            case "rtn": Genop(); Opdone(); return;

            case "mnz": Genop("mov", Getarg(i1), "xs"); Opdone(); return;

            case "mov": GMov(); return;

            // --- branch switch family ---
            case "bsw": GBsw(); return;
            case "iff": Genop("d_word", Getarg(i2)); Opdone(); return;
            case "esw": Genop("segment .text"); Opdone(); return;

            case "equ": Genopl(thislabel, "equ", i1.Text); Opdone(); return;
            case "dac": GDac(); return;
            case "dic": Genopl(thislabel, "d_word", i1.Text); Decend(); return;
            case "dbc": Genopl(thislabel, "d_word", Getarg(i1)); Opdone(); return;
            case "dca": Genop("sub", Getarg(i1), "cfp_b"); Opdone(); return;
            case "dcv": Genop("dec", Getarg(i1)); Opdone(); return;
            case "dtc": GDtc(); return;
            case "drc": GDrc(); return;

            // --- simple two-operand ALU (memmem + genop opc,getarg,getarg) ---
            case "add": Memmem(); Genop("add", Getarg(i1), Getarg(i2)); Opdone(); return;
            case "sub": Memmem(); Genop("sub", Getarg(i1), Getarg(i2)); Opdone(); return;

            // --- end of assembly ---
            case "end": GEnd(); return;

            // --- branch family ---
            case "brn":
            case "bri": Genop("jmp", Getarg(i1)); Opdone(); return;

            case "beq": case "bne": case "bgt": case "bge":
            case "blt": case "ble": case "blo": case "bhi":
                Memmem();
                Genop("cmp", Getarg(i1), Getarg(i2));
                Genop(Branchtab[code], Getarg(i3));
                Opdone(); return;

            case "bnz":
                if (IsReg(i1)) { Genop("test", Getarg(i1), Getarg(i1)); Genop("jnz", Getarg(i2)); }
                else { Genop("cmp", Getarg(i1), "0"); Genop("jnz", Getarg(i2)); }
                Opdone(); return;

            case "bze":
                if (IsReg(i1)) { string t = Getarg(i1); Genop("test", t, t); Genop("jz", Getarg(i2)); }
                else { Genop("cmp", Getarg(i1), "0"); Genop("jz", Getarg(i2)); }
                Opdone(); return;

            case "bct":
                Genop("dec", Getarg(i1)); Genop("jnz", Getarg(i2)); Opdone(); return;

            case "bod":
            {
                string t1 = Getarg(i1);
                if (i1.Type == 8) t1 = Reglow(t1);
                Genop("test", t1, "1"); Genop("jne", Getarg(i2)); Opdone(); return;
            }
            case "bev":
            {
                string t1 = Getarg(i1);
                if (i1.Type == 8) t1 = Reglow(t1);
                Genop("test", t1, "1"); Genop("je", Getarg(i2)); Opdone(); return;
            }

            default:
                NotImplemented.TryGetValue(code, out int n);
                NotImplemented[code] = n + 1;
                // emit nothing; flush any pending label so output stays aligned
                Opdone();
                return;
        }
    }

    // opdone: flush()  :(opnext)
    private void Opdone() => Flush();

    // decend: (label public handling omitted) :(opdone)
    private void Decend()
    {
        if (!string.IsNullOrEmpty(thislabel) && thislabel.EndsWith(":"))
            thislabel = thislabel.Substring(0, thislabel.Length - 1);
        Opdone();
    }

    // ============================================================
    //  memmem() -- if both operands in memory, load op2 into w0 first
    // ============================================================
    private void Memmem()
    {
        if (!IsMem(i1)) return;
        if (!IsMem(i2)) return;
        Genop("mov", "w0", Getarg(i2));
        i2 = new Operand(8, "w0");
    }

    // ============================================================
    //  handlers
    // ============================================================
    private void GSec()
    {
        Genop("");                       // blank separator
        sectnow++;
        switch (sectnow)
        {
            case 1: Genop("segment .text"); Genop("global", "sec01"); Genopl("sec01:"); break;
            case 2: Genop("segment .data"); Genop("global", "sec02"); Genopl("sec02:"); break;
            case 3: Genop("segment .data"); Genop("global", "sec03"); Genopl("sec03:"); break;
            case 4:
                Genop("global", "esec03"); Genopl("esec03:");
                Genop("segment .data"); Genop("global", "sec04"); Genopl("sec04:"); break;
            case 5:
                Genop("global", "esec04"); Genopl("esec04:");
                if (prcCount1 > 0) Genopl("prc_:", "times", prcCount1 + " dq 0");
                Genop("global", "lowspmin"); Genopl("lowspmin:", "d_word", "0");
                Genop("global", "end_min_data"); Genopl("end_min_data:");
                Genop("segment .text"); Genop("global", "sec05"); Genopl("sec05:"); break;
            case 6: Genop("global", "sec06"); Genopl("sec06:", "nop"); break;
            case 7:
                Genop("global", "sec07"); Genopl("sec07:");
                Genopl("err_:", "xchg", "wa", "m_word [" + "_rc_" + "]"); break;
        }
        Opdone();
    }

    private void GMov()
    {
        // i.src = i2 ; i.dst = i1
        string tsrc = i2.Text, tdst = i1.Text;

        if (tsrc == "(xl)+" || tsrc == "(xt)+")           // mov_xlp / mov_xtp
        {
            if (tdst == "(xr)+") { Genop("movs_w"); Opdone(); return; }
            Genop("lods_w");
            if (tdst == "-(xs)") { Genop("push", "w0"); Opdone(); return; }
            Genop("mov", Getarg(i1), "w0"); Opdone(); return;
        }
        if (tsrc == "(xs)+")                               // mov_xsp
        {
            if (tdst == "(xr)+") { Genop("pop", "w0"); Genop("stos_w"); Opdone(); return; }
            Genop("pop", Getarg(i1)); Opdone(); return;
        }
        if (tdst == "(xr)+")                               // mov_xrp
        {
            Genop("mov", "w0", Getarg(i2)); Genop("stos_w"); Opdone(); return;
        }
        if (tdst == "-(xs)")                               // mov_2
        {
            Genop("push", Getarg(i2)); Opdone(); return;
        }
        Memmem();                                          // default
        Genop("mov", Getarg(i1), Getarg(i2));
        Opdone();
    }

    private void GBsw()
    {
        string t1 = Getarg(i1);
        string t2 = Genlab();
        if (i3.Text.Length != 0)                           // default case present
        {
            Genop("cmp", t1, Getarg(i2));
            Genop("jge", Getarg(i3));
        }
        Genop("jmp", "m_word [" + t2 + "+" + t1 + "*cfp_b]");
        Genop("segment .data");
        Genopl(t2 + ":");
        Opdone();
    }

    private void GenRep(string op)
    {
        string l1 = Genlab(), l2 = Genlab();
        Genopl(l1 + ":");
        Genop("or", "wa", "wa");
        Genop("jz", l2);
        Genop(op);
        Genop("dec", "wa");
        Genop("jmp", l1);
        Genopl(l2 + ":");
    }

    private static string ExtractParenReg(string t2)
    {
        int p = t2.IndexOf('(');
        return t2.Substring(p + 1, 2);
    }

    private void GLch()
    {
        string t2 = i2.Text;
        string t1 = Getarg(i1);
        if (t2.StartsWith("-"))                              // predecrement
        {
            string r = ExtractParenReg(t2);
            Genop("dec", Register(r) ?? r);
        }
        string t3 = ExtractParenReg(t2);
        Genop("xor", "w0", "w0");
        Genop("mov", "al", "m_char [" + (Register(t3) ?? t3) + "]");
        Genop("mov", t1, "w0");
        if (t2.EndsWith("+")) Genop("inc", Register(t3) ?? t3);
        Opdone();
    }

    private void GSch()
    {
        string t2 = i2.Text;
        if (i1.Type == 8)                                    // g_scg_w: work-register source
        {
            string t1 = Reglow(Getarg(i1));
            switch (t2)
            {
                case "(xl)":  Genop("mov", "m_char [xl]", t1); break;
                case "-(xl)": Genop("dec", "xl"); Genop("mov", "m_char [xl]", t1); break;
                case "(xl)+": Genop("mov", "m_char [xl]", t1); Genop("inc", "xl"); break;
                case "(xr)":  Genop("mov", "m_char [xr]", t1); break;
                case "-(xr)": Genop("dec", "xr"); Genop("mov", "m_char [xr]", t1); break;
                case "(xr)+": Genop("mov", "al", t1); Genop("stos_b"); break;
            }
            Opdone(); return;
        }
        string t1b = Getarg(i1);
        if (t2.StartsWith("-"))                              // g_scg_0 predecrement
        {
            string r = ExtractParenReg(t2);
            Genop("dec", Register(r) ?? r);
            Genop("dec", Register(r) ?? r);                  // cfp_b==cfp_c
        }
        string t3 = ExtractParenReg(t2);
        Genop("mov", "w0", t1b);
        Genop("mov", "[" + (Register(t3) ?? t3) + "]", "al");
        if (t2.EndsWith("+")) Genop("inc", Register(t3) ?? t3);
        Opdone();
    }

    private void GFlc()
    {
        // (asm.sbl prints a "flc not supported" warning to stdout, then emits code)
        string t1 = Reglow(Getarg(i1));        // cfp_b==cfp_c -> reglow(getarg(i1))
        string t2 = Genlab();
        Genop("cmp", t1, "'A'");
        Genop("jb", t2);
        Genop("cmp", t1, "'Z'");
        Genop("ja", t2);
        Genop("add", t1, "32");
        Genopl(t2 + "             :");          // 13 spaces before ':' (verbatim)
        Opdone();
    }

    private void GTrc()
    {
        Genop("xchg", "xl", "xr");
        string t1 = Genlab();
        Genopl(t1 + ":", "movzx", "w0", "m_char [xr]");
        Genop("mov", "al", "[xl+w0]");
        Genop("stosb");                                      // stos + op_c('b')
        Genop("dec", "wa");
        Genop("jnz", t1);
        Genop("xor", "xl", "xl");
        Genop("xor", "xr", "xr");
        Opdone();
    }

    private void GRunder()
    {
        Genop("stmxcsr", "[mxcsr]");
        Genop("test", "m_word [mxcsr]", "0x0010");
        string l1 = Genlab();
        Genop("jz", l1);
        Genop("pxor", "ra", "ra");
        Genop("ldmxcsr", "[mxcsr_set]");
        Genopl(l1 + ":");
        Opdone();
    }

    private void GExi()
    {
        Getarg(i1);                          // t1 (side effects only; unused in output)
        string t2 = prcType;
        string t3 = i1.Text;
        if (t2 != "n" && (int.TryParse(prcArgs, out int pa) ? pa : 0) == 0)
        { Genop("ret"); Opdone(); return; }
        if (t3.Length == 0) t3 = "0";
        string rc = (int.TryParse(t3, out int v) ? v : 0).ToString();   // +t3
        Genop("mov", "m_word [_rc_]", rc);
        if (t2 == "n")                       // g_exi.1
        {
            Genop("mov", "w0", "m_word [" + Prcent(prcCount) + "]");
            Genop("jmp", "w0"); Opdone(); return;
        }
        Genop("ret"); Opdone();
    }

    private void GJsr()
    {
        string jsrProc = Getarg(i1);
        Genop("call", jsrProc);
        ppmCases.TryGetValue(jsrProc, out string? jc);
        jsrCount = int.TryParse(jc, out int v) ? v : 0;
        if (jsrCount == 0) { Opdone(); return; }   // eq(jsr_count) -> just the call
        jsrCalls++;
        jsrLabelNorm = "call_" + jsrCalls;
        Genop("dec", "m_word [_rc_]");
        Genop("js", jsrLabelNorm);
        Opdone();
    }

    private void GErr(bool isPpm)
    {
        string t1 = Getarg(i1);
        // err: count/errfile/max bookkeeping writes to the .err file only (skipped)
        labNext = Genlab();
        Genop("dec", "m_word [_rc_]");
        Genop("jns", labNext);
        if (isPpm)
        {
            if (i1.Text.Length == 0)                 // g_ppm.2: unexpected ppm branch
            {
                Genop("mov", "m_word [_rc_]", "299"); Genop("jmp", "err_");
            }
            else Genop("jmp", Getarg(i1));           // g_ppm.loop.ppm
        }
        else                                          // g_ppm.loop.err
        {
            string rc = (int.TryParse(t1, out int v) ? v : 0).ToString();
            Genop("mov", "m_word [_rc_]", rc); Genop("jmp", "err_");
        }
        // g_ppm.loop.next
        Genopl(labNext + ":");
        jsrCount--;
        if (jsrCount == 0) Genopl(jsrLabelNorm + ":");
        Opdone();
    }

    private void GDtc()
    {
        // strip the delimiter chars (first + last), quote each char, comma-join,
        // pad with ,0 up to a multiple of cfp_c (8) chars per word.
        const int cfp_c = 8;
        string raw = i1.Text;
        string t2 = raw.Length >= 2 ? raw.Substring(1, raw.Length - 2) : "";
        int t3 = t2.Length % cfp_c;
        var sb = new StringBuilder();
        for (int k = 0; k < t2.Length; k++)
        {
            if (k > 0) sb.Append(',');
            sb.Append('\'').Append(t2[k]).Append('\'');
        }
        string t4 = sb.ToString();
        if (t3 != 0) t4 += string.Concat(System.Linq.Enumerable.Repeat(",0", cfp_c - t3));
        Genopl(thislabel, "d_char", t4);
        Opdone();
    }

    private void GDac()
    {
        // t2 prefix logic is commented out in asm.sbl -> empty
        Genopl(thislabel, "d_word", i1.Text);
        Decend();
    }

    private void GDrc()
    {
        Genop("align", "8");
        string t1 = i1.Text;
        if (t1.StartsWith("+")) t1 = t1.Substring(1);   // fence "+" : strip a leading sign
        Genop("d_real", t1);
        // attach label to the last instruction (the d_real), not the align
        cstmts[cstmts.Count - 1].Label = thislabel;
        thislabel = "";
        Opdone();
    }

    private void GEnd()
    {
        Genop("section .note.GNU-stack noalloc noexec nowrite progbits");
        Flush();
        finished = true;
        // (trailer copy from hdr file handled by the build, not here)
    }
}

