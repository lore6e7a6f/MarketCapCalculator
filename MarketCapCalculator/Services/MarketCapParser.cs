using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MarketCapCalculator.Services
{
    
    // Parser per notazioni compatte di Market Cap
    // Supporta: K, M, B, T e separatori decimali sia . che ,
    
    public static class MarketCapParser
    {
        private static readonly Regex Pattern = new Regex(
            @"^\s*([\d.,]+)\s*([KMBT]?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static decimal Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Il valore non può essere vuoto");

            var cleanedInput = input.Trim().ToUpper();
            
            // Estrai il suffisso (K, M, B, T)
            var suffix = "";
            if (cleanedInput.EndsWith("K") || cleanedInput.EndsWith("M") || 
                cleanedInput.EndsWith("B") || cleanedInput.EndsWith("T"))
            {
                suffix = cleanedInput[^1].ToString();
                cleanedInput = cleanedInput[..^1].Trim();
            }
            
            // Normalizza il numero
            var normalizedNumber = NormalizeNumber(cleanedInput);
            
            if (!decimal.TryParse(normalizedNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                throw new ArgumentException($"Numero non valido: '{cleanedInput}'");

            // Applica il moltiplicatore
            var multiplier = suffix switch
            {
                "" => 1m,
                "K" => 1_000m,
                "M" => 1_000_000m,
                "B" => 1_000_000_000m,
                "T" => 1_000_000_000_000m,
                _ => 1m
            };

            return number * multiplier;
        }

        
        // Normalizza un numero gestendo sia . che , come separatore decimale
        private static string NormalizeNumber(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "0";

            input = input.Trim();
            
            // Se contiene entrambi . e , allora . è separatore migliaia e , è decimale
            if (input.Contains(".") && input.Contains(","))
            {
                // Rimuovi i punti (migliaia) e converti virgola in punto
                return input.Replace(".", "").Replace(",", ".");
            }
            
            // Se contiene solo virgole
            if (input.Contains(","))
            {
                // Se ci sono più virgole, la prima è decimale e le altre sono migliaia
                var parts = input.Split(',');
                if (parts.Length > 2)
                {
                    // Molte virgole: usa la prima come decimale
                    return parts[0] + "." + string.Join("", parts.Skip(1));
                }
                // Una sola virgola: è il separatore decimale
                return input.Replace(",", ".");
            }
            
            // Se contiene solo punti
            if (input.Contains("."))
            {
                var parts = input.Split('.');
                if (parts.Length > 2)
                {
                    // Molti punti: usa l'ultimo come decimale
                    var lastPart = parts[^1];
                    var intPart = string.Join("", parts[..^1]);
                    return intPart + "." + lastPart;
                }
                // Un solo punto: va bene così
                return input;
            }
            
            // Nessun separatore: numero intero
            return input;
        }

        
        // Formatta un valore in notazione compatta con virgola per decimali
        
        public static string FormatCompact(decimal value)
        {
            if (value >= 1_000_000_000_000m)
                return FormatWithComma(value / 1_000_000_000_000m) + "T";
            if (value >= 1_000_000_000m)
                return FormatWithComma(value / 1_000_000_000m) + "B";
            if (value >= 1_000_000m)
                return FormatWithComma(value / 1_000_000m) + "M";
            if (value >= 1_000m)
                return FormatWithComma(value / 1_000m) + "K";
            return FormatWithComma(value);
        }

        
        // Formatta un numero con virgola come separatore decimale
        
        private static string FormatWithComma(decimal value)
        {
            // Arrotonda a 2 decimali
            value = Math.Round(value, 2);
            
            // Formatta con punto e poi sostituisci con virgola
            var formatted = value.ToString("0.##", CultureInfo.InvariantCulture);
            return formatted.Replace('.', ',');
        }

        
        // Formatta in formato completo con virgola
        
        public static string FormatFull(decimal value)
        {
            return value.ToString("N2", CultureInfo.GetCultureInfo("it-IT"));
        }

        public static bool TryParse(string input, out decimal result)
        {
            result = 0;
            try
            {
                result = Parse(input);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}