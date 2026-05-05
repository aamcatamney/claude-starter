# syntax=docker/dockerfile:1.7

# Stage 1: build Angular client
# package-lock.json is intentionally not copied: it is generated on the host
# (often Windows) and only locks host-platform optional native deps
# (lightningcss, tailwindcss/oxide). Resolving fresh in-container pulls the
# linux-x64-gnu binaries needed at build time.
FROM node:25-bookworm-slim AS client-build
WORKDIR /src/ClientApp
COPY ClientApp/package.json ./
RUN npm install --no-audit --no-fund
COPY ClientApp/ ./
RUN npm run build -- --configuration production

# Stage 2: build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src
COPY global.json claude-starter.csproj ./
RUN dotnet restore claude-starter.csproj
COPY . .
RUN dotnet publish claude-starter.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Stage 3: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=server-build /app/publish ./
COPY --from=client-build /src/ClientApp/dist ./ClientApp/dist

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "claude-starter.dll"]
