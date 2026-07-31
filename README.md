# Mahlzeit

Eine für das iPhone optimierte, selbst gehostete Essensplanung unter .NET 10.

## Enthalten

- Wochenplan mit getrennten Slots für Mittag- und Abendessen
- Automatische Vorschläge mit getrennt einstellbarem Wochenumfang
- Gespeicherte Haushalts-, Ernährungs- und Kochzeiteinstellungen
- Rezeptverwaltung mit Zutaten und Kochschritten
- Dauerhafte Rezeptfavoriten mit Filter und bevorzugter Auswahl
- Instagram-Import aus öffentlichen Post-/Reel-Links oder kopierten Bildunterschriften
- AI-gestützter Import aus öffentlichen Rezept-Webseiten mit bearbeitbarem Entwurf
- Optionaler Kühlschrank-Check per Foto mit korrigierbarer Erkennung und Rezeptvorschlägen
- Rezeptbilder als Binärdaten direkt in PostgreSQL
- Optional erzeugte Rezeptbilder per AI aus Name und Zutaten
- Automatisch aggregierte und dauerhaft abhakbare Einkaufsliste
- Schrittweiser Kochmodus mit optionaler Bildschirm-Wachhaltung
- Anmeldung mit ASP.NET Core Identity, optional per Passkey oder festem Serverzugang
- Installierbare PWA für den iPhone-Home-Bildschirm
- Docker-Compose-Konfiguration für Synology Container Manager

Automatische Vorschläge berücksichtigen Ernährungsform, Allergien, ausgeschlossene Zutaten, bevorzugte Rezept-Tags, Standardportionen sowie getrennte Zeitlimits für Werktage und Wochenende.

Beim Instagram-Import entsteht immer zuerst ein bearbeitbarer Entwurf. Falls Instagram die öffentliche Bildunterschrift nicht ausliefert, kann der Text direkt aus der Instagram-App kopiert und eingefügt werden. Ein Instagram-Login oder Zugriffstoken ist nicht erforderlich; Bilder werden nur gespeichert, wenn sie selbst hochgeladen werden.

Beim Webseiten-Import lädt die App eine öffentliche HTTPS-Rezeptseite, liest
vorhandene Recipe-JSON-LD-Daten sowie sichtbaren Rezepttext aus und lässt die
Informationen per AI in Beschreibung, Zutaten und Arbeitsschritte aufteilen.
Auch hier wird zunächst nur ein bearbeitbarer Entwurf erzeugt.

Beim optionalen Kühlschrank-Check können auf dem iPhone bis zu drei Fotos direkt
mit der Rückkamera aufgenommen werden. Die erkannten Lebensmittel lassen sich vor
dem Rezeptabgleich bearbeiten oder ergänzen. Vorschläge berücksichtigen die
Haushaltsvorlieben sowie die in den Einstellungen gepflegten Vorratsbasics.

![Screenshot](./preview.png "preview")

## Lokal mit Docker starten

1. `.env.example` als `.env` kopieren und ein langes, zufälliges Datenbankpasswort setzen.
   Optional `MEALPREP_USERNAME` und `MEALPREP_PASSWORD` für einen festen Zugang setzen.
2. `docker compose up -d` ausführen.
3. `http://localhost:8088` öffnen. Mit festem Zugang direkt anmelden, andernfalls
   den ersten Zugang registrieren („Ersten Haushalt anlegen“).

Die Datenbankmigrationen und die Beispieldaten werden beim ersten Start automatisch angelegt.

## Optionale AI-Rezeptbilder

Beim Anlegen, Bearbeiten und Importieren eines Rezepts kann die App aus Name
und Zutaten ein Bild erzeugen. Die Funktion verwendet serverseitig
`Microsoft.Extensions.AI` mit dem OpenAI-Bildanbieter. Sie ist standardmäßig
deaktiviert; ohne API-Key wird kein Button angezeigt und es findet kein
externer Aufruf statt.

Für Docker Compose werden diese Werte in der nicht eingecheckten `.env`-Datei
gesetzt:

```dotenv
OPENAI_API_KEY=hier-den-api-key-eintragen
AI_RECIPE_IMAGES_ENABLED=true
AI_RECIPE_IMAGES_MODEL=gpt-image-1
AI_RECIPE_IMAGES_MEDIA_TYPE=image/png
```

`AI_RECIPE_IMAGES_API_KEY` kann weiterhin gesetzt werden, falls für die
Bildgenerierung ein anderer Key als `OPENAI_API_KEY` verwendet werden soll.

Alternativ stehen in `appsettings.json` die gleichnamigen Einstellungen unter
`AI:RecipeImages` zur Verfügung. API-Keys sollten nicht in das Repository oder
in Docker-Build-Argumente geschrieben werden. Beim Erzeugen werden Rezeptname,
Beschreibung und Zutaten an den konfigurierten AI-Anbieter übertragen. Das
Ergebnis erscheint zunächst nur als Vorschau und wird erst beim Speichern des
Rezepts als Binärdaten in PostgreSQL abgelegt.

## Optionaler Rezeptimport von Webseiten

Über das Kettensymbol auf der Rezeptseite kann eine öffentliche Rezept-URL
importiert werden. Die App extrahiert bevorzugt strukturierte Recipe-Daten und
ergänzend den sichtbaren Seitentext. Anschließend ordnet das konfigurierte
Sprachmodell die Inhalte den Rezeptfeldern zu. Der Entwurf kann vollständig
bearbeitet werden und wird erst mit **Rezept speichern** in PostgreSQL abgelegt.

Für Docker Compose werden der gemeinsame OpenAI-Key und die Funktion in `.env`
aktiviert:

```dotenv
OPENAI_API_KEY=hier-den-api-key-eintragen
AI_RECIPE_IMPORT_ENABLED=true
AI_RECIPE_IMPORT_MODEL=gpt-5.6-terra
```

Alternativ stehen die Einstellungen in `appsettings.json` unter `AI:OpenAI`
und `AI:RecipeImport` zur Verfügung. Unterstützt werden ausschließlich
öffentliche HTTPS-Seiten auf dem Standardport. Lokale, private und reservierte
Netzadressen werden auch nach DNS-Auflösung sowie bei Weiterleitungen
abgewiesen. HTML, Rezepttext und Bilder unterliegen Größen- und Zeitlimits.

Für die strukturierte Auswertung werden URL, Seitentitel, Seitenbeschreibung,
gefundene Recipe-Daten und bereinigter Rezepttext an OpenAI übertragen. Bis zu
drei gefundene Bildkandidaten werden nur serverseitig geprüft. Ein Bild landet
erst dann dauerhaft in der Datenbank, wenn es im Entwurf ausdrücklich
übernommen und das Rezept gespeichert wird. Seiten hinter Login, rein
JavaScript-gerenderte Inhalte und Seiten, die automatisierte Zugriffe sperren,
können nicht zuverlässig importiert werden.

## Optionaler Kühlschrank-Check per Foto

Über das Kamerasymbol auf der Start- oder Rezeptseite können bis zu drei Fotos
des geöffneten Kühlschranks aufgenommen oder ausgewählt werden. Die AI liefert
eine bearbeitbare Liste sichtbarer Lebensmittel. Danach gleicht die App diese
Liste lokal und deterministisch mit den gespeicherten Rezepten ab. Dabei werden
Ernährungsform, Allergien, ausgeschlossene Zutaten, Kochzeit, Favoriten und die
unter **Einstellungen → Vorlieben → Immer vorhandene Vorräte** gepflegten Basics
berücksichtigt. Ein Vorschlag kann als Mittag- oder Abendessen eingeplant werden.

Für Docker Compose werden die Funktion und der gemeinsame OpenAI-Key in `.env`
aktiviert:

```dotenv
OPENAI_API_KEY=hier-den-api-key-eintragen
AI_FRIDGE_VISION_ENABLED=true
AI_FRIDGE_VISION_MODEL=gpt-5.6-terra
```

Alternativ stehen die Einstellungen unter `AI:OpenAI` und `AI:FridgeVision` in
`appsettings.json` zur Verfügung. Die Funktion ist standardmäßig deaktiviert.
Fotos werden im Browser auf höchstens 2048 Pixel Kantenlänge verkleinert, zur
Analyse an OpenAI übertragen und von Mahlzeit weder als Datei noch in PostgreSQL
gespeichert. Beim Verlassen der Seite werden sie aus dem Arbeitsspeicher entfernt.
Die Erkennung beurteilt weder Haltbarkeit noch Lebensmittelsicherheit und prüft
keine exakten Mengen; die Zutatenangaben des Rezepts müssen vor dem Kochen
kontrolliert werden.

## Fester Benutzername und festes Passwort

Für eine private Installation kann genau ein Zugang über die Serverkonfiguration
vorgegeben werden. Sobald Benutzername und Passwort gemeinsam gesetzt sind,
werden Registrierung, Passkeys, externe Anmeldungen, Passwort-Reset und die
Kontoverwaltung deaktiviert. Nur der exakt konfigurierte Benutzername und das
konfigurierte Passwort werden akzeptiert.

In `appsettings.json`:

```json
{
  "Authentication": {
    "FixedCredentials": {
      "Username": "mealprep",
      "Password": "MealPrep-2026-sicher!"
    }
  }
}
```

Mit Docker Compose werden die Werte sicherer über die nicht eingecheckte
`.env`-Datei gesetzt:

```dotenv
MEALPREP_USERNAME=mealprep
MEALPREP_PASSWORD=MealPrep-2026-sicher!
```

Bei einem direkten `docker run` werden die ASP.NET-Core-Umgebungsvariablen
zusätzlich zu Port und Datenbankverbindung als Container-Argumente gesetzt:

```sh
docker run \
  -e Authentication__FixedCredentials__Username='mealprep' \
  -e Authentication__FixedCredentials__Password='MealPrep-2026-sicher!' \
  ghcr.io/twenzel/mealprep:latest
```

Beide Werte müssen entweder gesetzt oder leer sein. Eine unvollständige
Konfiguration wird beim Start abgelehnt. Das Konto wird beim Anwendungsstart
automatisch angelegt und das Passwort bei Konfigurationsänderungen
synchronisiert. Das Passwort muss die ASP.NET-Core-Identity-Passwortrichtlinie
erfüllen. Zugangsdaten sollten nicht als Docker-Build-Argumente verwendet
werden, da sie dadurch im Image landen können.

## Installation auf einer Synology

Voraussetzung ist ein Modell, das Synology Container Manager unterstützt.

1. Diesen Projektordner in einen gemeinsamen Ordner auf der NAS kopieren.
2. `.env.example` als `.env` kopieren und mindestens ein sicheres
   `POSTGRES_PASSWORD` setzen. Für den festen Zugang zusätzlich
   `MEALPREP_USERNAME` und `MEALPREP_PASSWORD` setzen.
3. In Container Manager ein neues Projekt aus `compose.yaml` erstellen.
4. Nach dem Start intern Port `8088` aufrufen.
5. Im DSM-Reverse-Proxy eine HTTPS-Adresse auf `http://127.0.0.1:8088` weiterleiten.
6. Die fertige HTTPS-Adresse in Safari öffnen und über **Teilen → Zum Home-Bildschirm** installieren.

Der PostgreSQL-Port wird nicht nach außen veröffentlicht. Anmeldeschlüssel bleiben über das Volume `mealprep-keys` auch bei Container-Neustarts erhalten.

### Compose-Vorschlag mit fertigem GHCR-Image

Der folgende Vorschlag lädt Version `0.0.4` direkt aus der GitHub Container
Registry und kann in Synology Container Manager als Projekt verwendet werden.
**Vor dem produktiven Einsatz unbedingt Datenbankpasswort, Benutzername und
Anwendungspasswort ändern.**

```yaml
services:
  db:
    image: postgres:17-alpine
    container_name: mealprep-db
    restart: unless-stopped
    environment:
      POSTGRES_DB: mealprep
      POSTGRES_USER: mealprep
      POSTGRES_PASSWORD: devMealPrep
      TZ: Europe/Berlin
    volumes:
      - mealprep-db:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mealprep -d mealprep"]
      interval: 10s
      timeout: 5s
      retries: 8
    security_opt:
      - no-new-privileges:true

  keys-init:
    image: postgres:17-alpine
    user: "0:0"
    entrypoint: ["/bin/sh", "-c"]
    command: ["chown -R 1654:1654 /keys"]
    volumes:
      - mealprep-keys:/keys
    security_opt:
      - no-new-privileges:true

  app:
    image: ghcr.io/twenzel/mealprep:latest
    container_name: mealprep-app
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
      keys-init:
        condition: service_completed_successfully
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: 8080
      ConnectionStrings__DefaultConnection: Host=db;Port=5432;Database=mealprep;Username=mealprep;Password=devMealPrep
      Authentication__FixedCredentials__Username: myfamily
      Authentication__FixedCredentials__Password: supersecure!
      DataProtection__KeysPath: /keys
      # Optional später aktivieren:
      OPENAI_API_KEY: ""
      AI__FridgeVision__Enabled: "false"
      AI__FridgeVision__Model: gpt-5.6-terra
      TZ: Europe/Berlin
    ports:
      - "8088:8080"
    volumes:
      - mealprep-keys:/keys
    tmpfs:
      - /tmp
    read_only: true
    security_opt:
      - no-new-privileges:true

volumes:
  mealprep-db:
  mealprep-keys:
```

## Versioniertes Docker-Image als TAR erstellen

Der Cake-Build ermittelt die aktuelle Version mit GitVersion, baut
`mealprep-app:<version>` und exportiert dieses Image anschließend als TAR-Datei.
Der Build verwendet das dateibasierte Cake.Sdk für .NET 10. Die Cake-Version ist
direkt in `build.cs` festgeschrieben. GitVersion wird durch Cake über
`InstallTools(...)` bereitgestellt.
Die ermittelte Version wird außerdem an `dotnet publish` übergeben und auf der
Einstellungsseite aus den Assembly-Metadaten angezeigt.

```sh
dotnet build.cs
```

Die Datei wird unter `artifacts/docker/mealprep-app-<version>.tar` abgelegt und
kann beispielsweise auf die Synology kopiert und dort geladen werden:

```sh
docker load -i artifacts/docker/mealprep-app-<version>.tar
```

Image-Name, Ausgabeordner und Zielplattform lassen sich überschreiben:

```sh
dotnet build.cs -- --target Docker-Export \
  --image-name mealprep-app \
  --output-directory artifacts/docker \
  --platform linux/amd64
```

Git-Tags wie `v1.2.3` ergeben das Image `mealprep-app:1.2.3`. Bei noch nicht
getaggten Commits wird die Build-Metadaten-Version in einen Docker-kompatiblen
Tag umgewandelt, beispielsweise `0.1.0+12` zu `0.1.0-build.12`.

## Docker-Image auf GitHub veröffentlichen

Das aktuelle Docker-Image liegt in der GitHub Container Registry unter
**`ghcr.io/twenzel/mealprep`**. Der neueste Stand kann direkt geladen werden:

```sh
docker pull ghcr.io/twenzel/mealprep:latest
```

Alternativ lässt sich mit
`docker pull ghcr.io/twenzel/mealprep:<version>` gezielt eine bestimmte
GitVersion laden.

Nach der Anmeldung bei der GitHub Container Registry veröffentlicht der
Cake-Task ein neues versioniertes Image:

```sh
docker login ghcr.io
dotnet build.cs -- --target Docker-Push \
  --github-owner twenzel \
  --github-image-name mealprep
```

In GitHub Actions wird der Besitzer automatisch aus
`GITHUB_REPOSITORY_OWNER` gelesen. Mit `--github-image-name` lässt sich der
Paketname ändern. `--push-latest` veröffentlicht zusätzlich den Tag `latest`:

```sh
dotnet build.cs -- --target Docker-Push \
  --github-owner twenzel \
  --github-image-name mealprep \
  --push-latest
```

Der Task führt keine eigene Anmeldung durch und gibt daher keine Zugangsdaten
an Cake weiter. Lokal muss Docker bereits bei `ghcr.io` angemeldet sein; in
GitHub Actions sollte dafür der bereitgestellte `GITHUB_TOKEN` verwendet werden.

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
