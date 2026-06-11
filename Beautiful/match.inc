*//-----------------------------------------------------------------------------------------------------------------------
*// $Workfile: match.inc $ $Revision: 3 $ $Date: 3/13/05 5:32p $ 
*//-----------------------------------------------------------------------------------------------------------------------
*//
*// Pattern matching during pattern matching:
*// pattern $ variable $ *match(variable, pattern)
*// pattern $ variable $ *notmatch(variable, pattern)
*//
*//----------- --------------------------------------------------------------------- -----------------
               define('match(subject,pattern)')                                      :(match_end)
match          match          =  .dummy
               subject        pattern                                                :S(nreturn)F(freturn)
match_end
*//----------- --------------------------------------------------------------------- -----------------
               define('notmatch(subject,pattern)')                                   :(notmatch_end)
notmatch       notmatch       =  .dummy
               subject        pattern                                                :S(freturn)F(nreturn)
notmatch_end
