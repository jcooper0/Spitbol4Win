# SpitbolTests — xUnit harness for the Windows SPITBOL port

Runs the built `Spitbol.exe` against a corpus of SNOBOL4 programs and checks the
result. One xUnit test node per program, so you get per-program pass/fail and
diffs in Visual Studio Test Explorer and from `dotnet test` / CI.

## Add it to the solution

1. Drop this `SpitbolTests` folder beside your other projects in
   `Spitbol4Win\`, then in VS: **Solution ▸ Add ▸ Existing Project ▸
   SpitbolTests.csproj**.
2. Make the tests build the exe first: **right-click SpitbolTests ▸ Build
   Dependencies ▸ Project Dependencies ▸** check **Spitbol**. (The native
   `Spitbol` vcxproj can't be a `<ProjectReference>`, so this just enforces
   build order.)
3. Build the solution, open **Test Explorer**, run. You should see
   `Configuration_IsResolvable`, `Hello_PrintsHelloWorld`, and one
   `Corpus(caseName: …)` per program.

## How a program becomes a test

Discovery scans the corpus directory (recursively) for `*.sbl`, `*.sno`,
`*.spt`. The companion files next to each program decide what happens:

| File                | Effect                                                              |
|---------------------|---------------------------------------------------------------------|
| `<name>.expected`   | **Golden mode** — stdout is diffed against this file.               |
| *(no `.expected`)*  | **Self-check mode** — pass iff exit code 0 **and** no `*FAIL` line.  |
| `<name>.in`         | Fed to the program's stdin.                                         |
| `<name>.filter`     | One regex per line; matching output lines are dropped before diff.  |
| `skip.txt` (in root)| One case name per line to exclude (helpers, unported features).     |

### Setting up a golden test

1. Put your program in `cases\` (e.g. `cases\kwic.sbl`).
2. Create `cases\kwic.expected` with the exact expected output. Easiest is to
   capture it from a trusted run:
   ```
   set SPITBOL_UPDATE_GOLDEN=1
   dotnet test --filter "DisplayName~kwic"
   set SPITBOL_UPDATE_GOLDEN=
   ```
   That writes `kwic.expected` from the actual output. Review it, then commit.
   (Capture from a build you trust — e.g. your Linux `sbl` — so the file is a
   real baseline, not whatever the build under test happened to print.)
3. Rebuild **SpitbolTests** so discovery sees the new files. The case now runs
   in golden mode and diffs output against `kwic.expected`.
4. If any line is nondeterministic (a date, an address, a full path), add
   `cases\kwic.filter` with one regex per line to drop those lines before the
   compare.

Golden mode compares **stdout and stderr combined** and ignores the exit code,
so it also works for programs that are *meant* to fail — e.g. a compile-error
test. To assert SPITBOL rejects bad syntax like the bare `array(-a:10,3:5,20)`:
put that one statement in `cases\array_syntax.sbl`, capture the golden with
`SPITBOL_UPDATE_GOLDEN`, and it records SPITBOL's `array_syntax.sbl(1,..) :
error 226 ..` listing. The program never runs and exits nonzero, but golden
mode only checks that the output matches. The filename in the message is just
the bare file name (the harness runs each program from its own directory), so
it's stable across machines; mask a version/date banner with a `.filter` if one
appears.

Self-check mode is the natural fit for the stock `math_*.sbl` tests, which use
`chks()` from `chks.inc` / `math_chks.inc` to print ` pass:` / `*FAIL:` lines.
Golden mode suits the demos (`eliza`, `kwic`, `treesort`, …) that just print.

Output is normalized before comparison: CRLF/CR → LF, trailing whitespace
stripped, trailing blank lines dropped — so Windows vs. Linux line endings
don't cause false diffs.

## Populating the corpus

Copy the Macro SPITBOL `test/` and `demos/` programs into `cases\`, **keeping
their `.inc` and `.in` companions alongside** (the runner sets the working
directory to each program's folder so `-INCLUDE 'chks.inc'` and relative I/O
resolve). The suite reads the corpus from the project's `cases\` folder directly, so a newly added program is picked up on the **next build of SpitbolTests** — the per-case nodes are produced at discovery, which only re-runs after a build. If a new program doesn't appear, rebuild the **SpitbolTests** project (not just an incremental solution build), then refresh Test Explorer.

Generate the golden files for the demo programs **from a trusted build** (your
working Linux `sbl`), not from the exe under test:

```
# capture baselines from a reference build, then commit the .expected files
set SPITBOL_UPDATE_GOLDEN=1
dotnet test            # writes <name>.expected from current output
set SPITBOL_UPDATE_GOLDEN=
```

(For real cross-platform parity, generate goldens on Linux and run the suite on
Windows — that turns these into differential tests that catch LLP64-style
divergence, the exact class of bug we hit during the port.)

## Pointing at an external corpus

Rather than copying, you can run the suite directly against your checked-out
`test/` directory:

```
set SPITBOL_TESTS=C:\path\to\macro-spitbol\test
dotnet test
```

## Environment knobs

| Variable                | Meaning                                                        |
|-------------------------|----------------------------------------------------------------|
| `SPITBOL_EXE`           | Full path to the exe (else probe `SpitbolExe\x64\{Debug,Release}`). |
| `SPITBOL_TESTS`         | Corpus directory (else the copied `cases\`).                   |
| `SPITBOL_TIMEOUT_MS`    | Per-program timeout in ms (default 30000).                     |
| `SPITBOL_UPDATE_GOLDEN` | When set, (re)write `.expected` from output. Use on a trusted build only. |

## Running subsets

```
dotnet test --filter "FullyQualifiedName~Corpus"               # just the corpus
dotnet test --filter "DisplayName~math_"                       # just math_* cases
dotnet test --filter "FullyQualifiedName~Hello_PrintsHelloWorld"  # smoke only
```

## Notes / gotchas baked in

* Tests run **serially** (`.runsettings`) since each launches a process in a
  shared working directory and some programs write files.
* Programs that print nondeterministic data (`&TIME`, `&STCOUNT`, `&DUMP`
  addresses) will diff-fail in golden mode — give them a `.filter`, or list
  them in `skip.txt`, or convert them to `chks()` self-checks.
* Stdin is closed immediately when there's no `.in`, so a program that reads
  `INPUT` gets EOF instead of hanging; a runaway program is killed at the
  timeout and reported as a failure.
* Stubbed features (`HOST()`, pipes, `LOAD`, save/restore) — add them to
  `skip.txt` for now; remove them as you implement each, and the suite starts
  guarding the real behavior.
