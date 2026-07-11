FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY KitchenServer.Web/*.csproj KitchenServer.Web/
COPY KitchenServer.ServiceDefaults/*.csproj KitchenServer.ServiceDefaults/
RUN dotnet restore "KitchenServer.Web/KitchenServer.Web.csproj"
COPY . .
RUN dotnet publish "KitchenServer.Web/KitchenServer.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# curl is used by the compose healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
# /app/data holds project saves and data-protection keys; owned by the app user
# so a named volume inherits writable permissions.
RUN mkdir -p /app/data && chown app:app /app/data
# 8080 = HTTP (site, API, WebGL); 8081 = MCP (agent hub + handshake)
ENV ASPNETCORE_URLS="http://+:8080;http://+:8081"
ENV Mcp__Port=8081
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ProjectStorage__RootPath=/app/data/projects
ENV DataProtection__KeysPath=/app/data/keys
EXPOSE 8080 8081
USER app
ENTRYPOINT ["dotnet", "KitchenServer.Web.dll"]
