using DynamicConfiguration;

namespace ExampleApplication
{
    /// <summary>
    /// Dinamik Konfigürasyon Çerçevesinin nasıl kullanılacağını gösteren örnek
    /// </summary>
    public class ConfigurationExample
    {
        private readonly ConfigurationReader _configReader;

        public ConfigurationExample()
        {
            // Konfigürasyon okuyucusunu başlat
            _configReader = new ConfigurationReader(
                applicationName: "SERVICE-A",
                connectionString: "mongodb://localhost:27017",
                refreshTimerIntervalInMs: 30000 // Her 30 saniyede bir yenile
            );
        }

        public void DemonstrateUsage()
        {
            try
            {
                // Temel kullanım
                string siteName = _configReader.GetValue<string>("SiteName");
                Console.WriteLine($"Site Adı: {siteName}");

                bool isBasketEnabled = _configReader.GetValue<bool>("IsBasketEnabled");
                Console.WriteLine($"Sepet Etkin: {isBasketEnabled}");

                int maxItemCount = _configReader.GetValue<int>("MaxItemCount");
                Console.WriteLine($"Maksimum Öğe Sayısı: {maxItemCount}");

                // Varsayılan değerlerle kullanım (güvenli geri dönüş)
                string optionalSetting = _configReader.GetValue("OptionalSetting", "varsayılan-değer");
                Console.WriteLine($"İsteğe Bağlı Ayar: {optionalSetting}");

                // Konfigürasyonun var olup olmadığını kontrol et
                if (_configReader.HasKey("SomeFeatureFlag"))
                {
                    bool featureFlag = _configReader.GetValue<bool>("SomeFeatureFlag");
                    Console.WriteLine($"Özellik Bayrağı: {featureFlag}");
                }

                // Tüm mevcut anahtarları getir
                var allKeys = _configReader.GetAllKeys();
                Console.WriteLine($"Mevcut konfigürasyon anahtarları: {string.Join(", ", allKeys)}");

                // Sağlık kontrolü
                bool isHealthy = _configReader.IsHealthy();
                Console.WriteLine($"Konfigürasyon servisi sağlıklı: {isHealthy}");

                // Manuel yenileme (hemen yeniden yüklemeye zorla)
                _configReader.RefreshAsync().Wait();
                Console.WriteLine("Konfigürasyon manuel olarak yenilendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Konfigürasyona erişirken hata: {ex.Message}");
            }
        }

        public void DemonstrateErrorHandling()
        {
            try
            {
                // Anahtar mevcut değilse KeyNotFoundException fırlatır
                string missingConfig = _configReader.GetValue<string>("NonExistentKey");
            }
            catch (KeyNotFoundException ex)
            {
                Console.WriteLine($"Konfigürasyon anahtarı bulunamadı: {ex.Message}");
            }

            try
            {
                // Tip dönüştürme başarısız olursa InvalidCastException fırlatır
                int invalidType = _configReader.GetValue<int>("SiteName"); // SiteName string tipinde
            }
            catch (InvalidCastException ex)
            {
                Console.WriteLine($"Tip dönüştürme başarısız: {ex.Message}");
            }

            // Varsayılan değerlerle güvenli erişim
            string safeValue = _configReader.GetValue("PotentiallyMissingKey", "güvenli-varsayılan");
            Console.WriteLine($"Güvenli değer: {safeValue}");
        }
    }

    /// <summary>
    /// Servis sınıfında konfigürasyon entegrasyonu örneği
    /// </summary>
    public class OrderService
    {
        private readonly ConfigurationReader _config;

        public OrderService(ConfigurationReader config)
        {
            _config = config;
        }

        public void ProcessOrder()
        {
            // İş mantığında konfigürasyon değerlerini kullan
            bool basketEnabled = _config.GetValue("IsBasketEnabled", true);
            int maxItems = _config.GetValue("MaxItemCount", 100);

            if (basketEnabled)
            {
                Console.WriteLine($"Maksimum {maxItems} öğe ile sipariş işleniyor");
            }
            else
            {
                Console.WriteLine("Sepet işlevi devre dışı");
            }
        }
    }

    /// <summary>
    /// Dependency Injection kurulumu örneği
    /// </summary>
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Konfigürasyon okuyucusunu singleton olarak kaydet
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config["MongoDbSettings:ConnectionString"];

                return new ConfigurationReader(
                    applicationName: "SERVICE-A",
                    connectionString: connectionString,
                    refreshTimerIntervalInMs: 30000
                );
            });

            // Konfigürasyona bağımlı servisleri kaydet
            services.AddScoped<OrderService>();
        }
    }

    /// <summary>
    /// Kullanımı gösteren program giriş noktası
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Dinamik Konfigürasyon Çerçevesi Örneği");
            Console.WriteLine("=====================================");

            var example = new ConfigurationExample();
            example.DemonstrateUsage();
            Console.WriteLine();

            example.DemonstrateErrorHandling();
            Console.WriteLine();

            // Periyodik yenilemeyi göster
            Console.WriteLine("Konfigürasyon her 30 saniyede bir otomatik yenilenecek...");
            Console.WriteLine("Çıkmak için Ctrl+C tuşuna basın");

            // Yenilemeyi göstermek için uygulamayı çalışır durumda tut
            var cancellationTokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Uygulama kapatılıyor...");
            }
        }
    }
}


