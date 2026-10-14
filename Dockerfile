# syntax=docker/dockerfile:1
# =====================================================================
# ESGImpact API — imagem de produção (.NET 8)
# ---------------------------------------------------------------------
# Multi-stage:
#   1) restore  — restaura dependências só a partir dos .csproj, para
#                 aproveitar o cache de camadas do Docker
#   2) build    — compila e publica em Release
#   3) final    — runtime ASP.NET enxuto, usuário não-root, healthcheck
# =====================================================================

ARG DOTNET_VERSION=8.0

# ---------- 1) restore ------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src
COPY src/ESGImpact.Api/ESGImpact.Api.csproj src/ESGImpact.Api/
RUN dotnet restore src/ESGImpact.Api/ESGImpact.Api.csproj

# ---------- 2) build / publish ---------------------------------------
FROM restore AS build
COPY src/ src/
RUN dotnet publish src/ESGImpact.Api/ESGImpact.Api.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- 3) runtime -----------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final

# metadados de rastreabilidade (preenchidos pelo pipeline)
ARG APP_VERSION=local
ARG GIT_SHA=local
LABEL org.opencontainers.image.title="esgimpact-api" \
      org.opencontainers.image.description="ESGImpact — API ESG para cidades inteligentes (.NET 8 + MongoDB)" \
      org.opencontainers.image.source="https://github.com/JaoWilqui/esgimpact-mongodb" \
      org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.revision="${GIT_SHA}"

# curl apenas para o HEALTHCHECK
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    APP_VERSION=${APP_VERSION} \
    GIT_SHA=${GIT_SHA}

EXPOSE 8080

# usuário sem privilégios já existente nas imagens .NET 8
USER app

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
  CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "ESGImpact.Api.dll"]
