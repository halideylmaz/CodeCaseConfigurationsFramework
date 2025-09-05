# Dinamik Konfigürasyon Yönetim Sistemi

## Bu Proje Ne Yapar?

Bu proje, mikro servis mimarilerinde konfigürasyon yönetimini merkezi ve dinamik olarak sağlayan bir sistemdir. Uygulamalar runtime sırasında konfigürasyon değişikliklerini gerçek zamanlı olarak alır.

## Ana Özellikler

- Gerçek zamanlı konfigürasyon güncellemeleri (RabbitMQ ile)
- Web arayüzü ile merkezi yönetim
- Her servis kendi konfigürasyonuna güvenli erişim
- MongoDB ile veri saklama
- Docker ve docker-compose desteği
- Kapsamlı unit testler

## Sistem Nasıl Çalışır?

### 1. Temel Akış
- Configuration Manager (Web API) konfigürasyonları MongoDB'de saklar
- Kullanıcı web arayüzü ile konfigürasyonları yönetir
- Değişiklikler RabbitMQ üzerinden yayınlanır
- Configuration Reader DLL, MongoDB'den konfigürasyonları okur
- RabbitMQ mesajları ile gerçek zamanlı güncellemeler yapılır

### 2. Güvenlik ve İzolasyon
- Her uygulama sadece kendi konfigürasyonlarına erişebilir
- ApplicationName bazlı filtreleme yapılır
- Sadece IsActive=true olan kayıtlar kullanılır

## Nasıl Çalıştırılır?

### Docker ile (En Kolay Yöntem)

```bash
# Projeye git
cd CodeCaseConfigurationsFramework

# Tüm servisleri başlat
docker-compose up -d

# Sistemi durdurmak için
docker-compose down
```

### Erişim Adresleri
- **Web Yönetim**: http://localhost:8080
- **API Dokümantasyonu**: http://localhost:8080/swagger
- **MongoDB Web Arayüzü**: http://localhost:8081
- **RabbitMQ Yönetim**: http://localhost:15672

### Manuel Çalıştırma

```bash
# API'yi çalıştır
cd ConfigurationManager
dotnet run

# Testleri çalıştır
cd ../DynamicConfiguration.Tests
dotnet test
```

## Diğer Projelerde Nasıl Kullanılır?

### 1. DLL Referansı Ekle

DynamicConfiguration projesini referans olarak ekleyin veya DLL'i kopyalayın.

### 2. Basit Kullanım

```csharp
using DynamicConfiguration;

// Configuration Reader oluştur
var configReader = new ConfigurationReader(
    applicationName: "SERVICE-A",                    // Uygulamanızın adı
    connectionString: "mongodb://localhost:27017",  // MongoDB bağlantısı
    refreshTimerIntervalInMs: 30000                  // 30 saniyede bir yenile
);

// Konfigürasyon değerlerini oku
string siteName = configReader.GetValue<string>("SiteName");
bool isBasketEnabled = configReader.GetValue<bool>("IsBasketEnabled");
int maxItemCount = configReader.GetValue<int>("MaxItemCount");

// Varsayılan değer ile güvenli okuma
string optionalValue = configReader.GetValue("OptionalSetting", "varsayılan-değer");
```

### 3. Dependency Injection ile

```csharp
// Program.cs veya Startup.cs
builder.Services.AddSingleton(sp =>
{
    return new ConfigurationReader(
        applicationName: "SERVICE-A",
        connectionString: config["MongoDbSettings:ConnectionString"],
        refreshTimerIntervalInMs: 30000
    );
});

// Controller'da kullan
public class OrderController : ControllerBase
{
    private readonly ConfigurationReader _config;

    public OrderController(ConfigurationReader config)
    {
        _config = config;
    }

    [HttpGet]
    public IActionResult GetOrders()
    {
        bool basketEnabled = _config.GetValue("IsBasketEnabled", true);
        int maxItems = _config.GetValue("MaxItemCount", 100);

        // İş mantığı...
        return Ok();
    }
}
```

## Temel API Endpoint'leri

```bash
# Konfigürasyonları listele
GET http://localhost:8080/api/configuration

# Yeni konfigürasyon ekle
POST http://localhost:8080/api/configuration

# Konfigürasyon güncelle
PUT http://localhost:8080/api/configuration/{id}

# Konfigürasyon sil
DELETE http://localhost:8080/api/configuration/{id}
```

## Test Çalıştırma

```bash
# Tüm testleri çalıştır
dotnet test

# Sadece belirli test sınıfını çalıştır
dotnet test --filter ConfigurationReaderTests
dotnet test --filter RabbitMQMessagingTests
```

## Teknik Detaylar

### Yapı
- **ConfigurationReader**: Ana sınıf, konfigürasyon erişimi sağlar
- **MongoConfigurationRepository**: MongoDB veri erişimi
- **RabbitMQMessagePublisher/Subscriber**: Gerçek zamanlı mesajlaşma
- **Web API**: Yönetim arayüzü
- **Unit Tests**: Kod kalitesi güvencesi

### Özellikler
- Otomatik yenileme (timer-based)
- Thread-safe cache
- Type safety (generic methods)
- Error handling ve fallback
- Health monitoring
- Application isolation

Bu sistem mikro servislerinizde konfigürasyon yönetimini merkezi ve dinamik hale getirir.
