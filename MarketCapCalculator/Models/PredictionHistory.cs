using System;

namespace MarketCapCalculator.Models
{
    
    /// Modello per salvare una predizione di mercato nella storia
    
    public class PredictionHistory
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal InvestmentAmount { get; set; }
        public decimal CurrentMarketCap { get; set; }
        public decimal TargetMarketCap { get; set; }
        public decimal GrowthMultiplier { get; set; }
        public decimal FutureValue { get; set; }
        public decimal NetProfit { get; set; }
        public decimal RoiPercentage { get; set; }
        public string? TokenName { get; set; }
        public string? Notes { get; set; }
        
        public string DisplayTimestamp => Timestamp.ToString("dd/MM/yyyy HH:mm");
        public string DisplayMarketCap => Services.MarketCapParser.FormatCompact(CurrentMarketCap);
        public string DisplayTarget => Services.MarketCapParser.FormatCompact(TargetMarketCap);
        public string DisplayROI => $"{RoiPercentage:N1}%";
        public string DisplayProfit => $"€ {NetProfit:N2}";
    }
}