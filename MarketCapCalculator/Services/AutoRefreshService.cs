using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MarketCapCalculator.Services
{
    
    // Servizio per l'auto-refresh dei prezzi e dei wallet
    public class AutoRefreshService
    {
        private readonly DispatcherTimer _timer;
        private readonly TokenSearchService _tokenService;
        private readonly WalletManagerService _walletService;

        public event EventHandler? WalletsUpdated;
        public event EventHandler? PricesUpdated;

        public bool IsRunning => _timer.IsEnabled;
        public int IntervalSeconds { get; private set; }

        public AutoRefreshService(int intervalSeconds = 30)
        {
            IntervalSeconds = intervalSeconds;
            _tokenService = new TokenSearchService();
            _walletService = new WalletManagerService();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(intervalSeconds);
            _timer.Tick += async (s, e) => await RefreshAsync();
        }

        public void Start()
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
                System.Diagnostics.Debug.WriteLine($"Auto-refresh started (every {IntervalSeconds}s)");
            }
        }

        public void Stop()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                System.Diagnostics.Debug.WriteLine("Auto-refresh stopped");
            }
        }

        public void SetInterval(int seconds)
        {
            IntervalSeconds = seconds;
            _timer.Interval = TimeSpan.FromSeconds(seconds);
        }

        private async Task RefreshAsync()
        {
            try
            {
                // Aggiorna i wallet
                await _walletService.UpdateAllBalancesAsync();
                WalletsUpdated?.Invoke(this, EventArgs.Empty);

                // Notifica aggiornamento prezzi
                PricesUpdated?.Invoke(this, EventArgs.Empty);

                System.Diagnostics.Debug.WriteLine($"Auto-refresh completato: {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-refresh error: {ex.Message}");
            }
        }
    }
}