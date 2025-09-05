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
    /// RabbitMQ'dan konfigürasyon değişikliklerini dinler.
    /// Real-time güncellemeler sağlar.
    /// </summary>
    public class RabbitMQMessageSubscriber : IMessageSubscriber, IDisposable
    {
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<RabbitMQMessageSubscriber> _logger;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly ConcurrentDictionary<string, string> _subscriptions = new();
        private readonly object _lock = new object();
        private bool _disposed = false;

        /// <summary>RabbitMQ bağlantısını kurar.</summary>
        public RabbitMQMessageSubscriber(IOptions<RabbitMQSettings> settings, ILogger<RabbitMQMessageSubscriber> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            InitializeConnection();
        }

        /// <summary>RabbitMQ'ya bağlanır ve exchange'i ayarlar.</summary>
        private void InitializeConnection()
        {
            try
            {
                // Connection factory setup
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

                // Connection ve channel oluştur
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Exchange declare
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

        /// <summary>Uygulama için konfigürasyon değişikliklerini dinler.</summary>
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
                    // Channel kapalıysa yeniden bağlan
                    if (_channel == null || _channel.IsClosed)
                    {
                        _logger.LogWarning("RabbitMQ kanalı kapalı, yeniden bağlanmaya çalışılıyor...");
                        InitializeConnection();
                    }

                    // Queue name oluştur
                    var queueName = $"{_settings.QueueNamePrefix}.{applicationName}";
                    
                    // Queue declare
                    _channel.QueueDeclare(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    // Queue'yu exchange'e bağla
                    var routingKey = $"configuration.{applicationName}.*";
                    _channel.QueueBind(
                        queue: queueName,
                        exchange: _settings.ExchangeName,
                        routingKey: routingKey);

                    // Consumer setup
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

                            // Message ack
                            _channel.BasicAck(ea.DeliveryTag, false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Konfigürasyon değişiklik olayı işlenirken hata oluştu");
                            // Message nack
                            _channel.BasicNack(ea.DeliveryTag, false, false);
                        }
                    };

                    // Consume başlat
                    var consumerTag = _channel.BasicConsume(
                        queue: queueName,
                        autoAck: false,
                        consumer: consumer);

                    // Subscription kaydet
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

        /// <summary>Abonelikten çıkar.</summary>
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

        /// <summary>Bağlantı sağlığını kontrol eder.</summary>
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

        /// <summary>Kaynakları temizler.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Dispose pattern implementation.</summary>
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
                        // Tüm subscription'ları iptal et
                        foreach (var subscription in _subscriptions.Values)
                        {
                            _channel?.BasicCancel(subscription);
                        }
                        _subscriptions.Clear();

                        // Channel'ı kapat
                        _channel?.Close();
                        _channel?.Dispose();
                        
                        // Connection'ı kapat
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
