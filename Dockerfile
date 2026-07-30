FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

COPY src/MealPrep.App/MealPrep.App.csproj src/MealPrep.App/
RUN dotnet restore src/MealPrep.App/MealPrep.App.csproj

COPY src/MealPrep.App/ src/MealPrep.App/
RUN dotnet publish src/MealPrep.App/MealPrep.App.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

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
