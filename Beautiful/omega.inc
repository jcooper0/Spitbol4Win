*//-----------------------------------------------------------------------------------------------------------------------
*// $Workfile: omega.inc $ $Revision: 3 $ $Date: 3/13/05 5:33p $ 
*//-------- ----------------- ----- -------------------------------------------------------- -----------------------------
            define('TV(lvl,pat,name)omega')                                                  :(TVEnd)
TV          omega             =     eq(doParseTree, FALSE) "pat"
            omega             =     eq(doParseTree, TRUE)  "(pat ~ 'identifier')"
            omega             =     omega ' $ tx *LEQ(lwr(tx), "' lwr(name) '")'
            TV                =     TZ(lvl, name, eval(omega))                               :S(return)F(error)
TVEnd
*//-------- ----------------- ----- -------------------------------------------------------- -----------------------------
            define('TW(lvl,pat,name)omega')                                                  :(TWEnd)
TW          omega             =     eq(doParseTree, FALSE) "pat"
            omega             =     eq(doParseTree, TRUE)  "(pat ~ 'identifier')"
            omega             =     omega " $ tx *LEQ(upr(tx), '" upr(name) "')"
            TW                =     TZ(lvl, name, eval(omega))                               :S(return)F(error)
TWEnd
*//-------- ----------------- ----- -------------------------------------------------------- -----------------------------
            define('TX(lvl,pat,name)omega')                                                  :(TXEnd)
TX          omega             =     eq(doParseTree, FALSE) "pat"                  
            omega             =     eq(doParseTree, TRUE)  "(pat ~ 'identifier')"
            omega             =     omega " $ tx *LEQ(tx, '" name "')"
            TX                =     TZ(lvl, name, eval(omega))                               :S(return)F(error)
TXEnd
*//-------- ----------------- ----- -------------------------------------------------------- -----------------------------
            define('TY(lvl,name,pat)omega')                                                  :(TYEnd)
TY          TY                =     LE(xTrace, 0) pat
+                                   @txOfs $ *assign(.t8Max, *(gt(txOfs, t8Max) txOfs))      :S(return)
            omega             =     "pat $ tz"
+                                   " @txOfs"
+                                   " $ *T8Trace(" lvl ", " Qize(name ': ') " tz, txOfs)"
            TY                =     eval(omega)                                              :S(return)F(error)
TYEnd
*//-------- ----------------- ----- -------------------------------------------------------- -----------------------------
*//         Instrument parser patterns for tracing:
*//         TZ(integer, string, pattern)
*//
            define('TZ(lvl,name,pat)omega')                                                  :(TZEnd)
TZ          TZ                =     LE(xTrace, 0) pat
+                                   @txOfs $ *assign(.t8Max, *(gt(txOfs, t8Max) txOfs))      :S(return)
            omega             =     "@txOfs $ *T8Trace(" lvl ", '?' " Qize(name) ", txOfs)"
+                                   " pat $ tz"
+                                   " @txOfs" 
+                                   " $ *T8Trace(" lvl ", " Qize(name ': ') " tz, txOfs)"
            TZ                =     eval(omega)                                              :S(return)F(error)
TZEnd
