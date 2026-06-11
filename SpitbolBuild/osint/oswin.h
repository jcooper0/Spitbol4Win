/* oswin.h -- Windows (MSVC / MinGW) compatibility shim for SPITBOL osint.
   Included from port.h under _WIN32. Keeps the Linux build untouched. */
#ifndef OSWIN_H
#define OSWIN_H
#ifdef _WIN32

#ifndef _CRT_NONSTDC_NO_WARNINGS
#define _CRT_NONSTDC_NO_WARNINGS 1   /* allow POSIX names: write/open/close/... */
#endif
#ifndef _CRT_SECURE_NO_WARNINGS
#define _CRT_SECURE_NO_WARNINGS 1
#endif

#include <io.h>
#include <fcntl.h>
#include <process.h>

#ifndef O_SYNC
#define O_SYNC 0          /* no write-through flag on Windows open() */
#endif
#ifndef SIGHUP
#define SIGHUP 1          /* absent on Windows; handlers not installed */
#endif
#ifndef SIGQUIT
#define SIGQUIT 3
#endif

#endif /* _WIN32 */
#endif /* OSWIN_H */
