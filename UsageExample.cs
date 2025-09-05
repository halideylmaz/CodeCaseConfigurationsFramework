using DynamicConfiguration;

namespace ExampleApplication
{
    /// <summary>Konfigürasyon kullanım örneği.</summary>
    public class ConfigurationExample
    {
        private readonly ConfigurationReader _configReader;

        public ConfigurationExample()
        {
            // Config reader başlat
            _configReader = new ConfigurationReader(
                applicationName: "SERVICE-A",
                connectionString: "mongodb://localhost:27017",
                refreshTimerIntervalInMs: 30000 // 30 saniyede bir yenile
            );
        }

        public async Task DemonstrateUsage()
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

                // Varsayılan değerlerle kullanım
                string optionalSetting = _configReader.GetValue("OptionalSetting", "varsayılan-değer");
                Console.WriteLine($"İsteğe Bağlı Ayar: {optionalSetting}");

                // Key var mı kontrol et
                if (_configReader.HasKey("SomeFeatureFlag"))
                {
                    bool featureFlag = _configReader.GetValue<bool>("SomeFeatureFlag");
                    Console.WriteLine($"Özellik Bayrağı: {featureFlag}");
                }

                // Tüm key'leri getir
                var allKeys = _configReader.GetAllKeys();
                Console.WriteLine($"Mevcut konfigürasyon anahtarları: {string.Join(", ", allKeys)}");

                // Health check
                bool isHealthy = _configReader.IsHealthy();
                Console.WriteLine($"Konfigürasyon servisi sağlıklı: {isHealthy}");

                // Manuel yenileme
                await _configReader.RefreshAsync();
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
                // Key yoksa exception fırlatır
                string missingConfig = _configReader.GetValue<string>("NonExistentKey");
            }
            catch (KeyNotFoundException ex)
            {
                Console.WriteLine($"Konfigürasyon anahtarı bulunamadı: {ex.Message}");
            }

            try
            {
                // Type conversion başarısız olursa exception fırlatır
                int invalidType = _configReader.GetValue<int>("SiteName"); // SiteName string tipinde
            }
            catch (InvalidCastException ex)
            {
                Console.WriteLine($"Tip dönüştürme başarısız: {ex.Message}");
            }

            // Güvenli erişim
            string safeValue = _configReader.GetValue("PotentiallyMissingKey", "güvenli-varsayılan");
            Console.WriteLine($"Güvenli değer: {safeValue}");
        }
    }

    /// <summary>Servis entegrasyonu örneği.</summary>
    public class OrderService
    {
        private readonly ConfigurationReader _config;

        public OrderService(ConfigurationReader config)
        {
            _config = config;
        }

        public void ProcessOrder()
        {
            // Config değerlerini kullan
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

    /// <summary>DI kurulumu örneği.</summary>
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Config reader singleton kaydet
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

            // Servisleri kaydet
            services.AddScoped<OrderService>();
        }
    }

    /// <summary>Program giriş noktası.</summary>
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

            // Periyodik yenileme
            Console.WriteLine("Konfigürasyon her 30 saniyede bir otomatik yenilenecek...");
            Console.WriteLine("Çıkmak için Ctrl+C tuşuna basın");

            // Uygulamayı çalışır durumda tut
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


