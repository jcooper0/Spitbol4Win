*//-----------------------------------------------------------------------------------------------------
*// $Workfile: is.inc $ $Revision: 4 $ $Date: 3/13/05 5:32p $ 
*//-----------------------------------------------------------------------------------------------------
*//
*// IsSpitbol() succeeds if the runtime environment is SPITBOL. It fails otherwise.
*// IsSnobol4() succeeds if the runtime environment is SNOBOL4. It fails otherwise.
*//
*//----------- -------------------------------------------------------------------- --------------------
               define('IsSpitbol()')                                                :(IsSpitbolEnd)
IsSpitbol      differ(.NAME, 'NAME')                                                :S(return)F(freturn)
IsSpitbolEnd
*//----------- -------------------------------------------------------------------- --------------------
               define('IsSnobol4()')                                                :(IsSnobol4End)
IsSnobol4      ident(.NAME, 'NAME')                                                 :S(return)F(freturn)
IsSnobol4End
*//----------- -------------------------------------------------------------------- -----------------
               define('IsType(it,types)type')                                       :(IsTypeEnd)
IsType         types          pos(0) span(&ucase &lcase) . type (',' | rpos(0)) =   :F(freturn)
               (ident(upr(type), 'NULL') ident(it))                                 :S(return)
               ident(datatype(it), upr(type))                                       :S(return)F(IsType)
IsTypeEnd
