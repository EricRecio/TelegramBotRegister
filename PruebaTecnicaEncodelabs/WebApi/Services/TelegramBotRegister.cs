using System.Text.Json;

namespace WebApi.Services
{
    public class TelegramBotRegister : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramBotRegister> _logger;

        public TelegramBotRegister(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<TelegramBotRegister> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando proceso de registro automático del Webhook...");

            // 1. Esperar un momento a que el contenedor de ngrok esté completamente activo
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            string publicUrl = string.Empty;
            var ngrokClient = _httpClientFactory.CreateClient();

            // 2. Intentar obtener la URL pública desde la API local de ngrok
            try
            {
                var response = await ngrokClient.GetAsync("http://ngrok:4040/api/tunnels", stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(stoppingToken);
                    using var doc = JsonDocument.Parse(content);

                    // Extrae la primera URL pública disponible (usualmente la https://)
                    publicUrl = doc.RootElement
                        .GetProperty("tunnels")[0]
                        .GetProperty("public_url")
                        .GetString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al conectar con la API local de ngrok. Asegúrate de que el contenedor está corriendo.");
                return;
            }

            if (string.IsNullOrEmpty(publicUrl))
            {
                _logger.LogError("No se pudo resolver la URL pública de ngrok.");
                return;
            }

            _logger.LogInformation("URL pública de ngrok detectada con éxito: {PublicUrl}", publicUrl);

            // 3. Registrar el webhook directamente en los servidores de Telegram
            var token = _configuration["Telegram:BotToken"];
            var webhookUrl = $"{publicUrl}/api/telegram/webhook"; // Endpoint que crearemos en el controlador

            var telegramClient = _httpClientFactory.CreateClient();
            var telegramEndpoint = $"https://telegram.org{token}/setWebhook?url={webhookUrl}";

            try
            {
                var telegramResponse = await telegramClient.PostAsync(telegramEndpoint, null, stoppingToken);
                if (telegramResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("¡Webhook registrado exitosamente en Telegram apuntando a: {WebhookUrl}!", webhookUrl);
                }
                else
                {
                    var errorContent = await telegramResponse.Content.ReadAsStringAsync(stoppingToken);
                    _logger.LogError("Telegram rechazó el registro del Webhook. Detalle: {Error}", errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un fallo crítico al intentar registrar el webhook en la API de Telegram.");
            }
        }
    }
}
