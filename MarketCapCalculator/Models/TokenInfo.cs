using System;

namespace MarketCapCalculator.Models
{
    public class TokenInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal MarketCap { get; set; }
        public decimal MarketCapRank { get; set; }
        public decimal TotalVolume { get; set; }
        public decimal PriceChangePercentage24h { get; set; }
        public decimal CirculatingSupply { get; set; }
        public decimal TotalSupply { get; set; }

        public string DisplaySymbol => Symbol.ToUpper();

        public string DisplayPrice
        {
            get
            {
                if (CurrentPrice <= 0)
                    return "N/D";

                if (CurrentPrice >= 1)
                    return $"€ {CurrentPrice:N4}";
                else if (CurrentPrice >= 0.01m)
                    return $"€ {CurrentPrice:N6}";
                else
                    return $"€ {CurrentPrice:N8}";
            }
        }

        public string DisplayMarketCap => MarketCap > 0 ?
            $"€ {MarketCap:N0}" : "N/D";

        public string DisplaySupply => CirculatingSupply > 0 ?
                        CirculatingSupply >= 1_000_000_000 ? $"{CirculatingSupply / 1_000_000_000m:0.##}B" :
                        CirculatingSupply >= 1_000_000 ? $"{CirculatingSupply / 1_000_000m:0.##}M" :
                        CirculatingSupply >= 1_000 ? $"{CirculatingSupply / 1_000m:0.##}K" :
                        $"{CirculatingSupply:N0}" : "N/D";

        // Formatta il prezzo per input con virgola
        public string PriceForInputWithComma
        {
            get
            {
                if (CurrentPrice <= 0)
                    return "";

                var formatted = CurrentPrice.ToString("F8").TrimEnd('0').TrimEnd('.');
                return formatted.Replace('.', ',');
            }
        }
    }
}