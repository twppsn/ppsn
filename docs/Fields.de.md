# Felder

Felder beschreiben Rückgabeelement. Es können verschiedene Definition getroffen
werden hinsichtlich der Darstellung (Formatierung, Ausrichtung, ...).

Attribute der Felder können Vererbt werden, damit nicht immer wieder die Definitionen
von neuen erfasst werden müssen.

## Registrierung von Felder

```Xml
<pps:ppsn name="ppsn">
	<pps:register source="cat">
		<pps:field name="dbo.KATA.KATAID" displayName="KatalogId" />
```

