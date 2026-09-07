FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/GestiSoft.slnx ./
COPY backend/src/GestiSoft.Domain/GestiSoft.Domain.csproj src/GestiSoft.Domain/
COPY backend/src/GestiSoft.Contracts/GestiSoft.Contracts.csproj src/GestiSoft.Contracts/
COPY backend/src/GestiSoft.Application/GestiSoft.Application.csproj src/GestiSoft.Application/
COPY backend/src/GestiSoft.Infrastructure/GestiSoft.Infrastructure.csproj src/GestiSoft.Infrastructure/
COPY backend/src/GestiSoft.WorkerSchedine/GestiSoft.WorkerSchedine.csproj src/GestiSoft.WorkerSchedine/
RUN dotnet restore src/GestiSoft.WorkerSchedine/GestiSoft.WorkerSchedine.csproj

COPY backend/src/ src/
RUN dotnet publish src/GestiSoft.WorkerSchedine/GestiSoft.WorkerSchedine.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
RUN useradd --uid 5678 --create-home appuser
COPY --from=build /app ./
USER appuser
ENTRYPOINT ["dotnet", "GestiSoft.WorkerSchedine.dll"]
