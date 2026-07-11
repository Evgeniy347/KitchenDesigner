FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY KitchenServer.Web/*.csproj KitchenServer.Web/
COPY KitchenServer.ServiceDefaults/*.csproj KitchenServer.ServiceDefaults/
RUN dotnet restore "KitchenServer.Web/KitchenServer.Web.csproj"
COPY . .
RUN dotnet publish "KitchenServer.Web/KitchenServer.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER app
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "KitchenServer.Web.dll"]
