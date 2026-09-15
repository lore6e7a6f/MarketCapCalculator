using System;
using System.Windows;
using System.Windows.Controls;
using MarketCapCalculator.Services;

namespace MarketCapCalculator.Views
{
    /// <summary>
    /// Finestra per la gestione dei wallet importati
    /// </summary>
    public partial class WalletManagerWindow : Window
    {
        private readonly WalletManagerService _walletService;

        // Evento per notificare che un wallet è stato aggiunto
        public event EventHandler? WalletAdded;
        public event EventHandler? WalletDeleted;


        public WalletManagerWindow()
        {
            InitializeComponent();
            _walletService = new WalletManagerService();
            LoadWallets();
            RefreshBalancesOnLoad();
        }

        private void LoadWallets()
        {
            WalletListBox.ItemsSource = null;
            WalletListBox.ItemsSource = _walletService.GetWallets();
        }

        private async void RefreshBalancesOnLoad()
        {
            try
            {
                await _walletService.UpdateAllBalancesAsync();
                LoadWallets();
            }
            catch (Exception)
            {
                // Ignora errori iniziali
            }
        }

        private async void ImportWallet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = WalletNameTextBox.Text.Trim();
                var address = WalletAddressTextBox.Text.Trim();
                
                if (string.IsNullOrEmpty(address))
                {
                    MessageBox.Show("Inserisci un indirizzo wallet.", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(name))
                {
                    name = $"Wallet {DateTime.Now:HHmmss}";
                }

                ImportButton.IsEnabled = false;
                ImportButton.Content = "CARICAMENTO...";

                var wallet = await _walletService.ImportWalletAsync(address, name);
                
                LoadWallets();

                // Notifica che un wallet è stato aggiunto
                WalletAdded?.Invoke(this, EventArgs.Empty);

                WalletNameTextBox.Clear();
                WalletAddressTextBox.Clear();
                
                var solBalanceStr = wallet.SolBalance.HasValue ? 
                    wallet.SolBalance.Value.ToString("F9") : "N/D";
                var eurBalanceStr = wallet.EurBalance.HasValue ? 
                    wallet.EurBalance.Value.ToString("F9") : "0.000000000";
                
                MessageBox.Show($"Wallet aggiunto con successo!\n\n" +
                            $"Nome: {wallet.DisplayName}\n" +
                            $"Indirizzo: {wallet.DisplayAddress}\n" +
                            $"Saldo SOL: {solBalanceStr} SOL\n" +
                            $"Valore EUR: € {eurBalanceStr}",
                            "Wallet Aggiunto",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore nell'aggiunta: {ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ImportButton.IsEnabled = true;
                ImportButton.Content = "AGGIUNGI";
            }
        }

        private async void RefreshBalances_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshButton.IsEnabled = false;
                RefreshButton.Content = "AGGIORNAMENTO...";
                
                await _walletService.UpdateAllBalancesAsync();
                LoadWallets();
                
                MessageBox.Show("Saldi aggiornati!", "Successo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore nell'aggiornamento: {ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshButton.IsEnabled = true;
                RefreshButton.Content = "⟳ AGGIORNA SALDI";
            }
        }

        private void DeleteWallet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string walletId)
            {
                var result = MessageBox.Show("Eliminare questo wallet?", "Conferma",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _walletService.DeleteWallet(walletId);
                    LoadWallets();

                    // Notifica che un wallet è stato eliminato
                    WalletDeleted?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
}