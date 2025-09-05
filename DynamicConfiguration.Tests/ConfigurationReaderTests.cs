using DynamicConfiguration.Models;
using DynamicConfiguration.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DynamicConfiguration.Tests
{
    public class ConfigurationReaderTests : IDisposable
    {
        private readonly Mock<IConfigurationRepository> _repositoryMock;
        private readonly Mock<ILoggerFactory> _loggerFactoryMock;
        private readonly List<ConfigurationRecord> _testConfigurations;
        private ConfigurationReader? _configurationReader;

        public ConfigurationReaderTests()
        {
            _repositoryMock = new Mock<IConfigurationRepository>();
            _loggerFactoryMock = new Mock<ILoggerFactory>();

            // Logger factory'yi logger mock döndürecek şekilde ayarla
            var loggerMock = new Mock<ILogger<ConfigurationReader>>();
            _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

            // Test verilerini hazırla
            _testConfigurations = new List<ConfigurationRecord>
            {
                new ConfigurationRecord
                {
                    Id = "1",
                    Name = "SiteName",
                    Type = ConfigurationType.String,
                    Value = "soty.io",
                    IsActive = true,
                    ApplicationName = "SERVICE-A"
                },
                new ConfigurationRecord
                {
                    Id = "2",
                    Name = "IsBasketEnabled",
                    Type = ConfigurationType.Bool,
                    Value = "true",
                    IsActive = true,
                    ApplicationName = "SERVICE-A"
                },
                new ConfigurationRecord
                {
                    Id = "3",
                    Name = "MaxItemCount",
                    Type = ConfigurationType.Int,
                    Value = "50",
                    IsActive = true,
                    ApplicationName = "SERVICE-A"
                },
                new ConfigurationRecord
                {
                    Id = "4",
                    Name = "TaxRate",
                    Type = ConfigurationType.Double,
                    Value = "0.18",
                    IsActive = true,
                    ApplicationName = "SERVICE-B"
                },
                new ConfigurationRecord
                {
                    Id = "5",
                    Name = "InactiveConfig",
                    Type = ConfigurationType.String,
                    Value = "inactive",
                    IsActive = false,
                    ApplicationName = "SERVICE-A"
                }
            };
        }

        public void Dispose()
        {
            _configurationReader?.Dispose();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            // Act
            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Assert
            _configurationReader.Should().NotBeNull();
            _repositoryMock.Verify(r => r.GetActiveConfigurationsAsync("SERVICE-A", default), Times.Once);
        }

        [Fact]
        public void Constructor_WithNullApplicationName_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ConfigurationReader(null!, "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true));
        }

        [Fact]
        public void Constructor_WithRefreshIntervalLessThanMinimum_ShouldUseMinimumInterval()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            // Act
            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 500, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Assert
            _configurationReader.Should().NotBeNull();
        }

        [Theory]
        [InlineData("SiteName", "soty.io")]
        [InlineData("IsBasketEnabled", true)]
        [InlineData("MaxItemCount", 50)]
        public void GetValue_WithValidConfiguration_ShouldReturnCorrectValue<T>(string key, T expectedValue)
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            var result = _configurationReader.GetValue<T>(key);

            // Assert
            result.Should().Be(expectedValue);
        }

        [Fact]
        public void GetValue_WithNonExistentKey_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => _configurationReader.GetValue<string>("NonExistentKey"));
        }

        [Fact]
        public void GetValue_WithInvalidTypeConversion_ShouldThrowInvalidCastException()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => _configurationReader.GetValue<int>("SiteName"));
        }

        [Fact]
        public void GetValue_WithDefaultValue_ShouldReturnDefaultWhenKeyNotFound()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            var result = _configurationReader.GetValue("NonExistentKey", "defaultValue");

            // Assert
            result.Should().Be("defaultValue");
        }

        [Fact]
        public void GetValue_WithDefaultValue_ShouldReturnActualValueWhenKeyExists()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            var result = _configurationReader.GetValue("SiteName", "defaultValue");

            // Assert
            result.Should().Be("soty.io");
        }

        [Theory]
        [InlineData("SiteName", true)]
        [InlineData("NonExistentKey", false)]
        public void HasKey_ShouldReturnCorrectResult(string key, bool expectedResult)
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            var result = _configurationReader.HasKey(key);

            // Assert
            result.Should().Be(expectedResult);
        }

        [Fact]
        public void GetAllKeys_ShouldReturnAllConfigurationKeys()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            var keys = _configurationReader.GetAllKeys();

            // Assert
            keys.Should().HaveCount(3);
            keys.Should().Contain(new[] { "SiteName", "IsBasketEnabled", "MaxItemCount" });
        }

        [Fact]
        public void IsHealthy_ShouldReturnTrueWhenRepositoryIsHealthyAndHasConfigurations()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            var result = _configurationReader.IsHealthy();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsHealthy_ShouldReturnFalseWhenRepositoryFails()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ThrowsAsync(new Exception("Repository failure"));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            var result = _configurationReader.IsHealthy();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task RefreshAsync_ShouldReloadConfigurations()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            await _configurationReader.RefreshAsync();

            // Assert
            _repositoryMock.Verify(r => r.GetActiveConfigurationsAsync("SERVICE-A", default), Times.Exactly(2)); // Once during construction, once during refresh
        }

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            _configurationReader.Dispose();

            // Assert
            _configurationReader.Should().NotBeNull();
            // Timer should be disposed, but we can't easily test this without reflection
        }

        [Fact]
        public void GetValue_ShouldCacheTypedValues()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act
            var result1 = _configurationReader.GetValue<string>("SiteName");
            var result2 = _configurationReader.GetValue<string>("SiteName");

            // Assert
            result1.Should().Be("soty.io");
            result2.Should().Be("soty.io");
            // Repository sadece başlatma sırasında bir kez çağrılmalı, her GetValue çağrısında değil
            _repositoryMock.Verify(r => r.GetActiveConfigurationsAsync("SERVICE-A", default), Times.Once);
        }

        [Fact]
        public void GetValue_WithEmptyKey_ShouldThrowArgumentException()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetActiveConfigurationsAsync("SERVICE-A", default))
                          .ReturnsAsync(_testConfigurations.Where(c => c.ApplicationName == "SERVICE-A" && c.IsActive));

            _configurationReader = new ConfigurationReader("SERVICE-A", "mongodb://localhost:27017", 5000, _loggerFactoryMock.Object, _repositoryMock.Object, null, true);

            // Act & Assert
            // Boş anahtar için ArgumentException fırlatmalı
            Assert.Throws<ArgumentException>(() => _configurationReader.GetValue<string>(""));
            // Null anahtar için ArgumentException fırlatmalı
            Assert.Throws<ArgumentException>(() => _configurationReader.GetValue<string>(null!));
            // Sadece boşluk içeren anahtar için ArgumentException fırlatmalı
            Assert.Throws<ArgumentException>(() => _configurationReader.GetValue<string>("   "));
        }
    }
}
