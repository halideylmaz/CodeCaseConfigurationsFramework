using DynamicConfiguration.Repositories;
using DynamicConfiguration.Messaging;
using Microsoft.Extensions.Options;

// Configuration Manager - Ana uygulama
var builder = WebApplication.CreateBuilder(args);

// Web API servisleri
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

// MongoDB ve RabbitMQ ayarları
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));

// RabbitMQ settings
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQSettings"));

// Repository ve message publisher kayıtları
builder.Services.AddSingleton<IConfigurationRepository>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>();
    var logger = serviceProvider.GetRequiredService<ILogger<MongoConfigurationRepository>>();
    return new MongoConfigurationRepository(settings, logger);
});

// Message publisher
builder.Services.AddSingleton<IMessagePublisher>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<RabbitMQSettings>>();
    var logger = serviceProvider.GetRequiredService<ILogger<RabbitMQMessagePublisher>>();
    return new RabbitMQMessagePublisher(settings, logger);
});

// CORS ayarları
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// App configuration

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Swagger UI
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dinamik Konfigürasyon API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Ana sayfa - index.html
app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.Run();
