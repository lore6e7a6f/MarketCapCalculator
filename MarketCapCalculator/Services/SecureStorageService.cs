using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MarketCapCalculator.Services
{
    public class SecureStorageService
    {
        private readonly string _dataFolder;
        private readonly string _walletsFile;
        private readonly string _historyFile;
        private readonly string _configFile;
        private readonly string _passwordHashFile;
        private readonly string _apiKeyPromptedFile;

        public SecureStorageService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dataFolder = Path.Combine(appDataPath, "MarketCapCalculator");
            Directory.CreateDirectory(_dataFolder);

            _walletsFile = Path.Combine(_dataFolder, "wallets.enc");
            _historyFile = Path.Combine(_dataFolder, "history.enc");
            _configFile = Path.Combine(_dataFolder, "config.enc");
            _passwordHashFile = Path.Combine(_dataFolder, "password.hash");
            _apiKeyPromptedFile = Path.Combine(_dataFolder, "api_key_prompted.flag");
        }

        public void SavePasswordHash(string password)
        {
            var hash = ComputeHash(password);
            File.WriteAllText(_passwordHashFile, hash);
        }

        public bool VerifyPassword(string password)
        {
            try
            {
                if (!File.Exists(_passwordHashFile)) return false;
                var savedHash = File.ReadAllText(_passwordHashFile);
                var inputHash = ComputeHash(password);
                return savedHash == inputHash;
            }
            catch
            {
                return false;
            }
        }

        private string ComputeHash(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public void SaveWallets<T>(T data, EncryptionService encryption)
        {
            try
            {
                var encrypted = encryption.EncryptObject(data);
                File.WriteAllText(_walletsFile, encrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveWallets error: {ex.Message}");
            }
        }

        public T? LoadWallets<T>(EncryptionService encryption)
        {
            try
            {
                if (!File.Exists(_walletsFile)) return default;
                var encrypted = File.ReadAllText(_walletsFile);
                return encryption.DecryptObject<T>(encrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadWallets error: {ex.Message}");
                return default;
            }
        }

        public void SaveHistory<T>(T data, EncryptionService encryption)
        {
            try
            {
                var encrypted = encryption.EncryptObject(data);
                File.WriteAllText(_historyFile, encrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveHistory error: {ex.Message}");
            }
        }

        public T? LoadHistory<T>(EncryptionService encryption)
        {
            try
            {
                if (!File.Exists(_historyFile)) return default;
                var encrypted = File.ReadAllText(_historyFile);
                return encryption.DecryptObject<T>(encrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadHistory error: {ex.Message}");
                return default;
            }
        }

        public void InitializeStorage(EncryptionService encryption, string password)
        {
            SavePasswordHash(password);

            var emptyWallets = new System.Collections.Generic.List<object>();
            var encryptedWallets = encryption.EncryptObject(emptyWallets);
            File.WriteAllText(_walletsFile, encryptedWallets);

            var emptyHistory = new System.Collections.Generic.List<object>();
            var encryptedHistory = encryption.EncryptObject(emptyHistory);
            File.WriteAllText(_historyFile, encryptedHistory);
        }

        public bool HasEncryptedData()
        {
            try
            {
                return File.Exists(_passwordHashFile);
            }
            catch
            {
                return false;
            }
        }

        public bool HasApiKeyPrompted()
        {
            return File.Exists(_apiKeyPromptedFile);
        }

        public void SetApiKeyPrompted()
        {
            try
            {
                File.WriteAllText(_apiKeyPromptedFile, DateTime.Now.ToString());
            }
            catch { }
        }

        public void ReEncryptAllData(EncryptionService oldEncryption, EncryptionService newEncryption)
        {
            try
            {
                if (File.Exists(_walletsFile))
                {
                    var encrypted = File.ReadAllText(_walletsFile);
                    var decrypted = oldEncryption.Decrypt(encrypted);
                    var newEncrypted = newEncryption.Encrypt(decrypted);
                    File.WriteAllText(_walletsFile, newEncrypted);
                }

                if (File.Exists(_historyFile))
                {
                    var encrypted = File.ReadAllText(_historyFile);
                    var decrypted = oldEncryption.Decrypt(encrypted);
                    var newEncrypted = newEncryption.Encrypt(decrypted);
                    File.WriteAllText(_historyFile, newEncrypted);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReEncrypt error: {ex.Message}");
            }
        }

        public void WipeAllData()
        {
            try
            {
                if (File.Exists(_walletsFile)) File.Delete(_walletsFile);
                if (File.Exists(_historyFile)) File.Delete(_historyFile);
                if (File.Exists(_configFile)) File.Delete(_configFile);
                if (File.Exists(_passwordHashFile)) File.Delete(_passwordHashFile);
            }
            catch { }
        }
    }
}