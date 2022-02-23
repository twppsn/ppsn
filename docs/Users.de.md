# Nutzer ppsn

ppsn erweitert das Nutzermodell um diverse Datenbanknutzer (NTML,BASIC).

Der Dienst verarbeitet intern die Rückgabe von `dbo.serverlogins`.

:::warn
Änderungen an einem Nutzer muss die Spalte `LoginVersion` erhöhen.
:::

Es wird zusätz ein System-Nutzer eingeführt, der den Dienstkontext repräsentiert.