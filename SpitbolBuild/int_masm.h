; ============================================================
;  masm.h  --  MASM (ml64) machine-definition header for SPITBOL x64
;
;  Drop-in replacement for the NASM header block that is prepended to
;  the generated assembly. It supplies the abstract macro layer that
;  asm.sbl emits against (size operators, register map, word-size
;  constants, string-op mnemonics, flags) in MASM syntax.
;
;  Pair it with nasm2masm.py, which rewrites the syntax a prepended
;  header cannot reach: typed EXTERN, PUBLIC, .CODE/.DATA segments,
;  0x hex literals, and the END directive.
;
;  Target: ml64.exe. No .MODEL (64-bit); [symbol] is RIP-relative by
;  default, so NASM's `BITS 64` / `DEFAULT REL` are simply dropped.
; ============================================================

; ---- memory size operators -------------------------------------
;  NASM  `m_word [x]` -> `qword [x]`   MASM -> `QWORD PTR [x]`
m_char   TEXTEQU <BYTE PTR>
m_word   TEXTEQU <QWORD PTR>
m_real   TEXTEQU <QWORD PTR>
m_reall  TEXTEQU <XMMWORD PTR>          ; NASM oword (128-bit)

; ---- data-definition directives --------------------------------
;  NOT defined here. MASM resolves a directive keyword BEFORE text-macro
;  substitution, so a `d_word -> dq` TEXTEQU is rejected in the directive
;  slot ("syntax error : d_word"). nasm2masm rewrites d_word/d_real -> dq
;  and d_char -> db directly in the body instead.

; ---- address-of (pairs with the asm.sbl m_addr patch) ----------
;  generator emits:  mov m_word [x], m_addr label
;  NASM:  m_addr is empty   -> mov qword [x], label        (= address)
;  MASM:  m_addr -> OFFSET   -> mov QWORD PTR [x], OFFSET label
m_addr   TEXTEQU <OFFSET>

; ---- minimal register map --------------------------------------
xl   TEXTEQU <rsi>
xr   TEXTEQU <rdi>
xt   TEXTEQU <rsi>
xs   TEXTEQU <rsp>
w0   TEXTEQU <rax>
wa   TEXTEQU <rcx>
wa_l TEXTEQU <cl>
wb   TEXTEQU <rbx>
wb_l TEXTEQU <bl>
wc   TEXTEQU <rdx>
wc_l TEXTEQU <dl>
ia   TEXTEQU <r12>
ra   TEXTEQU <xmm12>

; ---- word-size constants ---------------------------------------
;  NOTE: cfp_b is intentionally NOT defined here -- the generated
;  body already emits `cfp_b equ 8`, and a second definition would
;  be a MASM redefinition error.
log_cfp_b EQU 3
log_cfp_c EQU 3
cfp_c_val EQU 8
cfp_m_    EQU 9223372036854775807        ; max signed 64-bit

; ---- string-operation mnemonics (word size = 64-bit) -----------
lods_w TEXTEQU <lodsq>
lods_b TEXTEQU <lodsb>
movs_b TEXTEQU <movsb>
movs_w TEXTEQU <movsq>
stos_b TEXTEQU <stosb>
stos_w TEXTEQU <stosq>
cmps_b TEXTEQU <cmpsb>

; ---- misc ------------------------------------------------------
;  cdq is NOT aliased here. MASM forbids TEXTEQU on a reserved mnemonic.
;  nasm2masm rewrites the bare `cdq` instruction to `cqo` in the body so
;  the word-size sign-extend is 64-bit (RDX:RAX).

; ---- flags (NASM 0x80 -> MASM 80h) -----------------------------
flag_of EQU 80h
flag_cf EQU 01h
flag_ca EQU 40h


; ---- word size (int.asm uses `align cfp_b`; sbl body defines its own) ----
cfp_b   EQU 8
cfp_c   EQU 8
