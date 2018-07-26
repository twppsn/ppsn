---
uid: ppsn.mod.troubleshooting
title: PPSnMod Fehlerbehebung
---

## Fehler in der Datenbank

1. Login am Server fehlt
   ```sql
   ALTER LOGIN [Domain\User] WITH DEFAULT_DATABASE=[DB_NAME], DEFAULT_LANGUAGE=[Deutsch];
   CREATE USER [Domain\User] FOR LOGIN [Domain\User]
   ```
1. Datenbank ist nicht verfügbar
   ```bash
   Get-Service | Where-Object {$_.Name -Match 'SQL'}
   ```

2. Fehlende Rechte an Objekten
   ```sql
   GRANT SELECT, INSERT, UPDATE, DELETE ON [Schema].[Objekt] TO public;
   ```
   falls Change_Tracking aktiviert wurde zusätzlich:
   ```sql
   GRANT VIEW CHANGE TRACKING ON [Schema].[Objekt] TO public;
   ```

## Fehler in der Verbindung

```bash
([System.Net.WebRequest]::Create('http://[IP des DE-Servers]/ppsn/info.xml')).GetResponse().StatusCode
```

Die Antwort sollte <i>OK</i> lauten.