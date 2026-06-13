*  assert.inc -- shared self-check assertions for the ArraysTables conformance
*  cases ported from the Snobol4.Net C# test suite (Test.ArraysTables).
*
*  Pair with the harness SelfCheck mode (no <name>.expected file): a case PASSES
*  iff it exits 0 AND emits no line containing  *FAIL .  Each satisfied
*  assertion prints  ' pass: ...'  so a run is self-documenting.
*
*  -------------------------------------------------------------------------
*  Capturing &ERRTYPE
*  -------------------------------------------------------------------------
*  &ERRTYPE is set only when an error is DELIVERED to a SETEXIT handler.  An
*  error inside eval() merely makes eval() FAIL (printing the message) WITHOUT
*  driving the trap, so every error case here runs the failing primitive
*  DIRECTLY (inline), never via eval.  The shared handler 'caught' records the
*  FIRST error seen since caughtf was cleared -- mirroring the C# assertion on
*  ErrorCodeHistory[0] -- then RE-ARMS setexit (it is one-shot) and continues.
*
*  -------------------------------------------------------------------------
*  Vocabulary
*  -------------------------------------------------------------------------
*    vchk(tag,obs,exp)   value equality (ident: datatype AND value must match)
*    okchk(tag)          assert NO error was trapped since caughtf was cleared
*    errchk(tag,code)    assert the first DIRECT error since clear was 'code'
*
*  Per-block protocol  (clear caughtf at the top of every block)
*    no-error/value : caughtf = ; <snippet> ; okchk('tag') ; vchk('tag',v,exp)
*    expected error : caughtf = ; <setup> ; <erroring op> ; errchk('tag',code)
*    expected fail  : caughtf = ; <op> :s(Y)f(N) ; ... ; okchk ; vchk(result,..)

        define('vchk(tag,obs,exp)')
        define('okchk(tag)')
        define('errchk(tag,code)')
        define('rchk(tag,obs,exp)d')
        define('aabs(x)')                               :(assert_over)

*  rchk(tag,obs,exp) -- REAL value within a small relative+absolute tolerance
*  (float text formatting differs between implementations, so compare numbers).
rchk    d = aabs(obs - exp)
        le(d, 0.000001 * aabs(exp) + 0.000000001)       :s(rchk_p)f(rchk_f)
rchk_p  output = ' pass: ' tag ' = [' obs ']'           :(return)
rchk_f  output = '*FAIL: ' tag '  obs[' obs ']  exp[' exp ']'          :(return)
aabs    aabs = ge(x,0) x                                :s(return)
        aabs = -x                                       :(return)

*  Value comparison is by STRING VALUE (obs and exp coerced), matching the C#
*  tests which read StringVar.Data.  Native SPITBOL may return e.g. an integer
*  where the C# port returns a string; the value still compares equal here.
vchk    obs pos(0) exp rpos(0)                          :s(vchk_p)f(vchk_f)
vchk_p  output = ' pass: ' tag ' = [' obs ']'           :(return)
vchk_f  output = '*FAIL: ' tag '  obs[' obs ']  exp[' exp ']'           :(return)

okchk   ident(caughtf,1)                                :s(okchk_f)
        output = ' pass: ' tag ' (no error)'            :(return)
okchk_f output = '*FAIL: ' tag ' unexpected &errtype=' savedet         :(return)

errchk  ident(caughtf,1)                                :f(errchk_nf)
        eq(savedet,code)                                :s(errchk_p)f(errchk_w)
errchk_p output = ' pass: ' tag ' &errtype=' savedet                   :(return)
errchk_w output = '*FAIL: ' tag ' &errtype=[' savedet '] expected [' code ']'  :(return)
errchk_nf output = '*FAIL: ' tag ' no error trapped (op succeeded or only failed)'  :(return)

*  ---- one-shot-safe, first-error-wins trap handler -------------------------
caught  ident(caughtf,1)                                :s(caught_re)
        savedet = &errtype
        caughtf = 1
caught_re setexit(.caught)                              :(continue)

assert_over
        setexit(.caught)
        &errlimit = 1000000
