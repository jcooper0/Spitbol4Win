*//-----------------------------------------------------------------------------------------------------------------------
*// $Workfile: trace.inc $ $Revision: 2 $ $Date: 3/13/05 5:38p $ 
*//-------- ----------------- ----- -------------------------------------------------------- -----------------------------
            define('T8Trace(lvl,str,ofs)')                                                   :(T8TraceEnd)
T8Trace     T8Trace           =     .dummy
*//         t8Trigger         =     eq(xTrace, 0) t8Trigger + 1
*//         xTrace            =     gt(t8Trigger, 1048576) 5
            t8Max             =     gt(strOfs + ofs, t8Max) strOfs + ofs
            gt(xTrace, 0)                                                                    :F(nreturn)
            LE(lvl, xTrace)                                                                  :F(nreturn)
*//         gt(xTrace, 1)                                                                    :S(T8Trace1)
*//         str               pos(0) '?'                                                     :S(nreturn)
T8Trace1    str               pos(0) '?' = '? '                                              :S(T8Trace2)
            str               pos(0) = '  '
T8Trace2    str               =     gt(size(str), 1024) '  ...'
            output            =     T8Pos(strOfs + ofs, t8Map) str                           :(nreturn)
T8TraceEnd
*//-------- ----------------- ----- -------------------------------------------------------- -----------------------------
            define('T8Pos(t8Ofs,t8Map)i')                                                    :(T8PosEnd)
T8Pos       T8Pos             =     ident(t8Map) lpad(t8Ofs, 8)                              :S(return)
            i                 =     +t8Ofs
T8Pos1      i                 =     ident(t8Map[i]) i - 1                                    :S(T8Pos1)
            t8Line            =     t8Map[i]
            t8Pos             =     t8Ofs - i + 1
            i                 =     +t8Max
T8Pos2      i                 =     ident(t8Map[i]) i - 1                                    :S(T8Pos2)
            t8MaxLine         =     t8Map[i]
            t8MaxPos          =     t8Max - i + 1
            T8Pos             =     '(' lpad(t8MaxLine, 5)
+                                   ', ' lpad(t8MaxPos, 3)
+                                   ', ' lpad(t8Line, 5)
+                                   ', ' lpad(t8Pos, 3)
+                                   ')'                                                      :(return)
T8PosEnd
