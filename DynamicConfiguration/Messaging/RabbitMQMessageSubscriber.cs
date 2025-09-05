using DynamicConfiguration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace DynamicConfiguration.Messaging
{
    /// <summary>
    /// RabbitMQ Mesaj Abonesi - Konfigürasyon değişiklik olaylarını RabbitMQ'dan dinlemek için kullanılır
    /// 
    /// Bu sınıf, IMessageSubscriber arayüzünün RabbitMQ implementasyonudur.
    /// Konfigürasyon değişiklik olaylarını RabbitMQ'dan dinler ve
    /// gerçek zamanlı güncellemeler sağlar.
    /// 
    /// Özellikler:
    /// - RabbitMQ bağlantı yönetimi
    /// - Asenkron mesaj dinleme
    /// - Uygulama bazlı abonelik yönetimi
    /// - Otomatik bağlantı kurtarma
    /// - Thread-safe işlemler
    /// - Kaynak yönetimi (IDisposable)
    /// </summary>
    public class RabbitMQMessageSubscriber : IMessageSubscriber, IDisposable
    {
        // ========================================
        // PRIVATE FIELDS
        // ========================================
        private readonly RabbitMQSettings _settings;                              // RabbitMQ ayarları
        private readonly ILogger<RabbitMQMessageSubscriber> _logger;               // Loglama servisi
        private IConnection? _connection;                                         // RabbitMQ bağlantısı
        private IModel? _channel;                                                 // RabbitMQ kanalı
        private readonly ConcurrentDictionary<string, string> _subscriptions = new(); // Aktif abonelikler
        private readonly object _lock = new object();                             // Thread-safety için lock
        private bool _disposed = false;                                           // Dispose durumu

        /// <summary>
        /// RabbitMQ Mesaj Abonesi'ni başlatır
        /// 
        /// Bu constructor, RabbitMQ bağlantısını kurar ve exchange'i yapılandırır.
        /// </summary>
        /// <param name="settings">RabbitMQ bağlantı ayarları</param>
        /// <param name="logger">Loglama servisi</param>
        /// <exception cref="Exception">RabbitMQ bağlantısı kurulamazsa fırlatılır</exception>
        public RabbitMQMessageSubscriber(IOptions<RabbitMQSettings> settings, ILogger<RabbitMQMessageSubscriber> logger)
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

                _logger.LogInformation("RabbitMQ Abone bağlantısı başarıyla kuruldu - Host: {HostName}, Port: {Port}", _settings.HostName, _settings.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ Abone bağlantısı başlatılamadı");
                throw;
            }
        }

        /// <summary>
        /// Belirli bir uygulama için konfigürasyon değişiklik olaylarına abone olur
        /// </summary>
        /// <param name="applicationName">Abone olunacak uygulama adı</param>
        /// <param name="onConfigurationChanged">Konfigürasyon değiştiğinde çağrılacak callback</param>
        /// <returns>Asenkron işlemi temsil eden task</returns>
        /// <exception cref="ObjectDisposedException">Subscriber dispose edilmişse fırlatılır</exception>
        public async Task SubscribeToConfigurationChangesAsync(string applicationName, Func<ConfigurationChangeEvent, Task> onConfigurationChanged)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RabbitMQMessageSubscriber));
            }

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

                    // Bu uygulama için queue adı oluştur
                    var queueName = $"{_settings.QueueNamePrefix}.{applicationName}";
                    
                    // Queue'yu tanımla
                    _channel.QueueDeclare(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    // Queue'yu exchange'e routing key pattern ile bağla
                    var routingKey = $"configuration.{applicationName}.*";
                    _channel.QueueBind(
                        queue: queueName,
                        exchange: _settings.ExchangeName,
                        routingKey: routingKey);

                    // Consumer'ı ayarla
                    var consumer = new EventingBasicConsumer(_channel);
                    consumer.Received += async (model, ea) =>
                    {
                        try
                        {
                            var body = ea.Body.ToArray();
                            var message = Encoding.UTF8.GetString(body);
                            
                            var changeEvent = JsonSerializer.Deserialize<ConfigurationChangeEvent>(message, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });

                            if (changeEvent != null)
                            {
                                _logger.LogInformation("Konfigürasyon değişiklik olayı alındı - Uygulama: {ApplicationName}, Değişiklik Tipi: {ChangeType}",
                                    changeEvent.ApplicationName, changeEvent.ChangeType);

                                await onConfigurationChanged(changeEvent);
                            }

                            // Mesajı onayla
                            _channel.BasicAck(ea.DeliveryTag, false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Konfigürasyon değişiklik olayı işlenirken hata oluştu");
                            // Mesajı reddet ve tekrar kuyruğa alma
                            _channel.BasicNack(ea.DeliveryTag, false, false);
                        }
                    };

                    // Mesaj dinlemeye başla
                    var consumerTag = _channel.BasicConsume(
                        queue: queueName,
                        autoAck: false,
                        consumer: consumer);

                    // Aboneliği sakla
                    _subscriptions[applicationName] = consumerTag;

                    _logger.LogInformation("Konfigürasyon değişikliklerine abone olundu - Uygulama: {ApplicationName}, Queue: {QueueName}",
                        applicationName, queueName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon değişikliklerine abone olunamadı - Uygulama: {ApplicationName}",
                    applicationName);
                throw;
            }
        }

        /// <summary>
        /// Belirli bir uygulama için konfigürasyon değişiklik olaylarından abonelikten çıkar
        /// </summary>
        /// <param name="applicationName">Abonelikten çıkılacak uygulama adı</param>
        /// <returns>Asenkron işlemi temsil eden task</returns>
        public async Task UnsubscribeFromConfigurationChangesAsync(string applicationName)
        {
            try
            {
                lock (_lock)
                {
                    if (_subscriptions.TryRemove(applicationName, out var consumerTag) && _channel != null)
                    {
                        _channel.BasicCancel(consumerTag);
                        _logger.LogInformation("Konfigürasyon değişikliklerinden abonelikten çıkıldı - Uygulama: {ApplicationName}",
                            applicationName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon değişikliklerinden abonelikten çıkılamadı - Uygulama: {ApplicationName}",
                    applicationName);
                throw;
            }
        }

        /// <summary>
        /// Mesaj abonesinin sağlıklı ve bağlı olup olmadığını kontrol eder
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
                    _logger.LogDebug("RabbitMQ Subscriber sağlık kontrolü: {IsHealthy}", isHealthy);
                    return isHealthy;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ Subscriber sağlık kontrolü başarısız");
                return false;
            }
        }

        /// <summary>
        /// RabbitMQ Mesaj Abonesi kaynaklarını serbest bırakır
        /// 
        /// Bu method, tüm abonelikleri iptal eder, RabbitMQ bağlantısı ve kanalını
        /// güvenli bir şekilde serbest bırakır ve kaynak sızıntılarını önler.
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
        /// bağlantılarını ve abonelikleri güvenli bir şekilde temizler.
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
                        // Tüm abonelikleri iptal et
                        foreach (var subscription in _subscriptions.Values)
                        {
                            _channel?.BasicCancel(subscription);
                        }
                        _subscriptions.Clear();

                        // Kanalı kapat ve serbest bırak
                        _channel?.Close();
                        _channel?.Dispose();
                        
                        // Bağlantıyı kapat ve serbest bırak
                        _connection?.Close();
                        _connection?.Dispose();
                        
                        _logger.LogInformation("RabbitMQ Subscriber başarıyla serbest bırakıldı");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "RabbitMQ Subscriber serbest bırakılırken hata oluştu");
                    }
                }
            }

            _disposed = true;
        }
    }
}
