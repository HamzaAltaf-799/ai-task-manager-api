# syntax=docker/dockerfile:1

# ─── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies first (layer-cached until .csproj changes)
COPY ["AITaskManager.API/AITaskManager.API.csproj", "AITaskManager.API/"]
COPY ["AITaskManager.Tests/AITaskManager.Tests.csproj", "AITaskManager.Tests/"]
COPY ["AITaskManager.sln", "./"]
RUN dotnet restore "AITaskManager.API/AITaskManager.API.csproj"

# Copy source and build
COPY . .
WORKDIR /src/AITaskManager.API
RUN dotnet build "AITaskManager.API.csproj" -c Release --no-restore -o /app/build

# ─── Stage 2: Test ────────────────────────────────────────────────────────────
FROM build AS test
WORKDIR /src
RUN dotnet restore "AITaskManager.Tests/AITaskManager.Tests.csproj"
WORKDIR /src/AITaskManager.Tests
RUN dotnet run --no-restore -c Release

# ─── Stage 3: Publish ─────────────────────────────────────────────────────────
FROM build AS publish
WORKDIR /src/AITaskManager.API
RUN dotnet publish "AITaskManager.API.csproj" -c Release --no-restore \
    -o /app/publish /p:UseAppHost=false

# ─── Stage 4: Runtime (minimal attack surface) ────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Non-root user for security
RUN adduser --disabled-password --gecos "" --uid 1001 appuser

WORKDIR /app

# Health check (uses the /health endpoint we expose)
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

COPY --from=publish --chown=appuser:appuser /app/publish .

USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AITaskManager.API.dll"]
