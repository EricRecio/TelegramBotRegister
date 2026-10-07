using Application.Common.Events;
using Infraestructure.Hubs;
using Infraestructure.Persistence;
using Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace TelegramChatInbox.Infrastructure.Messaging;

public class TelegramMessageConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramMessageConsumer> _logger;
    private readonly IHubContext<ChatHub> _hubContext;
    private IConnection? _connection;
    private IChannel? _channel;

    public TelegramMessageConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<TelegramMessageConsumer> logger,
        IHubContext<ChatHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando Consumidor de RabbitMQ...");

        var factory = new ConnectionFactory()
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        string queueName = "telegram.received_messages";

        // Asegurar que la cola existe por si el consumidor levanta antes que el controlador
        await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);

            try
            {
                var telegramEvent = JsonSerializer.Deserialize<TelegramMessageReceivedEvent>(messageJson);
                if (telegramEvent != null)
                {
                    _logger.LogInformation("Procesando mensaje de cola para ChatId: {ChatId}", telegramEvent.ChatId);

                    // Procesar la lógica de negocio y guardar en Base de Datos
                    await ProcessAndPersistMessageAsync(telegramEvent);
                }

                // Confirmar procesamiento exitoso a RabbitMQ (Ack)
                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el mensaje extraído de RabbitMQ.");
                // En caso de error de infraestructura, rechazamos y reencolamos el mensaje para no perderlo
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        // Mantener el servicio vivo hasta que se cancele el token de la app
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessAndPersistMessageAsync(TelegramMessageReceivedEvent ev)
    {
        // Al ser un servicio Singleton (BackgroundService), debemos crear un Scope para usar el DbContext (Scoped) [3.2]
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. Buscar si el contacto ya existe en nuestra base de datos
        var contact = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == ev.ChatId);

        if (contact == null)
        {
            // Si el contacto es nuevo, lo creamos e inicializamos sus datos [3.1]
            contact = new Contact
            {
                Id = ev.ChatId,
                Username = string.IsNullOrEmpty(ev.Username) ? $"User_{ev.ChatId}" : ev.Username,
                FirstName = string.IsNullOrEmpty(ev.FirstName) ? "Usuario Telegram" : ev.FirstName,
                UnreadCount = 1,
                LastMessagePreview = ev.Text,
                LastMessageAt = ev.SentAt
            };
            dbContext.Contacts.Add(contact);
        }
        else
        {
            // Si ya existe, actualizamos su vista previa e incrementamos los no leídos [3.1]
            contact.UnreadCount++;
            contact.LastMessagePreview = ev.Text;
            contact.LastMessageAt = ev.SentAt;
            dbContext.Contacts.Update(contact);
        }

        // 2. Crear el nuevo mensaje asociado al contacto [3.1]
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ContactId = contact.Id,
            Text = ev.Text,
            SentAt = ev.SentAt,
            IsFromOperator = false, // Mensaje entrante de un cliente
            IsRead = false
        };
        dbContext.Messages.Add(message);

        // Guardar de forma transaccional en PostgreSQL
        await dbContext.SaveChangesAsync();

        // 3. ¡Tiempo Real! Notificar al Frontend a través de SignalR [3.1, 3.2]
        // Enviamos el evento "ReceiveMessage" con los datos actualizados para que Angular refresque la bandeja al instante
        await _hubContext.Clients.All.SendAsync("ReceiveMessage", new
        {
            ChatId = contact.Id,
            contact.Username,
            contact.FirstName,
            contact.UnreadCount,
            contact.LastMessagePreview,
            Message = new
            {
                message.Id,
                message.Text,
                message.SentAt,
                message.IsFromOperator
            }
        });
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
