using System;
using System.IO;
using System.Threading.Tasks;

namespace MarketCapCalculator.Services
{
    public class PasswordRecoveryService
    {
        private readonly string _recoveryFile;
        private readonly TelegramBotService _telegramBot;

        public PasswordRecoveryService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MarketCapCalculator");
            Directory.CreateDirectory(appFolder);
            _recoveryFile = Path.Combine(appFolder, "recovery_code.txt");
            _telegramBot = new TelegramBotService();
        }

        public async Task<string?> SendRecoveryCodeAsync()
        {
            var code = _telegramBot.GenerateRecoveryCode();
            File.WriteAllText(_recoveryFile, code);

            try
            {
                await _telegramBot.SendRecoveryCodeAsync(code);
            }
            catch { }

            return code;
        }

        public bool VerifyRecoveryCode(string code)
        {
            if (!File.Exists(_recoveryFile)) return false;
            var savedCode = File.ReadAllText(_recoveryFile);
            return savedCode == code;
        }
    }
}