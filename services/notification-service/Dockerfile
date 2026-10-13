FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY ["NotificationService.csproj", "./"]
RUN dotnet restore "./NotificationService.csproj"

COPY . .
RUN dotnet publish "NotificationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.dll"]
