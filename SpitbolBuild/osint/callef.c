/*
Copyright 1987-2012 Robert B. K. Dewar and Mark Emmer.
Copyright 2012-2017 David Shields
*/

/*
/   callef - invoke a loaded external function (called from sysex.c / zysex)
/
/   Parameters:
/       efb   - EFBLK describing the function (efcod -> node with the entry
/               point in xnu.ef.xnpfn; eftar[] = arg types; efrsl = result type)
/       sp    - pointer to the converted argument blocks on the stack
/       nargs - number of arguments
/   Returns:
/       0  -> call fails              (zysex EXIT_1)
/      -1  -> insufficient memory     (zysex EXIT_2 -> err 327)
/      -2  -> improper argument type  (zysex EXIT_3 -> err 326)
/       other -> pointer to the boxed result block
/
/   ---------------------------------------------------------------------------
/   MILESTONE A (loader) STATUS
/   ---------------------------------------------------------------------------
/   The loader (zysld -> loadDll -> loadef) is wired, so LOAD() now builds a
/   valid EFBLK and the interpreter's per-argument type-conversion dispatch
/   runs.  That dispatch is what raises errors 39/40/265/298, and it executes
/   BEFORE this function is ever entered -- so those error-code tests pass with
/   this stub in place.
/
/   The actual x64 call (marshalling the typed arguments into the Win64 calling
/   convention, invoking xnpfn, and boxing the typed result) is Milestone B and
/   requires an ml64 trampoline.  Until that lands, a *successful*-conversion
/   call cannot be completed, so we report "improper argument type" (-2 -> 326)
/   rather than crash.  No error-code test depends on a successful call.
/   ---------------------------------------------------------------------------
*/

#include "port.h"

#if EXTFUN

union block *
callef(struct efblk *efb, union block **sp, word nargs)
{
    (void)efb;
    (void)sp;
    (void)nargs;

    /* Milestone B (the x64 call trampoline) not yet implemented. */
    return (union block *)-2;   /* improper argument type -> err 326 */
}

#endif /* EXTFUN */
