---
uid: ppsn.mod.installation
title: Installation of PPSnMod for Production
---

# Installation des PPSn Servers

## I. Installation des DE-Servers

[!include[includetest3](~/des/articles/usersmanual/des.installationforppsn.md)]

## II. [Speedata](https://www.speedata.de) einrichten

1. PowerShell in <i>[Arbeitsverzeichnis]\\speedata</i> starten
   ```bash
   update.ps1
   ```

## III. Datenbankkonfiguration

Es sollte MS SQL Server 2016 (oder höher) Standart Edition oder höher installiert sein.

1. Filestream für E/A-Dateizugriff aktivieren <span style="color:red">ToDo: bebildern/Weg zum Menü aufzeigen/SQL-Befehl anführen/alle drei Optionen beschreiben</span>
2. Abfrage ausführen
    ```sql
    EXEC sp_configure filestream_access_level, 2;
    RECONFIGURE;
    ```
3. Datenbankpfad für Binärdateien einstellen <span style="color:red">ToDo: Bild</span>
4. Datenbankpfad für Backups einstellen <span style="color:red">ToDo: Bild</span>
5. Server neu starten

## IV. PPSn Datenbank einrichten

1. Das Datenbankschema importieren.
   ```sql
   sqlcmd -S [SERVERNAME]\[INSTANCE_NAME] -i \PPSnMaster.sql -o [Arbeitsverzeichnis]\Log\database_import_log.txt
   ```
2. Nutzer des PPSn anlegen <span style="color:red">ToDo: SQL-Befehle/Admin-Tool</span>

## V. PPSn-Server konfigurieren

In der Datei <i>[Arbeitsverzeichnis]\\Cfg\\PPSn.xml</i> folgene Anpassungen vornehmen:

* Die Eigenschaft <b>des-var-webBinding</b> muss entsprechend der Gegebenheiten angepasst werden. bei <b>localhost</b> ist der Server nur lokal erreichbar. Dies is ausreichend, sofern der Server nur über einen lokalen Proxy oder über einen Tunnel erreichbar sein soll.  
    Normalerweise ist hier die IP-Adresse oder der Hostname (FQDN) anzugeben, um den Server im Netzwerk erreichbart zu machen - dies, als auch ein Port kleiner 1024, erfordert einen Start des DE-Servers mit erhöhten Rechten!
* <b>server logpath=</b> sollte auf <i>[Arbeitsverzeichnis]\\Log</i> verweisen
* Reporting Pfad <span style="color:red">ToDo: wo wird der gesetzt?</span>
* Die Eigenschaft <b>pps:connectionString</b> muss entsprechend der eingerichteten Datenbank gesetzt werden

## Weitere Schritte

* @ppsn.wpf.installation
* @des.troubleshooting 
* @ppsn.mod.troubleshooting
