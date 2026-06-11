*//--------------------------------------------------------------------------------------------------
*// $Workfile: Qize.inc $ $Revision: 5 $ $Date: 3/13/05 5:35p $ 
*//--------------------------------------------------------------------------------------------------
*//
*// Qize(s) - Quote-ize a string. Make a SNOBOL4 expression from the string.
*//
*//----------- -------------------------------------------------------------------- -----------------
               define('Qize(str)part')
               QizeWierd      =    bSlash bs ff nl cr tab                           :(QizeEnd)
Qize           Qize           =    ident(str) "''"                                  :S(return)
Qize0          ident(str)                                                           :S(return)
               Qize           =    differ(Qize) Qize ' '
               str            pos(0)
+                             (  bSlash . *assign(.part, *'bSlash')
+                             |  bs     . *assign(.part, *'bs')
+                             |  ff     . *assign(.part, *'ff')
+                             |  nl     . *assign(.part, *'nl')
+                             |  cr     . *assign(.part, *'cr')
+                             |  tab    . *assign(.part, *'tab')
+                             ) =                                                   :S(Qize3)
               str            pos(0)
+                             (break('"' "'" QizeWierd) '"' arbno(notany("'" QizeWierd))) . part
+                             RTAB(0) . str                                         :S(Qize1)
               str            pos(0)
+                             (break("'" '"' QizeWierd) "'" arbno(notany('"' QizeWierd))) . part
+                             RTAB(0) . str                                         :S(Qize2)
               str            pos(0) break(QizeWierd) . part =                      :S(Qize1)
               str            pos(0) rem . part =                                   :S(Qize1)F(error)
Qize1          Qize           =    Qize "'" part "'"                                :(Qize0)
Qize2          Qize           =    Qize '"' part '"'                                :(Qize0)
Qize3          Qize           =    Qize part                                        :(Qize0)
QizeEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('SQize(str)part')                                             :(SQizeEnd)
SQize          ident(str)                                                           :S(return)
               SQize          =    differ(SQize) SQize ' '
               str            pos(0) break("'") . part "'" =                        :S(SQize1)
               str            pos(0) rem . part =                                   :S(SQize2)F(error)
SQize1         SQize          =    SQize "'" part "'" ' "' "'" '"'                  :(SQize)
SQize2         SQize          =    SQize "'" part "'"                               :(SQize)
SQizeEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('DQize(str)part')                                             :(DQizeEnd)
DQize          ident(str)                                                           :S(return)
               DQize          =    differ(DQize) DQize ' '
               str            pos(0) break('"') . part '"' =                        :S(DQize1)
               str            pos(0) rem . part =                                   :S(DQize2)F(error)
DQize1         DQize          =    DQize '"' part '"' " '" '"' "'"                  :(DQize)
DQize2         DQize          =    DQize '"' part '"'                               :(DQize)
DQizeEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('SqlSQize(str)part')                                          :(SqlSQizeEnd)
SqlSQize       ident(str)                                                           :S(return)
               str            pos(0) break("'") . part "'" =                        :S(SqlSQize1)
               str            pos(0) rem . part =                                   :S(SqlSQize2)F(error)
SqlSQize1      SqlSQize       =    SqlSQize part "''"                               :(SqlSQize)
SqlSQize2      SqlSQize       =    SqlSQize part                                    :(SqlSQize)
SqlSQizeEnd
*//----------- ----------------- ----- -------------------------------------------- -----------------
               define('Intize(qqstr)iq,qqdlm')                                      :(IntizeEnd)
Intize         qqstr          pos(0) ("'" | '"') $ qqdlm
+                             arbno(
+                                bSlash
+                                (  bSlash . *assign(.Intize, *(Intize bSlash))
+                                |  '"' . *assign(.Intize, *(Intize '"'))
+                                |  "'" . *assign(.Intize, *(Intize "'"))
+                                |  'b' . *assign(.Intize, *(Intize bs))
+                                |  'f' . *assign(.Intize, *(Intize ff))
+                                |  'n' . *assign(.Intize, *(Intize lf))
+                                |  'r' . *assign(.Intize, *(Intize cr))
+                                |  't' . *assign(.Intize, *(Intize tab))
+                                |  'u'
+                                   (  '00' len(2) . iq . *assign(.Intize, *(Intize Ucvt(iq)))
+                                   |  len(4) . iq . *assign(.Intize, *(Intize bSlash 'u' iq))
+                                   )
+                                )
+                             |  break(*(qqdlm bSlash)) . iq . *assign(.Intize, *(Intize iq))
+                             )
+                             *qqdlm rpos(0)                                        :S(return)F(freturn)
IntizeEnd
*//-------- ----------------- ----- ----------------------------------------------- -----------------
            define('Extize(str)')                                                   :(ExtizeEnd)
Extize
ExtizeEnd
