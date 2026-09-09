FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first (layer caching)
COPY services/inventory-service/InventoryService.csproj services/inventory-service/
RUN dotnet restore services/inventory-service/InventoryService.csproj

# Copy remaining source code and publish release build
COPY services/inventory-service/ services/inventory-service/
RUN dotnet publish services/inventory-service/InventoryService.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# Lightweight runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InventoryService.dll"]
