*//--------------------------------------------------------------------------------------------------
*// $Workfile: stack.inc $ $Revision: 4 $ $Date: 3/13/05 5:37p $ 
*//--------------------------------------------------------------------------------------------------
*//
*// A general purpose stack. To be used directly or with conditional assignment within pattern
*// matching. This stack holds the values from the pattern match which were produced as a
*// result of either a Shift() or a Reduce() operation.
*// Global: $'@S' -- link()
*//
*//----------- -------------------------------------------------------------------- -----------------
               define('InitStack()')
               define('Push(x)')
               define('Pop(var)')
               define('Top()')
               data('link(next,value)')                                             :(StackEnd)
InitStack      $'@S'          =                                                     :(return)
Push           output         =    gt(xTrace, 4) 'Push(' t(x) ')'
               $'@S'          =    link($'@S', x)
               Push           =    ident(x)  .value($'@S')                          :S(nreturn)
               Push           =    differ(x) .dummy                                 :(nreturn)
Pop            differ($'@S')                                                        :F(freturn)
               ident(var)                                                           :F(Pop1)
               Pop            =    value($'@S')
               output         =    gt(xTrace, 4) 'Pop() = ' t(Pop)
               $'@S'          =    next($'@S')                                      :(return)
Pop1           Pop            =    .dummy
               $var           =    value($'@S')
               output         =    gt(xTrace, 4) 'Pop() = ' t($var)
               $'@S'          =    next($'@S')                                      :(nreturn)
Top            differ($'@S')                                                        :F(freturn)
               Top            =    .value($'@S')
               output         =    gt(xTrace, 4) 'Top() = ' t(Top)                  :(nreturn)
StackEnd
