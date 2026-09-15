using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace MarketCapCalculator.Services
{
    
    // Servizio per l'invio di notifiche via Telegram.
    // Legge i dati di configurazione da TelegramConfigStorage.
    
    public class TelegramBotService
    {
        private readonly HttpClient _httpClient;

        public TelegramBotService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        
        // Invia un codice di recupero all'utente configurato.
        
        public async Task<bool> SendRecoveryCodeAsync(string recoveryCode)
        {
            var config = TelegramConfigStorage.Load();
            if (config == null || string.IsNullOrEmpty(config.BotToken) || string.IsNullOrEmpty(config.ChatId))
            {
                System.Diagnostics.Debug.WriteLine("Telegram non configurato.");
                return false;
            }

            try
            {
                var message =
                    $" <b>RECUPERO PASSWORD</b>\n\n" +
                    $"Il tuo codice di recupero è:\n\n" +
                    $"<code>{recoveryCode}</code>\n\n" +
                    $" Scade tra 10 minuti";

                var url = $"https://api.telegram.org/bot{config.BotToken}/sendMessage";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("chat_id", config.ChatId),
                    new KeyValuePair<string, string>("text", message),
                    new KeyValuePair<string, string>("parse_mode", "HTML")
                });

                var response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Telegram error: {ex.Message}");
                return false;
            }
        }

        
        // Genera un codice di recupero casuale a 6 cifre.
        
        public string GenerateRecoveryCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        
        // Invia un messaggio generico all'utente.
        
        public async Task<bool> SendMessageAsync(string text)
        {
            var config = TelegramConfigStorage.Load();
            if (config == null) return false;

            try
            {
                var url = $"https://api.telegram.org/bot{config.BotToken}/sendMessage";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("chat_id", config.ChatId),
                    new KeyValuePair<string, string>("text", text),
                    new KeyValuePair<string, string>("parse_mode", "HTML")
                });

                var response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}