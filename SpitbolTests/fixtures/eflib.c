/* eflib.c -- fixture library for SPITBOL external-function tests.
 *
 * Build (x64 Native Tools for VS Command Prompt -- NOT the plain prompt):
 *     cl /LD /O2 eflib.c
 * Confirm it is 64-bit before use:
 *     dumpbin /headers eflib.dll        (expect "8664 machine (x64)")
 *
 * efstr now REVERSES its argument in place and returns it, so a correct
 * 'abc' -> 'cba' result proves data actually round-trips through the DLL and
 * back -- the call really happened, it did not just return something. The
 * other three bodies stay trivial.
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
