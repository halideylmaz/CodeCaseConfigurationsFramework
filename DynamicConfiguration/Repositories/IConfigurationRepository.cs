using DynamicConfiguration.Models;

namespace DynamicConfiguration.Repositories
{
    /// <summary>Konfigürasyon repository arayüzü.</summary>
    public interface IConfigurationRepository
    {
        /// <summary>Uygulama için aktif konfigürasyonları getirir.</summary>
        Task<IEnumerable<ConfigurationRecord>> GetActiveConfigurationsAsync(string applicationName, CancellationToken cancellationToken = default);

        /// <summary>Tek konfigürasyon getirir.</summary>
        Task<ConfigurationRecord?> GetConfigurationAsync(string applicationName, string name, CancellationToken cancellationToken = default);

        /// <summary>Yeni konfigürasyon ekler.</summary>
        Task<ConfigurationRecord> AddConfigurationAsync(ConfigurationRecord record, CancellationToken cancellationToken = default);

        /// <summary>Konfigürasyon günceller.</summary>
        Task<bool> UpdateConfigurationAsync(string id, ConfigurationRecord record, CancellationToken cancellationToken = default);

        /// <summary>Konfigürasyon siler.</summary>
        Task<bool> DeleteConfigurationAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>Tüm konfigürasyonları getirir.</summary>
        Task<IEnumerable<ConfigurationRecord>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default);

        /// <summary>ID ile konfigürasyon getirir.</summary>
        Task<ConfigurationRecord?> GetConfigurationByIdAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>Repository sağlığını kontrol eder.</summary>
        Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    }
}


