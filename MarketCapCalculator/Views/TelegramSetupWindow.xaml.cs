using System;
using System.Windows;
using MarketCapCalculator.Services;

namespace MarketCapCalculator.Views
{
    public partial class TelegramSetupWindow : Window
    {
        public bool IsSaved { get; private set; }
        public bool IsSkipped { get; private set; }

        public TelegramSetupWindow()
        {
            InitializeComponent();

            // Precompila se già esiste una config
            var existing = TelegramConfigStorage.Load();
            if (existing != null)
            {
                BotTokenTextBox.Text = existing.BotToken;
                ChatIdTextBox.Text = existing.ChatId;
            }
        }

        private void OpenBotFather_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://t.me/BotFather",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore apertura Telegram: {ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenUserInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://t.me/userinfobot",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore apertura Telegram: {ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var token = BotTokenTextBox.Text.Trim();
            var chatId = ChatIdTextBox.Text.Trim();

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId))
            {
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                StatusText.Text = "Inserisci sia Bot Token sia Chat ID.";
                return;
            }

            TelegramConfigStorage.Save(token, chatId);

            IsSaved = true;
            StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            StatusText.Text = "Configurazione salvata correttamente!";

            MessageBox.Show("Configurazione Telegram salvata.", "Successo",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var token = BotTokenTextBox.Text.Trim();
            var chatId = ChatIdTextBox.Text.Trim();

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId))
            {
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                StatusText.Text = "Inserisci Bot Token e Chat ID prima di testare.";
                return;
            }

            // Salva temporaneamente per permettere al servizio di leggere
            TelegramConfigStorage.Save(token, chatId);

            var bot = new TelegramBotService();
            bool ok = await bot.SendMessageAsync("✅ Test di configurazione riuscito!\nMarketCap Calculator è pronto ad inviarti i codici di recupero.");

            if (ok)
            {
                StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                StatusText.Text = "Messaggio inviato! Controlla Telegram.";
            }
            else
            {
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                StatusText.Text = "Invio fallito. Verifica Token e Chat ID.";
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            IsSkipped = true;
            DialogResult = false;
            Close();
        }
    }
}