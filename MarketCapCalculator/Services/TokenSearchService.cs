using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MarketCapCalculator.Models;

namespace MarketCapCalculator.Services
{
    
    // Scanner universale per crypto e memecoin
    // Cerca dinamicamente su DexScreener e Binance
    
    public class TokenSearchService
    {
        private readonly HttpClient _httpClient;
        private const string BinanceUrl = "https://api.binance.com/api/v3";
        private const string DexScreenerUrl = "https://api.dexscreener.com/latest/dex";

        public TokenSearchService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        
        // Cerca token dinamicamente su DexScreener
        
        public async Task<List<TokenInfo>> SearchTokensAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<TokenInfo>();

            var results = new List<TokenInfo>();

            // 1. Cerca su DexScreener (trova TUTTO, incluse memecoin)
            var dexResults = await SearchDexScreenerAsync(query);
            results.AddRange(dexResults);

            // 2. Cerca su Binance (per token principali)
            var binanceResults = await SearchBinanceAsync(query);
            foreach (var token in binanceResults)
            {
                if (!results.Any(r => r.Symbol == token.Symbol))
                {
                    results.Add(token);
                }
            }

            // 3. Se non trova nulla, prova con query parziale
            if (!results.Any() && query.Length > 3)
            {
                var partialQuery = query.Substring(0, Math.Min(4, query.Length));
                var partialResults = await SearchDexScreenerAsync(partialQuery);
                
                foreach (var token in partialResults)
                {
                    if (!results.Any(r => r.Symbol == token.Symbol))
                    {
                        results.Add(token);
                    }
                }
            }

            return results;
        }

        
        // Cerca su DexScreener in modo dinamico
        private async Task<List<TokenInfo>> SearchDexScreenerAsync(string query)
        {
            var tokens = new List<TokenInfo>();
            
            try
            {
                var url = $"{DexScreenerUrl}/search?q={Uri.EscapeDataString(query)}";
                System.Diagnostics.Debug.WriteLine($"DexScreener URL: {url}");
                
                var response = await _httpClient.GetAsync(url);
                System.Diagnostics.Debug.WriteLine($"DexScreener Status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"DexScreener Response (first 500 chars): {json.Substring(0, Math.Min(500, json.Length))}");
                    
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("pairs", out var pairs) && pairs.ValueKind == JsonValueKind.Array)
                    {
                        var seenAddresses = new HashSet<string>();
                        
                        foreach (var pair in pairs.EnumerateArray().Take(50))
                        {
                            try
                            {
                                var baseToken = pair.TryGetProperty("baseToken", out var bt) ? bt : default;
                                
                                if (baseToken.ValueKind != JsonValueKind.Object)
                                    continue;
                                
                                var symbol = baseToken.TryGetProperty("symbol", out var sym) ? sym.GetString() ?? "" : "";
                                var name = baseToken.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                                var address = baseToken.TryGetProperty("address", out var addr) ? addr.GetString() ?? "" : "";
                                var chainId = pair.TryGetProperty("chainId", out var chain) ? chain.GetString() ?? "" : "";
                                
                                if (string.IsNullOrEmpty(symbol) || seenAddresses.Contains(address))
                                    continue;
                                
                                seenAddresses.Add(address);

                                // Nel metodo SearchDexScreenerAsync, dentro il foreach
                                var priceUsd = 0m;
                                if (pair.TryGetProperty("priceUsd", out var pu) && pu.ValueKind == JsonValueKind.String)
                                {
                                    priceUsd = ParseDecimalSafe(pu.GetString() ?? "0");
                                }

                                // Cerca il market cap in diversi campi
                                var marketCapUsd = 0m;

                                // 1. Prova campo "marketCap"
                                if (pair.TryGetProperty("marketCap", out var mc) && mc.ValueKind == JsonValueKind.String)
                                {
                                    marketCapUsd = ParseDecimalSafe(mc.GetString() ?? "0");
                                }

                                // 2. Se non c'è, prova "fdv" (Fully Diluted Valuation)
                                if (marketCapUsd == 0 && pair.TryGetProperty("fdv", out var fdv) && fdv.ValueKind == JsonValueKind.String)
                                {
                                    marketCapUsd = ParseDecimalSafe(fdv.GetString() ?? "0");
                                }

                                // 3. Se ancora 0, calcola dal prezzo e supply
                                if (marketCapUsd == 0 && priceUsd > 0)
                                {
                                    // Cerca la supply nel token
                                    var supply = 0m;

                                    // Prova a ottenere la supply da diversi campi
                                    if (pair.TryGetProperty("liquidity", out var liquidity) &&
                                        liquidity.TryGetProperty("base", out var baseLiquidity) &&
                                        baseLiquidity.ValueKind == JsonValueKind.Number)
                                    {
                                        // Se c'è liquidità, possiamo stimare la supply
                                        // ma non è preciso
                                    }

                                    // Usa una supply stimata basata sul prezzo
                                    if (priceUsd < 0.000001m)
                                        supply = 1_000_000_000_000m; // Token con supply enorme
                                    else if (priceUsd < 0.001m)
                                        supply = 100_000_000_000m;
                                    else if (priceUsd < 0.01m)
                                        supply = 10_000_000_000m;
                                    else if (priceUsd < 0.1m)
                                        supply = 1_000_000_000m;
                                    else if (priceUsd < 1m)
                                        supply = 100_000_000m;
                                    else if (priceUsd < 100m)
                                        supply = 10_000_000m;
                                    else
                                        supply = 1_000_000m;

                                    marketCapUsd = priceUsd * supply;
                                }

                                var volume24h = 0m;
                                if (pair.TryGetProperty("volume", out var vol) && vol.ValueKind == JsonValueKind.Object)
                                {
                                    if (vol.TryGetProperty("h24", out var vol24) && vol24.ValueKind == JsonValueKind.String)
                                    {
                                        volume24h = ParseDecimalSafe(vol24.GetString() ?? "0");
                                    }
                                }

                                // Converti USD a EUR
                                var eurPrice = priceUsd * 0.92m;
                                var eurMarketCap = marketCapUsd * 0.92m;
                                var eurVolume = volume24h * 0.92m;

                                var token = new TokenInfo
                                {
                                    Id = address,
                                    Symbol = symbol.ToUpper(),
                                    Name = name,
                                    CurrentPrice = eurPrice,
                                    MarketCap = eurMarketCap,
                                    TotalVolume = eurVolume,
                                    CirculatingSupply = eurPrice > 0 ? eurMarketCap / eurPrice : 0
                                };

                                tokens.Add(token);
                                
                                System.Diagnostics.Debug.WriteLine($"Found: {symbol} - {name} - MC: ${marketCapUsd}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Pair parse error: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No 'pairs' array in response");
                    }
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"DexScreener Error: {errorBody}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DexScreener Exception: {ex.Message}");
            }
            
            return tokens;
        }

        
        // Cerca su Binance in modo dinamico
        
        private async Task<List<TokenInfo>> SearchBinanceAsync(string query)
        {
            var tokens = new List<TokenInfo>();
            var symbol = query.ToUpper();
            
            try
            {
                // Prova direttamente su Binance con il simbolo
                var price = await GetBinancePriceAsync(symbol);
                
                if (price.HasValue)
                {
                    tokens.Add(new TokenInfo
                    {
                        Id = symbol.ToLower(),
                        Symbol = symbol,
                        Name = symbol,
                        CurrentPrice = price.Value,
                        MarketCap = price.Value * 1_000_000_000m,
                        CirculatingSupply = 1_000_000_000m
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Binance search error: {ex.Message}");
            }
            
            return tokens;
        }

        
        // Ottiene il prezzo da Binance
        
        private async Task<decimal?> GetBinancePriceAsync(string symbol)
        {
            try
            {
                // Prova EUR
                var response = await _httpClient.GetAsync($"{BinanceUrl}/ticker/price?symbol={symbol}EUR");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var priceStr = doc.RootElement.GetProperty("price").GetString() ?? "0";
                    return ParseDecimalSafe(priceStr);
                }
                
                // Prova USDT
                response = await _httpClient.GetAsync($"{BinanceUrl}/ticker/price?symbol={symbol}USDT");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var usdtPrice = ParseDecimalSafe(doc.RootElement.GetProperty("price").GetString() ?? "0");
                    return usdtPrice * 0.92m;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Binance price error: {ex.Message}");
            }
            
            return null;
        }

        
        // Ottiene il prezzo SOL
        
        public async Task<decimal?> GetSolPriceRealtimeAsync()
        {
            return await GetBinancePriceAsync("SOL");
        }

        
        // Parsi decimale sicuro
        
        private decimal ParseDecimalSafe(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;
            
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return 0;
        }
    }
}