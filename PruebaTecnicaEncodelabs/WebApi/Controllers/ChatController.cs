using Domain.Entities;
using Infraestructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;


namespace TelegramChatInbox.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatController> logger)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // 1. Ver el listado de contactos ordenados por el último mensaje recibido [3.1]
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts()
    {
        var contacts = await _context.Contacts
            .OrderByDescending(c => c.LastMessageAt)
            .Select(c => new
            {
                c.Id,
                c.Username,
                c.FirstName,
                c.UnreadCount,
                c.LastMessagePreview,
                c.LastMessageAt
            })
            .ToListAsync();

        return Ok(contacts);
    }

    // 2. Abrir una conversación, ver su historial y marcar los mensajes como leídos [3.1]
    [HttpGet("contacts/{chatId}/messages")]
    public async Task<IActionResult> GetChatHistory(long chatId)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == chatId);
        if (contact == null) return NotFound("El contacto especificado no existe.");

        // Traer el historial completo ordenado cronológicamente [3.1]
        var messages = await _context.Messages
            .Where(m => m.ContactId == chatId)
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                m.Id,
                m.Text,
                m.SentAt,
                m.IsFromOperator,
                m.IsRead
            })
            .ToListAsync();

        // Si el contacto tiene mensajes pendientes de lectura, limpiamos el contador [3.1]
        if (contact.UnreadCount > 0)
        {
            contact.UnreadCount = 0;

            // Marcar todos los mensajes entrantes de este chat como leídos [3.1]
            await _context.Messages
                .Where(m => m.ContactId == chatId && !m.IsFromOperator && !m.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

            await _context.SaveChangesAsync();
        }

        return Ok(new { Contact = contact, Messages = messages });
    }

    // 3. Responder al contacto desde la bandeja y enviarlo a Telegram [3.1]
    [HttpPost("contacts/{chatId}/reply")]
    public async Task<IActionResult> ReplyToContact(long chatId, [FromBody] ReplyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("El texto del mensaje no puede estar vacío.");

        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == chatId);
        if (contact == null) return NotFound("El contacto no existe.");

        // A. Despachar el mensaje hacia los servidores de Telegram [3.1]
        var botToken = _configuration["Telegram:BotToken"];
        var telegramUrl = $"https://telegram.org{botToken}/sendMessage";

        var payload = new
        {
            chat_id = chatId,
            text = request.Text
        };

        var client = _httpClientFactory.CreateClient();
        var jsonPayload = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(telegramUrl, jsonPayload);
            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                _logger.LogError("Telegram rechazó el mensaje del operador. Detalle: {Error}", errorDetails);
                return StatusCode(500, "Error de comunicación con la API de Telegram.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo crítico al conectar con la API de Telegram.");
            return StatusCode(500, "No se pudo despachar el mensaje a Telegram.");
        }

        // B. Si Telegram lo aceptó con éxito, lo persistimos en nuestra base de datos [3.1, 3.2]
        var utcNow = DateTime.UtcNow;
        var newMessage = new Message
        {
            Id = Guid.NewGuid(),
            ContactId = chatId,
            Text = request.Text,
            SentAt = utcNow,
            IsFromOperator = true, // Identifica que salió de nuestra bandeja web [3.1]
            IsRead = true
        };

        // Actualizar la vista previa del contacto
        contact.LastMessagePreview = request.Text;
        contact.LastMessageAt = utcNow;

        _context.Messages.Add(newMessage);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            newMessage.Id,
            newMessage.Text,
            newMessage.SentAt,
            newMessage.IsFromOperator
        });
    }
}

// DTO simple para recibir el cuerpo de la respuesta
public record ReplyRequest(string Text);
