/* eflib.c -- fixture library for SPITBOL external-function error tests.
 *
 * Build (MSVC, x64), produces eflib.dll next to the test exe / on PATH:
 *     cl /LD /O2 eflib.c
 *
 * Bodies are intentionally trivial. The argument-type errors (39/40/265/298)
 * are raised by SPITBOL during argument conversion BEFORE these run, so the
 * functions only need to exist as resolvable exports for LOAD() to succeed.
 *
 * NOTE: this is native C by necessity -- LOAD() resolves C-ABI symbols via
 * GetProcAddress/dlsym, which a managed C# assembly does not provide. The
 * usual "tooling in C#" preference does not apply to a loadable fixture DLL.
 */
#ifdef _WIN32
#  define EXPORT __declspec(dllexport)
#else
#  define EXPORT
#endif

EXPORT long   efint (long x)   { return x + 1; }
EXPORT char  *efstr (char *s)  { return s; }
EXPORT double efreal(double r) { return r * 2.0; }
EXPORT long   effile(void *f)  { (void)f; return 0; }
