using System.ComponentModel.DataAnnotations;

namespace DynamicConfiguration.Models
{
    /// <summary>
    /// API istekleri için konfigürasyon DTO'sı. Validation dahil.
    /// </summary>
    public class ConfigurationRecordDto
    {
        /// <summary>ID (güncelleme için)</summary>
        public string? Id { get; set; }

        /// <summary>Konfigürasyon adı</summary>
        [Required(ErrorMessage = "Konfigürasyon adı gereklidir")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Konfigürasyon adı 1-100 karakter arası olmalıdır")]
        public string Name { get; set; } = null!;

        /// <summary>Veri tipi</summary>
        [Required(ErrorMessage = "Konfigürasyon tipi gereklidir")]
        public ConfigurationType Type { get; set; }

        /// <summary>Değer</summary>
        [Required(ErrorMessage = "Konfigürasyon değeri gereklidir")]
        public string Value { get; set; } = null!;

        /// <summary>Aktif durumu</summary>
        [Required(ErrorMessage = "Aktiflik durumu gereklidir")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Bu konfigürasyonun ait olduğu uygulamanın adı (1-100 karakter arası, zorunlu)
        /// </summary>
        [Required(ErrorMessage = "Uygulama adı gereklidir")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Uygulama adı 1-100 karakter arası olmalıdır")]
        public string ApplicationName { get; set; } = null!;

        /// <summary>
        /// Konfigürasyon kaydının oluşturulma zamanı (otomatik atanır)
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Konfigürasyon kaydının son güncellenme zamanı (otomatik atanır)
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}


