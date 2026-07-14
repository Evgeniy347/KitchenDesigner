# Build context is the REPO ROOT (not server/), because KitchenServer.McpContract
# link-compiles the shared tool contract from Assets/Scripts/Core/MCP/Contract —
# the single source of truth also compiled by Unity. Build with:
#   docker build -f server/Dockerfile <repo-root>
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY server/KitchenServer.Web/*.csproj server/KitchenServer.Web/
COPY server/KitchenServer.ServiceDefaults/*.csproj server/KitchenServer.ServiceDefaults/
COPY server/KitchenServer.McpContract/*.csproj server/KitchenServer.McpContract/
RUN dotnet restore "server/KitchenServer.Web/KitchenServer.Web.csproj"
COPY server/ server/
# Shared contract sources (referenced by McpContract via ..\..\Assets\...).
COPY Assets/Scripts/Core/MCP/Contract/ Assets/Scripts/Core/MCP/Contract/
RUN dotnet publish "server/KitchenServer.Web/KitchenServer.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# curl is used by the compose healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
# /app/data holds project saves and data-protection keys; owned by the app user
# so a named volume inherits writable permissions.
RUN mkdir -p /app/data && chown app:app /app/data
# Seed data — example project that gets auto-imported for new users.
RUN mkdir -p /app/seed && chown app:app /app/seed
COPY docs/example.save.json /app/seed/example.save.json
# 8080 = HTTP (site, API, WebGL); 8081 = MCP (real MCP over Streamable HTTP)
ENV ASPNETCORE_URLS="http://+:8080;http://+:8081"
ENV Mcp__Port=8081
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ProjectStorage__RootPath=/app/data/projects
ENV DataProtection__KeysPath=/app/data/keys
EXPOSE 8080 8081
USER app
ENTRYPOINT ["dotnet", "KitchenServer.Web.dll"]
