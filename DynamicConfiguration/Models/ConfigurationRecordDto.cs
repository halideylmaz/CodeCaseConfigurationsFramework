using System.ComponentModel.DataAnnotations;

namespace DynamicConfiguration.Models
{
    /// <summary>
    /// Konfigürasyon Kaydı Veri Transfer Nesnesi (DTO) - API istekleri için konfigürasyon verilerini taşır
    /// 
    /// Bu sınıf, konfigürasyon kayıtlarının API üzerinden alınması ve gönderilmesi için
    /// kullanılan veri transfer nesnesidir. Validation attribute'ları ile veri doğrulama
    /// sağlar ve güvenli veri transferi yapar.
    /// 
    /// Özellikler:
    /// - Veri doğrulama (Required, StringLength)
    /// - API uyumlu veri yapısı
    /// - Güvenli veri transferi
    /// - Otomatik zaman damgası atama
    /// </summary>
    public class ConfigurationRecordDto
    {
        /// <summary>
        /// Konfigürasyon kaydının benzersiz kimliği (güncelleme için gerekli)
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Konfigürasyon adı/anahtarı (1-100 karakter arası, zorunlu)
        /// </summary>
        [Required(ErrorMessage = "Konfigürasyon adı gereklidir")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Konfigürasyon adı 1-100 karakter arası olmalıdır")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Konfigürasyon değerinin veri tipi (zorunlu)
        /// </summary>
        [Required(ErrorMessage = "Konfigürasyon tipi gereklidir")]
        public ConfigurationType Type { get; set; }

        /// <summary>
        /// Konfigürasyon değeri (zorunlu)
        /// </summary>
        [Required(ErrorMessage = "Konfigürasyon değeri gereklidir")]
        public string Value { get; set; } = null!;

        /// <summary>
        /// Konfigürasyonun aktif olup olmadığı (varsayılan: true)
        /// </summary>
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


