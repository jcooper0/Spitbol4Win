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
; copyright 1987-2012 robert b. k. dewar and mark emmer.

; copyright 2012-2015 david shields
;
; this file is part of macro spitbol.
;
;     macro spitbol is free software: you can redistribute it and/or modify
;     it under the terms of the gnu general public license as published by
;     the free software foundation, either version 2 of the license, or
;     (at your option) any later version.
;
;     macro spitbol is distributed in the hope that it will be useful,
;     but without any warranty; without even the implied warranty of
;     merchantability or fitness for a particular purpose.  see the
;     gnu general public license for more details.
;
;     you should have received a copy of the gnu general public license
;     along with macro spitbol.      if not, see <http://www.gnu.org/licenses/>.


;     ws is bits per word, cfp_b is bytes per word, cfp_c is characters per word


      PUBLIC reg_block
      PUBLIC reg_w0
      PUBLIC reg_wa
      PUBLIC reg_wb
      PUBLIC reg_ia
      PUBLIC reg_wc
      PUBLIC reg_xr
      PUBLIC reg_xl
      PUBLIC reg_cp
      PUBLIC reg_ra
      PUBLIC reg_pc
      PUBLIC reg_xs
      PUBLIC reg_size

      PUBLIC reg_rp
      PUBLIC mxcsr_set
      PUBLIC minimal

      EXTERN calltab:PROC
      EXTERN stacksiz:QWORD
      EXTERN lowsp:QWORD
      EXTERN lowspmin:QWORD
      EXTERN basemem:PROC

;     values below must agree with calltab defined in x64.hdr and also in osint/osint.h

minimal_relaj     equ   0
minimal_relcr     equ   1
minimal_reloc     equ   2
minimal_alloc     equ   3
minimal_alocs     equ   4
minimal_alost     equ   5
minimal_blkln     equ   6
minimal_insta     equ   7
minimal_rstrt     equ   8
minimal_start     equ   9
minimal_filnm     equ   10
minimal_dtype     equ   11
minimal_enevs     equ   12
minimal_engts     equ   13



;     ---------------------------------------

;     this file contains the assembly language routines that interface
;     the macro spitbol compiler written in 80386 assembly language to its
;     operating system interface functions written in c.

;     contents:

;     o overview
;     o global variables accessed by osint functions
;     o interface routines between compiler and osint functions
;     o c callable function startup
;     o c callable function get_fp
;     o c callable function restart
;     o c callable function makeexec
;     o routines for minimal opcodes chk and cvd
;     o math functions for integer multiply, divide, and remainder
;     o math functions for real operation

;     overview

;     the macro spitbol compiler relies on a set of operating system
;     interface functions to provide all interaction with the host
;     operating system.  these functions are referred to as osint
;     functions.  a typical call to one of these osint functions takes
;     the following form in the 80386 version of the compiler:

;           ...code to put arguments in registers...
;           call  sysxx       # call osint function
;           dq      exit_1            # address of exit point 1
;           dq      exit_2            # address of exit point 2
;           ...   ...         # ...
;           dq      exit_n            # address of exit point n
;           ...instruction following call...

;     the osint function 'sysxx' can then return in one of n+1 ways:
;     to one of the n exit points or to the instruction following the
;     last exit.  this is not really very complicated - the call places
;     the return address on the stack, so all the interface function has
;     to do is add the appropriate offset to the return address and then
;     pick up the exit address and jump to it or do a normal return via
;     an ret instruction.

;     unfortunately, a c function cannot handle this scheme.      so, an
;     intermediary set of routines have been established to allow the
;     interfacing of c functions.  the mechanism is as follows:

;     (1) the compiler calls osint functions as described above.

;     (2) a set of assembly language interface routines is established,
;         one per osint function, named accordingly.  each interface
;         routine ...

;         (a) saves all compiler registers in global variables
;           accessible by c functions
;         (b) calls the osint function written in c
;         (c) restores all compiler registers from the global variables
;         (d) inspects the osint function's return value to determine
;           which of the n+1 returns should be taken and does so

;     (3) a set of c language osint functions is established, one per
;         osint function, named differently than the interface routines.
;         each osint function can access compiler registers via global
;         variables.    no arguments are passed via the call.

;         when an osint function returns, it must return a value indicating
;         which of the n+1 exits should be taken.  these return values are
;         defined in header file 'inter.h'.

;     note:  in the actual implementation below, the saving and restoring
;     of registers is actually done in one common routine accessed by all
;     interface routines.

;     other notes:

;     some c ompilers transform "internal" global names to
;     "external" global names by adding a leading underscore at the front
;     of the internal name.  thus, the function name 'osopen' becomes
;     '_osopen'.  however, not all c compilers follow this convention.

;     global variables

      .DATA
;
; ; words saved during exit(-3)
; ;
      align cfp_b
reg_block LABEL BYTE
reg_ia      dq      0           ; register ia (r12)
reg_w0      dq      0           ; register w0 (rax)
reg_wa      dq      0           ; register wa (rcx)
reg_wb      dq      0           ; register wb (rbx)
reg_wc      dq      0           ; register wc (rdx)
reg_xl      dq      0           ; register xl (rsi)
reg_xr      dq      0           ; register xr (rdi)
reg_cp      dq      0           ; register cp (r13)
reg_ra      dq      0.0         ; register ra (xmm12)

; these locations save information needed to return after calling osint
; and after a restart from exit()


reg_pc      dq      0           ; return pc from caller
reg_xs      dq      0           ; minimal stack pointer

;     r_size      equ     $-reg_block
; use computed value for nasm conversion, put back proper code later
r_size      equ   10*cfp_b
reg_size    dd   r_size

; end of words saved during exit(-3)

; reg_rp is used to pass pointer to real operand for real arithmetic
reg_rp      dq      0

; reg_fl is used to communicate condition codes between minimal and c code.
      PUBLIC reg_fl
reg_fl      db    0           ; condition code register for numeric operations

      align 8
;                          08000h          01000h           00800h           00400h        | 0200h               |  00100h        | 00040h
mxcsr_set    dd  09fc0h  ; Flush to zero | Precision mask | Underflow mask | Overflow mask | Divide by Zero mask | Denormal mask | Denormals are zero
;
mxcsr_save   dd   0           ; Preserved mxcsr (restore when returning to C -- except for math functions)
      PUBLIC reg_flerr
reg_flerr   dq      0     ; Floatint point error

      align 8
;  constants

      PUBLIC ten
ten   dq      10              ; constant 10
      PUBLIC inf
      align 16
; double precision
;    0        1        2         3        4        5       6        7
; seeeeeee eeeeffff ffffffff ffffffff ffffffff ffffffff ffffffff ffffffff
;
zerop  dq         00000000000000000h     ; Positive zero
      PUBLIC zeron
zeron  dq         08000000000000000h     ; Negative zero
infp   dq         07ff0000000000000h     ; Postive infinity
infn   dq         08ff0000000000000h     ; Negative infinity
inf   dq          07ff0000000000000h     ; double precision infinity
nanp  dq          07fffffffffffffffh     ; positive nan
nann  dq          0ffffffffffffffffh     ; negative nan

infl  dd          -1
      dd          2146435071

      PUBLIC neg1f
      align 16
neg1f  dq         07fffffffffffffffh

      PUBLIC sav_block
;sav_block: times r_size db 0       ; save minimal registers during push/pop reg
sav_block db 44 dup(0)            ; save minimal registers during push/pop reg

      align cfp_b
      PUBLIC ppoff
ppoff       dq          0             ; offset for ppm exits
      PUBLIC compsp
compsp  dq        0             ; compiler's stack pointer
      PUBLIC sav_compsp
sav_compsp LABEL BYTE
      dq          0             ; save compsp here
      PUBLIC osisp
osisp       dq          0             ; osint's stack pointer
      PUBLIC lowspminx
lowspminx   dq          -1            ; lowest used stack
      PUBLIC _rc_
_rc_  dd   0                        ; return code from osint procedure

      align cfp_b
      PUBLIC save_cp
      PUBLIC save_xl
      PUBLIC save_xr
      PUBLIC save_xs
      PUBLIC save_wa
      PUBLIC save_wb
      PUBLIC save_wc
      PUBLIC save_w0
      PUBLIC save_ra
save_cp     dq      0           ; saved cp value
save_ia     dq      0           ; saved ia value
save_xl     dq      0           ; saved xl value
save_xr     dq      0           ; saved xr value
save_xs     dq      0           ; saved sp value
save_wa     dq      0           ; saved wa value
save_wb     dq      0           ; saved wb value
save_wc     dq      0           ; saved wc value
save_w0     dq      0           ; saved w0 value
save_ra     dq      0.0         ; saved ra value

      PUBLIC minimal_id
minimal_id  dq      0           ; id for call to minimal from c. see proc minimal below.

;

;     setup a number of internal addresses in the compiler that cannot
;     be directly accessed from within c because of naming difficulties.

      PUBLIC id1
id1   dd   0


      PUBLIC id1blk
id1blk      dq       152
      dq        0
      db 152 dup(0)

      PUBLIC id2blk
id2blk      dq       152
      dq        0
      db 152 dup(0)

      PUBLIC ticblk
ticblk      dq       0
      dq      0

      PUBLIC tscblk
tscblk       dq       512
      dq      0
      db 512 dup(0)

;     standard input buffer block.

      PUBLIC inpbuf
inpbuf      dq      0                 ; type word
      dq      0                 ; block length
      dq      1024              ; buffer size
      dq      0                 ; remaining chars to read
      dq      0                 ; offset to next character to read
      dq      0                 ; file position of buffer
      dq      0                 ; physical position in file
      db 1024 dup(0)         ; buffer

      PUBLIC ttybuf
ttybuf      dq        0   ; type word
      dq        0               ; block length
      dq        260             ; buffer size  (260 ok in ms-dos with cinread())
      dq        0               ; remaining chars to read
      dq        0               ; offset to next char to read
      dq        0               ; file position of buffer
      dq        0               ; physical position in file
      db 260 dup(0)          ; buffer
;
;     save and restore minimal and interface registers on stack.
;     used by any routine that needs to call back into the minimal
;     code in such a way that the minimal code might trigger another
;     sysxx call before returning.
;
;     note 1:      pushregs returns a collectable value in xl, safe
;     for subsequent call to memory allocation routine.
;
;     note 2:      these are not recursive routines.  only reg_xl is
;     saved on the stack, where it is accessible to the garbage
;     collector.  other registers are just moved to a temp area.
;
;     note 3:      popregs does not restore reg_cp, because it may have
;     been modified by the minimal routine called between pushregs
;     and popregs as a result of a garbage collection.  calling of
;     another sysxx routine in between is not a problem, because
;     cp will have been preserved by minimal.
;
;     note 4:      if there isn't a compiler stack yet, we don't bother
;     saving xl.  this only happens in call of nextef from sysxi when
;     reloading a save file.
;
;
      .CODE
      PUBLIC save_regs
save_regs:
      mov   m_word [save_ia],r12
      mov   m_word [save_xl],rsi
      mov   m_word [save_xr],rdi
      mov   m_word [save_xs],rsp
      mov   m_word [save_wa],rcx
      mov   m_word [save_wb],rbx
      mov   m_word [save_wc],rdx
      mov   m_word [save_w0],rax
      movsd m_real [save_ra],xmm12
      ret

      PUBLIC restore_regs
restore_regs:
      ;     restore regs, except for sp. that is caller's responsibility
      mov   r12,m_word [save_ia]
      mov   rsi,m_word [save_xl]
      mov   rdi,m_word [save_xr]
;     mov   rsp,m_word [save_xs     ; caller restores sp]
      mov   rcx,m_word [save_wa]
      mov   rbx,m_word [save_wb]
      mov   rdx,m_word [save_wc]
      mov   rax,m_word [save_w0]
      movsd xmm12,m_real [save_ra]
      ret
; ;
; ;     startup( char *dummy1, char *dummy2) - startup compiler
; ;
; ;     an osint c function calls startup to transfer control
; ;     to the compiler.
; ;
; ;     (rdi) = basemem
; ;     (rsi) = topmem - sizeof(word)
; ;
; ;   note: this function never returns.
; ;
;
      PUBLIC startup
;   ordinals for minimal calls from assembly language.

;   the order of entries here must correspond to the order of
;   calltab entries in the inter assembly language module.

calltab_relaj equ   0
calltab_relcr equ   1
calltab_reloc equ   2
calltab_alloc equ   3
calltab_alocs equ   4
calltab_alost equ   5
calltab_blkln equ   6
calltab_insta equ   7
calltab_rstrt equ   8
calltab_start equ   9
calltab_filnm equ   10
calltab_dtype equ   11
calltab_enevs equ   12
calltab_engts equ   13

startup:
      pop   rax               ; discard return
      call  stackinit         ; initialize minimal stack
      mov   rax,m_word [compsp]     ; get minimal's stack pointer
      mov m_word [reg_wa],rax       ; startup stack pointer
      cld                     ; default to up direction for string ops
      stmxcsr [mxcsr_save]    ; Remember default mxcsr
      mov   m_word [minimal_id],calltab_start
      call  minimal           ; load regs, switch stack, start compiler

;     stackinit  -- initialize lowspmin from sp.

;     input:      sp - current c stack
;           stacksiz - size of desired minimal stack in bytes

;     uses: rax

;     output: register wa, sp, lowspmin, compsp, osisp set up per diagram:

;     (high)      +----------------+
;                 |  old c stack   |
;                 |    //////      |  <- incoming sp
;                 |----------------|
;                 |   vdso/vvar    |
;                 |    etc ////    |
;                 |----------------| <-- high heap
;                 | free alloc     |
;                 |                | <- maxmem
;                 |----------------| <- topmem (will grow to maxmem)
;                 / memincb        /
;                 |----------------| <- basemem
;                 |          ^     | <- initial compsp (stack goes down)
;                 |          |     |
;                 / stacksiz bytes /
;                 |          |     |
;                 |          |     |
;                 |--------- | ----| <-- resultant lowspmin
;                 | 400 bytesv     |
;                 |----------------| <- lowsp
;    (low)          // free heap //


      PUBLIC stackinit
stackinit:
      mov   rax, m_word [lowsp]     ;
      add   rax, cfp_b*400
      mov   m_word [lowspmin], rax
      mov   rax, m_word [lowsp]
      add   rax, m_word [stacksiz]
      sub   rax, cfp_b
      mov   m_word [compsp],rax     ; save as minimal's stack pointer
      ret

;     mimimal -- call minimal function from c

;     usage:      extern void minimal(word callno)

;     where:
;       callno is an ordinal defined in osint.h, osint.inc, and calltab.

;     minimal registers wa, wb, wc, xr, and xl are loaded and
;     saved from/to the register block.

;     note that before restart is called, we do not yet have compiler
;     stack to switch to.  in that case, just make the call on the
;     the osint stack.

 minimal:
;       pushad                ; save all registers for c
      mov   rcx,m_word [reg_wa]     ; restore registers
      mov   rbx,m_word [reg_wb]
      mov   rdx,m_word [reg_wc]     ;
      mov   rdi,m_word [reg_xr]
      mov   rsi,m_word [reg_xl]
      mov   r12,m_word [reg_ia]
      mov   r13,m_word [reg_cp]
      movsd xmm12,m_real [reg_ra]

      mov   m_word [osisp],rsp      ; save osint stack pointer
      cmp   m_word [compsp],0 ; is there a compiler stack?
      je    min1              ; jump if none yet
      mov   rsp,m_word [compsp]     ; switch to compiler stack

 min1:
      mov   rax,m_word [minimal_id] ; get ordinal
      call   m_word [calltab+rax*cfp_b]    ; off to the minimal code

      mov   rsp,m_word [osisp]      ; switch to osint stack

      mov   m_word [reg_wa],rcx     ; save registers
      mov   m_word [reg_wb],rbx
      mov   m_word [reg_wc],rdx
      mov   m_word [reg_xr],rdi
      mov   m_word [reg_xl],rsi
      mov   m_word [reg_ia],r12
      mov   m_word [reg_cp],r13
      movsd m_real [reg_ra],xmm12
      ret

      .DATA
      align       cfp_b
      PUBLIC hasfpu
hasfpu      dq      0
      PUBLIC cprtmsg
cprtmsg LABEL BYTE
      db        ' copyright 1987-2012 robert b. k. dewar and mark emmer.',0,0


;     interface routines

;     each interface routine takes the following form:

;           sysxx call  ccaller ; call common interface
;                 dq      zysxx ; dd    of c osint function
;                 db    n     ; offset to instruction after
;                             ;   last procedure exit

;     in an effort to achieve portability of c osint functions, we
;     do not take take advantage of any "internal" to "external"
;     transformation of names by c compilers.    so, a c osint function
;     representing sysxx is named _zysxx.  this renaming should satisfy
;     all c compilers.

;     important  one interface routine, sysfc, is passed arguments on
;     the stack.  these items are removed from the stack before calling
;     ccaller, as they are not needed by this implementation.

;     ccaller is called by the os interface routines to call the
;     real c os interface function.

;     general calling sequence is

;           call  ccaller
;           dq      address_of_c_function
;           db    2*number_of_exit_points

;     control is never returned to a interface routine.  instead, control
;     is returned to the compiler (the caller of the interface routine).

;     the c function that is called must always return an integer
;     indicating the procedure exit to take or that a normal return
;     is to be performed.

;           c function  interpretation
;           return value
;           ------------      -------------------------------------------
;                <0           do normal return to instruction past
;                       last procedure exit (distance passed
;                       in by dummy routine and saved on stack)
;                 0           take procedure exit 1
;                 4           take procedure exit 2
;                 8           take procedure exit 3
;                ...    ...


      .DATA
call_adr    dq      0
      .CODE

syscall_init:
;     save registers in global variables

      mov   m_word [reg_wa],rcx      ; save registers
      mov   m_word [reg_wb],rbx
      mov   m_word [reg_wc],rdx
      mov   m_word [reg_xr],rdi
      mov   m_word [reg_xl],rsi
      mov   m_word [reg_ia],r12
      mov   m_word [reg_cp],r13
      movsd m_real [reg_ra],xmm12
      ldmxcsr [mxcsr_save]      ; Restore mxcsr register
      ret

syscall_exit:
      mov   m_word [_rc_],rax ; save return code from function
      mov   m_word [osisp],rsp       ; save osint's stack pointer
      mov   rsp,m_word [compsp]      ; restore compiler's stack pointer
      mov   rcx,m_word [reg_wa]      ; restore registers
      mov   rbx,m_word [reg_wb]
      mov   rdx,m_word [reg_wc]      ;
      mov   rdi,m_word [reg_xr]
      mov   rsi,m_word [reg_xl]
      mov   r12,m_word [reg_ia]
      mov   r13,m_word [reg_cp]
      movsd xmm12,m_real [reg_ra]
      cld
      mov   rax,m_word [reg_pc]
      jmp   rax

      syscallm MACRO p1, p2
      pop   rax               ; pop return address
      mov   m_word [reg_pc],rax
      call  syscall_init
;     save compiler stack and switch to osint stack
      mov   m_word [compsp],rsp      ; save compiler's stack pointer
      mov   rsp,m_word [osisp]       ; load osint's stack pointer
      and   rsp,0fffffffffffffff0h   ; 16byte alignment
      sub   rsp,32                   ; MS x64 shadow space
      call  p1
      add   rsp,32                   ; free MS x64 shadow space (caller cleanup)
      jmp   syscall_exit            ; was a call for debugging purposes, but that would cause a crash when the
                              ; compilers stack pointer blew up
          ENDM

      PUBLIC sysax
      EXTERN zysax:PROC
sysax:      syscallm       zysax,1

      PUBLIC sysbs
      EXTERN zysbs:PROC
sysbs:      syscallm       zysbs,2

      PUBLIC sysbx
      EXTERN zysbx:PROC
sysbx:      mov   m_word [reg_xs],rsp
      syscallm     zysbx,2

;      global syscr
;     extern      zyscr
;syscr:      syscallm    zyscr ;    ,0

      PUBLIC sysdc
      EXTERN zysdc:PROC
sysdc:      syscallm     zysdc,4

      PUBLIC sysdm
      EXTERN zysdm:PROC
sysdm:      syscallm     zysdm,5

      PUBLIC sysdt
      EXTERN zysdt:PROC
sysdt:      syscallm     zysdt,6

      PUBLIC sysea
      EXTERN zysea:PROC
sysea:      syscallm     zysea,7

      PUBLIC sysef
      EXTERN zysef:PROC
sysef:      syscallm     zysef,8

      PUBLIC sysej
      EXTERN zysej:PROC
sysej:      syscallm     zysej,9

      PUBLIC sysem
      EXTERN zysem:PROC
sysem:      syscallm     zysem,10

      PUBLIC sysen
      EXTERN zysen:PROC
sysen:      syscallm     zysen,11

      PUBLIC sysep
      EXTERN zysep:PROC
sysep:      syscallm     zysep,12

      PUBLIC sysex
      EXTERN zysex:PROC
sysex:      mov   m_word [reg_xs],rsp
      syscallm     zysex,13

      PUBLIC sysfc
      EXTERN zysfc:PROC
sysfc:      pop   rax         ; <<<<remove stacked scblk>>>>
      lea   rsp,[rsp+rdx*cfp_b]
      push  rax
      syscallm     zysfc,14

      PUBLIC sysgc
      EXTERN zysgc:PROC
sysgc:      syscallm     zysgc,15

      PUBLIC syshs
      EXTERN zyshs:PROC
syshs:      mov   m_word [reg_xs],rsp
      syscallm     zyshs,16

      PUBLIC sysid
      EXTERN zysid:PROC
sysid:      syscallm     zysid,17

      PUBLIC sysif
      EXTERN zysif:PROC
sysif:      syscallm     zysif,18

      PUBLIC sysil
      EXTERN zysil:PROC
sysil:      syscallm zysil,19

      PUBLIC sysin
      EXTERN zysin:PROC
sysin:      syscallm     zysin,20

      PUBLIC sysio
      EXTERN zysio:PROC
sysio:      syscallm     zysio,21

      PUBLIC sysld
      EXTERN zysld:PROC
sysld:      syscallm zysld,22

      PUBLIC sysmm
      EXTERN zysmm:PROC
sysmm:      syscallm     zysmm,23

      PUBLIC sysmx
      EXTERN zysmx:PROC
sysmx:      syscallm     zysmx,24

      PUBLIC sysou
      EXTERN zysou:PROC
sysou:      syscallm     zysou,25

      PUBLIC syspi
      EXTERN zyspi:PROC
syspi:      syscallm     zyspi,26

      PUBLIC syspl
      EXTERN zyspl:PROC
syspl:      syscallm     zyspl,27

      PUBLIC syspp
      EXTERN zyspp:PROC
syspp:      syscallm     zyspp,28

      PUBLIC syspr
      EXTERN zyspr:PROC
syspr:      syscallm     zyspr,29

      PUBLIC sysrd
      EXTERN zysrd:PROC
sysrd:      syscallm     zysrd,30

      PUBLIC sysri
      EXTERN zysri:PROC
sysri:      syscallm     zysri,32

      PUBLIC sysrw
      EXTERN zysrw:PROC
sysrw:      syscallm     zysrw,33

      PUBLIC sysst
      EXTERN zysst:PROC
sysst:      syscallm     zysst,34

      PUBLIC systm
      EXTERN zystm:PROC
systm:      syscallm     zystm,35

      PUBLIC systt
      EXTERN zystt:PROC
systt:      syscallm     zystt,36

      PUBLIC sysul
      EXTERN zysul:PROC
sysul:      syscallm     zysul,37

      PUBLIC sysxi
      EXTERN zysxi:PROC
sysxi:      mov   m_word [reg_xs],rsp
      syscallm     zysxi,38

      callext MACRO p1, p2
      EXTERN p1:PROC
      call  p1
      add   rsp,p2            ; pop arguments
          ENDM

            math_op MACRO p1, p2
      PUBLIC p1
      EXTERN p2:PROC
p1:
      push  rbp
      mov   rbp,rsp
      mov   m_word [save_wa],rcx
      mov   m_word [save_wc],rdx
      movsd m_real [reg_ra],ra
      ldmxcsr [mxcsr_set]
      and   rsp,0fffffffffffffff0h
      sub   rsp,32
      call  p2
      movsd ra,m_real [reg_ra]
      mov   rcx,m_word [save_wa]
      mov   rdx,m_word [save_wc]
      mov   rsp,rbp
      pop   rbp
      ret
                ENDM

      math_op     atn_,f_atn
      math_op     chp_,f_chp
      math_op     cos_,f_cos
      math_op     etx_,f_etx
      math_op     lnf_,f_lnf
      math_op     sin_,f_sin
      math_op     sqr_,f_sqr
      math_op     tan_,f_tan

      PUBLIC get_fp                  ; get frame pointer

get_fp:
       mov   rax,m_word [reg_xs]     ; minimal's xs
       add   rax,4                  ; pop return from call to sysbx or sysxi
       ret                    ; done

      EXTERN rereloc:PROC

      PUBLIC restart
      EXTERN stbas:QWORD
      EXTERN statb:QWORD
      EXTERN stage:QWORD
      EXTERN gbcnt:QWORD
      EXTERN lmodstk:QWORD
      EXTERN startbrk:PROC
      EXTERN outptr:QWORD
      EXTERN swcoup:PROC
;     scstr is offset to start of string in scblk, or two words
scstr equ   cfp_c+cfp_c

;
restart:
      pop   rax                ; discard return
      pop   rax                     ; discard dummy
      pop   rax                     ; get lowest legal stack value

      add   rax,m_word [stacksiz]   ; top of compiler's stack
      mov   rsp,rax                       ; switch to this stack
      call  stackinit         ; initialize minimal stack

                              ; set up for stack relocation
      lea   rax,[tscblk+scstr]       ; top of saved stack
      mov   rbx,m_word [lmodstk]          ; bottom of saved stack
      mov   rcx,m_word [stbas]      ; rcx = stbas from exit() time
      sub   rbx,rax                       ; wb = size of saved stack
      mov   rdx,rcx
      sub   rdx,rbx                       ; rdx = stack bottom from exit() time
      mov   rbx,rcx
      sub   rbx,rsp                       ; rbx =      stbas - new stbas

      mov   m_word [stbas],rsp       ; save initial sp
;      getoff      rax,dffnc         ; get address of ppm offset
      mov   m_word [ppoff],rax       ; save for use later
;
;     restore stack from tscblk.
;
      mov   rsi,m_word [lmodstk]          ; -> bottom word of stack in tscblk
      lea   rdi,[tscblk+scstr]            ; -> top word of stack
      cmp   rsi,rdi                       ; any stack to transfer?
      je    re3               ;  skip if not
      sub   rsi,4
      std
re1:  lodsd                   ; get old stack word to rax
      cmp   rax,rdx                       ; below old stack bottom?
      jb    re2               ;   j. if rax < rdx
      cmp   rax,rcx                       ; above old stack top?
      ja    re2               ;   j. if rax > rcx
      sub   rax,rbx                       ; within old stack, perform relocation
re2:  push  rax                     ; transfer word of stack
      cmp   rsi,rdi                       ; if not at end of relocation then
      jae   re1               ;    loop back

re3:  cld
      mov   m_word [compsp],rsp           ; save compiler's stack pointer
      mov   rsp,m_word [osisp]            ; back to osint's stack pointer
      and   rsp,0fffffffffffffff0h  ; 16byte alignment
      call   rereloc                ; relocate compiler pointers into stack
      mov   rax,m_word [statb]            ; start of static region to rdi
      mov   m_word [reg_xr],rax
      mov   rax,minimal_insta
      jmp   minimal                 ; initialize static region
                              ; was a call, but there is nothing to return to.  This was probably for
                              ; debugging purposes.

;
;     now pretend that we're executing the following c statement from
;     function zysxi:
;
;           return      normal_return;
;
;     if the load module was invoked by exit(), the return path is
;     as follows:  back to ccaller, back to s$ext following sysxi call,
;     back to user program following exit() call.
;
;     alternately, the user specified -w as a command line option, and
;     sysbx called makeexec, which in turn called sysxi.  the return path
;     should be:  back to ccaller, back to makeexec following sysxi call,
;     back to sysbx, back to minimal code.  if we allowed this to happen,
;     then it would require that stacked return address to sysbx still be
;     valid, which may not be true if some of the c programs have changed
;     size.  instead, we clear the stack and execute the restart code that
;     simulates resumption just past the sysbx call in the minimal code.
;     we distinguish this case by noting the variable stage is 4.
;
      call   startbrk               ; start control-c logic

      mov   rax,m_word [stage]            ; is this a -w call?
      cmp   rax,4
      je          re4         ; yes, do a complete fudge

;
;     jump back with return value = normal_return
      xor   rax,rax                 ; set to zero to indicate normal return
      call  syscall_exit
      ret

;     here if -w produced load module.  simulate all the code that
;     would occur if we naively returned to sysbx.  clear the stack and
;     go for it.
;
re4:  mov   rax,m_word [stbas]
      mov   m_word [compsp],rax           ; empty the stack

;     code that would be executed if we had returned to makeexec:
;
      mov   m_word [gbcnt],0  ; reset garbage collect count
      call  zystm             ; fetch execution time to reg_ia
      mov   rax,m_word [reg_ia]           ; set time into compiler
      EXTERN timsx:QWORD
      mov   m_word [timsx],rax

;     code that would be executed if we returned to sysbx:
;
      push  m_word [outptr]         ; swcoup(outptr)
      EXTERN swcoup:PROC
      call  swcoup
      add   rsp,cfp_b

;     jump to minimal code to restart a save file.

      mov   rax,minimal_rstrt
      mov   m_word [minimal_id],rax
      call  minimal                 ; no return



        END
