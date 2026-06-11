# SPITBOL Windows x64 — build status & roadmap

## Done (assembles clean under ml64 / verified with JWasm -win64)

| object | source | path | status |
|---|---|---|---|
| `sbl.obj`  | `sbl.min` → SpitbolBuild → `sbl_masm.asm` | `sbl_masm.asm` | ✅ ml64-clean |
| `err.obj`  | `bootstrap/err.asm` → converter → `err_masm.asm` | `err_masm.asm` | ✅ assembles 0-err |
| `int.obj`  | `int.asm` → `prep_int.py` → converter → `int_masm.asm` | `int_masm.asm` | ✅ assembles 0-err |

`err.obj`/`int.obj` are verified with JWasm only so far (no ml64 run yet) — assemble
them on your side with:

    conv err.asm  masm.h     err_masm.asm   &&  ml64 /c /Fo err.obj err_masm.asm
    python3 prep_int.py int.asm int_pp.asm
    conv int_pp.asm int_masm.h int_masm.asm &&  ml64 /c /Fo int.obj int_masm.asm

(`conv` = a thin driver over `Nasm2Masm.Convert` that prepends the header. err.asm
uses the same `masm.h` as sbl; int.asm uses `int_masm.h`, which is the abstract
layer WITHOUT the runtime-EXTERN block, because int.asm *defines* those symbols.)

## Stage 0 result — the MINIMAL ↔ C calling bridge (the key risk)

The whole C-call mechanism is centralized in `int.asm`, so the System V → Microsoft
x64 ABI adaptation is localized to a few macros, not the codebase:

- `sysXX` functions take **no C arguments** — they read/write the global register
  block (`reg_wa`, `reg_xl`, …). So SysV-vs-MS argument-register differences don't
  bite. `f_sin`/etc. likewise use the global `reg_ra`.
- MINIMAL uses rsi/rdi/rbx/r12 — all **callee-saved** under MS x64 — so they survive
  C calls (they did NOT under SysV, which is why the Linux code saved them; on
  Windows that saving is merely redundant, not wrong).
- The two real MS-x64 requirements are **16-byte stack alignment at the call** and
  **32 bytes of shadow space** below it. Alignment was already done (`and rsp,~15`);
  shadow space was missing. Fixes applied in `prep_int.py`:
  - `syscall` macro: add `sub rsp,32` after the align, before `call`. (`syscall_exit`
    resets rsp from `compsp`, so no cleanup needed.)
  - `math_op` macro: rewritten with an rbp frame + align + `sub rsp,32`, saving the
    volatile MINIMAL regs (wa/wc) around the libc math call.

These are correct by the MS x64 ABI but are **runtime-unverifiable without Windows** —
they are the thing the Stage-0 "hello" spike should confirm once a minimal link works.

Restart/`-w`-only C calls (`rereloc`, `startbrk`, `zystm`, `swcoup`) also need the
same shadow-space treatment, but they are off the first-light path, so they're
deferred to bring-up (they assemble fine as-is).

## Remaining: Stage 3 (osint) → Stage 4 (link) → bring-up

osint is 67 C files. Port in tiers, aiming for *link first, run second, complete third*:

- **Tier A — compiles ~as-is:** the ~35 `sysXX.c` + helpers (plain C / thin wrappers).
  Mostly just fix Linux `#include`s for MSVC.
- **Tier B — needs Win32:** memory (`sysmm.c`, `compress.c`, `main.c`: mmap/sbrk →
  `VirtualAlloc`), tty/console (`testty.c`, `systm.c`: ioctl/termios → console API),
  process/pipe (`ospipe.c`, `sysem.c`: fork/exec → `CreateProcess`), signals
  (`break.c`, `oswait.c`), dynamic load (`sysld.c`: dlopen → `LoadLibrary`).
- **Tier C — stub to link, implement later:** LOAD/dynamic code, pipes, EXEC —
  return "unsupported" so symbols resolve; fill in after first light.

Link: `link sbl.obj err.obj int.obj osint\*.obj` + MSVC CRT (math is in the CRT;
no separate `-lm`). The EXTERN/global names across `int.asm` (the `zysXX`, `calltab`,
`stacksiz`, `lowsp`, `lowspmin`, `basemem`, …) and osint are the resolution checklist.

Bring-up: run `OUTPUT = 'hello'`, then progressively harder programs; the Linux
`test/` and `demos/` become the regression suite.

## Files in the Linux tree that are NOT build inputs
`asm.sbl`, `lex.sbl`, `err.sbl` (generators replaced by SpitbolBuild / bootstrap),
`bin/sbl`, `int-risc.c`, `fakexit.old`, `demos/`, `test/`, `sanity-check`,
`spitbol.1`, `z.sbl`. The `osint/*.h` headers ARE needed to compile the C.
