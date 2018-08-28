---
uid: ppsn.mod.programming
title: PPSn Beispielprogrammierung
---

# Einführung

Der DE-Server bietet in Verbindung mit PPSn eine große Vielfalt von Möglichkeiten, Services bereit zu stellen. Gerade durch den direkten Zugriff auf Datenstrukturen von PPSn wird eine hohe Effizienz erreicht.  
Auf den [Beispielen des DE-Servers](xref:des.firststeps.programming) aufbauend werden hier exemplarisch die erweiterten Funktionalitäten beschrieben.

## Beispiele für angeheftete Aktionen

### Beispiel 0 - Ausgabe der Kontakte nach Postleitzahl

#### Anfügen des Scriptes an den Knoten PPSn

Um einen Knoten mit Funktionen zu erweitern wird zuerst die Konfigurationsdatei `PPSn.xml` bearbeitet:

1. zuerst das Script der LuaEngine hinzufügenn:
   ```xml
   ...
   <luaengine>
     ...
     <script id="contactFunctions" filename="ctc\contactfunctions.lua"/>
     ...
   </luaengine>
   ...
   ```

1. als nächstes das Script an den Knoten anhängen
   ```xml
   ...
   <pps:ppsn name="ppsn" mainDataSource="main" displayname="PPSN Demo" script="contactFunctions">
   ...
   ```

#### Erstellen der Scriptfnktionalität

Dazu im Verzeichnis der o.g. `PPSn.xml` die folgende `ctc\contactfunctions.lua` anlegen:

[!code[Main](ctc/contactfunctions.lua)]


### Beispiel 1 - Umlagern eines Objektes

Das Umlagern wird hier in vier Modulen abgebildet:

1. [Ausgangsmaske, zur Wahl der gewünschten Funktion](#umlagern-ausgangsmaske)
1. [Eingabemaske der Funktion, welche die Eingabe von Paramtern ermöglicht und ggf. beschreibt](#umlagern-eingabemaske)
1. [Zugrundeliegende Serverfunktionalität](#umlagern-serverfunktionalität)
1. [Ergebnisseite oder Folgeseite, welche über den Ausgang der Operation (Erfolg/Misserfolg) informiert.](#umlagern-ergebnisseite)

Die Erzeugung dieser Module wird im Folgenden erklärt.

* den Unterordner `mde` im Verzeichnis `Cfg` erstellen
* im Unterordner `mde` die Verzeichnisse `data` und `views` erstellen

#### Umlagern, Ausgangsmaske

Hier wird eine Übersichtsmaske erstellt. Diese ist als __Hauptmenü__ zu betrachten.

* im Verzeichnis `views` die Datei `index.html` erstellen:
   [!code[Main](code/index.html)]
* im Verzeichnis `views` die Datei `mde.css` erstellen:
   [!code[Main](code/mde.css)]

#### Umlagern, Eingabemaske

* im Verzeichnis `mde\views` die Datei `umlagern.html` anlegen:
   [!code[Main](code/umlagern.html)]

#### Umlagern, Serverfunktionalität

Um dem Server eine neue Funktionalität hinzuzufügen werden `ACTIONS` verwendet.

Dies erfolgt in folgenden Schritten:
1. Funktion deklarieren
   [!code[Main](code/mde.lua?range=2-57)]
1. Action deklarieren
   [!code[Main](code/mde.lua?range=64-74)]

#### Umlagern, Ergebnisseite


## Beispiele für die dynamische Erzeugung von Inhalten durch den DE-Server in Verbindung mit einer PPSn-Datenbank

> [!WARNING]
> Heutige Browser rufen Webseiten teilweise mehrfach auf (aufgrund von Prefetch und anderen Techniken). Dabei ist sicherzustellen, das die gewünschten Funktionen problemlos doppelt ausgeführt werden können (zum Beispiel, wenn der Ressourcenverbrauch einer Datenbankabfrage vernachlässigbar ist). Ansonsten ist dies durch ein Transaktionsmodell sicherzustellen.

> [!NOTE]
> Diese Beispiele beziehen sich auf die erweiterten Funktionalitäten des PPSn-Systemes. Die grundlegenden Programmierfunktionalitäten des DE-Servers werden [ hier](xref:des.firststeps.programming) beschrieben.

### HTML-Ausgabe des Kontaktstammes

```lua

otext('text/html')
print("<html><head></head><body>");
print("Contacts");
UseNode(
  "/ppsn/crmContacts",
  function(node)
    foreach con in node.Objects:GetObjects() do
      if con.Typ == "crmContacts" then
        print("<table border='1'>");
        local ds, obj = node.Pull(con.Id);
        print("<tr><td colspan='2'>" .. ds.Head[]["Name"]  .. "</td></tr>");
        foreach it in ds.Head.TableDefinition.Columns do
          if ds.Head[][it.Name] ~= nil and ds.Head[][it.Name] ~= '' then
            print("<tr><td>" .. it.Name .. "</td><td>" .. ds.Head[][it.Name] .. "</td></tr>");
          end;
        end;
        if #con.Tags > 0 then
          print("<tr><td rowspan='" .. #con.Tags+1 .. "'>Notes</td></tr>");
          foreach tg in con.Tags do
            if tg.TagClass == 256 then
              print("<tr><td>" .. tg.Value .. "</td></tr>");
            end;
          end;
        end;
        print("</table>");
      end;
    end;
  end
);
print("</body></html>");

```