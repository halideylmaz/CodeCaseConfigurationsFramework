using DynamicConfiguration.Models;

namespace DynamicConfiguration.Repositories
{
    /// <summary>
    /// Konfigürasyon Veri Erişim Arayüzü - Konfigürasyon verilerine erişim için gerekli işlemleri tanımlar
    /// 
    /// Bu arayüz, konfigürasyon kayıtlarının veritabanı işlemlerini (CRUD) tanımlar.
    /// Repository pattern implementasyonu için kullanılır ve farklı veri kaynakları
    /// (MongoDB, SQL Server, Redis vb.) için uygulanabilir.
    /// 
    /// Özellikler:
    /// - Asenkron veri erişimi
    /// - CancellationToken desteği
    /// - Sağlık durumu kontrolü
    /// - Uygulama bazlı veri filtreleme
    /// </summary>
    public interface IConfigurationRepository
    {
        /// <summary>
        /// Belirli bir uygulama için tüm aktif konfigürasyon kayıtlarını getirir
        /// </summary>
        /// <param name="applicationName">Uygulama adı</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Aktif konfigürasyon kayıtları koleksiyonu</returns>
        Task<IEnumerable<ConfigurationRecord>> GetActiveConfigurationsAsync(string applicationName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uygulama adı ve konfigürasyon adına göre belirli bir konfigürasyon kaydını getirir
        /// </summary>
        /// <param name="applicationName">Uygulama adı</param>
        /// <param name="name">Konfigürasyon adı</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Konfigürasyon kaydı veya null</returns>
        Task<ConfigurationRecord?> GetConfigurationAsync(string applicationName, string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Yeni bir konfigürasyon kaydı ekler
        /// </summary>
        /// <param name="record">Eklenecek konfigürasyon kaydı</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Eklenen konfigürasyon kaydı</returns>
        Task<ConfigurationRecord> AddConfigurationAsync(ConfigurationRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mevcut bir konfigürasyon kaydını günceller
        /// </summary>
        /// <param name="id">Güncellenecek kaydın kimliği</param>
        /// <param name="record">Güncellenmiş konfigürasyon kaydı</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Güncelleme başarılı ise true</returns>
        Task<bool> UpdateConfigurationAsync(string id, ConfigurationRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir konfigürasyon kaydını siler
        /// </summary>
        /// <param name="id">Silinecek kaydın kimliği</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Silme başarılı ise true</returns>
        Task<bool> DeleteConfigurationAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tüm konfigürasyon kayıtlarını getirir (yönetici amaçlı)
        /// </summary>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Tüm konfigürasyon kayıtları koleksiyonu</returns>
        Task<IEnumerable<ConfigurationRecord>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Kimliğe göre bir konfigürasyon kaydını getirir
        /// </summary>
        /// <param name="id">Konfigürasyon kaydı kimliği</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Konfigürasyon kaydı veya null</returns>
        Task<ConfigurationRecord?> GetConfigurationByIdAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Repository'nin sağlıklı ve erişilebilir olup olmadığını kontrol eder
        /// </summary>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Sağlıklı ise true</returns>
        Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    }
}


