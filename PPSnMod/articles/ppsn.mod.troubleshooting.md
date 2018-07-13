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
2. Fehlende Rechte an Objekten
   ```sql
   GRANT SELECT, INSERT, UPDATE, DELETE ON [Schema].[Objekt] TO public;
   ```
   falls Change_Tracking aktiviert wurde zusätzlich:
   ```sql
   GRANT VIEW CHANGE TRACKING ON [Schema].[Objekt] TO public;
   ```