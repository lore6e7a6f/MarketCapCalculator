using System;
using System.Windows;
using System.Globalization;
using MarketCapCalculator.Services;
using MarketCapCalculator.Views;

namespace MarketCapCalculator
{
    public partial class App : Application
    {
        private EncryptionService? _encryptionService;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            var secureStorage = new SecureStorageService();

            if (secureStorage.HasEncryptedData())
            {
                if (!TryLoginWithRetry(secureStorage))
                {
                    Shutdown();
                    return;
                }
            }
            else
            {
                if (!TryCreatePassword(secureStorage))
                {
                    Shutdown();
                    return;
                }
            }

            OpenMainWindow();
            CheckFirstRunSetup();
        }

        private bool TryLoginWithRetry(SecureStorageService secureStorage)
        {
            int attempts = 0;
            const int maxAttempts = 5;

            while (attempts < maxAttempts)
            {
                attempts++;

                var passwordWindow = new PasswordWindow(isCreateMode: false);

                if (attempts > 1)
                {
                    passwordWindow.ShowError("Password non valida!", maxAttempts - attempts + 1);
                }

                passwordWindow.ShowDialog();

                if (passwordWindow.DialogResult == true)
                {
                    var password = passwordWindow.Password;

                    if (secureStorage.VerifyPassword(password))
                    {
                        _encryptionService = new EncryptionService(password);
                        passwordWindow.ClearPassword();
                        return true;
                    }
                    else
                    {
                        passwordWindow.ClearPassword();

                        if (attempts >= maxAttempts)
                        {
                            MessageBox.Show("Troppi tentativi falliti. L'applicazione verrà chiusa.",
                                "Sicurezza", MessageBoxButton.OK, MessageBoxImage.Error);
                            return false;
                        }
                    }
                }
                else
                {
                    passwordWindow.ClearPassword();
                    return false;
                }
            }

            return false;
        }

        private bool TryCreatePassword(SecureStorageService secureStorage)
        {
            var passwordWindow = new PasswordWindow(isCreateMode: true);
            passwordWindow.ShowDialog();

            if (passwordWindow.DialogResult == true)
            {
                var password = passwordWindow.Password;
                if (!string.IsNullOrEmpty(password))
                {
                    _encryptionService = new EncryptionService(password);
                    secureStorage.InitializeStorage(_encryptionService, password);
                    passwordWindow.ClearPassword();
                    return true;
                }
            }

            return false;
        }

        private void OpenMainWindow()
        {
            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
            _mainWindow.Show();
        }

        
        /// Al primo avvio mostra la configurazione Telegram e la API key CoinMarketCap.
        private void CheckFirstRunSetup()
        {
            // 1) Telegram
            if (!TelegramConfigStorage.IsConfigured())
            {
                var telegramWindow = new TelegramSetupWindow { Owner = _mainWindow };
                telegramWindow.ShowDialog();
            }

            // 2) CoinMarketCap API key (se non già impostata)
            var secureStorage = new SecureStorageService();
            if (!ApiKeyStorage.HasApiKey() && !secureStorage.HasApiKeyPrompted())
            {
                secureStorage.SetApiKeyPrompted();
                var apiKeyWindow = new ApiKeyWindow { Owner = _mainWindow };
                apiKeyWindow.ShowDialog();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _encryptionService = null;
            _mainWindow = null;
            base.OnExit(e);
        }

        public EncryptionService? GetEncryptionService()
        {
            return _encryptionService;
        }
    }
}