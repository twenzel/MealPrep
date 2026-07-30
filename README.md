# Mahlzeit

Eine für das iPhone optimierte, selbst gehostete Essensplanung unter .NET 10.

## Enthalten

- Wochenplan mit getrennten Slots für Mittag- und Abendessen
- Automatische Vorschläge mit getrennt einstellbarem Wochenumfang
- Gespeicherte Haushalts-, Ernährungs- und Kochzeiteinstellungen
- Rezeptverwaltung mit Zutaten und Kochschritten
- Dauerhafte Rezeptfavoriten mit Filter und bevorzugter Auswahl
- Instagram-Import aus öffentlichen Post-/Reel-Links oder kopierten Bildunterschriften
- Rezeptbilder als Binärdaten direkt in PostgreSQL
- Automatisch aggregierte und dauerhaft abhakbare Einkaufsliste
- Schrittweiser Kochmodus mit optionaler Bildschirm-Wachhaltung
- Anmeldung mit ASP.NET Core Identity und Passkey-Unterstützung
- Installierbare PWA für den iPhone-Home-Bildschirm
- Docker-Compose-Konfiguration für Synology Container Manager

Automatische Vorschläge berücksichtigen Ernährungsform, Allergien, ausgeschlossene Zutaten, bevorzugte Rezept-Tags, Standardportionen sowie getrennte Zeitlimits für Werktage und Wochenende.

Beim Instagram-Import entsteht immer zuerst ein bearbeitbarer Entwurf. Falls Instagram die öffentliche Bildunterschrift nicht ausliefert, kann der Text direkt aus der Instagram-App kopiert und eingefügt werden. Ein Instagram-Login oder Zugriffstoken ist nicht erforderlich; Bilder werden nur gespeichert, wenn sie selbst hochgeladen werden.

![Screenshot](./preview.png "preview")

## Lokal mit Docker starten

1. In `.env` ein langes, zufälliges Datenbankpasswort setzen.
2. `docker compose up -d` ausführen.
3. `http://localhost:8088` öffnen und den ersten Zugang registrieren.

Die Datenbankmigrationen und die Beispieldaten werden beim ersten Start automatisch angelegt.

## Installation auf einer Synology

Voraussetzung ist ein Modell, das Synology Container Manager unterstützt.

1. Diesen Projektordner in einen gemeinsamen Ordner auf der NAS kopieren.
2. Eine `.env` mit einem sicheren `POSTGRES_PASSWORD` anlegen.
3. In Container Manager ein neues Projekt aus `compose.yaml` erstellen.
4. Nach dem Start intern Port `8088` aufrufen.
5. Im DSM-Reverse-Proxy eine HTTPS-Adresse auf `http://127.0.0.1:8088` weiterleiten.
6. Die fertige HTTPS-Adresse in Safari öffnen und über **Teilen → Zum Home-Bildschirm** installieren.

Der PostgreSQL-Port wird nicht nach außen veröffentlicht. Anmeldeschlüssel bleiben über das Volume `mealprep-keys` auch bei Container-Neustarts erhalten.

## Sicherung

`scripts/backup.sh` erzeugt einen komprimierten, konsistenten PostgreSQL-Dump. Der Ausgabeordner sollte zusätzlich mit Synology Hyper Backup gesichert werden.

Beispiel auf der NAS:

```sh
sh scripts/backup.sh /volume1/docker/mealprep /volume1/backups/mealprep
```

## Entwicklung ohne Docker

Eine lokale PostgreSQL-Datenbank mit folgenden Entwicklungsdaten bereitstellen:

```text
Host=localhost
Database=mealprep
Username=mealprep
Password=mealprep_dev
```

Danach:

```sh
dotnet run --project src/MealPrep.App
```

## Tests

```sh
dotnet test MealPrep.slnx
```
