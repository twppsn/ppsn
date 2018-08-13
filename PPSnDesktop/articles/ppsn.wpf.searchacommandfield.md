---
uid: ppsn.wpf.searchacommandfield
title: Das Such- und Befehlsfeld
---

# Das Such- und Befehlsfeld

Rechts über dem Browser befindet sich ein Eingabefeld. Mit diesem können zum Einen die Einträge des Browsers gefiltert, zum anderen aber auch Befehle oder kleine Scripte ausgeführt werden.

## Suchfunktionen

ToDo

## Befehle

Handelt es sich um eine Abfrage, ershceint das Ergebnis in einer MessageBox

### Name des Angemeldeten Benutzers

```lua

.:=Environment.UserName

```

### Development

.:=msgbox(MasterData.Land)

.:=msgbox(dbo.Geschlecht[0])

.:=msgbox(#dbo.Land)

.:=msgbox(GetObject("Deutschland"))

.:=foreach col in MasterData.Land.Columns do msgbox(col) end

foreach col in MasterData.Land.Columns do Environment.Traces.AppendText(nil,col) end

return table.ToLson(dbo["Geschlecht"])

return GetObject(1)["Name"]

.:=msgbox(App)