namespace MarketCapCalculator.Models
{
    /// <summary>
    /// Modello per i risultati del calcolo di investimento
    /// </summary>
    public class InvestmentCalculation
    {
        public decimal InvestmentAmount { get; set; }
        public decimal CurrentMarketCap { get; set; }
        public decimal TargetMarketCap { get; set; }
        public decimal GrowthMultiplier { get; set; }
        public decimal FutureValue { get; set; }
        public decimal NetProfit { get; set; }
        public decimal RoiPercentage { get; set; }
        public decimal? CurrentTokenPrice { get; set; }
        public decimal? TokenSupply { get; set; }
        public decimal? TokensAcquired { get; set; }
        public decimal? EstimatedTokenPrice { get; set; }
    }
}