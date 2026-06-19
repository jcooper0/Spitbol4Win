# SPITBOL error-code coverage & classification

- Codes defined by sbl.min : 329
- tested                   : 274
- untested-gap             : 17
- unreachable              : 20
- not-testable             : 18
- reserved (numbering gaps): 3

## Untested gaps (actionable: add a test or classify)

- 7: compilation error encountered during execution
- 105: exit action not available in this implementation
- 106: exit action caused irrecoverable error
- 203: input file record has incorrect format
- 213: syntax error: statement is too complicated.
- 245: translation/execution time expired
- 249: expression evaluated by name returned value
- 253: print limit exceeded on standard output channel
- 260: conversion array size exceeds maximum permitted
- 268: inconsistent value assigned to keyword profile
- 284: excessively nested include files
- 288: exit second argument is not a string
- 319: backspace caused non-recoverable error
- 321: goto scontinue with no preceding error
- 329: requested maxlngth too large
- 331: goto scontinue with no user interrupt
- 332: goto continue with error in failure goto

## Full classification

| Code | Class | Note | Message |
|-----:|-------|------|---------|
| 001 | tested |  | addition left operand is not numeric |
| 002 | tested |  | addition right operand is not numeric |
| 003 | tested |  | addition caused integer overflow |
| 004 | tested |  | affirmation operand is not numeric |
| 005 | tested |  | alternation right operand is not pattern |
| 006 | tested |  | alternation left operand is not pattern |
| 007 | untested-gap |  | compilation error encountered during execution |
| 008 | tested |  | concatenation left operand is not a string or pattern |
| 009 | tested |  | concatenation right operand is not a string or pattern |
| 010 | tested |  | negation operand is not numeric |
| 011 | tested |  | negation caused integer overflow |
| 012 | tested |  | division left operand is not numeric |
| 013 | tested |  | division right operand is not numeric |
| 014 | tested |  | division caused integer overflow |
| 015 | tested |  | exponentiation right operand is not numeric |
| 016 | tested |  | exponentiation left operand is not numeric |
| 017 | tested |  | exponentiation caused integer overflow |
| 018 | tested |  | exponentiation result is undefined |
| 019 | unreachable | auto-promotes to real; never fires | exponentiation right operand is negative |
| 020 | tested |  | goto evaluation failure |
| 021 | tested |  | function called by name returned a value |
| 022 | tested |  | undefined function called |
| 023 | tested |  | goto operand is not a natural variable |
| 024 | tested |  | goto operand in direct goto is not code |
| 025 | tested |  | immediate assignment left operand is not pattern |
| 026 | tested |  | multiplication left operand is not numeric |
| 027 | tested |  | multiplication right operand is not numeric |
| 028 | tested |  | multiplication caused integer overflow |
| 029 | tested |  | undefined operator referenced |
| 030 | tested |  | pattern assignment left operand is not pattern |
| 031 | tested |  | pattern replacement right operand is not a string |
| 032 | tested |  | subtraction left operand is not numeric |
| 033 | tested |  | subtraction right operand is not numeric |
| 034 | tested |  | subtraction caused integer overflow |
| 035 | tested |  | unexpected failure in -nofail mode |
| 036 | tested |  | goto abort with no preceding error |
| 037 | tested |  | goto continue with no preceding error |
| 038 | tested |  | goto undefined label |
| 039 | tested |  | external function argument is not a string |
| 040 | tested |  | external function argument is not integer |
| 041 | tested |  | field function argument is wrong datatype |
| 042 | tested |  | attempt to change value of protected variable |
| 043 | tested |  | any evaluated argument is not a string |
| 044 | tested |  | break evaluated argument is not a string |
| 045 | tested |  | breakx evaluated argument is not a string |
| 046 | tested |  | expression does not evaluate to pattern |
| 047 | tested |  | len evaluated argument is not integer |
| 048 | tested |  | len evaluated argument is negative or too large |
| 049 | tested |  | notany evaluated argument is not a string |
| 050 | tested |  | pos evaluated argument is not integer |
| 051 | tested |  | pos evaluated argument is negative or too large |
| 052 | tested |  | rpos evaluated argument is not integer |
| 053 | tested |  | rpos evaluated argument is negative or too large |
| 054 | tested |  | rtab evaluated argument is not integer |
| 055 | tested |  | rtab evaluated argument is negative or too large |
| 056 | tested |  | span evaluated argument is not a string |
| 057 | tested |  | tab evaluated argument is not integer |
| 058 | tested |  | tab evaluated argument is negative or too large |
| 059 | tested |  | any argument is not a string or expression |
| 060 | tested |  | apply first arg is not natural variable name |
| 061 | tested |  | arbno argument is not pattern |
| 062 | tested |  | arg second argument is not integer |
| 063 | tested |  | arg first argument is not program function name |
| 064 | tested |  | array first argument is not integer or string |
| 065 | tested |  | array first argument lower bound is not integer |
| 066 | tested |  | array first argument upper bound is not integer |
| 067 | tested |  | array dimension is zero, negative or out of range |
| 068 | tested |  | array size exceeds maximum permitted |
| 069 | tested |  | break argument is not a string or expression |
| 070 | tested |  | breakx argument is not a string or expression |
| 071 | tested |  | clear argument is not a string |
| 072 | tested |  | clear argument has null variable name |
| 073 | tested |  | collect argument is not integer |
| 074 | tested |  | convert second argument is not a string |
| 075 | tested |  | data argument is not a string |
| 076 | tested |  | data argument is null |
| 077 | tested |  | data argument is missing a left paren |
| 078 | tested |  | data argument has null datatype name |
| 079 | tested |  | data argument is missing a right paren |
| 080 | tested |  | data argument has null field name |
| 081 | tested |  | define first argument is not a string |
| 082 | tested |  | define first argument is null |
| 083 | tested |  | define first argument is missing a left paren |
| 084 | tested |  | define first argument has null function name |
| 085 | tested |  | null arg name or missing ) in define first arg. |
| 086 | tested |  | define function entry point is not defined label |
| 087 | tested |  | detach argument is not appropriate name |
| 088 | tested |  | dump argument is not integer |
| 089 | tested |  | dump argument is negative or too large |
| 090 | tested |  | dupl second argument is not integer |
| 091 | tested |  | dupl first argument is not a string or pattern |
| 092 | tested |  | eject argument is not a suitable name |
| 093 | tested |  | eject file does not exist |
| 094 | tested |  | eject file does not permit page eject |
| 095 | not-testable | fault-injection | eject caused non-recoverable output error |
| 096 | tested |  | endfile argument is not a suitable name |
| 097 | tested |  | endfile argument is null |
| 098 | tested |  | endfile file does not exist |
| 099 | not-testable | fault-injection | endfile file does not permit endfile |
| 100 | not-testable | fault-injection | endfile caused non-recoverable output error |
| 101 | tested |  | eq first argument is not numeric |
| 102 | tested |  | eq second argument is not numeric |
| 103 | tested |  | eval argument is not expression |
| 104 | tested |  | exit first argument is not suitable integer or string |
| 105 | untested-gap |  | exit action not available in this implementation |
| 106 | untested-gap |  | exit action caused irrecoverable error |
| 107 | tested |  | field second argument is not integer |
| 108 | tested |  | field first argument is not datatype name |
| 109 | tested |  | ge first argument is not numeric |
| 110 | tested |  | ge second argument is not numeric |
| 111 | tested |  | gt first argument is not numeric |
| 112 | tested |  | gt second argument is not numeric |
| 113 | tested |  | input third argument is not a string |
| 114 | tested |  | inappropriate second argument for input |
| 115 | tested |  | inappropriate first argument for input |
| 116 | tested |  | inappropriate file specification for input |
| 117 | tested |  | input file cannot be read |
| 118 | tested |  | le first argument is not numeric |
| 119 | tested |  | le second argument is not numeric |
| 120 | tested |  | len argument is not integer or expression |
| 121 | tested |  | len argument is negative or too large |
| 122 | tested |  | leq first argument is not a string |
| 123 | tested |  | leq second argument is not a string |
| 124 | tested |  | lge first argument is not a string |
| 125 | tested |  | lge second argument is not a string |
| 126 | tested |  | lgt first argument is not a string |
| 127 | tested |  | lgt second argument is not a string |
| 128 | tested |  | lle first argument is not a string |
| 129 | tested |  | lle second argument is not a string |
| 130 | tested |  | llt first argument is not a string |
| 131 | tested |  | llt second argument is not a string |
| 132 | tested |  | lne first argument is not a string |
| 133 | tested |  | lne second argument is not a string |
| 134 | tested |  | local second argument is not integer |
| 135 | tested |  | local first arg is not a program function name |
| 136 | tested |  | load second argument is not a string |
| 137 | tested |  | load first argument is not a string |
| 138 | tested |  | load first argument is null |
| 139 | tested |  | load first argument is missing a left paren |
| 140 | tested |  | load first argument has null function name |
| 141 | tested |  | load first argument is missing a right paren |
| 142 | tested |  | load function does not exist |
| 143 | not-testable | needs external DLL (load input error during load) | load function caused input error during load |
| 144 | tested |  | lpad third argument is not a string |
| 145 | tested |  | lpad second argument is not integer |
| 146 | tested |  | lpad first argument is not a string |
| 147 | tested |  | lt first argument is not numeric |
| 148 | tested |  | lt second argument is not numeric |
| 149 | tested |  | ne first argument is not numeric |
| 150 | tested |  | ne second argument is not numeric |
| 151 | tested |  | notany argument is not a string or expression |
| 152 | tested |  | opsyn third argument is not integer |
| 153 | tested |  | opsyn third argument is negative or too large |
| 154 | tested |  | opsyn second arg is not natural variable name |
| 155 | tested |  | opsyn first arg is not natural variable name |
| 156 | tested |  | opsyn first arg is not correct operator name |
| 157 | tested |  | output third argument is not a string |
| 158 | tested |  | inappropriate second argument for output |
| 159 | tested |  | inappropriate first argument for output |
| 160 | tested |  | inappropriate file specification for output |
| 161 | not-testable | fault-injection (open failure maps to statement failure) | output file cannot be written to |
| 162 | tested |  | pos argument is not integer or expression |
| 163 | tested |  | pos argument is negative or too large |
| 164 | tested |  | prototype argument is not valid object |
| 165 | tested |  | remdr second argument is not numeric |
| 166 | tested |  | remdr first argument is not numeric |
| 167 | tested |  | remdr caused integer overflow |
| 168 | tested |  | replace third argument is not a string |
| 169 | tested |  | replace second argument is not a string |
| 170 | tested |  | replace first argument is not a string |
| 171 | tested |  | null or unequally long 2nd, 3rd args to replace |
| 172 | tested |  | rewind argument is not a suitable name |
| 173 | tested |  | rewind argument is null |
| 174 | tested |  | rewind file does not exist |
| 175 | tested |  | rewind file does not permit rewind |
| 176 | not-testable | fault-injection | rewind caused non-recoverable error |
| 177 | tested |  | reverse argument is not a string |
| 178 | tested |  | rpad third argument is not a string |
| 179 | tested |  | rpad second argument is not integer |
| 180 | tested |  | rpad first argument is not a string |
| 181 | tested |  | rtab argument is not integer or expression |
| 182 | tested |  | rtab argument is negative or too large |
| 183 | tested |  | tab argument is not integer or expression |
| 184 | tested |  | tab argument is negative or too large |
| 185 | tested |  | rpos argument is not integer or expression |
| 186 | tested |  | rpos argument is negative or too large |
| 187 | tested |  | setexit argument is not label name or null |
| 188 | tested |  | span argument is not a string or expression |
| 189 | tested |  | size argument is not a string |
| 190 | unreachable | numeric first arg accepted; never fires | stoptr first argument is not appropriate name |
| 191 | tested |  | stoptr second argument is not trace type |
| 192 | tested |  | substr third argument is not integer |
| 193 | tested |  | substr second argument is not integer |
| 194 | tested |  | substr first argument is not a string |
| 195 | tested |  | table argument is not integer |
| 196 | tested |  | table argument is out of range |
| 197 | unreachable | never fires on this build | trace fourth arg is not function name or null |
| 198 | unreachable | numeric first arg accepted; never fires | trace first argument is not appropriate name |
| 199 | tested |  | trace second argument is not trace type |
| 200 | tested |  | trim argument is not a string |
| 201 | tested |  | unload argument is not natural variable name |
| 202 | not-testable | fault-injection | input from file caused non-recoverable error |
| 203 | untested-gap |  | input file record has incorrect format |
| 204 | not-testable | fault-injection (out of memory) | memory overflow |
| 205 | tested |  | string length exceeds value of maxlngth keyword |
| 206 | not-testable | fault-injection | output caused file overflow |
| 207 | tested |  | output caused non-recoverable error |
| 208 | tested |  | keyword value assigned is not integer |
| 209 | tested |  | keyword in assignment is protected |
| 210 | tested |  | keyword value assigned is negative or too large |
| 211 | tested |  | value assigned to keyword errtext not a string |
| 212 | tested |  | syntax error: value used where name is required |
| 213 | untested-gap |  | syntax error: statement is too complicated. |
| 214 | tested |  | bad label or misplaced continuation line |
| 215 | tested |  | syntax error: undefined or erroneous entry label |
| 216 | not-testable | eNd valid under case-folding; missing-END abort is unnumbered | syntax error: missing end line |
| 217 | tested |  | syntax error: duplicate label |
| 218 | tested |  | syntax error: duplicated goto field |
| 219 | tested |  | syntax error: empty goto field |
| 220 | tested |  | syntax error: missing operator |
| 221 | tested |  | syntax error: missing operand |
| 222 | tested |  | syntax error: invalid use of left bracket |
| 223 | tested |  | syntax error: invalid use of comma |
| 224 | tested |  | syntax error: unbalanced right parenthesis |
| 225 | tested |  | syntax error: unbalanced right bracket |
| 226 | tested |  | syntax error: missing right paren |
| 227 | tested |  | syntax error: right paren missing from goto |
| 228 | tested |  | syntax error: right bracket missing from goto |
| 229 | tested |  | syntax error: missing right array bracket |
| 230 | tested |  | syntax error: illegal character |
| 231 | tested |  | syntax error: invalid numeric item |
| 232 | tested |  | syntax error: unmatched string quote |
| 233 | tested |  | syntax error: invalid use of operator |
| 234 | tested |  | syntax error: goto field incorrect |
| 235 | tested |  | subscripted operand is not table or array |
| 236 | tested |  | array referenced with wrong number of subscripts |
| 237 | tested |  | table referenced with more than one subscript |
| 238 | tested |  | array subscript is not integer |
| 239 | tested |  | indirection operand is not name |
| 240 | tested |  | pattern match right operand is not pattern |
| 241 | tested |  | pattern match left operand is not a string |
| 242 | tested |  | function return from level zero |
| 243 | tested |  | function result in nreturn is not name |
| 244 | tested |  | statement count exceeds value of stlimit keyword |
| 245 | untested-gap |  | translation/execution time expired |
| 246 | tested |  | stack overflow |
| 247 | tested |  | invalid control statement |
| 248 | tested |  | attempted redefinition of system function |
| 249 | untested-gap |  | expression evaluated by name returned value |
| 250 | not-testable | fault-injection (dump OOM) | insufficient memory to complete dump |
| 251 | tested |  | keyword operand is not name of defined keyword |
| 252 | not-testable | fault-injection | error on printing to interactive channel |
| 253 | untested-gap |  | print limit exceeded on standard output channel |
| 254 | not-testable | HOST platform-dependent | erroneous argument for host |
| 255 | not-testable | HOST platform-dependent | error during execution of host |
| 256 | tested |  | sort/rsort 1st arg not suitable array or table |
| 257 | tested |  | erroneous 2nd arg in sort/rsort of vector |
| 258 | tested |  | sort/rsort 2nd arg out of range or non-integer |
| 259 | tested |  | fence argument is not pattern |
| 260 | untested-gap |  | conversion array size exceeds maximum permitted |
| 261 | tested |  | addition caused real overflow |
| 262 | tested |  | division caused real overflow |
| 263 | tested |  | multiplication caused real overflow |
| 264 | tested |  | subtraction caused real overflow |
| 265 | tested |  | external function argument is not real |
| 266 | tested |  | exponentiation caused real overflow |
| 267 | unreachable | real exponent accepted; never fires | exponentiation right operand is real not integer |
| 268 | untested-gap |  | inconsistent value assigned to keyword profile |
| 269 | unreachable | BUFFER not callable | buffer first argument is not integer |
| 270 | unreachable | BUFFER not callable | buffer second argument is not a string or buffer |
| 271 | unreachable | BUFFER not callable | buffer initial value too big for allocation |
| 272 | unreachable | BUFFER not callable | buffer first argument is not positive |
| 273 | unreachable | BUFFER not callable | buffer size exceeds value of maxlngth keyword |
| 274 | tested |  | value assigned to keyword fullscan is zero |
| 275 | unreachable | APPEND not callable | append first argument is not a buffer |
| 276 | unreachable | APPEND not callable | append second argument is not a string |
| 277 | unreachable | INSERT not callable | insert third argument not integer |
| 278 | unreachable | INSERT not callable | insert second argument not integer |
| 279 | unreachable | INSERT not callable | insert first argument is not a buffer |
| 280 | unreachable | INSERT not callable | insert fourth argument is not a string |
| 281 | tested |  | char argument not integer |
| 282 | tested |  | char argument not in range |
| 283 | unreachable | long strings accepted; never fires | string length exceeded for generalized lexical comparison |
| 284 | untested-gap |  | excessively nested include files |
| 285 | tested |  | include file cannot be opened |
| 286 | tested |  | function call to undefined entry label |
| 287 | tested |  | value assigned to keyword maxlngth is too small |
| 288 | untested-gap |  | exit second argument is not a string |
| 289 | tested |  | input channel currently in use |
| 290 | tested |  | output channel currently in use |
| 291 | tested |  | set first argument is not a suitable name |
| 292 | tested |  | set first argument is null |
| 293 | tested |  | inappropriate second argument to set |
| 294 | tested |  | inappropriate third argument to set |
| 295 | tested |  | set file does not exist |
| 296 | tested |  | set file does not permit setting file pointer |
| 297 | not-testable | fault-injection | set caused non-recoverable i/o error |
| 298 | tested |  | external function argument is not file |
| 299 | reserved |  | (undefined / reserved) |
| 300 | reserved |  | (undefined / reserved) |
| 301 | tested |  | atan argument not numeric |
| 302 | tested |  | chop argument not numeric |
| 303 | tested |  | cos argument not numeric |
| 304 | tested |  | exp argument not numeric |
| 305 | tested |  | exp produced real overflow |
| 306 | tested |  | ln argument not numeric |
| 307 | tested |  | ln produced real overflow |
| 308 | tested |  | sin argument not numeric |
| 309 | tested |  | tan argument not numeric |
| 310 | unreachable | tan returns finite; never fires | tan produced real overflow or argument is out of range |
| 311 | tested |  | exponentiation of negative base to non-integral power |
| 312 | tested |  | remdr caused real overflow |
| 313 | tested |  | sqrt argument not numeric |
| 314 | tested |  | sqrt argument negative |
| 315 | tested |  | ln argument negative |
| 316 | tested |  | backspace argument is not a suitable name |
| 317 | tested |  | backspace file does not exist |
| 318 | tested |  | backspace file does not permit backspace |
| 319 | untested-gap |  | backspace caused non-recoverable error |
| 320 | not-testable | user interrupt (SIGINT) | user interrupt |
| 321 | untested-gap |  | goto scontinue with no preceding error |
| 322 | unreachable | cos reduces via libc; never fires | cos argument is out of range |
| 323 | unreachable | sin reduces via libc; never fires | sin argument is out of range |
| 324 | tested |  | set second argument not numeric |
| 325 | reserved |  | (undefined / reserved) |
| 326 | tested |  | calling external function - bad argument type |
| 327 | not-testable | needs external DLL | calling external function - not found |
| 328 | not-testable | needs external DLL | load function - insufficient memory |
| 329 | untested-gap |  | requested maxlngth too large |
| 330 | tested |  | date argument is not integer |
| 331 | untested-gap |  | goto scontinue with no user interrupt |
| 332 | untested-gap |  | goto continue with error in failure goto |

