# syntax=docker/dockerfile:1

# ---- Build stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SchoolCafeteria.sln .
COPY src/SchoolCafeteria.Domain/SchoolCafeteria.Domain.csproj src/SchoolCafeteria.Domain/
COPY src/SchoolCafeteria.Application/SchoolCafeteria.Application.csproj src/SchoolCafeteria.Application/
COPY src/SchoolCafeteria.Infrastructure/SchoolCafeteria.Infrastructure.csproj src/SchoolCafeteria.Infrastructure/
COPY src/SchoolCafeteria.Api/SchoolCafeteria.Api.csproj src/SchoolCafeteria.Api/
COPY tests/SchoolCafeteria.UnitTests/SchoolCafeteria.UnitTests.csproj tests/SchoolCafeteria.UnitTests/
COPY tests/SchoolCafeteria.IntegrationTests/SchoolCafeteria.IntegrationTests.csproj tests/SchoolCafeteria.IntegrationTests/
RUN dotnet restore src/SchoolCafeteria.Api/SchoolCafeteria.Api.csproj

COPY src/ src/
RUN dotnet publish src/SchoolCafeteria.Api/SchoolCafeteria.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is only needed for the HEALTHCHECK probe below; installed while still root, then dropped.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 10001 --create-home appuser \
    && mkdir -p /data/storage && chown -R appuser:appuser /data/storage
USER appuser

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=appuser:appuser /app/publish .

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "SchoolCafeteria.Api.dll"]
