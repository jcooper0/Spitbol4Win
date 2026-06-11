*//--------------------------------------------------------------------------------------------------
*// $Workfile: global.inc $ $Revision: 4 $ $Date: 3/10/05 11:46a $ 
*//----------- -------------------------------------------------------------------- -----------------
  
			   &alphabet      pos(0)  len(1) . nul    ;*// null character
               &alphabet      pos(8)  len(1) . bs     ;*// backspace
               &alphabet      pos(9)  len(1) . ht     ;*// horizontal tab
               &alphabet      pos(9)  len(1) . tab    ;*// tab
               &alphabet      pos(10) len(1) . nl     ;*// new line
               &alphabet      pos(10) len(1) . lf     ;*// line feed
               &alphabet      pos(11) len(1) . vt     ;*// vertical tab
               &alphabet      pos(12) len(1) . ff     ;*// form feed
               &alphabet      pos(13) len(1) . cr     ;*// carriage return
               &alphabet      pos(47) len(1) . fSlash ;*// forward slash
               &alphabet      pos(92) len(1) . bSlash ;*// back slash

               TRUE           =   1
               FALSE          =   0
               digits         =  '0123456789'
