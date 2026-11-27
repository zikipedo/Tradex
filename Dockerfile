# Image de production de TradX : l'API ASP.NET Core sert aussi le site React (même domaine).
# Construction :  docker build -t tradx .
# Aucun secret dans l'image : tout arrive par variables d'environnement (voir DEPLOIEMENT.md).

# ---------- 1. Site React (Vite) ----------
FROM node:24-alpine AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npm run build

# ---------- 2. API (.NET 10) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
# Architecture du serveur cible (fournie par Docker BuildKit) : amd64 ou arm64.
ARG TARGETARCH
WORKDIR /src
# global.json n'est pas copié : il fixe la version exacte du SDK du poste de développement,
# alors que l'image officielle peut contenir une autre version 10.0.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
# Publication pour Linux et la seule architecture visée : sans cela, les bibliothèques natives de
# SkiaSharp pour Windows, macOS, etc. (environ 300 Mo inutiles) seraient copiées dans l'image.
RUN RID="linux-$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64)" \
    && dotnet restore src/TradX.Api/TradX.Api.csproj -r "$RID" \
    && dotnet publish src/TradX.Api/TradX.Api.csproj -c Release -r "$RID" --self-contained false \
       -o /out --no-restore /p:UseAppHost=false

# ---------- 3. Image finale (runtime seul, utilisateur non root) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=api /out ./
COPY --from=web /web/dist ./wwwroot
# Dossier des photos (à monter sur un volume pour qu'elles survivent aux mises à jour).
RUN mkdir -p /app/data/uploads && chown -R "$APP_UID" /app/data
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Storage__RootPath=/app/data/uploads \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TradX.Api.dll"]
