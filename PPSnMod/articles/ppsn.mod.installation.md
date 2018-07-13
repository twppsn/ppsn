---
uid: ppsn.mod.installation
title: Installation of PPSnMod for Production
---

# Installation des PPSn Servers

## I. Vorarbeiten

1. Es sollte ein neuer Benutzer mit <span style="color:red">ToDo: erforderliche Rechte herausfinden</span> eingerichtet werden.  
   Dazu Eingabeaufforderung mit erhöhten Rechten starten.
   ```bash
   net user /add [DES-Benutzername] [DES-Passwort]
   net user [DES-Benutzername] /passwordchg:no
   net user [DES-Benutzername] /expires:never
   net localgroup Administratoren [DES-Benutzername] /add
   ```
   <span style="color:red">oder Managed Service Account?</span>

## II. Dateisystem vorbereiten

1. Ein neues Verzeichnis anlegen, bspw: <i>C:\DE-Server</i> ->Arbeitsverzeichnis
2. folgende Unterordner anlegen:
    * <i>Backup</i> -> für Backups des PPSn Systemes
    * <i>Bin</i> -> Binärdateien des DE-Servers (alle Dateien aus <i>PPSnOS\\ppsn\\PPSnMod\\bin\\[Release/Debug]</i>)
    * <i>Cfg</i> -> Konfigurationsdateien des DE-Servers (alle Dateien aus <i>PPSnOS\\ppsn\\PPSnModCfg\\cfg</i>)
    * <i>Client</i> -> Clientanwendung (alle Dateien aus <i>PPSnOS\\ppsn\\PPSnDesktop\\bin\\[Release/Debug]</i>)
    * <i>Data</i> -> Datenbankverzeichnis
    * <i>Log</i> -> Logdateien des Servers
    * <i>speedata</i> -> Verzeichnis für das Reportingsystem (<i>update.ps1</i> aus <i>PPSnOS\\ppsn\\PPSnReport\\system</i>)
    * <i>Temp</i> -> Temporäre Daten

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

1. <span style="color:red">ToDo: welche Datei ist wie auszuführen</span>
2. Nutzer des PPSn anlegen <span style="color:red">ToDo: SQL-Befehle/Admin-Tool</span>

## V. Zertifikat

1. <span style="color:red">ToDo: ???</span>

## VI. PPSn-Server konfigurieren

In der Datei <i>[Arbeitsverzeichnis]\\Cfg\\PPSn.xml</i> folgene Anpassungen vornehmen:

* Die Eigenschaft <b>des-var-webBinding</b> muss entsprechend der Gegebenheiten angepasst werden. bei <b>localhost</b> ist der Server nur lokal erreichbar. Dies is ausreichend, sofern der Server nur über einen lokalen Proxy oder über einen Tunnel erreichbar sein soll.  
    Normalerweise ist hier die IP-Adresse oder der Hostname (FQDN) anzugeben, um den Server im Netzwerk erreichbart zu machen - dies, als auch ein Port kleiner 1024, erfordert einen Start des DE-Servers mit erhöhten Rechten!
* <b>server logpath=</b> sollte auf <i>[Arbeitsverzeichnis]\\Log</i> verweisen
* Reporting Pfad <span style="color:red">ToDo: wo wird der gesetzt?</span>
* Die Eigenschaft <b>pps:connectionString</b> muss entsprechend der eingerichteten Datenbank gesetzt werden

## VII. DE-Server als Dienst einrichten

1. Eingabeaufforderung mit erhöhten Rechten starten:
   ```bash
   #Service registrieren
   [Arbeitsverzeichnis]\\Bin\\DEServer.exe register --config [Arbeitsverzeichnis]\\Cfg\\[Config].xml --name [Unternehmensname]
   #Benutzer für den Service festlegen
   sc.exe config "Tw_DES_[Unternehmensname]" obj= ".\[DES-Benutzername]" password= "[DES-Passwort]"
   #URL für DE-Server freigeben
   netsh http add urlacl url=http://+:80/ppsn user=[DES-Benutzername] listen=yes
   #Ausnahme in der Firewall erstellen
   netsh advfirewall firewall add rule name="DE-Server" dir=in protocol=TCP localport=80 action=allow
   #oder
   netsh advfirewall firewall add rule name="DE-Server" dir=in program="[Arbeitsverzeichnis]\\Bin\\DE-Server.exe" action=allow
   #DE-Server als Service starten
   net start Tw_DES_[Unternehmensname]
   ```
2. Gegebenenfalls müssen in übergeordneten Routern Exposed Hosts/Portweiterleitungen eingerichtet werden, um den DE-Server aus weiteren Netzsegmenten erreichen zu können

## IIX. [Speedata](https://www.speedata.de) einrichten

1. PowerShell in <i>[Arbeitsverzeichnis]\\speedata</i> starten
   ```bash
   update.ps1
   ```

## Weitere Schritte

* @ppsn.wpf.installation
* @des.troubleshooting 
* @ppsn.mod.troubleshooting
