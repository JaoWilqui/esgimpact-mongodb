# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/ESGImpact.Api/ESGImpact.Api.csproj src/ESGImpact.Api/
RUN dotnet restore src/ESGImpact.Api/ESGImpact.Api.csproj
COPY . .
RUN dotnet publish src/ESGImpact.Api/ESGImpact.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ESGImpact.Api.dll"]
