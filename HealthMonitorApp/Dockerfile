# Use the official ASP.NET Core runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-arm64v8 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble-arm64v8 AS build
WORKDIR /src

# Install git
RUN apt-get update && apt-get install -y git

# Copy csproj and restore as distinct layers
COPY HealthMonitorApp.csproj .
RUN dotnet restore "HealthMonitorApp.csproj"

# Copy everything else and build app
COPY . .
RUN dotnet build "HealthMonitorApp.csproj" -c Release -o /app/build

# Ensure the data file is copied to the build output
COPY app.db /app/build/
COPY Data /app/build/Data

# Publish the application
RUN dotnet publish "HealthMonitorApp.csproj" -c Release -o /app/publish

# Install additional .NET tools (ApplicationInspector)
RUN dotnet tool install -g Microsoft.CST.ApplicationInspector.CLI

# Ensure the dotnet tools are available in PATH
ENV PATH="$PATH:/root/.dotnet/tools"

# Create a new stage for the final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-arm64v8 AS final
WORKDIR /app

# Install git in the final image
RUN apt-get update && apt-get install -y git

# Copy everything from the publish and build stages
COPY --from=build /app/publish .
COPY --from=build /root/.dotnet /root/.dotnet
COPY --from=build /root/.nuget /root/.nuget

# Set the environment variables
ENV PATH="$PATH:/root/.dotnet/tools"

ENTRYPOINT ["dotnet", "HealthMonitorApp.dll"]
