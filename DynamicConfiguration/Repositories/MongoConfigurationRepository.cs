using DynamicConfiguration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace DynamicConfiguration.Repositories
{
    /// <summary>
    /// MongoDB Konfigürasyon Repository Implementasyonu - MongoDB veritabanı ile konfigürasyon verilerine erişim sağlar
    /// 
    /// Bu sınıf, IConfigurationRepository arayüzünün MongoDB implementasyonudur.
    /// Konfigürasyon kayıtlarının MongoDB'de saklanması, alınması, güncellenmesi ve
    /// silinmesi işlemlerini gerçekleştirir.
    /// 
    /// Özellikler:
    /// - MongoDB bağlantı yönetimi
    /// - Performans için index oluşturma
    /// - Asenkron veri işlemleri
    /// - Hata yönetimi ve loglama
    /// - Sağlık durumu kontrolü
    /// </summary>
    public class MongoConfigurationRepository : IConfigurationRepository
    {
        // ========================================
        // PRIVATE FIELDS
        // ========================================
        private readonly IMongoCollection<ConfigurationRecord> _collection;  // MongoDB koleksiyonu
        private readonly ILogger<MongoConfigurationRepository> _logger;      // Loglama servisi

        /// <summary>
        /// MongoDB Konfigürasyon Repository'sini başlatır
        /// 
        /// Bu constructor, MongoDB bağlantısını kurar, veritabanı ve koleksiyonu
        /// yapılandırır ve performans için gerekli indexleri oluşturur.
        /// </summary>
        /// <param name="settings">MongoDB bağlantı ayarları</param>
        /// <param name="logger">Loglama servisi</param>
        /// <exception cref="Exception">MongoDB bağlantısı kurulamazsa fırlatılır</exception>
        public MongoConfigurationRepository(
            IOptions<MongoDbSettings> settings,
            ILogger<MongoConfigurationRepository> logger)
        {
            _logger = logger;

            try
            {
                // MongoDB istemcisini oluştur
                var client = new MongoClient(settings.Value.ConnectionString);
                var database = client.GetDatabase(settings.Value.DatabaseName);
                _collection = database.GetCollection<ConfigurationRecord>(settings.Value.CollectionName);

                // Performans için index oluştur
                var indexKeys = Builders<ConfigurationRecord>.IndexKeys
                    .Ascending(x => x.ApplicationName)
                    .Ascending(x => x.Name)
                    .Ascending(x => x.IsActive);

                _collection.Indexes.CreateOne(new CreateIndexModel<ConfigurationRecord>(indexKeys));
                
                _logger.LogInformation("MongoDB Konfigürasyon Repository başlatıldı - Veritabanı: {DatabaseName}, Koleksiyon: {CollectionName}", 
                    settings.Value.DatabaseName, settings.Value.CollectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MongoDB bağlantısı başlatılamadı");
                throw;
            }
        }

        /// <summary>
        /// Belirli bir uygulama için tüm aktif konfigürasyon kayıtlarını getirir
        /// </summary>
        /// <param name="applicationName">Uygulama adı</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Aktif konfigürasyon kayıtları koleksiyonu</returns>
        public async Task<IEnumerable<ConfigurationRecord>> GetActiveConfigurationsAsync(string applicationName, CancellationToken cancellationToken = default)
        {
            try
            {
                // Uygulama adı ve aktiflik durumuna göre filtre oluştur
                var filter = Builders<ConfigurationRecord>.Filter.And(
                    Builders<ConfigurationRecord>.Filter.Eq(x => x.ApplicationName, applicationName),
                    Builders<ConfigurationRecord>.Filter.Eq(x => x.IsActive, true)
                );

                // Konfigürasyonları veritabanından al
                var configurations = await _collection.Find(filter).ToListAsync(cancellationToken);
                _logger.LogInformation("Aktif konfigürasyonlar alındı - Uygulama: {ApplicationName}, Adet: {Count}", applicationName, configurations.Count);

                return configurations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Aktif konfigürasyonlar alınamadı - Uygulama: {ApplicationName}", applicationName);
                throw;
            }
        }

        /// <summary>
        /// Uygulama adı ve konfigürasyon adına göre belirli bir konfigürasyon kaydını getirir
        /// </summary>
        /// <param name="applicationName">Uygulama adı</param>
        /// <param name="name">Konfigürasyon adı</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Konfigürasyon kaydı veya null</returns>
        public async Task<ConfigurationRecord?> GetConfigurationAsync(string applicationName, string name, CancellationToken cancellationToken = default)
        {
            try
            {
                // Uygulama adı, konfigürasyon adı ve aktiflik durumuna göre filtre oluştur
                var filter = Builders<ConfigurationRecord>.Filter.And(
                    Builders<ConfigurationRecord>.Filter.Eq(x => x.ApplicationName, applicationName),
                    Builders<ConfigurationRecord>.Filter.Eq(x => x.Name, name),
                    Builders<ConfigurationRecord>.Filter.Eq(x => x.IsActive, true)
                );

                // Konfigürasyonu veritabanından al
                var configuration = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

                if (configuration != null)
                {
                    _logger.LogInformation("Konfigürasyon alındı - Ad: {Name}, Uygulama: {ApplicationName}", name, applicationName);
                }
                else
                {
                    _logger.LogWarning("Konfigürasyon bulunamadı - Ad: {Name}, Uygulama: {ApplicationName}", name, applicationName);
                }

                return configuration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon alınamadı - Ad: {Name}, Uygulama: {ApplicationName}", name, applicationName);
                throw;
            }
        }

        /// <summary>
        /// Yeni bir konfigürasyon kaydı ekler
        /// </summary>
        /// <param name="record">Eklenecek konfigürasyon kaydı</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Eklenen konfigürasyon kaydı</returns>
        public async Task<ConfigurationRecord> AddConfigurationAsync(ConfigurationRecord record, CancellationToken cancellationToken = default)
        {
            try
            {
                // Zaman damgalarını ayarla
                record.CreatedAt = DateTime.UtcNow;
                record.UpdatedAt = DateTime.UtcNow;

                // Konfigürasyonu veritabanına ekle
                await _collection.InsertOneAsync(record, cancellationToken: cancellationToken);
                _logger.LogInformation("Yeni konfigürasyon eklendi - Ad: {Name}, Uygulama: {ApplicationName}", record.Name, record.ApplicationName);

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon eklenemedi - Ad: {Name}, Uygulama: {ApplicationName}", record.Name, record.ApplicationName);
                throw;
            }
        }

        /// <summary>
        /// Mevcut bir konfigürasyon kaydını günceller
        /// </summary>
        /// <param name="id">Güncellenecek kaydın kimliği</param>
        /// <param name="record">Güncellenmiş konfigürasyon kaydı</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Güncelleme başarılı ise true</returns>
        public async Task<bool> UpdateConfigurationAsync(string id, ConfigurationRecord record, CancellationToken cancellationToken = default)
        {
            try
            {
                // Güncelleme zamanını ayarla
                record.UpdatedAt = DateTime.UtcNow;

                // Güncellenecek kaydı bul
                var filter = Builders<ConfigurationRecord>.Filter.Eq(x => x.Id, id);
                
                // Güncelleme işlemini tanımla
                var update = Builders<ConfigurationRecord>.Update
                    .Set(x => x.Name, record.Name)
                    .Set(x => x.Type, record.Type)
                    .Set(x => x.Value, record.Value)
                    .Set(x => x.IsActive, record.IsActive)
                    .Set(x => x.ApplicationName, record.ApplicationName)
                    .Set(x => x.UpdatedAt, record.UpdatedAt);

                // Güncelleme işlemini gerçekleştir
                var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

                if (result.ModifiedCount > 0)
                {
                    _logger.LogInformation("Konfigürasyon güncellendi - ID: {Id}", id);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Güncellenecek konfigürasyon bulunamadı - ID: {Id}", id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon güncellenemedi - ID: {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Bir konfigürasyon kaydını siler
        /// </summary>
        /// <param name="id">Silinecek kaydın kimliği</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Silme başarılı ise true</returns>
        public async Task<bool> DeleteConfigurationAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Silinecek kaydı bul
                var filterConfiguration = Builders<ConfigurationRecord>.Filter.Eq(x => x.Id, id);
                
                // Silme işlemini gerçekleştir
                var result = await _collection.DeleteOneAsync(filterConfiguration, cancellationToken: cancellationToken);

                if (result.DeletedCount > 0)
                {
                    _logger.LogInformation("Konfigürasyon silindi - ID: {Id}", id);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Silinecek konfigürasyon bulunamadı - ID: {Id}", id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon silinemedi - ID: {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Tüm konfigürasyon kayıtlarını getirir (yönetici amaçlı)
        /// </summary>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Tüm konfigürasyon kayıtları koleksiyonu</returns>
        public async Task<IEnumerable<ConfigurationRecord>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Tüm konfigürasyonları al (filtre yok)
                var configurations = await _collection.Find(_ => true).ToListAsync(cancellationToken);
                _logger.LogInformation("Tüm konfigürasyonlar alındı - Toplam adet: {Count}", configurations.Count);

                return configurations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm konfigürasyonlar alınamadı");
                throw;
            }
        }

        /// <summary>
        /// Kimliğe göre bir konfigürasyon kaydını getirir
        /// </summary>
        /// <param name="id">Konfigürasyon kaydı kimliği</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Konfigürasyon kaydı veya null</returns>
        public async Task<ConfigurationRecord?> GetConfigurationByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                // ID'ye göre filtre oluştur
                var filter = Builders<ConfigurationRecord>.Filter.Eq(x => x.Id, id);
                
                // Konfigürasyonu veritabanından al
                var configuration = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

                if (configuration != null)
                {
                    _logger.LogInformation("Konfigürasyon alındı - ID: {Id}", id);
                }
                else
                {
                    _logger.LogWarning("Konfigürasyon bulunamadı - ID: {Id}", id);
                }

                return configuration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon alınamadı - ID: {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Repository'nin sağlıklı ve erişilebilir olup olmadığını kontrol eder
        /// </summary>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Sağlıklı ise true</returns>
        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Basit sağlık kontrolü - döküman sayısını almaya çalış
                await _collection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);
                _logger.LogDebug("MongoDB Repository sağlık kontrolü başarılı");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MongoDB Repository sağlık kontrolü başarısız");
                return false;
            }
        }
    }

    /// <summary>
    /// MongoDB Ayarları - MongoDB bağlantı ve yapılandırma bilgilerini içerir
    /// 
    /// Bu sınıf, MongoDB bağlantısı için gerekli ayarları tanımlar.
    /// appsettings.json dosyasından yüklenir ve dependency injection ile
    /// repository'ye enjekte edilir.
    /// </summary>
    public class MongoDbSettings
    {
        /// <summary>
        /// MongoDB bağlantı dizesi (zorunlu)
        /// </summary>
        public string ConnectionString { get; set; } = null!;
        
        /// <summary>
        /// Veritabanı adı (varsayılan: "ConfigurationDb")
        /// </summary>
        public string DatabaseName { get; set; } = "ConfigurationDb";
        
        /// <summary>
        /// Koleksiyon adı (varsayılan: "Configurations")
        /// </summary>
        public string CollectionName { get; set; } = "Configurations";
    }
}


