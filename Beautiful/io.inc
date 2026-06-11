*//-----------------------------------------------------------------------------------------------------
*// $Workfile: io.inc $ $Revision: 3 $ $Date: 3/10/05 11:46a $ 
*//-----------------------------------------------------------------------------------------------------
*//
*// Provide SPITBOL input/output semantics within SNOBOL4 programs.
*// Make input() and output() functions still accessible.
*// Map input() and output() functions to the following user defined functions.
*// The recognition and interpretation of the options is incomplete.
*//
*//----------- -------------------------------------------------------------------- --------------------
               IsSnobol4()                                                          :F(io_end)
               define('input_(name,channel,fileName)bch,cmd,maxreclen,opts,options')
               define('output_(name,channel,fileName)bch,cmd,maxreclen,opts,options')
               opsyn('input__', 'input')
               opsyn('output__', 'output')
               opsyn('input', 'input_')
               opsyn('output', 'output_')
               ioFileOptDash  =  (  '-l'
+                                   span(digits) . maxreclen . *assign(.options, *(options maxreclen))
+                                |  '-m' span(digits)
+                                |  '-a' . *assign(.options, *(options 'a'))
+                                |  '-w' . *assign(.options, *(options 'w'))
+                                )
               ioFileOptList  =  *ioFileOptDash (span(' ,') *ioFileOptList | epsilon)
               ioFileOptPat   =  '[' *ioFileOptList ']'
               ioFileOptPat0  =  *ioFileOptPat | epsilon
               ioCmdDlmtPat1  =  len(1) $ bch break(*bch) . cmd *bch
               ioCmdDlmtPat2  =  len(1) rem . cmd                                   :(io_end)
input_         io             =  .input__                                           :(io)
output_        io             =  .output__                                          :(io)
io             fileName       pos(0) '!' ioCmdDlmtPat1 ioFileOptPat0 = '|' cmd      :S(io1)
               fileName       pos(0) '!' ioCmdDlmtPat2 = '|' cmd                    :S(io1)
               fileName       ioFileOptPat =                                        :S(io1)
io1            options        =  ident(maxreclen) options 1024
               apply(io, name, channel, options, fileName)                          :S(return)F(freturn)
io_end
