using DynamicConfiguration.Models;
using DynamicConfiguration.Repositories;
using DynamicConfiguration.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DynamicConfiguration
{
    /// <summary>
    /// Dinamik Konfigürasyon Okuyucu - Güçlü Tipli Erişim ve Otomatik Yenileme
    /// 
    /// Bu sınıf, uygulamaların konfigürasyon değerlerine güçlü tipli erişim sağlar.
    /// Otomatik yenileme, gerçek zamanlı güncellemeler ve son başarılı değerlere geri dönüş özellikleri sunar.
    /// 
    /// Özellikler:
    /// - Güçlü tipli konfigürasyon değeri erişimi (GetValue&lt;T&gt;)
    /// - Otomatik periyodik yenileme (Timer tabanlı)
    /// - Gerçek zamanlı değişiklik bildirimleri (RabbitMQ)
    /// - Thread-safe cache yönetimi
    /// - Son başarılı değerlere geri dönüş (fallback)
    /// - Sağlık durumu takibi
    /// </summary>
    public class ConfigurationReader : IDisposable
    {
        // ========================================
        // TEMEL YAPILANDIRMA PARAMETRELERİ
        // ========================================
        private readonly string _applicationName;                    // Uygulama adı
        private readonly int _refreshTimerIntervalInMs;              // Yenileme aralığı (milisaniye)
        private readonly ILogger<ConfigurationReader> _logger;       // Loglama servisi
        private readonly IConfigurationRepository _repository;       // Veri erişim katmanı
        private readonly Timer _refreshTimer;                        // Otomatik yenileme timer'ı
        private readonly IMessageSubscriber? _messageSubscriber;     // Gerçek zamanlı mesaj abonesi

        // ========================================
        // THREAD-SAFE CACHE YÖNETİMİ
        // ========================================
        private readonly ConcurrentDictionary<string, ConfigurationRecord> _configurationCache = new();  // Konfigürasyon kayıtları cache'i
        private readonly ConcurrentDictionary<string, object> _typedValueCache = new();                  // Tipli değerler cache'i
        private readonly ReaderWriterLockSlim _cacheLock = new();                                        // Cache erişim kilidi
        private bool _isDisposed;                                                                         // Dispose durumu

        // ========================================
        // SAĞLIK DURUMU TAKİBİ
        // ========================================
        private volatile bool _isRepositoryHealthy = true;           // Repository sağlık durumu
        private DateTime _lastSuccessfulRefresh = DateTime.MinValue; // Son başarılı yenileme zamanı

        /// <summary>
        /// ConfigurationReader'ın yeni bir örneğini başlatır (Basit Constructor)
        /// 
        /// Bu constructor, temel kullanım senaryoları için tasarlanmıştır.
        /// Varsayılan logger ve repository ile çalışır.
        /// </summary>
        /// <param name="applicationName">Bu konfigürasyonu kullanan uygulamanın adı</param>
        /// <param name="connectionString">MongoDB bağlantı dizesi</param>
        /// <param name="refreshTimerIntervalInMs">Konfigürasyonu depolamadan yenileme aralığı (milisaniye)</param>
        public ConfigurationReader(string applicationName, string connectionString, int refreshTimerIntervalInMs)
            : this(applicationName, connectionString, refreshTimerIntervalInMs, null, null, null, false)
        {
        }

        /// <summary>
        /// ConfigurationReader'ın yeni bir örneğini başlatır (Gelişmiş Constructor)
        /// 
        /// Bu constructor, özelleştirilmiş logger, repository ve mesaj abonesi ile
        /// gelişmiş kullanım senaryoları için tasarlanmıştır.
        /// </summary>
        /// <param name="applicationName">Bu konfigürasyonu kullanan uygulamanın adı</param>
        /// <param name="connectionString">MongoDB bağlantı dizesi</param>
        /// <param name="refreshTimerIntervalInMs">Konfigürasyonu depolamadan yenileme aralığı (milisaniye)</param>
        /// <param name="loggerFactory">Özel logger factory (null ise varsayılan oluşturulur)</param>
        /// <param name="repository">Özel repository örneği (null ise varsayılan oluşturulur)</param>
        /// <param name="messageSubscriber">Gerçek zamanlı güncellemeler için mesaj abonesi</param>
        /// <param name="loadSynchronously">Konfigürasyonları senkron olarak yükleyip yüklemeyeceği (test için)</param>
        public ConfigurationReader(
            string applicationName,
            string connectionString,
            int refreshTimerIntervalInMs,
            ILoggerFactory? loggerFactory = null,
            IConfigurationRepository? repository = null,
            IMessageSubscriber? messageSubscriber = null,
            bool loadSynchronously = false)
        {
            // ========================================
            // PARAMETRE DOĞRULAMA VE ATAMA
            // ========================================
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            _refreshTimerIntervalInMs = Math.Max(1000, refreshTimerIntervalInMs); // Minimum 1 saniye

            // ========================================
            // LOGGER YAPILANDIRMASI
            // ========================================
            // Logger factory sağlanmamışsa varsayılan oluştur
            var factory = loggerFactory ?? LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            _logger = factory.CreateLogger<ConfigurationReader>();

            // ========================================
            // REPOSITORY YAPILANDIRMASI
            // ========================================
            // Repository sağlanmamışsa varsayılan oluştur
            _repository = repository ?? CreateDefaultRepository(connectionString);

            // ========================================
            // MESAJ ABONELİĞİ YAPILANDIRMASI
            // ========================================
            // Gerçek zamanlı güncellemeler için mesaj abonesini sakla
            _messageSubscriber = messageSubscriber;

            // ========================================
            // İLK KONFIGÜRASYON YÜKLEMESİ
            // ========================================
            // Cache'i başlat
            if (loadSynchronously)
            {
                // Senkron yükleme (test senaryoları için)
                Task.Run(() => LoadConfigurationsAsync()).Wait();
            }
            else
            {
                // Asenkron yükleme (normal kullanım)
                _ = Task.Run(() => LoadConfigurationsAsync());
            }

            // ========================================
            // OTOMATİK YENİLEME TİMER'I
            // ========================================
            // Periyodik yenileme timer'ını başlat
            _refreshTimer = new Timer(RefreshConfigurations, null, _refreshTimerIntervalInMs, _refreshTimerIntervalInMs);

            // ========================================
            // GERÇEK ZAMANLI GÜNCELLEME ABONELİĞİ
            // ========================================
            // Mesaj abonesi mevcutsa gerçek zamanlı güncellemelere abone ol
            if (_messageSubscriber != null)
            {
                _ = Task.Run(() => SubscribeToConfigurationChangesAsync());
            }

            _logger.LogInformation("ConfigurationReader başlatıldı - Uygulama: {ApplicationName}, Yenileme Aralığı: {Interval}ms",
                _applicationName, _refreshTimerIntervalInMs);
        }

        /// <summary>
        /// Belirtilen anahtara göre güçlü tipli konfigürasyon değeri getirir
        /// 
        /// Bu method, konfigürasyon değerlerine güçlü tipli erişim sağlar.
        /// Önce tipli cache'den, sonra konfigürasyon cache'inden değeri alır.
        /// </summary>
        /// <typeparam name="T">Konfigürasyon değerinin dönüştürüleceği tip</typeparam>
        /// <param name="key">Konfigürasyon anahtarı</param>
        /// <returns>Belirtilen tipe dönüştürülmüş konfigürasyon değeri</returns>
        /// <exception cref="KeyNotFoundException">Konfigürasyon anahtarı bulunamadığında fırlatılır</exception>
        /// <exception cref="InvalidCastException">Değer belirtilen tipe dönüştürülemediğinde fırlatılır</exception>
        public T GetValue<T>(string key)
        {
            // ========================================
            // PARAMETRE DOĞRULAMA
            // ========================================
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Anahtar null veya boş olamaz", nameof(key));

            // ========================================
            // TİPLİ CACHE'DEN HIZLI ERİŞİM
            // ========================================
            // Performans için önce tipli cache'den almaya çalış
            if (_typedValueCache.TryGetValue(key, out var cachedValue) && cachedValue is T typedValue)
            {
                return typedValue;
            }

            // ========================================
            // KONFIGÜRASYON CACHE'İNDEN ERİŞİM
            // ========================================
            // Konfigürasyon cache'inden kaydı al
            if (!_configurationCache.TryGetValue(key, out var record))
            {
                _logger.LogWarning("Konfigürasyon anahtarı '{Key}' uygulama '{ApplicationName}' için bulunamadı", key, _applicationName);
                throw new KeyNotFoundException($"Konfigürasyon anahtarı '{key}' bulunamadı");
            }

            // ========================================
            // TİP DÖNÜŞTÜRME VE CACHE'E KAYDETME
            // ========================================
            try
            {
                var value = ConvertToType<T>(record.Value, record.Type);
                _typedValueCache[key] = value!;

                _logger.LogDebug("Konfigürasyon değeri alındı - Anahtar: '{Key}', Type: {Type}", key, typeof(T).Name);
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon değeri dönüştürülemedi - Anahtar: '{Key}', Type: {Type}", key, typeof(T).Name);
                throw new InvalidCastException($"Konfigürasyon değeri '{record.Value}' {typeof(T).Name} tipine dönüştürülemedi", ex);
            }
        }

        /// <summary>
        /// Belirtilen anahtara göre güçlü tipli konfigürasyon değeri getirir (varsayılan değer ile)
        /// 
        /// Bu method, konfigürasyon değeri bulunamazsa veya dönüştürülemezse
        /// belirtilen varsayılan değeri döndürür. Hata durumlarında exception fırlatmaz.
        /// </summary>
        /// <typeparam name="T">Konfigürasyon değerinin dönüştürüleceği tip</typeparam>
        /// <param name="key">Konfigürasyon anahtarı</param>
        /// <param name="defaultValue">Anahtar bulunamazsa veya dönüştürme başarısız olursa döndürülecek varsayılan değer</param>
        /// <returns>Konfigürasyon değeri veya varsayılan değer</returns>
        public T GetValue<T>(string key, T defaultValue)
        {
            try
            {
                return GetValue<T>(key);
            }
            catch
            {
                _logger.LogWarning("Konfigürasyon anahtarı '{Key}' için varsayılan değer kullanılıyor", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Belirtilen konfigürasyon anahtarının mevcut olup olmadığını kontrol eder
        /// </summary>
        /// <param name="key">Kontrol edilecek konfigürasyon anahtarı</param>
        /// <returns>Anahtar mevcutsa true, aksi halde false</returns>
        public bool HasKey(string key)
        {
            return _configurationCache.ContainsKey(key);
        }

        /// <summary>
        /// Mevcut uygulama için tüm konfigürasyon anahtarlarını getirir
        /// </summary>
        /// <returns>Konfigürasyon anahtarları koleksiyonu</returns>
        public IEnumerable<string> GetAllKeys()
        {
            return _configurationCache.Keys.ToList();
        }

        /// <summary>
        /// Konfigürasyon cache'ini zorla yeniler
        /// 
        /// Bu method, depolamadan konfigürasyonları yeniden yükler.
        /// Normal durumda otomatik yenileme yeterlidir, ancak acil durumlarda
        /// manuel yenileme için kullanılabilir.
        /// </summary>
        public async Task RefreshAsync()
        {
            await LoadConfigurationsAsync();
        }

        /// <summary>
        /// Konfigürasyon okuyucunun sağlık durumunu getirir
        /// 
        /// Sağlıklı olması için repository bağlantısı aktif olmalı ve
        /// en az bir konfigürasyon kaydı cache'de bulunmalıdır.
        /// </summary>
        /// <returns>Sağlıklı ise true, aksi halde false</returns>
        public bool IsHealthy()
        {
            return _isRepositoryHealthy && _configurationCache.Any();
        }

        /// <summary>
        /// Son başarılı konfigürasyon yenileme zamanını getirir
        /// 
        /// Bu bilgi, sistem sağlığı ve performans izleme için kullanılabilir.
        /// </summary>
        public DateTime LastSuccessfulRefresh => _lastSuccessfulRefresh;

        /// <summary>
        /// Varsayılan MongoDB repository'sini oluşturur
        /// 
        /// Bu method, özel repository sağlanmadığında varsayılan MongoDB repository'sini
        /// oluşturmak için kullanılır.
        /// </summary>
        /// <param name="connectionString">MongoDB bağlantı dizesi</param>
        /// <returns>Yapılandırılmış MongoDB repository örneği</returns>
        private IConfigurationRepository CreateDefaultRepository(string connectionString)
        {
            // MongoDB ayarlarını oluştur
            var settings = new MongoDbSettings
            {
                ConnectionString = connectionString,
                DatabaseName = "ConfigurationDb",
                CollectionName = "Configurations"
            };

            // Repository için uygun logger tipini oluştur
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var repositoryLogger = loggerFactory.CreateLogger<MongoConfigurationRepository>();

            return new MongoConfigurationRepository(Options.Create(settings), repositoryLogger);
        }

        /// <summary>
        /// Konfigürasyonları depolamadan asenkron olarak yükler
        /// 
        /// Bu method, repository'den aktif konfigürasyonları alır ve cache'i günceller.
        /// Thread-safe çalışır ve hata durumlarında son başarılı değerleri korur.
        /// </summary>
        private async Task LoadConfigurationsAsync()
        {
            try
            {
                // Cache yazma kilidini al
                _cacheLock.EnterWriteLock();

                // Repository'den aktif konfigürasyonları al
                var configurations = await _repository.GetActiveConfigurationsAsync(_applicationName);

                // Mevcut cache'leri temizle
                _configurationCache.Clear();
                _typedValueCache.Clear();

                // Yeni konfigürasyonları yükle
                foreach (var config in configurations)
                {
                    _configurationCache[config.Name] = config;
                }

                // Başarılı yenileme bilgilerini güncelle
                _lastSuccessfulRefresh = DateTime.UtcNow;
                _isRepositoryHealthy = true;

                _logger.LogInformation("Başarıyla yüklendi - {Count} konfigürasyon, Uygulama: {ApplicationName}",
                    _configurationCache.Count, _applicationName);
            }
            catch (Exception ex)
            {
                // Hata durumunda sağlık durumunu güncelle
                _isRepositoryHealthy = false;
                _logger.LogError(ex, "Konfigürasyonlar yüklenemedi - Uygulama: {ApplicationName}. Mevcut cache değerleri kullanılıyor.",
                    _applicationName);
            }
            finally
            {
                // Cache yazma kilidini serbest bırak
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Timer callback method - Konfigürasyonları yeniler
        /// 
        /// Bu method, Timer tarafından periyodik olarak çağrılır ve
        /// konfigürasyonları asenkron olarak yeniler.
        /// </summary>
        /// <param name="state">Timer state parametresi (kullanılmaz)</param>
        private void RefreshConfigurations(object? state)
        {
            Task.Run(() => LoadConfigurationsAsync());
        }

        /// <summary>
        /// Gerçek zamanlı konfigürasyon değişikliklerine abone olur
        /// 
        /// Bu method, RabbitMQ üzerinden gelen konfigürasyon değişikliklerini
        /// dinlemek için mesaj abonesine abone olur.
        /// </summary>
        private async Task SubscribeToConfigurationChangesAsync()
        {
            if (_messageSubscriber == null)
            {
                _logger.LogWarning("Gerçek zamanlı güncellemeler için mesaj abonesi mevcut değil");
                return;
            }

            try
            {
                await _messageSubscriber.SubscribeToConfigurationChangesAsync(_applicationName, OnConfigurationChanged);
                _logger.LogInformation("Gerçek zamanlı konfigürasyon değişikliklerine abone olundu - Uygulama: {ApplicationName}", _applicationName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon değişikliklerine abone olunamadı - Uygulama: {ApplicationName}", _applicationName);
            }
        }

        /// <summary>
        /// Konfigürasyon değişiklik olayını işler
        /// 
        /// Bu method, RabbitMQ'dan gelen konfigürasyon değişiklik olaylarını işler.
        /// Değişiklik tipine göre cache'i günceller veya yeniden yükler.
        /// </summary>
        /// <param name="changeEvent">Konfigürasyon değişiklik olayı</param>
        private async Task OnConfigurationChanged(ConfigurationChangeEvent changeEvent)
        {
            try
            {
                _logger.LogInformation("Konfigürasyon değişiklik olayı alındı: {ChangeType} - {ConfigurationName}",
                    changeEvent.ChangeType, changeEvent.ConfigurationName);

                switch (changeEvent.ChangeType)
                {
                    case ConfigurationChangeType.Created:
                    case ConfigurationChangeType.Updated:
                        // Belirli konfigürasyonu veya tüm konfigürasyonları yeniden yükle
                        await LoadConfigurationsAsync();
                        break;

                    case ConfigurationChangeType.Deleted:
                        // Cache'den kaldır
                        _cacheLock.EnterWriteLock();
                        try
                        {
                            _configurationCache.TryRemove(changeEvent.ConfigurationName, out _);
                            _typedValueCache.TryRemove(changeEvent.ConfigurationName, out _);
                            _logger.LogInformation("Konfigürasyon cache'den kaldırıldı: {ConfigurationName}", changeEvent.ConfigurationName);
                        }
                        finally
                        {
                            _cacheLock.ExitWriteLock();
                        }
                        break;

                    case ConfigurationChangeType.StatusChanged:
                        // Güncellenmiş durumu almak için konfigürasyonları yeniden yükle
                        await LoadConfigurationsAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon değişiklik olayı işlenirken hata - {ConfigurationName}",
                    changeEvent.ConfigurationName);
            }
        }

        /// <summary>
        /// String değeri belirtilen tipe dönüştürür
        /// 
        /// Bu method, konfigürasyon değerlerini güçlü tipli değerlere dönüştürür.
        /// Tip uyumluluğunu kontrol eder ve güvenli dönüştürme yapar.
        /// </summary>
        /// <typeparam name="T">Hedef tip</typeparam>
        /// <param name="value">Dönüştürülecek string değer</param>
        /// <param name="configType">Konfigürasyon tipi</param>
        /// <returns>Dönüştürülmüş değer</returns>
        private T ConvertToType<T>(string value, ConfigurationType configType)
        {
            try
            {
                // Tip uyumluluğunu doğrula
                var targetType = typeof(T);
                ValidateTypeCompatibility(configType, targetType);

                var convertedValue = Convert.ChangeType(value, targetType);
                return (T)convertedValue!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Type dönüştürme başarısız - Değer: '{Value}', Type: {Type}", value, typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Konfigürasyon tipi ile hedef tip arasındaki uyumluluğu doğrular
        /// 
        /// Bu method, güvenli tip dönüştürme için tip uyumluluğunu kontrol eder.
        /// </summary>
        /// <param name="configType">Konfigürasyon tipi</param>
        /// <param name="targetType">Hedef tip</param>
        /// <exception cref="InvalidCastException">Tip uyumsuzluğu durumunda fırlatılır</exception>
        private void ValidateTypeCompatibility(ConfigurationType configType, Type targetType)
        {
            var isCompatible = configType switch
            {
                ConfigurationType.String => targetType == typeof(string),
                ConfigurationType.Int => targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(short),
                ConfigurationType.Double => targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal),
                ConfigurationType.Bool => targetType == typeof(bool),
                _ => false
            };

            if (!isCompatible)
            {
                throw new InvalidCastException($"Konfigürasyon tipi {configType} {targetType.Name} tipine dönüştürülemez");
            }
        }

        /// <summary>
        /// ConfigurationReader kaynaklarını serbest bırakır
        /// 
        /// Bu method, timer, cache lock ve mesaj abonesi gibi kaynakları
        /// güvenli bir şekilde serbest bırakır.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Kaynakları serbest bırakır (protected virtual)
        /// 
        /// Bu method, IDisposable pattern'ini uygular ve kaynakları
        /// güvenli bir şekilde temizler.
        /// </summary>
        /// <param name="disposing">Managed kaynakların serbest bırakılıp bırakılmayacağı</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                // Timer'ı serbest bırak
                _refreshTimer?.Dispose();
                
                // Cache lock'ı serbest bırak
                _cacheLock?.Dispose();
                
                // Mesaj abonesini serbest bırak
                _messageSubscriber?.Dispose();
            }

            _isDisposed = true;
        }
    }
}
