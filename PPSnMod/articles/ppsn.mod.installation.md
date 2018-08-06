---
uid: ppsn.mod.installation
title: Installation vons PPSnMod
---

# Installation des PPSn Servers

## 0. Vorbedingungen

@des.installation.dc

## I. Installation des DE-Servers

[!include[Installation of the DE-Server](~/des/articles/usersmanual/des.installationforppsn.md)]

## II. [Speedata](https://www.speedata.de) einrichten

```bash
[Arbeitsverzeichnis]\Speedata\update.ps1 [-targetDirectory] [-version]
```

> [!NOTE]
> | Parameter | Beispiel | Bemerkung |
> | --- | --- | --- |
> | targetDirectory | `-targetDirectory [Arbeitsverzeichnis]\Speedata` | wird er weg gelassen, werden die Dateien in das aktuelle Verzeichnis kopiert |
> | version | `-version 3.2.0` | kann weggelassen werden, sofern Version 3.2.0 (default) installiert werden soll |

## III. PPSn Datenbank einrichten

Es sollte MS SQL Server 2016 (oder höher) Standart Edition oder höher installiert sein.

> [!TIP]
> [Beispielhafte Installation](<xref:des.installation.dc#einrichtung-des-mssql-servers-2016>)

1. Das Datenbankschema importieren.
   ```bash
   sqlcmd -S [Servername]\[Instanzname] -i [Arbeitsverzeichnis]\Temp\PPSnMaster.publish.sql -o [Arbeitsverzeichnis]\Log\database_import_log.txt
   # Ausgabe überprüfen
   cat [Arbeitsverzeichnis]\Log\database_import_log.txt
   ```

   > [!NOTE]
   > | Variable | Beispiel |
   > | --- | --- |
   > | [Arbeitsverzeichnis] | `C:\DEServer` |
   > | [Instanzname] | `PPSnDatabase` |
   > | [Servername] | `localhost` |

1. Der Nutzer unter dem der DE-Server gestartet wird, muss Zugriff auf die Datenbank erhalten
   ```bash
   Invoke-Sqlcmd -ServerInstance "[Servername]\[Instanzname]" -Query "CREATE LOGIN [[DES-Benutzername]] FROM WINDOWS"
   Invoke-Sqlcmd -ServerInstance "[Servername]\[Instanzname]" -Query "exec sp_addsrvrolemember @loginame='[DES-Benutzername]', @rolename=sysadmin"
   ```

   > [!NOTE]
   > | Variable | Beispiel |
   > | --- | --- |
   > | [Arbeitsverzeichnis] | `C:\DEServer` |
   > | [DES-Benutzername] | `ppsn\PPSnServiceUser$` |
   > | [Instanzname] | `PPSnDatabase` |
   > | [Servername] | `localhost` |

1. <i>Development use only</i> Testnutzer des PPSn anlegen
   ```bash
   Invoke-Sqlcmd -ServerInstance "localhost\PPSnDatabase" -Query "USE [Datenbankname];
    DECLARE @objkId BIGINT;
    DECLARE @ktktId BIGINT;
    DECLARE @nr NVARCHAR(20);
    SET @nr = 'P000001';
	INSERT INTO dbo.ObjK ([Guid], [Typ], [MimeType], [Nr], [IsRev]) VALUES (NEWID(), 'crmContacts', 'text/dataset', @nr, 0);
	SET @objkId = @@IDENTITY;
	INSERT INTO [dbo].[Ktkt] ([ObjkId], [ParentId], [Name], [KurzName]) VALUES (@objkId, NULL,'Max Mustermann','mm');
	SET @ktktId = @@IDENTITY;
	INSERT INTO [dbo].[User] ([Login], [Security], [LoginVersion], [KtktId]) VALUES ('[Benutzername]', 'desSys', 0, @ktktId);
	INSERT INTO [dbo].[Pers] ([KtktId], [Kurz]) VALUES (@ktktId, 'mm');
	INSERT INTO dbo.ObjT ([ObjkId], [ObjRId], [Class], [UserId], [Key], [Value]) VALUES (@objkId, null, 0, 0, 'Name', 'Max Mustermann');"
   ```

   > [!NOTE]
   > | Variable | Beispiel |
   > | --- | --- |
   > | [Datenbankname] | `PPSn1` |
   > | [Benutzername] | `ppsn\Administrtator` |
   > | [Instanzname] | `PPSnDatabase` |
   > | [Servername] | `localhost` |

## IV. PPSn-Server konfigurieren

In der Datei <i>[Arbeitsverzeichnis]\\Cfg\\PPSn.xml</i> folgene Anpassungen vornehmen:

| Variable | Beispiel | Bemerkung |
| --- | --- | --- |
| des-var-webBinding | `http://+:80` | Die Eigenschaft muss entsprechend der Gegebenheiten angepasst werden. Bei <b>localhost</b> ist der Server nur lokal erreichbar. Dies is ausreichend, sofern der Server nur über einen lokalen Proxy oder über einen Tunnel erreichbar sein soll. Bei <b>+</b> ist er von aussen erreichbar. |
| des-var-reportSystem | `C:\DEServer\Speedata\bin` | zeigt auf das Verzeichnis der sp.exe <b>oder die Exe?</b> |
| server logpath= | `..\Log` | in diesem Verzeichnis werden die Log-Dateien angelegt. |
| pps:connectionString | `Data Source=localhost\PPSnDatabase; Integrated Security=True; Persist Security Info=False; Pooling=False; MultipleActiveResultSets=False ;Connect Timeout=60; Encrypt=False; TrustServerCertificate=True; Initial Catalog=PPSn` | Data Source muss auf [Hostnamen]\ [Benannte Instanz der Datenbank] zeigen, Initial Catalog auf die angelegte Datenbank. |

> [!IMPORTANT]
> Wird der Hostname auf <b>+</b> gesetzt benötigt der DE-Server einen Eintrag in der URLACL.

> [!IMPORTANT]
> Wird der Port auf einen Wert kleiner <b>1024</b> gesetzt benötigt der DE-Server einen Eintrag in der URLACL.

> [!IMPORTANT]
> Der Benutzer unter dem der DE-Server gestartet wird, benötigt Schreibrechte auf das Log-Verzeichnis.

## Weitere Schritte

* @ppsn.wpf.installation
* @des.troubleshooting 
* @ppsn.mod.troubleshooting