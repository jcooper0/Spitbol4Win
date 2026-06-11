# Building SPITBOL for Windows x64 (MSVC)

End-to-end: generate the three MASM files, assemble with ml64, compile osint
with cl, link with link.exe. Verified here through assembly + a full link
(MinGW proxy) producing a runnable `spitbol.exe`.

## 1. Sources in the working directory
Put these together in the SpitbolBuild working folder:
`sbl.min`, `err.asm` (from bootstrap/), `int.asm`, `int.dcl`.

## 2. Generate MASM  (SpitbolBuild)
Run SpitbolBuild with no arguments. It produces, in `runtime\`:
`sbl_masm.asm`, `err_masm.asm`, `int_masm.asm`.
(int.dcl is spliced into sbl.asm automatically; int.asm gets the ABI prep.)

## 3. Assemble  (ml64)
    ml64 /c /Fo sbl.obj runtime\sbl_masm.asm
    ml64 /c /Fo err.obj runtime\err_masm.asm
    ml64 /c /Fo int.obj runtime\int_masm.asm

## 4. Port + compile osint  (cl)
Copy the 9 files from `osint-win/` over your `osint/` (8 modified + new
`oswin.h`), or apply `osint-win.patch` and add `oswin.h`. All Windows changes
are `#ifdef _WIN32`-guarded, so the Linux build is unaffected. Then:

    cl /c /Dm64 /D_CRT_SECURE_NO_WARNINGS /D_CRT_NONSTDC_NO_WARNINGS osint\*.c

(`m64` selects 64-bit; word size keys off the absence of `m32`.)

## 5. Link  (link.exe)
The MINIMAL code uses 32-bit absolute addressing, so the image must load low
with ASLR off (otherwise: "relocation truncated to fit / ADDR32"):

    link sbl.obj err.obj int.obj osint\*.obj ^
         /OUT:spitbol.exe /SUBSYSTEM:CONSOLE ^
         /BASE:0x10000 /FIXED /DYNAMICBASE:NO /HIGHENTROPYVA:NO /LARGEADDRESSAWARE:NO

(libm is not needed; the math is in the MSVC CRT.)

## Status
- Assembly: sbl/err/int all assemble clean under ml64 (confirmed) and JWasm.
- Link: full link succeeds; `spitbol.exe` is produced and runs -- the usage/
  brag banner ("spitbol v4.0f ...") prints, so main, malloc-backed memory
  (sbrkx), the startup ABI bridge and the sysou->zysou->write output path work.
- Open: compiling a source file segfaults in the compiler-execution path
  (startup -> minimal). Debug in the Visual Studio debugger for an exact source
  location -- likely first to check: the syscall/math_op shadow-space bridge
  under load, the calltab dispatch, and heap pointer handling. The local repro
  here is MinGW+Wine and unreliable for this; MSVC + VS will pinpoint it.

## Deferred features (stubbed for Windows, off the first-light path)
EXTFUN/LOAD (sysld), pipes & EXEC (ospipe/oswait/osclose teardown), tty raw
mode (testty ttyraw), a.out writing (wrtaout / EXECFILE=0). Each is
`#ifdef _WIN32`-guarded and returns "unsupported"/no-op; implement after first
light.
