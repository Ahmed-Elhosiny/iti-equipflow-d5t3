# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
# Note: Using .slnx format (new .NET solution format)
COPY EquipFlow.slnx ./
COPY src/Domain/EquipFlow.Domain.csproj src/Domain/
COPY src/Application/EquipFlow.Application.csproj src/Application/
COPY src/Infrastructure/EquipFlow.Infrastructure.csproj src/Infrastructure/
COPY src/WebApi/EquipFlow.WebApi.csproj src/WebApi/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the code
COPY . .

# Build the application
WORKDIR /src/src/WebApi
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Default: run API
# For seed mode, override with: docker compose run --rm seed dotnet EquipFlow.WebApi.dll --seed
ENTRYPOINT ["dotnet", "EquipFlow.WebApi.dll"]