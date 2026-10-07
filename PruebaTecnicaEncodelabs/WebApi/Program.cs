using Infraestructure.Hubs;
using Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TelegramChatInbox.Infrastructure.Messaging;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Obligatorio para SignalR WebSockets
    });
});

// 3. Registrar nuestros Hosted Services (El del Webhook y ahora el del Consumidor) [3.2]
builder.Services.AddHostedService<TelegramBotRegister>();
builder.Services.AddHostedService<TelegramMessageConsumer>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddHttpClient();

// 1. Registrar PostgreSQL DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSignalR();
builder.Services.AddHostedService<TelegramBotRegister>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())    
{
    app.MapOpenApi();
}

app.UseCors();
//app.UseHttpsRedirection();
app.UseAuthorization();

// ==============================================================================
// MIGRACIONES AUTOMÁTICAS AL INICIAR LA APLICACIÓN [3.2]
// ==============================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        logger.LogInformation("Verificando y aplicando migraciones pendientes en PostgreSQL...");

        // Aplica cualquier migración de Entity Framework Core pendiente de forma asíncrona [3.2]
        await context.Database.MigrateAsync();

        logger.LogInformation("¡Base de datos sincronizada y lista para operar exitosamente!");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ocurrió un error crítico al intentar migrar la base de datos de PostgreSQL.");
        // Opcional: Podrías relanzar la excepción si deseas que el contenedor falle explícitamente y se reinicie
    }
}


app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.Run();
