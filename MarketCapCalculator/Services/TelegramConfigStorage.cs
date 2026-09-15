using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MarketCapCalculator.Services
{
    // Gestisce la configurazione del bot Telegram (token e chat_id)
    // in modo sicuro usando DPAPI.
    
    public static class TelegramConfigStorage
    {
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarketCapCalculator", "telegram_config.bin");

        public class TelegramConfig
        {
            public string BotToken { get; set; } = string.Empty;
            public string ChatId { get; set; } = string.Empty;
        }

        // Salva la configurazione cifrata con DPAPI.
        
        public static void Save(string botToken, string chatId)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MarketCapCalculator");
            Directory.CreateDirectory(appFolder);

            var config = new TelegramConfig { BotToken = botToken, ChatId = chatId };
            var json = JsonSerializer.Serialize(config);
            var bytes = Encoding.UTF8.GetBytes(json);

            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(ConfigFilePath, encrypted);
        }

        // Carica la configurazione decifrata. Restituisce null se non esiste.
        
        public static TelegramConfig? Load()
        {
            if (!File.Exists(ConfigFilePath)) return null;

            try
            {
                var encrypted = File.ReadAllBytes(ConfigFilePath);
                var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize<TelegramConfig>(json);
            }
            catch
            {
                return null;
            }
        }

        // Verifica se la configurazione esiste.
        
        public static bool IsConfigured() => File.Exists(ConfigFilePath);

        // Elimina la configurazione salvata.
        
        public static void Delete()
        {
            if (File.Exists(ConfigFilePath)) File.Delete(ConfigFilePath);
        }
    }
}