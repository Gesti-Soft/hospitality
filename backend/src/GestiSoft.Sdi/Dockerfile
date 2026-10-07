FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/GestiSoft.slnx ./
COPY backend/src/GestiSoft.Domain/GestiSoft.Domain.csproj src/GestiSoft.Domain/
COPY backend/src/GestiSoft.Contracts/GestiSoft.Contracts.csproj src/GestiSoft.Contracts/
COPY backend/src/GestiSoft.Application/GestiSoft.Application.csproj src/GestiSoft.Application/
COPY backend/src/GestiSoft.Infrastructure/GestiSoft.Infrastructure.csproj src/GestiSoft.Infrastructure/
COPY backend/src/GestiSoft.Sdi/GestiSoft.Sdi.csproj src/GestiSoft.Sdi/
RUN dotnet restore src/GestiSoft.Sdi/GestiSoft.Sdi.csproj

COPY backend/src/ src/
RUN dotnet publish src/GestiSoft.Sdi/GestiSoft.Sdi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl per l'HEALTHCHECK.
RUN apt-get update -qq \
    && apt-get install -y -qq --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Gruppo 5678 esplicito: sulla VPS il certificato client (.p12) è root:5678 con permessi 640, quindi
# lo legge solo questo gruppo. Con un gruppo creato da useradd il numero non sarebbe garantito.
RUN groupadd --gid 5678 appuser && useradd --uid 5678 --gid 5678 --create-home appuser
# Archivio su disco delle fatture ricevute (volume gestisoft_sdi_ricevuti): un volume nuovo prende
# proprietario e permessi dalla cartella che trova nell'immagine.
RUN mkdir -p /var/lib/gestisoft/sdi-ricevuti && chown appuser:appuser /var/lib/gestisoft/sdi-ricevuti
COPY --from=build /app ./
USER appuser
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "GestiSoft.Sdi.dll"]
