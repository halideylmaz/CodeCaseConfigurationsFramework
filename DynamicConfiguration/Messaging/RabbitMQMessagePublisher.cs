using DynamicConfiguration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace DynamicConfiguration.Messaging
{
    /// <summary>
    /// RabbitMQ Mesaj Yayınlayıcı - Konfigürasyon değişiklik olaylarını RabbitMQ'ya yayınlamak için kullanılır
    /// 
    /// Bu sınıf, IMessagePublisher arayüzünün RabbitMQ implementasyonudur.
    /// Konfigürasyon değişiklik olaylarını RabbitMQ exchange'ine yayınlar ve
    /// gerçek zamanlı bildirimler sağlar.
    /// 
    /// Özellikler:
    /// - RabbitMQ bağlantı yönetimi
    /// - Asenkron mesaj yayınlama
    /// - Yeniden deneme mekanizması
    /// - Otomatik bağlantı kurtarma
    /// - Thread-safe işlemler
    /// - Kaynak yönetimi (IDisposable)
    /// </summary>
    public class RabbitMQMessagePublisher : IMessagePublisher, IDisposable
    {
        // ========================================
        // PRIVATE FIELDS
        // ========================================
        private readonly RabbitMQSettings _settings;                              // RabbitMQ ayarları
        private readonly ILogger<RabbitMQMessagePublisher> _logger;               // Loglama servisi
        private IConnection? _connection;                                         // RabbitMQ bağlantısı
        private IModel? _channel;                                                 // RabbitMQ kanalı
        private readonly object _lock = new object();                             // Thread-safety için lock
        private bool _disposed = false;                                           // Dispose durumu

        /// <summary>
        /// RabbitMQ Mesaj Yayınlayıcı'sını başlatır
        /// 
        /// Bu constructor, RabbitMQ bağlantısını kurar ve exchange'i yapılandırır.
        /// </summary>
        /// <param name="settings">RabbitMQ bağlantı ayarları</param>
        /// <param name="logger">Loglama servisi</param>
        /// <exception cref="Exception">RabbitMQ bağlantısı kurulamazsa fırlatılır</exception>
        public RabbitMQMessagePublisher(IOptions<RabbitMQSettings> settings, ILogger<RabbitMQMessagePublisher> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            InitializeConnection();
        }

        /// <summary>
        /// RabbitMQ bağlantısını başlatır ve exchange'i yapılandırır
        /// 
        /// Bu method, RabbitMQ sunucusuna bağlanır, kanal oluşturur ve
        /// konfigürasyon değişiklik olayları için exchange'i tanımlar.
        /// </summary>
        /// <exception cref="Exception">Bağlantı kurulamazsa fırlatılır</exception>
        private void InitializeConnection()
        {
            try
            {
                // RabbitMQ bağlantı factory'sini yapılandır
                var factory = new ConnectionFactory
                {
                    HostName = _settings.HostName,
                    Port = _settings.Port,
                    UserName = _settings.UserName,
                    Password = _settings.Password,
                    VirtualHost = _settings.VirtualHost,
                    RequestedConnectionTimeout = TimeSpan.FromMilliseconds(_settings.ConnectionTimeout),
                    RequestedHeartbeat = TimeSpan.FromSeconds(_settings.RequestedHeartbeat),
                    AutomaticRecoveryEnabled = _settings.AutomaticRecoveryEnabled,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(_settings.NetworkRecoveryInterval)
                };

                // Bağlantı ve kanal oluştur
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Exchange'i tanımla
                _channel.ExchangeDeclare(
                    exchange: _settings.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                _logger.LogInformation("RabbitMQ bağlantısı başarıyla kuruldu - Host: {HostName}, Port: {Port}", _settings.HostName, _settings.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ bağlantısı başlatılamadı");
                throw;
            }
        }

        /// <summary>
        /// Konfigürasyon değişiklik olayını asenkron olarak yayınlar (varsayılan yeniden deneme sayısı ile)
        /// </summary>
        /// <param name="changeEvent">Yayınlanacak konfigürasyon değişiklik olayı</param>
        /// <returns>Asenkron işlemi temsil eden task</returns>
        public async Task PublishConfigurationChangeAsync(ConfigurationChangeEvent changeEvent)
        {
            await PublishConfigurationChangeAsync(changeEvent, 3);
        }

        /// <summary>
        /// Konfigürasyon değişiklik olayını yeniden deneme mantığı ile asenkron olarak yayınlar
        /// </summary>
        /// <param name="changeEvent">Yayınlanacak konfigürasyon değişiklik olayı</param>
        /// <param name="retryCount">Yeniden deneme sayısı</param>
        /// <returns>Asenkron işlemi temsil eden task</returns>
        /// <exception cref="ObjectDisposedException">Publisher dispose edilmişse fırlatılır</exception>
        public async Task PublishConfigurationChangeAsync(ConfigurationChangeEvent changeEvent, int retryCount)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RabbitMQMessagePublisher));
            }

            var attempts = 0;
            while (attempts < retryCount)
            {
                try
                {
                    lock (_lock)
                    {
                        // Kanal kapalıysa yeniden bağlan
                        if (_channel == null || _channel.IsClosed)
                        {
                            _logger.LogWarning("RabbitMQ kanalı kapalı, yeniden bağlanmaya çalışılıyor...");
                            InitializeConnection();
                        }

                        // Olayı JSON'a serialize et
                        var json = JsonSerializer.Serialize(changeEvent, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        var body = Encoding.UTF8.GetBytes(json);

                        // Uygulama adı ve değişiklik tipine göre routing key oluştur
                        var routingKey = $"configuration.{changeEvent.ApplicationName}.{changeEvent.ChangeType.ToString().ToLower()}";

                        // Mesajı yayınla
                        _channel.BasicPublish(
                            exchange: _settings.ExchangeName,
                            routingKey: routingKey,
                            basicProperties: null,
                            body: body);

                        _logger.LogInformation("Konfigürasyon değişiklik olayı yayınlandı - Uygulama: {ApplicationName}, Değişiklik Tipi: {ChangeType}",
                            changeEvent.ApplicationName, changeEvent.ChangeType);
                    }

                    return; // Başarılı, yeniden deneme döngüsünden çık
                }
                catch (Exception ex)
                {
                    attempts++;
                    _logger.LogWarning(ex, "Konfigürasyon değişiklik olayı yayınlanamadı (deneme {Attempt}/{RetryCount})",
                        attempts, retryCount);

                    if (attempts >= retryCount)
                    {
                        _logger.LogError(ex, "Konfigürasyon değişiklik olayı {RetryCount} denemeden sonra yayınlanamadı",
                            retryCount);
                        throw;
                    }

                    // Yeniden denemeden önce bekle (exponential backoff)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts)));
                }
            }
        }

        /// <summary>
        /// Mesaj yayınlayıcının sağlıklı ve bağlı olup olmadığını kontrol eder
        /// </summary>
        /// <returns>Sağlıklı ise true, aksi halde false</returns>
        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                if (_disposed || _connection == null || _channel == null)
                {
                    return false;
                }

                lock (_lock)
                {
                    var isHealthy = _connection.IsOpen && _channel.IsOpen;
                    _logger.LogDebug("RabbitMQ Publisher sağlık kontrolü: {IsHealthy}", isHealthy);
                    return isHealthy;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ Publisher sağlık kontrolü başarısız");
                return false;
            }
        }

        /// <summary>
        /// RabbitMQ Mesaj Yayınlayıcı kaynaklarını serbest bırakır
        /// 
        /// Bu method, RabbitMQ bağlantısı ve kanalını güvenli bir şekilde
        /// serbest bırakır ve kaynak sızıntılarını önler.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Kaynakları serbest bırakır (protected virtual)
        /// 
        /// Bu method, IDisposable pattern'ini uygular ve RabbitMQ
        /// bağlantılarını güvenli bir şekilde temizler.
        /// </summary>
        /// <param name="disposing">Managed kaynakların serbest bırakılıp bırakılmayacağı</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_lock)
                {
                    try
                    {
                        // Kanalı kapat ve serbest bırak
                        _channel?.Close();
                        _channel?.Dispose();
                        
                        // Bağlantıyı kapat ve serbest bırak
                        _connection?.Close();
                        _connection?.Dispose();
                        
                        _logger.LogInformation("RabbitMQ Publisher başarıyla serbest bırakıldı");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "RabbitMQ Publisher serbest bırakılırken hata oluştu");
                    }
                }
            }

            _disposed = true;
        }
    }
}
