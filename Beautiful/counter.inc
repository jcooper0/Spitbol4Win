*//--------------------------------------------------------------------------------------------------
*// $Workfile: counter.inc $ $Revision: 3 $ $Date: 3/10/05 11:43a $ 
*//--------------------------------------------------------------------------------------------------
*//
*// A stack of counters. To be used with conditional assignment within pattern matching.
*// These routines are useful for counting items on the stack at multiple nesting levels which
*// are suitable for reduction on the stack.
*// Global: $'#N' -- link_counter()
*//
*//---------- --------------------------------------------------------------------- -----------------
              define('InitCounter()')
              define('PushCounter()')
              define('IncCounter()')
              define('DecCounter()')
              define('TopCounter()')
              define('PopCounter()')
              data('link_counter(next,value)')                                      :(CounterEnd)
InitCounter   $'#N'          =                                                      :(return)
PushCounter   output         =    gt(xTrace, 4) 'PushCounter()'
              $'#N'          =    link_counter($'#N', 0)
              PushCounter    =    .dummy                                            :(nreturn)
IncCounter    value($'#N')   =    value($'#N') + 1
              output         =    gt(xTrace, 4) value($'#N') ' = IncCounter()'     
              IncCounter     =    .dummy                                            :(nreturn)
DecCounter    value($'#N')   =    value($'#N') - 1
              output         =    gt(xTrace, 4) value($'#N') ' = DecCounter()'     
              DecCounter     =    .dummy                                            :(nreturn)
PopCounter    output         =    gt(xTrace, 4) 'PopCounter()'
              $'#N'          =    differ($'#N') next($'#N')                         :F(freturn)
              PopCounter     =    .dummy                                            :(nreturn)
TopCounter    TopCounter     =    differ($'#N') value($'#N')                        :F(freturn)
              output         =    gt(xTrace, 4) TopCounter ' = TopCounter()'        :(return)
CounterEnd
*//--------------------------------------------------------------------------------------------------
*//
*// A stack of XML or HTML begin and end tags. To be used with unconditional assignment within
*// pattern matching.
*// Global: $'@B' -- link_tag()
*//         $'@E' -- link_tag()
*//
*//-------- --------------- --- ------------------------------------------- -------------------------
            data('link_tag(next,value)')                                    
*//-------- --------------- --- ------------------------------------------- -------------------------
            define('InitBegTag()')
            define('PushBegTag(t)')
            define('PopBegTag()')
            define('TopBegTag()')
            define('DumpBegTag()b,list,v')                                  :(BegTagEnd)
InitBegTag  $'@B'           =                                               :(return)
PushBegTag  output          =   gt(xTrace, 4) 'PushBegTag(' upr(t) ')'
            $'@B'           =   link_tag($'@B', upr(t))
            PushBegTag      =   ident(t)  .value($'@B')                     :S(nreturn)
            PushBegTag      =   differ(t) .dummy                            :(nreturn)
PopBegTag   output          =   gt(xTrace, 4) (differ($'@B') value($'@B'), 'fail') ' = PopBegTag()'
            $'@B'           =   differ($'@B') next($'@B')                   :F(freturn)
            PopBegTag       =   .dummy                                      :(nreturn)
TopBegTag   TopBegTag       =   differ($'@B') value($'@B')                  :F(freturn)
            output          =   gt(xTrace, 4) TopBegTag ' = TopBegTag()'    :(return)
DumpBegTag  DumpBegTag      =   .dummy                                      
            gt(xTrace, 5)                                                   :F(nreturn)
            b               =   $'@B'
DumpBegTag3 v               =   differ(b) value(b)                          :F(DumpBegTag9)
            list            =   list (differ(list) ', ', '') v
            b               =   next(b)                                     :(DumpBegTag3)
DumpBegTag9 output          =   '@B = (' list ')'                           :(nreturn)
BegTagEnd
*//-------- --------------- --- ------------------------------------------- -------------------------
            define('InitEndTag()')
            define('PushEndTag(t)')
            define('PopEndTag()')
            define('TopEndTag()')
            define('DumpEndTag()e,list,v')                                  :(EndTagEnd)
InitEndTag  $'@E'           =                                               :(return)
PushEndTag  output          =   gt(xTrace, 4) 'PushEndTag(' upr(t) ')'
            $'@E'           =   link_tag($'@E', upr(t))
            PushEndTag      =   ident(t)  .value($'@E')                     :S(nreturn)
            PushEndTag      =   differ(t) .dummy                            :(nreturn)
PopEndTag   output          =   gt(xTrace, 4) (differ($'@E') value($'@E'), 'fail') ' = PopEndTag()'
            $'@E'           =   differ($'@E') next($'@E')                   :F(freturn)
            PopEndTag       =   .dummy                                      :(nreturn)
TopEndTag   TopEndTag       =   differ($'@E') value($'@E')                  :F(freturn)
            output          =   gt(xTrace, 4) TopEndTag ' = TopEndTag()'    :(return)
DumpEndTag  DumpEndTag      =   .dummy
            gt(xTrace, 5)                                                   :F(nreturn)
            e               =   $'@E'
DumpEndTag3 v               =   differ(e) value(e)                          :F(DumpEndTag9)
            list            =   list (differ(list) ', ', '') v
            e               =   next(e)                                     :(DumpEndTag3)
DumpEndTag9 output          =   '@E = (' list ')'                           :(nreturn)
EndTagEnd
*//-------- --------------- --- ------------------------------------------- -------------------------
