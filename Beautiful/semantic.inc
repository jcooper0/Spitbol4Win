*//------------------------------------------------------------------------------------------------------
*// $Workfile: semantic.inc $ $Revision: 2 $ $Date: 3/13/05 5:36p $ 
*//------------------------------------------------------------------------------------------------------
*//
*// Semantic building routines. These functions are called while building the parser
*// patterns, not during pattern matching.
*//
*//----------- -------------- -- ----------------------------------------------- ------------------------
               define('shift(p,t)')
               define('reduce(t,n)')
               opsyn('~', 'shift', 2)
               opsyn('&', 'reduce', 2)
               define('pop()')
               define('nPush()')
               define('nInc()')
               define('nDec()')
               define('nTop()')
               define('nPop()')                                                  :(semanticEnd)
*//----------- -------------- -- ----------------------------------------------- ------------------------
shift          shift          =  eval("p . thx . *Shift('" t "', thx)")          :(return)
reduce         reduce         =  eval("epsilon . *Reduce(" t ", " n ")")         :(return)
pop            pop            =  epsilon . *Pop(.dummy)                          :(return)
*//----------- -------------- -- ----------------------------------------------- ------------------------
nPush          nPush          =  epsilon . *PushCounter()                        :(return)
nInc           nInc           =  epsilon . *IncCounter()                         :(return)
nDec           nDec           =  epsilon . *DecCounter()                         :(return)
nTop           nTop           =  TopCounter()                                    :(return)
nPop           nPop           =  epsilon . *PopCounter()                         :(return)
*//----------- -------------- -- ----------------------------------------------- ------------------------
semanticEnd
