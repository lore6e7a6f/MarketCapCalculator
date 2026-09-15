using System;
using System.Windows;
using MarketCapCalculator.Services;

namespace MarketCapCalculator.Views
{
    public partial class ApiKeyWindow : Window
    {
        public bool IsSaved { get; private set; }
        public bool IsSkipped { get; private set; }

        public ApiKeyWindow()
        {
            InitializeComponent();
        }

        private void OpenInfoUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://coinmarketcap.com/api/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore nell'apertura del browser: {ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var apiKey = ApiKeyTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ErrorText.Text = "Inserisci una API key valida.";
                return;
            }

            ApiKeyStorage.SaveApiKey(apiKey);
            IsSaved = true;
            DialogResult = true;
            Close();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            IsSkipped = true;
            DialogResult = true;
            Close();
        }
    }
}