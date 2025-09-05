using DynamicConfiguration.Models;
using DynamicConfiguration.Repositories;
using DynamicConfiguration.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace ConfigurationManager.Controllers
{
    /// <summary>Configuration management API controller.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationRepository _repository;
        private readonly IMessagePublisher _messagePublisher;
        private readonly ILogger<ConfigurationController> _logger;

        /// <summary>Constructor - DI ile servisleri alır.</summary>
        public ConfigurationController(
            IConfigurationRepository repository,
            IMessagePublisher messagePublisher,
            ILogger<ConfigurationController> logger)
        {
            _repository = repository;
            _messagePublisher = messagePublisher;
            _logger = logger;
        }

        /// <summary>Tüm konfigürasyonları listeler (filtreleme ile).</summary>
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

        /// <summary>Uygulamaya ait aktif konfigürasyonları getirir.</summary>
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

        /// <summary>Tek bir konfigürasyon kaydını getirir.</summary>
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

        /// <summary>Yeni konfigürasyon kaydı oluşturur.</summary>
        [HttpPost]
        public async Task<ActionResult<ConfigurationRecord>> CreateConfiguration([FromBody] ConfigurationRecordDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Value type validation
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

                // Change notification
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

        /// <summary>Konfigürasyon kaydını günceller.</summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateConfiguration(string id, [FromBody] ConfigurationRecordDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Value type validation
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

                // Change notification
                await PublishConfigurationChangeEventAsync(ConfigurationChangeType.Updated, record);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Id} konfigürasyonu güncellenemedi", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>Konfigürasyon kaydını siler.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteConfiguration(string id)
        {
            try
            {
                // Check if config exists
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

                // Change notification
                await PublishConfigurationChangeEventAsync(ConfigurationChangeType.Deleted, existingConfig);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Id} konfigürasyonu silinemedi", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>Servis sağlık durumunu kontrol eder.</summary>
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

        /// <summary>Config değişiklik olayını RabbitMQ'ya yayınlar.</summary>
        private async Task PublishConfigurationChangeEventAsync(ConfigurationChangeType changeType, ConfigurationRecord record)
        {
            try
            {
                // Change event oluştur
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

                // RabbitMQ'ya yayınla
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

        /// <summary>Value type validation yapar.</summary>
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


