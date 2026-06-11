*//--------------------------------------------------------------------------
*// $Workfile: assign.inc $ $Revision: 6 $ $Date: 3/25/05 11:29p $ 
*//--------------------------------------------------------------------------
*//               
*// Assignment during pattern matching: The unevaluated expression may fail
*// which causes the assignment not to occur, but the pattern that invoked
*// the assignment will always continue.
*//               
*// pattern . *assign(name, expression)
*//               
*//-------- -------------------------------------------- --------------------
            define('assign(name,expression)')            :(assign_end)
assign      assign   =     .dummy
            ident(datatype(expression), 'EXPRESSION')    :F(assign1)
            $name    =     eval(expression)              :(nreturn)
assign1     $name    =     expression                    :(nreturn)
assign_end
