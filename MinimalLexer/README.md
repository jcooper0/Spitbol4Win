# MinimalLexer — Component 1 of the C# MINIMAL translator

Front-end port of SPITBOL's `lex.sbl`. Reads the MINIMAL source and
writes the tokenized, type-tagged intermediate (`sbl.lex`) that the
back end (Component 2) will consume. Verified byte-for-byte against
the Linux bootstrap's `sbl.lex` on the full `sbl.min` (12,337 lines,
zero diff).

## Open / build in Visual Studio 2026
1. Double-click `MinimalLexer.sln`.
2. Build: **Ctrl+Shift+B** (or Build > Build Solution).
   Output: `bin\Release\net8.0\MinimalLexer.exe`
   (Configuration is Any CPU, so there is no `x64` path segment.)

Or from a terminal in this folder:
    dotnet build -c Release

## The MINIMAL files required for SPITBOL
There is exactly **one** MINIMAL source file: **`sbl.min`** — the entire
SPITBOL system (~29,310 lines). The other source files are NOT MINIMAL:
`lex.sbl` / `asm.sbl` / `err.sbl` are the translator (written in SPITBOL),
and `int.asm` is hand-written assembly. So lexing SPITBOL means lexing
`sbl.min` alone.

## Lex sbl.min
From the folder that contains `sbl.min` (point at the exe with a full path,
or copy the exe next to sbl.min):

    MinimalLexer.exe sbl.min sbl.lex

Or, from this project folder, without locating the exe:

    dotnet run -c Release -- C:\path\to\sbl.min sbl.lex

## Validate against the bootstrap (the diff oracle)
Generate the reference once with your existing SPITBOL, then compare:

    sbl lex.sbl                 (Linux/bootstrap; produces reference sbl.lex)
    fc sbl.lex sbl_reference.lex

`fc` should report "no differences encountered".

## sbl.lex line format
Pipe-delimited (`|`); a type field is present only when the operand has one:

    |label|opcode|typ1,op1|typ2,op2|typ3,op3|comment|sourceline
