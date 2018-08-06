---
uid: ppsn.wpf.troubleshooting
title: PPSnWpf Fehlerbehebung
---

## Fehlerbehebung für den PPSnWpf-Client

### Fehler bei oder kurz nach der Installation

#### <i>"Es steht keine Offline-Version bereit. Online Synchronisation jetzt starten?"</i>

* Der Client kann keine Verbindung zum Server herstellen.
  > [!TIP]
  > 1. Überprüfen sie, ob der Client mit dem Netzwerk verbunden ist.
  > 2. [Überprüfen Sie, ob der Server korrekt arbeitet](<xref:ppsn.mod.troubleshooting>)

#### <i>"Die zugrunde liegende Verbindung wurde geschlossen: Für den geschützten SSL\TLS-Kanal konnte keine Vertrauensstellung hergestellt werden"</i>

* Es wurde <i>https</i> als Protokoll gewählt
* Der Client vertraut dem Server nicht
   * Die Uhrzeit des Clients unterscheidet sich zu stark von der des Servers
     > [!TIP]
     > Im Unternehmen sollte ein Zeitserver (NTP-Server) eingerichtet werden, mit dem sich alle Clients synchronisieren.
   * Der Client kann die Zertifikatskette nicht auflösen
     > [!TIP]
     > Handelt es sich um ein öffentliches Zertifikat benötigt der Client möglicherweise eine Internetverbindung um die Authentizität verifizieren zu können.
   * Der Server verwendet ein selbst erstelltes Zertifikat
     > [!TIP]
     > Das Zertifikat des Servers muss bei jedem Client als "Vertrauenswürdige Stammzertifizierungsstelle" eingerichtet werden.