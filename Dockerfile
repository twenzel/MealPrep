FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG VERSION=0.0.0-local
ARG ASSEMBLY_VERSION=0.0.0.0
ARG FILE_VERSION=0.0.0.0
ARG INFORMATIONAL_VERSION=0.0.0-local
WORKDIR /source

COPY src/MealPrep.App/MealPrep.App.csproj src/MealPrep.App/
RUN dotnet restore src/MealPrep.App/MealPrep.App.csproj

COPY src/MealPrep.App/ src/MealPrep.App/
# .NET 10 ermittelt die Blazor-Framework-Assets aus dem vollständigen
# Razor-Projekt. Deshalb muss der Restore nach dem Kopieren aktualisiert werden.
RUN dotnet restore src/MealPrep.App/MealPrep.App.csproj --force
RUN dotnet publish src/MealPrep.App/MealPrep.App.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --disable-build-servers \
    /p:UseAppHost=false \
    /p:StaticWebAssetsEnabled=true \
    /p:ContinuousIntegrationBuild=true \
    /p:Version="$VERSION" \
    /p:AssemblyVersion="$ASSEMBLY_VERSION" \
    /p:FileVersion="$FILE_VERSION" \
    /p:InformationalVersion="$INFORMATIONAL_VERSION"

# Die interaktive Oberfläche benötigt diesen Client. Ein unvollständiges
# Publish-Image soll deshalb bereits beim Build fehlschlagen.
RUN test -f /app/publish/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache icu-data-full icu-libs krb5-libs tzdata
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    TZ=Europe/Berlin
EXPOSE 8080

COPY --from=build --chown=app:app /app/publish .
USER app
ENTRYPOINT ["dotnet", "MealPrep.App.dll"]
