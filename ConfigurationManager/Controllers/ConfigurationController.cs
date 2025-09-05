using DynamicConfiguration.Models;
using DynamicConfiguration.Repositories;
using DynamicConfiguration.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace ConfigurationManager.Controllers
{
    /// <summary>
    /// Dinamik Konfigürasyon Yönetim API Controller'ı
    /// 
    /// Bu controller, uygulamaların konfigürasyon değerlerini dinamik olarak yönetmesini sağlar.
    /// Konfigürasyon değişiklikleri gerçek zamanlı olarak tüm bağlı servislere bildirilir.
    /// 
    /// Özellikler:
    /// - Konfigürasyon kayıtlarını listeleme, ekleme, güncelleme ve silme
    /// - Uygulama bazında filtreleme ve arama
    /// - Otomatik type doğrulama (string, int, double, bool)
    /// - RabbitMQ ile gerçek zamanlı değişiklik bildirimleri
    /// - MongoDB ile güvenli veri saklama
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationRepository _repository;
        private readonly IMessagePublisher _messagePublisher;
        private readonly ILogger<ConfigurationController> _logger;

        /// <summary>
        /// Konfigürasyon denetleyicisi constructor'ı
        /// 
        /// Dependency Injection ile gerekli servisleri alır:
        /// - IConfigurationRepository: MongoDB veri erişimi için
        /// - IMessagePublisher: RabbitMQ mesajlaşma için
        /// - ILogger: Loglama işlemleri için
        /// </summary>
        public ConfigurationController(
            IConfigurationRepository repository,
            IMessagePublisher messagePublisher,
            ILogger<ConfigurationController> logger)
        {
            _repository = repository;
            _messagePublisher = messagePublisher;
            _logger = logger;
        }

        /// <summary>
        /// Tüm konfigürasyon kayıtlarını listeler (isteğe bağlı filtreleme ile)
        /// 
        /// Bu endpoint, sistemdeki tüm konfigürasyon kayıtlarını döndürür.
        /// İsteğe bağlı olarak uygulama adı, konfigürasyon adı ve aktiflik durumuna göre filtreleme yapabilir.
        /// 
        /// Parametreler:
        /// - applicationName: Belirli bir uygulamanın konfigürasyonlarını filtreler
        /// - nameFilter: Konfigürasyon adında arama yapar
        /// - isActive: Sadece aktif/pasif konfigürasyonları döndürür
        /// 
        /// Dönen değer: Filtrelenmiş konfigürasyon kayıtları listesi
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConfigurationRecord>>> GetConfigurations(
            [FromQuery] string? applicationName = null,
            [FromQuery] string? nameFilter = null,
            [FromQuery] bool? isActive = null)
        {
            try
            {
                var configurations = await _repository.GetAllConfigurationsAsync();

                // Filtreleri uygula
                if (!string.IsNullOrWhiteSpace(applicationName))
                {
                    configurations = configurations.Where(c => c.ApplicationName.Contains(applicationName, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(nameFilter))
                {
                    configurations = configurations.Where(c => c.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
                }

                if (isActive.HasValue)
                {
                    configurations = configurations.Where(c => c.IsActive == isActive.Value);
                }

                _logger.LogInformation("Filtrelerle {Count} konfigürasyon alındı", configurations.Count());
                return Ok(configurations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyonlar alınamadı");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Belirli bir uygulamaya ait aktif konfigürasyonları getirir
        /// 
        /// Bu endpoint, belirtilen uygulama adına sahip tüm aktif konfigürasyon kayıtlarını döndürür.
        /// Sadece IsActive=true olan kayıtlar döndürülür.
        /// 
        /// Parametreler:
        /// - applicationName: Konfigürasyonları getirilecek uygulamanın adı
        /// 
        /// Dönen değer: Belirtilen uygulamaya ait aktif konfigürasyon kayıtları listesi
        /// </summary>
        [HttpGet("application/{applicationName}")]
        public async Task<ActionResult<IEnumerable<ConfigurationRecord>>> GetConfigurationsByApplication(string applicationName)
        {
            try
            {
                var configurations = await _repository.GetActiveConfigurationsAsync(applicationName);

                _logger.LogInformation("{ApplicationName} uygulaması için {Count} aktif konfigürasyon alındı",
                    applicationName, configurations.Count());
                return Ok(configurations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ApplicationName} uygulaması için konfigürasyonlar alınamadı", applicationName);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Belirli bir uygulama ve konfigürasyon adına göre tek bir konfigürasyon kaydı getirir
        /// 
        /// Bu endpoint, belirtilen uygulama adı ve konfigürasyon adına sahip tek bir konfigürasyon kaydını döndürür.
        /// 
        /// Parametreler:
        /// - applicationName: Konfigürasyonun ait olduğu uygulamanın adı
        /// - name: Getirilecek konfigürasyonun adı
        /// 
        /// </summary>
        [HttpGet("application/{applicationName}/name/{name}")]
        public async Task<ActionResult<ConfigurationRecord>> GetConfiguration(string applicationName, string name)
        {
            try
            {
                var configuration = await _repository.GetConfigurationAsync(applicationName, name);

                if (configuration == null)
                {
                    _logger.LogWarning("{ApplicationName} uygulaması için {Name} konfigürasyonu bulunamadı", applicationName, name);
                    return NotFound();
                }

                return Ok(configuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ApplicationName} uygulaması için {Name} konfigürasyonu alınamadı", applicationName, name);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Yeni bir konfigürasyon kaydı oluşturur
        /// 
        /// Bu endpoint, sistemde yeni bir konfigürasyon kaydı oluşturur.
        /// Konfigürasyon değeri, belirtilen tipe göre doğrulanır ve kayıt oluşturulduktan sonra
        /// tüm bağlı servislere RabbitMQ üzerinden değişiklik bildirimi gönderilir.
        /// 
        /// Parametreler:
        /// - ConfigurationRecordDto: Oluşturulacak konfigürasyon kaydının bilgileri
        /// 
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ConfigurationRecord>> CreateConfiguration([FromBody] ConfigurationRecordDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // value'nun type'a uygun olup olmadığını kontrol et
                if (!IsValidValue(dto.Value, dto.Type))
                {
                    return BadRequest($"Invalid value '{dto.Value}' for type {dto.Type}");
                }

                var record = new ConfigurationRecord
                {
                    Name = dto.Name,
                    Type = dto.Type,
                    Value = dto.Value,
                    IsActive = dto.IsActive,
                    ApplicationName = dto.ApplicationName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createdRecord = await _repository.AddConfigurationAsync(record);

                _logger.LogInformation("{ApplicationName} uygulaması için yeni konfigürasyon {Name} oluşturuldu",
                    record.ApplicationName, record.Name);

                // Değişiklik bildirimi yayınla
                await PublishConfigurationChangeEventAsync(ConfigurationChangeType.Created, createdRecord);

                return CreatedAtAction(
                    nameof(GetConfiguration),
                    new { applicationName = record.ApplicationName, name = record.Name },
                    createdRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ApplicationName} uygulaması için {Name} konfigürasyonu oluşturulamadı",
                    dto.ApplicationName, dto.Name);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Mevcut bir konfigürasyon kaydını günceller
        /// 
        /// Bu endpoint, belirtilen ID'ye sahip konfigürasyon kaydını günceller.
        /// Konfigürasyon değeri, belirtilen tipe göre doğrulanır ve güncelleme işlemi tamamlandıktan sonra
        /// tüm bağlı servislere RabbitMQ üzerinden değişiklik bildirimi gönderilir.
        /// 
        /// Parametreler:
        /// - id: Güncellenecek konfigürasyon kaydının ID'si
        /// - ConfigurationRecordDto: Güncellenecek konfigürasyon kaydının yeni bilgileri 
        /// 
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateConfiguration(string id, [FromBody] ConfigurationRecordDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // value'nun type'a uygun olup olmadığını kontrol et
                if (!IsValidValue(dto.Value, dto.Type))
                {
                    return BadRequest($"Invalid value '{dto.Value}' for type {dto.Type}");
                }

                var record = new ConfigurationRecord
                {
                    Name = dto.Name,
                    Type = dto.Type,
                    Value = dto.Value,
                    IsActive = dto.IsActive,
                    ApplicationName = dto.ApplicationName
                };

                var success = await _repository.UpdateConfigurationAsync(id, record);

                if (!success)
                {
                    _logger.LogWarning("Güncelleme için {Id} konfigürasyonu bulunamadı", id);
                    return NotFound();
                }

                _logger.LogInformation("{Id} konfigürasyonu güncellendi", id);

                // Değişiklik bildirimi yayınla
                await PublishConfigurationChangeEventAsync(ConfigurationChangeType.Updated, record);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Id} konfigürasyonu güncellenemedi", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Bir konfigürasyon kaydını siler
        /// 
        /// Bu endpoint, belirtilen ID'ye sahip konfigürasyon kaydını sistemden kalıcı olarak siler.
        /// Silme işlemi tamamlandıktan sonra tüm bağlı servislere RabbitMQ üzerinden değişiklik bildirimi gönderilir.
        /// 
        /// Parametreler:
        /// - id: Silinecek konfigürasyon kaydının ID'si
        /// 
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteConfiguration(string id)
        {
            try
            {
                // Silinecek konfigurasyonun varlığını kontrol et
                var existingConfig = await _repository.GetConfigurationByIdAsync(id);
                if (existingConfig == null)
                {
                    _logger.LogWarning("Silme için {Id} konfigürasyonu bulunamadı", id);
                    return NotFound();
                }

                var success = await _repository.DeleteConfigurationAsync(id);

                if (!success)
                {
                    _logger.LogWarning("Silme için {Id} konfigürasyonu bulunamadı", id);
                    return NotFound();
                }

                _logger.LogInformation("{Id} konfigürasyonu silindi", id);

                // Değişiklik bildirimi yayınla
                await PublishConfigurationChangeEventAsync(ConfigurationChangeType.Deleted, existingConfig);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Id} konfigürasyonu silinemedi", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Konfigürasyon servisinin sağlık durumunu kontrol eder
        /// 
        /// Bu endpoint, konfigürasyon servisinin ve bağlı veritabanının sağlık durumunu kontrol eder.
        /// Sistemin çalışır durumda olup olmadığını belirlemek için kullanılır.
        /// 
        /// Dönen değer: 
        /// - 200 OK: Servis sağlıklı (healthy)
        /// - 503 Service Unavailable: Servis sağlıksız (unhealthy)
        /// 
        /// Response formatı: { "status": "healthy/unhealthy", "timestamp": "UTC zaman damgası" }
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            try
            {
                var isHealthy = await _repository.IsHealthyAsync();
                if (isHealthy)
                {
                    return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
                }
                else
                {
                    return StatusCode(503, new { status = "unhealthy", timestamp = DateTime.UtcNow });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sağlık kontrolü başarısız");
                return StatusCode(503, new { status = "unhealthy", timestamp = DateTime.UtcNow, error = ex.Message });
            }
        }

        /// <summary>
        /// Konfigürasyon değişiklik olayını RabbitMQ üzerinden yayınlar
        /// 
        /// Bu private method, konfigürasyon kayıtlarında yapılan değişiklikleri (oluşturma, güncelleme, silme)
        /// RabbitMQ üzerinden tüm bağlı servislere bildirir. Bu sayede diğer servisler konfigürasyon
        /// değişikliklerini gerçek zamanlı olarak takip edebilir.
        /// 
        /// Parametreler:
        /// - changeType: Yapılan değişikliğin türü (Created, Updated, Deleted)
        /// - record: Değişiklik yapılan konfigürasyon kaydı
        /// 
        /// Not: Mesaj yayınlama hatası ana işlemi etkilemez (hata dönmez)
        /// </summary>
        private async Task PublishConfigurationChangeEventAsync(ConfigurationChangeType changeType, ConfigurationRecord record)
        {
            try
            {
                // Konfigürasyon değişiklik olayını oluştur
                var changeEvent = new ConfigurationChangeEvent
                {
                    ChangeType = changeType,
                    ApplicationName = record.ApplicationName,
                    ConfigurationName = record.Name,
                    ConfigurationValue = record.Value,
                    ConfigurationType = record.Type,
                    IsActive = record.IsActive,
                    ConfigurationId = record.Id,
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        { "CreatedAt", record.CreatedAt },
                        { "UpdatedAt", record.UpdatedAt }
                    }
                };

                // RabbitMQ üzerinden değişiklik olayını yayınla
                await _messagePublisher.PublishConfigurationChangeAsync(changeEvent);
                _logger.LogInformation("Konfigürasyon değişiklik olayı yayınlandı: {ChangeType} - {ApplicationName}.{ConfigurationName}",
                    changeType, record.ApplicationName, record.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konfigürasyon değişiklik olayı yayınlanamadı: {ApplicationName}.{ConfigurationName}",
                    record.ApplicationName, record.Name);
                // Hata fırlatma - mesaj yayınlama hatası ana işlemi etkilememeli
            }
        }

        /// <summary>
        /// Konfigürasyon değerinin belirtilen tipe uygun olup olmadığını doğrular
        /// 
        /// Bu private method, konfigürasyon kayıtlarında belirtilen değerin, 
        /// belirtilen tipe uygun olup olmadığını kontrol eder.
        /// 
        /// Parametreler:
        /// - value: Doğrulanacak değer (string formatında)
        /// - type: Beklenen veri typei (String, Int, Double, Bool)
        /// 
        /// Dönen değer: true, false
        /// 
        /// Desteklenen typelar:
        /// - String: Herhangi bir string değer geçerlidir
        /// - Int: Sadece geçerli integer değerler
        /// - Double: Sadece geçerli double değerler
        /// - Bool: Sadece "true" veya "false" değerleri
        /// </summary>
        private bool IsValidValue(string value, ConfigurationType type)
        {
            try
            {
                switch (type)
                {
                    case ConfigurationType.String:
                        return true; // Herhangi bir string değer geçerlidir
                    case ConfigurationType.Int:
                        return int.TryParse(value, out _);
                    case ConfigurationType.Double:
                        return double.TryParse(value, out _);
                    case ConfigurationType.Bool:
                        return bool.TryParse(value, out _);
                    default:
                        return false; // Desteklenmeyen tip
                }
            }
            catch
            {
                return false; // Parse hatası durumunda geçersiz
            }
        }
    }
}


