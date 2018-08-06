---
uid: ppsn.wpf.commandline
title: Startparameter
---

## Mögliche Startparameter von PPSnWpf

| Parameter | Beispiel | Bedeutung
| --- | --- |
| a | `-a"TestunternehmenPPSn"` | Es wird versucht den zuletzt angemeldeten Benutzer unter dem angegebenen Mandanten anzumelden. Dazu müssen die Anmeldeinformationen im Windows Anmeldeinformationsspeicher angelegt sein. |
| u | `-u"Testnutzer"`; `-u"domaene\Nutzer"` | Benutzername für eine Anmeldung ohne Anmeldeinformationsspeicher. Nur in Verbindung mit `-p` |
| p | `-p"Passwort123"` | Kennwort für eine Anmeldung ohne Anmeldeinformationsspeicher. Nur in Verbindung mit `-u` |