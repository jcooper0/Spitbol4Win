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
/   MILESTONE B (this file): single-argument calls.
/   ---------------------------------------------------------------------------
/   Zero- and one-argument functions are handled exactly, covering the common
/   case (and all four eflib fixtures).  A single argument is placed correctly
/   by the C compiler from the cast prototype (integer/pointer -> RCX,
/   double -> XMM0), and the result comes back in RAX or XMM0, so no assembly
/   trampoline is needed at this arity.
/
/   Functions of two or more arguments need positional placement across the
/   integer (RCX/RDX/R8/R9) and XMM (XMM0-3) register files for mixed
/   int/float signatures, which cannot be expressed portably in C; that is the
/   next increment and currently reports "improper argument" (-2) rather than
/   risk a malformed call.
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

    /* argument, sorted into one of three call categories */
    long   ai = 0;          /* integer category */
    double ad = 0.0;        /* real category */
    void  *ap = 0;          /* pointer category (string / file / unconverted) */
    int    acat = 0;        /* 0 none, 1 int, 2 real, 3 ptr */
    char  *cstr = 0;        /* NUL-terminated copy of a string argument */

    if (nargs > 1)
        return (union block *)-2;   /* multi-arg not yet supported -> 326 */

    if (nargs == 1) {
        word atype = efb->eftar[0];
        switch (atype) {
        case 2:                                 /* integer */
            ai = (long)((struct icblk *)sp[0])->val;
            acat = 1;
            break;
        case 3:                                 /* real */
            ad = ((struct rcblk *)sp[0])->rcval;
            acat = 2;
            break;
        case 1: {                               /* string -> char* (NUL-term) */
            struct scblk *s = (struct scblk *)sp[0];
            word i, n = s->len;
            cstr = (char *)malloc((size_t)n + 1);
            if (!cstr)
                return (union block *)-1;       /* out of memory -> 327 */
            for (i = 0; i < n; i++)
                cstr[i] = s->str[i];
            cstr[n] = '\0';
            ap = (void *)cstr;
            acat = 3;
            break;
        }
        case 4:                                 /* file -> FCBLK pointer */
        default:                                /* unconverted -> block ptr */
            ap = (void *)sp[0];
            acat = 3;
            break;
        }
    }

    switch (rtype) {

    case 2: {                                   /* integer result */
        long r;
        if      (acat == 1) r = ((long (*)(long))pfn)(ai);
        else if (acat == 2) r = ((long (*)(double))pfn)(ad);
        else if (acat == 3) r = ((long (*)(void *))pfn)(ap);
        else                r = ((long (*)(void))pfn)();
        if (cstr) free(cstr);
        blk = ef_alloc(2 * sizeof(word));
        if (!blk) return (union block *)-1;
        ((word *)blk)[0] = TYPE_ICL;
        ((word *)blk)[1] = (word)r;             /* icval (sign-extended) */
        return (union block *)blk;
    }

    case 3: {                                   /* real result */
        double r;
        if      (acat == 1) r = ((double (*)(long))pfn)(ai);
        else if (acat == 2) r = ((double (*)(double))pfn)(ad);
        else if (acat == 3) r = ((double (*)(void *))pfn)(ap);
        else                r = ((double (*)(void))pfn)();
        if (cstr) free(cstr);
        blk = ef_alloc(2 * sizeof(word));
        if (!blk) return (union block *)-1;
        ((word *)blk)[0] = TYPE_RCL;
        ((struct rcblk *)blk)->rcval = r;
        return (union block *)blk;
    }

    case 1: {                                   /* string result */
        char *r;
        struct scblk *out;
        word i, n, total;
        if      (acat == 1) r = ((char *(*)(long))pfn)(ai);
        else if (acat == 2) r = ((char *(*)(double))pfn)(ad);
        else if (acat == 3) r = ((char *(*)(void *))pfn)(ap);
        else                r = ((char *(*)(void))pfn)();
        n = 0;
        if (r) while (r[n]) n++;                /* strlen of the C result */
        total = (word)(2 * sizeof(word)) + EF_WORDS_UP(n);
        out = (struct scblk *)ef_alloc(total);
        if (!out) { if (cstr) free(cstr); return (union block *)-1; }
        ((word *)out)[0] = TYPE_SCL;
        out->len = n;
        for (i = 0; i < n; i++)
            out->str[i] = r[i];
        while (i < EF_WORDS_UP(n))              /* zero-pad final word */
            out->str[i++] = '\0';
        if (cstr) free(cstr);
        return (union block *)out;
    }

    default:                                    /* unsupported result type */
        if (cstr) free(cstr);
        return (union block *)-2;               /* improper -> 326 */
    }
}

#endif /* EXTFUN */
