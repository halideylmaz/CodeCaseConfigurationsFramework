using DynamicConfiguration.Models;

namespace DynamicConfiguration.Messaging
{
    /// <summary>
    /// Mesaj Abonesi Arayüzü - Mesaj broker'dan konfigürasyon değişiklik olaylarına abone olmak için kullanılır
    /// 
    /// Bu arayüz, konfigürasyon değişiklik olaylarının mesaj broker'dan (RabbitMQ)
    /// dinlenmesi için gerekli işlemleri tanımlar. Subscriber pattern implementasyonu
    /// için kullanılır ve gerçek zamanlı güncellemeler sağlar.
    /// 
    /// Özellikler:
    /// - Asenkron mesaj dinleme
    /// - Uygulama bazlı abonelik yönetimi
    /// - Sağlık durumu kontrolü
    /// - Kaynak yönetimi (IDisposable)
    /// </summary>
    public interface IMessageSubscriber
    {
        /// <summary>
        /// Belirli bir uygulama için konfigürasyon değişiklik olaylarına abone olur
        /// </summary>
        /// <param name="applicationName">Abone olunacak uygulama adı</param>
        /// <param name="onConfigurationChanged">Konfigürasyon değiştiğinde çağrılacak callback</param>
        /// <returns>Asenkron işlemi temsil eden task</returns>
        Task SubscribeToConfigurationChangesAsync(string applicationName, Func<ConfigurationChangeEvent, Task> onConfigurationChanged);

        /// <summary>
        /// Belirli bir uygulama için konfigürasyon değişiklik olaylarından abonelikten çıkar
        /// </summary>
        /// <param name="applicationName">Abonelikten çıkılacak uygulama adı</param>
        /// <returns>Asenkron işlemi temsil eden task</returns>
        Task UnsubscribeFromConfigurationChangesAsync(string applicationName);

        /// <summary>
        /// Mesaj abonesinin sağlıklı ve bağlı olup olmadığını kontrol eder
        /// </summary>
        /// <returns>Sağlıklı ise true, aksi halde false</returns>
        Task<bool> IsHealthyAsync();

        /// <summary>
        /// Mesaj abonesini serbest bırakır ve bağlantıları kapatır
        /// </summary>
        void Dispose();
    }
}
