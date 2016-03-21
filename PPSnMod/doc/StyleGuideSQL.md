# Styleguide Datenabank (MSSQL)

## allg. Regeln

* Leerzeichen bei "<", ">" und "=" davor und danach verwenden
* Leerzeichen bei "," danach verwenden
* jeder Befehl wird mit ";" abgeschlossen
* Reservierte Schlüsselwörter werden immer GROSS geschrieben
	Bsp.: SELECT, SUM, AND, CHECK, NOT NULL
* Kommentare vor den Block/Anweisung schreiben
* nie "SELECT *" bei den Rückgaben verwenden
* Blöcke/Anweisungen formatieren (Umbrüche, Leerzeilen, Tabulator, ... )
```sql 
	BEGIN
		WITH tmpBest (tNr, tDatum, tText) AS
		(
			SELECT BekoNr, BekoDatum, ProdText
				FROM vvs.Beko AS tBeko
					INNER JOIN vvs.Bepo AS tBepo ON tBeko.BekoId = tBepo.BepoBekoId
					INNER JOIN sds.Prod AS tProd ON tBepo.BepoProdId = tProd.ProdId
				WHERE BekoDatum > CAST('2011-11-11' as DATE)
				ORDER BY BekoDatum ASC
		)
		SELECT tNr, tDatum, tText
			FROM tmpBest;
	END;
```
			
## Datentypen

* nvarchar für Anzeigetexte/Schlüssel
* varchar/char für interne Daten / Statusfelder mit sprechenden Bezeichnern
	Bsp.:  char(1) -> 'E' - Entwurf, 'A' - Archiv
* bigint für Identity-Spalten
* money für Währungswerte
* date für Datum ohne Zeit
* datetime2/datetimeoffset für Datum mit Zeit und Sekunden
* xml für XML-Daten
* varbinary(max) für umfangreiche Objekte (z.B. Bilder, Dateien)
* bit für Flags
* int für Bitmasken
* decimal(?,?) für Mengen

## Namenskonvention

### Schema

* Kleinschreibung
* sprechende Bezeichnung
* 3 Buchstaben für Systemschema (siehe auch Anhang)
* mehr als 3 Buchstaben für Kundenspezifische Lösungen und nicht auf 's' endend
* Bsp.: pps - Produktionsplanungsystem, hrs - Personalsystem, kmbde - BDE Kersten Maschinenbau

### Tabelle

* 4-Buchstaben 
* erster Buchstabe GROSS, alle anderen klein (Ausnahme temporäre Tabellen)
* temporäre Tabelle mit 'tmp' beginnen
* sprechende Bezeichner 
* jede Tabelle hat einen PK als bigint (gilt auch für Verknüpfungstabellen m:n)
* Bsp.: Kont - Kontakte, Beko - Bestellkopf

### Spalten

* Camel Case - Schreibweise
* sprechende Bezeichner mit führendem Tabellenamen
* Bsp.: KontName, BekoId
* feste Spaltennamen (Tabellename + Spaltenname):
	- Id - Primärschlüssel
	- Status - Statusfelder
	- Datum - Datumsfeld (Bsp.: BekoDatum, AukoDatum)
	- Nr - externe Nummer
	- Name?? - Namesfelder
	- Text?? - freie Textfelder

### Primär- / Fremdschlüssel

* Syntax: <Typ>_<Spaltenname>[_<NameFKSpalte>]
* <Typ> - pk (Primary Key), fk (Foreign Key)
* Bsp.: pk_KontId, fk_KontWaehId_WaehId

### Indizes / Checks 

* Syntax: <Typ>_<Spaltenname>[_<Spaltennname>]
* <Typ> idx - Index, chk - Check, 
* Bsp.: idx_KontId_KontName, chk_KontName

### Views

* Syntax: <Name>View
* mit Angabe der Spaltennamen
* Bsp.: 
```sql
	CREATE VIEW sds.BekoView (Nr, Datum, ...)
		AS 
			SELECT ...
```

### Variablen

* erster Buchstabe immer klein
* Bsp.: @bepoId, @text


## OBJK

* zentrale Tabelle für alle Objekte

## Anhang

### Systemschemas

| Sys.-schema | Name                                    |
|-------------|-----------------------------------------|
| pps         | Produktionsplanungsystem                |
| hrs         | Human Ressourses (Personalsystem)       |
| fas					| Financial Accounting (Finanzsystem)     |
| qms         | Quality Management (Qualitätmanagement) |
| sds         | Sales & Distribution (Einkauf/Verkauf)  |
