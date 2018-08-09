---
uid: ppsn.mod.programming
title: PPSn Beispielprogrammierung
---

## Beispiele für die dynamische Erzeugung von Inhalten durch den DE-Server in Verbindung mit einer PPSn-Datenbank

### Einführung

> [!IMPORTANT]
> Eibnführung schreiben

> [!WARNING]
> Heutige Browser rufen Webseiten teilweise mehrfach auf (aufgrund von Prefetch und anderen Techniken). Dabei ist sicherzustellen, das die gewünschten Funktionen problemlos doppelt ausgeführt werden können (zum Beispiel, wenn der Ressourcenverbrauch einer Datenbankabfrage vernachlässigbar ist). Ansonsten ist dies durch ein Transaktionsmodell sicherzustellen.

> [!NOTE]
> Diese Beispiele beziehen sich auf die erweiterten Funktionalitäten des PPSn-Systemes. Die grundlegenden Programmierfunktionalitäten des DE-Servers werden [ hier](xref:des.firststeps.programming) beschrieben.

### HTML-Ausgabe des Kontaktstammes

```lua

otext('text/html')
print("<html><head></head><body>");
print("Contacts");
UseNode(
  "/ppsn/crmContacts",
  function(node)
    foreach con in node.Objects:GetObjects() do
      if con.Typ == "crmContacts" then
        print("<table border='1'>");
        local ds, obj = node.Pull(con.Id);
        print("<tr><td colspan='2'>" .. ds.Head[]["Name"]  .. "</td></tr>");
        foreach it in ds.Head.TableDefinition.Columns do
          if ds.Head[][it.Name] ~= nil and ds.Head[][it.Name] ~= '' then
            print("<tr><td>" .. it.Name .. "</td><td>" .. ds.Head[][it.Name] .. "</td></tr>");
          end;
        end;
        if #con.Tags > 0 then
          print("<tr><td rowspan='" .. #con.Tags+1 .. "'>Notes</td></tr>");
          foreach tg in con.Tags do
            if tg.TagClass == 256 then
              print("<tr><td>" .. tg.Value .. "</td></tr>");
            end;
          end;
        end;
        print("</table>");
      end;
    end;
  end
);
print("</body></html>");

```