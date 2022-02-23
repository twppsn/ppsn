# Filter

Für Filter gibt es aktuell 3 Darstellungen von Filterbedingungen.

Diese bietet eine möglicheit Zeilen-Bedingungen zu definieren, welche in diverse Sprachen übersetzt werden. Z.b. SQL, MaschinenCode, ... .
Komplexerer Möglichkeiten können per Native-Filter definiert werden.

## Syntax - Text

```
expr ::=
    [identifier] ( ':' [  symbol  ] )
    [ '(' ]  value  [ ')' ]
    [ 'and' | 'or' | 'nand' | 'nor' ] '(' expr { SP ... } [ ')' ]
    ':' native ':'
    value
    
symbol ::=
	=	Gleich
	<	Weniger als
	>	Größer als
	[	Beginnt mit
	]	Endet mit
	[]	Enthält
	<=	kleiner als oder gleich
	>=	Größer oder gleich
	!	Verneinungsoperator 
	!=	Ungleich
	![	Nicht mit beginnt
	!]	Nicht mit Endet
	![]	Nicht enthält
	
value ::= 
    Text |
    `"` Text `"` |
    `#` Text
    `#` number `#`
    `$` Text
```

Bsp:
```
ID:=(23, 34, 545)
NAME:["St"
```

## Syntax - Table

Filter können als Lua-Table definiert werden.

```
{ [0] = logic, { COLUMN = VALUE, ... }, "Expr", { ... } }
```

Eine `table` umklammert einen logischen Operator. Dieser Operator wird am Index `0` abgelegt.

- `nil`: Wird als `UND` interpretiert.
- `"and"`: Logisches Und.
- `"or"`: Logisches Oder.
- `"nand"`: Logisches Nicht Und.
- `"nor"`: Logisches Nicht Oder.

Zusätzlich kann auch der `PpsDataFilterExpressionType` verwendet werden.

```
{ [0] = "or", ID = 23, TNR = "4223" }
```
Die `Column/Value` paare werden zu Equal Operationen umgebaut. Ist der Wert `nil`, wird die
entsprechende Operation weggelassen. Es wird kein `NULL` vergleich generiert.

```
{ ID = 23 }
```

Der `Value` kann ebenfalls eine Tabelle sein, wenn ein `in` generiert werden soll.

```
{ ID = { 213, 324, 435, 546 }}
```

Eine einzelne Zeichenkette wird als Text-Expression geparst.

```
{ "ID:=23" }
```

Die logischen Operatoren können geschachtelt/geklammert werden.

```
{ [0] = "or", "ID:=23", { [0] = "and", TNR = "2332", POS = "023" } }
```

## Syntax - C#

Siehe dazu `PpsDataFilterExpression`...