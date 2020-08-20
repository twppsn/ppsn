# Views 

## Registrierung eines Views

Views werden immer an Datenquellen gebunden und mittels des Register-Knotes
zur Verfügung gestellt.

```Xml
<pps:ppsn name="ppsn">
	<pps:register source="cat">
		<pps:view name="dbo.KataView">
			<pps:source type="file">KataView.sql</pps:source>
```

### Name

Der Name besteht bei Sql-Datenbanken immer aus einem Namespace und den 
eigentlichen View-Name.

:::warn
Der Namespace muss in der Datenbank existieren.
:::

Es sollte zusätzlich für den Anwender ein `displayName` definiert werden.

### Quelle

Die Quelle (`source`) gibt die Viewdefinition vor. Es handelt sich dabei um ein
Select-Ausdruck.

`type` beschreibt die Art der Quelle.
- `select` - Der Select wird direkt in der Xml-Definition abgelegt.
- `file` - Der Select steht in einer Datei.
- `resource` - Der Select ist in einem geladenen Assembly definiert.
- `view` - Die Quelle ist ein in der DB vorhandener View.

### Join-Vorschläge

todo!

```Xml
<pps:join id="joinLief" view="dbo.LiefView" on="KATALIEFID=LIEFID" />
```

todo: beschreibung `alias`

### Native Filter

todo!

### Native Sortierungen

todo!

### Attribute

Es können beliebige Attribute an den View gebunden werden.

```Xml
<pps:view>
	...
	<pps:attribute name="description">Katalog</pps:attribute>
	<pps:attribute name="bi.visible">true</pps:attribute>
```

## Views neu laden

Wurde der View geändert so kann er einzeln im Server aktualisiert werden, ohne
das der Server neugestartet werden muss.

Dazu muss am Knoten `ppsn` `RefreshView` aufgerufen werden.

```Lua
RefreshView("Cat.dbo.KataView");
```

## Zugriff

### `GetViewDefinition`

Gibt die View-Registrierungsdaten zurück. Siehe Klasse [`PpsViewDescription`](@type:TecWare.PPSn.Server.PpsViewDescription).

Der Name kann dabei voll qualifiziert sein, also: [Datenquelle].[Namespace].[ViewName]

Datenquelle und Namespace kann wegelassen werden.

### Web-Interface

```
http://server/ppsn/?action=viewget&v=views.Artikel%20t&f=or%28t.TEILBEST%3A%3C10%20t.TEILBEST%3A%3E100%29&r=t.TEILTNR:Artikel_Nr,t.TEILNAME1:Artikelbezeichnung,t.TEILBEST:Bestand&o=%2Bt.TEILTNR
```

`v`: Name der Views
`f`: Filter-Bedingung
`r`: Spaltendefinition