# Excel-Plugin

L�dt Smart-Tables auf Basis von XML-Datenstr�men von der Rest-Schnittstelle des PPSn in ein Excel.

![Men�](Imgs/Excel.Menu.png)

Zuerst muss eine Umgebung/Datenbankverbindung gew�hlt werden.

Danach k�nnen `Reporte` eingef�gt/geladen oder Smart-Table definiert werden (`Tabelle`).

## Tabelle definieren

Als erstes w�hlt man eine Basis-Tabelle aus.

![Tabelle ausw�hlen](Imgs/Excel.Table1.png)

## Spalten definieren

Spalten werden in der mittleren Liste definiert. Dies werden per Drag&Drop in die 
R�ckgabe aufgenommen.

Sortierung, Name einer Spalte kann mittels des Kontextmen�s beeinflusst werden.

![Tabelle ausw�hlen](Imgs/Excel.Table2.png)

:::tipp
Mit F4 kann die Liste der Quellspalten auf die internen Datenbanknamen umgeschaltet werden und es werden zus�tzlich versteckte Spalten angezeigt.
:::

## Bedingungen

Die dritte Spalte beinh�lt die Filterbedingungen.

![Tabelle ausw�hlen](Imgs/Excel.Table3.png)

Neben Konstanten und anderen Felder k�nnen auch Excel-Zellen referenziert werden.

- Via Namen z.B. `$Feld`
- Oder Zelladresse z.B. `$C23` oder `$R23C3`
- Tabellen k�nnen auch angegeben werden, z.B. `$Tabelle1_C23`

:::warn
Sind die Namen der Spalten durchgestrichen, so ist diese Bedingung irrelevant f�r
die R�ckgabe und wird beim Aktualisieren entfernt.
:::

Die Formatierung des Wertes der Variable hat Einfluss darauf, wie der Wert vom System interpretiert wird.
Grundlegend gibt es in Excel 3 Datentypen.

- Text
:   Text werden als Zeichenkette weitergegeben.

- Zahl
:   Bei Zahlen wird grundlegend in Gleitkomma und Ganzzahlen unterschieden. 
    Gleitkommazahlen werden als Dezimalwert weitergegeben. Bei Ganzzahlen werden
    als 64-Bit Integer weitegegeben. Existiert ein Format mit einer `0+` Schablone 
    so wird angenommen, dass es sich im einen Key handelt mit signifikanten f�hrenden
    Nullen.

- Datum
:   Daten werden immer in von-bis Bereiche aufgespalten. Welche Bereiche verwendet
    werden sollen, wird anhand des Formates ermittelt. Enth�lt das Format
    - `JJJJ` wird das Jahr verwendet
    - `MMM` oder `MM`, aktiviert den Monat
    - `dd` setzt den Tag

- Auflistungen
  :   Benannte Felder (Namensmanager) k�nnen auch Bereiche umfassen. Einem Bereich wird 
    die erste Spalte in ein Array umgewandelt, welches f�r die Variable eingesetzt wird.

:::tipp
Bei der Eingabe von '#', '$' oder ':' erscheint ein Popup-Fenster, das Ihnen damit hilft, den Wert des Operanden auszuw�hlen. 
'#' f�r Zeit-Kalender, '$' f�r eine Liste anderer Feldnamen und ':' f�r eine Liste mit allen vordefinierten Zellen im Excel-Arbeitsblatt.
Die gleiche Funktionalit�t kann durch Klicken auf die entsprechende Schaltfl�che ausgef�hrt werden.
:::
