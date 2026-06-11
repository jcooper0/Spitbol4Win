// ============================================================
//  MinimalLexer.cs  -- Component 1 of the C# MINIMAL translator.
//  Faithful port of lex.sbl: parses MINIMAL statements, validates
//  operands, assigns each a gross type (1-27), handles conditional
//  assembly (.def/.undef/.if/.then/.else/.fi) and equ evaluation,
//  and writes the tokenized intermediate file (sbl.lex).
//
//  Output line contract (from lex.sbl outstmt), '|' = sepchar:
//      |label|opcode|typ1,op1|typ2,op2|typ3,op3|comment|nlines
//  A type field is omitted (just "op") when that operand has no type.
//
//  Method and table names mirror lex.sbl so the two can be diffed
//  and debugged side by side. Tables are transcribed VERBATIM from
//  lex.sbl -- do not regenerate them.
//
//  Usage:  MinimalLexer <in.min> <out.lex>
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace SpitbolTools;

public sealed class MinimalLexer
{
    private const char Sep = '|';                  // sepchar
    private const string MinLets =
        "abcdefghijklmnopqrstuvwxy_zABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Nos = "0123456789";

    // ---- counters (mirror lex.sbl) ----
    private int nstmts, nlines, noutlines, ntarget, nerrors;
    private int? lasterror;

    // ---- per-statement parse results ----
    private string label = "", opcode = "", comment = "";
    private string op1 = "", op2 = "", op3 = "";
    private string typ1 = "", typ2 = "", typ3 = "";   // "" means null (no type)
    private string thisline = "";
    private int argerrs;

    // ---- section tracking ----
    private int sectnow;   // null in SNOBOL -> 0 here; each 'sec' increments

    // ---- tables ----
    private readonly Dictionary<string, int> ityptab = new();
    private readonly Dictionary<string, int> opformtab = new();
    private readonly bool[,] validform = new bool[19, 28];   // [1..18, 1..27]
    private readonly Dictionary<string, string> optab = new();
    private readonly Dictionary<string, BigInteger> equates = new();
    private readonly Dictionary<string, BigInteger> equDefs = new();
    private readonly Dictionary<string, int> labtab = new();
    private readonly Dictionary<string, int> prctab = new();

    // ---- conditional assembly ----
    private readonly HashSet<string> symtbl = new();
    private readonly List<(int result, int mode)> statestk = new();
    private int level;
    private const int False = 0, True = 1, Bypass = 2;
    private const int ElseMode = 0, ThenMode = 1;

    // bsw/esw state
    private int bswFlag;
    private BigInteger bswUb;
    private string dplbl = "";
    private string[]? iffar;            // iffar[index] = "type,op|type,op|comment"

    private readonly TextWriter outFile;

    public MinimalLexer(TextWriter output) { outFile = output; Init(); }

    // ============================================================
    //  init() -- build all tables (verbatim from lex.sbl)
    // ============================================================
    private void Init()
    {
        // ityptab (lex.sbl 113-131)
        ityptab["0"] = 1; ityptab["1"] = 1;
        ityptab["wa"] = 8; ityptab["wb"] = 8; ityptab["wc"] = 8;
        ityptab["xl"] = 7; ityptab["xr"] = 7; ityptab["xs"] = 7; ityptab["xt"] = 7;
        ityptab["(xl)"] = 9; ityptab["(xr)"] = 9; ityptab["(xs)"] = 9; ityptab["(xt)"] = 9;
        ityptab["-(xl)"] = 11; ityptab["-(xr)"] = 11; ityptab["-(xs)"] = 11; ityptab["-(xt)"] = 11;
        ityptab["(xl)+"] = 10; ityptab["(xr)+"] = 10; ityptab["(xs)+"] = 10; ityptab["(xt)+"] = 10;

        // opformtab (lex.sbl 135-137)
        InitMap(opformtab,
            "val[1]reg[2]opc[3]ops[4]opw[5]opn[6]opv[7]addr[8]" +
            "x[9]w[10]plbl[11](x)[12]integer[13]real[14]" +
            "dtext[15]eqop[16]int[17]pnam[18]");

        // validform[18,27] sparse matrix (lex.sbl 145-160)
        foreach (var (i, j) in new (int, int)[]
        {
            (1,1),(1,2),(2,7),(2,8),(3,9),(3,10),(3,11),(4,3),(4,4),(4,9),
            (4,12),(4,13),(4,14),(4,15),(5,3),(5,4),(5,8),(5,9),(5,10),(5,11),
            (5,12),(5,13),(5,14),(5,15),(6,3),(6,4),(6,7),(6,8),(6,9),(6,10),
            (6,11),(6,12),(6,13),(6,14),(6,15),(7,3),(7,4),(7,7),(7,8),(7,9),
            (7,10),(7,11),(7,12),(7,13),(7,14),(7,15),(7,18),(7,19),(7,20),(7,21),
            (7,22),(8,1),(8,2),(8,3),(8,4),(8,5),(8,6),(9,7),(10,8),(11,6),
            (12,9),(13,16),(14,17),(15,27),(16,24),(17,1),(18,6),(18,23),
        })
            validform[i, j] = true;

        // optab (lex.sbl 197-230)
        InitOptab(
            "flc[w]add[opn,opv]adi[ops]adr[ops]anb[w,opw]aov[opv,opn,plbl]atn[none]" +
            "bod[opn,plbl]bev[opn,plbl]" +
            "bct[w,plbl]beq[opn,opv,plbl]bge[opn,opv,plbl]bgt[opn,opv,plbl]" +
            "bhi[opn,opv,plbl]ble[opn,opv,plbl]blo[opn,opv,plbl]" +
            "blt[opn,opv,plbl]bne[opn,opv,plbl]bnz[opn,plbl]brn[plbl]" +
            "bri[opn]bsw[x,val,*plbl bsw]btw[reg]" +
            "bze[opn,plbl]ceq[ops,ops,plbl]" +
            "chk[none]chp[none]cmb[w]cmc[plbl,plbl]cne[ops,ops,plbl]cos[none]" +
            "csc[x]ctb[w,val]ctw[w,val]cvd[none]cvm[plbl]dac[addr]dbc[val]dca[opn]" +
            "dcv[opn]def[def]dic[integer]drc[real]dtc[dtext]dvi[ops]dvr[ops]ejc[none]" +
            "else[else]end[none end]enp[none]ent[*val ent]equ[eqop equ]" +
            "erb[int,text erb]err[int,text err]esw[none esw]etx[none]exi[*int]" +
            "exp[int]fi[fi]ica[opn]icp[none]icv[opn]ieq[plbl]if[if]iff[val,plbl iff]" +
            "ige[plbl]igt[plbl]ile[plbl]ilt[plbl]ine[plbl]ino[plbl]inp[ptyp,int inp]" +
            "inr[none]iov[plbl]itr[none]jsr[pnam]lch[reg,opc]lct[w,opv]lcp[reg]" +
            "lcw[reg]ldi[ops]ldr[ops]lei[x]lnf[none]lsh[w,val]lsx[w,(x)]mcb[none]" +
            "mfi[opn,*plbl]mli[ops]mlr[ops]mnz[opn]mov[opn,opv]mti[opn]" +
            "mvc[none]mvw[none]mwb[none]ngi[none]ngr[none]nzb[w,plbl]" +
            "orb[w,opw]plc[x,*opv]ppm[*plbl]prc[ptyp,val prc]psc[x,*opv]req[plbl]" +
            "rge[plbl]rgt[plbl]rle[plbl]rlt[plbl]rmi[ops]rne[plbl]rno[plbl]" +
            "rov[plbl]rsh[w,val]rsx[w,(x)]rti[*plbl]rtn[none]sbi[ops]" +
            "sbr[ops]sch[reg,opc]scp[reg]sec[none sec]sin[none]sqr[none]ssl[opw]" +
            "sss[opw]sti[ops]str[ops]sub[opn,opv]tan[none]then[then]trc[none]" +
            "ttl[none ttl]undef[undef]wtb[reg]xob[w,opw]zer[opn]zgb[opn]zrb[w,plbl]" +
            "zzz[int]");

        // equ_defs (lex.sbl 268-289) -- predefined equate symbols
        InitEqu(
            "nstmx[10]cfp_s[15]cfp_x[3]e_srs[100]e_sts[1000]e_cbs[500]e_hnb[257]" +
            "e_hnw[3]e_fsp[15]e_sed[25]ch_ua[65]ch_ub[66]ch_uc[67]ch_ud[68]ch_ue[69]" +
            "ch_uf[70]ch_ug[71]ch_uh[72]ch_ui[73]ch_uj[74]ch_uk[75]ch_ul[76]" +
            "ch_um[77]ch_un[78]ch_uo[79]ch_up[80]ch_uq[81]ch_ur[82]ch_us[83]" +
            "ch_ut[84]ch_uu[85]ch_uv[86]ch_uw[87]ch_ux[88]ch_uy[89]ch_uz[90]" +
            "ch_d0[48]ch_d1[49]ch_d2[50]ch_d3[51]ch_d4[52]ch_d5[53]ch_d6[54]ch_d7[55]" +
            "ch_d8[56]ch_d9[57]ch_la[97]ch_lb[98]ch_lc[99]ch_ld[100]ch_le[101]ch_lf" +
            "[102]ch_lg[103]ch_lh[104]ch_li[105]ch_lj[106]ch_lk[107]ch_ll[108]" +
            "ch_lm[109]ch_ln[110]ch_lo[111]ch_lp[112]ch_lq[113]ch_lr[114]ch_ls[115]" +
            "ch_lt[116]ch_lu[117]ch_lv[118]ch_lw[119]ch_lx[120]ch_ly[121]ch_l_[122]" +
            "ch_am[38]ch_as[42]ch_at[64]ch_bb[60]ch_bl[32]ch_br[124]ch_cl[58]" +
            "ch_cm[44]ch_dl[36]ch_dt[46]ch_dq[34]ch_eq[61]ch_ex[33]ch_mn[45]ch_nm[35" +
            "]ch_nt[126]ch_pc[37]ch_pl[43]ch_pp[40]ch_rb[62]ch_rp[41]ch_qu[63]" +
            "ch_sl[47]ch_sm[59]ch_sq[39]ch_u_[95]ch_ob[91]ch_cb[93]ch_ht[9]ch_vt[11]" +
            "ch_ey[94]iodel[32]cfp_a[256]cfp_b[8]cfp_c[8]cfp_f[16]cfp_i[1]" +
            "cfp_l[18446744073709551616]cfp_m[9223372036854775807]cfp_n[64]" +
            "cfp_r[1]cfp_u[128]");

        // statestk: 1-based level indexing; slot 0 unused
        statestk.Add((0, 0));
    }

    // initmap: "name[val]name[val]..." ; a missing/blank val repeats the last
    private static void InitMap(Dictionary<string, int> tbl, string s)
    {
        int i = 0; int lastval = 0;
        while (i < s.Length)
        {
            int lb = s.IndexOf('[', i);
            if (lb < 0) break;
            string name = s.Substring(i, lb - i);
            int rb = s.IndexOf(']', lb);
            string valStr = s.Substring(lb + 1, rb - lb - 1);
            int val = valStr.Length == 0 ? lastval : int.Parse(valStr, CultureInfo.InvariantCulture);
            lastval = val;
            tbl[name] = val;
            i = rb + 1;
        }
    }

    private void InitOptab(string s)
    {
        // entries like "mov[opn,opv]" -- store the inside-bracket skeleton
        int i = 0;
        while (i < s.Length)
        {
            int lb = s.IndexOf('[', i);
            if (lb < 0) break;
            string name = s.Substring(i, lb - i);
            int rb = s.IndexOf(']', lb);
            string skel = s.Substring(lb + 1, rb - lb - 1);
            optab[name] = skel;
            i = rb + 1;
        }
    }

    private void InitEqu(string s)
    {
        int i = 0; BigInteger lastval = 0;
        while (i < s.Length)
        {
            int lb = s.IndexOf('[', i);
            if (lb < 0) break;
            string name = s.Substring(i, lb - i);
            int rb = s.IndexOf(']', lb);
            string valStr = s.Substring(lb + 1, rb - lb - 1);
            BigInteger val = valStr.Length == 0 ? lastval : BigInteger.Parse(valStr, CultureInfo.InvariantCulture);
            lastval = val;
            equDefs[name] = val;
            i = rb + 1;
        }
    }

    // ============================================================
    //  Main scan loop (lex.sbl dsout/dostmt)
    // ============================================================
    public void Run(TextReader input)
    {
        string? raw;
        while (true)
        {
            raw = ReadLine(input, out bool eof);
            if (eof) break;
            if (raw == null) continue;     // line consumed by rdline (comment/blank/cond)

            thisline = raw;
            if (!Crack(thisline)) continue;            // syntax error -> reported, skip
            if (label.Length > 0) LabEnter();
            argerrs = 0;

            if (!optab.TryGetValue(opcode, out string? opskel) || opskel == null)
            {
                Error("bad op-code");
                continue;
            }

            // split skeleton into argskel (the forms) and trailing handler word
            string argskel = opskel;
            string handler = "";
            int sp = opskel.IndexOf(' ');
            if (sp >= 0) { argskel = opskel.Substring(0, sp); handler = opskel.Substring(sp + 1); }

            if (argskel != "none")
                ClassifyArgs(argskel);

            if (argerrs > 0) Error("arg type not known");

            // dispatch on the handler word, else default emit (dsgen)
            switch (handler)
            {
                case "":      Dsgen(); break;
                case "bsw":   GBsw(); break;
                case "iff":   GIff(); break;
                case "equ":   GEqu(); break;
                case "esw":   GEsw(); break;
                case "end":   GEnd(); break;
                case "ent":   GEnt(); break;
                case "sec":   GSec(); break;
                case "ttl":   GTtl(); break;
                case "erb":
                case "err":   GErr(); break;
                case "inp":   GInp(); break;
                case "prc":   GPrc(); break;
                default:      Dsgen(); break;   // if/then/else/fi/def/undef handled in ReadLine
            }

            if (finished) break;   // g.end terminates the scan
        }
    }

    // ============================================================
    //  ClassifyArgs -- assign typ1/typ2/typ3 from argskel (dos01..dos05)
    //  argskel like "opn,opv" or "x,val,*plbl"; '*' marks repeatable/optional
    // ============================================================
    private void ClassifyArgs(string argskel)
    {
        // p.argskel1: first form is break(',')|rem ; p.argskel2: skip 1 char then same
        // Effect: split argskel on commas into up to three "argthis" forms.
        var forms = argskel.Split(',');
        string[] ops = { op1, op2, op3 };
        for (int n = 0; n < forms.Length && n < 3; n++)
        {
            string argthis = forms[n];
            // "argthis '*' ident(opN)" : a leading '*' form is optional -- if the
            // corresponding operand is empty, stop classifying.
            bool optional = argthis.StartsWith("*");
            if (optional && ops[n].Length == 0) break;
            if (optional) argthis = argthis.Substring(1);
            // also handle a '*' that trails a form name (e.g. "*plbl")
            argthis = argthis.TrimStart('*');

            int t = ArgType(ops[n], argthis);
            switch (n)
            {
                case 0: typ1 = t == 0 ? "" : t.ToString(); if (t == 0) argerrs++; break;
                case 1: typ2 = t == 0 ? "" : t.ToString(); if (t == 0) argerrs++; break;
                case 2: typ3 = t == 0 ? "" : t.ToString(); if (t == 0) argerrs++; break;
            }
        }
    }

    // ============================================================
    //  argtype(op,typ) -- gross type 1-27, or 0 if invalid (lex.sbl 383)
    // ============================================================
    private int ArgType(string op, string typ)
    {
        typ = typ.Replace("*", "");
        switch (typ)
        {
            case "text":  return 26;
            case "dtext": return 27;
            case "ptyp":
                // "op any('rne')" fails for most ops; succeeds only if op has r/n/e
                return HasAny(op, "rne") ? 25 : 0;
            case "eqop":
                // arg.eqop:  op1 = ident(op,'*') equ_defs[label]
                // If the operand is '*', replace it with the predefined value
                // from equ_defs[label]; otherwise leave the operand text as-is
                // (e.g. "cfp_s+cfp_x" passes through verbatim).
                if (op == "*" && equDefs.TryGetValue(label, out var ed))
                    op1 = ed.ToString();
                return 24;
        }
        int itype = ArgForm(op);
        if (!opformtab.TryGetValue(typ, out int opform)) return 0;
        if (opform >= 1 && opform <= 18 && itype >= 1 && itype <= 27 && validform[opform, itype])
            return itype;
        return 0;
    }

    private static bool HasAny(string s, string set)
    {
        foreach (char c in s) if (set.IndexOf(c) >= 0) return true;
        return false;
    }

    // ============================================================
    //  argform(arg) -- gross type from operand shape (lex.sbl 384)
    // ============================================================
    private int ArgForm(string arg)
    {
        if (ityptab.TryGetValue(arg, out int t0)) return t0;

        // arg p.nos  -> all digits (span(nos) rpos(0))
        if (IsAllNos(arg)) return 1;                          // argform.int

        if (arg.StartsWith("="))                              // argform.eq
        {
            string itypa = arg.Substring(1);
            int it = labtab.TryGetValue(itypa, out int v) ? v : 0;
            if (it == 2) return 18;
            if (it == 6) return 22;
            if (it > 2) return it + 17;
            // fall-through: argform = 22 ; labtab[itypa] = 5
            labtab[itypa] = 5;
            return 22;
        }
        if (arg.StartsWith("*"))                              // argform.star
        {
            string rest = arg.Substring(1);
            if (labtab.TryGetValue(rest, out int v) && v == 2) return 19;
            return 0;
        }
        if (arg.Length > 0 && (arg[0] == '+' || arg[0] == '-'))   // signed
        {
            string rest = arg.Substring(1);
            if (IsAllNos(rest)) return 16;                    // argform.snum
            if (IsReal(rest)) return 17;                      // argform.sreal
            return 0;
        }
        int paren = arg.IndexOf('(');
        if (paren >= 0)                                       // argform.index
        {
            // arg break('(') . t '(x' any('lrst') ')' rpos(0)
            string t = arg.Substring(0, paren);
            string tail = arg.Substring(paren);
            if (tail.Length == 4 && tail[0] == '(' && tail[1] == 'x'
                && "lrst".IndexOf(tail[2]) >= 0 && tail[3] == ')')
            {
                if (IsAllNos(t)) return 12;
                if (labtab.TryGetValue(t, out int tv))
                {
                    if (tv == 2) return 13;
                    if (tv == 3) return 15;
                    if (tv == 4) return 14;
                }
            }
            return 0;
        }
        // plain label. In lex.sbl, labtab returns null when unset, and
        // argform.plbl treats that as "fresh plabel": set it to 6 and return 6.
        // A stored 0 (from a labenter while sectnow<2) is equivalent to unset.
        if (labtab.TryGetValue(arg, out int lt) && lt != 0) return lt;
        labtab[arg] = 6;                                      // argform.plbl
        return 6;
    }

    private static bool IsAllNos(string s)
    {
        if (s.Length == 0) return false;
        foreach (char c in s) if (Nos.IndexOf(c) < 0) return false;
        return true;
    }

    // p.real = span(nos) '.' (span(nos)|null) (exp|null) rpos(0)
    private static bool IsReal(string s)
    {
        int dot = s.IndexOf('.');
        if (dot <= 0) return false;
        for (int k = 0; k < dot; k++) if (Nos.IndexOf(s[k]) < 0) return false;
        return true; // sufficient for classification (16 vs 17)
    }

    // ============================================================
    //  labenter() -- record label's section (lex.sbl 406)
    // ============================================================
    private void LabEnter()
    {
        if (label.Length == 0) return;
        int v = sectnow == 2 ? 2 : sectnow == 3 ? 4 : sectnow == 4 ? 3 : sectnow > 4 ? 6 : 0;
        // lex.sbl assigns null (not 0) for sectnow<2; leave the entry unset so
        // argform later treats the symbol as a fresh plabel.
        if (v != 0) labtab[label] = v;
    }

    // ============================================================
    //  crack(line) -- split into label/opcode/op1-3/comment (lex.sbl 285)
    //  Returns false on syntax error.
    // ============================================================
    private bool Crack(string line)
    {
        nstmts++;
        label = opcode = comment = "";
        op1 = op2 = op3 = typ1 = typ2 = typ3 = "";

        if (!CsParse(line)) { Error("source line syntax error"); return false; }

        if (opcode == "dtc")
        {
            // p.csdtc handled inside CsParse for dtc; op1 already set
            return true;
        }
        // split the operand field on commas (p.csoperand)
        if (_operands.Length > 0)
        {
            var parts = SplitOperands(_operands);
            if (parts.Count > 0) op1 = parts[0];
            if (parts.Count > 1) op2 = parts[1];
            if (parts.Count > 2) op3 = parts[2];
        }
        return true;
    }

    private string _operands = "";

    private static List<string> SplitOperands(string s)
    {
        var list = new List<string>();
        int i = 0;
        while (i < s.Length && list.Count < 3)
        {
            int c = s.IndexOf(',', i);
            if (c < 0) { list.Add(s.Substring(i)); break; }
            list.Add(s.Substring(i, c - i));
            i = c + 1;
        }
        return list;
    }

    // ============================================================
    //  p.csparse -- fixed-column statement parser (lex.sbl 167)
    //  Two shapes:
    //    (a) label(0-5)  + 2 spaces + opcode(3) + operands/comment
    //    (b) '.' label  cond-opcode  ...   (conditional-assembly form)
    //  Returns false on no match.
    // ============================================================
    private bool CsParse(string line)
    {
        if (line.StartsWith("."))
        {
            // conditional-assembly statements are intercepted in ReadLine;
            // if one reaches here it is a syntax error in this port.
            return false;
        }

        // label: chars 0.. up to a 5-char field, then "  " then 3-char opcode.
        // p.minlabel = 2-5 chars OR a 5-blank field (no label).
        // Layout: positions 0-4 label field, 5-6 = "  ", 7-9 = opcode.
        if (line.Length < 7) return false;

        string labField = line.Length >= 5 ? line.Substring(0, 5) : line.PadRight(5);
        label = labField.TrimEnd();
        // a label must match p.minlabel (2-5 of minlets/nos) or be blank
        if (label.Length > 0 && !IsMinLabel(label)) return false;

        // expect two spaces at 5-6
        if (line[5] != ' ' || line[6] != ' ') return false;
        if (line.Length < 10) { opcode = line.Substring(7).Trim(); _operands = ""; comment = ""; return opcode.Length > 0; }
        opcode = line.Substring(7, 3);

        // remainder after opcode: "  operands  comment" OR "operands+comment"
        string rest = line.Length > 10 ? line.Substring(10) : "";

        if (opcode == "dtc")
        {
            // p.csdtc: label then len(7) then delimited char then break to comment.
            // Simplify: operand is the quoted/char field; pass through to op1.
            // The original uses len(7)(len(1) $ char break(*char) len(1)).
            ParseDtc(line);
            return true;
        }

        // operands: starts after the "  " that follows the opcode (if present)
        if (rest.StartsWith("  "))
        {
            string body = rest.Substring(2);
            // operands = break(' ')|rem ; then optional spaces; rest = comment
            int spc = body.IndexOf(' ');
            if (spc < 0) { _operands = body; comment = ""; }
            else
            {
                _operands = body.Substring(0, spc);
                comment = body.Substring(spc).TrimStart(' ');
            }
        }
        else
        {
            // rpos(0).operands.comment : whole remainder is operands, no comment
            _operands = rest.Trim();
            comment = "";
        }
        return true;
    }

    private void ParseDtc(string line)
    {
        // p.csdtc: 5-char label field, then len(7), then a delimited operand
        //   (len(1)$char break(*char) len(1)) -> first char is the delimiter,
        //   operand runs through the matching closing delimiter (inclusive),
        //   then optional spaces and the rest is the comment.
        // So the operand begins at column 12 (5 + 7).
        op1 = ""; comment = "";
        if (line.Length <= 12) return;
        string field = line.Substring(12);
        if (field.Length >= 2)
        {
            char delim = field[0];
            int end = field.IndexOf(delim, 1);
            if (end > 0)
            {
                op1 = field.Substring(0, end + 1);
                comment = field.Substring(end + 1).TrimStart(' ');
                return;
            }
        }
        op1 = field;
    }

    private static bool IsMinLabel(string s)
    {
        if (s.Length < 1 || s.Length > 5) return false;
        // first two must be minlets; rest minlets or nos (per p.minlabel, len 2-5)
        for (int k = 0; k < s.Length; k++)
        {
            bool ok = MinLets.IndexOf(s[k]) >= 0 || (k >= 2 && Nos.IndexOf(s[k]) >= 0);
            if (!ok) return false;
        }
        return true;
    }

    // ============================================================
    //  rdline() + conditional assembly (lex.sbl 430-505)
    //  Returns the next MINIMAL statement, or null if the caller should
    //  loop (line was a comment/blank/conditional that we consumed), and
    //  sets eof=true at end of input (after emitting a synthetic 'end').
    // ============================================================
    private bool emittedEnd;
    private string? ReadLine(TextReader input, out bool eof)
    {
        eof = false;
        string? rd = input.ReadLine();
        if (rd == null)
        {
            if (emittedEnd) { eof = true; return null; }
            emittedEnd = true;
            rd = "       end";   // synthetic end (rl03)
        }
        else
        {
            nlines++;
        }

        if (rd.Length == 0)
        {
            // blank line -> NOT written to the lexeme file (listing only). consume.
            return null;
        }

        char first = rd[0];
        if (first == '.')
        {
            // conditional assembly directive
            HandleCond(rd);
            return null;
        }
        if (first == '*')
        {
            // full-line comment -> NOT written to the lexeme file. consume.
            return null;
        }

        // 'other': honor conditional-assembly skipping
        if (level != 0 && Processrec(top().result, top().mode) == 0)
            return null;   // skipped by inactive .if branch

        return rd;
    }

    private (int result, int mode) top() => statestk[level];

    private static int Processrec(int result, int mode)
    {
        if (result == True && mode == ThenMode) return 1;
        if (result == False && mode == ElseMode) return 1;
        return 0;
    }

    private void HandleCond(string rd)
    {
        // p.condasm: condcmd then condvar
        string body = rd;
        string condcmd, condvar = "";
        int sp = body.IndexOf(' ');
        if (sp < 0) condcmd = body;
        else
        {
            condcmd = body.Substring(0, sp);
            string after = body.Substring(sp).TrimStart(' ');
            int sp2 = after.IndexOf(' ');
            condvar = sp2 < 0 ? after : after.Substring(0, sp2);
        }

        switch (condcmd)
        {
            case ".def":
                if (condvar.Length == 0) { SynErr(rd); return; }
                if (level == 0 || Processrec(top().result, top().mode) != 0)
                    symtbl.Add(condvar);
                break;
            case ".undef":
                if (condvar.Length == 0) { SynErr(rd); return; }
                if (level == 0 || Processrec(top().result, top().mode) != 0)
                    symtbl.Remove(condvar);
                break;
            case ".if":
                if (condvar.Length == 0) { SynErr(rd); return; }
                if (level != 0 && Processrec(top().result, top().mode) == 0)
                {
                    level++; Push((Bypass, ThenMode));
                }
                else
                {
                    level++; Push((symtbl.Contains(condvar) ? True : False, ThenMode));
                }
                break;
            case ".then":
                if (condvar.Length != 0 || level == 0) SynErr(rd);
                break;
            case ".else":
                if (condvar.Length != 0) { SynErr(rd); return; }
                if (level != 0) SetMode(ElseMode); else SynErr(rd);
                break;
            case ".fi":
                if (condvar.Length != 0) { SynErr(rd); return; }
                if (level != 0) level--; else SynErr(rd);
                break;
            default:
                SynErr(rd);
                break;
        }
    }

    private void Push((int, int) st)
    {
        if (level < statestk.Count) statestk[level] = st;
        else statestk.Add(st);
    }
    private void SetMode(int mode)
    {
        var s = statestk[level]; statestk[level] = (s.result, mode);
    }
    private void SynErr(string rd)
    {
        Console.Error.WriteLine($"{nlines}(syntax error): {rd}");
    }

    // ============================================================
    //  Generators (lex.sbl g.* that affect tokenized output)
    // ============================================================
    private void Dsgen() => OutStmt(label, opcode, op1, op2, op3, comment);

    private void GEnt() { labtab[label] = 5; Dsgen(); }      // g.ent
    private void GSec() { sectnow++; Dsgen(); }              // g.sec

    private void GTtl()                                       // g.ttl
    {
        string t = thisline.Length > 10 ? thisline.Substring(10) : "";
        t = t.TrimStart(' ');
        typ1 = ""; typ2 = ""; typ3 = "";
        OutStmtRaw(label: "", opcode: "ttl", o1: "27," + t, o2: "", o3: "", cmt: "");
    }

    private void GErr()                                       // g.erb / g.err
    {
        // thisline break(',') len(1) rem . t  -> text after first comma
        int c = thisline.IndexOf(',');
        string t = c >= 0 ? thisline.Substring(c + 1) : "";
        OutStmt(label, opcode, op1, t, "", "");   // note: op2 position carries text
    }

    private void GInp()                                       // g.inp
    {
        if (label.Length == 0) Error("no label for inp");
        if (prctab.ContainsKey(label)) Error("duplicate inp");
        prctab[label] = 1;     // value is op1's presence; we only need existence
        _prcOp[label] = op1;
        Dsgen();
    }

    private readonly Dictionary<string, string> _prcOp = new();

    private void GPrc()                                       // g.prc
    {
        if (label.Length == 0) Error("no label for prc");
        if (!prctab.ContainsKey(label)) Error("missing inp");
        else if (_prcOp.TryGetValue(label, out var ip) && ip != op1) Error("inconsistent inp/prc");
        prctab.Remove(label); _prcOp.Remove(label);
        Dsgen();
    }

    private void GEnt_unused() { }

    // ---- bsw / iff / esw ----
    private void GBsw()                                       // g.bsw
    {
        BigInteger ub = TryInt(op2, out var v) ? v
                      : equates.TryGetValue(op2, out var e) ? e
                      : throw new InvalidOperationException();
        if (!TryInt(ub.ToString(), out _)) { Error("non-integer lower bound for bsw"); return; }
        bswUb = ub;
        iffar = new string[(int)ub];                 // array('0:ub-1', sep sep)
        for (int k = 0; k < iffar.Length; k++) iffar[k] = Sep.ToString() + Sep;
        dplbl = op3;
        bswFlag = 1;
        Dsgen();
    }

    private void GIff()                                       // g.iff
    {
        if (bswFlag == 0) Error("iff without bsw");
        string ifftyp = TryInt(op1, out _) ? "1" : "2";
        BigInteger iffval = TryInt(op1, out var iv) ? iv
                          : equates.TryGetValue(op1, out var e) ? e : BigInteger.MinusOne;
        if (iffval == BigInteger.MinusOne && ifftyp == "2" && !equates.ContainsKey(op1))
        { Error("non-integer iff value"); return; }
        if (iffar != null && iffval >= 0 && iffval < iffar.Length)
            iffar[(int)iffval] = ifftyp + "," + op1 + Sep + typ2 + "," + op2 + Sep + comment;
    }

    private void GEsw()                                       // g.esw
    {
        if (bswFlag == 0) Error("esw without bsw");
        if (iffar != null)
        {
            for (int idx = 0; idx < iffar.Length; idx++)
            {
                // iffar[idx]: "val|plbl|cmnt" using sep
                var f = iffar[idx].Split(Sep);
                string val = f.Length > 0 ? f[0] : "";
                string plbl = f.Length > 1 ? f[1] : "";
                string cmnt = f.Length > 2 ? f[2] : "";
                if (val.Length == 0) val = "1," + idx;
                if (plbl.Length == 0) plbl = "6," + dplbl;
                if (dplbl.Length == 0 && plbl == "6,")
                    Error("missing iff value: " + val + " without plbl in preceding bsw");
                OutStmtRaw("", "iff", val, plbl, "", cmnt);
            }
        }
        iffar = null; bswFlag = 0; dplbl = "";
        Dsgen();
    }

    private void GEqu()                                       // g.equ
    {
        if (op1 == "*")
        {
            if (equDefs.TryGetValue(label, out var d)) { equates[label] = d; Dsgen(); return; }
        }
        // p.equ.rip: num1 [op] num2 with symbol substitution
        if (!EquRip(op1, out var val)) { Error("equ operand syntax error"); return; }
        equates[label] = val;
        Dsgen();
    }

    private bool EquRip(string expr, out BigInteger result)
    {
        result = 0;
        // ( span(nos) num1 | sym1 ) ( [+-] )? ( span(nos) num2 | sym2 )?
        int i = 0;
        if (!ParseTerm(expr, ref i, out BigInteger a)) return false;
        if (i >= expr.Length) { result = a; return true; }
        char op = expr[i];
        if (op != '+' && op != '-') return false;
        i++;
        if (!ParseTerm(expr, ref i, out BigInteger b)) return false;
        result = op == '+' ? a + b : a - b;
        return true;
    }

    private bool ParseTerm(string s, ref int i, out BigInteger val)
    {
        val = 0;
        int start = i;
        while (i < s.Length && Nos.IndexOf(s[i]) >= 0) i++;
        if (i > start) { val = BigInteger.Parse(s.Substring(start, i - start)); return true; }
        // symbol
        int sstart = i;
        while (i < s.Length && (MinLets.IndexOf(s[i]) >= 0 || Nos.IndexOf(s[i]) >= 0)) i++;
        if (i > sstart)
        {
            string sym = s.Substring(sstart, i - sstart);
            if (equates.TryGetValue(sym, out var v)) { val = v; return true; }
            return false;
        }
        return false;
    }

    private bool finished;

    private void GEnd()                                       // g.end
    {
        OutStmtRaw("", "end", "", "", "", comment);
        if (level != 0) Error("  unclosed if conditional clause");
        finished = true;     // g.end ends the program: :(end)
    }

    private static bool TryInt(string s, out BigInteger v)
        => BigInteger.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v);

    // ============================================================
    //  outstmt -- write one tokenized line (lex.sbl 416). THE contract:
    //  |label|opcode|typ1,op1|typ2,op2|typ3,op3|comment|nlines
    // ============================================================
    private void OutStmt(string lab, string opc, string o1, string o2, string o3, string cmt)
    {
        string f1 = (typ1.Length == 0 ? "" : typ1 + ",") + o1;
        string f2 = (typ2.Length == 0 ? "" : typ2 + ",") + o2;
        string f3 = (typ3.Length == 0 ? "" : typ3 + ",") + o3;
        WriteLexLine(lab, opc, f1, f2, f3, cmt);
    }

    // for generators that pass pre-formatted "type,op" fields directly
    private void OutStmtRaw(string label, string opcode, string o1, string o2, string o3, string cmt)
        => WriteLexLine(label, opcode, o1, o2, o3, cmt);

    private void WriteLexLine(string lab, string opc, string f1, string f2, string f3, string cmt)
    {
        var sb = new StringBuilder();
        sb.Append(Sep).Append(lab).Append(Sep).Append(opc).Append(Sep)
          .Append(f1).Append(Sep).Append(f2).Append(Sep).Append(f3).Append(Sep)
          .Append(cmt).Append(Sep).Append(nlines);
        outFile.Write(sb.ToString());
        outFile.Write('\n');
        ntarget++;
        noutlines++;
    }

    private void Error(string text)
    {
        outFile.Write("* *???* " + thisline); outFile.Write('\n');
        string second = "*       " + text +
            (lasterror.HasValue ? "" : ". last error was line " + lasterror);
        outFile.Write(second); outFile.Write('\n');
        lasterror = noutlines;
        noutlines += 2;
        nerrors++;
    }
}

