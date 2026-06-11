# osint Windows fixes — apply ON TOP of the 67 stock osint files

Drop these into your `osint\` source folder (overwrite the 8 modified files,
add the 2 new files `oswin.h` and the now-included `sproto.h`). All edits are
`#ifdef _WIN32`-guarded, so the Linux build is byte-for-byte unchanged.

## The three things that mattered

1. **`sproto.h` — the build blocker (your 66 "Cannot open unistd.h" errors).**
   `sproto.h` `#include <unistd.h>`, and `port.h` pulls `sproto.h` into every
   `.c`, so all 66 files failed. The include is now guarded `#ifndef _WIN32`;
   on Windows `read/write/lseek/close` come from `<io.h>` via `oswin.h` instead.

2. **`port.h` — `word` was 32-bit on Windows (the segfault).**
   `typedef long word;` is 64-bit on Linux (LP64) but **32-bit on Windows
   (LLP64)**. The MINIMAL registers (`reg_wa/reg_xr/reg_xl/...`, declared
   `word` in `osint.h`) and `sizeof(word)` drive all pointer math and memory
   layout, while the asm side (`reg_block` in int.asm) is 64-bit — so on
   Windows every pointer truncated to 32 bits. `word`/`uword` are now
   `long long`/`unsigned long long` (64-bit on both) under `_WIN32`.

3. **"formal parameter 1 different from declaration" — warnings, not errors.**
   MSVC **C4028**: benign prototype/definition mismatches osint has always had
   (gcc tolerates them silently). They do **not** stop the build. If your
   project has `/WX` (treat warnings as errors) on, turn it off.
