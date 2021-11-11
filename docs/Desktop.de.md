# Bde

## Installation

```
msiexec /i http://host:port/ppsn/app/PPSnDesktop.msi SHELLNAME=name
```

Optional `SHELLURI`

`http://localhost:8080/ppsn/?id=*`
`http://localhost:8080/ppsn/?id=*.ID`
`http://localhost:8080/ppsn/?id=ID`

## LiveData

Ausgabe des Status:

```Lua
DumpLiveData("<filename>")
```

Wird kein Dateiname angegeben, wird nach einem gefragt.

## Lock-Modus

Einige Administrative Befehle sind durch eine PIN gesichert oder versteckt.

Die Befehlen können aktiviert werden durch:

```Lua
Unlock("<pin>");
```

Bzw. gesperrt:
```Lua
Lock();
```

Das Entsperren kann auch über einen Barcode erledigt werden: `DPC.UnlockCode`

## Shell Modus

- Shell Modus ersetzt die Windows-Shell (Explorer) durch PPSnDesktop-Anwendung.
- Es wird ein Schutzprozess gestartet der die Anwendung neustartet, wenn Sie abstürzt.
- Minimieren,Maximieren wird ausgeblendet
- Beenden der Anwendung mittels Alt+F4 oder `Quit()` bewirkt ein abmelden des Nutzers

Aktiviert wird er Shell-Modus mittels des Befehls:

```Lua
SetAsShell(<PIN>);
```

Deaktivierung via:
```Lua
RemoveAsShell(<PIN>);
```

## Ausführen/Befehle

Anwendung aus dem Desktop Client heraus starten.

```Lua
Exec("<cmd>")
```

`cmd` sollte durch die Anwendung ersetzt werden.
- Wird `nil` angegeben, wird die `cmd.exe` gestartet.
- `settings` startet die Windows-Einstellungen
- `rdbg` versucht den Visual Studio Remote Debugger zu starten.

Beenden von Windows:
```Lua
ExecShutdown(<pin>)
```

Neustarten von Windows:
```Lua
ExecRestart(<pin>)
```

## Themes

```Lua
SetTheme("<name>")
```

## Virtual Keyboard (nur BDE-Modus)

Im Shell-Modus wird die Windows-Bildschirmtastatur nicht vollständig initialisiert.

Als erstes sollte diese deswegen deaktiviert werden.
```
Geräte > Eingabe > ??? muss deaktivert werden
todo: bild einfügen, gibt es nur bei TouchScreen
```

Die Tastatutur sollte sich automatisch aktivieren, wenn sich die Anwendung im Shell-Modus befinden.

Aktivieren der BDE-Variante über die Einstellung `PPSn.Application.TouchKeyboard` = `true` ist auch möglich.