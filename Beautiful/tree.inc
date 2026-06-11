*//-----------------------------------------------------------------------------------------------------------------------
*// $Workfile: tree.inc $ $Revision: 4 $ $Date: 3/13/05 5:39p $ 
*//-------- ----------------- ----- -------------------------------------------------------- -----------------------------
*//               
*// tree: a general purpose tree datatype
*//    t: type of tree (unique identification)
*//    v: value of tree (for leaf node)
*//    n: number of children (for internal node)
*//    c: array of children trees
*//               
               data('tree(t,v,n,c)')        
*//               
*// Append(x,y)       -- insert y tree as the rightmost child of x tree
*// Prepend(x,y)      -- insert y tree as the leftmost child of x tree
*// Insert(x,y,place) -- insert y tree as child of x tree at the designated place
*// Remove(x,place)   -- remove the child tree at the designated place
*//               
*//----------- -------------------------------------------------------------------- -----------------
               define('Append(x,y)')                                                 
               define('Prepend(x,y)')        
               define('Insert(x,y,place)c,i')                                        
               define('Remove(x,place)c,i')                                         :(TreeEnd)
Append         Append         =    Insert(x, y, n(x) + 1)                           :(return)
Prepend        Prepend        =    Insert(x, y, 1)                                  :(return)
Insert         Insert         =    x
               c              =    array('1:' n(x) + 1)
               i              =    0
Insert1        i              =    lt(i, place - 1) i + 1                           :F(Insert2)
               c[i]           =    c(x)[i]                                          :(Insert1)
Insert2        c[i + 1]       =    y
Insert3        i              =    lt(i, n(x)) i + 1                                :F(Insert4)
               c[i + 1]       =    c(x)[i]                                          :(Insert3)
Insert4        n(x)           =    n(x) + 1
               c(x)           =    c                                                :(return)
Remove         Remove         =    x
               c              =    gt(n(x) - 1, 0) array('1:' n(x) - 1) 
               i              =    0
Remove1        i              =    lt(i, place - 1) i + 1                           :F(Remove2)
               c[i]           =    c(x)[i]                                          :(Remove1)
Remove2        i              =    i + 1
Remove3        i              =    lt(i, n(x)) i + 1                                :F(Remove4)
               c[i - 1]       =    c(x)[i]                                          :(Remove3)
Remove4        n(x)           =    n(x) - 1
               c(x)           =    c                                                :(return)
TreeEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('Tree(t,v,n,c1,c2,c3,c4,c5,c6,c7,c8)i,nc')                    :(Tree_End)
Tree           nc             =    8
Tree1          nc             =    gt(nc, 0) ident($('c' nc)) nc - 1                :S(Tree1)
               Tree           =    tree(t, v,
+                                      (gt(nc, 0) nc, NULL)
+                                  ,   (gt(nc, 0) array('1:' nc), NULL)
+                                  )
               i              =    0
Tree3          i              =    lt(i, nc) i + 1                                  :F(return)
               c(Tree)[i]     =    $('c' i)                                         :(Tree3)
Tree_End
*//----------- -------------------------------------------------------------------- -----------------
               define('Equal(x,y)i')                                                :(EqualEnd)
Equal          epsilon        *ident(x) *ident(y)                                   :S(return)
               epsilon        *ident(x) | *ident(y)                                 :F(freturn)
               ident(t(x), t(y))                                                    :F(freturn)
               ident(v(x), v(y))                                                    :F(freturn)
               ident(n(x), n(y))                                                    :F(freturn)
               i              =    0
Equal5         i              =    lt(i, n(x)) i + 1                                :F(return)
               Equal(c(x)[i], c(y)[i])                                              :S(Equal5)F(freturn)
EqualEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('Equiv(x,y)i')                                                :(EquivEnd)
Equiv          t(x)           pos(0) t(y) rpos(0)                                   :F(freturn)
               v(x)           pos(0) v(y) rpos(0)                                   :F(freturn)
               n(x)           pos(0) n(y) rpos(0)                                   :F(freturn)
               i              =    0
Equiv5         i              =    i + 1                                 
               (differ(c(y)) c(y)[i])                                               :F(return)
               Equiv(c(x)[i], c(y)[i])                                              :S(Equiv5)F(freturn)
EquivEnd
*//----------- -------------------------------------------------------------------- -----------------
               define('Find(xn,y,f)i')                                              :(FindEnd)
Find           differ($xn)                                                          :F(return)
               (Equiv($xn, y) apply(f, xn))                                         :S(return)
               i              =    0
Find5          i              =    lt(i, n($xn)) i + 1                              :F(return)
               Find(.c($xn)[i], y, f)                                               :(Find5)
FindEnd
*//----------- -------------------------------------------------------------------- ---------------------
*//            Perform a pre-order traversal of tree x, and apply the function fnc at each node 
*//
               define('Visit(x,fnc)i')                                              :(VisitEnd)
Visit          apply(fnc, x)                                                        :F(return)
               i              =  0
Visit_1        i              =  lt(i, n(x)) i + 1                                  :F(return)
               Visit(c(x)[i], fnc)                                                  :(Visit_1)
VisitEnd
