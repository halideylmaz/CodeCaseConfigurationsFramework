using DynamicConfiguration.Repositories;
using DynamicConfiguration.Messaging;
using Microsoft.Extensions.Options;

// Dinamik Konfigürasyon Yönetim Sistemi - Ana Uygulama Başlatıcısı
// Bu dosya, konfigürasyon yönetimi için gerekli tüm servisleri yapılandırır
var builder = WebApplication.CreateBuilder(args);

// ========================================
// SERVİS KAYITLARI (Service Registrations)
// ========================================

// Web API servislerini ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Dinamik Konfigürasyon Yönetim Sistemi", 
        Version = "v1",
        Description = "Uygulamalar için dinamik konfigürasyon değerlerini yöneten API"
    });
});

// ========================================
// VERİTABANI VE MESAJLAŞMA YAPILANDIRMASI
// ========================================

// MongoDB bağlantı ayarlarını yapılandır
// Konfigürasyon kayıtları MongoDB'de saklanır
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));

// RabbitMQ mesajlaşma ayarlarını yapılandır
// Konfigürasyon değişiklikleri RabbitMQ ile diğer servislere bildirilir
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQSettings"));

// ========================================
// DEPENDENCY INJECTION KAYITLARI
// ========================================

// MongoDB konfigürasyon repository'sini kaydet
// Bu servis, konfigürasyon verilerini MongoDB'den okur/yazar
builder.Services.AddSingleton<IConfigurationRepository>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>();
    var logger = serviceProvider.GetRequiredService<ILogger<MongoConfigurationRepository>>();
    return new MongoConfigurationRepository(settings, logger);
});

// RabbitMQ mesaj yayıncısını kaydet
// Bu servis, konfigürasyon değişikliklerini diğer servislere bildirir
builder.Services.AddSingleton<IMessagePublisher>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<RabbitMQSettings>>();
    var logger = serviceProvider.GetRequiredService<ILogger<RabbitMQMessagePublisher>>();
    return new RabbitMQMessagePublisher(settings, logger);
});

// ========================================
// CORS YAPILANDIRMASI
// ========================================

// Cross-Origin Resource Sharing ayarları
// Web arayüzünden API'ye erişim için gerekli
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()      // Tüm kaynaklardan erişime izin ver
              .AllowAnyMethod()      // Tüm HTTP metodlarına izin ver
              .AllowAnyHeader();     // Tüm header'lara izin ver
    });
});

// ========================================
// UYGULAMA YAPILANDIRMASI
// ========================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Geliştirme ortamında Swagger UI'ı etkinleştir
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dinamik Konfigürasyon API v1");
        c.RoutePrefix = "swagger"; // Swagger UI'ı /swagger adresinde erişilebilir yap
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// ========================================
// ANA SAYFA YAPILANDIRMASI
// ========================================

// Ana sayfa olarak index.html'i sun
// Bu, kullanıcıların web arayüzüne erişmesini sağlar
app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.Run();
