# SpitbolBuild — MINIMAL → MASM in one step

Runs the whole Windows translation pipeline in-process and writes every
intermediate:

```
sbl.min ─[MinimalLexer]→ sbl.lex ─[MinimalCodeGen]→ sbl.asm ─[Nasm2Masm]→ ─[+masm.h]→ sbl_masm.asm
            tokenizer               NASM body          NASM→MASM body      prepend header
```

All three stages are verified byte-for-byte against the Linux bootstrap:
`sbl.lex` is identical to the reference lex output, and `sbl.asm` is the
reference NASM body with the `m_addr` address-of patch applied.

## Build (Visual Studio 2026)
Open `SpitbolBuild.sln`, build (Ctrl+Shift+B). Output:
`bin\Release\net8.0\SpitbolBuild.exe` (Any CPU — no `x64` path segment).

Or from a terminal in this folder: `dotnet build -c Release`.

## Run
```
SpitbolBuild.exe sbl.min
```
Writes, next to `sbl.min`: **sbl.lex**, **sbl.asm** (NASM body), **sbl_masm.asm**.

Optional output directory and header override:
```
SpitbolBuild.exe sbl.min out\                 (write outputs into out\)
SpitbolBuild.exe sbl.min --header my_masm.h   (use a custom MASM header)
```

`masm.h` is embedded in the exe, so there is nothing else to copy. Use
`--header` only if you want to override it.

## Assemble
From an **x64 Native Tools Command Prompt for VS 2026** (so `ml64` is on PATH):
```
ml64 /c /Fo sbl.obj sbl_masm.asm
```
Then link `sbl.obj` with the runtime objects (`int.obj` + the osint C objects).

## What each source file is
- `MinimalLexer.cs`   — port of `lex.sbl` (MINIMAL → tokenized `sbl.lex`)
- `MinimalCodeGen.cs` — port of `asm.sbl` back end (`sbl.lex` → NASM `sbl.asm`)
- `Nasm2Masm.cs`      — NASM→MASM post-pass (directives, externs, `d_word`→`dq`,
                        `d_char`→`db`, `cdq`→`cqo`, hex, `%macro`→`MACRO`, …)
- `masm.h`            — MASM machine-definition header (embedded resource)
- `Program.cs`        — the orchestrator

## Notes
- The 78-line `masm.h` is the analog of the NASM header that the original build
  prepended; it defines the abstract macro layer (`m_word`, `m_char`, register
  map, `m_addr` → `OFFSET`, word-size constants, string-op mnemonics, flags).
- `sbl.asm` is the generated **body**. The original full `sbl.asm` also carries
  a hand-written prologue/epilogue (`m.hdr`); on Windows that role is split
  between `masm.h` (prepended here) and the separately-assembled `int.asm`/osint
  objects you link against.
