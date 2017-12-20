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

## Define report



## Execute report

* Run report
```cmd
	:use /ppsn
	local fileName = RunReport{name="Report"}
```
* debug report with parameter
```cmd
	DebugReport{name="Report",ParamName1="Wert"}
```
* debug data stream
```cmd
	:use /ppsn
	DebugData{name="Report"}
```

