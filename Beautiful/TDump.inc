*//--------------------------------------------------------------------------------------------------
*// $Workfile: TDump.inc $ $Revision: 5 $ $Date: 3/13/05 5:37p $ 
*//--------------------------------------------------------------------------------------------------
*//
*// TDump(x) - Recursive routine to dump tree x to output in a lisp-like parenthetical
*// representation. Uses TLump(x, len) to build string for output on one line, but if it will
*// not fit then uses multiple line format.
*//
*// TLump(x) - Recursive function to return as a string tree x in a
*// lisp-like parenthetical representation.
*//
*//----------- -------------------------------------------------------------------- -----------------
               define('TValue(x)i')                                                 :(TValueEnd)
TValue         differ(t(x), '.')                                                    :F(TValue3)
               TValue         =  ident(v(x)) "."                                    :S(return)
               TValue         =  ident(t(x), 'float') v(x)                          :S(return)
               TValue         =  ident(t(x), 'integer') v(x)                        :S(return)
               TValue         =  ident(t(x), 'bool') v(x)                           :S(return)
               TValue         =  ident(t(x), 'datetime') "'" SqlSQize(v(x)) "'"     :S(return)
               TValue         =  ident(t(x), 'character') "'" SqlSQize(v(x)) "'"    :S(return)
               TValue         =  ident(t(x), 'string') "'" SqlSQize(v(x)) "'"       :S(return)
               TValue         =  ident(t(x), 'identifier') v(x)                     :S(return)
               TValue         =  t(x)                                               :S(return)
TValue3        i              =  0
TValue5        i              =  lt(i, n(x)) i + 1                                  :F(return)
               TValue         =  TValue (differ(TValue) '.', '') v(c(x)[i])         :(TValue5)
TValueEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('TDump(x,outNm)i,t')                                          :(TDumpEnd)
TDump          outNm          =  ident(outNm) .output
               x              =  ident(datatype(x), 'NAME') $x
               Gen(TLump(x, 140 - GetLevel()) nl, outNm)                            :S(return)
               NULL           *ident(n(x)) | *ident(t(x), '.')                      :F(TDump0)
               Gen(TValue(x) nl, outNm)                                             :(return)
TDump0         t(x)           pos(0) any(&ucase &lcase)
+                             (span( digits &ucase '_' &lcase) | epsilon) rpos(0)   :F(TDump1)
               t              =  t(x)                                               :(TDump2)
TDump1         t              =  '"' t(x) '"'                                       :(TDump2)
TDump2         Gen('(' t nl, outNm)
               IncLevel()
               i              =  0
TDump3         i              =  lt(i, n(x)) i + 1                                  :F(TDump4)
               TDump(c(x)[i], outNm)                                                :(TDump3)
TDump4         DecLevel()
               Gen(')' nl, outNm)                                                   :(return)
TDumpEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('TLump(x,len)i,t')                                            :(TLumpEnd)
TLump          gt(len, 0)                                                           :F(freturn)
               TLump          =  ident(x) '()'                                      :S(return)
               NULL           *ident(n(x)) | *ident(t(x), '.')                      :F(TLump0)
               TLump          =  TValue(x)
               LE(size(TLump), len)                                                 :S(return)F(freturn)
TLump0         TLump          =  '('
               t(x)           pos(0) any(&ucase &lcase)
+                             (span( digits &ucase '_' &lcase) | epsilon) rpos(0)   :F(TLump1)
               t              =  t(x)                                               :(TLump2)
TLump1         t              =  '"' t(x) '"'                                       :(TLump2)
TLump2         TLump          =  TLump t
               i              =  0
TLump3         i              =  lt(i, n(x)) i + 1                                  :F(TLump4)
               TLump          =  TLump ' ' TLump(c(x)[i], len - size(TLump) - 2)    :S(TLump3)F(freturn)
TLump4         TLump          =  TLump ')'                                          :(return)
TLumpEnd
*//----------- -------------------------------------------------------------------- -----------------
