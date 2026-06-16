
/*
Copyright 1987-2012 Robert B. K. Dewar and Mark Emmer.
Copyright 2012-2017 David Shields
*/

/*    zysld - load external function */

/* */

/*    Parameters: */

/*        XR - pointer to SCBLK containing function name */

/*        XL - pointer to SCBLK containing library name */

/*    Returns: */

/*        XR - pointer to code (or other data structure) to be stored in the EFBLK. */

/*    Exits: */

/*        1 - function does not exist */

/*        2 - I/O error loading function */

/*        3 - insufficient memory */

/* */

/* */

/*    WARNING:  THIS FUNCTION CALLS A FUNCTION WHICH MAY INVOKE A GARBAGE */

/*    COLLECTION.  STACK MUST REMAIN WORD ALIGNED AND COLLECTABLE. */

/* */

#include "port.h"
#ifndef _WIN32
#include <dlfcn.h>
#endif
#include <fcntl.h>

int
zysld()
{
#if EXTFUN
    /* Per the asm contract:  XR = SCBLK with function name,
                              XL = SCBLK with library name.   */
    struct scblk *fnscb = XR(struct scblk *);
    struct scblk *lnscb = XL(struct scblk *);
    char  fnName[256];
    char  libName[260];
    PFN   pfn = 0;
    mword handle;
    void *node;
    word  i, n;

    /* SCBLKs are not NUL-terminated: copy len bytes, then terminate. */
    n = fnscb->len;
    if(n > (word)(sizeof(fnName) - 1)) n = sizeof(fnName) - 1;
    for(i = 0; i < n; i++) fnName[i] = fnscb->str[i];
    fnName[n] = '\0';

    n = lnscb->len;
    if(n > (word)(sizeof(libName) - 1)) n = sizeof(libName) - 1;
    for(i = 0; i < n; i++) libName[i] = lnscb->str[i];
    libName[n] = '\0';

    handle = loadDll(libName, fnName, &pfn);
    if(handle == (mword)-1)
        return EXIT_1;                   /* library/function not found -> err 142 */

    node = loadef(handle, (char *)&pfn); /* loadef reads pfn = *(PFN *)arg */
    switch((word)node) {
    case(word)0:
        return EXIT_2;                   /* I/O error */
    case(word)-2:
        return EXIT_3;                   /* insufficient memory */
    default:
        SET_XR(node);
        return NORMAL_RETURN;            /* success: XR = node stored in EFBLK->efcod */
    }
#else  /* EXTFUN */
    return EXIT_1;
#endif /* EXTFUN */
}
