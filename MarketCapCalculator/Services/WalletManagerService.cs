using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MarketCapCalculator.Models;

namespace MarketCapCalculator.Services
{
    
    // Servizio per la gestione dei wallet con saldi reali
    
    public class WalletManagerService
    {
        private readonly HttpClient _httpClient;
        private readonly string _walletFilePath;
        private List<WalletAccount> _wallets;
        
        private const string SolanaRpcUrl = "https://api.mainnet-beta.solana.com";
        private const string CoinGeckoUrl = "https://api.coingecko.com/api/v3";

        public WalletManagerService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MarketCapCalculator");
            Directory.CreateDirectory(appFolder);
            _walletFilePath = Path.Combine(appFolder, "wallets.json");
            
            _wallets = LoadWallets();
        }

        // Importa un wallet con nome personalizzato
        
        public async Task<WalletAccount> ImportWalletAsync(string address, string name = "")
        {
            if (!IsValidSolanaAddress(address))
                throw new ArgumentException("Indirizzo Solana non valido");

            var existingWallet = _wallets.FirstOrDefault(w => w.Address == address);
            if (existingWallet != null)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    existingWallet.Name = name;
                    SaveWallets();
                }
                return existingWallet;
            }

            var wallet = new WalletAccount
            {
                Name = string.IsNullOrEmpty(name) ? $"Wallet {_wallets.Count + 1}" : name,
                Address = address,
                IsActive = true
            };

            _wallets.Add(wallet);
            SaveWallets();
            
            await UpdateWalletBalanceAsync(wallet);
            
            return wallet;
        }

        // Aggiorna il saldo di un singolo wallet
        
        public async Task UpdateWalletBalanceAsync(WalletAccount wallet)
        {
            var solBalance = await GetSolBalance(wallet.Address);
            var solPrice = await GetSolPriceInEUR();
            
            wallet.SolBalance = solBalance;
            
            if (solBalance.HasValue && solPrice.HasValue)
            {
                wallet.EurBalance = solBalance.Value * solPrice.Value;
                
                // Debug
                System.Diagnostics.Debug.WriteLine($"SOL Balance: {solBalance.Value}");
                System.Diagnostics.Debug.WriteLine($"SOL Price: {solPrice.Value}");
                System.Diagnostics.Debug.WriteLine($"EUR Balance: {wallet.EurBalance.Value}");
            }
            else
            {
                wallet.EurBalance = 0;
                
                // Debug
                System.Diagnostics.Debug.WriteLine($"SOL Balance: {(solBalance.HasValue ? solBalance.Value.ToString() : "null")}");
                System.Diagnostics.Debug.WriteLine($"SOL Price: {(solPrice.HasValue ? solPrice.Value.ToString() : "null")}");
            }
            
            wallet.LastUpdated = DateTime.Now;
            SaveWallets();
        }

        // Aggiorna i saldi di tutti i wallet
        
        public async Task UpdateAllBalancesAsync()
        {
            var solPrice = await GetSolPriceInEUR();
            
            System.Diagnostics.Debug.WriteLine($"SOL Price EUR: {(solPrice.HasValue ? solPrice.Value.ToString() : "null")}");
            
            foreach (var wallet in _wallets)
            {
                var solBalance = await GetSolBalance(wallet.Address);
                wallet.SolBalance = solBalance;
                
                if (solBalance.HasValue && solPrice.HasValue)
                {
                    wallet.EurBalance = solBalance.Value * solPrice.Value;
                }
                else
                {
                    wallet.EurBalance = 0;
                }
                
                wallet.LastUpdated = DateTime.Now;
            }
            
            SaveWallets();
        }

        // Ottiene tutti i wallet salvati
        
        public IEnumerable<WalletAccount> GetWallets()
        {
            return _wallets.OrderByDescending(w => w.DateAdded);
        }

        // Elimina un wallet
        
        public void DeleteWallet(string walletId)
        {
            var wallet = _wallets.FirstOrDefault(w => w.Id == walletId);
            if (wallet != null)
            {
                _wallets.Remove(wallet);
                SaveWallets();
            }
        }

        // Ottiene il saldo SOL reale
        
        public async Task<decimal?> GetSolBalance(string address)
        {
            try
            {
                var requestBody = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "getBalance",
                    @params = new[] { address }
                };

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(SolanaRpcUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("result", out var result) && 
                        result.TryGetProperty("value", out var value))
                    {
                        var lamports = value.GetDecimal();
                        var solBalance = lamports / 1_000_000_000m;
                        
                        System.Diagnostics.Debug.WriteLine($"Lamports: {lamports}");
                        System.Diagnostics.Debug.WriteLine($"SOL Balance: {solBalance}");
                        
                        return solBalance;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"HTTP Error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting SOL balance: {ex.Message}");
            }
            return null;
        }

        // Ottiene il prezzo SOL in EUR da CoinGecko
        
        public async Task<decimal?> GetSolPriceInEUR()
        {
            // Usa TokenSearchService per prezzo in tempo reale
            var tokenService = new TokenSearchService();
            return await tokenService.GetSolPriceRealtimeAsync();
        }

        // Verifica se un indirizzo Solana è valido
        
        public bool IsValidSolanaAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            if (address.Length < 32 || address.Length > 44)
                return false;

            const string base58Chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
            return address.All(c => base58Chars.Contains(c));
        }

        // Carica i wallet dal file JSON
        
        private List<WalletAccount> LoadWallets()
        {
            try
            {
                var encryption = ((App)System.Windows.Application.Current).GetEncryptionService();
                if (encryption == null)
                    return new List<WalletAccount>();
                
                var secureStorage = new SecureStorageService();
                var wallets = secureStorage.LoadWallets<List<WalletAccount>>(encryption);
                return wallets ?? new List<WalletAccount>();
            }
            catch
            {
                return new List<WalletAccount>();
            }
        }

        // Salva i wallet nel file JSON
        
        private void SaveWallets()
        {
            try
            {
                var encryption = ((App)System.Windows.Application.Current).GetEncryptionService();
                if (encryption == null)
                    return;
                
                var secureStorage = new SecureStorageService();
                secureStorage.SaveWallets(_wallets, encryption);
            }
            catch
            {
                // Ignora errori di salvataggio
            }
        }
    }
}