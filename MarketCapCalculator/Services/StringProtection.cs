using System;
using System.Security.Cryptography;
using System.Text;

namespace MarketCapCalculator.Services
{
    
    // Protezione delle stringhe sensibili
    
    public static class StringProtection
    {
        private static readonly byte[] Key = { 0x4A, 0x8B, 0x3C, 0x9D, 0x1E, 0x7F, 0x2A, 0x5B, 0x6C, 0x8D, 0x3E, 0x9F, 0x4A, 0x1B, 0x7C, 0x2D };
        
        
        // Cripta una stringa in modo reversibile
        
        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;
            
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.GenerateIV();
            
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
            Array.Copy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
            
            return Convert.ToBase64String(result);
        }
        
        
        // Decripta una stringa protetta
        
        public static string Unprotect(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;
            
            var fullCipher = Convert.FromBase64String(cipherText);
            
            using var aes = Aes.Create();
            aes.Key = Key;
            
            var iv = new byte[16];
            var cipherBytes = new byte[fullCipher.Length - 16];
            Array.Copy(fullCipher, 0, iv, 0, 16);
            Array.Copy(fullCipher, 16, cipherBytes, 0, cipherBytes.Length);
            
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            
            return Encoding.UTF8.GetString(plainBytes);
        }
        
        
        // Distrugge i dati sensibili dalla memoria
        
        public static void SecureWipe(byte[] data)
        {
            if (data == null)
                return;
            
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 0;
            }
        }
    }
}