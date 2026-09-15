using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace MarketCapCalculator.Services
{
    public class CandlestickService
    {
        private readonly HttpClient _httpClient;
        private const string BinanceUrl = "https://api.binance.com/api/v3";
        private const string DexScreenerUrl = "https://api.dexscreener.com/latest/dex";
        private const string CoinGeckoUrl = "https://api.coingecko.com/api/v3";
        private const string CoinMarketCapBaseUrl = "https://pro-api.coinmarketcap.com";

        private static List<CoinGeckoCoin>? _cachedCoins;
        private readonly string? _cmcApiKey;

        public CandlestickService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            _cmcApiKey = ApiKeyStorage.LoadApiKey();
            if (!string.IsNullOrEmpty(_cmcApiKey))
            {
                System.Diagnostics.Debug.WriteLine("CoinMarketCap API key caricata.");
            }
        }

        public async Task<decimal> GetAthAsync(string symbol)
        {
            var candles = await GetCandlesticksAsync(symbol, "1M");
            if (candles.Any())
                return candles.Max(c => c.High);
            return 0;
        }

        
        /// Ottiene candele reali. Priorità: Binance → CoinGecko → CoinMarketCap → DexScreener.
        /// Se non trova dati, restituisce lista vuota.
        
        public async Task<List<CandlestickData>> GetCandlesticksAsync(string symbol, string timeframe = "24H")
        {
            string interval;
            int limit;

            switch (timeframe)
            {
                case "1H": interval = "15m"; limit = 4; break;
                case "4H": interval = "1h"; limit = 4; break;
                case "24H": interval = "4h"; limit = 6; break;
                case "7D": interval = "1d"; limit = 7; break;
                case "1M": interval = "1d"; limit = 30; break;
                default: interval = "1h"; limit = 50; break;
            }

            // 1. Binance
            var candles = await GetFromBinanceAsync(symbol, interval, limit);
            if (candles.Any())
                return candles;

            // 2. CoinGecko
            candles = await GetFromCoinGeckoAsync(symbol, timeframe);
            if (candles.Any())
            {
                await EnrichCandlesWithMarketData(symbol, candles);
                return candles;
            }

            // 3. CoinMarketCap (priorità rispetto a DexScreener)
            candles = await GetFromCoinMarketCapAsync(symbol, timeframe);
            if (candles.Any())
            {
                await EnrichCandlesWithMarketData(symbol, candles);
                return candles;
            }

            // 4. DexScreener (ultimo fallback)
            candles = await GetFromDexScreenerCandlesAsync(symbol, timeframe);
            if (candles.Any())
            {
                await EnrichCandlesWithMarketData(symbol, candles);
                return candles;
            }

            // Nessun dato disponibile
            return new List<CandlestickData>();
        }

        private async Task<List<CandlestickData>> GetFromBinanceAsync(string symbol, string interval, int limit)
        {
            var candles = new List<CandlestickData>();
            try
            {
                var url = $"{BinanceUrl}/klines?symbol={symbol.ToUpper()}USDT&interval={interval}&limit={limit}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return candles;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                foreach (var candle in doc.RootElement.EnumerateArray())
                {
                    var supply = GetApproximateSupply(symbol);
                    var close = GetDecimalSafe(candle[4]);

                    candles.Add(new CandlestickData
                    {
                        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(candle[0].GetInt64()).DateTime,
                        Open = GetDecimalSafe(candle[1]),
                        High = GetDecimalSafe(candle[2]),
                        Low = GetDecimalSafe(candle[3]),
                        Close = close,
                        Volume = GetDecimalSafe(candle[5]),
                        Supply = supply,
                        MarketCap = close * supply
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Binance error: {ex.Message}");
            }
            return candles;
        }

        private async Task<List<CandlestickData>> GetFromCoinGeckoAsync(string symbol, string timeframe)
        {
            var candles = new List<CandlestickData>();
            try
            {
                var allCoins = await GetAllCoinsAsync();
                var coinId = FindCoinId(allCoins, symbol);

                if (string.IsNullOrEmpty(coinId))
                    return candles;

                int days = timeframe switch
                {
                    "1H" => 1,
                    "4H" => 1,
                    "24H" => 1,
                    "7D" => 7,
                    "1M" => 30,
                    _ => 1
                };

                var ohlcUrl = $"{CoinGeckoUrl}/coins/{coinId}/ohlc?vs_currency=usd&days={days}";
                var response = await _httpClient.GetAsync(ohlcUrl);
                if (!response.IsSuccessStatusCode)
                    return candles;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.GetArrayLength() < 5) continue;
                    var timestamp = item[0].GetInt64();
                    var open = GetDecimalSafe(item[1]);
                    var high = GetDecimalSafe(item[2]);
                    var low = GetDecimalSafe(item[3]);
                    var close = GetDecimalSafe(item[4]);

                    candles.Add(new CandlestickData
                    {
                        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime,
                        Open = open,
                        High = high,
                        Low = low,
                        Close = close,
                        Volume = 0,
                        Supply = 0,
                        MarketCap = 0
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CoinGecko error: {ex.Message}");
            }
            return candles;
        }

        private async Task<List<CandlestickData>> GetFromDexScreenerCandlesAsync(string symbol, string timeframe)
        {
            var candles = new List<CandlestickData>();
            try
            {
                var pairAddress = await GetPairAddressFromDexScreenerAsync(symbol);
                if (string.IsNullOrEmpty(pairAddress))
                    return candles;

                string resolution; int limit;
                switch (timeframe)
                {
                    case "1H": resolution = "15"; limit = 4; break;
                    case "4H": resolution = "60"; limit = 4; break;
                    case "24H": resolution = "240"; limit = 6; break;
                    case "7D": resolution = "1440"; limit = 7; break;
                    case "1M": resolution = "1440"; limit = 30; break;
                    default: resolution = "60"; limit = 50; break;
                }

                var url = $"{DexScreenerUrl}/candles?pairAddress={pairAddress}&resolution={resolution}&limit={limit}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"DexScreener candles HTTP: {response.StatusCode}");
                    return candles;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("candles", out var candlesArray))
                {
                    foreach (var item in candlesArray.EnumerateArray())
                    {
                        if (item.GetArrayLength() < 5) continue;
                        var timestamp = item[0].GetInt64();
                        var open = GetDecimalSafe(item[1]);
                        var high = GetDecimalSafe(item[2]);
                        var low = GetDecimalSafe(item[3]);
                        var close = GetDecimalSafe(item[4]);

                        candles.Add(new CandlestickData
                        {
                            OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime,
                            Open = open,
                            High = high,
                            Low = low,
                            Close = close,
                            Volume = 0,
                            Supply = 0,
                            MarketCap = 0
                        });
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("DexScreener: campo 'candles' mancante");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DexScreener candles error: {ex.Message}");
            }
            return candles;
        }

        
        /// Ottiene OHLCV storici da CoinMarketCap (richiede API key).
        
        private async Task<List<CandlestickData>> GetFromCoinMarketCapAsync(string symbol, string timeframe)
        {
            var candles = new List<CandlestickData>();
            if (string.IsNullOrEmpty(_cmcApiKey))
                return candles;

            try
            {
                long coinId = await GetCmcCoinIdAsync(symbol);
                if (coinId <= 0)
                    return candles;

                int count = timeframe switch
                {
                    "1H" => 1,
                    "4H" => 1,
                    "24H" => 1,
                    "7D" => 7,
                    "1M" => 30,
                    _ => 7
                };

                var url = $"{CoinMarketCapBaseUrl}/v2/cryptocurrency/ohlcv/historical?id={coinId}&time_period=daily&count={count}&interval=daily";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-CMC_PRO_API_KEY", _cmcApiKey);
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"CoinMarketCap HTTP: {response.StatusCode}");
                    return candles;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("quotes", out var quotes))
                {
                    foreach (var quote in quotes.EnumerateArray())
                    {
                        var timestamp = quote.TryGetProperty("time_open", out var t) ? t.GetDateTime() : DateTime.MinValue;
                        var quoteData = quote.TryGetProperty("quote", out var q) ? q : default;
                        var usd = quoteData.TryGetProperty("USD", out var usdData) ? usdData : default;

                        if (usd.ValueKind != JsonValueKind.Object) continue;

                        var open = GetDecimalSafe(usd.GetProperty("open"));
                        var high = GetDecimalSafe(usd.GetProperty("high"));
                        var low = GetDecimalSafe(usd.GetProperty("low"));
                        var close = GetDecimalSafe(usd.GetProperty("close"));
                        var volume = GetDecimalSafe(usd.GetProperty("volume"));

                        candles.Add(new CandlestickData
                        {
                            OpenTime = timestamp,
                            Open = open,
                            High = high,
                            Low = low,
                            Close = close,
                            Volume = volume,
                            Supply = 0,
                            MarketCap = 0
                        });
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("CoinMarketCap: struttura dati non valida.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CoinMarketCap error: {ex.Message}");
            }
            return candles;
        }

        private async Task<long> GetCmcCoinIdAsync(string symbol)
        {
            try
            {
                var url = $"{CoinMarketCapBaseUrl}/v1/cryptocurrency/map?symbol={symbol.ToUpper()}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-CMC_PRO_API_KEY", _cmcApiKey);
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return 0;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                {
                    return data[0].GetProperty("id").GetInt64();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCmcCoinId error: {ex.Message}");
            }
            return 0;
        }

        private async Task<string> GetPairAddressFromDexScreenerAsync(string symbol)
        {
            try
            {
                string url = symbol.Length >= 32
                    ? $"{DexScreenerUrl}/token/{symbol}"
                    : $"{DexScreenerUrl}/search?q={Uri.EscapeDataString(symbol)}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return string.Empty;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("pairs", out var pairs) && pairs.GetArrayLength() > 0)
                {
                    var firstPair = pairs[0];
                    if (firstPair.TryGetProperty("pairAddress", out var pairAddr))
                    {
                        return pairAddr.GetString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPairAddress error: {ex.Message}");
            }
            return string.Empty;
        }

        // troppi fallback, da sistemare
        private async Task EnrichCandlesWithMarketData(string symbol, List<CandlestickData> candles)
        {
            if (!candles.Any()) return;

            decimal supply = 0;

            // 1. Prova CoinMarketCap
            supply = await GetSupplyFromCoinMarketCapAsync(symbol);

            // 2. Se fallisce, prova DexScreener
            if (supply == 0)
            {
                var (_, _, supplyFromDs) = await GetRealPriceAndMcFromDexScreenerAsync(symbol);
                supply = supplyFromDs;
            }

            // 3. Se fallisce, prova CoinGecko
            if (supply == 0)
            {
                supply = await GetSupplyFromCoinGeckoAsync(symbol);
            }

            // 4. Fallback approssimativa
            if (supply == 0)
                supply = GetApproximateSupply(symbol);

            foreach (var candle in candles)
            {
                candle.Supply = supply;
                candle.MarketCap = candle.Close * supply;
            }
        }

        private async Task<decimal> GetSupplyFromCoinGeckoAsync(string symbol)
        {
            try
            {
                var allCoins = await GetAllCoinsAsync();
                var coinId = FindCoinId(allCoins, symbol);
                if (string.IsNullOrEmpty(coinId)) return 0;

                var url = $"{CoinGeckoUrl}/coins/{coinId}?localization=false&tickers=false&market_data=true&community_data=false&developer_data=false";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return 0;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("market_data", out var marketData) &&
                    marketData.TryGetProperty("circulating_supply", out var circulatingSupply))
                {
                    return GetDecimalSafe(circulatingSupply);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSupplyFromCoinGecko error: {ex.Message}");
            }
            return 0;
        }

        private async Task<decimal> GetSupplyFromCoinMarketCapAsync(string symbol)
        {
            if (string.IsNullOrEmpty(_cmcApiKey)) return 0;
            try
            {
                long coinId = await GetCmcCoinIdAsync(symbol);
                if (coinId <= 0) return 0;

                var url = $"{CoinMarketCapBaseUrl}/v1/cryptocurrency/quotes/latest?id={coinId}&convert=USD";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-CMC_PRO_API_KEY", _cmcApiKey);
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return 0;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty(coinId.ToString(), out var coinData) &&
                    coinData.TryGetProperty("circulating_supply", out var supply))
                {
                    return GetDecimalSafe(supply);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSupplyFromCoinMarketCap error: {ex.Message}");
            }
            return 0;
        }

        public async Task<(decimal price, decimal marketCap, decimal supply)> GetRealPriceAndMcFromDexScreenerAsync(string symbol)
        {
            try
            {
                string url = symbol.Length >= 32
                    ? $"{DexScreenerUrl}/token/{symbol}"
                    : $"{DexScreenerUrl}/search?q={Uri.EscapeDataString(symbol)}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return (0, 0, 0);

                var json = await response.Content.ReadAsStringAsync();

                var priceRegex = new Regex("\"priceUsd\"\\s*:\\s*\"?([0-9.]+)\"?");
                var priceMatch = priceRegex.Match(json);
                var price = priceMatch.Success ? decimal.Parse(priceMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;

                var mcRegex = new Regex("\"marketCap\"\\s*:\\s*\"?([0-9.]+)\"?");
                var mcMatch = mcRegex.Match(json);
                var marketCap = mcMatch.Success ? decimal.Parse(mcMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;

                var supplyRegex = new Regex("\"circulatingSupply\"\\s*:\\s*\"?([0-9.]+)\"?");
                var supplyMatch = supplyRegex.Match(json);
                var supply = supplyMatch.Success ? decimal.Parse(supplyMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;

                if (supply == 0 && price > 0 && marketCap > 0)
                    supply = marketCap / price;
                if (marketCap == 0 && price > 0 && supply > 0)
                    marketCap = price * supply;

                return (price, marketCap, supply);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DexScreener error: {ex.Message}");
                return (0, 0, 0);
            }
        }

        private async Task<List<CoinGeckoCoin>> GetAllCoinsAsync()
        {
            if (_cachedCoins != null && _cachedCoins.Any())
                return _cachedCoins;

            try
            {
                var url = $"{CoinGeckoUrl}/coins/list";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<CoinGeckoCoin>();

                var json = await response.Content.ReadAsStringAsync();
                var coins = JsonSerializer.Deserialize<List<CoinGeckoCoin>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _cachedCoins = coins ?? new List<CoinGeckoCoin>();
                return _cachedCoins;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllCoins error: {ex.Message}");
                return new List<CoinGeckoCoin>();
            }
        }

        private string? FindCoinId(List<CoinGeckoCoin> coins, string symbol)
        {
            if (coins == null || !coins.Any()) return null;

            var trimmed = symbol.Trim();

            var bySymbol = coins.FirstOrDefault(c => c.Symbol.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (bySymbol != null) return bySymbol.Id;

            var byName = coins.FirstOrDefault(c => c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            return byName?.Id;
        }

        private class CoinGeckoCoin
        {
            public string Id { get; set; } = string.Empty;
            public string Symbol { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public ISeries[] CreateCandlestickSeries(List<CandlestickData> candles)
        {
            var pricePoints = candles.Select((c, i) => new ObservablePoint(i, (double)c.Close)).ToArray();

            return new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = pricePoints,
                    Name = string.Empty,
                    Stroke = new SolidColorPaint(SKColor.Parse("#4A90E2"), 2),
                    Fill = null,
                    GeometrySize = 0,
                    GeometryStroke = null,
                    GeometryFill = null,
                    LineSmoothness = 0.3,
                    XToolTipLabelFormatter = _ => string.Empty,
                    YToolTipLabelFormatter = point =>
                    {
                        var index = (int)point.Coordinate.SecondaryValue;
                        if (index >= 0 && index < candles.Count)
                        {
                            var c = candles[index];
                            return $"Prezzo: ${FormatPriceWithoutTrailingZeros(c.Close)}{Environment.NewLine}MC: {FormatMarketCapTooltip(c.MarketCap)}";
                        }
                        return string.Empty;
                    }
                }
            };
        }

        public Axis[] CreateXAxes(List<CandlestickData> candles, string timeframe = "24H")
        {
            string[] labels = timeframe switch
            {
                "7D" => candles.Select(c => c.OpenTime.ToString("ddd dd/MM")).ToArray(),
                "1M" => candles.Select(c => c.OpenTime.ToString("dd/MM")).ToArray(),
                _ => candles.Select(c => c.OpenTime.ToString("HH:mm")).ToArray()
            };

            return new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#888888")),
                    TextSize = 9,
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2A2A2A")),
                    LabelsRotation = 45
                }
            };
        }

        public Axis[] CreateYAxes(List<CandlestickData> candles)
        {
            if (!candles.Any())
                return Array.Empty<Axis>();

            var minPrice = candles.Min(c => c.Low);
            var maxPrice = candles.Max(c => c.High);
            var range = maxPrice - minPrice;
            var padding = range * 0.05m;

            return new Axis[]
            {
                new Axis
                {
                    MinLimit = (double)(minPrice - padding),
                    MaxLimit = (double)(maxPrice + padding),
                    Labeler = value => FormatPrice(value),
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#4A90E2")),
                    TextSize = 9,
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2A2A2A"))
                }
            };
        }

        private string FormatPriceWithoutTrailingZeros(decimal price)
        {
            return price.ToString("0.####################", System.Globalization.CultureInfo.InvariantCulture);
        }

        private string FormatMarketCapTooltip(decimal marketCap)
        {
            if (marketCap >= 1_000_000_000m) return $"${marketCap / 1_000_000_000m:N2}B";
            if (marketCap >= 1_000_000m) return $"${marketCap / 1_000_000m:N2}M";
            if (marketCap >= 1_000m) return $"${marketCap / 1_000m:N2}K";
            return $"${marketCap:N0}";
        }

        private string FormatPrice(double value)
        {
            return $"${value.ToString("0.################", System.Globalization.CultureInfo.InvariantCulture)}";
        }

        private decimal GetDecimalSafe(JsonElement element)
        {
            try
            {
                if (element.ValueKind == JsonValueKind.String)
                    return decimal.Parse(element.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                if (element.ValueKind == JsonValueKind.Number)
                    return element.GetDecimal();
            }
            catch { }
            return 0;
        }

        private decimal ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
            return 0;
        }

        private decimal GetApproximateSupply(string symbol)
        {
            return symbol.ToUpper() switch
            {
                "BTC" => 19_700_000m,
                "ETH" => 120_000_000m,
                "SOL" => 500_000_000m,
                "BNB" => 150_000_000m,
                "XRP" => 55_000_000_000m,
                "ADA" => 35_000_000_000m,
                "DOGE" => 145_000_000_000m,
                "AVAX" => 400_000_000m,
                "DOT" => 1_400_000_000m,
                "LINK" => 600_000_000m,
                _ => 1_000_000_000m
            };
        }
    }

    public class CandlestickData
    {
        public DateTime OpenTime { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public decimal MarketCap { get; set; }
        public decimal Supply { get; set; }
        public bool IsBullish => Close >= Open;
    }
}