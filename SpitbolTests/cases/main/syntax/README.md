# syntax/ — compile-time / syntax-error golden batch

One self-contained `.sbl` per compile-time error code. Each program is the
minimal source that makes Macro SPITBOL raise exactly that error during
**compilation** (not execution). These are **Golden** cases, never SelfCheck:
a syntax error aborts the compile before any code runs (the build exits 999, or
1 for the include case), so there is no `RESULT: PASS` line to self-check. The
discriminating signal is the `... : error NNN -- ...` line in the compiler
output, which is why each case needs a captured `.expected` baseline.

Every trigger here was validated against the reference `sbl` binary built from
the same `spitbol/x64` source as the Windows port. All listed codes fire exactly.
Codes were taken from the authoritative `bootstrap/sbl.lex` error table (the
`erb` entries), not the manual.

Folder location in the repo: `SpitbolTests/cases/syntax/`.

## Codes covered (22 firing)

| file              | code | message                                       |
|-------------------|------|-----------------------------------------------|
| namereq212        | 212  | value used where name is required             |
| badcont214        | 214  | bad label or misplaced continuation line      |
| erlabel215        | 215  | undefined or erroneous entry label (after END)|
| duplabel217       | 217  | duplicate label                               |
| dupgoto218        | 218  | duplicated goto field                         |
| emptygoto219      | 219  | empty goto field (bare `:` introducer)        |
| missop220         | 220  | missing operator                              |
| missopnd221       | 221  | missing operand                               |
| lbracket222       | 222  | invalid use of left bracket                   |
| comma223          | 223  | invalid use of comma                          |
| rparen224         | 224  | unbalanced right parenthesis                  |
| rbracket225       | 225  | unbalanced right bracket                      |
| norparen226       | 226  | missing right paren                           |
| gotorparen227     | 227  | right paren missing from goto                 |
| gotorbrkt228      | 228  | right bracket missing from goto (direct goto) |
| arrbracket229     | 229  | missing right array bracket                   |
| illchar230        | 230  | illegal character (0x01 in source)            |
| badnum231         | 231  | invalid numeric item                          |
| strquote232       | 232  | unmatched string quote                        |
| badop233          | 233  | invalid use of operator (two binops abutting) |
| gotofield234      | 234  | goto field incorrect                          |
| ctrlstmt247       | 247  | invalid control statement                     |
| include285        | 285  | include file cannot be opened                 |

Trigger notes for the less-obvious ones:
- **219** needs a *bare* `:` with no field (`output = 1 :`). `:()` does not work —
  empty parens parse as a name context and raise 212 first.
- **233** is the build's catch-all for malformed operators; the clean isolated
  trigger is two binary operators abutting (`output = 1 // 2`).
- **247** fires on a *recognised* control card given a bad-typed argument
  (`-line abc` — it expects an integer), or a `-` followed by a non-name token.
  An *unrecognised* card (e.g. `-frobnicate`) is silently ignored, not an error.

## Present but NOT triggerable on this build

These were investigated and deliberately left out — they are findings, not gaps:

| code | message                              | reason                                                                 |
|------|--------------------------------------|------------------------------------------------------------------------|
| 213  | statement is too complicated         | not attempted — requires a pathologically complex statement            |
| 216  | missing end line                     | unreachable from source: the host-level "No END statement found in source file(s)" guard preempts the only path that reaches it, and the runtime `code()`/execute-compile paths land on the OK branch (`stgxe`). Tried no-END, empty file, control-only, comment-only, bare label at EOF, and `code()` without END. |
| 286  | function call to undefined entry label | build divergence: fires on the Linux reference (`define("f()","nolabel")` → 286) but **not** on the Windows build, where it compiles and exits 0. Same bucket as the BUFFER/APPEND/INSERT and never-fire findings. (`undeflabel286.sbl` is kept for reference but is a no-op on Windows.) |

## Setting up the goldens (Windows)

These are Golden cases, so each needs a sibling `.filter` and a captured
`.expected`. Two things make the bootstrap non-obvious:

1. **Classification is by file presence.** A case is Golden only if a sibling
   `.expected` exists; otherwise it falls back to SelfCheck and fails on the
   non-zero exit. So the `.expected` must exist before the case can be Golden.
2. **`SPITBOL_UPDATE_GOLDEN=1` is refresh-only.** It rewrites goldens for cases
   already classified Golden; it will not promote a SelfCheck case. So you must
   seed an empty `.expected` first, then capture.

The `.filter` is **not** empty: the captured compiler output includes a banner
whose timestamp line changes every run. Each `.filter` must contain these two
lines so the diff lands only on the deterministic listing/caret/error lines:

```
macro spitbol version
x86-64
```

Full bootstrap from the repo root:

```powershell
# 1. Seed .filter (banner-drop rules) + empty .expected beside each .sbl
cd C:\Users\jcooper\source\repos\Spitbol4Win\SpitbolTests\cases\syntax
Get-ChildItem *.sbl | ForEach-Object {
    Set-Content -Path ($_.BaseName + ".filter") -Value @('macro spitbol version','x86-64')
    New-Item -ItemType File -Name ($_.BaseName + ".expected") -Force | Out-Null
}

# 2. Capture the real output into the now-existing .expected files
cd C:\Users\jcooper\source\repos\Spitbol4Win\SpitbolTests
$env:SPITBOL_UPDATE_GOLDEN = "1"
dotnet test --filter "DisplayName~syntax"
Remove-Item Env:\SPITBOL_UPDATE_GOLDEN

# 3. Verify
dotnet test --filter "DisplayName~syntax"
```

Run from inside `SpitbolTests`, never the solution root (the C++ `SpitbolExe`
vcxproj can't be built by the dotnet CLI and will break the run).

## Notes

- The `(line,col)` position is baked into each golden. That's fine for frozen
  files (it's deterministic), but it is why the deferred **OutputNormalizer
  substitution filter** matters: once it lands, a substitution like
  `s/\([0-9]+,[0-9]+\)/(L,C)/` lets you re-capture position-independent goldens.
  Until then, do not edit a `.sbl` after capture — adding a line shifts every
  position and invalidates the golden.
- The filename in the error line is relative (e.g. `strquote232.sbl(2,16)`)
  because the runner sets cwd to the case directory, so the goldens are portable.
- `syntax.filter` in this folder is the shared reference copy of the filter rules.
