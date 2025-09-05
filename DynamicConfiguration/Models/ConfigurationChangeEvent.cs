using System.Text.Json.Serialization;

namespace DynamicConfiguration.Models
{
    /// <summary>Konfigürasyon değişiklik olayı.</summary>
    public class ConfigurationChangeEvent
    {
        /// <summary>Değişiklik tipi</summary>
        [JsonPropertyName("changeType")]
        public ConfigurationChangeType ChangeType { get; set; }

        /// <summary>Uygulama adı</summary>
        [JsonPropertyName("applicationName")]
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>Konfigürasyon adı</summary>
        [JsonPropertyName("configurationName")]
        public string ConfigurationName { get; set; } = string.Empty;

        /// <summary>Konfigürasyon değeri</summary>
        [JsonPropertyName("configurationValue")]
        public string? ConfigurationValue { get; set; }

        /// <summary>Konfigürasyon tipi</summary>
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

        /// <summary>Konfigürasyon ID</summary>
        [JsonPropertyName("configurationId")]
        public string? ConfigurationId { get; set; }

        /// <summary>Metadata</summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>Konfigürasyon değişiklik tipleri.</summary>
    public enum ConfigurationChangeType
    {
        /// <summary>Oluşturuldu</summary>
        Created = 1,

        /// <summary>Güncellendi</summary>
        Updated = 2,

        /// <summary>Silindi</summary>
        Deleted = 3,

        /// <summary>Durum değişti</summary>
        StatusChanged = 4
    }
}
