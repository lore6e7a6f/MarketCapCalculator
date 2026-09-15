using System;

namespace MarketCapCalculator.Models
{
    public class WalletAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime DateAdded { get; set; } = DateTime.Now;
        public decimal? SolBalance { get; set; }
        public decimal? EurBalance { get; set; }
        public DateTime? LastUpdated { get; set; }
        public bool IsActive { get; set; }

        public string DisplayName => string.IsNullOrEmpty(Name) ?
            $"{Address.Substring(0, Math.Min(8, Address.Length))}..." : Name;

        public string DisplayAddress => Address.Length > 20 ?
            $"{Address.Substring(0, 10)}...{Address.Substring(Address.Length - 10)}" : Address;

        public string DisplaySolBalance => SolBalance.HasValue ?
            $"{SolBalance.Value:F6} SOL" : "N/D";

        public string DisplayEurBalance => EurBalance.HasValue ?
            $"€ {EurBalance.Value:F6}" : "€ 0.000000";

        public string DisplayLastUpdated => LastUpdated.HasValue ?
            LastUpdated.Value.ToString("dd/MM/yyyy HH:mm") : "Mai";
    }
}