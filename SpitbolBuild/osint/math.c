
/*
Copyright 1987-2012 Robert B. K. Dewar and Mark Emmer.
Copyright 2012-2017 David Shields
*/

/*
 * math.c - extended math support for spitbol
 *
 * Routines not provided by hardware floating point.
 *
 * These routines are not called from other C routines.  Rather they
 * are called by inter.*, and by external functions.
 */

#include <errno.h>

#include <math.h>
#include <float.h>

#include "port.h"

#ifndef errno
int errno;
# endif

extern double inf;     /* infinity */
extern word reg_flerr; /* Floating point error */

/*
 * underflowed - true only when reg_ra holds a genuine subnormal underflow
 * result (a nonzero magnitude below the smallest normal double).  We test the
 * RESULT rather than the sticky MXCSR underflow flag: library routines such as
 * the Windows CRT atan() raise that flag for a spurious intermediate (e.g. x^3
 * for tiny x) while returning a perfectly normal result, which previously
 * caused correct answers to be forced to zero.  glibc does not raise the flag
 * in those cases, which is why the bug only showed on the Windows build.
 */
#define underflowed(r) ((r) != 0.0 && fabs(r) < DBL_MIN)

/*
 * f_atn - arctangent
 */
void
f_atn(void)
{
    reg_ra = atan(reg_ra);
    if(underflowed(reg_ra))
        reg_ra = 0;
}

/*
 * f_chp - chop
 */
void
f_chp(void)
{
    if(reg_ra >= 0.0)
        reg_ra = floor(reg_ra);
    else
        reg_ra = ceil(reg_ra);
}

/*
 * f_cos - cosine
 */
void
f_cos(void)
{
    reg_ra = cos(reg_ra);
    if(underflowed(reg_ra))
        reg_ra = 0;
}

/*
 * f_etx - e to the x
 */
void
f_etx(void)
{
    errno = 0;
    reg_ra = exp(reg_ra);
    if(underflowed(reg_ra))
        reg_ra = 0;
    else if(errno)
        reg_ra = inf;
}

/*
 * f_lnf - natural log
 */
void
f_lnf(void)
{
    errno = 0;
    reg_ra = log(reg_ra);
    if(underflowed(reg_ra))
        reg_ra = 0;
    else if(errno)
        reg_ra = inf;
}

/*
 * f_sin - sine
 */
void
f_sin(void)
{
    reg_ra = sin(reg_ra);
    if(underflowed(reg_ra))
        reg_ra = 0;
}

/*
 * f_sqr - square root  (range checked by caller)
 */
void
f_sqr(void)
{
    reg_ra = sqrt(reg_ra);
    if(underflowed(reg_ra))
        reg_ra = 0;
}

/*
 * f_tan - tangent
 */
void
f_tan(void)
{
    errno = 0;
    reg_ra = tan(reg_ra);
    if(underflowed(reg_ra))
        reg_ra = 0;
    else if(errno)
        reg_ra = inf;
}
