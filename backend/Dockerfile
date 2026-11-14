FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Hanora.Api/Hanora.Api.csproj
RUN dotnet publish src/Hanora.Api/Hanora.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# QuestPDF/Skia membutuhkan fontconfig dan font dasar untuk export PDF.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libfontconfig1 \
        libfreetype6 \
        libgssapi-krb5-2 \
        fonts-dejavu-core \
        curl \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /app/logs /var/hanora/uploads /var/hanora/keys \
    && chown -R app:app /app /var/hanora

COPY --from=build /app/publish .

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "Hanora.Api.dll"]
