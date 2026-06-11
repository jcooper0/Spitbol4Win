## Runtime sources included
- `err.asm` and `int.asm` (original, unmodified NASM from the Linux tree) now ship
  in the project directory so the working folder is self-contained. Put your
  `sbl.min` alongside them and run with no arguments; outputs go to `runtime/`.
- `int.asm` is transformed at runtime by `IntPrep` (no need to pre-edit it).
- ml64-confirmed: sbl_masm.asm, err_masm.asm, int_masm.asm all assemble cleanly
  (Microsoft Macro Assembler 14.51). Use distinct object names when assembling:
  `ml64 /c /Fo sbl.obj runtime\sbl_masm.asm` etc. (sbl.obj / err.obj / int.obj).

# SpitbolBuild — ml64 error fixes

All changes are confined to `Nasm2Masm.cs` and `masm.h`. `MinimalLexer.cs`,
`MinimalCodeGen.cs`, and `Program.cs` are unchanged, and `sbl.lex` / `sbl.asm`
regenerate byte-identical — so the codegen is untouched; only the
NASM→MASM post-pass and the prepended header changed.

The ~100+ `ml64` errors collapsed to five root causes.

## 1. Bare `rep` prefix  (A2008 "syntax error : rep")
NASM allows `rep` alone on a line with the string op on the next line; MASM
requires them on one line. The converter now merges a lone
`rep`/`repe`/`repz`/`repne`/`repnz` line with the following instruction
(`rep movs_b` → `rep movsb`).

## 2. Data label colliding with a mnemonic  (A2008 "syntax error : dq")
`cmpss: d_word 0` → after the colon strip becomes `cmpss dq 0`, which MASM
parses as the SSE instruction `cmpss`. The converter now carries a reserved-
mnemonic table, detects any data label that collides, and renames it
whole-word with a `_` suffix throughout the file (`cmpss` → `cmpss_`). New
collisions in future `sbl.min` revisions are handled automatically.

## 3. Address-of operands MASM can't take as an immediate  (A2070 / A2084)
A 64-bit address is only legal as an immediate in `mov r64, imm64`. So
`mov reg, m_addr S` is fine and left as-is, but `mov [mem], m_addr S`,
`cmp`, `add`, `sub`, and `push m_addr S` all fail. Those ~584 sites now
materialize the address through **r11** first, e.g.

    push m_addr ndabb        ->   mov  r11,m_addr ndabb
                                  push r11

    cmp  xr,m_addr nulls     ->   mov  r11,m_addr nulls
                                  cmp  xr,r11

`r11` is never used by the generated body and is Win64-volatile, so it is a
safe scratch with no liveness concerns (`mov` doesn't disturb flags, so the
following conditional branch after a rewritten `cmp` is unaffected).

## 4. Scalar-SSE memory operands without a size  (A2070)
The generator emits `movsd ra,[(cfp_b*rcval)+xr]` with no size word; the
integer ops carry `m_word`, which is why only the SSE ones failed. The
converter now inserts `m_real` (= QWORD PTR) on scalar-SSE ops whose operand
is a bare bracket, skipping the `ngr` lines that already have it.
Also: bare `byte [x]` → `BYTE PTR [x]` (hardening).

## 5. Missing runtime EXTERNs  (surfaced once the syntax errors cleared)
Sixteen runtime symbols are referenced by the body but only declared by the
prepended header. This copy of `masm.h` had dropped the runtime-extern block;
it is restored, complete and typed by usage:

    PROC : do_dvi do_rmi do_chk_real_inf sin_ cos_ tan_ atn_ sqr_ lnf_ etx_ chp_
    DWORD: mxcsr mxcsr_set
    QWORD: _rc_ zeron lowspminx

These were masked behind the syntax-error cascade -- both ml64 (stops at 100
errors) and JWasm (stops its symbol pass early) only reveal them in layers as
earlier errors are fixed. The set above was enumerated by assembling to
convergence (no new undefined symbols on the final pass), so it is complete
for the current `sbl.min`. Add to the runtime block in `masm.h` if the body
later references more.

## 6. Standalone labels in `.DATA` (`sec02:`, `_l0001:`)
A bare `name:` is a near code label; some MASM-compatible assemblers reject it
inside a `.DATA` segment. The converter now tracks segment state and re-emits
such labels as `name LABEL BYTE` (accepted by ml64 and JWasm alike, equivalent
for address use).

## 7. `m_addr` on an EQU constant — `OFFSET <const>` is illegal  (A2098)
MINIMAL `=X` means "immediate value of X". For a memory label that is its
address (needs `OFFSET`); for an EQU constant it is just the number, and
`OFFSET <number>` is rejected by ml64 (A2098: invalid operand for OFFSET).
NASM's empty `m_addr` handled both; the uniform `m_addr -> OFFSET` did not.
The converter now collects all EQU names and, when an `=X` target is an EQU
constant, drops `m_addr` entirely so the plain immediate form is emitted
(`mov wb, rnsi_` rather than `mov wb, OFFSET rnsi_`). Label targets keep the
OFFSET / r11 handling from fixes 3.

NOTE: JWasm does not flag this — it treats `OFFSET <const>` as the constant's
value under every strict flag (`-Zne`, `-Zg`, `-Zm`), which is why the prior
build passed JWasm clean yet failed ml64. JWasm remains a good check for
structure and symbol resolution, but ml64 is the authority on OFFSET strictness.

## Verification
Verified with JWasm (`-win64`), which reproduced the original ml64 syntax
errors and, after the fixes, completes every pass with 0 warnings / 0 errors
and emits `sbl.obj`. `sbl.lex` and `sbl.asm` regenerate byte-identical. Fix 7
was driven directly by ml64's A2098 output and removes 100% of OFFSET-on-
constant sites (verified by inspection, since JWasm cannot see them).

The real confirmation is on your side:

    ml64 /c /Fo sbl.obj sbl_masm.asm

`sbl_masm.asm` in this folder is already regenerated and ready to assemble.
To rebuild from source instead:

    dotnet build SpitbolBuild.csproj -c Debug
    SpitbolBuild.exe sbl.min .
