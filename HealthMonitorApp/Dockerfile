# Use the official image as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY HealthMonitorApp.csproj .
RUN dotnet restore "HealthMonitorApp.csproj"

# Copy everything else and build app
COPY . .
RUN dotnet build "HealthMonitorApp.csproj" -c Release -o /app/build

# Ensure the data file is copied to the build output
COPY Data/1.txt /app/build/Data/1.txt
COPY app.db /app/build/

FROM build AS publish
RUN dotnet publish "HealthMonitorApp.csproj" -c Release -o /app/publish

# Copy build app from publish to base image and define entrypoint
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HealthMonitorApp.dll"]
