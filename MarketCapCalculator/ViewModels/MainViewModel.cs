using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MarketCapCalculator.Models;
using MarketCapCalculator.Services;
using SkiaSharp;

namespace MarketCapCalculator.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly PredictionHistoryService _historyService;
        private readonly WalletManagerService _walletService;
        private readonly AutoRefreshService _autoRefreshService;
        private readonly ExportService _exportService;
        private readonly CandlestickService _candlestickService;

        [ObservableProperty]
        private bool autoRefreshEnabled = true;

        [ObservableProperty]
        private string lastRefreshDisplay = "Mai";

        [ObservableProperty]
        private string investmentInput = "500";

        [ObservableProperty]
        private string currentMarketCapInput = "100M";

        [ObservableProperty]
        private string targetMarketCapInput = "2B";

        [ObservableProperty]
        private string tokenPriceInput = "";

        [ObservableProperty]
        private string tokenSupplyInput = "";

        [ObservableProperty]
        private string tokenNameInput = "";

        [ObservableProperty]
        private string roiDisplay = "+0.00%";

        [ObservableProperty]
        private string profitDisplay = "+€ 0.00";

        [ObservableProperty]
        private string futureValueDisplay = "€ 0.00";

        [ObservableProperty]
        private string multiplierDisplay = "0.00x";

        [ObservableProperty]
        private string tokenPriceEstimateDisplay = "N/A";

        [ObservableProperty]
        private string tokensAcquiredDisplay = "N/A";

        [ObservableProperty]
        private string statusMessage = "Pronto";

        [ObservableProperty]
        private bool showError;

        [ObservableProperty]
        private string errorMessage = "";

        [ObservableProperty]
        private bool useWalletBalance;

        [ObservableProperty]
        private WalletAccount? selectedWallet;

        [ObservableProperty]
        private string walletBalanceDisplay = "€ 0.000000";

        [ObservableProperty]
        private string maxInvestableDisplay = "€ 0.000000";

        [ObservableProperty]
        private decimal investmentPercentage = 50;

        [ObservableProperty]
        private string calculatedInvestmentDisplay = "€ 0.00";

        [ObservableProperty]
        private string selectedSymbol = "BTC";

        [ObservableProperty]
        private string selectedTimeframe = "24H";

        [ObservableProperty]
        private decimal currentAth;

        public ObservableCollection<ScenarioRow> Scenarios { get; } = new();
        public ObservableCollection<PredictionHistory> PredictionHistory { get; } = new();
        public ObservableCollection<WalletAccount> Wallets { get; } = new();

        public ISeries[] CandlestickSeries { get; private set; } = Array.Empty<ISeries>();
        public Axis[] CandlestickXAxes { get; private set; } = Array.Empty<Axis>();
        public Axis[] CandlestickYAxes { get; private set; } = Array.Empty<Axis>();

        public ISeries[] PredictionSeries { get; private set; } = Array.Empty<ISeries>();
        public Axis[] PredictionXAxes { get; private set; } = Array.Empty<Axis>();
        public Axis[] PredictionYAxes { get; private set; } = Array.Empty<Axis>();

        public IRelayCommand CalculateCommand { get; }
        public IRelayCommand ClearCommand { get; }
        public IRelayCommand OpenAxiomCommand { get; }
        public IRelayCommand OpenFomoCommand { get; }
        public IRelayCommand ClearHistoryCommand { get; }
        public IRelayCommand ExportHistoryCsvCommand { get; }
        public IRelayCommand ExportWalletsCsvCommand { get; }
        public IRelayCommand ExportHistoryPdfCommand { get; }
        public IRelayCommand ExportWalletsPdfCommand { get; }
        public IRelayCommand SetTimeframeCommand { get; }

        public MainViewModel()
        {
            _historyService = new PredictionHistoryService();
            _walletService = new WalletManagerService();
            _autoRefreshService = new AutoRefreshService(30);
            _exportService = new ExportService();
            _candlestickService = new CandlestickService();

            CalculateCommand = new RelayCommand(ExecuteCalculation);
            ClearCommand = new RelayCommand(ExecuteClear);
            OpenAxiomCommand = new RelayCommand(OpenAxiom);
            OpenFomoCommand = new RelayCommand(OpenFomo);
            ClearHistoryCommand = new RelayCommand(ClearHistory);
            ExportHistoryCsvCommand = new RelayCommand(ExportHistoryCsv);
            ExportWalletsCsvCommand = new RelayCommand(ExportWalletsCsv);
            ExportHistoryPdfCommand = new RelayCommand(ExportHistoryPdf);
            ExportWalletsPdfCommand = new RelayCommand(ExportWalletsPdf);
            SetTimeframeCommand = new RelayCommand<string>(timeframe => SelectedTimeframe = timeframe);

            LoadPredictionHistory();
            LoadWallets();

            _autoRefreshService.WalletsUpdated += OnWalletsUpdated;
            _autoRefreshService.Start();
        }

        private void OnWalletsUpdated(object? sender, EventArgs e)
        {
            RefreshWallets();
            LastRefreshDisplay = DateTime.Now.ToString("HH:mm:ss");
        }

        partial void OnAutoRefreshEnabledChanged(bool value)
        {
            if (value)
                _autoRefreshService.Start();
            else
                _autoRefreshService.Stop();
        }

        public void RefreshWallets()
        {
            Wallets.Clear();
            var wallets = _walletService.GetWallets();
            foreach (var wallet in wallets)
                Wallets.Add(wallet);

            if (SelectedWallet != null)
            {
                var stillExists = Wallets.FirstOrDefault(w => w.Id == SelectedWallet.Id);
                if (stillExists != null)
                    SelectedWallet = stillExists;
            }
        }

        private void LoadWallets()
        {
            Wallets.Clear();
            var wallets = _walletService.GetWallets();
            foreach (var wallet in wallets)
                Wallets.Add(wallet);
        }

        partial void OnSelectedWalletChanged(WalletAccount? value)
        {
            if (value != null)
            {
                WalletBalanceDisplay = value.DisplayEurBalance;
                MaxInvestableDisplay = value.DisplayEurBalance;
            }
            else
            {
                WalletBalanceDisplay = "€ 0.000000";
                MaxInvestableDisplay = "€ 0.000000";
            }
            UpdateCalculatedInvestment();
        }

        partial void OnInvestmentPercentageChanged(decimal value)
        {
            UpdateCalculatedInvestment();
        }

        private void UpdateCalculatedInvestment()
        {
            if (SelectedWallet?.EurBalance.HasValue == true)
            {
                var amount = SelectedWallet.EurBalance.Value * (InvestmentPercentage / 100m);
                CalculatedInvestmentDisplay = $"€ {amount:F6}";
                InvestmentInput = amount.ToString("F6", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
            }
            else
            {
                CalculatedInvestmentDisplay = "€ 0.000000";
            }
        }

        partial void OnSelectedTimeframeChanged(string value)
        {
            if (!string.IsNullOrEmpty(SelectedSymbol))
                _ = LoadCandlesticksAsync(SelectedSymbol);
        }

        public async Task LoadCandlesticksAsync(string symbol)
        {
            try
            {
                SelectedSymbol = symbol.ToUpper();
                StatusMessage = $"Caricamento {symbol.ToUpper()}...";
                System.Diagnostics.Debug.WriteLine($"LoadCandlesticksAsync: {symbol}, timeframe={SelectedTimeframe}");

                CurrentAth = await _candlestickService.GetAthAsync(symbol);
                var candles = await _candlestickService.GetCandlesticksAsync(symbol, SelectedTimeframe);

                if (candles.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"Candles: {candles.Count}");
                    CandlestickSeries = _candlestickService.CreateCandlestickSeries(candles);
                    CandlestickXAxes = _candlestickService.CreateXAxes(candles, SelectedTimeframe);
                    CandlestickYAxes = _candlestickService.CreateYAxes(candles);

                    OnPropertyChanged(nameof(CandlestickSeries));
                    OnPropertyChanged(nameof(CandlestickXAxes));
                    OnPropertyChanged(nameof(CandlestickYAxes));

                    StatusMessage = $"Grafico {symbol.ToUpper()} pronto";
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Nessun dato per {symbol}");
                    CandlestickSeries = Array.Empty<ISeries>();
                    CandlestickXAxes = Array.Empty<Axis>();
                    CandlestickYAxes = Array.Empty<Axis>();

                    OnPropertyChanged(nameof(CandlestickSeries));
                    OnPropertyChanged(nameof(CandlestickXAxes));
                    OnPropertyChanged(nameof(CandlestickYAxes));

                    StatusMessage = $"⚠️ Nessun dato disponibile per {symbol.ToUpper()}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load error: {ex.Message}");
                StatusMessage = $"Errore: {ex.Message}";
            }
        }

        private void ExecuteCalculation()
        {
            try
            {
                ShowError = false;
                StatusMessage = "Calcolo in corso...";

                var investment = ParseDecimal(InvestmentInput, "Investimento");

                if (UseWalletBalance)
                {
                    if (SelectedWallet == null)
                        throw new ArgumentException("Seleziona un wallet prima di investire");

                    if (!SelectedWallet.EurBalance.HasValue)
                        throw new ArgumentException("Saldo wallet non disponibile. Aggiorna i saldi.");

                    var saldoDisponibile = SelectedWallet.EurBalance.Value;
                    var tolleranza = 0.000001m;

                    if (investment > saldoDisponibile + tolleranza)
                        throw new ArgumentException($"Investimento (€ {investment:F6}) supera il saldo disponibile (€ {saldoDisponibile:F6})");
                }

                var currentMc = MarketCapParser.Parse(CurrentMarketCapInput);
                var targetMc = MarketCapParser.Parse(TargetMarketCapInput);

                decimal? tokenPrice = null;
                decimal? tokenSupply = null;

                if (!string.IsNullOrWhiteSpace(TokenPriceInput))
                    tokenPrice = ParseDecimal(TokenPriceInput, "Prezzo token");

                if (!string.IsNullOrWhiteSpace(TokenSupplyInput))
                    tokenSupply = MarketCapParser.Parse(TokenSupplyInput);

                var result = CalculationService.CalculateInvestment(investment, currentMc, targetMc, tokenPrice, tokenSupply);

                UpdateDisplay(result);

                var scenarios = CalculationService.GenerateScenarios(investment, currentMc, targetMc);
                Scenarios.Clear();
                foreach (var scenario in scenarios)
                    Scenarios.Add(scenario);

                UpdatePredictionChart(investment, currentMc, targetMc);

                _historyService.AddPrediction(result, TokenNameInput);
                LoadPredictionHistory();

                StatusMessage = $"Calcolo completato - {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                ShowError = true;
                ErrorMessage = ex.Message;
                StatusMessage = "Errore nel calcolo";
            }
        }

        private void UpdateDisplay(InvestmentCalculation result)
        {
            var sign = result.NetProfit >= 0 ? "+" : "";
            RoiDisplay = $"{sign}{result.RoiPercentage:N1}%";
            ProfitDisplay = $"{sign}€ {result.NetProfit:N2}";
            FutureValueDisplay = $"€ {result.FutureValue:N2}";
            MultiplierDisplay = $"{result.GrowthMultiplier:N2}x";

            TokenPriceEstimateDisplay = result.EstimatedTokenPrice.HasValue
                ? $"€ {result.EstimatedTokenPrice.Value:N6}"
                : "N/A";

            TokensAcquiredDisplay = result.TokensAcquired.HasValue
                ? result.TokensAcquired.Value.ToString("N0")
                : "N/A";
        }

        private void UpdatePredictionChart(decimal investment, decimal currentMc, decimal targetMc)
        {
            var (marketCaps, values, labels) = CalculationService.GenerateChartData(investment, currentMc, targetMc);

            var valuePoints = values.Select((v, i) => new ObservablePoint(i, (double)v)).ToArray();

            PredictionSeries = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = valuePoints,
                    Name = "Valore Investimento",
                    Stroke = new SolidColorPaint(SKColor.Parse("#00C853"), 2),
                    Fill = new SolidColorPaint(SKColor.Parse("#1A00C853")),
                    GeometrySize = 0,
                    LineSmoothness = 0.5
                }
            };

            PredictionXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#888888")),
                    TextSize = 9,
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2A2A2A"))
                }
            };

            PredictionYAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value => $"€ {value:N0}",
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#888888")),
                    TextSize = 9,
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2A2A2A"))
                }
            };

            OnPropertyChanged(nameof(PredictionSeries));
            OnPropertyChanged(nameof(PredictionXAxes));
            OnPropertyChanged(nameof(PredictionYAxes));
        }

        private void OpenAxiom() => OpenInChrome("https://axiom.trade");
        private void OpenFomo() => OpenInChrome("https://fomo.family");

        private void OpenInChrome(string url)
        {
            try
            {
                var chromePath = FindChromePath();

                if (!string.IsNullOrEmpty(chromePath))
                {
                    var userDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    userDataDir = System.IO.Path.Combine(userDataDir, "Google", "Chrome", "User Data");

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = chromePath,
                        Arguments = $"--user-data-dir=\"{userDataDir}\" {url}",
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }

                StatusMessage = $"Apertura {url} in Chrome...";
            }
            catch (Exception ex)
            {
                ShowError = true;
                ErrorMessage = $"Errore: {ex.Message}";
                StatusMessage = "Errore apertura browser";
            }
        }

        private string FindChromePath()
        {
            var paths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Google\Chrome\Application\chrome.exe"
            };

            foreach (var path in paths)
                if (System.IO.File.Exists(path))
                    return path;

            return string.Empty;
        }

        private void LoadPredictionHistory()
        {
            PredictionHistory.Clear();
            var predictions = _historyService.GetPredictions();
            foreach (var prediction in predictions)
                PredictionHistory.Add(prediction);
        }

        private void ClearHistory()
        {
            var result = MessageBox.Show("Cancellare tutta la storia?", "Conferma",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _historyService.ClearHistory();
                LoadPredictionHistory();
                StatusMessage = "Storia cancellata";
            }
        }

        private void ExecuteClear()
        {
            InvestmentInput = "";
            CurrentMarketCapInput = "";
            TargetMarketCapInput = "";
            TokenPriceInput = "";
            TokenSupplyInput = "";
            TokenNameInput = "";

            RoiDisplay = "+0.00%";
            ProfitDisplay = "+€ 0.00";
            FutureValueDisplay = "€ 0.00";
            MultiplierDisplay = "0.00x";
            TokenPriceEstimateDisplay = "N/A";
            TokensAcquiredDisplay = "N/A";

            Scenarios.Clear();
            ShowError = false;
            StatusMessage = "Campi puliti";
        }

        private decimal ParseDecimal(string input, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException($"{fieldName} non può essere vuoto");

            var normalizedInput = NormalizeNumber(input.Trim());

            if (!decimal.TryParse(normalizedInput, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
                throw new ArgumentException($"{fieldName} non è un numero valido");

            if (result <= 0)
                throw new ArgumentException($"{fieldName} deve essere positivo");

            return result;
        }

        private string NormalizeNumber(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "0";
            input = input.Trim();

            if (input.Contains(".") && input.Contains(","))
                return input.Replace(".", "").Replace(",", ".");

            if (input.Contains(","))
            {
                var parts = input.Split(',');
                if (parts.Length > 2)
                    return parts[0] + "." + string.Join("", parts.Skip(1));
                return input.Replace(",", ".");
            }

            if (input.Contains("."))
            {
                var parts = input.Split('.');
                if (parts.Length > 2)
                {
                    var lastPart = parts[^1];
                    var intPart = string.Join("", parts[..^1]);
                    return intPart + "." + lastPart;
                }
                return input;
            }

            return input;
        }

        private void ExportHistoryCsv()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"storia_predizioni_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                _exportService.ExportHistoryToCsv(PredictionHistory, dialog.FileName);
                StatusMessage = "CSV esportato";
            }
        }

        private void ExportWalletsCsv()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"wallets_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                _exportService.ExportWalletsToCsv(Wallets, dialog.FileName);
                StatusMessage = "CSV esportato";
            }
        }

        private void ExportHistoryPdf()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"storia_predizioni_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                _exportService.ExportHistoryToPdf(PredictionHistory, dialog.FileName);
                StatusMessage = "PDF esportato";
            }
        }

        private void ExportWalletsPdf()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"wallets_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                _exportService.ExportWalletsToPdf(Wallets, dialog.FileName);
                StatusMessage = "PDF esportato";
            }
        }
    }
}