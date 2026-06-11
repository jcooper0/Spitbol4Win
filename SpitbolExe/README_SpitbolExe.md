# SpitbolExe -- native build (osint + asm -> spitbol.exe)

This Visual Studio C++ project compiles the osint C runtime, assembles the three
generated MASM files, and links `spitbol.exe`. The C# **SpitbolBuild** project
generates the `.asm`; this one turns everything into the executable. Put both in
the same VS **solution** (two separate projects -- a .NET project can't compile C).

## One-time setup
1. Place this folder beside SpitbolBuild, i.e. `Spitbol4Win\SpitbolExe\`.
   If your layout differs, edit `<OsintDir>` and `<RuntimeDir>` at the top of
   `Spitbol.vcxproj`.
2. Populate `SpitbolBuild\osint\` with the **full** osint sources from the
   `x64-main\osint` tree (all 67 `.c` + the `.h` files), then copy the 9
   `osint-win` files on top (8 overwrites + `oswin.h`). The project compiles
   `osint\*.c` (non-recursive), so don't leave the port files in a subfolder.
3. In Solution Explorer: right-click the solution -> Add -> Existing Project ->
   `Spitbol.vcxproj`.
4. If VS offers to retarget the toolset/SDK, accept it. (The project uses
   `$(DefaultPlatformToolset)`, so it should already match your install.)

## Before building
Run **SpitbolBuild** first so `runtime\sbl_masm.asm` is current and includes the
int.dcl splice (it must contain `calltab`/`typet`). Regenerate it whenever
`sbl.min` changes.

## Build & debug
- Set configuration to **Debug | x64**, set SpitbolExe as the startup project.
- In project Properties -> Debugging, set Command Arguments to a test program,
  e.g. `hello.sbl`, and Working Directory to where that file lives.
- F5. Breakpoints in the osint `.c` work directly; for the `.asm` you can step
  in the Disassembly window. This is how to localize the compile-path segfault.

## The link flags (already set in the project)
`/BASE:0x10000 /FIXED /DYNAMICBASE:NO /HIGHENTROPYVA:NO /LARGEADDRESSAWARE:NO`
-- the MINIMAL code uses 32-bit absolute addressing, so the image must load at a
fixed low base with ASLR disabled, or the link fails with ADDR32 relocation
overflow.
