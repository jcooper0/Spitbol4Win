/*
Copyright 1987-2012 Robert B. K. Dewar and Mark Emmer.
Copyright 2012-2017 David Shields
*/

/*
/   callef - invoke a loaded external function (called from sysex.c / zysex)
/
/   Parameters:
/       efb   - EFBLK describing the function (efcod -> node whose
/               xnu.ef.xnpfn is the entry point; eftar[] = argument type
/               codes; efrsl = result type code)
/       sp    - pointer to the converted argument blocks on the stack
/               (sp[0] is the first argument; zysex has already skipped the
/               return word)
/       nargs - number of arguments
/   Returns:
/       0  -> call fails              (zysex EXIT_1)
/      -1  -> insufficient memory     (zysex EXIT_2 -> err 327)
/      -2  -> improper argument type  (zysex EXIT_3 -> err 326)
/       other -> pointer to the boxed result block
/
/   Type codes (eftar[] / efrsl), per b_efc and the .cnlf model:
/       0 = unconverted   1 = string   2 = integer   3 = real   4 = file
/
/   Converted arguments on the stack are block pointers, exactly as the
/   per-argument conversion routines leave them:
/       integer -> ICBLK   real -> RCBLK   string -> SCBLK   file -> FCBLK
/
/   Result handling: b_efc (befc8) keeps a result that is already in the
/   dynamic region as-is, so we allocate the result block with minimal_alloc
/   (dynamic, reclaimable) -- NOT minimal_alost, which is permanent and would
/   leak one block per call -- and set its type word and value ourselves.
/
/   ---------------------------------------------------------------------------
/   ARITY / SIGNATURES SUPPORTED
/   ---------------------------------------------------------------------------
/   Zero, one, and two arguments in any integer/real/string combination (a
/   file argument is accepted in any non-real position too).  This covers the
/   common case without an assembly trampoline.
/
/   The key fact about the Win64 calling convention: each of the first four
/   arguments is placed by POSITION into either the integer register
/   (RCX/RDX/R8/R9) or the XMM register (XMM0-3) for that position, chosen by
/   type -- integers and pointers use the integer register, doubles use XMM.
/   Integers and pointers (so: integer, string, file) are therefore placed
/   IDENTICALLY; only "real vs not" changes placement.  That collapses a
/   two-argument call to four shapes (NN, NR, RN, RR), each expressible as an
/   ordinary C function-pointer cast that the compiler lowers to the correct
/   register placement.  This generalizes to three and four arguments the same
/   way (2^n shapes); only functions with more than four arguments -- which
/   spill onto the stack -- would require the ml64 trampoline.
/   ---------------------------------------------------------------------------
*/

#include "port.h"

#if EXTFUN

/*
/   Minimal view of the loaded-function node.  The full struct xnblk belongs
/   to the blocks32 model, which this build does not include; xnu.ef.xnpfn is
/   the first member of that node's union, i.e. the third word.  We read only
/   that field, so a self-contained prefix avoids depending on xnblk's
/   visibility here.
*/
struct xn_efview {
    word  xntyp;
    word  xnlen;
    void *xnpfn;        /* == xnu.ef.xnpfn : the entry point */
};

/* Round a byte count up to a whole number of words. */
#define EF_WORDS_UP(n) \
    ((word)(((n) + (word)sizeof(word) - 1) & ~(word)(sizeof(word) - 1)))

/* Maximum register-passed arguments handled here (Win64: 4). */
#define EF_MAXARG 2

/*
/   Invoke pfn for a given C return type RT, storing the result in dst.  The
/   shape selector encodes arity and the real-vs-not placement pattern:
/       0            : ()
/       1 / 2        : (N) / (R)
/       3 / 4 / 5 / 6: (N,N) / (N,R) / (R,N) / (R,R)
/   where N is an integer-register value (passed as void*: integer, string,
/   or file) and R is a double passed in XMM.  pv[]/dv[] hold the marshalled
/   operands; the compiler places each one correctly from the cast prototype.
*/
#define EF_CALL(RT, dst)                                                      \
    do {                                                                      \
        switch (shape) {                                                      \
        case 0: dst = ((RT (*)(void))pfn)();                       break;     \
        case 1: dst = ((RT (*)(void *))pfn)(pv[0]);                break;     \
        case 2: dst = ((RT (*)(double))pfn)(dv[0]);                break;     \
        case 3: dst = ((RT (*)(void *, void *))pfn)(pv[0], pv[1]); break;     \
        case 4: dst = ((RT (*)(void *, double))pfn)(pv[0], dv[1]); break;     \
        case 5: dst = ((RT (*)(double, void *))pfn)(dv[0], pv[1]); break;     \
        case 6: dst = ((RT (*)(double, double))pfn)(dv[0], dv[1]); break;     \
        default: dst = 0;                                          break;     \
        }                                                                     \
    } while (0)

/*
/   Allocate nbytes in the dynamic region and return the block.  Mirrors the
/   allocation discipline proven in loadef: MINSAVE pushes the live
/   collectable registers as GC roots across a possible garbage collection,
/   the block is captured from XR before MINRESTORE, and minimal_alloc keeps
/   the result in dynamic storage so b_efc returns it without an extra copy.
*/
static void *
ef_alloc(word nbytes)
{
    void *blk;
    MINSAVE();
    SET_WA(nbytes);
    MINIMAL(minimal_alloc);
    blk = XR(void *);
    MINRESTORE();
    return blk;
}

union block *
callef(struct efblk *efb, union block **sp, word nargs)
{
    void *pfn = ((struct xn_efview *)efb->efcod)->xnpfn;
    word  rtype = efb->efrsl;
    void *blk;

    void  *pv[EF_MAXARG];       /* integer-register operands (int/str/file) */
    double dv[EF_MAXARG];       /* XMM operands (real) */
    char  *cb[EF_MAXARG];       /* NUL-terminated copies of string args */
    int    isreal[EF_MAXARG];
    int    shape;
    word   i;

    if (nargs > EF_MAXARG)
        return (union block *)-2;   /* >2 args not handled here -> 326 */

    for (i = 0; i < EF_MAXARG; i++) {
        pv[i] = 0; dv[i] = 0.0; cb[i] = 0; isreal[i] = 0;
    }

    /*
    /   The stack order is the REVERSE of the prototype order: b_efc walks the
    /   stack upward while walking eftar downward, and zysex points sp at the
    /   lowest slot, so prototype argument i (eftar[i]) is found at
    /   sp[nargs-1-i].  (For a single argument the two coincide.)  We index
    /   pv[]/dv[] by prototype position so EF_CALL passes them in C order.
    */
    for (i = 0; i < nargs; i++) {
        word t = efb->eftar[i];
        union block *ab = sp[nargs - 1 - i];
        if (t == 3) {                           /* real -> XMM */
            dv[i] = ((struct rcblk *)ab)->rcval;
            isreal[i] = 1;
        } else if (t == 2) {                    /* integer -> int register */
            pv[i] = (void *)(word)((struct icblk *)ab)->val;
        } else if (t == 1) {                    /* string -> char* (NUL-term) */
            struct scblk *s = (struct scblk *)ab;
            word k, n = s->len;
            cb[i] = (char *)malloc((size_t)n + 1);
            if (!cb[i]) {
                word j;
                for (j = 0; j < i; j++) if (cb[j]) free(cb[j]);
                return (union block *)-1;       /* out of memory -> 327 */
            }
            for (k = 0; k < n; k++)
                cb[i][k] = s->str[k];
            cb[i][n] = '\0';
            pv[i] = (void *)cb[i];
        } else {                                /* file / unconverted -> ptr */
            pv[i] = (void *)ab;
        }
    }

    if (nargs == 0)
        shape = 0;
    else if (nargs == 1)
        shape = 1 + isreal[0];                  /* 1=N, 2=R */
    else
        shape = 3 + (isreal[0] * 2 + isreal[1]);/* 3=NN 4=NR 5=RN 6=RR */

    switch (rtype) {

    case 2: {                                   /* integer result */
        long r;
        EF_CALL(long, r);
        if (cb[0]) free(cb[0]);
        if (cb[1]) free(cb[1]);
        blk = ef_alloc(2 * sizeof(word));
        if (!blk) return (union block *)-1;
        ((word *)blk)[0] = TYPE_ICL;
        ((word *)blk)[1] = (word)r;             /* icval (sign-extended) */
        return (union block *)blk;
    }

    case 3: {                                   /* real result */
        double r;
        EF_CALL(double, r);
        if (cb[0]) free(cb[0]);
        if (cb[1]) free(cb[1]);
        blk = ef_alloc(2 * sizeof(word));
        if (!blk) return (union block *)-1;
        ((word *)blk)[0] = TYPE_RCL;
        ((struct rcblk *)blk)->rcval = r;
        return (union block *)blk;
    }

    case 1: {                                   /* string result */
        char *r;
        struct scblk *out;
        word n, total;
        EF_CALL(char *, r);
        n = 0;
        if (r) while (r[n]) n++;                /* strlen of the C result */
        total = (word)(2 * sizeof(word)) + EF_WORDS_UP(n);
        out = (struct scblk *)ef_alloc(total);  /* box BEFORE freeing cb[] */
        if (!out) {
            if (cb[0]) free(cb[0]);
            if (cb[1]) free(cb[1]);
            return (union block *)-1;
        }
        ((word *)out)[0] = TYPE_SCL;
        out->len = n;
        for (i = 0; i < n; i++)
            out->str[i] = r[i];
        while (i < EF_WORDS_UP(n))              /* zero-pad final word */
            out->str[i++] = '\0';
        if (cb[0]) free(cb[0]);                 /* r may alias cb[0]; copied */
        if (cb[1]) free(cb[1]);
        return (union block *)out;
    }

    default:                                    /* unsupported result type */
        if (cb[0]) free(cb[0]);
        if (cb[1]) free(cb[1]);
        return (union block *)-2;               /* improper -> 326 */
    }
}

#endif /* EXTFUN */
