using DynamicConfiguration.Models;

namespace DynamicConfiguration.Messaging
{
    /// <summary>Mesaj abonesi arayüzü.</summary>
    public interface IMessageSubscriber
    {
        /// <summary>Konfigürasyon değişikliklerine abone olur.</summary>
        Task SubscribeToConfigurationChangesAsync(string applicationName, Func<ConfigurationChangeEvent, Task> onConfigurationChanged);

        /// <summary>Abonelikten çıkar.</summary>
        Task UnsubscribeFromConfigurationChangesAsync(string applicationName);

        /// <summary>Subscriber sağlığını kontrol eder.</summary>
        Task<bool> IsHealthyAsync();

        /// <summary>Subscriber'ı dispose eder.</summary>
        void Dispose();
    }
}
