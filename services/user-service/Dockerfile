FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY services/user-service/UserService.csproj services/user-service/
RUN dotnet restore services/user-service/UserService.csproj

COPY services/user-service/ services/user-service/
RUN dotnet publish services/user-service/UserService.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UserService.dll"]
