using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MarketCapCalculator.Services
{
    public class EncryptionService
    {
        private const int SaltSize = 32;
        private const int KeySize = 32;
        private const int IvSize = 16;
        private const int Iterations = 100000;

        private readonly byte[] _salt;
        private readonly byte[] _key;

        public EncryptionService(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password richiesta per la crittografia");

            _salt = LoadOrCreateSalt();

            using var deriveBytes = new Rfc2898DeriveBytes(
                password, _salt, Iterations, HashAlgorithmName.SHA256);

            _key = deriveBytes.GetBytes(KeySize);
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var result = new byte[IvSize + cipherBytes.Length];
            Array.Copy(aes.IV, 0, result, 0, IvSize);
            Array.Copy(cipherBytes, 0, result, IvSize, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            var iv = new byte[IvSize];
            var cipherBytes = new byte[fullCipher.Length - IvSize];
            Array.Copy(fullCipher, 0, iv, 0, IvSize);
            Array.Copy(fullCipher, IvSize, cipherBytes, 0, cipherBytes.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }

        public string EncryptObject<T>(T obj)
        {
            var json = JsonSerializer.Serialize(obj);
            return Encrypt(json);
        }

        public T? DecryptObject<T>(string cipherText)
        {
            var json = Decrypt(cipherText);
            if (string.IsNullOrEmpty(json)) return default;
            return JsonSerializer.Deserialize<T>(json);
        }

        private byte[] LoadOrCreateSalt()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MarketCapCalculator");
            Directory.CreateDirectory(appFolder);

            var saltPath = Path.Combine(appFolder, "salt.bin");

            if (File.Exists(saltPath))
            {
                return File.ReadAllBytes(saltPath);
            }

            var salt = new byte[SaltSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);

            File.WriteAllBytes(saltPath, salt);
            return salt;
        }

        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}