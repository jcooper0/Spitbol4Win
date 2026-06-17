/*
Copyright 1987-2012 Robert B. K. Dewar and Mark Emmer.
Copyright 2012-2017 David Shields
*/

/*
/   regsave - pushregs() / popregs()  (Spitbol4Win)
/
/   These are MINSAVE()/MINRESTORE() (see osint.h).  An osint C routine that
/   needs to call back into the MINIMAL code -- in a way that the MINIMAL code
/   might itself trigger another sysxx call or a garbage collection before
/   returning -- brackets that call with pushregs()/popregs() so the live
/   MINIMAL/interface register values survive.
/
/   On the original Linux build these were a few lines of hand-written
/   assembly (see the "save and restore minimal and interface registers"
/   notes that survive in int.asm / int_masm.asm).  On this x64 Windows port
/   the MINIMAL registers do not live in CPU registers while C code runs --
/   the syscall interface has already spilled them to the reg_* memory cells
/   of reg_block (and saved the compiler stack pointer in compsp).  That makes
/   pushregs/popregs expressible in plain portable C operating on those same
/   cells, with no new assembly and no change to the .asm generation pipeline.
/
/   Contract (verbatim from the int.asm notes):
/
/     note 1: pushregs returns a collectable value in xl, safe for a
/             subsequent call to a memory-allocation routine.
/     note 2: these are NOT recursive routines.  Only reg_xl is saved on the
/             compiler stack, where it is visible to (and relocatable by) the
/             garbage collector.  The other registers are just moved to a
/             temp area.
/     note 3: popregs does NOT restore reg_cp, because the MINIMAL routine
/             called in between may have changed it as a result of a garbage
/             collection.  (A nested sysxx call is fine: MINIMAL preserves cp.)
/     note 4: if there is no compiler stack yet, do not bother saving xl on
/             the stack.  This only happens when nextef is called from sysxi
/             while reloading a save file.
/
/   Why this works against minimal():  minimal() reads [compsp] afresh on every
/   call to position the compiler stack pointer, scans the compiler stack from
/   there for GC roots, and -- because the allocation routines are stack
/   balanced -- returns with the stack at that same depth.  It never writes
/   compsp back.  So decrementing compsp here and storing reg_xl makes that
/   slot the top-most GC root for the bracketed MINIMAL call; on return the
/   slot holds the (possibly relocated) value, which popregs reads back.  This
/   mirrors exactly the single 8-byte word the original assembly pushed.
/
/   Non-reentrant by design (note 2): a single static save area is used.
*/

#include "port.h"

#if EXTFUN

/* reg_ia, reg_w0, reg_wa, reg_wb, reg_wc, reg_xr, reg_xl (word) and reg_ra
   (double) are the reg_block cells declared in osint.h; compsp (void *) is
   the compiler stack pointer declared in globals.h.  reg_cp is deliberately
   left out (note 3); reg_xs (the MINIMAL stack pointer) and reg_w0 are not
   touched by minimal() itself, so they are stable across the bracketed call
   -- reg_w0 is saved here anyway for faithfulness, reg_xs is left alone. */

static word   sv_ia, sv_w0, sv_wa, sv_wb, sv_wc, sv_xr, sv_xl;
static double sv_ra;
static word  *sv_xlslot; /* compiler-stack slot holding the GC-visible xl, or 0 */

void
pushregs(void)
{
    sv_ia = reg_ia;
    sv_w0 = reg_w0;
    sv_wa = reg_wa;
    sv_wb = reg_wb;
    sv_wc = reg_wc;
    sv_xr = reg_xr;
    sv_ra = reg_ra;
    sv_xl = reg_xl;

    if(compsp) {
        /* Push reg_xl onto the compiler stack (grows down, word-wide slots)
           so the garbage collector can see and relocate it across the
           allocation performed by the bracketed MINIMAL call. */
        compsp = (char *)compsp - sizeof(word);
        *(word *)compsp = reg_xl;
        sv_xlslot = (word *)compsp;
    } else {
        /* note 4: no compiler stack yet -- nothing to make GC-visible. */
        sv_xlslot = (word *)0;
    }
    /* note 1: reg_xl still holds the collectable value on return. */
}

void
popregs(void)
{
    reg_ia = sv_ia;
    reg_w0 = sv_w0;
    reg_wa = sv_wa;
    reg_wb = sv_wb;
    reg_wc = sv_wc;
    reg_xr = sv_xr;
    reg_ra = sv_ra;
    /* note 3: reg_cp is intentionally NOT restored. */

    if(sv_xlslot) {
        /* Recover the (possibly relocated) xl and pop the compiler stack. */
        reg_xl = *sv_xlslot;
        compsp = (char *)compsp + sizeof(word);
        sv_xlslot = (word *)0;
    } else {
        reg_xl = sv_xl;
    }
}

#endif /* EXTFUN */
