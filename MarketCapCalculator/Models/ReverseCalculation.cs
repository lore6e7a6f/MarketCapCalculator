namespace MarketCapCalculator.Models
{
    /// <summary>
    /// Modello per il calcolo reverse
    /// </summary>
    public class ReverseCalculation
    {
        public decimal InvestmentAmount { get; set; }
        public decimal DesiredProfit { get; set; }
        public decimal CurrentMarketCap { get; set; }
        public decimal RequiredMarketCap { get; set; }
        public decimal RequiredMultiplier { get; set; }
        public decimal FinalValue { get; set; }
        public decimal RoiPercentage => (DesiredProfit / InvestmentAmount) * 100;
    }
}