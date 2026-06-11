
/*
Copyright 1987-2012 Robert B. K. Dewar and Mark Emmer.
Copyright 2012-2017 David Shields
*/

/*
/   The systty module contains two functions, zyspi and zysri, that
/   perform terminal I/O.
/
/   During program execution assignment to variable TERMINAL causes a line
/   to be printed on the terminal.  A call is made to zyspi to actually
/   print the line.
/
/   During program execution a value reference to varible TERMINAL causes
/   a line to be read from the terminal.  A call is made to zysri to actually
/   read the line.
/
/   Under Un*x file descriptor 2 will be used for terminal access.
*/

#include "port.h"

#ifdef _WIN32
# include <io.h>
# include <fcntl.h>
#endif

void
ttyinit()
{
    ttyiobin.bfb = MP_OFF(pttybuf, struct bfblk *);

#ifdef _WIN32
    /*
    /   On Un*x the shell leaves fd 2 opened read/write on the controlling
    /   tty, so TERMINAL input simply reads fd 2 (STDERRFD in globals.h).
    /   On Windows fd 2 wraps a write-only stderr handle, so reads fail
    /   instantly and TERMINAL sees immediate EOF instead of waiting for
    /   keyboard input.
    /
    /   When stderr is an attached console, route TERMINAL input to that
    /   console's keyboard by opening CONIN$ in text mode (so line reads
    /   see "\n", not "\r\n", and Ctrl-Z yields EOF).  When stderr is
    /   redirected (pipe or file -- e.g. under the test harness), leave
    /   fd 2 in place so reads fail -> EOF, matching what the Un*x build
    /   does in the same situation.
    */
    if(_isatty(STDERRFD)) {
        int fd = _open("CONIN$", _O_RDONLY | _O_TEXT);
        if(fd >= 0)
            ttyiobin.fdn = fd;
    }
#endif /* _WIN32 */
}

/*
/   zyspi - print on interactive channel
/
/   zyspi prints a line on the user's terminal.
/
/   Parameters:
/    xr    pointer to SCBLK containing string to print
/    wa    length of string
/   Returns:
/    Nothing
/   Exits:
/    1    failure
*/

int
zyspi()
{
    word retval;

    retval =
        oswrite(1, ttyiobout.len, WA(word), &ttyiobout, XR(struct scblk *));

    /*
       /    Return error if oswrite fails.
     */
    if(retval != 0)
        return EXIT_1;

    return NORMAL_RETURN;
}

/*
/   zysri - read from interactive channel
/
/   zysri reads a line from the user's terminal.
/
/   Parameters:
/    xr    pointer to SCBLK to receive line
/   Returns:
/    Nothing
/   Exits:
/    1    EOF
*/

int
zysri()
{
    word length;
    struct scblk *scb = XR(struct scblk *);
    char *saveptr, savechr;

    /*
       /    Read a line specified by length of scblk.  If EOF take exit 1.
     */
    length = scb->len;           /* Length of buffer provided */
    saveptr = scb->str + length; /* Save char following buffer for \n */
    savechr = *saveptr;

    ((struct bfblk *)(ttyiobin.bfb))->size =
        ++length; /* Size includes extra byte for \n */

    length = osread(1, length, &ttyiobin, scb);

    *saveptr = savechr; /* Restore saved char */

    if(length < 0)
        return EXIT_1;

    /*
       /    Line read OK, so set string length and return normally.
     */
    scb->len = length;
    return NORMAL_RETURN;
}

/* change handle used for TERMINAL output */
void
ttyoutfdn(File_handle h)
{
    ttyiobout.fdn = h;
    if(testty(h))
        ttyiobout.flg1 &= ~IO_COT;
    else
        ttyiobout.flg1 |= IO_COT;
}
