using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MarketCapCalculator.Services;

namespace MarketCapCalculator.Views
{
    public partial class PasswordWindow : Window
    {
        private string _password = string.Empty;
        private readonly bool _isCreateMode;
        private LocalWebServerService? _webServer;

        public string Password => _password;
        public bool IsConfirmed { get; private set; }

        public PasswordWindow(bool isCreateMode = false)
        {
            InitializeComponent();
            _isCreateMode = isCreateMode;

            if (_isCreateMode)
            {
                TitleText.Text = "CREA PASSWORD";
                SubtitleText.Text = "Scegli una password per proteggere i tuoi dati";
                ConfirmButton.Content = "CREA";

                // In modalità creazione NON mostro il recupero password
                ForgotPasswordButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                TitleText.Text = "PASSWORD";
                SubtitleText.Text = "Inserisci la tua password";
                ConfirmButton.Content = "SBLOCCA";

                // In modalità login mostriamo il recupero password
                ForgotPasswordButton.Visibility = Visibility.Visible;
            }

            PasswordInput.Focus();
            Closing += PasswordWindow_Closing;
        }

        private void PasswordWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _webServer?.Stop();
        }

        public void ShowError(string message, int attemptsLeft = -1)
        {
            ErrorText.Text = message;

            if (attemptsLeft >= 0)
            {
                AttemptsText.Text = $"Tentativi rimasti: {attemptsLeft}";
                AttemptsText.Visibility = Visibility.Visible;
            }
            else
            {
                AttemptsText.Visibility = Visibility.Collapsed;
            }

            ErrorBorder.Visibility = Visibility.Visible;
            PasswordInput.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 82, 82));
            PasswordInput.Clear();
            PasswordInput.Focus();
        }

        public void ClearPassword()
        {
            _password = string.Empty;
            PasswordInput.Clear();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var input = PasswordInput.Password;

            if (string.IsNullOrEmpty(input))
            {
                ShowError(_isCreateMode ? "Scegli una password" : "Inserisci la password");
                return;
            }

            if (_isCreateMode && input.Length < 8)
            {
                ShowError("La password deve essere di almeno 8 caratteri");
                return;
            }

            _password = input;
            IsConfirmed = true;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _webServer?.Stop();
            IsConfirmed = false;
            DialogResult = false;
        }

        private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmButton_Click(sender, e);
            }
        }

        private async void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            // Sicurezza: impedisci il recupero in modalità creazione
            if (_isCreateMode) return;

            try
            {
                var recoveryCode = new Random().Next(100000, 999999).ToString();

                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var appFolder = Path.Combine(appDataPath, "MarketCapCalculator");
                Directory.CreateDirectory(appFolder);
                File.WriteAllText(Path.Combine(appFolder, "recovery_code.txt"), recoveryCode);

                _webServer = new LocalWebServerService();
                var newPassword = await _webServer.StartRecoveryServerAsync(recoveryCode);

                if (!string.IsNullOrEmpty(newPassword))
                {
                    var secureStorage = new SecureStorageService();
                    secureStorage.SavePasswordHash(newPassword);

                    PasswordInput.Clear();
                    PasswordInput.Focus();
                    ErrorBorder.Visibility = Visibility.Collapsed;
                    ErrorText.Text = "";
                }

                _webServer.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForgotPassword error: {ex.Message}");
                _webServer?.Stop();
            }
        }
    }
}