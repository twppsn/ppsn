# Filter

## Syntax - Text

```
expr ::=
    [ identifier ] ( ':' [ '<' | '>' | '<=' | '>=' | '!' | '!=' ) [ '(' ] value [ ')' ]
    [ 'and' | 'or' | 'nand' | 'nor' ] '(' expr { SP ... } [ ')' ]
    ':' native ':'
    value

value ::= 
    Text |
    `"` Text `"` |
    `#` Text
    `#` number `#`
    `$` Text
```

```
ID:=(23, 34, 545)
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
- `"nand"`: Logisches NichtUnd.
- `"nor"`: Logisches NichtOder.

Zusätzlich kann auch der `PpsDataFilterExpressionType` verwendet werden.

```
{ [0] = "or", ID = 23, TNR = "4223" }
```
Die `Column/Value` paare werden zu Equal operationen umgebaut. Ist der Wert `nil`, wird die
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

Die logischen Operatoren können geschachelt/geklammert werden.

```
{ [0] = "or", "ID:=23", { [0] = "and", TNR = "2332", POS = "023" } }
```