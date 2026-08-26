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
RUN useradd --uid 5678 --create-home appuser
COPY --from=build /app ./
USER appuser
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GestiSoft.Api.dll"]
