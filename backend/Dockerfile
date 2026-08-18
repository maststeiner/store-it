# SPEC-004 — backend image for the local container stack.
# Two things ship from this file: the API runtime image, and a `migrate` image that runs
# EF migrations as a one-shot admin process (12-factor) before the API starts. Both come
# from the same build so they can never disagree about which migrations exist.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, with only the files that affect it, so editing code does not invalidate
# the (slow) restore layer. Central Package Management means the props files are inputs.
COPY Directory.Build.props Directory.Packages.props StoreIt.sln dotnet-tools.json ./
COPY src/StoreIt.Api/StoreIt.Api.csproj src/StoreIt.Api/
COPY src/StoreIt.Application/StoreIt.Application.csproj src/StoreIt.Application/
COPY src/StoreIt.Domain/StoreIt.Domain.csproj src/StoreIt.Domain/
COPY src/StoreIt.Infrastructure/StoreIt.Infrastructure.csproj src/StoreIt.Infrastructure/
RUN dotnet restore src/StoreIt.Api/StoreIt.Api.csproj

COPY src/ src/
RUN dotnet publish src/StoreIt.Api/StoreIt.Api.csproj \
    --configuration Release --no-restore --output /app

# ── migrate ───────────────────────────────────────────────────────────────────
# Runs `dotnet ef database update` and exits. Kept on the SDK image because that is
# exactly what CI does (job 1b), so local and CI apply migrations the same way.
# The connection string comes from ConnectionStrings__storeit, which
# DesignTimeDbContextFactory reads directly.
FROM build AS migrate
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "ef", "database", "update", \
            "--project", "src/StoreIt.Infrastructure", \
            "--startup-project", "src/StoreIt.Api"]

# ── runtime ───────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# One layer for both root-only steps (Sonar S7031). curl is here for the container health
# check only — compose gates the frontend on the API being healthy (AC-06), and the aspnet
# image ships no HTTP client. The user is non-root: the API needs no privileges, and
# rootless podman is a target (AC-08).
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 10001 storeit
USER storeit

WORKDIR /app
COPY --from=build /app .

# 8080 rather than 80: unprivileged ports only, so the image also runs rootless.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=5s --timeout=3s --start-period=20s --retries=12 \
    CMD curl --fail --silent http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "StoreIt.Api.dll"]
