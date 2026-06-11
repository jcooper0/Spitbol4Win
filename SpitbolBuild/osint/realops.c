/*
 * realops.c - real-arithmetic link targets for the Windows/MSVC build.
 *
 * int.dcl declares ldr_/str_/itr_/adr_/sbr_/mlr_/dvr_/ngr_/cpr_/ovr_ as
 * `extern` for the software-float (FLTHDWR=0) path.  On this build the
 * MINIMAL code generator inlines every real op (addsd/subsd/mulsd/divsd,
 * cvtsi2sd, pxor sign-flip, ...), so these routines are never actually
 * called.  GNU ld silently drops the unreferenced externals, but MSVC
 * link.exe reports them as unresolved.  Defining them here satisfies the
 * linker.  Bodies mirror osint/float.c so they stay correct if a
 * software-float code generator ever does call them.
 *
 * Guarded to _WIN32 so the Linux build is unchanged.
 */

#include "port.h"

#ifdef _WIN32

#include <fenv.h>
#include <math.h>
#define FE_SBL_EXCEPT (FE_INVALID | FE_DIVBYZERO | FE_OVERFLOW | FE_UNDERFLOW)

void ldr_(void) { reg_ra = *reg_rp; }                 /* load real            */
void str_(void) { *reg_rp = reg_ra; }                 /* store real           */
void itr_(void) { reg_ra = (double)reg_ia; }          /* integer -> real      */
void ngr_(void) { reg_ra = -reg_ra; }                 /* negate real          */

void adr_(void) { feclearexcept(FE_ALL_EXCEPT); reg_ra += *reg_rp; reg_flerr = fetestexcept(FE_SBL_EXCEPT); }
void sbr_(void) { feclearexcept(FE_ALL_EXCEPT); reg_ra -= *reg_rp; reg_flerr = fetestexcept(FE_SBL_EXCEPT); }
void mlr_(void) { feclearexcept(FE_ALL_EXCEPT); reg_ra *= *reg_rp; reg_flerr = fetestexcept(FE_SBL_EXCEPT); }

void dvr_(void)
{
    feclearexcept(FE_ALL_EXCEPT);
    if (*reg_rp != 0.0) {
        reg_ra /= *reg_rp;
        reg_flerr = fetestexcept(FE_SBL_EXCEPT);
    } else {
        reg_ra = NAN;
        reg_flerr = FE_DIVBYZERO;
    }
}

void cpr_(void)                                       /* compare real vs 0.0  */
{
    reg_fl = (reg_ra == 0.0) ? 0 : (reg_ra < 0.0 ? -1 : 1);
}

void ovr_(void) { reg_ia = (long)reg_ra; }            /* real -> integer      */

#endif /* _WIN32 */
