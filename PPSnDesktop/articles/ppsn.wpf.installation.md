---
uid: ppsn.wpf.installation
title: Installation von PPSnWpf
---

## Einrichtung des Clients

1. Client starten (`PPSnDesktop.exe`)
1. Da beim ersten Start kein Mandant angelegt ist, muss dieser angelegt werden, dazu:
   1. <b>Strg</b>+<b>N</b> drücken - es öffnet sich die Maske für einen neuen Mandanten
   1. In das Feld <i>Mandant</i> kann ein belibiger Name für die Umgebung eingegeben werden
   1. In das Feld URI muss die Anwendungsadresse des Servers angegeben werden
   1. Mit Anlegen wird der Mandant eingerichtet
1. Die Felder Nutzer und Passwort ausfüllen
1. Der zuletzt gewählte Benutzer wird generell bei erneutem Start angezeigt - das Kennwort nur bei Klick auf <i>Kennwort speichern</i>
1. Mit <i>Anmelden</i> startet die Anwendung

> [!TIP]
> Entsprechend der Einstellung des Administrators werden freie Accounts, Domänenaccounts oder eine Mischung aus diesen erlaubt.
> Bei Domänenaccounts bitte den Benutzernamen im Format <i>Domäne\Benutzername</i> (bspw. `tecware\Mustermann`) eingeben.

> [!TIP]
> Verwendet der Nutzer den gleichen Domänenaccount für Windows und PPSn, so ist die Eingabe eines Passwortes nicht nötig.

> [!NOTE]
> Eine URI sollte folgendermaßen aufgebaut sein:  
> `http[s]://<IP/Hostname>[:<Port>]/<Anwendungsname>`  
> Beispiele `http://192.168.169.170/ppsn`, `https://server.intranet/produktionsplanung`, `http://planungsserver:8080/ppsn`

## Weitere Schritte

* @ppsn.wpf.commandline
* @ppsn.wpf.troubleshooting
* @ppsn.mod.troubleshooting 