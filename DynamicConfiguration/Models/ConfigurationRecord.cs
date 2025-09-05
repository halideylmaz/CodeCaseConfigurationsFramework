using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DynamicConfiguration.Models
{
    /// <summary>
    /// Konfigürasyon Kaydı - Veritabanında saklanan konfigürasyon verilerini temsil eder
    /// 
    /// Bu sınıf, MongoDB'de saklanan konfigürasyon kayıtlarının yapısını tanımlar.
    /// Her konfigürasyon kaydı, belirli bir uygulamaya ait olan ve belirli bir tipte
    /// değer içeren konfigürasyon bilgilerini barındırır.
    /// 
    /// Özellikler:
    /// - Benzersiz kimlik (ObjectId)
    /// - Konfigürasyon adı ve değeri
    /// - Veri tipi (String, Int, Double, Bool)
    /// - Aktiflik durumu
    /// - Uygulama adı
    /// - Oluşturulma ve güncellenme zamanları
    /// </summary>
    public class ConfigurationRecord
    {
        /// <summary>
        /// Konfigürasyon kaydının benzersiz kimliği (MongoDB ObjectId)
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        /// <summary>
        /// Konfigürasyon adı/anahtarı (örn: "SiteName", "MaxItemCount")
        /// </summary>
        [BsonElement("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Konfigürasyon değerinin veri tipi (String, Int, Double, Bool)
        /// </summary>
        [BsonElement("type")]
        public ConfigurationType Type { get; set; }

        /// <summary>
        /// Konfigürasyon değeri (string formatında saklanır, tip dönüşümü ile kullanılır)
        /// </summary>
        [BsonElement("value")]
        public string Value { get; set; } = null!;

        /// <summary>
        /// Konfigürasyonun aktif olup olmadığı (sadece aktif kayıtlar kullanılır)
        /// </summary>
        [BsonElement("isActive")]
        public bool IsActive { get; set; }

        /// <summary>
        /// Bu konfigürasyonun ait olduğu uygulamanın adı (örn: "SERVICE-A")
        /// </summary>
        [BsonElement("applicationName")]
        public string ApplicationName { get; set; } = null!;

        /// <summary>
        /// Konfigürasyon kaydının oluşturulma zamanı (UTC)
        /// </summary>
        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Konfigürasyon kaydının son güncellenme zamanı (UTC)
        /// </summary>
        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}


