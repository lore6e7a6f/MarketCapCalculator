using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MarketCapCalculator.Models;

namespace MarketCapCalculator.Services
{
    
    // Servizio per l'esportazione dei dati
    
    public class ExportService
    {
        
        // Esporta la storia predizioni in CSV
        
        public void ExportHistoryToCsv(IEnumerable<PredictionHistory> predictions, string filePath)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("Data;Token;MC Attuale;MC Target;Moltiplicatore;ROI;Profitto");

            foreach (var p in predictions)
            {
                sb.AppendLine($"{p.Timestamp:dd/MM/yyyy HH:mm};{p.TokenName};{p.DisplayMarketCap};{p.DisplayTarget};{p.GrowthMultiplier:F2}x;{p.RoiPercentage:F1}%;{p.NetProfit:F2}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        
        // Esporta i wallet in CSV
        
        public void ExportWalletsToCsv(IEnumerable<WalletAccount> wallets, string filePath)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("Nome;Indirizzo;Saldo SOL;Saldo EUR;Ultimo Aggiornamento");

            foreach (var w in wallets)
            {
                sb.AppendLine($"{w.DisplayName};{w.Address};{w.SolBalance?.ToString("F6") ?? "0"};{w.EurBalance?.ToString("F6") ?? "0"};{w.LastUpdated?.ToString("dd/MM/yyyy HH:mm") ?? "Mai"}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        
        // Esporta la storia predizioni in PDF semplice
        
        public void ExportHistoryToPdf(IEnumerable<PredictionHistory> predictions, string filePath)
        {
            var html = GenerateHistoryHtml(predictions);
            File.WriteAllText(filePath.Replace(".pdf", ".html"), html, Encoding.UTF8);

            // Apri il file HTML nel browser (l'utente può stamparlo come PDF)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath.Replace(".pdf", ".html"),
                UseShellExecute = true
            });
        }

        
        // Esporta i wallet in PDF semplice
        
        public void ExportWalletsToPdf(IEnumerable<WalletAccount> wallets, string filePath)
        {
            var html = GenerateWalletsHtml(wallets);
            File.WriteAllText(filePath.Replace(".pdf", ".html"), html, Encoding.UTF8);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath.Replace(".pdf", ".html"),
                UseShellExecute = true
            });
        }

        private string GenerateHistoryHtml(IEnumerable<PredictionHistory> predictions)
        {
            var rows = string.Join("\n", predictions.Select(p => $@"
                <tr>
                    <td>{p.Timestamp:dd/MM/yyyy HH:mm}</td>
                    <td>{p.TokenName}</td>
                    <td>{p.DisplayMarketCap}</td>
                    <td>{p.DisplayTarget}</td>
                    <td>{p.GrowthMultiplier:F2}x</td>
                    <td style='color: {(p.RoiPercentage >= 0 ? "#00C853" : "#FF5252")};'>{p.RoiPercentage:F1}%</td>
                    <td style='color: {(p.NetProfit >= 0 ? "#00C853" : "#FF5252")};'>€ {p.NetProfit:F2}</td>
                </tr>"));

            return $@"<!DOCTYPE html>
                        <html>
                        <head>
                            <meta charset='UTF-8'>
                            <title>Storia Predizioni - MarketCap Calculator</title>
                            <style>
                                body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #121212; color: #EAEAEA; padding: 20px; }}
                                h1 {{ color: #00C853; }}
                                table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
                                th {{ background: #2A2A2A; padding: 10px; text-align: left; }}
                                td {{ padding: 10px; border-bottom: 1px solid #2A2A2A; }}
                                tr:hover {{ background: #1A1A1A; }}
                            </style>
                        </head>
                        <body>
                            <h1>📊 Storia Predizioni</h1>
                            <p>Generato: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                            <table>
                                <tr>
                                    <th>Data</th>
                                    <th>Token</th>
                                    <th>MC Attuale</th>
                                    <th>MC Target</th>
                                    <th>Moltiplicatore</th>
                                    <th>ROI</th>
                                    <th>Profitto</th>
                                </tr>
                                {rows}
                            </table>
                        </body>
                        </html>";
        }

        private string GenerateWalletsHtml(IEnumerable<WalletAccount> wallets)
        {
            var rows = string.Join("\n", wallets.Select(w => $@"
                <tr>
                    <td>{w.DisplayName}</td>
                    <td>{w.DisplayAddress}</td>
                    <td>{w.SolBalance?.ToString("F6") ?? "0"} SOL</td>
                    <td style='color: #00C853;'>€ {w.EurBalance?.ToString("F6") ?? "0.000000"}</td>
                    <td>{w.LastUpdated?.ToString("dd/MM/yyyy HH:mm") ?? "Mai"}</td>
                </tr>"));

            return $@"<!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='UTF-8'>
                        <title>Wallet - MarketCap Calculator</title>
                        <style>
                            body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #121212; color: #EAEAEA; padding: 20px; }}
                            h1 {{ color: #4A90E2; }}
                            table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
                            th {{ background: #2A2A2A; padding: 10px; text-align: left; }}
                            td {{ padding: 10px; border-bottom: 1px solid #2A2A2A; }}
                            tr:hover {{ background: #1A1A1A; }}
                        </style>
                    </head>
                    <body>
                        <h1>👛 Wallet</h1>
                        <p>Generato: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                        <table>
                            <tr>
                                <th>Nome</th>
                                <th>Indirizzo</th>
                                <th>Saldo SOL</th>
                                <th>Saldo EUR</th>
                                <th>Aggiornato</th>
                            </tr>
                            {rows}
                        </table>
                    </body>
                    </html>";
        }
    }
}