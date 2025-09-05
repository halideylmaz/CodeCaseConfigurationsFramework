namespace DynamicConfiguration.Messaging
{
    /// <summary>
    /// RabbitMQ Ayarları - RabbitMQ bağlantı ve yapılandırma bilgilerini içerir
    /// 
    /// Bu sınıf, RabbitMQ mesaj broker bağlantısı için gerekli ayarları tanımlar.
    /// appsettings.json dosyasından yüklenir ve dependency injection ile
    /// messaging servislerine enjekte edilir.
    /// 
    /// Özellikler:
    /// - Bağlantı bilgileri (host, port, kullanıcı adı, şifre)
    /// - Exchange ve queue yapılandırması
    /// - Bağlantı timeout ve heartbeat ayarları
    /// - Otomatik kurtarma seçenekleri
    /// </summary>
    public class RabbitMQSettings
    {
        /// <summary>
        /// RabbitMQ sunucu host adı (varsayılan: "localhost")
        /// </summary>
        public string HostName { get; set; } = "localhost";

        /// <summary>
        /// RabbitMQ sunucu port numarası (varsayılan: 5672)
        /// </summary>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// RabbitMQ kimlik doğrulama kullanıcı adı (varsayılan: "guest")
        /// </summary>
        public string UserName { get; set; } = "guest";

        /// <summary>
        /// RabbitMQ kimlik doğrulama şifresi (varsayılan: "guest")
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// Sanal host adı (varsayılan: "/")
        /// </summary>
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// Konfigürasyon değişiklik olayları için exchange adı (varsayılan: "configuration.changes")
        /// </summary>
        public string ExchangeName { get; set; } = "configuration.changes";

        /// <summary>
        /// Konfigürasyon değişiklik olayları için queue adı öneki (varsayılan: "configuration.changes")
        /// </summary>
        public string QueueNamePrefix { get; set; } = "configuration.changes";

        /// <summary>
        /// Bağlantı timeout süresi (milisaniye) (varsayılan: 30000)
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30000;

        /// <summary>
        /// İstenen heartbeat aralığı (saniye) (varsayılan: 60)
        /// </summary>
        public ushort RequestedHeartbeat { get; set; } = 60;

        /// <summary>
        /// Otomatik kurtarma kullanılıp kullanılmayacağı (varsayılan: true)
        /// </summary>
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// Ağ kurtarma aralığı (saniye) (varsayılan: 5)
        /// </summary>
        public int NetworkRecoveryInterval { get; set; } = 5;
    }
}
