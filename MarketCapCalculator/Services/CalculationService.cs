using System;
using System.Collections.Generic;
using System.Linq;
using MarketCapCalculator.Models;

namespace MarketCapCalculator.Services
{
    
    // Servizio di business logic per i calcoli finanziari
    
    public static class CalculationService
    {
        public static InvestmentCalculation CalculateInvestment(
            decimal investment,
            decimal currentMarketCap,
            decimal targetMarketCap,
            decimal? tokenPrice = null,
            decimal? tokenSupply = null)
        {
            if (investment <= 0)
                throw new ArgumentException("L'investimento deve essere positivo");
            if (currentMarketCap <= 0)
                throw new ArgumentException("Il Market Cap attuale deve essere positivo");
            if (targetMarketCap <= currentMarketCap)
                throw new ArgumentException("Il Market Cap target deve essere maggiore di quello attuale");

            var multiplier = targetMarketCap / currentMarketCap;
            var futureValue = investment * multiplier;
            var profit = futureValue - investment;
            var roi = (profit / investment) * 100;

            var result = new InvestmentCalculation
            {
                InvestmentAmount = investment,
                CurrentMarketCap = currentMarketCap,
                TargetMarketCap = targetMarketCap,
                GrowthMultiplier = multiplier,
                FutureValue = futureValue,
                NetProfit = profit,
                RoiPercentage = roi,
                CurrentTokenPrice = tokenPrice,
                TokenSupply = tokenSupply
            };

            if (tokenPrice.HasValue && tokenPrice.Value > 0)
            {
                result.TokensAcquired = investment / tokenPrice.Value;
                
                if (tokenSupply.HasValue && tokenSupply.Value > 0)
                {
                    result.EstimatedTokenPrice = targetMarketCap / tokenSupply.Value;
                }
                else
                {
                    result.EstimatedTokenPrice = tokenPrice.Value * multiplier;
                }
            }

            return result;
        }

        public static ReverseCalculation CalculateRequiredMarketCap(
            decimal investment,
            decimal desiredProfit,
            decimal currentMarketCap)
        {
            if (investment <= 0)
                throw new ArgumentException("L'investimento deve essere positivo");
            if (desiredProfit <= 0)
                throw new ArgumentException("Il profitto desiderato deve essere positivo");
            if (currentMarketCap <= 0)
                throw new ArgumentException("Il Market Cap attuale deve essere positivo");

            var desiredFinalValue = investment + desiredProfit;
            var requiredMultiplier = desiredFinalValue / investment;
            var requiredMarketCap = currentMarketCap * requiredMultiplier;

            return new ReverseCalculation
            {
                InvestmentAmount = investment,
                DesiredProfit = desiredProfit,
                CurrentMarketCap = currentMarketCap,
                RequiredMarketCap = requiredMarketCap,
                RequiredMultiplier = requiredMultiplier,
                FinalValue = desiredFinalValue
            };
        }

        public static IEnumerable<ScenarioRow> GenerateScenarios(
            decimal investment,
            decimal currentMarketCap,
            decimal targetMarketCap)
        {
            var scenarios = new List<ScenarioRow>();
            var multiplierValues = new[] { 2m, 3m, 5m, 10m, 20m, 50m, 100m };
            
            scenarios.Add(new ScenarioRow
            {
                MarketCap = currentMarketCap,
                MarketCapDisplay = MarketCapParser.FormatCompact(currentMarketCap),
                Multiplier = 1,
                MultiplierDisplay = "1x",
                InvestmentValue = investment,
                InvestmentValueDisplay = $"€ {investment:N2}",
                IsTargetScenario = false
            });

            foreach (var multiplier in multiplierValues)
            {
                var marketCap = currentMarketCap * multiplier;
                var value = investment * multiplier;
                
                scenarios.Add(new ScenarioRow
                {
                    MarketCap = marketCap,
                    MarketCapDisplay = MarketCapParser.FormatCompact(marketCap),
                    Multiplier = multiplier,
                    MultiplierDisplay = $"{multiplier}x",
                    InvestmentValue = value,
                    InvestmentValueDisplay = $"€ {value:N2}",
                    IsTargetScenario = Math.Abs(marketCap - targetMarketCap) < marketCap * 0.01m
                });
            }

            var targetMultiplier = targetMarketCap / currentMarketCap;
            if (!scenarios.Any(s => s.IsTargetScenario))
            {
                scenarios.Add(new ScenarioRow
                {
                    MarketCap = targetMarketCap,
                    MarketCapDisplay = MarketCapParser.FormatCompact(targetMarketCap),
                    Multiplier = targetMultiplier,
                    MultiplierDisplay = $"{targetMultiplier:0.##}x",
                    InvestmentValue = investment * targetMultiplier,
                    InvestmentValueDisplay = $"€ {investment * targetMultiplier:N2}",
                    IsTargetScenario = true
                });
            }

            return scenarios
                .GroupBy(s => s.MarketCap)
                .Select(g => g.First())
                .OrderBy(s => s.MarketCap)
                .ToList();
        }

        public static (decimal[] marketCaps, decimal[] values, string[] labels) GenerateChartData(
            decimal investment,
            decimal currentMarketCap,
            decimal targetMarketCap)
        {
            const int points = 50;
            var marketCaps = new decimal[points];
            var values = new decimal[points];
            var labels = new string[points];

            var ratio = (decimal)Math.Pow(
                (double)(targetMarketCap / currentMarketCap), 
                1.0 / (points - 1));

            for (int i = 0; i < points; i++)
            {
                var multiplier = (decimal)Math.Pow((double)ratio, i);
                marketCaps[i] = currentMarketCap * multiplier;
                values[i] = investment * multiplier;
                labels[i] = MarketCapParser.FormatCompact(marketCaps[i]);
            }

            return (marketCaps, values, labels);
        }
    }
}