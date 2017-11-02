# Report

## Voraussetzungen

* Einbindung Reportsystem in Config (z.B. PPSn.xml)
```xml
	<?des-var-reportSystem C:\Projects\PPSnOS\twppsn\PPSnCore\PPSnReport\system?>
```
* Definition im Modul (z.B. System.xml)
```xml
	<pps:reports system="$(reportSystem)" base="reports" storeSuccessLogs="true" zipLogFiles="false" />
```

## View definieren

* View in SQL schreiben -> unter Views ablegen
```sql
	CREATE VIEW ...
```
* View in XML registrieren
```xml
	<pps:view name="bms.TabuKopf" displayName="Kopfdaten">
		<pps:source type="view">bms.TabuKopf</pps:source>
	</pps:view>
```

## Report definieren

* Tex-Datei anlegen und bearbeiten
* Daten abrufen
```tex
	\startluacode
		package.path = package.path .. ";C:\\Projects\\PPSnOS\\twppsn\\PPSnMod\\PPSnCoreCfg\\reports\\?.lua";

		PpsData = PpsData or require "libs/PpsData";

		local data = PpsData;

		daten = data.loadList { select = "dbo.ViewName", columns = { "Name", "Anmerk", "Von", "Bis" } };
	\stopluacode
```
* Daten filtern / Paramter übergeben
```tex
	\startluacode
		local id = environment.getargument("BtkoId") or error("Gibts nicht!");
		daten = data.loadList { select = "dbo.ViewName", columns = { "Name", "Anmerk", "Von", "Bis" }, filter = "Id:="..id };
	\stopluacode
```
* Daten sortieren
```tex
	\startluacode
		daten = data.loadList { select = "dbo.ViewName", columns = { "Name", "Anmerk", "Von", "Bis" }, order = "+Name,-Nr" };
	\stopluacode
```

## Report debuggen

* in der Console 
```cmd
	:use /ppsn
	DebugReport{name="Report"}
```
* Report mit Parameter
```cmd
	DebugReport{name="Report",ParamName1="Wert"}
```