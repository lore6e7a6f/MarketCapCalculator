using System;
using System.Security;

namespace MarketCapCalculator.Services
{
    public class SecurePassword : IDisposable
    {
        private SecureString? _secureString;
        private byte[]? _passwordBytes;

        public SecurePassword(string password)
        {
            _secureString = new SecureString();
            foreach (char c in password) _secureString.AppendChar(c);
            _secureString.MakeReadOnly();

            _passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        }

        public byte[] GetBytes() => _passwordBytes ?? Array.Empty<byte>();
        public SecureString GetSecureString() => _secureString ?? new SecureString();

        public void Dispose()
        {
            if (_passwordBytes != null)
            {
                Array.Clear(_passwordBytes, 0, _passwordBytes.Length);
                _passwordBytes = null;
            }
            if (_secureString != null)
            {
                _secureString.Dispose();
                _secureString = null;
            }
        }
    }
}