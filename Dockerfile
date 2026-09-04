# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY EquipFlow.sln ./
COPY src/Domain/EquipFlow.Domain.csproj src/Domain/
COPY src/Application/EquipFlow.Application.csproj src/Application/
COPY src/Infrastructure/EquipFlow.Infrastructure.csproj src/Infrastructure/
COPY src/WebApi/EquipFlow.WebApi.csproj src/WebApi/
COPY tests/EquipFlow.Domain.Tests/EquipFlow.Domain.Tests.csproj tests/EquipFlow.Domain.Tests/

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
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EquipFlow.WebApi.dll"]