# syntax=docker/dockerfile:1

# ---- Build stage: full SDK, only used to compile ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first so this layer is cached until the package list changes
COPY RaceDay.Api.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish RaceDay.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime stage: small ASP.NET runtime image, no SDK ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

COPY --from=build /app/publish .

# Run as the non-root user that ships with the .NET images
USER $APP_UID
ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
