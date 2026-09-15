using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MarketCapCalculator.Models;

namespace MarketCapCalculator.Services
{
    
    // Servizio per la gestione della storia delle predizioni
    // Salva e carica le ultime 50 predizioni in un file JSON non criptato
    
    public class PredictionHistoryService
    {
        private const int MaxPredictions = 50;
        private readonly string _historyFilePath;
        private List<PredictionHistory> _predictions;

        public PredictionHistoryService()
        {
            // Salva nella cartella AppData dell'utente
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MarketCapCalculator");
            Directory.CreateDirectory(appFolder);
            _historyFilePath = Path.Combine(appFolder, "prediction_history.json");
            _predictions = LoadHistory();
        }

        
        // Aggiunge una nuova predizione alla storia
        
        public void AddPrediction(InvestmentCalculation calculation, string? tokenName = null, string? notes = null)
        {
            var prediction = new PredictionHistory
            {
                Id = _predictions.Any() ? _predictions.Max(p => p.Id) + 1 : 1,
                Timestamp = DateTime.Now,
                InvestmentAmount = calculation.InvestmentAmount,
                CurrentMarketCap = calculation.CurrentMarketCap,
                TargetMarketCap = calculation.TargetMarketCap,
                GrowthMultiplier = calculation.GrowthMultiplier,
                FutureValue = calculation.FutureValue,
                NetProfit = calculation.NetProfit,
                RoiPercentage = calculation.RoiPercentage,
                TokenName = tokenName,
                Notes = notes
            };

            _predictions.Add(prediction);
            
            // Mantieni solo le ultime 50 predizioni
            if (_predictions.Count > MaxPredictions)
            {
                _predictions = _predictions
                    .OrderByDescending(p => p.Timestamp)
                    .Take(MaxPredictions)
                    .OrderBy(p => p.Timestamp)
                    .ToList();
            }

            SaveHistory();
        }

        
        // Restituisce tutte le predizioni salvate
        
        public IEnumerable<PredictionHistory> GetPredictions()
        {
            return _predictions.OrderByDescending(p => p.Timestamp);
        }

        
        // Cancella tutta la storia
        
        public void ClearHistory()
        {
            _predictions.Clear();
            SaveHistory();
        }

        
        // Elimina una singola predizione
        
        public void DeletePrediction(int id)
        {
            var prediction = _predictions.FirstOrDefault(p => p.Id == id);
            if (prediction != null)
            {
                _predictions.Remove(prediction);
                SaveHistory();
            }
        }

        
        // Carica la storia dal file JSON
        private List<PredictionHistory> LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    return JsonSerializer.Deserialize<List<PredictionHistory>>(json) ?? new List<PredictionHistory>();
                }
            }
            catch (Exception)
            {
                // Se c'è un errore nel caricamento, inizia con una lista vuota
            }
            return new List<PredictionHistory>();
        }

        
        // Salva la storia nel file JSON
        private void SaveHistory()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(_predictions, options);
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception)
            {
                // Ignora errori di salvataggio
            }
        }
    }
}