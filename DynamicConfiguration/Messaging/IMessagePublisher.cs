using DynamicConfiguration.Models;

namespace DynamicConfiguration.Messaging
{
    /// <summary>
    /// Mesaj Yayınlayıcı Arayüzü - Konfigürasyon değişiklik olaylarını mesaj broker'a yayınlamak için kullanılır
    /// 
    /// Bu arayüz, konfigürasyon değişiklik olaylarının mesaj broker'a (RabbitMQ)
    /// yayınlanması için gerekli işlemleri tanımlar. Publisher pattern implementasyonu
    /// için kullanılır ve gerçek zamanlı bildirimler sağlar.
    /// 
    /// Özellikler:
    /// - Asenkron mesaj yayınlama
    /// - Yeniden deneme mekanizması
    /// - Sağlık durumu kontrolü
    /// - Kaynak yönetimi (IDisposable)
    /// </summary>
    public interface IMessagePublisher
    {
        /// <summary>
        /// Konfigürasyon değişiklik olayını asenkron olarak yayınlar
        /// </summary>
        /// <param name="changeEvent">Yayınlanacak konfigürasyon değişiklik olayı</param>
        /// <returns>Asenkron işlemi temsil eden task</returns>
        Task PublishConfigurationChangeAsync(ConfigurationChangeEvent changeEvent);

        /// <summary>
        /// Konfigürasyon değişiklik olayını yeniden deneme mantığı ile asenkron olarak yayınlar
        /// </summary>
        /// <param name="changeEvent">Yayınlanacak konfigürasyon değişiklik olayı</param>
        /// <param name="retryCount">Yeniden deneme sayısı</param>
        /// <returns>Asenkron işlemi temsil eden task</returns>
        Task PublishConfigurationChangeAsync(ConfigurationChangeEvent changeEvent, int retryCount);

        /// <summary>
        /// Mesaj yayınlayıcının sağlıklı ve bağlı olup olmadığını kontrol eder
        /// </summary>
        /// <returns>Sağlıklı ise true, aksi halde false</returns>
        Task<bool> IsHealthyAsync();

        /// <summary>
        /// Mesaj yayınlayıcıyı serbest bırakır ve bağlantıları kapatır
        /// </summary>
        void Dispose();
    }
}
