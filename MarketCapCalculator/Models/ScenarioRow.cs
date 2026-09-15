namespace MarketCapCalculator.Models
{
    /// <summary>
    /// Modello per una riga della tabella scenari
    /// </summary>
    public class ScenarioRow
    {
        public decimal MarketCap { get; set; }
        public string MarketCapDisplay { get; set; } = string.Empty;
        public decimal Multiplier { get; set; }
        public string MultiplierDisplay { get; set; } = string.Empty;
        public decimal InvestmentValue { get; set; }
        public string InvestmentValueDisplay { get; set; } = string.Empty;
        public bool IsTargetScenario { get; set; }
    }
}