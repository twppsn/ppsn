# Nutzer ppsn

Die Nutzer innerhalb des PPSn werden in der Hauptdatenbank verwaltet.

In dieser Datenbank muss die Tabelle `dbo.User` existieren, 
in der alle Nutzer gelistet werden. Die Authentifizerung erfolgt ebenfalls
gegen die Hauptdatenbank.

Es gibt zwei Nutzertypen, Datenbank-Nutzer und Domain-Nutzer.

## Anzeige der aktuellen Nutzer

### Datenbank

Der Dienst verarbeitet intern die Rückgabe von `dbo.serverlogins`.

:::warn
Änderungen an einem Nutzer muss die Spalte `LoginVersion` erhöhen.
:::

### SimpleDebug

Die aktuellen Nutzer werden in der DE-Liste `tw_users` verwaltet.

Auflistung der Nutzer:
```
:use /ppsn
:listget tw_users
```

Aktualisierung kann durch `RefreshUsers` angeschoben werden.
