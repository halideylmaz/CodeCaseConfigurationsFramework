# Resmi .NET 8 SDK imajını kullanarak uygulamayı derle
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# csproj dosyalarını kopyala ve bağımlılıkları geri yükle
COPY ["DynamicConfiguration/DynamicConfiguration.csproj", "DynamicConfiguration/"]
COPY ["ConfigurationManager/ConfigurationManager.csproj", "ConfigurationManager/"]

# Bağımlılıkları geri yükle
RUN dotnet restore "ConfigurationManager/ConfigurationManager.csproj"

# Diğer tüm dosyaları kopyala ve derle
COPY ["DynamicConfiguration/", "DynamicConfiguration/"]
COPY ["ConfigurationManager/", "ConfigurationManager/"]

WORKDIR "/src/ConfigurationManager"
RUN dotnet build "ConfigurationManager.csproj" -c Release -o /app/build

# Uygulamayı yayınla
FROM build AS publish
RUN dotnet publish "ConfigurationManager.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Resmi .NET 8 runtime imagei kullan
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Portları aç
EXPOSE 80
EXPOSE 443

# Giriş noktasını ayarla
ENTRYPOINT ["dotnet", "ConfigurationManager.dll"]

