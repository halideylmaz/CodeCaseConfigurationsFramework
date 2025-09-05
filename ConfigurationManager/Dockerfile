# Build with .NET 8 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["DynamicConfiguration/DynamicConfiguration.csproj", "DynamicConfiguration/"]
COPY ["ConfigurationManager/ConfigurationManager.csproj", "ConfigurationManager/"]

# Restore dependencies
RUN dotnet restore "ConfigurationManager/ConfigurationManager.csproj"

# Copy source and build
COPY ["DynamicConfiguration/", "DynamicConfiguration/"]
COPY ["ConfigurationManager/", "ConfigurationManager/"]

WORKDIR "/src/ConfigurationManager"
RUN dotnet build "ConfigurationManager.csproj" -c Release -o /app/build

# Publish app
FROM build AS publish
RUN dotnet publish "ConfigurationManager.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use .NET 8 runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose ports
EXPOSE 80
EXPOSE 443

# Set entrypoint
ENTRYPOINT ["dotnet", "ConfigurationManager.dll"]

