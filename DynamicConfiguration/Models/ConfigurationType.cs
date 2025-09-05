namespace DynamicConfiguration.Models
{
    /// <summary>
    /// Desteklenen Konfigürasyon Değer Tipleri - Konfigürasyon değerlerinin hangi veri tiplerinde olabileceğini tanımlar
    /// 
    /// Bu enum, konfigürasyon değerlerinin desteklenen veri tiplerini belirtir.
    /// Her tip, güçlü tipli dönüştürme işlemleri için kullanılır ve
    /// tip güvenliği sağlar.
    /// </summary>
    public enum ConfigurationType
    {
        /// <summary>
        /// string tipinde konfigürasyon değeri
        /// </summary>
        String,

        /// <summary>
        /// int tipinde konfigürasyon değeri
        /// </summary>
        Int,

        /// <summary>
        /// double tipinde konfigürasyon değeri
        /// </summary>
        Double,

        /// <summary>
        /// bool tipinde konfigürasyon değeri
        /// </summary>
        Bool
    }
}


