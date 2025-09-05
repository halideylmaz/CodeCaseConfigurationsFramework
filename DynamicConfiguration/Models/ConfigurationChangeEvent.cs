using System.Text.Json.Serialization;

namespace DynamicConfiguration.Models
{
    /// <summary>
    /// Konfigürasyon Değişiklik Olayı - Mesaj broker'a yayınlanan konfigürasyon değişiklik olayını temsil eder
    /// 
    /// Bu sınıf, konfigürasyon kayıtlarında yapılan değişiklikleri (oluşturma, güncelleme, silme)
    /// diğer servislere bildirmek için kullanılır. RabbitMQ üzerinden yayınlanır ve
    /// gerçek zamanlı konfigürasyon güncellemelerini sağlar.
    /// 
    /// Özellikler:
    /// - Değişiklik tipi (Created, Updated, Deleted, StatusChanged)
    /// - Uygulama ve konfigürasyon bilgileri
    /// - Değişiklik zamanı ve metadata
    /// - JSON serialization desteği
    /// </summary>
    public class ConfigurationChangeEvent
    {
        /// <summary>
        /// Konfigürasyon değişikliğinin tipi (Oluşturuldu, Güncellendi, Silindi, Durum Değişti)
        /// </summary>
        [JsonPropertyName("changeType")]
        public ConfigurationChangeType ChangeType { get; set; }

        /// <summary>
        /// Bu konfigürasyonun sahibi olan uygulamanın adı
        /// </summary>
        [JsonPropertyName("applicationName")]
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>
        /// Konfigürasyon adı/anahtarı
        /// </summary>
        [JsonPropertyName("configurationName")]
        public string ConfigurationName { get; set; } = string.Empty;

        /// <summary>
        /// Konfigürasyon değeri (oluşturma/güncelleme işlemleri için)
        /// </summary>
        [JsonPropertyName("configurationValue")]
        public string? ConfigurationValue { get; set; }

        /// <summary>
        /// Konfigürasyon tipi (oluşturma/güncelleme işlemleri için)
        /// </summary>
        [JsonPropertyName("configurationType")]
        public ConfigurationType? ConfigurationType { get; set; }

        /// <summary>
        /// Konfigürasyonun aktif olup olmadığı (oluşturma/güncelleme işlemleri için)
        /// </summary>
        [JsonPropertyName("isActive")]
        public bool? IsActive { get; set; }

        /// <summary>
        /// Değişikliğin gerçekleştiği zaman damgası (UTC)
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Konfigürasyon kaydının benzersiz kimliği
        /// </summary>
        [JsonPropertyName("configurationId")]
        public string? ConfigurationId { get; set; }

        /// <summary>
        /// Değişiklik hakkında ek metadata bilgileri
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Konfigürasyon Değişiklik Tipleri - Konfigürasyon kayıtlarında yapılabilecek değişiklik türlerini tanımlar
    /// </summary>
    public enum ConfigurationChangeType
    {
        /// <summary>
        /// Yeni bir konfigürasyon oluşturuldu
        /// </summary>
        Created = 1,

        /// <summary>
        /// Mevcut bir konfigürasyon güncellendi
        /// </summary>
        Updated = 2,

        /// <summary>
        /// Bir konfigürasyon silindi
        /// </summary>
        Deleted = 3,

        /// <summary>
        /// Bir konfigürasyonun aktiflik durumu değiştirildi (aktif/pasif)
        /// </summary>
        StatusChanged = 4
    }
}
