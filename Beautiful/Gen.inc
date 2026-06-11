*//--------------------------------------------------------------------------------------------------
*// $Workfile: Gen.inc $ $Revision: 4 $ $Date: 3/10/05 11:46a $ 
*//--------------------------------------------------------------------------------------------------
*//
*// Routines to manage generated output. Since SNOBOL4 naturally writes one line at a time
*// a way is needed to buffer up the output until a newline character is reached, and a way
*// to automatically handle the indention of each line.
*//
*// IncLevel(delta) -- Increment indention level by delta
*// DecLevel(delta) -- Decrement indention level by delta
*// SetLevel(level) -- Set indention level
*// GetLevel()      -- Get current indention level
*//
*// Gen(string) -- Generate string
*// GenTab(integer) -- Generate tab (spaces) to specified or current margin level
*// GenSetCont(character) -- Set continue character and begin with 1st line.
*//
*// Globals:
*//    $'$B' -- buffer
*//    $'$C' -- continuation character
*//    $'$X' -- marks the spot for $'$C'
*//    $'#L' -- indention level
*//
*//----------- -------------------------------------------------------------------- -----------------
               define('IncLevel(delta)')
               define('DecLevel(delta)')
               define('SetLevel(level)')
               define('GetLevel()')                                                 :(LevelEnd)
IncLevel       IncLevel       =  .dummy
               delta          =  ident(delta) 3
               $'#L'          =  $'#L' + delta                                      :(nreturn)
DecLevel       DecLevel       =  .dummy
               delta          =  ident(delta) 3
               $'#L'          =  $'#L' - delta                                      :(nreturn)
SetLevel       SetLevel       =  .dummy
               $'#L'          =  level                                              :(nreturn)
GetLevel       GetLevel       =  $'#L'                                              :(return)
LevelEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('Gen(str,outNm)ind,outline')
               define('GenTab(pos)')
               define('GenSetCont(cont)')
               indent         =  dupl(' ', 120)                                     :(GenEnd)
Gen            Gen            =  .dummy
               outNm          =  ident(outNm) .output
               indent         gt($'#L', 0) len($'#L' - size($'$X')) . ind
               $'$B'          =  differ($'$B') $'$B' str
               $'$B'          =  ident($'$B') $'$X' ind str
               $'$B'          break(nl) . outline nl rem . $'$B'                    :F(nreturn)
               $'$X'          =  $'$C'
               $outNm         =  outline
Gen10          $'$B'          break(nl) . outline nl rem . $'$B'                    :F(nreturn)
               $outNm         =  $'$C' ind outline                                  :(Gen10)
GenTab         GenTab         =  .dummy
               pos            =  ident(pos) $'#L'
               $'$B'          =  $'$B' ' ' dupl(' ', pos - size($'$B') - 1)         :S(nreturn)
               $'$B'          =  $'$B' ' '                                          :(nreturn)
GenSetCont     GenSetCont     =  .dummy
               $'$X'          =
               $'$C'          =  cont                                               :(nreturn)
GenEnd
