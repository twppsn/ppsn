# Installation of the Debug Environment
## Pull the Repository

```bash

mkdir PPSn
cd PPSn
git clone --recursive https://github.com/twppsn/ppsn.git
# or
git clone --recursive --branch development https://github.com/twppsn/ppsn.git

```

## Pull the [Speedata Publisher]:https://www.speedata.de/ as Reporting Engine

```bash

.\\ppsn\\PPSnReport\\system\\update.ps1 -version x.x.xx

```

where '-version x.x.xx' is optional (default is 3.1.16)

## Setup of the Database

1. open PPSnMod.sln
1. right-click PPSnMaster->Publish
1. Target database connection -> edit
1. ->browse
1. ->A Microsoft SQL Server 2016 (or creater) Instance
1. Authentification->Windows Authentification
1. Database Name->PPSnTestDatabase
1. ->OK
1. ->Publish

### Create User

1. Visual Studio->Tools->SQL Server->New Query
1. ->A Microsoft SQL Server 2016 (or creater) Instance
1. Database Name->PPSnTestDatabase
1. ->Connect

```sql

INSERT INTO dbo.[User] ([Login], [Security]) VALUES ('domain\username', 'desSys;Chef');
-- if not happened automaticly by the PostScript.sql, the system user has to be manually created:
SET IDENTITY_INSERT [dbo].[User] ON
INSERT INTO [dbo].[User] ([Id], [Login], [Security], [LoginVersion]) VALUES (0, N'System', NULL, 0);
SET IDENTITY_INSERT [dbo].[User] OFF

```

## Setup of Server

### Setup ConfigFile

```bash

cp ppsn/PPSnModCfg/cfg/PPSn.template ppsn/PPSnModCfg/cfg/PPSn.xml
nano ppsn/PPSnModCfg/cfg/PPSn.xml

```

1. ->change in <pps:connectionString> the Initial Catalog from ??????? to PPSnTestDatabase
1. exit

### Build Solution

1. Visual Studio->Solution->Rebuild Solution

### Setup Project Properties

1. Visual Studio->PPSnModCfg->Set as StartUp Project
1. Visual Studio->PPSnModCfg->Properties
1. Debug->Start external Program->Browse->PPSnModCfg\bin\Debug\DEServer.exe
1. Command line arguments->run -v -c cfg\PPSn.xml

### Start the Server

1. F5

## Setup of Client

1. open PPSnWpf.sln
1. Visual Studio->PPSnDesktop->Set as StartUp Project
1. F5

## Connect to Dev-Server

1. Ctrl+N
1. Mandant->PPSnTestDatabase [or any other name]
1. URI->http://localhost:8080/ppsn
1. ->Anlegen
1. Nutzer->[domain]\[username]
1. ->Anmelden