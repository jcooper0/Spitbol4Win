# m_addr patch — address-of operands for the MASM build

## What changed
`MinimalCodeGen.cs`, `Getarg`, the address-of operand cases:

| getarg case | operand form | before        | after                |
|-------------|--------------|---------------|----------------------|
| 18          | `=dlbl`      | `substr(l1,2)`| `m_addr ` + name     |
| 20/21       | `=name` (data section)    | `substr(l1,2)` | `m_addr ` + name |
| 22          | `=name` (program section) | `substr(l1,2)` | `m_addr ` + name |

Case 19 (`*dlbl` → `cfp_b*name`) is **unchanged**: it is a scaled value, not
an address-of, so it must not get the marker.

## Why
`=name` means *address of name*. In NASM, `mov reg, name` already loads name's
address, so the bootstrap emitted a bare symbol. In MASM, `mov reg, name` loads
the **contents** at name; you need `mov reg, OFFSET name` to load the address.

The generator now emits a neutral `m_addr ` marker at exactly those sites. Each
assembler's header turns it into the right thing:

```
generator:   mov m_word [x], m_addr label
NASM (m_addr empty)   ->  mov qword   [x], label          ; = address
MASM (m_addr=OFFSET)  ->  mov QWORD PTR [x], OFFSET label  ; = address
```

## Required header definitions
- **MASM** (`masm.h`) — already present:
  ```
  m_addr   TEXTEQU <OFFSET>
  ```
- **NASM** header (the prepended `m.hdr` / `nasm.h`) — **add this one line**, or
  the NASM/validation build will fail with "symbol m_addr undefined":
  ```
  %define m_addr
  ```
  (defines `m_addr` as empty, so `m_addr label` -> ` label`).

`Nasm2Masm` passes the `m_addr` token through untouched; `masm.h` does the rest.

## Validation
Regenerated `sbl.asm` from the bootstrap `sbl.lex` and diffed against the
original (unpatched) reference body:

- **921 changed lines, every one a pure `m_addr ` insertion** at a `=name`/`=dlbl`
  operand (verified by stripping the marker + re-normalizing the comment column:
  the instruction and comment then match the reference exactly).
- No other line moved. No spurious changes.

So the byte-exact target for Component 2 going forward is "the bootstrap body
with `m_addr ` inserted at the address-of sites" — which the generator now
produces. To re-validate later, either regenerate the reference from a patched
`asm.sbl`, or diff against the original reference and confirm the differences are
exactly these 921 `m_addr` insertions.
