using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DynamicConfiguration.Models
{
    /// <summary>
    /// MongoDB'de saklanan konfigürasyon kaydı.
    /// </summary>
    public class ConfigurationRecord
    {
        /// <summary>Unique ID</summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        /// <summary>Konfigürasyon adı</summary>
        [BsonElement("name")]
        public string Name { get; set; } = null!;

        /// <summary>Data type</summary>
        [BsonElement("type")]
        public ConfigurationType Type { get; set; }

        /// <summary>Değer (string olarak saklanır)</summary>
        [BsonElement("value")]
        public string Value { get; set; } = null!;

        /// <summary>Aktif mi?</summary>
        [BsonElement("isActive")]
        public bool IsActive { get; set; }

        /// <summary>Hangi uygulamaya ait
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


