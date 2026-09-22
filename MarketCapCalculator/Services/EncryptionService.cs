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
        // AES-GCM standard sizes
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int Iterations = 210_000; // OWASP 2023+ minimum for PBKDF2-SHA256

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

        /* Cifra con AES-256-GCM (crittografia autenticata: garantisce sia
        riservatezza che integrità - i dati non possono essere manomessi
        senza che la decifratura fallisca). */
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSize];

            using (var aesGcm = new AesGcm(_key, TagSize))
            {
                aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
            }

            // Formato: nonce (12) || tag (16) || ciphertext
            var result = new byte[NonceSize + TagSize + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(cipherBytes, 0, result, NonceSize + TagSize, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            var fullCipher = Convert.FromBase64String(cipherText);

            if (fullCipher.Length < NonceSize + TagSize)
                throw new CryptographicException("Dati cifrati corrotti o non validi.");

            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var cipherBytes = new byte[fullCipher.Length - NonceSize - TagSize];

            Buffer.BlockCopy(fullCipher, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(fullCipher, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(fullCipher, NonceSize + TagSize, cipherBytes, 0, cipherBytes.Length);

            var plainBytes = new byte[cipherBytes.Length];

            using (var aesGcm = new AesGcm(_key, TagSize))
            {
                // Se i dati sono stati alterati o la password è sbagliata,
                // questa chiamata lancia CryptographicException invece di
                // restituire silenziosamente dati corrotti/manomessi.
                aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

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

        // Crea un hash della password sicuro contro brute-force (PBKDF2 con
        // salt casuale per hash, dedicato e diverso dal salt di cifratura).
        // Formato restituito: "iterazioni.saltBase64.hashBase64"
        public static string HashPassword(string password)
        {
            var salt = new byte[SaltSize];
            RandomNumberGenerator.Fill(salt);

            using var deriveBytes = new Rfc2898DeriveBytes(
                password, salt, Iterations, HashAlgorithmName.SHA256);
            var hash = deriveBytes.GetBytes(KeySize);

            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        // Verifica una password contro un hash prodotto da HashPassword,
        // usando un confronto a tempo costante per evitare timing attack.
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;

            var parts = storedHash.Split('.');
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out var iterations)) return false;

            byte[] salt, expectedHash;
            try
            {
                salt = Convert.FromBase64String(parts[1]);
                expectedHash = Convert.FromBase64String(parts[2]);
            }
            catch (FormatException)
            {
                return false;
            }

            using var deriveBytes = new Rfc2898DeriveBytes(
                password, salt, iterations, HashAlgorithmName.SHA256);
            var actualHash = deriveBytes.GetBytes(expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}