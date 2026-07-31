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
