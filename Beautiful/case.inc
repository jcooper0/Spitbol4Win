*//--------------------------------------------------------------------------------------------------
*// $Workfile: case.inc $ $Revision: 3 $ $Date: 3/10/05 11:42a $ 
*//----------- -------------------------------------------------------------------- -----------------
*//
*// Lower case string: string = lwr(string)
*//
               define('lwr(lwr)')                                                   :(lwr_end)
lwr            lwr            =   replace(lwr, &ucase, &lcase)                      :(return)
lwr_end
*//----------- -------------------------------------------------------------------- -----------------
*//
*// Upper case string: string = upr(string)
*//
               define('upr(upr)')                                                   :(upr_end)
upr            upr            =   replace(upr, &lcase, &ucase)                      :(return)
upr_end
*//----------- -------------------------------------------------------------------- -----------------
*//
*// Capitalize string: string = cap(string)
*//
               define('cap(cap)')                                                   :(cap_end)
cap            cap            =   replace(substr(cap, 1, 1), &lcase, &ucase)
+                                 replace(substr(cap, 2), &ucase, &lcase)           :S(return)F(error)
cap_end
*//----------- -------------------------------------------------------------------- -----------------
*//
*// Create pattern for case insensitive string match:
*// pattern = icase(string)
*//
               define('icase(str)letter,character')                                 :(icase_end)
icase          ident(str)                                                           :S(return)
               str            pos(0) any(&ucase &lcase) . letter =                  :F(icase1)
               icase          =   icase (upr(letter) | lwr(letter))                 :(icase)
icase1         str            pos(0) len(1) . character =
               icase          =   icase character                                   :(icase)
icase_end
