*//--------------------------------------------------------------------------------------------------
*// $Workfile: ReadWrite.inc $ $Revision: 3 $ $Date: 3/13/05 5:35p $ 
*//--------------------------------------------------------------------------------------------------
*//
*// Read(fileName, rdMapName) - Read the specified file into one string with lines seperated
*// by newline characters. Also create a map table indexed by offsets of each line with its
*// corresponding line number.
*//
*//----------- -------------------------------------------------------------------- -----------------
               define('Read(fileName,rdMapName)'
+                  'rdInput,rdIn,rdLine,rdLineNo,rdMap,rdOfs'
+              )                                                                     :(ReadEnd)
Read           input(.rdInput, 8, fileName '[-m10 -l131072]')                        :F(freturn)
               rdMap          =    table()
               rdOfs          =    0
               rdLineNo       =    1
               Read           =
Read3          rdMap[rdOfs]   =    rdLineNo
               rdLine         =
Read5          rdIn           =    rdInput                                          :F(Read9)
               rdLine         =    rdLine rdIn
               lt(size(rdIn), 131072)                                               :F(Read5)
               rdLine         rpos(1) cr =
               rdOfs          =    rdOfs + size(rdLine) + 1
               rdLineNo       =    rdLineNo + 1
               Read           =    Read rdLine nl                                   :(Read3)
Read9          ENDFILE(8)
               differ(rdMapName)                                                    :F(return)
               $rdMapName     =    rdMap                                            :(return)
ReadEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('Write(fileName,fileStr)wrLine,wrOutput')                     :(WriteEnd)
Write          output(.wrOutput, 8, fileName)                                       :F(freturn)
Write1         fileStr        pos(0) rpos(0)                                        :S(Write9)
               fileStr        pos(0) break(nl) . wrLine nl =                        :S(Write3)
               fileStr        pos(0) RTAB(0) . wrLine =                             :S(Write3)F(err)
Write3         wrOutput       =    wrLine                                           :(Write1)
Write9         ENDFILE(8)                                                           :(return)
WriteEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('LineMap(str,lmMapName)lmLineNo,lmMap,lmOfs,xOfs')            :(LineMapEnd)
LineMap        lmMap          =    table()
               lmOfs          =    0
               lmLineNo       =    1
LineMap3       lmMap[lmOfs]   =    lmLineNo
               str            pos(0) break(nl) nl @xOfs =                           :F(LineMap9)
               lmOfs          =    lmOfs + xOfs                                     
               lmLineNo       =    lmLineNo + 1                                     :(LineMap3)
LineMap9       $lmMapName     =    lmMap                                            :(return)
LineMapEnd
