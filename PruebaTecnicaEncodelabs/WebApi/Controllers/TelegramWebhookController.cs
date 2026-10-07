using Application.Common.Events;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace TelegramChatInbox.WebApi.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(IConfiguration configuration, ILogger<TelegramWebhookController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveUpdate([FromBody] JsonElement update)
    {
        try
        {
            // 1. Validar que la actualización contenga un mensaje con texto (ignora imágenes/audios)
            if (!update.TryGetProperty("message", out var messageProperty) ||
                !messageProperty.TryGetProperty("text", out var textProperty))
            {
                return Ok(); // Respondemos 200 OK a Telegram para no reintentar
            }

            string text = textProperty.GetString() ?? string.Empty;

            // Extraer datos básicos del remitente
            var chatProperty = messageProperty.GetProperty("chat");
            long chatId = chatProperty.GetProperty("id").GetInt64();

            string firstName = chatProperty.TryGetProperty("first_name", out var fn) ? fn.GetString() ?? "" : "";
            string username = chatProperty.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "";

            // Convertir la fecha Unix de Telegram a DateTime UTC
            long unixDate = messageProperty.GetProperty("date").GetInt64();
            DateTime sentAt = DateTimeOffset.FromUnixTimeSeconds(unixDate).UtcDateTime;           
            _logger.LogInformation("Mensaje recibido en Webhook desde ChatId {ChatId}: {Text}", chatId, text);
        
            // 2. Mapear al evento de nuestra aplicación
            var receivedEvent = new TelegramMessageReceivedEvent(chatId, username, firstName, text, sentAt);

            // 3. Publicar el evento de forma asíncrona en RabbitMQ
            await PublishToQueueAsync(receivedEvent);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico procesando el webhook de Telegram.");
            return Ok(); // Mantenemos el Ok para evitar que Telegram se sature reintentando fallos de código interno
        }
    }

    private async Task PublishToQueueAsync(TelegramMessageReceivedEvent ev)
    {
        var factory = new ConnectionFactory()
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        // Creamos la conexión y el canal (Sintaxis moderna asíncrona)
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Declaramos el Exchange de tipo Fanout o Direct y la Cola de manera segura
        string exchangeName = "telegram.updates";
        string queueName = "telegram.received_messages";
        string routingKey = "telegram.message.received";

        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, durable: true);
        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);

        // Serializar el mensaje a JSON string
        var messageJson = JsonSerializer.Serialize(ev);
        var body = Encoding.UTF8.GetBytes(messageJson);

        // Publicar de forma persistente
        var properties = new BasicProperties
        {
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body
        );

        _logger.LogInformation("Mensaje publicado exitosamente en RabbitMQ [Cola: {Queue}]", queueName);
    }
}
