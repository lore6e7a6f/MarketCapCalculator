using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MarketCapCalculator.Services
{
    
    // Gestisce il salvataggio sicuro della chiave API di CoinMarketCap
    // usando DPAPI (Data Protection API) di Windows.
    public static class ApiKeyStorage
    {
        private static readonly string KeyFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarketCapCalculator", "cmc_api_key.bin");

        
        // Salva la chiave API cifrata con DPAPI.

        public static void SaveApiKey(string apiKey)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MarketCapCalculator");
            Directory.CreateDirectory(appFolder);

            byte[] plainBytes = Encoding.UTF8.GetBytes(apiKey);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFilePath, encryptedBytes);
        }

        
        // Carica la chiave API decifrata. Restituisce stringa vuota se non esiste.

        public static string LoadApiKey()
        {
            if (!File.Exists(KeyFilePath))
                return string.Empty;

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(KeyFilePath);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        
        // Verifica se una chiave API è stata salvata.

        public static bool HasApiKey()
        {
            return File.Exists(KeyFilePath);
        }

        
        // Elimina la chiave API salvata.

        public static void DeleteApiKey()
        {
            if (File.Exists(KeyFilePath))
                File.Delete(KeyFilePath);
        }
    }
}