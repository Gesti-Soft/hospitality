FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/GestiSoft.slnx ./
COPY backend/src/GestiSoft.Domain/GestiSoft.Domain.csproj src/GestiSoft.Domain/
COPY backend/src/GestiSoft.Contracts/GestiSoft.Contracts.csproj src/GestiSoft.Contracts/
COPY backend/src/GestiSoft.Application/GestiSoft.Application.csproj src/GestiSoft.Application/
COPY backend/src/GestiSoft.Infrastructure/GestiSoft.Infrastructure.csproj src/GestiSoft.Infrastructure/
COPY backend/src/GestiSoft.Api/GestiSoft.Api.csproj src/GestiSoft.Api/
RUN dotnet restore src/GestiSoft.Api/GestiSoft.Api.csproj

COPY backend/src/ src/
RUN dotnet publish src/GestiSoft.Api/GestiSoft.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# pg_dump per la pagina Backup del pannello Super Admin (genera un dump al volo via rete verso
# postgres:5432, non tocca il volume dati). L'immagine base è Ubuntu 24.04: il suo repo apt di
# default offre solo postgresql-client 16, più vecchio del server (Postgres 17) — non supportato
# in modo affidabile per pg_dump. Serve il repo ufficiale PGDG per la versione 17 esatta.
RUN apt-get update -qq \
    && apt-get install -y -qq --no-install-recommends gnupg curl ca-certificates \
    && curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /usr/share/keyrings/postgresql.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] http://apt.postgresql.org/pub/repos/apt noble-pgdg main" > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update -qq \
    && apt-get install -y -qq --no-install-recommends postgresql-client-17 \
    && apt-get purge -y -qq gnupg curl \
    && apt-get autoremove -y -qq \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --uid 5678 --create-home appuser
COPY --from=build /app ./
USER appuser
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GestiSoft.Api.dll"]
