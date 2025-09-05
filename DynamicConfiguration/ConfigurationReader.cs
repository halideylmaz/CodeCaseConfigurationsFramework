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
    /// Konfigürasyonları veritabanından okur ve cache'ler.
    /// Otomatik yenileme ve real-time güncellemeler destekler.
    /// </summary>
    public class ConfigurationReader : IDisposable
    {
        private readonly string _applicationName;
        private readonly int _refreshTimerIntervalInMs;
        private readonly ILogger<ConfigurationReader> _logger;
        private readonly IConfigurationRepository _repository;
        private readonly Timer _refreshTimer;
        private readonly IMessageSubscriber? _messageSubscriber;

        // Cache yapıları
        private readonly ConcurrentDictionary<string, ConfigurationRecord> _configurationCache = new();
        private readonly ConcurrentDictionary<string, object> _typedValueCache = new();
        private readonly ReaderWriterLockSlim _cacheLock = new();
        private bool _isDisposed;

        // Sağlık durumu
        private volatile bool _isRepositoryHealthy = true;
        private DateTime _lastSuccessfulRefresh = DateTime.MinValue;

        /// <summary>
        /// Basit kullanım için constructor. Varsayılan ayarlarla çalışır.
        /// </summary>
        /// <param name="applicationName">Uygulama adı</param>
        /// <param name="connectionString">MongoDB connection string</param>
        /// <param name="refreshTimerIntervalInMs">Yenileme aralığı (ms)</param>
        public ConfigurationReader(string applicationName, string connectionString, int refreshTimerIntervalInMs)
            : this(applicationName, connectionString, refreshTimerIntervalInMs, null, null, null, false)
        {
        }

        /// <summary>
        /// Gelişmiş constructor. Özel logger, repository ve message subscriber ile çalışır.
        /// </summary>
        public ConfigurationReader(
            string applicationName,
            string connectionString,
            int refreshTimerIntervalInMs,
            ILoggerFactory? loggerFactory = null,
            IConfigurationRepository? repository = null,
            IMessageSubscriber? messageSubscriber = null,
            bool loadSynchronously = false)
        {
            // Parameter validation
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            _refreshTimerIntervalInMs = Math.Max(1000, refreshTimerIntervalInMs); // Minimum 1 saniye

            // Logger setup
            var factory = loggerFactory ?? LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            _logger = factory.CreateLogger<ConfigurationReader>();

            // Repository setup  
            _repository = repository ?? CreateDefaultRepository(connectionString);
            
            // Message subscriber setup
            _messageSubscriber = messageSubscriber;

            // İlk cache yüklemesi
            if (loadSynchronously)
            {
                // Test için senkron yükleme
                Task.Run(() => LoadConfigurationsAsync()).Wait();
            }
            else
            {
                // Normal async yükleme
                _ = Task.Run(() => LoadConfigurationsAsync());
            }

            // Timer başlat
            _refreshTimer = new Timer(RefreshConfigurations, null, _refreshTimerIntervalInMs, _refreshTimerIntervalInMs);

            // Real-time update subscription
            if (_messageSubscriber != null)
            {
                _ = Task.Run(() => SubscribeToConfigurationChangesAsync());
            }

            _logger.LogInformation("ConfigurationReader başlatıldı - Uygulama: {ApplicationName}, Yenileme Aralığı: {Interval}ms",
                _applicationName, _refreshTimerIntervalInMs);
        }

        /// <summary>
        /// Konfigürasyon değerini belirtilen tipte getirir.
        /// </summary>
        public T GetValue<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key boş olamaz", nameof(key));

            // Önce cache'den bak
            if (_typedValueCache.TryGetValue(key, out var cachedValue) && cachedValue is T typedValue)
            {
                return typedValue;
            }

            // Cache'de yoksa configuration'dan al
            if (!_configurationCache.TryGetValue(key, out var record))
            {
                _logger.LogWarning("Konfigürasyon anahtarı '{Key}' uygulama '{ApplicationName}' için bulunamadı", key, _applicationName);
                throw new KeyNotFoundException($"Konfigürasyon anahtarı '{key}' bulunamadı");
            }

            // Type conversion ve cache'e kaydet
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

        /// <summary>Değeri getirir, yoksa default döner.</summary>
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

        /// <summary>Key var mı kontrol eder.</summary>
        public bool HasKey(string key)
        {
            return _configurationCache.ContainsKey(key);
        }

        /// <summary>Tüm key'leri getirir.</summary>
        public IEnumerable<string> GetAllKeys()
        {
            return _configurationCache.Keys.ToList();
        }

        /// <summary>Cache'i manuel yeniler.</summary>
        public async Task RefreshAsync()
        {
            await LoadConfigurationsAsync();
        }

        /// <summary>Sağlık durumunu kontrol eder.</summary>
        public bool IsHealthy()
        {
            return _isRepositoryHealthy && _configurationCache.Any();
        }

        /// <summary>Son başarılı yenileme zamanı.</summary>
        public DateTime LastSuccessfulRefresh => _lastSuccessfulRefresh;

        /// <summary>Default MongoDB repository oluşturur.</summary>
        private IConfigurationRepository CreateDefaultRepository(string connectionString)
        {
            // MongoDB settings
            var settings = new MongoDbSettings
            {
                ConnectionString = connectionString,
                DatabaseName = "ConfigurationDb",
                CollectionName = "Configurations"
            };

            // Logger factory
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var repositoryLogger = loggerFactory.CreateLogger<MongoConfigurationRepository>();

            return new MongoConfigurationRepository(Options.Create(settings), repositoryLogger);
        }

        /// <summary>Repository'den konfigürasyonları yükler ve cache'i günceller.</summary>
        private async Task LoadConfigurationsAsync()
        {
            try
            {
                // Write lock al
                _cacheLock.EnterWriteLock();

                // Repository'den aktif config'leri al
                var configurations = await _repository.GetActiveConfigurationsAsync(_applicationName);

                // Cache'leri temizle
                _configurationCache.Clear();
                _typedValueCache.Clear();

                // Yeni config'leri yükle
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
                // Hata durumunda health status güncelle
                _isRepositoryHealthy = false;
                _logger.LogError(ex, "Konfigürasyonlar yüklenemedi - Uygulama: {ApplicationName}. Mevcut cache değerleri kullanılıyor.",
                    _applicationName);
            }
            finally
            {
                // Write lock serbest bırak
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>Timer callback - config'leri yeniler.</summary>
        private void RefreshConfigurations(object? state)
        {
            Task.Run(() => LoadConfigurationsAsync());
        }

        /// <summary>Real-time config değişikliklerine abone olur.</summary>
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

        /// <summary>Config değişiklik olayını işler.</summary>
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
                        // Config'leri yeniden yükle
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
                        // Status değişikliği için config'leri yeniden yükle
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

        /// <summary>String değeri belirtilen tipe dönüştürür.</summary>
        private T ConvertToType<T>(string value, ConfigurationType configType)
        {
            try
            {
                // Type compatibility check
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

        /// <summary>Type compatibility kontrol eder.</summary>
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

        /// <summary>Kaynakları temizler.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Dispose pattern implementation.</summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                // Timer'ı dispose et
                _refreshTimer?.Dispose();
                
                // Cache lock'ı dispose et
                _cacheLock?.Dispose();
                
                // Message subscriber'ı dispose et
                _messageSubscriber?.Dispose();
            }

            _isDisposed = true;
        }
    }
}
