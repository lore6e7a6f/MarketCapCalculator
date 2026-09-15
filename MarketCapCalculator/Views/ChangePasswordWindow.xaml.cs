using System;
using System.IO;
using System.Windows;
using MarketCapCalculator.Services;

namespace MarketCapCalculator.Views
{
    public partial class ChangePasswordWindow : Window
    {
        public string OldPassword { get; private set; } = string.Empty;
        public string NewPassword { get; private set; } = string.Empty;
        public bool IsConfirmed { get; private set; }

        private LocalWebServerService? _webServer;

        public ChangePasswordWindow()
        {
            InitializeComponent();
            OldPasswordInput.Focus();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = "";

            var oldPassword = OldPasswordInput.Password;
            var newPassword = NewPasswordInput.Password;
            var confirmPassword = ConfirmNewPasswordInput.Password;

            if (string.IsNullOrEmpty(oldPassword)) { ErrorText.Text = "Inserisci la vecchia password"; return; }
            if (string.IsNullOrEmpty(newPassword)) { ErrorText.Text = "Inserisci la nuova password"; return; }
            if (newPassword.Length < 8) { ErrorText.Text = "La nuova password deve essere di almeno 8 caratteri"; return; }
            if (newPassword != confirmPassword) { ErrorText.Text = "Le password non coincidono!"; ConfirmNewPasswordInput.Clear(); return; }
            if (newPassword == oldPassword) { ErrorText.Text = "La nuova password deve essere diversa dalla vecchia"; return; }

            OldPassword = oldPassword;
            NewPassword = newPassword;
            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }

        private async void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
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

                    MessageBox.Show("Password aggiornata con successo!", "Successo",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    IsConfirmed = true;
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante il recupero: {ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _webServer?.Stop();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _webServer?.Stop();
            OldPasswordInput.Clear();
            NewPasswordInput.Clear();
            ConfirmNewPasswordInput.Clear();
        }
    }
}