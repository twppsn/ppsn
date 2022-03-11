# Excel-Plugin

Lädt Smart-Tables auf Basis von XML-Datenströmen von der Rest-Schnittstelle des PPSn in ein Excel.

![Menü](Imgs/Excel.Menu.png)

Zuerst muss eine Umgebung/Datenbankverbindung gewählt werden.

Danach können `Reporte` eingefügt/geladen oder Smart-Table definiert werden (`Tabelle`).

## Tabelle definieren

Als erstes wählt man eine Basis-Tabelle aus.

![Tabelle auswählen](Imgs/Excel.Table1.png)

## Spalten definieren

Spalten werden in der mittleren Liste definiert. Dies werden per Drag&Drop in die 
Rückgabe aufgenommen.

Sortierung, Name einer Spalte kann mittels des Kontextmenüs beeinflusst werden.

![Tabelle auswählen](Imgs/Excel.Table2.png)

:::tipp
Mit F4 kann die Liste der Quellspalten auf die internen Datenbanknamen umgeschaltet werden und es werden zusätzlich versteckte Spalten angezeigt.
:::

## Bedingungen

Die dritte Spalte beinhält die Filterbedingungen.

![Tabelle auswählen](Imgs/Excel.Table3.png)

Neben Konstanten und anderen Felder können auch Excel-Zellen referenziert werden.

- Via Namen z.B. `$Feld`
- Oder Zelladresse z.B. `$C23` oder `$R23C3`
- Tabellen können auch angegeben werden, z.B. `$Tabelle1_C23`

:::warn
Sind die Namen der Spalten durchgestrichen, so ist diese Bedingung irrelevant für
die Rückgabe und wird beim Aktualisieren entfernt.
:::

Die Formatierung des Wertes der Variable hat Einfluss darauf, wie der Wert vom System interpretiert wird.
Grundlegend gibt es in Excel 3 Datentypen.

- Text
:   Text werden als Zeichenkette weitergegeben.

- Zahl
:   Bei Zahlen wird grundlegend in Gleitkomma und Ganzzahlen unterschieden. 
    Gleitkommazahlen werden als Dezimalwert weitergegeben. Bei Ganzzahlen werden
    als 64-Bit Integer weitegegeben. Existiert ein Format mit einer `0+` Schablone 
    so wird angenommen, dass es sich im einen Key handelt mit signifikanten führenden
    Nullen.

- Datum
:   Daten werden immer in von-bis Bereiche aufgespalten. Welche Bereiche verwendet
    werden sollen, wird anhand des Formates ermittelt. Enthält das Format
    - `JJJJ` wird das Jahr verwendet
    - `MMM` oder `MM`, aktiviert den Monat
    - `dd` setzt den Tag

- Auflistungen
  :   Benannte Felder (Namensmanager) können auch Bereiche umfassen. Einem Bereich wird 
    die erste Spalte in ein Array umgewandelt, welches für die Variable eingesetzt wird.

:::tipp
Bei der Eingabe von '#', '$' oder ':' erscheint ein Popup-Fenster, das Ihnen damit hilft, den Wert des Operanden auszuwählen. 
'#' für Zeit-Kalender, '$' für eine Liste anderer Feldnamen und ':' für eine Liste mit allen vordefinierten Zellen im Excel-Arbeitsblatt.
Die gleiche Funktionalität kann durch Klicken auf die entsprechende Schaltfläche ausgeführt werden.
:::
