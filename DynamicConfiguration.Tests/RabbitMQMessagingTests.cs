using DynamicConfiguration.Messaging;
using DynamicConfiguration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using System.Text.Json;
using Xunit;

namespace DynamicConfiguration.Tests
{
    /// <summary>
    /// RabbitMQ Mesajlaşma Fonksiyonları Testleri
    /// </summary>
    public class RabbitMQMessagingTests : IDisposable
    {
        private readonly Mock<ILogger<RabbitMQMessagePublisher>> _publisherLoggerMock;
        private readonly Mock<ILogger<RabbitMQMessageSubscriber>> _subscriberLoggerMock;
        private readonly RabbitMQSettings _settings;
        private RabbitMQMessagePublisher? _publisher;
        private RabbitMQMessageSubscriber? _subscriber;

        public RabbitMQMessagingTests()
        {
            _publisherLoggerMock = new Mock<ILogger<RabbitMQMessagePublisher>>();
            _subscriberLoggerMock = new Mock<ILogger<RabbitMQMessageSubscriber>>();

            _settings = new RabbitMQSettings
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "admin",
                Password = "password123",
                VirtualHost = "/",
                ExchangeName = "configuration.changes",
                QueueNamePrefix = "configuration.changes",
                ConnectionTimeout = 30000,
                RequestedHeartbeat = 60,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = 5
            };
        }

        public void Dispose()
        {
            _publisher?.Dispose();
            _subscriber?.Dispose();
        }

        #region RabbitMQMessagePublisher Tests

        [Fact]
        public void RabbitMQMessagePublisher_InheritsFromIMessagePublisher()
        {
            // Bu test, RabbitMQMessagePublisher'ın IMessagePublisher arayüzünü implement edip etmediğini doğrular
            var publisherType = typeof(RabbitMQMessagePublisher);
            var interfaceType = typeof(IMessagePublisher);

            Assert.True(interfaceType.IsAssignableFrom(publisherType));
        }

        [Fact]
        public void RabbitMQSettings_ShouldHaveRequiredProperties()
        {
            // RabbitMQSettings'in tüm gerekli özellikleri olup olmadığını test et
            var settingsType = typeof(RabbitMQSettings);
            var hostNameProperty = settingsType.GetProperty("HostName");
            var portProperty = settingsType.GetProperty("Port");
            var userNameProperty = settingsType.GetProperty("UserName");
            var passwordProperty = settingsType.GetProperty("Password");
            var exchangeNameProperty = settingsType.GetProperty("ExchangeName");

            Assert.NotNull(hostNameProperty);
            Assert.NotNull(portProperty);
            Assert.NotNull(userNameProperty);
            Assert.NotNull(passwordProperty);
            Assert.NotNull(exchangeNameProperty);
        }

        [Fact]
        public void RabbitMQSettings_ShouldHaveDefaultValues()
        {
            // RabbitMQSettings'in varsayılan değerleri olup olmadığını test et
            var settings = new RabbitMQSettings();

            Assert.Equal("localhost", settings.HostName);
            Assert.Equal(5672, settings.Port);
            Assert.Equal("guest", settings.UserName);
            Assert.Equal("guest", settings.Password);
            Assert.Equal("/", settings.VirtualHost);
            Assert.Equal("configuration.changes", settings.ExchangeName);
            Assert.Equal(30000, settings.ConnectionTimeout);
            Assert.Equal(60, settings.RequestedHeartbeat);
            Assert.True(settings.AutomaticRecoveryEnabled);
            Assert.Equal(5, settings.NetworkRecoveryInterval);
        }

        [Fact]
        public void RabbitMQSettings_CanBeOverridden()
        {
            // RabbitMQSettings'in değerlerinin geçersiz kılınıp kılınamayacağını test et
            var settings = new RabbitMQSettings
            {
                HostName = "rabbitmq.example.com",
                Port = 5673,
                UserName = "testuser",
                Password = "testpass",
                VirtualHost = "/test",
                ExchangeName = "test.exchange",
                ConnectionTimeout = 15000,
                RequestedHeartbeat = 30,
                AutomaticRecoveryEnabled = false,
                NetworkRecoveryInterval = 10
            };

            Assert.Equal("rabbitmq.example.com", settings.HostName);
            Assert.Equal(5673, settings.Port);
            Assert.Equal("testuser", settings.UserName);
            Assert.Equal("testpass", settings.Password);
            Assert.Equal("/test", settings.VirtualHost);
            Assert.Equal("test.exchange", settings.ExchangeName);
            Assert.Equal(15000, settings.ConnectionTimeout);
            Assert.Equal(30, settings.RequestedHeartbeat);
            Assert.False(settings.AutomaticRecoveryEnabled);
            Assert.Equal(10, settings.NetworkRecoveryInterval);
        }

        #endregion

        #region RabbitMQMessageSubscriber Tests

        [Fact]
        public void RabbitMQMessageSubscriber_InheritsFromIMessageSubscriber()
        {
            // Bu test, RabbitMQMessageSubscriber'ın IMessageSubscriber arayüzünü implement edip etmediğini doğrular
            var subscriberType = typeof(RabbitMQMessageSubscriber);
            var interfaceType = typeof(IMessageSubscriber);

            Assert.True(interfaceType.IsAssignableFrom(subscriberType));
        }

        #endregion

        #region ConfigurationChangeEvent Tests

        [Fact]
        public void ConfigurationChangeEvent_ShouldHaveAllRequiredProperties()
        {
            // ConfigurationChangeEvent'ın tüm gerekli özellikleri olup olmadığını test et
            var eventType = typeof(ConfigurationChangeEvent);
            var changeTypeProperty = eventType.GetProperty("ChangeType");
            var applicationNameProperty = eventType.GetProperty("ApplicationName");
            var configurationNameProperty = eventType.GetProperty("ConfigurationName");
            var timestampProperty = eventType.GetProperty("Timestamp");

            Assert.NotNull(changeTypeProperty);
            Assert.NotNull(applicationNameProperty);
            Assert.NotNull(configurationNameProperty);
            Assert.NotNull(timestampProperty);
        }

        [Fact]
        public void ConfigurationChangeEvent_CanBeCreatedWithValidData()
        {
            // ConfigurationChangeEvent'ın geçerli verilerle oluşturulup oluşturulamayacağını test et
            var changeEvent = new ConfigurationChangeEvent
            {
                ChangeType = ConfigurationChangeType.Created,
                ApplicationName = "SERVICE-A",
                ConfigurationName = "SiteName",
                ConfigurationValue = "soty.io",
                ConfigurationType = ConfigurationType.String,
                IsActive = true,
                ConfigurationId = "12345",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "CreatedAt", DateTime.UtcNow },
                    { "UpdatedAt", DateTime.UtcNow }
                }
            };

            Assert.Equal(ConfigurationChangeType.Created, changeEvent.ChangeType);
            Assert.Equal("SERVICE-A", changeEvent.ApplicationName);
            Assert.Equal("SiteName", changeEvent.ConfigurationName);
            Assert.Equal("soty.io", changeEvent.ConfigurationValue);
            Assert.Equal(ConfigurationType.String, changeEvent.ConfigurationType);
            Assert.True(changeEvent.IsActive);
            Assert.Equal("12345", changeEvent.ConfigurationId);
            Assert.NotNull(changeEvent.Metadata);
        }

        [Fact]
        public void ConfigurationChangeEvent_CanBeSerializedToJson()
        {
            // ConfigurationChangeEvent'ın JSON'a serialize edilebilip edilemeyeceğini test et
            var changeEvent = new ConfigurationChangeEvent
            {
                ChangeType = ConfigurationChangeType.Updated,
                ApplicationName = "SERVICE-B",
                ConfigurationName = "MaxItemCount",
                ConfigurationValue = "100",
                ConfigurationType = ConfigurationType.Int,
                IsActive = true,
                ConfigurationId = "67890",
                Timestamp = DateTime.UtcNow
            };

            // Use custom JsonSerializerOptions to match RabbitMQ publisher settings
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(changeEvent, options);
            Assert.NotNull(json);
            Assert.Contains("SERVICE-B", json);
            Assert.Contains("MaxItemCount", json);
            Assert.Contains("2", json); // Updated = 2 (enum value)
        }

        [Fact]
        public void ConfigurationChangeEvent_CanBeDeserializedFromJson()
        {
            // ConfigurationChangeEvent'ın JSON'dan deserialize edilebilip edilemeyeceğini test et
            var json = @"{
                ""changeType"": 2,
                ""applicationName"": ""SERVICE-B"",
                ""configurationName"": ""MaxItemCount"",
                ""configurationValue"": ""100"",
                ""configurationType"": 1,
                ""isActive"": true,
                ""configurationId"": ""67890"",
                ""timestamp"": ""2024-01-01T12:00:00Z""
            }";

            // Use custom JsonSerializerOptions to match RabbitMQ publisher settings
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var changeEvent = JsonSerializer.Deserialize<ConfigurationChangeEvent>(json, options);
            Assert.NotNull(changeEvent);
            Assert.Equal(ConfigurationChangeType.Updated, changeEvent.ChangeType); // 2 = Updated
            Assert.Equal("SERVICE-B", changeEvent.ApplicationName);
            Assert.Equal("MaxItemCount", changeEvent.ConfigurationName);
            Assert.Equal("100", changeEvent.ConfigurationValue);
            Assert.Equal(ConfigurationType.Int, changeEvent.ConfigurationType); // 1 = Int
            Assert.True(changeEvent.IsActive);
            Assert.Equal("67890", changeEvent.ConfigurationId);
        }

        #endregion

        #region ConfigurationChangeType Tests

        [Fact]
        public void ConfigurationChangeType_ShouldHaveAllRequiredValues()
        {
            // ConfigurationChangeType enum'ının tüm gerekli değerleri olup olmadığını test et
            var changeTypes = Enum.GetValues(typeof(ConfigurationChangeType)).Cast<ConfigurationChangeType>();
            Assert.NotNull(changeTypes);

            // Beklenen değerler var mı kontrol et
            Assert.Contains(ConfigurationChangeType.Created, changeTypes);
            Assert.Contains(ConfigurationChangeType.Updated, changeTypes);
            Assert.Contains(ConfigurationChangeType.Deleted, changeTypes);
            Assert.Contains(ConfigurationChangeType.StatusChanged, changeTypes);
        }

        [Fact]
        public void ConfigurationChangeType_ValuesShouldBeUnique()
        {
            // ConfigurationChangeType değerlerinin benzersiz olup olmadığını test et
            var changeTypes = Enum.GetValues(typeof(ConfigurationChangeType));
            var uniqueValues = new HashSet<int>();

            foreach (var changeType in changeTypes)
            {
                var intValue = (int)changeType;
                Assert.True(uniqueValues.Add(intValue), $"Duplicate value found: {intValue}");
            }
        }

        [Fact]
        public void ConfigurationChangeType_CanConvertToString()
        {
            // ConfigurationChangeType'ın string'e dönüştürülebilip dönüştürülemeyeceğini test et
            var createdString = ConfigurationChangeType.Created.ToString();
            var updatedString = ConfigurationChangeType.Updated.ToString();
            var deletedString = ConfigurationChangeType.Deleted.ToString();

            Assert.Equal("Created", createdString);
            Assert.Equal("Updated", updatedString);
            Assert.Equal("Deleted", deletedString);
        }

        #endregion

        #region Messaging Interfaces Tests

        [Fact]
        public void IMessagePublisher_ShouldHaveRequiredMethods()
        {
            // IMessagePublisher arayüzünün gerekli methodları olup olmadığını test et
            var interfaceType = typeof(IMessagePublisher);

            // Required methods
            var publishMethod = interfaceType.GetMethod("PublishConfigurationChangeAsync", new[] { typeof(ConfigurationChangeEvent) });
            var publishWithRetryMethod = interfaceType.GetMethod("PublishConfigurationChangeAsync", new[] { typeof(ConfigurationChangeEvent), typeof(int) });
            var isHealthyMethod = interfaceType.GetMethod("IsHealthyAsync");
            var disposeMethod = interfaceType.GetMethod("Dispose");

            Assert.NotNull(publishMethod);
            Assert.NotNull(publishWithRetryMethod);
            Assert.NotNull(isHealthyMethod);
            Assert.NotNull(disposeMethod);
        }

        [Fact]
        public void IMessageSubscriber_ShouldHaveRequiredMethods()
        {
            // IMessageSubscriber arayüzünün gerekli methodları olup olmadığını test et
            var interfaceType = typeof(IMessageSubscriber);

            // Required methods
            var subscribeMethod = interfaceType.GetMethod("SubscribeToConfigurationChangesAsync");
            var unsubscribeMethod = interfaceType.GetMethod("UnsubscribeFromConfigurationChangesAsync");
            var isHealthyMethod = interfaceType.GetMethod("IsHealthyAsync");
            var disposeMethod = interfaceType.GetMethod("Dispose");

            Assert.NotNull(subscribeMethod);
            Assert.NotNull(unsubscribeMethod);
            Assert.NotNull(isHealthyMethod);
            Assert.NotNull(disposeMethod);
        }

        #endregion

        #region Integration Tests (Mock-based)

        [Fact]
        public void MessagePublisher_ShouldImplementIDisposable()
        {
            // MessagePublisher'ın IDisposable arayüzünü implement edip etmediğini test et
            var publisherType = typeof(RabbitMQMessagePublisher);
            var disposableInterface = typeof(IDisposable);

            Assert.True(disposableInterface.IsAssignableFrom(publisherType));
        }

        [Fact]
        public void MessageSubscriber_ShouldImplementIDisposable()
        {
            // MessageSubscriber'ın IDisposable arayüzünü implement edip etmediğini test et
            var subscriberType = typeof(RabbitMQMessageSubscriber);
            var disposableInterface = typeof(IDisposable);

            Assert.True(disposableInterface.IsAssignableFrom(subscriberType));
        }

        [Fact]
        public async Task ConfigurationChangeEvent_ShouldSupportAllDataTypes()
        {
            // ConfigurationChangeEvent'ın tüm veri tiplerini destekleyip desteklemediğini test et
            var testCases = new[]
            {
                new { Type = ConfigurationType.String, Value = "test-string" },
                new { Type = ConfigurationType.Int, Value = "42" },
                new { Type = ConfigurationType.Double, Value = "3.14" },
                new { Type = ConfigurationType.Bool, Value = "true" }
            };

            foreach (var testCase in testCases)
            {
                var changeEvent = new ConfigurationChangeEvent
                {
                    ChangeType = ConfigurationChangeType.Created,
                    ApplicationName = "SERVICE-A",
                    ConfigurationName = "TestConfig",
                    ConfigurationValue = testCase.Value,
                    ConfigurationType = testCase.Type,
                    IsActive = true,
                    ConfigurationId = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow
                };

                // JSON serialization/deserialization testi
                var json = JsonSerializer.Serialize(changeEvent);
                var deserialized = JsonSerializer.Deserialize<ConfigurationChangeEvent>(json);

                Assert.NotNull(deserialized);
                Assert.Equal(changeEvent.ConfigurationType, deserialized.ConfigurationType);
                Assert.Equal(changeEvent.ConfigurationValue, deserialized.ConfigurationValue);
            }

            await Task.CompletedTask; // Async test gereksinimi için
        }

        #endregion
    }
}
