using DynamicConfiguration.Models;

namespace DynamicConfiguration.Messaging
{
    /// <summary>Mesaj yayınlayıcı arayüzü.</summary>
    public interface IMessagePublisher
    {
        /// <summary>Konfigürasyon değişiklik olayını yayınlar.</summary>
        Task PublishConfigurationChangeAsync(ConfigurationChangeEvent changeEvent);

        /// <summary>Yeniden deneme ile yayınlar.</summary>
        Task PublishConfigurationChangeAsync(ConfigurationChangeEvent changeEvent, int retryCount);

        /// <summary>Publisher sağlığını kontrol eder.</summary>
        Task<bool> IsHealthyAsync();

        /// <summary>Publisher'ı dispose eder.</summary>
        void Dispose();
    }
}
