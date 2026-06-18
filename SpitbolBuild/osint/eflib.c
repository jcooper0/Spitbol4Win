/* eflib.c -- fixture library for SPITBOL external-function tests.
 *
 * Build (x64 Native Tools for VS Command Prompt -- NOT the plain prompt):
 *     cl /LD /O2 eflib.c
 * Confirm it is 64-bit before use:
 *     dumpbin /headers eflib.dll        (expect "8664 machine (x64)")
 *
 * One-argument fixtures (LoadCall.sbl):
 *     efint/efreal/effile trivial; efstr REVERSES in place so 'abc'->'cba'
 *     proves data round-trips through the DLL.
 *
 * Two-argument fixtures (LoadCall2.sbl) exercise every Win64 placement shape
 * -- N = integer-register operand (integer OR string), R = XMM operand (real):
 *     efaddii  (int,  int )  N,N   eflenmul (str, int)  N,N (string as N)
 *     efscale  (int,  real)  N,R
 *     efaxpy   (real, int )  R,N
 *     efmul2   (real, real)  R,R
 *
 * NOTE: native C by necessity -- LOAD() resolves C-ABI symbols via
 * GetProcAddress, which a managed C# assembly does not provide. The usual
 * "tooling in C#" preference does not apply to a loadable fixture DLL.
 */
#include <string.h>

#ifdef _WIN32
#  define EXPORT __declspec(dllexport)
#else
#  define EXPORT
#endif

/* ---- one argument ------------------------------------------------------ */
EXPORT long   efint (long x)   { return x + 1; }

EXPORT double efreal(double r) { return r * 2.0; }

EXPORT char  *efstr (char *s)
{
    size_t i, j, n = strlen(s);
    for (i = 0, j = (n ? n - 1 : 0); i < j; i++, j--) {
        char t = s[i];
        s[i] = s[j];
        s[j] = t;
    }
    return s;
}

EXPORT long   effile(void *f)  { (void)f; return 0; }

/* ---- two arguments, every int/real/string placement -------------------- */
EXPORT long   efaddii (long a, long b)     { return a + b; }            /* N,N */
EXPORT double efscale (long n, double a)   { return a * (double)n; }    /* N,R */
EXPORT double efaxpy  (double a, long n)   { return a * (double)n; }    /* R,N */
EXPORT double efmul2  (double a, double b) { return a * b; }            /* R,R */
EXPORT long   eflenmul(char *s, long n)    { return (long)strlen(s) * n; } /* str,int */
