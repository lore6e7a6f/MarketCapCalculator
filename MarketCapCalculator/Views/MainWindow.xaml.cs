using System;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarketCapCalculator.Models;
using MarketCapCalculator.Services;
using MarketCapCalculator.ViewModels;

namespace MarketCapCalculator.Views
{
    public partial class MainWindow : Window
    {
        private readonly TokenSearchService _tokenService;
        private WalletManagerWindow? _walletManagerWindow;

        public MainWindow()
        {
            InitializeComponent();
            _tokenService = new TokenSearchService();
        }

        private void OpenGitHubProfile(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/lore6e7a6f",
                UseShellExecute = true
            });
        }

        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SearchTokensAsync();
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await SearchTokensAsync();
        }

        private async System.Threading.Tasks.Task SearchTokensAsync()
        {
            var query = SearchTextBox.Text.Trim();

            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show("Inserisci un nome o simbolo da cercare.", "Ricerca",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                SearchResultsPanel.Visibility = Visibility.Visible;
                SearchResultsTitle.Text = $"CERCO: {query.ToUpper()}...";

                var tokens = await _tokenService.SearchTokensAsync(query);

                SearchResultsList.ItemsSource = tokens;
                SearchResultsTitle.Text = $"RISULTATI PER: {query.ToUpper()} ({tokens.Count})";

                if (!tokens.Any())
                {
                    SearchResultsTitle.Text = $"NESSUN RISULTATO PER: {query.ToUpper()}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore nella ricerca: {ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SearchResultsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CloseSearchResults_Click(object sender, RoutedEventArgs e)
        {
            SearchResultsPanel.Visibility = Visibility.Collapsed;
        }

        private void TokenResult_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is TokenInfo token)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.TokenNameInput = token.Name;
                    if (token.MarketCap > 0)
                        vm.CurrentMarketCapInput = FormatMarketCapForInput(token.MarketCap);
                    if (token.CurrentPrice > 0)
                        vm.TokenPriceInput = token.PriceForInputWithComma;
                    if (token.CirculatingSupply > 0)
                        vm.TokenSupplyInput = token.CirculatingSupply.ToString("F0");
                    if (token.MarketCap > 0)
                    {
                        var targetMc = token.MarketCap * 2m;
                        vm.TargetMarketCapInput = FormatMarketCapForInput(targetMc);
                    }

                    // Usa il simbolo se disponibile, altrimenti il nome, altrimenti l'id
                    string symbolForChart = !string.IsNullOrEmpty(token.Symbol) ? token.Symbol :
                                            !string.IsNullOrEmpty(token.Name) ? token.Name :
                                            token.Id;

                    System.Diagnostics.Debug.WriteLine($"Chart symbol: {symbolForChart}");

                    _ = vm.LoadCandlesticksAsync(symbolForChart);

                    vm.StatusMessage = $"Token: {token.Name} | Grafico caricato";
                }

                SearchResultsPanel.Visibility = Visibility.Collapsed;
                SearchTextBox.Clear();
            }
        }

        private string FormatMarketCapForInput(decimal marketCap)
        {
            if (marketCap >= 1_000_000_000m)
            {
                var value = marketCap / 1_000_000_000m;
                return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "B";
            }
            else if (marketCap >= 1_000_000m)
            {
                var value = marketCap / 1_000_000m;
                return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "M";
            }
            else if (marketCap >= 1_000m)
            {
                var value = marketCap / 1_000m;
                return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "K";
            }
            else
            {
                return marketCap.ToString("F0");
            }
        }

        private void OpenWalletManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_walletManagerWindow == null || !_walletManagerWindow.IsVisible)
                {
                    _walletManagerWindow = new WalletManagerWindow();

                    // Sottoscrivi agli eventi
                    _walletManagerWindow.WalletAdded += (s, args) =>
                    {
                        if (DataContext is MainViewModel vm)
                        {
                            vm.RefreshWallets();
                        }
                    };

                    _walletManagerWindow.WalletDeleted += (s, args) =>
                    {
                        if (DataContext is MainViewModel vm)
                        {
                            vm.RefreshWallets();
                        }
                    };

                    _walletManagerWindow.Closed += (s, args) =>
                    {
                        _walletManagerWindow = null;

                        // Aggiorna i wallet alla chiusura
                        if (DataContext is MainViewModel vm)
                        {
                            vm.RefreshWallets();
                        }
                    };

                    _walletManagerWindow.Show();
                }
                else
                {
                    if (_walletManagerWindow.WindowState == WindowState.Minimized)
                    {
                        _walletManagerWindow.WindowState = WindowState.Normal;
                    }
                    _walletManagerWindow.Activate();
                    _walletManagerWindow.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore: {ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UseMaxButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.SelectedWallet != null)
            {
                if (vm.SelectedWallet.EurBalance.HasValue)
                {
                    // Imposta lo slider al 100%
                    vm.InvestmentPercentage = 100;

                    // Imposta l'investimento al massimo
                    vm.InvestmentInput = vm.SelectedWallet.EurBalance.Value.ToString("F6",
                        System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
                }
            }
        }

        private async void TimeframeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string timeframe)
            {
                // Reset tutti i bottoni timeframe
                var timeframeButtons = new[] { Btn1H, Btn4H, Btn24H, Btn7D, Btn1M };

                foreach (var btn in timeframeButtons)
                {
                    if (btn != null)
                    {
                        btn.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(51, 51, 51));
                    }
                }

                // Evidenzia il bottone selezionato
                button.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(74, 144, 226));

                // Aggiorna il ViewModel
                if (DataContext is MainViewModel vm)
                {
                    vm.SelectedTimeframe = timeframe;
                    await vm.LoadCandlesticksAsync(vm.SelectedSymbol);
                }
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Vuoi effettuare il logout?", "Logout",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            this.Hide();

            var secureStorage = new SecureStorageService();
            int attempts = 0;
            const int maxAttempts = 5;

            while (attempts < maxAttempts)
            {
                attempts++;
                var passwordWindow = new PasswordWindow(isCreateMode: false);
                passwordWindow.Owner = this;

                if (attempts > 1)
                    passwordWindow.ShowError("Password non valida!", maxAttempts - attempts + 1);

                passwordWindow.ShowDialog();

                if (passwordWindow.DialogResult == true)
                {
                    if (secureStorage.VerifyPassword(passwordWindow.Password))
                    {
                        passwordWindow.ClearPassword();
                        this.Show();
                        this.Activate();
                        return;
                    }
                    else
                    {
                        passwordWindow.ClearPassword();
                        if (attempts >= maxAttempts)
                        {
                            MessageBox.Show("Troppi tentativi falliti! L'applicazione verrà chiusa.",
                                "Sicurezza", MessageBoxButton.OK, MessageBoxImage.Error);
                            Application.Current.Shutdown();
                            return;
                        }
                    }
                }
                else
                {
                    passwordWindow.ClearPassword();
                    this.Show();
                    this.Activate();
                    return;
                }
            }
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var changeWindow = new ChangePasswordWindow();
            changeWindow.Owner = this;

            if (changeWindow.ShowDialog() == true)
            {
                var secureStorage = new SecureStorageService();
                if (!secureStorage.VerifyPassword(changeWindow.OldPassword))
                {
                    MessageBox.Show("La vecchia password non è corretta!", "Errore",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                secureStorage.SavePasswordHash(changeWindow.NewPassword);
                MessageBox.Show("Password cambiata con successo!", "Successo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}