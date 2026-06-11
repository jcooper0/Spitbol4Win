*//--------------------------------------------------------------------------------------------------
*// $Workfile: XDump.inc $ $Revision: 3 $ $Date: 3/10/05 11:47a $ 
*//--------------------------------------------------------------------------------------------------
*//
*// XDump(object, nm) - Generic SNOBOL4 object dump routine. This recursive routine is useful
*// from within the debugger or as debug code sprinkled throughout the SNOBOL4 application.
*// Needs to be enhanced to handle 2+ dimensional arrays.
*//
*//----------- -------------------------------------------------------------------- -----------------
               define('XDump(object,nm)'
+                 'i,iMax,iMin,objArr,objField,objKey,objKeyNm,objType,objVal'
+              )                                                                    :(XDumpEnd)
XDump          objType        =    datatype(object)
               ident(objType, 'code')                                               :S(XDump00)
               ident(objType, 'EXPRESSION')                                         :S(XDump00)
               ident(objType, 'integer')                                            :S(XDump05)
               ident(objType, 'NAME')                                               :S(XDump00)
               ident(objType, 'PATTERN')                                            :S(XDump00)
               ident(objType, 'REAL')                                               :S(XDump05)
               ident(objType, 'STRING')                                             :S(XDump10)
               ident(objType, 'array')                                              :S(XDump20)
               ident(objType, 'table')                                              :S(XDump30)F(XDump40)
XDump00        output         =    nm ' = ' objType                                 :(return)
XDump05        output         =    nm ' = ' object                                  :(return)
XDump10        output         =    nm ' = ' Qize(object)                            :(return)
XDump20        objProto       =    prototype(object)
               objProto       pos(0)
+                             (('+' | '-' | epsilon) span(digits)) . iMin ':'
+                             (('+' | '-' | epsilon) span(digits)) . iMax
+                             rpos(0)
               output         =    nm ' = ' "array['" objProto "']"
               i              =    iMin - 1
XDump25        i              =    lt(i, iMax) i + 1                                :F(return)
               XDump(object[i], nm '[' i ']')                                       :(XDump25)
XDump30        output         =    nm ' = ' 'table'
*//            objArr         =    convert(object, 'array')                         :F(return)
               objArr         =    sort(object)                                     :F(return)
               i              =    0
XDump35        i              =    i + 1
               objKey         =    objArr[i, 1]                                     :F(return)
               objVal         =    objArr[i, 2]
               objKeyNm       =    ident(datatype(objKey), 'integer') objKey        :S(XDump37)
               objKeyNm       =    ident(datatype(objKey), 'STRING') Qize(objKey)   :S(XDump37)
               objKeyNm       =    datatype(objKey)
XDump37        XDump(objVal, nm '[' objKeyNm ']')                                   :(XDump35)
XDump40        output         =    nm ' = ' objType '()'
               i              =    0
XDump45        i              =    i + 1
               objField       =    field(objType, i)                                :F(return)
               XDump(apply(objField, object), objField '(' nm ')')                  :(XDump45)
XDumpEnd
