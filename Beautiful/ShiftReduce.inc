*//--------------------------------------------------------------------------------------------------
*// $Workfile: ShiftReduce.inc $ $Revision: 4 $ $Date: 3/13/05 5:36p $ 
*//--------------------------------------------------------------------------------------------------
*//
*// Shift(t,v) -- push a tree of type t with value v onto the stack
*//
*// Reduce(t, n) -- pop n trees from the stack and push a tree of type t that contains these
*// n trees as children in the order they were stacked. If either the t or n parameters are
*// unevaluated expressions they will be evaluated on-the-fly here within this routine. If the
*// evaluation of an unevaluated expression fails then there is no reduction of the stack.
*//
*//----------- -------------------------------------------------------------------- -----------------
               define('Shift(t,v)s')                                                :(ShiftEnd)
Shift          v              pos(0) whitespace =
               s              =    tree(t, v)
               Push(s)
               output         =    gt(xTrace, 3) 'Shift(' t ', ' v ')'
*//            output         =    gt(xTrace, 4) ' = ' TLump(s, 1024)
               Shift          =    ident(v) .v(s)                                   :S(nreturn)
               Shift          =    differ(v) .dummy                                 :(nreturn)
ShiftEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('Reduce(t,n)c,i,r')                                           :(ReduceEnd)
Reduce         Reduce         =    .dummy
               ident(datatype(t), 'EXPRESSION')                                     :F(Reduce0)
               t              =    eval(t)                                          :F(nreturn)
Reduce0        ident(datatype(n), 'EXPRESSION')                                     :F(Reduce1)
               n              =    eval(n)                                          :F(nreturn)
Reduce1        output         =    gt(xTrace, 3) 'Reduce(' t ', ' n ')'
               c              =    ge(n, 1) array('1:' n)
               i              =    n + 1
Reduce2        i              =    gt(i, 1) i - 1                                   :F(Reduce3)
               c[i]           =    Pop()                                            :(Reduce2)
Reduce3        r              =    tree(t,, n, c)
*//            (gt(xTrace, 4) TDump(r))
               Push(r)                                                              :(nreturn)
ReduceEnd
