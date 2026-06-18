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
/       sp    - pointer to the converted argument blocks on the stack.
/               The stack order is the REVERSE of the prototype order, so
/               prototype argument i (eftar[i]) is at sp[nargs-1-i].
/       nargs - number of arguments
/   Returns:
/       0  -> call fails              (zysex EXIT_1 -> statement failure)
/      -1  -> insufficient memory     (zysex EXIT_2 -> err 327)
/      -2  -> improper argument type  (zysex EXIT_3 -> err 326)
/       other -> pointer to the boxed result block
/
/   Type codes (eftar[] / efrsl), per b_efc and the .cnlf model:
/       0 = unconverted   1 = string   2 = integer   3 = real   4 = file
/
/   Converted arguments on the stack are block pointers:
/       integer -> ICBLK   real -> RCBLK   string -> SCBLK   file -> FCBLK
/
/   Result handling: b_efc (befc8) keeps a result that is already in the
/   dynamic region as-is, so we allocate the result block with minimal_alloc
/   (dynamic, reclaimable) -- NOT minimal_alost, which is permanent and would
/   leak one block per call -- and set its type word and value ourselves.
/
/   ---------------------------------------------------------------------------
/   ARITY / SIGNATURES SUPPORTED:  0 to 4 arguments, any integer/real/string
/   combination (a file argument is accepted in any non-real position too).
/   ---------------------------------------------------------------------------
/   Win64 places each of the first four arguments by POSITION into either the
/   integer register (RCX/RDX/R8/R9) or the XMM register (XMM0-3) for that
/   position, chosen by type: integers and pointers use the integer register,
/   doubles use XMM.  Integers, strings, and files are therefore placed
/   IDENTICALLY ("N"); only "real vs not" ("R") changes placement.  So a call
/   reduces to a real/not bit-pattern (mask) over the arguments, each pattern
/   being an ordinary C function-pointer cast the compiler lowers to the right
/   registers.  We enumerate all 2^n patterns for n = 0..4 (31 shapes).  Only
/   functions with more than four arguments -- which spill onto the stack --
/   would require an ml64 trampoline; those return -2 (err 326) here.
/   ---------------------------------------------------------------------------
*/

#include "port.h"

#if EXTFUN

/*
/   Minimal view of the loaded-function node.  The full struct xnblk belongs
/   to the blocks32 model, which this build does not include; xnu.ef.xnpfn is
/   the first member of that node's union, i.e. the third word.
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
#define EF_MAXARG 4

/*
/   Invoke pfn for C return type RT, storing the result in dst, selecting the
/   call signature from `shape` = (2^nargs - 1) + mask, where bit i of mask is
/   set when argument i is a real (passed in XMM via dv[i]); otherwise the
/   argument is an integer-register value (integer/string/file via pv[i]).
/   Generated mechanically for n = 0..4 (31 shapes).
*/
#define EF_CALL(RT, dst)                                                      \
    do {                                                                      \
        switch (shape) {                                                      \
        case 0: dst = ((RT (*)(void))pfn)(); break;                      \
        case 1: dst = ((RT (*)(void *))pfn)(pv[0]); break;               \
        case 2: dst = ((RT (*)(double))pfn)(dv[0]); break;               \
        case 3: dst = ((RT (*)(void *, void *))pfn)(pv[0], pv[1]); break;\
        case 4: dst = ((RT (*)(double, void *))pfn)(dv[0], pv[1]); break;\
        case 5: dst = ((RT (*)(void *, double))pfn)(pv[0], dv[1]); break;\
        case 6: dst = ((RT (*)(double, double))pfn)(dv[0], dv[1]); break;\
        case 7: dst = ((RT (*)(void *, void *, void *))pfn)(pv[0], pv[1], pv[2]); break;\
        case 8: dst = ((RT (*)(double, void *, void *))pfn)(dv[0], pv[1], pv[2]); break;\
        case 9: dst = ((RT (*)(void *, double, void *))pfn)(pv[0], dv[1], pv[2]); break;\
        case 10: dst = ((RT (*)(double, double, void *))pfn)(dv[0], dv[1], pv[2]); break;\
        case 11: dst = ((RT (*)(void *, void *, double))pfn)(pv[0], pv[1], dv[2]); break;\
        case 12: dst = ((RT (*)(double, void *, double))pfn)(dv[0], pv[1], dv[2]); break;\
        case 13: dst = ((RT (*)(void *, double, double))pfn)(pv[0], dv[1], dv[2]); break;\
        case 14: dst = ((RT (*)(double, double, double))pfn)(dv[0], dv[1], dv[2]); break;\
        case 15: dst = ((RT (*)(void *, void *, void *, void *))pfn)(pv[0], pv[1], pv[2], pv[3]); break;\
        case 16: dst = ((RT (*)(double, void *, void *, void *))pfn)(dv[0], pv[1], pv[2], pv[3]); break;\
        case 17: dst = ((RT (*)(void *, double, void *, void *))pfn)(pv[0], dv[1], pv[2], pv[3]); break;\
        case 18: dst = ((RT (*)(double, double, void *, void *))pfn)(dv[0], dv[1], pv[2], pv[3]); break;\
        case 19: dst = ((RT (*)(void *, void *, double, void *))pfn)(pv[0], pv[1], dv[2], pv[3]); break;\
        case 20: dst = ((RT (*)(double, void *, double, void *))pfn)(dv[0], pv[1], dv[2], pv[3]); break;\
        case 21: dst = ((RT (*)(void *, double, double, void *))pfn)(pv[0], dv[1], dv[2], pv[3]); break;\
        case 22: dst = ((RT (*)(double, double, double, void *))pfn)(dv[0], dv[1], dv[2], pv[3]); break;\
        case 23: dst = ((RT (*)(void *, void *, void *, double))pfn)(pv[0], pv[1], pv[2], dv[3]); break;\
        case 24: dst = ((RT (*)(double, void *, void *, double))pfn)(dv[0], pv[1], pv[2], dv[3]); break;\
        case 25: dst = ((RT (*)(void *, double, void *, double))pfn)(pv[0], dv[1], pv[2], dv[3]); break;\
        case 26: dst = ((RT (*)(double, double, void *, double))pfn)(dv[0], dv[1], pv[2], dv[3]); break;\
        case 27: dst = ((RT (*)(void *, void *, double, double))pfn)(pv[0], pv[1], dv[2], dv[3]); break;\
        case 28: dst = ((RT (*)(double, void *, double, double))pfn)(dv[0], pv[1], dv[2], dv[3]); break;\
        case 29: dst = ((RT (*)(void *, double, double, double))pfn)(pv[0], dv[1], dv[2], dv[3]); break;\
        case 30: dst = ((RT (*)(double, double, double, double))pfn)(dv[0], dv[1], dv[2], dv[3]); break;\
        default: dst = 0; break;                                         \
        }                                                                     \
    } while (0)


/*
/   Allocate nbytes in the dynamic region and return the block.  Mirrors the
/   discipline proven in loadef: MINSAVE protects live collectable registers
/   across a possible garbage collection, the block is captured from XR before
/   MINRESTORE, and minimal_alloc keeps the result in dynamic storage so b_efc
/   returns it without an extra copy.
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

static void
ef_freeargs(char **cb)
{
    int i;
    for (i = 0; i < EF_MAXARG; i++)
        if (cb[i]) free(cb[i]);
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
        return (union block *)-2;   /* >4 args need a trampoline -> 326 */

    for (i = 0; i < EF_MAXARG; i++) {
        pv[i] = 0; dv[i] = 0.0; cb[i] = 0; isreal[i] = 0;
    }

    /*
    /   The stack order is the REVERSE of the prototype order: prototype
    /   argument i (eftar[i]) is found at sp[nargs-1-i].  Index pv[]/dv[] by
    /   prototype position so EF_CALL passes them in C order.
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
                ef_freeargs(cb);
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

    {   /* shape = (2^nargs - 1) + mask, mask bit i = argument i is real */
        int mask = 0;
        for (i = 0; i < nargs; i++)
            mask |= (isreal[i] << i);
        shape = ((1 << nargs) - 1) + mask;
    }

    switch (rtype) {

    case 2: {                                   /* integer result */
        long r;
        EF_CALL(long, r);
        ef_freeargs(cb);
        blk = ef_alloc(2 * sizeof(word));
        if (!blk) return (union block *)-1;
        ((word *)blk)[0] = TYPE_ICL;
        ((word *)blk)[1] = (word)r;             /* icval (sign-extended) */
        return (union block *)blk;
    }

    case 3: {                                   /* real result */
        double r;
        EF_CALL(double, r);
        ef_freeargs(cb);
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
            ef_freeargs(cb);
            return (union block *)-1;
        }
        ((word *)out)[0] = TYPE_SCL;
        out->len = n;
        for (i = 0; i < n; i++)
            out->str[i] = r[i];
        while (i < EF_WORDS_UP(n))              /* zero-pad final word */
            out->str[i++] = '\0';
        ef_freeargs(cb);                        /* r may alias cb[*]; copied */
        return (union block *)out;
    }

    default:                                    /* unsupported result type */
        ef_freeargs(cb);
        return (union block *)-2;               /* improper -> 326 */
    }
}

#endif /* EXTFUN */
